# ==============================================================================
# Script: Generate_Standardized_DB_Files.ps1
# Purpose: Exports clean schema and complete seed dataset directly from the
#          live, verified DentalClinicManagementDB instance using SMO.
# Output:
#   1. DB\Dental_Clinic_Final_Database_Clean.sql (Schema Only: 44 tables, constraints, indexes)
#   2. DB\DentalClinic_Full_Database_And_Data.sql (Full DB: Schema + Complete UTF-8 Data)
#   3. DB\DentalClinicManagementDB.bak (Standard SQL Server compressed backup)
# ==============================================================================

[CmdletBinding()]
param (
    [string]$ServerInstance = ".\MSSQLSERVER01",
    [string]$DatabaseName = "DentalClinicManagementDB",
    [string]$OutputDir = "D:\SWP\SWP391_Group6\DB"
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

# 44 tables in topological dependency order
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
[void]$schemaBuilder.AppendLine("-- MASTER CLEAN DATABASE SCHEMA (SCHEMA ONLY - 44 TABLES)")
[void]$schemaBuilder.AppendLine("-- Target: Microsoft SQL Server 2017 / 2019 / 2022 / LocalDB / SQLEXPRESS")
[void]$schemaBuilder.AppendLine("-- Database Name: DentalClinicManagementDB")
[void]$schemaBuilder.AppendLine("-- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$schemaBuilder.AppendLine("-- =========================================================================================")
[void]$schemaBuilder.AppendLine("")
[void]$schemaBuilder.AppendLine("IF DB_ID(N'DentalClinicManagementDB') IS NULL")
[void]$schemaBuilder.AppendLine("BEGIN")
[void]$schemaBuilder.AppendLine("    CREATE DATABASE DentalClinicManagementDB;")
[void]$schemaBuilder.AppendLine("END;")
[void]$schemaBuilder.AppendLine("GO")
[void]$schemaBuilder.AppendLine("")
[void]$schemaBuilder.AppendLine("USE DentalClinicManagementDB;")
[void]$schemaBuilder.AppendLine("GO")
[void]$schemaBuilder.AppendLine("")
[void]$schemaBuilder.AppendLine("SET ANSI_NULLS ON;")
[void]$schemaBuilder.AppendLine("SET QUOTED_IDENTIFIER ON;")
[void]$schemaBuilder.AppendLine("GO")
[void]$schemaBuilder.AppendLine("")

$schemaLines = $schemaScripter.EnumScript($tables)
foreach ($line in $schemaLines) {
    [void]$schemaBuilder.AppendLine($line)
    [void]$schemaBuilder.AppendLine("GO")
}

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
[void]$fullBuilder.AppendLine("-- FULL DATABASE SCHEMA & COMPLETE VERIFIED SEED DATASET (39 TABLES)")
[void]$fullBuilder.AppendLine("-- Target: Microsoft SQL Server 2017 / 2019 / 2022 / LocalDB / SQLEXPRESS")
[void]$fullBuilder.AppendLine("-- Database Name: DentalClinicManagementDB")
[void]$fullBuilder.AppendLine("-- Shared Demo Password for ALL Accounts: Password123@")
[void]$fullBuilder.AppendLine("-- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$fullBuilder.AppendLine("-- =========================================================================================")
[void]$fullBuilder.AppendLine("")
[void]$fullBuilder.AppendLine("IF DB_ID(N'DentalClinicManagementDB') IS NULL")
[void]$fullBuilder.AppendLine("BEGIN")
[void]$fullBuilder.AppendLine("    CREATE DATABASE DentalClinicManagementDB;")
[void]$fullBuilder.AppendLine("END;")
[void]$fullBuilder.AppendLine("GO")
[void]$fullBuilder.AppendLine("")
[void]$fullBuilder.AppendLine("USE DentalClinicManagementDB;")
[void]$fullBuilder.AppendLine("GO")
[void]$fullBuilder.AppendLine("")
[void]$fullBuilder.AppendLine("SET ANSI_NULLS ON;")
[void]$fullBuilder.AppendLine("SET QUOTED_IDENTIFIER ON;")
[void]$fullBuilder.AppendLine("GO")
[void]$fullBuilder.AppendLine("")
[void]$fullBuilder.AppendLine("-- =========================================================================================")
[void]$fullBuilder.AppendLine("-- PART 1: DDL TABLE SCHEMAS, CONSTRAINTS & INDEXES")
[void]$fullBuilder.AppendLine("-- =========================================================================================")
[void]$fullBuilder.AppendLine("")

foreach ($line in $schemaLines) {
    [void]$fullBuilder.AppendLine($line)
    [void]$fullBuilder.AppendLine("GO")
}

[void]$fullBuilder.AppendLine("")
[void]$fullBuilder.AppendLine("-- =========================================================================================")
[void]$fullBuilder.AppendLine("-- PART 2: COMPLETE SYSTEM DATASET SEEDING (44 TABLES, UTF-8 ACCENTED VIETNAMESE)")
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
[void]$fullBuilder.AppendLine("PRINT N'>>> DENTAL CLINIC DATABASE & DATA SUCCESSFULLY INITIALIZED 100%!';")
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

Write-Host ">>> All standardized database files generated successfully!" -ForegroundColor Green
