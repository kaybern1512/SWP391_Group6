Write-Host "==> Stopping DentalCare daemons on ports 7350, 7129, 5350, 5129..." -ForegroundColor Yellow
$ports = @(7350, 7129, 5350, 5129)
foreach ($p in $ports) {
    $conns = Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue
    foreach ($c in $conns) {
        try {
            Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue
            Write-Host "Killed process $($c.OwningProcess) listening on port $p"
        } catch {}
    }
}
Write-Host "==> Daemons stopped." -ForegroundColor Green
