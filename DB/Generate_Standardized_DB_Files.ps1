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
-- IDEMPOTENT INITIALIZATION & SAFE CLEANUP
-- Drops foreign keys, views, stored procedures, and tables if they already exist,
-- ensuring safe, conflict-free re-runs on any machine or existing database.
-- =========================================================================================
IF DB_ID(N'DentalClinicManagementDB') IS NULL
BEGIN
    CREATE DATABASE DentalClinicManagementDB COLLATE SQL_Latin1_General_CP1_CI_AS;
END;
GO

USE DentalClinicManagementDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- 1. Drop all Foreign Key Constraints
DECLARE @dropFkSql NVARCHAR(MAX) = N'';
SELECT @dropFkSql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(13)
FROM sys.foreign_keys fk
JOIN sys.tables t ON fk.parent_object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id;
IF LEN(@dropFkSql) > 0 EXEC sp_executesql @dropFkSql;

-- 2. Drop all Views
DECLARE @dropViewSql NVARCHAR(MAX) = N'';
SELECT @dropViewSql += N'DROP VIEW ' + QUOTENAME(s.name) + N'.' + QUOTENAME(v.name) + N';' + CHAR(13)
FROM sys.views v
JOIN sys.schemas s ON v.schema_id = s.schema_id
WHERE v.is_ms_shipped = 0;
IF LEN(@dropViewSql) > 0 EXEC sp_executesql @dropViewSql;

-- 3. Drop all Stored Procedures
DECLARE @dropSpSql NVARCHAR(MAX) = N'';
SELECT @dropSpSql += N'DROP PROCEDURE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(p.name) + N';' + CHAR(13)
FROM sys.procedures p
JOIN sys.schemas s ON p.schema_id = s.schema_id
WHERE p.is_ms_shipped = 0;
IF LEN(@dropSpSql) > 0 EXEC sp_executesql @dropSpSql;

-- 4. Drop all Tables
DECLARE @dropTableSql NVARCHAR(MAX) = N'';
SELECT @dropTableSql += N'DROP TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';' + CHAR(13)
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name <> N'__EFMigrationsHistory';
IF LEN(@dropTableSql) > 0 EXEC sp_executesql @dropTableSql;
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
