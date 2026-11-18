<#
.SYNOPSIS
    Resets the isolated DentalCare_SWP391_ONLINE_DEMO database to its pristine snapshot.
    NEVER touches DentalCare_SWP391_DEMO_V2_TEST.
#>
[CmdletBinding()]
param()

$backupPath = "D:\SWP391\DentalCare_SWP391_ONLINE_DEMO_baseline.bak"
if (!(Test-Path $backupPath)) {
    Write-Error "Baseline backup file not found at $backupPath"
    return
}

Write-Host "==> [Reset-OnlineDemo-DB] Resetting DentalCare_SWP391_ONLINE_DEMO from baseline backup..." -ForegroundColor Cyan

$resetSql = @"
ALTER DATABASE [DentalCare_SWP391_ONLINE_DEMO] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [DentalCare_SWP391_ONLINE_DEMO] FROM DISK = N'$backupPath' WITH REPLACE;
ALTER DATABASE [DentalCare_SWP391_ONLINE_DEMO] SET MULTI_USER;
"@

sqlcmd -S ".\SQLEXPRESS" -E -Q $resetSql

Write-Host "==> [Reset-OnlineDemo-DB] Reset complete. Fixtures #55-#62 and 62 appointments restored." -ForegroundColor Green
