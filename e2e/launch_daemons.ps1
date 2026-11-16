$repoRoot = "D:\SWP391\DentalCare-DB-Simplification"
$beDir = Join-Path $repoRoot "BE\DentalClinic.Api"
$feDir = Join-Path $repoRoot "FE\DentalClinic.Web"

Write-Host "==> Stopping any previous instances on 7350 and 7129..." -ForegroundColor Yellow
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
Start-Sleep -Seconds 2

Write-Host "==> Starting Backend API on port 7350 (DentalCare_SWP391_UAT_Fixture_TEST)..." -ForegroundColor Cyan
$beConnStr = 'Server=.\SQLEXPRESS;Database=DentalCare_SWP391_UAT_Fixture_TEST;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true'
$beProc = Start-Process -FilePath "dotnet" -ArgumentList "run --launch-profile https --no-build --no-restore --ConnectionStrings:DentalClinicDatabase=`"$beConnStr`"" -WorkingDirectory $beDir -PassThru -WindowStyle Hidden

Write-Host "==> Starting Frontend Web on port 7129..." -ForegroundColor Cyan
$feProc = Start-Process -FilePath "dotnet" -ArgumentList "run --launch-profile https --no-build --no-restore" -WorkingDirectory $feDir -PassThru -WindowStyle Hidden

Write-Host "==> Waiting for servers to initialize..." -ForegroundColor Yellow
$maxWait = 30
$beReady = $false
$feReady = $false

for ($i = 0; $i -lt $maxWait; $i++) {
    Start-Sleep -Seconds 1
    if (-not $beReady) {
        $beConn = Get-NetTCPConnection -LocalPort 7350 -State Listen -ErrorAction SilentlyContinue
        if ($beConn) { $beReady = $true; Write-Host "  [OK] Backend API listening on port 7350." -ForegroundColor Green }
    }
    if (-not $feReady) {
        $feConn = Get-NetTCPConnection -LocalPort 7129 -State Listen -ErrorAction SilentlyContinue
        if ($feConn) { $feReady = $true; Write-Host "  [OK] Frontend Web listening on port 7129." -ForegroundColor Green }
    }
    if ($beReady -and $feReady) {
        break
    }
}

if ($beReady -and $feReady) {
    Write-Host "==> Both Daemons are ready!" -ForegroundColor Green
    Exit 0
} else {
    Write-Host "==> Error: Servers failed to initialize within $maxWait seconds. (Backend: $beReady, Frontend: $feReady)" -ForegroundColor Red
    Exit 1
}
