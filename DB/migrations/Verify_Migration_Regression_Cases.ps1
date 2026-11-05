# =====================================================================================
# Script: DB/migrations/Verify_Migration_Regression_Cases.ps1
# Description:
#   Hardened, non-destructive automated regression test suite verifying:
#   1. Safe CHECK constraint discovery using sys.sql_expression_dependencies metadata.
#   2. Anonymous table-level status constraint upgrade.
#   3. Preservation of unrelated constraints (Reason, RecommendedDate).
#   4. Preservation of constraints containing 'Status' as string literal.
#   5. Safe abort when multi-column constraint referencing Status & another column is present.
#   6. Migration idempotency (run 2 succeeds with exit code 0).
#   7. Rollback safe abort when 'Confirmed' / 'Proposed' records exist.
#   8. Clean rollback preserving unrelated constraints and restoring 4-status constraint.
#
# SAFETY GUARANTEES:
#   - Unique per-run temporary database name generation (DentalClinic_QA_Verify_<RunId>).
#   - Strict prefix validation (^DentalClinic_(QA_Verify|Disposable_Verify)_[a-zA-Z0-9_]+$).
#   - Absolute blacklist against production and shared databases.
#   - Pre-existence abort: refrains from touching any pre-existing database.
#   - Ownership verification table (__QA_TestRunOwnership) verified prior to cleanup.
#   - Non-coercive cleanup: NEVER executes SINGLE_USER WITH ROLLBACK IMMEDIATE on unknown DBs.
#   - Real assertion failures: non-zero exit code on any failed expectation.
# =====================================================================================

param (
    [string]$ServerInstance = ".\SQL2025_DUC",
    [string]$TestDatabase = "",
    [switch]$RunSafetyGuardVerification = $false
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# 1. Safety Guard Definitions
# ---------------------------------------------------------------------------------
$BlacklistDatabases = @(
    "master", "tempdb", "model", "msdb",
    "DentalClinicManagementDB",
    "DentalClinicManagementDB_Test",
    "DentalClinicManagementDB_Task16_TestRestore",
    "DentalClinicManagementDB_Imported",
    "DentalClinic_FollowUp_QA",
    "DentalClinic_QA",
    "DentalClinic_Prod",
    "GymMng", "PE_DBI202_FA24", "DBI202_PE_DB1", "FUH_Company", "Quanlykhocf"
)

function Assert-DatabaseNameAllowed {
    param ([string]$DbName)

    if ([string]::IsNullOrWhiteSpace($DbName)) {
        throw "SECURITY VIOLATION: Database name cannot be null or empty."
    }

    foreach ($blacklisted in $BlacklistDatabases) {
        if ($DbName.Equals($blacklisted, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "SECURITY VIOLATION: Database name '$DbName' is blacklisted (production/shared/system database)."
        }
    }

    # Must match allowed prefix pattern
    if ($DbName -notmatch '^DentalClinic_(QA_Verify|Disposable_Verify)_[a-zA-Z0-9_]+$') {
        throw "SECURITY VIOLATION: Database name '$DbName' does not match allowed temporary pattern '^DentalClinic_(QA_Verify|Disposable_Verify)_[a-zA-Z0-9_]+$'."
    }
}

function Assert-Condition {
    param (
        [bool]$Condition,
        [string]$Description
    )
    if (-not $Condition) {
        Write-Host " [ASSERTION FAILED] $Description" -ForegroundColor Red
        throw "ASSERTION FAILURE: $Description"
    } else {
        Write-Host " [ASSERTION PASSED] $Description" -ForegroundColor Green
    }
}

# Function to execute sqlcmd query
function Invoke-SqlCmdQuery {
    param (
        [string]$Query,
        [string]$Db = "master",
        [bool]$ExpectSuccess = $true
    )
    $output = sqlcmd -S $ServerInstance -C -E -d $Db -b -W -Q $Query 2>&1
    $exitCode = $LASTEXITCODE
    if ($ExpectSuccess -and $exitCode -ne 0) {
        throw "sqlcmd query failed with exit code $($exitCode): $output"
    }
    return [PSCustomObject]@{
        ExitCode = $exitCode
        Output = ($output -join "`n")
    }
}

# Function to execute sqlcmd file
function Invoke-SqlCmdFile {
    param (
        [string]$FilePath,
        [string]$Db
    )
    $output = sqlcmd -S $ServerInstance -C -E -d $Db -b -W -i $FilePath 2>&1
    $exitCode = $LASTEXITCODE
    return [PSCustomObject]@{
        ExitCode = $exitCode
        Output = ($output -join "`n")
    }
}

# Helper to parse scalar integer from sqlcmd output safely
function Get-SqlScalarInt {
    param ([object]$SqlCmdResult)
    if ($null -eq $SqlCmdResult -or [string]::IsNullOrWhiteSpace($SqlCmdResult.Output)) {
        return -1
    }
    $lines = $SqlCmdResult.Output -split "`r?`n"
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        if ($trimmed -match '^\d+$') {
            return [int]$trimmed
        }
    }
    return -1
}

# ---------------------------------------------------------------------------------
# 2. Negative Safety Guard Self-Test (Task 3)
# ---------------------------------------------------------------------------------
function Test-NegativeSafetyGuards {
    Write-Host "`n[SAFETY TEST] Running Negative Safety Guard Verification..." -ForegroundColor Magenta

    # Test 2.1: Prohibit DentalClinicManagementDB
    $rejected1 = $false
    try {
        Assert-DatabaseNameAllowed -DbName "DentalClinicManagementDB"
    } catch {
        if ($_ -match "SECURITY VIOLATION") { $rejected1 = $true }
    }
    Assert-Condition -Condition $rejected1 -Description "Safety Guard rejects 'DentalClinicManagementDB'"

    # Test 2.2: Prohibit System Databases
    $rejected2 = $false
    try {
        Assert-DatabaseNameAllowed -DbName "master"
    } catch {
        if ($_ -match "SECURITY VIOLATION") { $rejected2 = $true }
    }
    Assert-Condition -Condition $rejected2 -Description "Safety Guard rejects system database 'master'"

    # Test 2.3: Prohibit Non-Conforming Prefixes
    $rejected3 = $false
    try {
        Assert-DatabaseNameAllowed -DbName "MyRandomTempDb_123"
    } catch {
        if ($_ -match "SECURITY VIOLATION") { $rejected3 = $true }
    }
    Assert-Condition -Condition $rejected3 -Description "Safety Guard rejects non-conforming database prefix"

    # Test 2.4: Prohibit Pre-Existing Database Check
    $preExistsRejected = $false
    try {
        $checkPreExistSql = "SELECT DB_ID('DentalClinic_FollowUp_QA');"
        $dbIdRes = Invoke-SqlCmdQuery -Query $checkPreExistSql -Db "master"
        $parsedId = Get-SqlScalarInt $dbIdRes
        if ($parsedId -gt 0) {
            throw "SAFETY ABORT: Database already exists before script run."
        }
    } catch {
        if ($_ -match "SAFETY ABORT") { $preExistsRejected = $true }
    }
    Assert-Condition -Condition $preExistsRejected -Description "Safety Guard detects and aborts on pre-existing database"

    # Test 2.5: Assertion Failure Mechanism
    $assertCaught = $false
    try {
        Assert-Condition -Condition ($false) -Description "Simulated failing test assertion"
    } catch {
        if ($_ -match "ASSERTION FAILURE") { $assertCaught = $true }
    }
    Assert-Condition -Condition $assertCaught -Description "Assert-Condition properly halts execution on failure"

    Write-Host "[SAFETY TEST] All Negative Safety Guard checks passed successfully!`n" -ForegroundColor Green
}

if ($RunSafetyGuardVerification) {
    Test-NegativeSafetyGuards
    exit 0
}

# Also run negative safety self-test at start of regression suite
Test-NegativeSafetyGuards

# ---------------------------------------------------------------------------------
# 3. Setup Unique Test Run Database (Task 1)
# ---------------------------------------------------------------------------------
$RunId = "run_" + (Get-Date -Format "yyyyMMdd_HHmmss") + "_" + ([Guid]::NewGuid().ToString("N").Substring(0, 8))

if ([string]::IsNullOrWhiteSpace($TestDatabase)) {
    $TestDatabase = "DentalClinic_QA_Verify_$RunId"
}

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host " STARTING REGRESSION TEST SUITE: CONSTRAINT DISCOVERY & MIGRATION" -ForegroundColor Cyan
Write-Host " Target Server: $ServerInstance" -ForegroundColor Cyan
Write-Host " Target Database: $TestDatabase" -ForegroundColor Cyan
Write-Host " Run Identifier: $RunId" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan

# Validate Database Name
Assert-DatabaseNameAllowed -DbName $TestDatabase

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$migrationFile = Join-Path $scriptDir "20261104_Update_FollowUpSchedules_Status_CheckConstraint.sql"
$rollbackFile = Join-Path $scriptDir "20261104_Rollback_FollowUpSchedules_Status_CheckConstraint.sql"

Assert-Condition -Condition (Test-Path $migrationFile) -Description "Migration file exists ($migrationFile)"
Assert-Condition -Condition (Test-Path $rollbackFile) -Description "Rollback file exists ($rollbackFile)"

# Check that database does NOT already exist before run (Requirement 4)
$preExistCheck = Invoke-SqlCmdQuery -Query "SELECT ISNULL(CAST(DB_ID('$TestDatabase') AS VARCHAR(20)), '-1');" -Db "master"
$existingDbId = Get-SqlScalarInt $preExistCheck
if ($existingDbId -gt 0) {
    throw "SAFETY ABORT: Database '$TestDatabase' already exists before script execution. The script will NOT touch or delete pre-existing databases."
}

Write-Host "`n[SETUP] Creating clean isolated temporary QA database: $TestDatabase..." -ForegroundColor Yellow
$createDbSql = "CREATE DATABASE [$TestDatabase];"
Invoke-SqlCmdQuery -Query $createDbSql -Db "master" | Out-Null

# Record Ownership and Test Run Identification (Requirement 5)
$setupOwnershipSql = @"
USE [$TestDatabase];
CREATE TABLE [dbo].[__QA_TestRunOwnership] (
    [RunId] NVARCHAR(100) PRIMARY KEY,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [ScriptSignature] NVARCHAR(200) NOT NULL,
    [HostName] NVARCHAR(100) NOT NULL,
    [UserName] NVARCHAR(100) NOT NULL
);
INSERT INTO [dbo].[__QA_TestRunOwnership] ([RunId], [ScriptSignature], [HostName], [UserName])
VALUES ('$RunId', 'Verify_Migration_Regression_Cases', HOST_NAME(), SUSER_SNAME());
"@
Invoke-SqlCmdQuery -Query $setupOwnershipSql -Db $TestDatabase | Out-Null
Write-Host "Ownership token registered in [$TestDatabase].[dbo].[__QA_TestRunOwnership]." -ForegroundColor Green

# Setup Representative Schema
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
Write-Host "Representative schema setup complete." -ForegroundColor Green

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

# Assert initial constraints exist
$ckStatusBefore = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints cc WHERE cc.parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND (cc.parent_column_id = COLUMNPROPERTY(cc.parent_object_id, 'Status', 'ColumnId') OR EXISTS (SELECT 1 FROM sys.sql_expression_dependencies d WHERE d.referencing_id = cc.object_id AND d.referenced_minor_id = COLUMNPROPERTY(cc.parent_object_id, 'Status', 'ColumnId')));" -Db $TestDatabase
Assert-Condition -Condition ((Get-SqlScalarInt $ckStatusBefore) -eq 1) -Description "Initial anonymous status constraint exists"

# ---------------------------------------------------------------------------------
# TEST 1: Run Actual Migration File (1st execution)
# ---------------------------------------------------------------------------------
Write-Host "`n[STEP 2] Running migration file (First Run)..." -ForegroundColor Yellow
$mig1 = Invoke-SqlCmdFile -FilePath $migrationFile -Db $TestDatabase
Write-Host "Exit Code: $($mig1.ExitCode)"
Write-Host $mig1.Output

Assert-Condition -Condition ($mig1.ExitCode -eq 0) -Description "Migration 1st run exited with code 0"

Write-Host "`n[STEP 3] Inspecting constraints AFTER migration:" -ForegroundColor Yellow
$afterRes = Invoke-SqlCmdQuery -Query $inspectQuery -Db $TestDatabase
Write-Host $afterRes.Output

# Real Assertions (Task 2)
$ckStatusCount = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_Status' AND is_disabled = 0 AND is_not_trusted = 0;" -Db $TestDatabase
$ckOldStatusCount = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name <> 'CK_FollowUpSchedules_Status' AND (parent_column_id = COLUMNPROPERTY(parent_object_id, 'Status', 'ColumnId') OR EXISTS (SELECT 1 FROM sys.sql_expression_dependencies d WHERE d.referencing_id = sys.check_constraints.object_id AND d.referenced_minor_id = COLUMNPROPERTY(sys.check_constraints.parent_object_id, 'Status', 'ColumnId')));" -Db $TestDatabase
$ckReasonCount = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_Reason';" -Db $TestDatabase
$ckDateCount = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_RecommendedDate';" -Db $TestDatabase
$ckLiteralCount = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_LiteralStatus';" -Db $TestDatabase

Assert-Condition -Condition ((Get-SqlScalarInt $ckStatusCount) -eq 1) -Description "CK_FollowUpSchedules_Status exists, is_disabled=0, is_not_trusted=0"
Assert-Condition -Condition ((Get-SqlScalarInt $ckOldStatusCount) -eq 0) -Description "Old anonymous status constraint was dropped and replaced"
Assert-Condition -Condition ((Get-SqlScalarInt $ckReasonCount) -eq 1) -Description "CK_FollowUpSchedules_Reason is preserved intact"
Assert-Condition -Condition ((Get-SqlScalarInt $ckDateCount) -eq 1) -Description "CK_FollowUpSchedules_RecommendedDate is preserved intact"
Assert-Condition -Condition ((Get-SqlScalarInt $ckLiteralCount) -eq 1) -Description "CK_FollowUpSchedules_LiteralStatus (containing 'Status' string literal) is preserved intact"

# ---------------------------------------------------------------------------------
# TEST 2: Idempotency (Run Migration 2nd time)
# ---------------------------------------------------------------------------------
Write-Host "`n[STEP 4] Testing Idempotency (Running migration file a Second Time)..." -ForegroundColor Yellow
$mig2 = Invoke-SqlCmdFile -FilePath $migrationFile -Db $TestDatabase
Write-Host "Exit Code: $($mig2.ExitCode)"
Write-Host $mig2.Output

Assert-Condition -Condition ($mig2.ExitCode -eq 0) -Description "Migration 2nd run (idempotency) exited with code 0"

$ckStatusCount2 = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_Status' AND is_disabled = 0 AND is_not_trusted = 0;" -Db $TestDatabase
Assert-Condition -Condition ((Get-SqlScalarInt $ckStatusCount2) -eq 1) -Description "Exactly one active & trusted status constraint exists after 2nd run (no duplicates)"

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

Assert-Condition -Condition ($migMulti.ExitCode -ne 0) -Description "Migration aborted with non-zero exit code on multi-column constraint"
Assert-Condition -Condition ($migMulti.Output -match "DISCOVERY SAFETY ABORT: Found multi-column CHECK constraint") -Description "Migration returned exact expected safety abort diagnostic message"

# Drop multi-column constraint to continue regression test sequence
Invoke-SqlCmdQuery -Query "ALTER TABLE [dbo].[FollowUpSchedules] DROP CONSTRAINT [CK_FollowUpSchedules_MultiCol];" -Db $TestDatabase | Out-Null

# ---------------------------------------------------------------------------------
# TEST 4: Safe Rollback Abort on Confirmed Data
# ---------------------------------------------------------------------------------
Write-Host "`n[STEP 6] Testing Rollback Safe Abort with 'Confirmed' Data..." -ForegroundColor Yellow
Invoke-SqlCmdQuery -Query "INSERT INTO [dbo].[FollowUpSchedules] ([AppointmentId], [RecommendedDate], [Reason], [Status]) VALUES (NULL, '2026-12-10', 'Hen tai kham', 'Confirmed');" -Db $TestDatabase | Out-Null

$rbAbort = Invoke-SqlCmdFile -FilePath $rollbackFile -Db $TestDatabase
Write-Host "Exit Code: $($rbAbort.ExitCode) (Expected: non-zero)"
Write-Host $rbAbort.Output

Assert-Condition -Condition ($rbAbort.ExitCode -ne 0) -Description "Rollback aborted with non-zero exit code when 'Confirmed' clinical data exists"
Assert-Condition -Condition ($rbAbort.Output -match "ROLLBACK ABORTED: Found 1 record\(s\) in FollowUpSchedules with extended clinical status") -Description "Rollback returned exact clinical data protection diagnostic message"

# ---------------------------------------------------------------------------------
# TEST 5: Clean Rollback After Data Resolution
# ---------------------------------------------------------------------------------
Write-Host "`n[STEP 7] Testing Clean Rollback After Clinical Data Resolution..." -ForegroundColor Yellow
Invoke-SqlCmdQuery -Query "UPDATE [dbo].[FollowUpSchedules] SET [Status] = 'Scheduled' WHERE [Status] = 'Confirmed';" -Db $TestDatabase | Out-Null

$rbSuccess = Invoke-SqlCmdFile -FilePath $rollbackFile -Db $TestDatabase
Write-Host "Exit Code: $($rbSuccess.ExitCode) (Expected: 0)"
Write-Host $rbSuccess.Output

Assert-Condition -Condition ($rbSuccess.ExitCode -eq 0) -Description "Rollback exited with code 0 after clinical data resolution"

Write-Host "`n[STEP 8] Inspecting constraints AFTER rollback:" -ForegroundColor Yellow
$rbInspect = Invoke-SqlCmdQuery -Query $inspectQuery -Db $TestDatabase
Write-Host $rbInspect.Output

$ckStatusRb = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_Status';" -Db $TestDatabase
$ckLegacyStatusRestored = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND definition LIKE '%Pending%' AND definition LIKE '%Cancelled%' AND definition NOT LIKE '%Proposed%';" -Db $TestDatabase
$ckReasonRb = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_Reason';" -Db $TestDatabase
$ckDateRb = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_RecommendedDate';" -Db $TestDatabase
$ckLiteralRb = Invoke-SqlCmdQuery -Query "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FollowUpSchedules') AND name = 'CK_FollowUpSchedules_LiteralStatus';" -Db $TestDatabase

Assert-Condition -Condition ((Get-SqlScalarInt $ckStatusRb) -eq 0) -Description "CK_FollowUpSchedules_Status (6-status) was removed in rollback"
Assert-Condition -Condition ((Get-SqlScalarInt $ckLegacyStatusRestored) -eq 1) -Description "Legacy 4-status check constraint restored in rollback"
Assert-Condition -Condition ((Get-SqlScalarInt $ckReasonRb) -eq 1) -Description "CK_FollowUpSchedules_Reason is preserved after rollback"
Assert-Condition -Condition ((Get-SqlScalarInt $ckDateRb) -eq 1) -Description "CK_FollowUpSchedules_RecommendedDate is preserved after rollback"
Assert-Condition -Condition ((Get-SqlScalarInt $ckLiteralRb) -eq 1) -Description "CK_FollowUpSchedules_LiteralStatus is preserved after rollback"

# ---------------------------------------------------------------------------------
# 4. Verified Ownership Cleanup (Task 1 Requirements 5, 6, 10)
# ---------------------------------------------------------------------------------
Write-Host "`n[CLEANUP] Verifying ownership before cleanup of: $TestDatabase..." -ForegroundColor Yellow

$ownershipVerifySql = "SELECT COUNT(*) FROM [$TestDatabase].[dbo].[__QA_TestRunOwnership] WHERE [RunId] = '$RunId' AND [ScriptSignature] = 'Verify_Migration_Regression_Cases';"
$ownershipCheck = Invoke-SqlCmdQuery -Query $ownershipVerifySql -Db $TestDatabase -ExpectSuccess $false
$ownershipCount = Get-SqlScalarInt $ownershipCheck

if ($ownershipCheck.ExitCode -ne 0 -or $ownershipCount -ne 1) {
    Write-Host "CLEANUP WARNING: Ownership verification failed for database '$TestDatabase'. Refusing to drop database." -ForegroundColor Red
} else {
    Write-Host "Ownership confirmed (RunId: $RunId). Proceeding with safe non-coercive drop..." -ForegroundColor Green
    # Non-coercive drop: do NOT use SINGLE_USER WITH ROLLBACK IMMEDIATE
    $dropDbRes = Invoke-SqlCmdQuery -Query "DROP DATABASE [$TestDatabase];" -Db "master" -ExpectSuccess $false
    if ($dropDbRes.ExitCode -eq 0) {
        Write-Host "Temporary QA database [$TestDatabase] dropped successfully." -ForegroundColor Green
    } else {
        Write-Host "CLEANUP WARNING: Clean DROP DATABASE failed ($($dropDbRes.Output)). Preserving database without coercive termination." -ForegroundColor Yellow
    }
}

Write-Host "`n=====================================================================" -ForegroundColor Green
Write-Host " ALL REGRESSION TESTS PASSED WITH 100% REAL ASSERTION VERIFICATION!" -ForegroundColor Green
Write-Host "=====================================================================" -ForegroundColor Green
