<#
.SYNOPSIS
    Starts DentalClinic Backend API and Frontend Web in separate background processes.
#>
[CmdletBinding()]
param()

$swpRoot = "D:\SWP"
$beDir = Join-Path $swpRoot "BE\DentalClinic.Api"
$feDir = Join-Path $swpRoot "FE\DentalClinic.Web"

Write-Host "==> [Start-Daemons] Launching DentalClinic.Api (Port 7350/5350)..." -ForegroundColor Cyan
Start-Process -FilePath "dotnet" -ArgumentList "run --launch-profile https --no-build --no-restore" -WorkingDirectory $beDir -WindowStyle Hidden

Write-Host "==> [Start-Daemons] Launching DentalClinic.Web (Port 7129/5129)..." -ForegroundColor Cyan
Start-Process -FilePath "dotnet" -ArgumentList "run --launch-profile https --no-build --no-restore" -WorkingDirectory $feDir -WindowStyle Hidden

Write-Host "==> [Start-Daemons] Daemons started. Waiting 8s for ports to bind..." -ForegroundColor Yellow
Start-Sleep -Seconds 8

$beListening = Get-NetTCPConnection -LocalPort 7350 -State Listen -ErrorAction SilentlyContinue
$feListening = Get-NetTCPConnection -LocalPort 7129 -State Listen -ErrorAction SilentlyContinue

if ($beListening) {
    Write-Host "  [OK] Backend API is listening on port 7350." -ForegroundColor Green
} else {
    Write-Host "  [WAIT] Backend API still initializing..." -ForegroundColor Yellow
}

if ($feListening) {
    Write-Host "  [OK] Frontend Web is listening on port 7129." -ForegroundColor Green
} else {
    Write-Host "  [WAIT] Frontend Web still initializing..." -ForegroundColor Yellow
}
