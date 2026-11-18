<#
.SYNOPSIS
    DentalCare V3 — Run Full Clinical Journey & Launch Program
.DESCRIPTION
    1. Verifies SQL Server and database DentalCare_SWP391_DEMO_V2_TEST.
    2. Ensures Backend API (https://localhost:7350) and Frontend Web (https://localhost:7129) are running.
    3. Executes the complete 16-Stage End-to-End Clinical Flow with Playwright:
       - 4 X-ray modalities: Panorama, Bitewing, Cephalo, Periapical
       - Odontogram 32 teeth (FDI) & Clinical Entries
       - Chair reassignment, Shift handover (Option A / Option B)
       - Leave request & R003 coverage guard
       - Receptionist dispatch, billing, and Patient portal
    4. Generates updated Word documents with all screenshots embedded.
    5. Keeps the program running live for interactive usage.
#>

[CmdletBinding()]
param(
    [string]$SqlServerInstance = ".\MSSQLSERVER01",
    [string]$TargetDatabase = "DentalCare_SWP391_DEMO_V2_TEST",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " DENTALCARE V3 — FULL CLINICAL WORKSPACE & X-RAY JOURNEY RUNNER" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# 1. Check SQL Server
Write-Host "`n[1/4] Checking SQL Server database [$TargetDatabase]..." -ForegroundColor Yellow
$checkDbSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';"
$tableCount = sqlcmd -S $SqlServerInstance -d $TargetDatabase -h -1 -W -Q $checkDbSql
if ($LASTEXITCODE -ne 0 -or [int]$tableCount -lt 30) {
    Write-Host "  -> Database needs rebuild. Running Rebuild_DentalCare_DEMO_V2.ps1..." -ForegroundColor Yellow
    powershell -ExecutionPolicy Bypass -File "$RepoRoot\DB\Rebuild_DentalCare_DEMO_V2.ps1"
} else {
    Write-Host "  -> Database [$TargetDatabase] is healthy with $tableCount canonical tables." -ForegroundColor Green
}

# 2. Check / Start Services
Write-Host "`n[2/4] Checking backend & frontend services..." -ForegroundColor Yellow
$beRunning = $false
$feRunning = $false

try {
    $beTest = curl.exe -k -s -m 3 https://localhost:7350/api/Booking/departments
    if ($beTest -like "*success*") { $beRunning = $true }
} catch {}

try {
    $feTest = curl.exe -k -s -I -m 3 https://localhost:7129/
    if ($feTest -like "*HTTP/1.1 200*") { $feRunning = $true }
} catch {}

if (!$beRunning) {
    Write-Host "  -> Starting Backend API (https://localhost:7350)..." -ForegroundColor Yellow
    Start-Process -FilePath "dotnet" -ArgumentList "run --launch-profile https" -WorkingDirectory "$RepoRoot\BE\DentalClinic.Api" -WindowStyle Minimized
    Start-Sleep -Seconds 4
} else {
    Write-Host "  -> Backend API is running on https://localhost:7350 (HTTP 200)." -ForegroundColor Green
}

if (!$feRunning) {
    Write-Host "  -> Starting Frontend Web (https://localhost:7129)..." -ForegroundColor Yellow
    Start-Process -FilePath "dotnet" -ArgumentList "run --launch-profile https" -WorkingDirectory "$RepoRoot\FE\DentalClinic.Web" -WindowStyle Minimized
    Start-Sleep -Seconds 4
} else {
    Write-Host "  -> Frontend Web is running on https://localhost:7129 (HTTP 200)." -ForegroundColor Green
}

# 3. Execute Full 16-Stage Clinical Flow
if (!$SkipTests) {
    Write-Host "`n[3/4] Executing full 16-Stage Clinical Journey with Playwright..." -ForegroundColor Yellow
    node "$RepoRoot\e2e\run_dentist_workspace_v3_e2e_flow.js"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  -> All 16 stages executed & evidence captured successfully!" -ForegroundColor Green
    } else {
        Write-Host "  -> Warning: Some test stages reported an issue." -ForegroundColor Yellow
    }

    Write-Host "`n[4/4] Updating Word documentation catalogs..." -ForegroundColor Yellow
    python "$RepoRoot\scripts\build_e2e_flow_doc.py"
    python "$RepoRoot\scripts\build_dentist_features_doc.py"
}

Write-Host "`n======================================================================" -ForegroundColor Green
Write-Host " HỆ THỐNG DENTALCARE V3 ĐANG HOẠT ĐỘNG SẴN SÀNG (LIVE & READY)!" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host " 1. Frontend Web Portal   : https://localhost:7129" -ForegroundColor Cyan
Write-Host " 2. Backend API Docs      : https://localhost:7350" -ForegroundColor Cyan
Write-Host "----------------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host " DANH SÁCH TÀI KHOẢN TRẢI NGHIỆM (Mật khẩu chung: DentalCare@2026!):" -ForegroundColor Yellow
Write-Host "   • Bệnh nhân (Patient)      : patient.demo22@dentalcare.test (Đinh Minh Phúc)" -ForegroundColor White
Write-Host "   • Lễ tân (Receptionist)    : reception.demo01@dentalcare.test (Nguyễn Thu Hương)" -ForegroundColor White
Write-Host "   • Bác sĩ Tổng quát (R101)  : dentist.gen02@dentalcare.test (BS. Nguyễn Hoài Phương)" -ForegroundColor White
Write-Host "   • Bác sĩ Chỉnh nha (R201)  : dentist.ort01@dentalcare.test (BS. Trần Thanh Huyền)" -ForegroundColor White
Write-Host "   • Bác sĩ X-quang ca sáng   : dentist.xray01@dentalcare.test (BS. Đặng Minh Khôi)" -ForegroundColor White
Write-Host "   • Bác sĩ X-quang ca chiều  : dentist.xray02@dentalcare.test (BS. Nguyễn Thu Lan)" -ForegroundColor White
Write-Host "   • Quản trị viên (Admin)    : admin.demo@dentalcare.test" -ForegroundColor White
Write-Host "======================================================================" -ForegroundColor Green
