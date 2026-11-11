# ==============================================================================
# Script: Generate_Standardized_DB_Files.ps1
# Purpose: Exports clean schema, complete seed dataset, and compressed .bak backup
#          directly from the live DentalClinicManagementDB instance using SMO.
# Output:
#   1. DB\Dental_Clinic_Final_Database_Clean.sql (Schema Only: 44 tables, views, SPs, indexes)
#   2. DB\DentalClinic_Full_Database_And_Data.sql (Full DB: Idempotent Schema + Complete UTF-8 Data)
#   3. DB\DentalClinicManagementDB.bak (Standard SQL Server compressed backup)
# ==============================================================================

[CmdletBinding()]
param (
    [string]$ServerInstance = ".\MSSQLSERVER01",
    [string]$DatabaseName = "DentalClinicManagementDB",
    [string]$OutputDir = "D:\SWP\DB"
)

$ErrorActionPreference = "Stop"

Write-Host ">>> Loading SQL Server Management Objects (SMO)..." -ForegroundColor Cyan
[System.Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.Smo") | Out-Null
[System.Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.SmoExtended") | Out-Null

$srv = New-Object Microsoft.SqlServer.Management.Smo.Server($ServerInstance)
$db = $srv.Databases[$DatabaseName]

if ($null -eq $db) {
    throw "Database '$DatabaseName' not found on server '$ServerInstance'."
}

Write-Host ">>> Connected to $ServerInstance - Database: $DatabaseName" -ForegroundColor Green

# 44 tables in strict topological dependency order
$orderedTableNames = @(
    'ClinicProfile', 'UserAccounts', 'PatientProfiles', 'StaffProfiles', 'DentistProfiles',
    'Departments', 'DentistDepartments', 'DepartmentWorkSchedules', 'DentistWorkSchedules',
    'DentistAvailability', 'Rooms', 'DentalChairs', 'DepartmentServices', 'Appointments',
    'AppointmentChangeProposals', 'AppointmentStatusHistories', 'AppointmentReminderJobs', 'Notifications', 'SupportChatMessages',
    'DentalMedicalRecords', 'ClinicalEntries', 'OdontogramEntries', 'ImagingRecords',
    'TreatmentPlans', 'TreatmentPlanItems', 'TreatmentSessions', 'PerformedServices',
    'Prescriptions', 'PrescriptionItems', 'DepartmentReferrals', 'FollowUpSchedules',
    'Invoices', 'InvoiceItems', 'Payments', 'VisitFeedback', 'AuditLogs',
    'BlogCategories', 'BlogPosts', 'BlogPostMedia',
    'Medicines', 'MedicineBatches', 'MedicineStockTransactions', 'MedicineDispensations', 'MedicineDispensationItems'
)

$tables = @()
foreach ($tblName in $orderedTableNames) {
    $t = $db.Tables[$tblName]
    if ($null -eq $t) {
        throw "Table '$tblName' not found in database."
    }
    $tables += $t
}
Write-Host ">>> Verified all $($tables.Count) tables in dependency order." -ForegroundColor Green

# Query non-system Views and Stored Procedures
$viewNames = @()
$spNames = @()
$objQuery = @"
SELECT name, type
FROM sys.objects
WHERE is_ms_shipped = 0 AND type IN ('V', 'P')
ORDER BY type, name;
"@
$dt = $db.ExecuteWithResults($objQuery).Tables[0]
foreach ($row in $dt.Rows) {
    if ($row["type"].ToString().Trim() -eq "V") {
        $viewNames += $row["name"].ToString()
    } elseif ($row["type"].ToString().Trim() -eq "P") {
        $spNames += $row["name"].ToString()
    }
}
Write-Host ">>> Discovered $($viewNames.Count) views: $($viewNames -join ', ')" -ForegroundColor Green
Write-Host ">>> Discovered $($spNames.Count) stored procedures: $($spNames -join ', ')" -ForegroundColor Green

# Common idempotent teardown block
$idempotentCleanupSql = @"
-- =========================================================================================
-- TARGET DATABASE SAFETY GUARD
-- Strict safety controls to prevent accidental overwriting or data loss:
-- 1. Prevents execution against system databases (master, tempdb, model, msdb).
-- 2. Prevents execution against protected production & QA databases.
-- 3. Prevents execution against non-empty databases (must NOT automatically drop tables/data).
-- 4. Fails fast with severity 20 (terminating connection immediately).
-- =========================================================================================
IF DB_NAME() IN (N'master', N'tempdb', N'model', N'msdb')
BEGIN
    DECLARE @sysErr NVARCHAR(500) = N'FATAL SAFETY ERROR: Script cannot be executed against system database [' + DB_NAME() + N']. Please specify a valid empty target database: sqlcmd -d <TargetDatabase> -b -i <script.sql>';
    RAISERROR(@sysErr, 20, 1) WITH LOG;
END;

IF LOWER(DB_NAME()) IN (
    N'dentalclinicmanagementdb',
    N'dentalcare_integration_dev',
    N'dentalcare_release_clean_qa',
    N'dentalcare_restore_verification_qa'
)
BEGIN
    DECLARE @protErr NVARCHAR(500) = N'FATAL SAFETY ERROR: Target database [' + DB_NAME() + N'] is a protected database. Direct execution of SQL Master installation script against protected databases is strictly prohibited to prevent data loss.';
    RAISERROR(@protErr, 20, 1) WITH LOG;
END;

IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
BEGIN
    DECLARE @tblCount INT;
    SELECT @tblCount = COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0;
    DECLARE @nonEmptyErr NVARCHAR(500) = N'FATAL SAFETY ERROR: Target database [' + DB_NAME() + N'] is not empty (contains ' + CAST(@tblCount AS NVARCHAR(10)) + N' existing user tables). SQL Master installation requires an empty target database and will never automatically drop tables or data on a non-empty database. Please create a clean empty database or specify an empty database: sqlcmd -d <EmptyDatabase> -b -i <script.sql>';
    RAISERROR(@nonEmptyErr, 20, 1) WITH LOG;
END;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
"@

# ------------------------------------------------------------------------------
# 1. GENERATE CLEAN SCHEMA FILE: Dental_Clinic_Final_Database_Clean.sql
# ------------------------------------------------------------------------------
Write-Host ">>> Generating clean schema (Dental_Clinic_Final_Database_Clean.sql)..." -ForegroundColor Cyan

$schemaScripter = New-Object Microsoft.SqlServer.Management.Smo.Scripter($srv)
$schemaScripter.Options.ScriptSchema = $true
$schemaScripter.Options.ScriptData = $false
$schemaScripter.Options.Indexes = $true
$schemaScripter.Options.ClusteredIndexes = $true
$schemaScripter.Options.NonClusteredIndexes = $true
$schemaScripter.Options.DriAllConstraints = $true
$schemaScripter.Options.DriAllKeys = $true
$schemaScripter.Options.Triggers = $true
$schemaScripter.Options.IncludeHeaders = $false
$schemaScripter.Options.ScriptDrops = $false
$schemaScripter.Options.AnsiPadding = $true
$schemaScripter.Options.SchemaQualify = $true

$schemaBuilder = New-Object System.Text.StringBuilder
[void]$schemaBuilder.AppendLine("-- =========================================================================================")
[void]$schemaBuilder.AppendLine("-- DENTAL CLINIC MANAGEMENT SYSTEM (SWP391 GROUP 6)")
[void]$schemaBuilder.AppendLine("-- MASTER CLEAN DATABASE SCHEMA (44 TABLES, VIEWS, STORED PROCEDURES)")
[void]$schemaBuilder.AppendLine("-- Target: Microsoft SQL Server 2017 / 2019 / 2022 / LocalDB / SQLEXPRESS")
[void]$schemaBuilder.AppendLine("-- Database Name: DentalClinicManagementDB")
[void]$schemaBuilder.AppendLine("-- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$schemaBuilder.AppendLine("-- =========================================================================================")
[void]$schemaBuilder.AppendLine("")
[void]$schemaBuilder.AppendLine($idempotentCleanupSql)
[void]$schemaBuilder.AppendLine("")
[void]$schemaBuilder.AppendLine("-- =========================================================================================")
[void]$schemaBuilder.AppendLine("-- PART 1: 44 DDL TABLE SCHEMAS, CONSTRAINTS & INDEXES")
[void]$schemaBuilder.AppendLine("-- =========================================================================================")
[void]$schemaBuilder.AppendLine("")

$schemaLines = $schemaScripter.EnumScript($tables)
foreach ($line in $schemaLines) {
    [void]$schemaBuilder.AppendLine($line)
    [void]$schemaBuilder.AppendLine("GO")
}

# Script Views
if ($viewNames.Count -gt 0) {
    [void]$schemaBuilder.AppendLine("")
    [void]$schemaBuilder.AppendLine("-- =========================================================================================")
    [void]$schemaBuilder.AppendLine("-- PART 2: DATABASE VIEWS")
    [void]$schemaBuilder.AppendLine("-- =========================================================================================")
    [void]$schemaBuilder.AppendLine("")
    foreach ($vn in $viewNames) {
        $viewObj = $db.Views[$vn, "dbo"]
        if ($viewObj) {
            $vLines = $schemaScripter.EnumScript(@($viewObj))
            foreach ($vl in $vLines) {
                [void]$schemaBuilder.AppendLine($vl)
                [void]$schemaBuilder.AppendLine("GO")
            }
        }
    }
}

# Script Stored Procedures
if ($spNames.Count -gt 0) {
    [void]$schemaBuilder.AppendLine("")
    [void]$schemaBuilder.AppendLine("-- =========================================================================================")
    [void]$schemaBuilder.AppendLine("-- PART 3: STORED PROCEDURES")
    [void]$schemaBuilder.AppendLine("-- =========================================================================================")
    [void]$schemaBuilder.AppendLine("")
    foreach ($spn in $spNames) {
        $spObj = $db.StoredProcedures[$spn, "dbo"]
        if ($spObj) {
            $spLines = $schemaScripter.EnumScript(@($spObj))
            foreach ($sl in $spLines) {
                [void]$schemaBuilder.AppendLine($sl)
                [void]$schemaBuilder.AppendLine("GO")
            }
        }
    }
}

[void]$schemaBuilder.AppendLine("")
[void]$schemaBuilder.AppendLine("PRINT N'>>> DENTAL CLINIC CLEAN SCHEMA INITIALIZED SUCCESSFULLY (44 TABLES, VIEWS & PROCEDURES)!';")
[void]$schemaBuilder.AppendLine("GO")

$cleanSchemaPath = Join-Path $OutputDir "Dental_Clinic_Final_Database_Clean.sql"
$utf8Encoding = New-Object System.Text.UTF8Encoding($true) # UTF-8 with BOM
[System.IO.File]::WriteAllText($cleanSchemaPath, $schemaBuilder.ToString(), $utf8Encoding)
Write-Host ">>> Wrote: $cleanSchemaPath ($((Get-Item $cleanSchemaPath).Length) bytes)" -ForegroundColor Green

# ------------------------------------------------------------------------------
# 2. GENERATE FULL DB & DATA FILE: DentalClinic_Full_Database_And_Data.sql
# ------------------------------------------------------------------------------
Write-Host ">>> Generating full database & seed data (DentalClinic_Full_Database_And_Data.sql)..." -ForegroundColor Cyan

$dataScripter = New-Object Microsoft.SqlServer.Management.Smo.Scripter($srv)
$dataScripter.Options.ScriptSchema = $false
$dataScripter.Options.ScriptData = $true
$dataScripter.Options.AnsiPadding = $true
$dataScripter.Options.SchemaQualify = $true

$fullBuilder = New-Object System.Text.StringBuilder
[void]$fullBuilder.AppendLine("-- =========================================================================================")
[void]$fullBuilder.AppendLine("-- DENTAL CLINIC MANAGEMENT SYSTEM (SWP391 GROUP 6)")
[void]$fullBuilder.AppendLine("-- FULL MASTER DATABASE SCRIPT: SCHEMA + COMPLETE SEED DATASET (44 TABLES)")
[void]$fullBuilder.AppendLine("-- Target: Microsoft SQL Server 2017 / 2019 / 2022 / LocalDB / SQLEXPRESS")
[void]$fullBuilder.AppendLine("-- Database Name: DentalClinicManagementDB")
[void]$fullBuilder.AppendLine("-- Shared Demo Password for ALL Accounts: Password123@")
[void]$fullBuilder.AppendLine("-- Key Demo Accounts:")
[void]$fullBuilder.AppendLine("--   Admin:                 admin@dentalcare.com")
[void]$fullBuilder.AppendLine("--   Department Manager:    manager.tongquat@dentalcare.com / manager@dentalcare.com")
[void]$fullBuilder.AppendLine("--   Dentist:               dentist.hung@dentalcare.com / dentist@dentalcare.com")
[void]$fullBuilder.AppendLine("--   Receptionist:          receptionist@dentalcare.com")
[void]$fullBuilder.AppendLine("--   Patient:               patient.tuan@gmail.com / patient1@dentalcare.com")
[void]$fullBuilder.AppendLine("-- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$fullBuilder.AppendLine("-- =========================================================================================")
[void]$fullBuilder.AppendLine("")
[void]$fullBuilder.AppendLine($idempotentCleanupSql)
[void]$fullBuilder.AppendLine("")
[void]$fullBuilder.AppendLine("-- =========================================================================================")
[void]$fullBuilder.AppendLine("-- PART 1: 44 DDL TABLE SCHEMAS, CONSTRAINTS & INDEXES")
[void]$fullBuilder.AppendLine("-- =========================================================================================")
[void]$fullBuilder.AppendLine("")

foreach ($line in $schemaLines) {
    [void]$fullBuilder.AppendLine($line)
    [void]$fullBuilder.AppendLine("GO")
}

# Script Views
if ($viewNames.Count -gt 0) {
    [void]$fullBuilder.AppendLine("")
    [void]$fullBuilder.AppendLine("-- =========================================================================================")
    [void]$fullBuilder.AppendLine("-- PART 2: DATABASE VIEWS")
    [void]$fullBuilder.AppendLine("-- =========================================================================================")
    [void]$fullBuilder.AppendLine("")
    foreach ($vn in $viewNames) {
        $viewObj = $db.Views[$vn, "dbo"]
        if ($viewObj) {
            $vLines = $schemaScripter.EnumScript(@($viewObj))
            foreach ($vl in $vLines) {
                [void]$fullBuilder.AppendLine($vl)
                [void]$fullBuilder.AppendLine("GO")
            }
        }
    }
}

# Script Stored Procedures
if ($spNames.Count -gt 0) {
    [void]$fullBuilder.AppendLine("")
    [void]$fullBuilder.AppendLine("-- =========================================================================================")
    [void]$fullBuilder.AppendLine("-- PART 3: STORED PROCEDURES")
    [void]$fullBuilder.AppendLine("-- =========================================================================================")
    [void]$fullBuilder.AppendLine("")
    foreach ($spn in $spNames) {
        $spObj = $db.StoredProcedures[$spn, "dbo"]
        if ($spObj) {
            $spLines = $schemaScripter.EnumScript(@($spObj))
            foreach ($sl in $spLines) {
                [void]$fullBuilder.AppendLine($sl)
                [void]$fullBuilder.AppendLine("GO")
            }
        }
    }
}

[void]$fullBuilder.AppendLine("")
[void]$fullBuilder.AppendLine("-- =========================================================================================")
[void]$fullBuilder.AppendLine("-- PART 4: COMPLETE SYSTEM DATASET SEEDING (44 TABLES, UTF-8 ACCENTED VIETNAMESE)")
[void]$fullBuilder.AppendLine("-- =========================================================================================")
[void]$fullBuilder.AppendLine("")
[void]$fullBuilder.AppendLine("-- Disable foreign key checks during data loading")
[void]$fullBuilder.AppendLine("EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all';")
[void]$fullBuilder.AppendLine("GO")
[void]$fullBuilder.AppendLine("")

foreach ($tbl in $tables) {
    $tblName = $tbl.Name
    $dataLines = $dataScripter.EnumScript(@($tbl))
    $insertLines = $dataLines | Where-Object { $_ -like "INSERT*" }

    if ($insertLines.Count -gt 0) {
        Write-Host "  -> Scripting $tblName ($($insertLines.Count) rows)..." -ForegroundColor Gray
        [void]$fullBuilder.AppendLine("-- -----------------------------------------------------------------------------------------")
        [void]$fullBuilder.AppendLine("-- Data for [$tblName] ($($insertLines.Count) rows)")
        [void]$fullBuilder.AppendLine("-- -----------------------------------------------------------------------------------------")
        
        $hasIdentity = $false
        foreach ($col in $tbl.Columns) {
            if ($col.Identity) {
                $hasIdentity = $true
                break
            }
        }

        if ($hasIdentity) {
            [void]$fullBuilder.AppendLine("SET IDENTITY_INSERT [dbo].[$tblName] ON;")
        }

        foreach ($ins in $insertLines) {
            [void]$fullBuilder.AppendLine($ins)
        }

        if ($hasIdentity) {
            [void]$fullBuilder.AppendLine("SET IDENTITY_INSERT [dbo].[$tblName] OFF;")
        }

        [void]$fullBuilder.AppendLine("GO")
        [void]$fullBuilder.AppendLine("")
    }
}

[void]$fullBuilder.AppendLine("-- Re-enable and verify all foreign key constraints")
[void]$fullBuilder.AppendLine("EXEC sp_msforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all';")
[void]$fullBuilder.AppendLine("GO")
[void]$fullBuilder.AppendLine("")
[void]$fullBuilder.AppendLine("PRINT N'>>> DENTAL CLINIC DATABASE & DATA SUCCESSFULLY INITIALIZED 100% (44 TABLES)!';")
[void]$fullBuilder.AppendLine("GO")

$fullSqlPath = Join-Path $OutputDir "DentalClinic_Full_Database_And_Data.sql"
[System.IO.File]::WriteAllText($fullSqlPath, $fullBuilder.ToString(), $utf8Encoding)
Write-Host ">>> Wrote: $fullSqlPath ($((Get-Item $fullSqlPath).Length) bytes)" -ForegroundColor Green

# ------------------------------------------------------------------------------
# 3. GENERATE SQL SERVER BACKUP: DentalClinicManagementDB.bak
# ------------------------------------------------------------------------------
Write-Host ">>> Creating compressed SQL Server backup (DentalClinicManagementDB.bak)..." -ForegroundColor Cyan
$bakPath = Join-Path $OutputDir "DentalClinicManagementDB.bak"
$backupSql = "BACKUP DATABASE [DentalClinicManagementDB] TO DISK = N'$bakPath' WITH FORMAT, INIT, COMPRESSION, NAME = N'DentalClinicManagementDB-Full Database Backup';"
$srv.Databases['master'].ExecuteNonQuery($backupSql)
Write-Host ">>> Wrote: $bakPath ($((Get-Item $bakPath).Length) bytes)" -ForegroundColor Green

# Verify backup integrity
$verifySql = "RESTORE VERIFYONLY FROM DISK = N'$bakPath';"
$srv.Databases['master'].ExecuteNonQuery($verifySql)
Write-Host ">>> Backup verification passed: DentalClinicManagementDB.bak is valid and healthy." -ForegroundColor Green

Write-Host ">>> All standardized database files generated and verified successfully!" -ForegroundColor Green
