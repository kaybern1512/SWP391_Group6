<#
.SYNOPSIS
    DentalCare SWP391 Automated Test Synchronization and Verification Runner.
    Ensures zero locked DLL errors, builds cleanly, and executes both BE & FE test suites.
#>
[CmdletBinding()]
param(
    [switch]$SkipStopProcs = $false,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
if (Get-Variable -Name "PSNativeCommandUseErrorActionPreference" -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}
$swpRoot = "D:\SWP"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Ensure local SQL Server instance is targeted for SQL Server QA integration tests
if (-not $env:TEST_SQL_SERVER) {
    $env:TEST_SQL_SERVER = ".\MSSQLSERVER01"
}
if (-not $env:DENTAL_CLINIC_SQL_INSTANCE) {
    $env:DENTAL_CLINIC_SQL_INSTANCE = ".\MSSQLSERVER01"
}
if (-not $env:DENTAL_CLINIC_TEST_DB) {
    $env:DENTAL_CLINIC_TEST_DB = "DentalCare_SWP391"
}
if (-not $env:DENTAL_CLINIC_TEST_CONNECTION_STRING) {
    $env:DENTAL_CLINIC_TEST_CONNECTION_STRING = "Server=$($env:TEST_SQL_SERVER);Database=DentalCare_SWP391;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
}
if (-not $env:DENTAL_CLINIC_CONNECTION_STRING) {
    $env:DENTAL_CLINIC_CONNECTION_STRING = "Server=$($env:TEST_SQL_SERVER);Database=DentalCare_SWP391;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
}
if (-not $env:DENTAL_CLINIC_FOLLOWUP_TEST_CONNECTION_STRING) {
    $env:DENTAL_CLINIC_FOLLOWUP_TEST_CONNECTION_STRING = "Server=$($env:TEST_SQL_SERVER);Database=DentalClinic_FollowUp_QA;Trusted_Connection=True;MultipleActiveResultSets=false;TrustServerCertificate=True;"
}

$ErrorActionPreference = "Continue"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " DentalCare SWP391 - Automated Test Synchronization Runner       " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. Terminate running daemons to avoid MSB3026 / MSB3027 locked DLL errors
if (-not $SkipStopProcs) {
    $stopScript = Join-Path $scriptDir "stop-daemons.ps1"
    if (Test-Path $stopScript) {
        & $stopScript
    }
}

# 2. Run Backend Unit & Integration Tests
$beProj = Join-Path $swpRoot "BE\DentalClinic.Tests\DentalClinic.Tests.csproj"
Write-Host "`n[1/2] Running Backend Tests ($beProj)..." -ForegroundColor Yellow

$beStartTime = Get-Date
$beOutput = dotnet test $beProj -c $Configuration --nologo 2>&1
$beExitCode = $LASTEXITCODE
$beDuration = [Math]::Round(((Get-Date) - $beStartTime).TotalSeconds, 1)

$beOutput | ForEach-Object { Write-Host "  $_" }

if ($beExitCode -ne 0) {
    Write-Host "`n[ERROR] Backend tests FAILED (Exit Code: $beExitCode)." -ForegroundColor Red
    exit 1
} else {
    Write-Host "[SUCCESS] Backend tests PASSED in [${beDuration}s]." -ForegroundColor Green
}

# 3. Run Frontend Unit Tests
$feProj = Join-Path $swpRoot "FE\DentalClinic.Web.Tests\DentalClinic.Web.Tests.csproj"
Write-Host "`n[2/2] Running Frontend Tests ($feProj)..." -ForegroundColor Yellow

$feStartTime = Get-Date
$feOutput = dotnet test $feProj -c $Configuration --nologo 2>&1
$feExitCode = $LASTEXITCODE
$feDuration = [Math]::Round(((Get-Date) - $feStartTime).TotalSeconds, 1)

$feOutput | ForEach-Object { Write-Host "  $_" }

if ($feExitCode -ne 0) {
    Write-Host "`n[ERROR] Frontend tests FAILED (Exit Code: $feExitCode)." -ForegroundColor Red
    exit 1
} else {
    Write-Host "[SUCCESS] Frontend tests PASSED in [${feDuration}s]." -ForegroundColor Green
}

Write-Host "`n=================================================================" -ForegroundColor Green
Write-Host " All DentalCare Test Suites Passed with 100% Success!            " -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
exit 0
