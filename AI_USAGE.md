# AI Usage Documentation

This document transparently outlines the usage of AI assistance during the architectural design, implementation, and verification of the School Admission Management System.

---

## 1. AI Tools Utilized
- **AI Coding Assistant:** Google Antigravity (powered by Gemini models).
- **Primary Roles:** Senior Software Architect, Senior .NET Backend Engineer, PostgreSQL Database Engineer, Senior React Frontend Engineer, DevOps Engineer, QA Automation Engineer, and Technical Documentation Specialist.

---

## 2. Scope of AI Assistance
AI was used across the development lifecycle for:
1. **Requirements Decomposition & Traceability Checklist:** Parsing the comprehensive prompt into discrete functional, non-functional, and API criteria.
2. **Architecture and Data Model Design:** Drafting PostgreSQL entity definitions, index designs (including partial unique indexes for confirmed admissions), and formulating the row-level locking strategy (`SELECT ... FOR UPDATE`) to prevent race conditions.
3. **Backend Implementation:**
   - Authoring the ASP.NET Core Web API controllers, models, and EF Core DbContext.
   - Crafting the `HoldExpirationWorker` background service.
   - Formulating error handling, RFC 7807 problem details, and CORS configuration.
4. **Frontend Implementation:**
   - Building the React 18 / TypeScript application with Vite and Tailwind CSS.
   - Creating custom hooks for live countdowns, student ID persistence (`localStorage`), and polling class seat counts.
   - Designing responsive UI components with clean visual feedback.
5. **Testing & QA:**
   - Designing integration tests with `WebApplicationFactory`.
   - Authoring the concurrent hold creation test simulating simultaneous requests targeting a limited seat capacity.
   - Setting up Vitest unit tests for frontend countdown and validation logic.
6. **Docker & DevOps:**
   - Crafting multi-stage Dockerfiles for the .NET backend and Vite React frontend.
   - Designing `docker-compose.yml` with health checks, persistent PostgreSQL volumes, and multi-backend instance configuration.

---

## 3. AI-Generated Suggestions, Discovered Mistakes & Corrections

During development, several AI-generated proposals were reviewed and critically corrected:

1. **In-Memory Concurrency Trap:**
   - *Initial AI Suggestion:* Use a `SemaphoreSlim` or `lock` block in the C# service layer to synchronize hold creations.
   - *Critique & Correction:* In-memory locks fail immediately when scaling to multiple backend instances (Bonus Requirement 1). The design was corrected to enforce database-level transactional concurrency control using PostgreSQL's `SELECT ... FOR UPDATE` on the target class row.
2. **Seat Count Drift Risk:**
   - *Initial AI Suggestion:* Store mutable counters `held_seats` and `confirmed_seats` directly on the `classes` table and increment/decrement them via triggers or application code.
   - *Critique & Correction:* Maintaining redundant counters introduces drift if transactions fail partially or background sweeps crash. The design was corrected to derive seat counts dynamically from the immutable status of the `holds` table, ensuring 100% mathematical consistency at all times.
3. **Expired Hold Confirmation Race:**
   - *Initial AI Suggestion:* Rely solely on the background worker to update `Status = 'EXPIRED'` before rejecting confirmation.
   - *Critique & Correction:* If a student submits a confirmation request in the few seconds between a hold expiring and the background worker running its next sweep, an expired hold could be confirmed. The design was corrected to add an immediate gate: all operations check `hold.ExpiresAtUtc <= DateTime.UtcNow` and immediately treat it as expired.
4. **Student ID Type Handling:**
   - *Initial AI Suggestion:* Store student IDs as `long` or `BigInteger`.
   - *Critique & Correction:* Storing 17-digit birth registration numbers as numeric types loses leading zeroes. The design was corrected to strictly treat and store student IDs as 17-character strings (`VARCHAR(17)`).

