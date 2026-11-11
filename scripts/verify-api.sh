#!/usr/bin/env bash
# API Verification & Concurrency Smoke Test Script for Linux / macOS
# Usage: ./scripts/verify-api.sh [http://localhost:8080] [http://localhost:8081]

BASE_URL="${1:-http://localhost:8080}"
BASE_URL2="${2:-http://localhost:8081}"

echo "=========================================================="
echo " School Admission Management System - Live Verification "
echo " Primary: $BASE_URL | Secondary: $BASE_URL2"
echo "=========================================================="

# 1. Health check
echo -e "\n[1/7] Testing Health Checks..."
curl -s -f "$BASE_URL/health" || { echo "Health check failed at $BASE_URL"; exit 1; }
echo " -> Health check passed"

# 2. Create Class
echo -e "\n[2/7] Creating Test Class..."
RANDOM_ID=$((1000 + RANDOM % 9000))
CLASS_RES=$(curl -s -X POST "$BASE_URL/classes" \
  -H "Content-Type: application/json" \
  -d "{\"name\": \"Bash E2E Class $RANDOM_ID\", \"totalSeats\": 20}")
CLASS_ID=$(echo "$CLASS_RES" | grep -o '"id":"[^"]*' | cut -d'"' -f4)
echo " -> Created Class ID: $CLASS_ID"

# 3. Create Hold
echo -e "\n[3/7] Reserving Seat..."
TEST_STUDENT="20152691111111111"
HOLD_RES=$(curl -s -X POST "$BASE_URL/classes/$CLASS_ID/holds" \
  -H "X-Student-Id: $TEST_STUDENT")
HOLD_ID=$(echo "$HOLD_RES" | grep -o '"holdId":"[^"]*' | cut -d'"' -f4)
echo " -> Created Hold ID: $HOLD_ID"

# 4. Confirm Hold
echo -e "\n[4/7] Confirming Hold..."
CONFIRM_RES=$(curl -s -X POST "$BASE_URL/holds/$HOLD_ID/confirm" \
  -H "X-Student-Id: $TEST_STUDENT")
echo " -> Confirmation response: $CONFIRM_RES"

# 5. Concurrency check
echo -e "\n[5/7] Running Concurrency Check (20 requests for 3 seats)..."
CONC_CLASS_RES=$(curl -s -X POST "$BASE_URL/classes" \
  -H "Content-Type: application/json" \
  -d "{\"name\": \"Bash Conc Class $RANDOM_ID\", \"totalSeats\": 3}")
CONC_CLASS_ID=$(echo "$CONC_CLASS_RES" | grep -o '"id":"[^"]*' | cut -d'"' -f4)

TMP_DIR=$(mktemp -d)
for i in $(seq 1 20); do
  SID=$(printf "202688880000000%02d" $i)
  TARGET=$BASE_URL
  if [ $((i % 2)) -eq 0 ] && [ -n "$BASE_URL2" ]; then
    TARGET=$BASE_URL2
  fi
  (
    CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$TARGET/classes/$CONC_CLASS_ID/holds" -H "X-Student-Id: $SID")
    echo "$CODE" >> "$TMP_DIR/results.txt"
  ) &
done
wait

SUCCESS=$(grep -c "201" "$TMP_DIR/results.txt" || true)
CONFLICT=$(grep -c "409" "$TMP_DIR/results.txt" || true)
rm -rf "$TMP_DIR"

echo " -> Successful holds (201): $SUCCESS (Expected: 3)"
echo " -> Rejected holds (409): $CONFLICT (Expected: 17)"

if [ "$SUCCESS" -eq 3 ] && [ "$CONFLICT" -eq 17 ]; then
  echo -e "\n>>> ALL VERIFICATION CHECKS PASSED SUCCESSFULLY! <<<"
else
  echo -e "\n>>> Warning: Concurrency test had unexpected counts. <<<"
fi

