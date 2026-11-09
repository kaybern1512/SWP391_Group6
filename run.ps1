# DentalCare System Launcher
Set-Location -Path "D:\SWP"

Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "     DENTALCARE CLINIC MANAGEMENT SYSTEM" -ForegroundColor Yellow
Write-Host "===================================================" -ForegroundColor Cyan

# 1. Kiem tra SQL Server
Write-Host "[1/3] Kiem tra SQL Server (MSSQLSERVER01)..." -ForegroundColor Green
$svc = Get-Service -Name "MSSQL`$MSSQLSERVER01" -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -ne "Running") {
    Write-Host "Dang khoi dong SQL Server service..." -ForegroundColor Yellow
    Start-Service -Name "MSSQL`$MSSQLSERVER01"
} else {
    Write-Host "SQL Server dang hoat dong tot." -ForegroundColor Gray
}

# 2. Khoi chay Backend
Write-Host "[2/3] Dang khoi chay Backend API (https://localhost:7350)..." -ForegroundColor Green
Start-Process cmd.exe -ArgumentList '/k "cd /d D:\SWP && dotnet run --project BE\DentalClinic.Api --launch-profile https"'

Start-Sleep -Seconds 4

# 3. Khoi chay Frontend
Write-Host "[3/3] Dang khoi chay Frontend Web (https://localhost:7129)..." -ForegroundColor Green
Start-Process cmd.exe -ArgumentList '/k "cd /d D:\SWP && dotnet run --project FE\DentalClinic.Web --launch-profile https"'

Start-Sleep -Seconds 5

# 4. Mo trinh duyet
Start-Process "https://localhost:7129"
Start-Process "https://localhost:7350/swagger"

Write-Host ""
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "     DA KHOI CHAY HE THONG THANH CONG!" -ForegroundColor Green
Write-Host "     - Frontend Web:    https://localhost:7129" -ForegroundColor White
Write-Host "     - Backend Swagger: https://localhost:7350/swagger" -ForegroundColor White
Write-Host "===================================================" -ForegroundColor Cyan
