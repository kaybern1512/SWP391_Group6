@echo off
chcp 65001 >nul
title DentalCare Launcher
cd /d "D:\SWP"

echo ===================================================
echo     DENTALCARE CLINIC MANAGEMENT SYSTEM
echo ===================================================
echo.

echo [1/3] Kiem tra SQL Server (MSSQLSERVER01)...
sc query "MSSQL$MSSQLSERVER01" | find "RUNNING" >nul
if %errorlevel% neq 0 (
    echo Dang khoi dong SQL Server...
    net start "MSSQL$MSSQLSERVER01"
) else (
    echo SQL Server dang hoat dong tot.
)
echo.

echo [2/3] Dang khoi chay Backend API tren https://localhost:7350 ...
start "DentalCare Backend API (Port 7350)" cmd /k "cd /d D:\SWP && dotnet run --project BE\DentalClinic.Api --launch-profile https"

timeout /t 4 /nobreak >nul

echo [3/3] Dang khoi chay Frontend Blazor tren https://localhost:7129 ...
start "DentalCare Frontend Web (Port 7129)" cmd /k "cd /d D:\SWP && dotnet run --project FE\DentalClinic.Web --launch-profile https"

timeout /t 5 /nobreak >nul

echo.
echo Dang mo trinh duyet...
start https://localhost:7129
start https://localhost:7350/swagger

echo.
echo ===================================================
echo     DA KHOI CHAY HE THONG THANH CONG!
echo     - Frontend Web:    https://localhost:7129
echo     - Backend Swagger: https://localhost:7350/swagger
echo ===================================================
echo (De dung he thong, chi can dong 2 cua so Backend va Frontend)
echo.
pause
