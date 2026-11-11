# API Verification & Concurrency Smoke Test Script
# Usage: powershell -ExecutionPolicy Bypass -File scripts/verify-api.ps1 [-BaseUrl http://localhost:8080]

param (
    [string]$BaseUrl = "http://localhost:8080",
    [string]$BaseUrl2 = "http://localhost:8081"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " School Admission Management System - Live Verification " -ForegroundColor Cyan
Write-Host " Target 1: $BaseUrl" -ForegroundColor Cyan
Write-Host " Target 2: $BaseUrl2" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Step 1: Health Check
Write-Host "`n[1/7] Testing Health Checks..." -ForegroundColor Yellow
try {
    $health1 = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get
    Write-Host "  [OK] Primary Backend Health: $health1" -ForegroundColor Green
} catch {
    Write-Host "  [FAIL] Could not connect to $BaseUrl/health: $_" -ForegroundColor Red
    exit 1
}

# Step 2: Create Class
Write-Host "`n[2/7] Creating Test Class via POST /classes..." -ForegroundColor Yellow
$classPayload = @{
    name = "E2E Verification Class $(Get-Random)"
    totalSeats = 25
} | ConvertTo-Json

$createdClass = Invoke-RestMethod -Uri "$BaseUrl/classes" -Method Post -Body $classPayload -ContentType "application/json"
Write-Host "  [OK] Created Class ID: $($createdClass.id), Name: '$($createdClass.name)', Seats: $($createdClass.totalSeats)" -ForegroundColor Green

# Step 3: List Classes
Write-Host "`n[3/7] Fetching Class List via GET /classes..." -ForegroundColor Yellow
$classes = Invoke-RestMethod -Uri "$BaseUrl/classes" -Method Get
$matched = $classes | Where-Object { $_.id -eq $createdClass.id }
Write-Host "  [OK] Total: $($matched.totalSeats), Held: $($matched.heldSeats), Confirmed: $($matched.confirmedSeats), Available: $($matched.availableSeats)" -ForegroundColor Green

# Step 4: Create Hold
Write-Host "`n[4/7] Reserving Seat via POST /classes/{id}/holds..." -ForegroundColor Yellow
$testStudentId = "20152690000000001"
$hold = Invoke-RestMethod -Uri "$BaseUrl/classes/$($createdClass.id)/holds" -Method Post -Headers @{ "X-Student-Id" = $testStudentId }
Write-Host "  [OK] Hold Created: ID: $($hold.holdId), Status: $($hold.status), ExpiresAt: $($hold.expiresAt)" -ForegroundColor Green

# Step 5: Confirm Hold
Write-Host "`n[5/7] Confirming Hold via POST /holds/{id}/confirm..." -ForegroundColor Yellow
$confirmed = Invoke-RestMethod -Uri "$BaseUrl/holds/$($hold.holdId)/confirm" -Method Post -Headers @{ "X-Student-Id" = $testStudentId }
Write-Host "  [OK] Hold Confirmed: Status = $($confirmed.status)" -ForegroundColor Green

# Verify seat counts
$classAfterConfirm = Invoke-RestMethod -Uri "$BaseUrl/classes/$($createdClass.id)" -Method Get
Write-Host "  [OK] Updated Seats -> Total: $($classAfterConfirm.totalSeats), Confirmed: $($classAfterConfirm.confirmedSeats), Available: $($classAfterConfirm.availableSeats)" -ForegroundColor Green

# Step 6: Query My Holds
Write-Host "`n[6/7] Fetching Student Applications via GET /me/holds..." -ForegroundColor Yellow
$myHolds = Invoke-RestMethod -Uri "$BaseUrl/me/holds" -Method Get -Headers @{ "X-Student-Id" = $testStudentId }
Write-Host "  [OK] Found $($myHolds.Count) application(s) for student $testStudentId" -ForegroundColor Green

# Step 7: Live Concurrency Smoke Test across both instances
Write-Host "`n[7/7] Executing Live Concurrency Test (20 simultaneous requests competing for 3 seats)..." -ForegroundColor Yellow
$concClassPayload = @{
    name = "Live Concurrency Test Class $(Get-Random)"
    totalSeats = 3
} | ConvertTo-Json
$concClass = Invoke-RestMethod -Uri "$BaseUrl/classes" -Method Post -Body $concClassPayload -ContentType "application/json"

$jobs = @()
1..20 | ForEach-Object {
    $i = $_
    $studentId = "202699990000000$($i.ToString('00'))"
    # alternate between instance 1 and instance 2
    $targetUrl = if ($i % 2 -eq 0 -and $BaseUrl2) { $BaseUrl2 } else { $BaseUrl }

    $scriptBlock = {
        param($url, $cid, $sid)
        try {
            $resp = Invoke-WebRequest -Uri "$url/classes/$cid/holds" -Method Post -Headers @{ "X-Student-Id" = $sid } -UseBasicParsing
            return [pscustomobject]@{ StatusCode = $resp.StatusCode; StudentId = $sid }
        } catch {
            return [pscustomobject]@{ StatusCode = $_.Exception.Response.StatusCode.value__; StudentId = $sid }
        }
    }

    $jobs += Start-Job -ScriptBlock $scriptBlock -ArgumentList $targetUrl, $concClass.id, $studentId
}

$results = $jobs | Wait-Job | Receive-Job
$jobs | Remove-Job

$successCount = ($results | Where-Object { $_.StatusCode -eq 201 }).Count
$conflictCount = ($results | Where-Object { $_.StatusCode -eq 409 }).Count

Write-Host "  -> Successful Holds (201 Created): $successCount (Expected: 3)" -ForegroundColor Green
Write-Host "  -> Rejected Holds (409 Conflict): $conflictCount (Expected: 17)" -ForegroundColor Green

if ($successCount -eq 3 -and $conflictCount -eq 17) {
    Write-Host "`n>>> ALL VERIFICATION CHECKS AND CONCURRENCY TESTS PASSED SUCCESSFULLY! <<<" -ForegroundColor Green
} else {
    Write-Host "`n>>> Concurrency check finished with unexpected counts (Success: $successCount, Conflict: $conflictCount) <<<" -ForegroundColor Red
}

