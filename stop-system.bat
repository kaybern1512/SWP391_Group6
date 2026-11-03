@echo off
chcp 65001 > nul
echo ======================================================================
echo   DENTAL CLINIC MANAGEMENT SYSTEM - DỪNG HỆ THỐNG
echo ======================================================================
echo.

echo Đang dừng tiến trình Backend và Frontend...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":7350 "') do (
    taskkill /F /PID %%a >nul 2>&1
)
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":7129 "') do (
    taskkill /F /PID %%a >nul 2>&1
)
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":5350 "') do (
    taskkill /F /PID %%a >nul 2>&1
)
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":5049 "') do (
    taskkill /F /PID %%a >nul 2>&1
)

echo.
echo Đã dừng tất cả các tiến trình chạy trên cổng 7350 và 7129.
pause
