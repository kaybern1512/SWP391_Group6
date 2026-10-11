@echo off
echo ========================================================
echo   THIET LAP .NET USER SECRETS CHO HE THONG DENTALCLINIC (TEMPLATE)
echo ========================================================
echo.

echo 1. Dang cai dat mat khau Gmail SMTP...
dotnet user-secrets set "EmailSettings:Password" "YOUR_GMAIL_APP_PASSWORD" --project BE\DentalClinic.Api

echo 2. Dang cai dat Cloudinary (Upload Anh)...
dotnet user-secrets set "Cloudinary:CloudName" "YOUR_CLOUDINARY_CLOUD_NAME" --project BE\DentalClinic.Api
dotnet user-secrets set "Cloudinary:ApiKey" "YOUR_CLOUDINARY_API_KEY" --project BE\DentalClinic.Api
dotnet user-secrets set "Cloudinary:ApiSecret" "YOUR_CLOUDINARY_API_SECRET" --project BE\DentalClinic.Api

echo 3. Dang cai dat Google OAuth Client ID...
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_GOOGLE_CLIENT_ID" --project BE\DentalClinic.Api

echo.
echo ========================================================
echo   DANH SACH SECRETS HIEN TAI TRONG MAY CUA BAN:
echo ========================================================
dotnet user-secrets list --project BE\DentalClinic.Api

echo.
echo Hoan tat!
pause
