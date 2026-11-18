<#
.SYNOPSIS
    DentalCare Online Demo Stopper
    Safely terminates the isolated Backend API (Port 7351), Frontend Web (Port 7128),
    and Cloudflare Tunnel process.
#>
[CmdletBinding()]
param()

Write-Host "==> [Stop-OnlineDemo] Stopping Cloudflare Tunnel and isolated demo processes..." -ForegroundColor Cyan

# 1. Stop cloudflared
$tunnels = Get-Process cloudflared -ErrorAction SilentlyContinue
if ($tunnels) {
    $tunnels | Stop-Process -Force
    Write-Host "  -> Terminated cloudflared tunnel process." -ForegroundColor Green
}

# 2. Stop processes listening on 7351 and 7128
$ports = @(7351, 7128)
foreach ($p in $ports) {
    $conns = Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue
    foreach ($c in $conns) {
        try {
            Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue
            Write-Host "  -> Terminated process $($c.OwningProcess) on port $p." -ForegroundColor Green
        } catch {}
    }
}

Write-Host "==> [Stop-OnlineDemo] All online demo processes stopped." -ForegroundColor Green
