<#
.SYNOPSIS
    DentalCare SWP391 — Demo Daemon Shutdown Helper
.DESCRIPTION
    Safely terminates all running DentalCare backend and frontend processes.
#>

Write-Host "======================================================================" -ForegroundColor Yellow
Write-Host " Stopping DentalCare demo daemons (ports 7350, 7129, 5350, 5129)..." -ForegroundColor Yellow
Write-Host "======================================================================" -ForegroundColor Yellow

$ports = @(7350, 7129, 5350, 5129)
$killedCount = 0

foreach ($p in $ports) {
    $conns = Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue
    foreach ($c in $conns) {
        try {
            Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue
            Write-Host "  Terminated process $($c.OwningProcess) listening on port $p" -ForegroundColor Green
            $killedCount++
        } catch {}
    }
}

Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" | ForEach-Object {
    if ($_.CommandLine -match "DentalClinic" -or $_.CommandLine -match "MSBuild.dll") {
        try {
            Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
            Write-Host "  Terminated dotnet process $($_.ProcessId)" -ForegroundColor Green
            $killedCount++
        } catch {}
    }
}

Write-Host "`nAll DentalCare demo daemons stopped successfully ($killedCount process(es) terminated)." -ForegroundColor Cyan
