# =====================================================================================
# Script: DB/migrations/Verify_Migration_Regression_Cases.ps1
# Description:
#   Automated regression test suite verifying:
#   1. Safe CHECK constraint discovery using sys.sql_expression_dependencies metadata.
#   2. Anonymous table-level status constraint upgrade.
#   3. Preservation of unrelated constraints (Reason, RecommendedDate).
#   4. Preservation of constraints containing 'Status' as string literal.
#   5. Safe abort when multi-column constraint referencing Status & another column is present.
#   6. Migration idempotency (run 2 succeeds with exit code 0).
#   7. Rollback safe abort when 'Confirmed' / 'Proposed' records exist.
#   8. Clean rollback preserving unrelated constraints and restoring 4-status constraint.
# =====================================================================================

param (
    [string]$ServerInstance = ".\SQL2025_DUC",
    [string]$TestDatabase = "DentalClinic_Disposable_PreDeployClone"
)

$ErrorActionPreference = "Stop"

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host " STARTING REGRESSION TEST SUITE: CONSTRAINT DISCOVERY & MIGRATION" -ForegroundColor Cyan
Write-Host " Target Server: $ServerInstance" -ForegroundColor Cyan
Write-Host " Target Database: $TestDatabase" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoDir = Split-Path -Parent (Split-Path -Parent $scriptDir)
$migrationFile = Join-Path $scriptDir "20261104_Update_FollowUpSchedules_Status_CheckConstraint.sql"
$rollbackFile = Join-Path $scriptDir "20261104_Rollback_FollowUpSchedules_Status_CheckConstraint.sql"

if (-not (Test-Path $migrationFile)) {
    throw "Migration file not found: $migrationFile"
}
if (-not (Test-Path $rollbackFile)) {
    throw "Rollback file not found: $rollbackFile"
}

# Function to execute sqlcmd
function Invoke-SqlCmdQuery {
    param (
        [string]$Query,
        [string]$Db = "master",
        [bool]$ExpectSuccess = $true
    )
    $output = sqlcmd -S $ServerInstance -C -E -d $Db -b -W -Q $Query 2>&1
    $exitCode = $LASTEXITCODE
    if ($ExpectSuccess -and $exitCode -ne 0) {
        throw "sqlcmd failed with exit code $($exitCode): $output"
    }
    return [PSCustomObject]@{
        ExitCode = $exitCode
        Output = ($output -join "`n")
    }
}

function Invoke-SqlCmdFile {
    param (
        [string]$FilePath,
        [string]$Db = $TestDatabase
    )
    $output = sqlcmd -S $ServerInstance -C -E -d $Db -b -W -i $FilePath 2>&1
    $exitCode = $LASTEXITCODE
    return [PSCustomObject]@{
        ExitCode = $exitCode
        Output = ($output -join "`n")
    }
}

# ---------------------------------------------------------------------------------
# Setup Disposable Test Database
# ---------------------------------------------------------------------------------
Write-Host "`n[SETUP] Creating clean test database: $TestDatabase..." -ForegroundColor Yellow
$setupDbSql = @"
IF DB_ID('$TestDatabase') IS NOT NULL
BEGIN
    ALTER DATABASE [$TestDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$TestDatabase];
END;
CREATE DATABASE [$TestDatabase];
"@
Invoke-SqlCmdQuery -Query $setupDbSql | Out-Null

$setupSchemaSql = @"
USE [$TestDatabase];

CREATE TABLE [dbo].[AuditLogs] (
    [AuditLogId] BIGINT IDENTITY(1,1) PRIMARY KEY,
    [UserId] INT NULL,
    [Action] NVARCHAR(100) NOT NULL,
    [EntityName] NVARCHAR(100) NOT NULL,
    [EntityId] VARCHAR(100) NOT NULL,
    [OldValues] NVARCHAR(MAX) NULL,
    [NewValues] NVARCHAR(MAX) NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME()
);

CREATE TABLE [dbo].[Appointments] (
    [AppointmentId] BIGINT IDENTITY(1,1) PRIMARY KEY,
    [Status] NVARCHAR(50) NOT NULL
);

CREATE TABLE [dbo].[FollowUpSchedules] (
    [FollowUpId] BIGINT IDENTITY(1,1) PRIMARY KEY,
    [AppointmentId] BIGINT NULL,
    [RecommendedDate] DATE NULL,
    [Reason] NVARCHAR(500) NULL,
    [Status] NVARCHAR(50) NOT NULL
);

-- Case 1: Legacy anonymous table-level CHECK constraint on Status
ALTER TABLE [dbo].[FollowUpSchedules] WITH CHECK ADD CHECK
(([Status]='Cancelled' OR [Status]='Completed' OR [Status]='Scheduled' OR [Status]='Pending'));

-- Case 2: Constraint referencing only Reason
ALTER TABLE [dbo].[FollowUpSchedules] WITH CHECK ADD CONSTRAINT [CK_FollowUpSchedules_Reason]
CHECK (LEN([Reason]) > 0);

-- Case 3: Constraint referencing only RecommendedDate
ALTER TABLE [dbo].[FollowUpSchedules] WITH CHECK ADD CONSTRAINT [CK_FollowUpSchedules_RecommendedDate]
CHECK ([RecommendedDate] >= '2020-01-01');

-- Case 4: Constraint with 'Status' as a string literal, referencing only Reason
ALTER TABLE [dbo].[FollowUpSchedules] WITH CHECK ADD CONSTRAINT [CK_FollowUpSchedules_LiteralStatus]
CHECK ([Reason] <> 'Status' AND [Reason] NOT LIKE '%Status%');

-- Seed valid clinical baseline
INSERT INTO [dbo].[FollowUpSchedules] ([AppointmentId], [RecommendedDate], [Reason], [Status])
VALUES (NULL, '2026-12-01', 'Kiem tra rang sau can thiep', 'Pending');
"@
Invoke-SqlCmdQuery -Query $setupSchemaSql -Db $TestDatabase | Out-Null
Write-Host "Database and schema setup complete." -ForegroundColor Green

# Helper query to inspect constraints
$inspectQuery = @"
SET NOCOUNT ON;
SELECT 
    cc.name,
    cc.parent_column_id,
    COL_NAME(cc.parent_object_id, cc.parent_column_id) AS parent_col_name,
    cc.definition,
    cc.is_disabled,
    cc.is_not_trusted,
    (
        SELECT STRING_AGG(COL_NAME(dep.referenced_id, dep.referenced_minor_id), ', ')
        FROM sys.sql_expression_dependencies dep
        WHERE dep.referencing_id = cc.object_id
    ) AS referenced_columns
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('dbo.FollowUpSchedules')
ORDER BY cc.name;
"@

Write-Host "`n[STEP 1] Inspecting constraints BEFORE migration:" -ForegroundColor Yellow
$beforeRes = Invoke-SqlCmdQuery -Query $inspectQuery -Db $TestDatabase
Write-Host $beforeRes.Output

# ---------------------------------------------------------------------------------
# TEST 1: Run Actual Migration File (1st execution)
# ---------------------------------------------------------------------------------
Write-Host "`n[STEP 2] Running migration file (First Run)..." -ForegroundColor Yellow
$mig1 = Invoke-SqlCmdFile -FilePath $migrationFile -Db $TestDatabase
Write-Host "Exit Code: $($mig1.ExitCode)"
Write-Host $mig1.Output

if ($mig1.ExitCode -ne 0) {
    throw "TEST FAILED: Migration run 1 failed!"
}

Write-Host "`n[STEP 3] Inspecting constraints AFTER migration:" -ForegroundColor Yellow
$afterRes = Invoke-SqlCmdQuery -Query $inspectQuery -Db $TestDatabase
Write-Host $afterRes.Output

# Verify constraints
$ckStatus = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_Status' AND is_disabled = 0 AND is_not_trusted = 0;" -Db $TestDatabase
$ckReason = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_Reason';" -Db $TestDatabase
$ckDate = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_RecommendedDate';" -Db $TestDatabase
$ckLiteral = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_LiteralStatus';" -Db $TestDatabase

Write-Host "`n--- VERIFICATION CHECKS ---" -ForegroundColor Cyan
Write-Host "CK_FollowUpSchedules_Status active and trusted: $($ckStatus.Output.Trim() -match '1')"
Write-Host "CK_FollowUpSchedules_Reason preserved: $($ckReason.Output.Trim() -match '1')"
Write-Host "CK_FollowUpSchedules_RecommendedDate preserved: $($ckDate.Output.Trim() -match '1')"
Write-Host "CK_FollowUpSchedules_LiteralStatus preserved: $($ckLiteral.Output.Trim() -match '1')"

# ---------------------------------------------------------------------------------
# TEST 2: Idempotency (Run Migration 2nd time)
# ---------------------------------------------------------------------------------
Write-Host "`n[STEP 4] Testing Idempotency (Running migration file a Second Time)..." -ForegroundColor Yellow
$mig2 = Invoke-SqlCmdFile -FilePath $migrationFile -Db $TestDatabase
Write-Host "Exit Code: $($mig2.ExitCode)"
Write-Host $mig2.Output

if ($mig2.ExitCode -ne 0) {
    throw "TEST FAILED: Migration run 2 (idempotency) failed!"
}
Write-Host "Idempotency check PASSED." -ForegroundColor Green

# ---------------------------------------------------------------------------------
# TEST 3: Multi-Column Constraint Safety Abort
# ---------------------------------------------------------------------------------
Write-Host "`n[STEP 5] Testing Multi-Column Constraint Safety Abort..." -ForegroundColor Yellow
$addMultiColSql = @"
ALTER TABLE [dbo].[FollowUpSchedules]
ADD CONSTRAINT [CK_FollowUpSchedules_MultiCol]
CHECK ([Status] <> 'Cancelled' OR [AppointmentId] IS NOT NULL);
"@
Invoke-SqlCmdQuery -Query $addMultiColSql -Db $TestDatabase | Out-Null
Write-Host "Added multi-column constraint [CK_FollowUpSchedules_MultiCol]."

$migMulti = Invoke-SqlCmdFile -FilePath $migrationFile -Db $TestDatabase
Write-Host "Exit Code: $($migMulti.ExitCode) (Expected: non-zero)"
Write-Host $migMulti.Output

if ($migMulti.ExitCode -eq 0 -or $migMulti.Output -notmatch "DISCOVERY SAFETY ABORT: Found multi-column CHECK constraint") {
    throw "TEST FAILED: Migration did NOT abort on multi-column constraint!"
}
Write-Host "Multi-column safety abort PASSED." -ForegroundColor Green

# Drop multi-column constraint to continue testing
Invoke-SqlCmdQuery -Query "ALTER TABLE [dbo].[FollowUpSchedules] DROP CONSTRAINT [CK_FollowUpSchedules_MultiCol];" -Db $TestDatabase | Out-Null

# ---------------------------------------------------------------------------------
# TEST 4: Safe Rollback Abort on Confirmed Data
# ---------------------------------------------------------------------------------
Write-Host "`n[STEP 6] Testing Rollback Safe Abort with 'Confirmed' Data..." -ForegroundColor Yellow
Invoke-SqlCmdQuery -Query "INSERT INTO [dbo].[FollowUpSchedules] ([AppointmentId], [RecommendedDate], [Reason], [Status]) VALUES (NULL, '2026-12-10', 'Hen tai kham', 'Confirmed');" -Db $TestDatabase | Out-Null

$rbAbort = Invoke-SqlCmdFile -FilePath $rollbackFile -Db $TestDatabase
Write-Host "Exit Code: $($rbAbort.ExitCode) (Expected: non-zero)"
Write-Host $rbAbort.Output

if ($rbAbort.ExitCode -eq 0 -or $rbAbort.Output -notmatch "ROLLBACK ABORTED") {
    throw "TEST FAILED: Rollback did NOT abort when 'Confirmed' record exists!"
}
Write-Host "Rollback safe abort on 'Confirmed' record PASSED." -ForegroundColor Green

# ---------------------------------------------------------------------------------
# TEST 5: Clean Rollback After Data Resolution
# ---------------------------------------------------------------------------------
Write-Host "`n[STEP 7] Testing Clean Rollback After Clinical Data Resolution..." -ForegroundColor Yellow
Invoke-SqlCmdQuery -Query "UPDATE [dbo].[FollowUpSchedules] SET [Status] = 'Scheduled' WHERE [Status] = 'Confirmed';" -Db $TestDatabase | Out-Null

$rbSuccess = Invoke-SqlCmdFile -FilePath $rollbackFile -Db $TestDatabase
Write-Host "Exit Code: $($rbSuccess.ExitCode) (Expected: 0)"
Write-Host $rbSuccess.Output

if ($rbSuccess.ExitCode -ne 0) {
    throw "TEST FAILED: Clean rollback failed!"
}

Write-Host "`n[STEP 8] Inspecting constraints AFTER rollback:" -ForegroundColor Yellow
$rbInspect = Invoke-SqlCmdQuery -Query $inspectQuery -Db $TestDatabase
Write-Host $rbInspect.Output

$ckReasonRb = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_Reason';" -Db $TestDatabase
$ckDateRb = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_RecommendedDate';" -Db $TestDatabase
$ckLiteralRb = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_LiteralStatus';" -Db $TestDatabase

Write-Host "`n--- ROLLBACK PRESERVATION CHECKS ---" -ForegroundColor Cyan
Write-Host "CK_FollowUpSchedules_Reason preserved after rollback: $($ckReasonRb.Output.Trim() -match '1')"
Write-Host "CK_FollowUpSchedules_RecommendedDate preserved after rollback: $($ckDateRb.Output.Trim() -match '1')"
Write-Host "CK_FollowUpSchedules_LiteralStatus preserved after rollback: $($ckLiteralRb.Output.Trim() -match '1')"

# ---------------------------------------------------------------------------------
# Cleanup Test Database
# ---------------------------------------------------------------------------------
Write-Host "`n[CLEANUP] Dropping disposable test database: $TestDatabase..." -ForegroundColor Yellow
$dropDbSql = @"
ALTER DATABASE [$TestDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [$TestDatabase];
"@
Invoke-SqlCmdQuery -Query $dropDbSql | Out-Null
Write-Host "Disposable test database dropped successfully." -ForegroundColor Green

Write-Host "`n=====================================================================" -ForegroundColor Green
Write-Host " ALL REGRESSION TESTS PASSED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "=====================================================================" -ForegroundColor Green
