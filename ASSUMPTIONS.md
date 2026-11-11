# Engineering Assumptions and Architectural Decisions

This document records the engineering interpretations, trade-offs, and design decisions made for the School Admission Management System.

---

## 1. Concurrency Control and Seat Overbooking Invariant

- **Unclear / Open Requirement:** The specification allows choosing between calculating seat counts dynamically from hold records or maintaining transactional counters.
- **Chosen Interpretation:**
  1. Primary concurrency control uses **PostgreSQL row-level locking (`SELECT ... FOR UPDATE`)** on the target class record during seat allocation transactions.
  2. Active held seats and confirmed seats are queried and validated atomically inside the locked transaction.
  3. Real-time availability queries calculate seats dynamically from hold records using the invariant:
     `Available Seats = Total Seats - Held Seats - Confirmed Seats`.
- **Reason:**
  Calculating dynamically from hold records guarantees that seat counts never drift from actual database records, even after abrupt crashes or background worker latency.
  Row-level locking on the class record serializes hold requests per class, completely eliminating race conditions even across multiple backend instances sharing the database.
- **Effect on System Behavior:**
  Under high concurrent load (e.g., 100 simultaneous requests for 1 remaining seat), exactly one request secures the seat, while the other 99 receive `409 Conflict`. Zero overbooking is mathematically guaranteed.

---

## 2. Student Identity and Authentication

- **Requirement:** No authentication or login system is required. Student identity is provided via a 17-digit birth registration number in the `X-Student-Id` header.
- **Chosen Interpretation:**
  - The student ID is strictly validated as a string of exactly 17 decimal digits (`^\d{17}$`).
  - Leading zeroes are preserved.
  - The `X-Student-Id` header is required for all hold-related endpoints (`POST /classes/{id}/holds`, `POST /holds/{id}/confirm`, `DELETE /holds/{id}`, and `GET /me/holds`).
  - Missing or malformed headers return `400 Bad Request` with an explanatory message.
- **Security Note:**
  As noted in the specification, the `X-Student-Id` header is an identification mechanism for this take-home exercise and does not represent cryptographic authentication. In production, a secure token (such as JWT/OAuth2) would authenticate the caller.

---

## 3. Repeated / Idempotent Confirmations

- **Unclear / Open Requirement:** "Document any reasonable interpretation needed for repeated confirmation response behavior."
- **Chosen Interpretation:**
  If a student calls `POST /holds/{holdId}/confirm` on a hold that is already in `CONFIRMED` status (and belongs to that student), the endpoint returns `200 OK` with `{"holdId": holdId, "status": "CONFIRMED"}`.
  It does not re-increment confirmed seats or create duplicate allocations.
- **Reason:**
  Network retries or accidental double-clicks by the client should be safe and idempotent, returning success without altering the database state.

---

## 4. Cancellation Behavior

- **Unclear / Open Requirement:** "Repeated cancellation, cancellation of an expired hold, and cancellation of a confirmed admission must be handled according to a clearly documented and consistent policy."
- **Chosen Policy:**
  1. **Active Hold (`HELD`):** Successfully transitioned to `CANCELLED`, releasing the held seat. Returns `204 No Content`.
  2. **Already Cancelled Hold (`CANCELLED`):** DELETE is an idempotent HTTP verb; if the hold was already cancelled by this student, the endpoint returns `204 No Content`.
  3. **Expired Hold (`EXPIRED` or past expiration timestamp):** Cannot be cancelled because the hold was already expired and its seat was already released. Returns `400 Bad Request` with message: `"Cannot cancel an expired hold."`
  4. **Confirmed Admission (`CONFIRMED`):** Cannot be cancelled via the student self-service cancellation API. Once an admission fee is paid and confirmed, school policy requires administrative intervention. Returns `400 Bad Request` with message: `"Cannot cancel a confirmed admission."`
  5. **Non-Owner Cancellation:** If a student attempts to cancel another student's hold, the endpoint returns `403 Forbidden`.

---

## 5. Duplicate Active Application Handling

- **Requirement:** "A student must not have more than one active application for the same class at the same time. Active means: A currently valid HELD application or a CONFIRMED admission. Expired and cancelled applications must not permanently prevent a new application."
- **Chosen Interpretation:**
  - Before allocating a new hold, the backend checks if the student has any existing hold for that class with `Status = 'CONFIRMED'` or (`Status = 'HELD'` AND `ExpiresAt > now`).
  - If an active application exists, the backend rejects the request with `409 Conflict` (with a clear message: `"Student already has an active application or confirmed admission for this class."`).
  - A PostgreSQL partial unique index is also enforced on `("StudentId", "ClassId") WHERE "Status" = 'CONFIRMED'` to guarantee database-level uniqueness for confirmed seats.
  - Expired and cancelled holds do not count as active, allowing the student to re-apply if a previous hold lapsed or was cancelled.

---

## 6. Real-Time vs Background Expiration

- **Requirement:** Automatic hold expiration after `HOLD_DURATION_SECONDS` (default: 600s).
- **Chosen Interpretation:**
  - Dual-layer expiration strategy:
    1. **Immediate In-Query Logic:** All queries (seat calculations, hold queries, confirmation checks) evaluate `ExpiresAtUtc <= DateTime.UtcNow`. If a hold has passed its expiration time, it is immediately treated as expired, even if the background worker has not updated the database row yet.
    2. **Background Sweeper Service:** An ASP.NET Core `BackgroundService` runs periodically (every `SWEEP_INTERVAL_SECONDS`, default 5 seconds) to transition lapsed `HELD` rows to `EXPIRED` in the database.
- **Reason:**
  Eliminates race conditions where an expired hold might be confirmed or counted as held during the brief interval between expiration and the background service tick.

---

## 7. Class Name and Identifier Conventions

- **Requirement:** Class names are strings; IDs follow prefixes like `c_123` and holds follow `h_789`.
- **Chosen Interpretation:**
  - Class IDs are generated with prefix `c_` followed by an 8-character hex string (e.g., `c_6a1f8b2c`).
  - Hold IDs are generated with prefix `h_` followed by an 8-character hex string (e.g., `h_9c4e2a1d`).
  - Class names must be unique and trimmed. Creating a class with an existing name returns `409 Conflict`.
  - Seat capacity must be greater than zero. Non-positive numbers or invalid payloads return `400 Bad Request`.

