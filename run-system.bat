@echo off
chcp 65001 > nul
echo ======================================================================
echo   DENTAL CLINIC MANAGEMENT SYSTEM - KHỞI CHẠY HỆ THỐNG
echo ======================================================================
echo.

cd /d "%~dp0"

echo [1/2] Đang mở cửa sổ Backend Web API (https://localhost:7350)...
start "DentalClinic - Backend API (Port 7350)" cmd /k "cd /d %~dp0BE\DentalClinic.Api && dotnet run --launch-profile https"

timeout /t 5 /nobreak > nul

echo [2/2] Đang mở cửa sổ Frontend Web (https://localhost:7129)...
start "DentalClinic - Frontend Web (Port 7129)" cmd /k "cd /d %~dp0FE\DentalClinic.Web && dotnet run --launch-profile https"

echo.
echo ======================================================================
echo   HỆ THỐNG ĐÃ SẴN SÀNG:
echo   - Frontend Web App: https://localhost:7129
echo   - Backend Swagger:  https://localhost:7350/swagger
echo   (Đóng các cửa sổ console để tắt dịch vụ)
echo ======================================================================
pause
