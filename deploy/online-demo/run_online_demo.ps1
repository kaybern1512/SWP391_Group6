<#
.SYNOPSIS
    DentalCare Online Demo Launcher
    Starts isolated Backend API (Port 7351), Frontend Web (Port 7128) on DentalCare_SWP391_ONLINE_DEMO,
    and opens a Cloudflare Quick Tunnel for public HTTPS access.
#>
[CmdletBinding()]
param(
    [switch]$ResetDatabase
)

$ErrorActionPreference = "Stop"
$root = "D:\SWP391"
$runtimeDir = Join-Path $root "DentalCare-OnlineDemo-Runtime"
$apiDir = Join-Path $runtimeDir "Api"
$webDir = Join-Path $runtimeDir "Web"
$cloudflaredPath = "C:\tools\cloudflared.exe"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host " DENTALCARE SWP391 — ONLINE DEMO LAUNCHER (CLOUDFLARE TUNNEL)    " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

# 1. Optional Database Reset
if ($ResetDatabase) {
    Write-Host "`n[1/5] Resetting DentalCare_SWP391_ONLINE_DEMO to pristine baseline..." -ForegroundColor Yellow
    $resetSql = "ALTER DATABASE [DentalCare_SWP391_ONLINE_DEMO] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [DentalCare_SWP391_ONLINE_DEMO] FROM DISK = N'D:\SWP391\DentalCare_SWP391_ONLINE_DEMO_baseline.bak' WITH REPLACE; ALTER DATABASE [DentalCare_SWP391_ONLINE_DEMO] SET MULTI_USER;"
    sqlcmd -S ".\SQLEXPRESS" -E -Q $resetSql
    Write-Host "  -> Database restored successfully." -ForegroundColor Green
}

# 2. Stop any existing online demo processes on ports 7351 / 7128
Write-Host "`n[2/5] Cleaning up existing demo processes on ports 7351 and 7128..." -ForegroundColor Yellow
$activePorts = @(7351, 7128)
foreach ($p in $activePorts) {
    $conns = Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue
    foreach ($c in $conns) {
        try {
            Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue
            Write-Host "  -> Terminated process $($c.OwningProcess) on port $p" -ForegroundColor DarkGray
        } catch {}
    }
}
Get-Process cloudflared -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# 3. Launch Backend API (7351)
Write-Host "`n[3/5] Starting isolated Backend API (Port 7351) on DentalCare_SWP391_ONLINE_DEMO..." -ForegroundColor Yellow
Start-Process -FilePath "dotnet" -ArgumentList "DentalClinic.Api.dll --urls `"https://localhost:7351;http://localhost:5351`"" -WorkingDirectory $apiDir -WindowStyle Hidden
Start-Sleep -Seconds 3

# 4. Launch Frontend Web (7128)
Write-Host "`n[4/5] Starting isolated Frontend Web (Port 7128)..." -ForegroundColor Yellow
Start-Process -FilePath "dotnet" -ArgumentList "DentalClinic.Web.dll --urls `"https://localhost:7128;http://localhost:5128`"" -WorkingDirectory $webDir -WindowStyle Hidden
Start-Sleep -Seconds 4

# 5. Launch Cloudflare Tunnel
Write-Host "`n[5/5] Initializing Cloudflare Tunnel to https://localhost:7128..." -ForegroundColor Yellow
$logPath = Join-Path $runtimeDir "cloudflared.log"
if (Test-Path $logPath) { Remove-Item $logPath -Force }

Start-Process -FilePath $cloudflaredPath -ArgumentList "tunnel --url https://localhost:7128 --no-tls-verify --logfile `"$logPath`"" -WindowStyle Hidden
Write-Host "  Waiting for Cloudflare Tunnel to assign public HTTPS URL..." -ForegroundColor Yellow

$publicUrl = $null
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 1
    if (Test-Path $logPath) {
        $content = Get-Content $logPath -Raw -ErrorAction SilentlyContinue
        if ($content -match '(https://[a-zA-Z0-9-]+\.trycloudflare\.com)') {
            $publicUrl = $matches[1]
            break
        }
    }
}

Write-Host "`n==================================================================" -ForegroundColor Green
if ($publicUrl) {
    Write-Host " ONLINE DEMO IS LIVE!" -ForegroundColor Green
    Write-Host " Public URL : $publicUrl" -ForegroundColor White -BackgroundColor DarkGreen
    Write-Host " Local Web  : https://localhost:7128" -ForegroundColor Gray
    Write-Host " Local API  : https://localhost:7351" -ForegroundColor Gray
    Write-Host " Database   : DentalCare_SWP391_ONLINE_DEMO (SQL Server)" -ForegroundColor Gray
} else {
    Write-Host " [WARNING] Could not parse tunnel URL automatically." -ForegroundColor Red
    Write-Host " Check log at: $logPath" -ForegroundColor Yellow
}
Write-Host "==================================================================" -ForegroundColor Green
Write-Host "To stop the demo, run: .\deploy\online-demo\stop_online_demo.ps1" -ForegroundColor Cyan
