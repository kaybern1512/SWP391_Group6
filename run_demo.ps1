<#
.SYNOPSIS
    DentalCare SWP391 — Production Demo Environment Launcher
.DESCRIPTION
    Safely starts the isolated DentalCare demo environment on localhost.
    - Validates SQL Server and DentalCare_SWP391_UAT_Fixture_TEST.
    - Enforces strict safety boundary (Never touches DentalCare_SWP391 or DentalCare_SWP391_Refactor_UAT).
    - Starts Backend API (7350) and Frontend MVC (7129).
    - Verifies HTTP 200 health on both services.
    - Prints demo portal URLs and role account information.
#>

[CmdletBinding()]
param(
    [string]$SqlServerInstance = ".\SQLEXPRESS",
    [string]$TargetDatabase = "DentalCare_SWP391_DEMO_V2_TEST",
    [switch]$SkipBrowserLaunch
)

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " DENTALCARE SWP391 — PRODUCTION DEMO ENVIRONMENT LAUNCHER" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# --------------------------------------------------------------------
# 1. STRICT SAFETY GATES: Database Protection
# --------------------------------------------------------------------
$ProtectedDatabases = @("DentalCare_SWP391", "DentalCare_SWP391_Refactor_UAT", "DentalCare_SWP391_UAT_Fixture_TEST")
if ($ProtectedDatabases -contains $TargetDatabase) {
    Write-Host "[FATAL SAFETY VIOLATION] TargetDatabase '$TargetDatabase' is a PROTECTED database." -ForegroundColor Red
    Write-Host "The demo launcher is strictly prohibited from running against protected databases." -ForegroundColor Red
    Exit 1
}

Write-Host "--> Checking SQL Server instance ($SqlServerInstance)..." -ForegroundColor Yellow
try {
    $testConn = New-Object System.Data.SqlClient.SqlConnection("Server=$SqlServerInstance;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5")
    $testConn.Open()
    $cmd = $testConn.CreateCommand()
    $cmd.CommandText = "SELECT database_id FROM sys.databases WHERE name = '$TargetDatabase'"
    $dbId = $cmd.ExecuteScalar()
    $testConn.Close()

    if ($null -eq $dbId) {
        Write-Host "[ERROR] Database '$TargetDatabase' does not exist on $SqlServerInstance!" -ForegroundColor Red
        Write-Host "Please ensure the demo fixture database is created before starting the demo." -ForegroundColor Red
        Exit 1
    }
    Write-Host "  [OK] Connected to $SqlServerInstance. Fixture DB '$TargetDatabase' verified (ID: $dbId)." -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Could not connect to SQL Server at ${SqlServerInstance}: $($_.Exception.Message)" -ForegroundColor Red
    Exit 1
}

# --------------------------------------------------------------------
# 2. Release Previous Port Locks
# --------------------------------------------------------------------
Write-Host "--> Clearing any existing processes on ports 7350 and 7129..." -ForegroundColor Yellow
$ports = @(7350, 7129, 5350, 5129)
foreach ($p in $ports) {
    $conns = Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue
    foreach ($c in $conns) {
        try {
            Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue
            Write-Host "  Killed existing process $($c.OwningProcess) on port $p" -ForegroundColor DarkGray
        } catch {}
    }
}
Start-Sleep -Seconds 1

# --------------------------------------------------------------------
# 3. Launch Backend API on Port 7350
# --------------------------------------------------------------------
$beDir = Join-Path $RepoRoot "BE\DentalClinic.Api"
if (-not (Test-Path $beDir)) {
    Write-Host "[ERROR] Backend directory not found: $beDir" -ForegroundColor Red
    Exit 1
}

Write-Host "--> Starting Backend API on port 7350 (Isolated DB: $TargetDatabase)..." -ForegroundColor Cyan
$beConnStr = "Server=$SqlServerInstance;Database=$TargetDatabase;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
$env:ConnectionStrings__DentalClinicDatabase = $beConnStr
$beProc = Start-Process -FilePath "dotnet" `
    -ArgumentList "run --launch-profile https --no-build --no-restore" `
    -WorkingDirectory $beDir `
    -PassThru `
    -WindowStyle Hidden

# --------------------------------------------------------------------
# 4. Launch Frontend Web on Port 7129
# --------------------------------------------------------------------
$feDir = Join-Path $RepoRoot "FE\DentalClinic.Web"
if (-not (Test-Path $feDir)) {
    Write-Host "[ERROR] Frontend directory not found: $feDir" -ForegroundColor Red
    Exit 1
}

Write-Host "--> Starting Frontend Web on port 7129..." -ForegroundColor Cyan
$feProc = Start-Process -FilePath "dotnet" `
    -ArgumentList "run --launch-profile https --no-build --no-restore" `
    -WorkingDirectory $feDir `
    -PassThru `
    -WindowStyle Hidden

# --------------------------------------------------------------------
# 5. Verify Health Endpoints (HTTP 200)
# --------------------------------------------------------------------
Write-Host "--> Waiting for services to initialize and respond..." -ForegroundColor Yellow
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

$maxWaitSeconds = 40
$beHealthy = $false
$feHealthy = $false

for ($i = 1; $i -le $maxWaitSeconds; $i++) {
    Start-Sleep -Seconds 1

    if (-not $beHealthy) {
        try {
            $req = [System.Net.WebRequest]::Create("https://localhost:7350/swagger/v1/swagger.json")
            $req.Timeout = 2000
            $res = $req.GetResponse()
            if ($res.StatusCode -eq 200) {
                $beHealthy = $true
                Write-Host "  [OK] Backend API healthy on https://localhost:7350 (Checked in ${i}s)" -ForegroundColor Green
            }
            $res.Close()
        } catch {}
    }

    if (-not $feHealthy) {
        try {
            $req = [System.Net.WebRequest]::Create("https://localhost:7129/")
            $req.Timeout = 2000
            $res = $req.GetResponse()
            if ($res.StatusCode -eq 200) {
                $feHealthy = $true
                Write-Host "  [OK] Frontend Web healthy on https://localhost:7129 (Checked in ${i}s)" -ForegroundColor Green
            }
            $res.Close()
        } catch {}
    }

    if ($beHealthy -and $feHealthy) { break }
}

if (-not ($beHealthy -and $feHealthy)) {
    Write-Host "[ERROR] One or more services failed to initialize within $maxWaitSeconds seconds." -ForegroundColor Red
    Write-Host "Backend Status: $(if ($beHealthy) {'UP'} else {'DOWN'})" -ForegroundColor Red
    Write-Host "Frontend Status: $(if ($feHealthy) {'UP'} else {'DOWN'})" -ForegroundColor Red
    Exit 1
}

# --------------------------------------------------------------------
# 6. Display Demo Information
# --------------------------------------------------------------------
Write-Host "`n======================================================================" -ForegroundColor Green
Write-Host " DENTALCARE SWP391 IS LIVE AND READY FOR DEMO!" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host " Frontend Web Application : https://localhost:7129" -ForegroundColor White
Write-Host " Backend Swagger API Docs  : https://localhost:7350/swagger/index.html" -ForegroundColor White
Write-Host " Target Database           : $TargetDatabase ($SqlServerInstance)" -ForegroundColor White
Write-Host "----------------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host " DEMO ACCOUNTS (Credentials set via `$env:DEMO_PASSWORD or defaults):" -ForegroundColor Yellow
Write-Host "   1. Patient          : patient@dentalcare.test" -ForegroundColor Cyan
Write-Host "   2. Receptionist     : receptionist@dentalcare.test" -ForegroundColor Cyan
Write-Host "   3. Treating Dentist : dentist1@dentalcare.test (BS. Le Van Bac Si Mot)" -ForegroundColor Cyan
Write-Host "   4. X-ray Dentist    : dentist.hung@dentalcare.test (BS. Tran Hung)" -ForegroundColor Cyan
Write-Host "   5. System Admin     : admin@dentalcare.test" -ForegroundColor Cyan
Write-Host "----------------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host " To stop all demo daemons safely: .\stop_demo.ps1" -ForegroundColor Yellow
Write-Host "======================================================================`n" -ForegroundColor Green

if (-not $SkipBrowserLaunch) {
    try {
        Start-Process "https://localhost:7129"
    } catch {}
}
