-- =====================================================================================
-- DENTALCARE SWP391 - PRE & POST MIGRATION VALIDATION QUERIES
-- Purpose: Independent DBA / CI-CD Quality Gate Verification
-- Baseline: RC2 (72 Tables, 803 Columns, 180 FKs)
-- Target:   Target Schema (54 Tables, 553 Columns, 122 FKs)
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =====================================================================================
-- SECTION 1: PRE-MIGRATION READINESS AUDIT (Run before running migration)
-- =====================================================================================
PRINT N'====================================================================================';
PRINT N'>>> EXECUTING PRE-MIGRATION READINESS AUDIT...';
PRINT N'====================================================================================';

-- 1.1. Check if database is currently RC2
SELECT 
    CASE 
        WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = N'DentistLeaveRequests')
         AND EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ReceptionistLeaveRequests')
         AND COL_LENGTH('dbo.Appointments', 'ComboCode') IS NOT NULL
         AND COL_LENGTH('dbo.Departments', 'ManagerUserId') IS NOT NULL
        THEN N'[PASS] Database is verified as RC2 baseline ready for migration.'
        ELSE N'[FAIL] Database does NOT match RC2 baseline specification!'
    END AS [PreMigration_BaselineCheck];

-- 1.2. Check Lead Dentist clinical profile existence
SELECT 
    d.[DepartmentId],
    d.[Name] AS [DepartmentName],
    d.[ManagerUserId],
    CASE 
        WHEN dp.[UserId] IS NOT NULL THEN N'[PASS] Clinical profile verified in DentistProfiles'
        ELSE N'[FAIL] User is NOT registered in DentistProfiles! Must resolve before migrating.'
    END AS [LeadDentist_IntegrityStatus]
FROM [dbo].[Departments] d
LEFT JOIN [dbo].[DentistProfiles] dp ON d.[ManagerUserId] = dp.[UserId];

-- 1.3. Pre-migration row counts snapshot
SELECT 
    'DentistLeaveRequests' AS [TableName], COUNT(*) AS [RowCount] FROM [dbo].[DentistLeaveRequests]
UNION ALL
SELECT 'ReceptionistLeaveRequests', COUNT(*) FROM [dbo].[ReceptionistLeaveRequests]
UNION ALL
SELECT 'ReceptionistLeaveRequestPeriods', COUNT(*) FROM [dbo].[ReceptionistLeaveRequestPeriods]
UNION ALL
SELECT 'AppointmentDisruptions', COUNT(*) FROM [dbo].[AppointmentDisruptions]
UNION ALL
SELECT 'Departments', COUNT(*) FROM [dbo].[Departments]
UNION ALL
SELECT 'Appointments', COUNT(*) FROM [dbo].[Appointments]
UNION ALL
SELECT 'TreatmentPlans', COUNT(*) FROM [dbo].[TreatmentPlans]
UNION ALL
SELECT 'TreatmentPlanItems', COUNT(*) FROM [dbo].[TreatmentPlanItems]
UNION ALL
SELECT 'InvoiceItems', COUNT(*) FROM [dbo].[InvoiceItems];
GO

-- =====================================================================================
-- SECTION 2: POST-MIGRATION INTEGRITY AUDIT (Run after running migration)
-- =====================================================================================
PRINT N'====================================================================================';
PRINT N'>>> EXECUTING POST-MIGRATION INTEGRITY AUDIT...';
PRINT N'====================================================================================';

-- 2.1. Structural Metrics Audit
SELECT 
    (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0) AS [ActualTables],
    54 AS [ExpectedTables],
    CASE WHEN (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0) = 54 THEN N'PASS' ELSE N'FAIL' END AS [TableStatus],

    (SELECT COUNT(*) FROM sys.columns c INNER JOIN sys.tables t ON c.object_id = t.object_id WHERE t.is_ms_shipped = 0) AS [ActualColumns],
    553 AS [ExpectedColumns],
    CASE WHEN (SELECT COUNT(*) FROM sys.columns c INNER JOIN sys.tables t ON c.object_id = t.object_id WHERE t.is_ms_shipped = 0) = 553 THEN N'PASS' ELSE N'FAIL' END AS [ColumnStatus],

    (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_ms_shipped = 0) AS [ActualForeignKeys],
    122 AS [ExpectedForeignKeys],
    CASE WHEN (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_ms_shipped = 0) = 122 THEN N'PASS' ELSE N'FAIL' END AS [FkStatus],

    (SELECT COUNT(*) FROM sys.key_constraints WHERE type = 'UQ' AND is_ms_shipped = 0) AS [ActualUniqueConstraints],
    22 AS [ExpectedUniqueConstraints],
    CASE WHEN (SELECT COUNT(*) FROM sys.key_constraints WHERE type = 'UQ' AND is_ms_shipped = 0) = 22 THEN N'PASS' ELSE N'FAIL' END AS [UniqueConstraintStatus],

    (SELECT COUNT(*) FROM sys.indexes WHERE is_unique = 1 AND is_primary_key = 0 AND is_unique_constraint = 0 AND object_id IN (SELECT object_id FROM sys.tables WHERE is_ms_shipped = 0)) AS [ActualUniqueIndexes],
    11 AS [ExpectedUniqueIndexes],
    CASE WHEN (SELECT COUNT(*) FROM sys.indexes WHERE is_unique = 1 AND is_primary_key = 0 AND is_unique_constraint = 0 AND object_id IN (SELECT object_id FROM sys.tables WHERE is_ms_shipped = 0)) = 11 THEN N'PASS' ELSE N'FAIL' END AS [UniqueIndexStatus];

-- 2.2. Decommissioned Legacy Objects Verification (Must return 0 rows)
SELECT 
    t.[name] AS [LegacyTable],
    N'ERROR: Table still exists!' AS [Status]
FROM sys.tables t
WHERE t.name IN (
    N'DentistLeaveRequests', N'ReceptionistLeaveRequests', N'ReceptionistLeaveRequestPeriods',
    N'ImagingServiceConfigs', N'ImagingRecords', N'BlogPostMedia',
    N'StaffAttendanceCorrectionRequests', N'StaffAttendanceEvents', N'StaffAttendanceRecords',
    N'AttendancePeriodClosures', N'AttendancePolicies',
    N'ReceptionistShiftSwapRequests', N'ReceptionistWorkHandovers', N'ReceptionistCoverageIncidents',
    N'ImagingSessions', N'XrayDentistRosters', N'ImagingEquipments',
    N'EpisodeServiceTaskStepProgresses', N'EpisodeServiceTasks', N'CareEpisodes'
);

-- 2.3. Removed Column Verification
SELECT 
    [CheckedColumn],
    CASE WHEN [IsRemoved] = 1 THEN N'PASS (Column successfully removed)' ELSE N'FAIL (Column still exists)' END AS [Status]
FROM (
    VALUES
        (N'Appointments.ComboCode', CASE WHEN COL_LENGTH('dbo.Appointments', 'ComboCode') IS NULL THEN 1 ELSE 0 END),
        (N'Appointments.CareEpisodeId', CASE WHEN COL_LENGTH('dbo.Appointments', 'CareEpisodeId') IS NULL THEN 1 ELSE 0 END),
        (N'Invoices.CareEpisodeId', CASE WHEN COL_LENGTH('dbo.Invoices', 'CareEpisodeId') IS NULL THEN 1 ELSE 0 END),
        (N'InvoiceItems.ServiceTaskId', CASE WHEN COL_LENGTH('dbo.InvoiceItems', 'ServiceTaskId') IS NULL THEN 1 ELSE 0 END),
        (N'DepartmentReferrals.CareEpisodeId', CASE WHEN COL_LENGTH('dbo.DepartmentReferrals', 'CareEpisodeId') IS NULL THEN 1 ELSE 0 END),
        (N'DiagnosticResults.ImagingSessionId', CASE WHEN COL_LENGTH('dbo.DiagnosticResults', 'ImagingSessionId') IS NULL THEN 1 ELSE 0 END),
        (N'DiagnosticResults.EquipmentId', CASE WHEN COL_LENGTH('dbo.DiagnosticResults', 'EquipmentId') IS NULL THEN 1 ELSE 0 END),
        (N'UserAccounts.EmailVerifiedAt', CASE WHEN COL_LENGTH('dbo.UserAccounts', 'EmailVerifiedAt') IS NULL THEN 1 ELSE 0 END),
        (N'PatientProfiles.PatientCode', CASE WHEN COL_LENGTH('dbo.PatientProfiles', 'PatientCode') IS NULL THEN 1 ELSE 0 END),
        (N'StaffProfiles.EmployeeCode', CASE WHEN COL_LENGTH('dbo.StaffProfiles', 'EmployeeCode') IS NULL THEN 1 ELSE 0 END)
) AS ColCheck([CheckedColumn], [IsRemoved]);

-- 2.4. Renamed Column & Lead Dentist FK Verification
SELECT 
    N'Departments.LeadDentistUserId' AS [CheckedColumn],
    CASE WHEN COL_LENGTH('dbo.Departments', 'LeadDentistUserId') IS NOT NULL THEN N'PASS (Column exists)' ELSE N'FAIL' END AS [ColumnStatus],
    CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Departments_LeadDentist') THEN N'PASS (FK points to DentistProfiles)' ELSE N'FAIL' END AS [FkStatus];

-- 2.5. Redundant / Retired FK Elimination Verification (Must return 0 rows)
SELECT 
    fk.[name] AS [RedundantFk],
    N'ERROR: Redundant or retired FK still exists!' AS [Status]
FROM sys.foreign_keys fk
WHERE fk.name IN (
    N'FK_CareEpisodes_RootAppointment',
    N'FK_Appointments_CareEpisodes',
    N'FK_Appointments_CareEpisode_Patient_Composite',
    N'FK_Invoices_CareEpisodes',
    N'FK_InvoiceItems_EpisodeServiceTasks',
    N'FK_DepartmentReferrals_CareEpisodes',
    N'FK_DiagnosticResults_ImagingSessions',
    N'FK_DiagnosticResults_ImagingEquipments'
);

-- 2.6. Treatment Status Constraints Verification
SELECT 
    'TreatmentPlans' AS [Entity],
    COUNT(*) AS [TotalPlans],
    COUNT(CASE WHEN [Status] = 'Draft' THEN 1 END) AS [DraftCount],
    COUNT(CASE WHEN [Status] = 'Planned' THEN 1 END) AS [PlannedCount],
    COUNT(CASE WHEN [Status] = 'Active' THEN 1 END) AS [ActiveCount],
    COUNT(CASE WHEN [Status] = 'InProgress' THEN 1 END) AS [InProgressCount],
    COUNT(CASE WHEN [Status] = 'Completed' THEN 1 END) AS [CompletedCount],
    COUNT(CASE WHEN [Status] = 'Paused' THEN 1 END) AS [PausedCount],
    COUNT(CASE WHEN [Status] = 'Cancelled' THEN 1 END) AS [CancelledCount],
    COUNT(CASE WHEN [Status] NOT IN ('Draft', 'Planned', 'Active', 'InProgress', 'Completed', 'Paused', 'Cancelled') THEN 1 END) AS [InvalidStatusCount]
FROM [dbo].[TreatmentPlans];

SELECT 
    'TreatmentPlanItems' AS [Entity],
    COUNT(*) AS [TotalItems],
    COUNT(CASE WHEN [Status] = 'Planned' THEN 1 END) AS [PlannedCount],
    COUNT(CASE WHEN [Status] = 'Pending' THEN 1 END) AS [PendingCount],
    COUNT(CASE WHEN [Status] = 'Scheduled' THEN 1 END) AS [ScheduledCount],
    COUNT(CASE WHEN [Status] = 'InProgress' THEN 1 END) AS [InProgressCount],
    COUNT(CASE WHEN [Status] = 'Completed' THEN 1 END) AS [CompletedCount],
    COUNT(CASE WHEN [Status] = 'Skipped' THEN 1 END) AS [SkippedCount],
    COUNT(CASE WHEN [Status] = 'Cancelled' THEN 1 END) AS [CancelledCount],
    COUNT(CASE WHEN [Status] NOT IN ('Planned', 'Pending', 'Scheduled', 'InProgress', 'Completed', 'Skipped', 'Cancelled') THEN 1 END) AS [InvalidStatusCount]
FROM [dbo].[TreatmentPlanItems];

-- 2.7. Invoice Items Deduplication & Source Integrity Verification
SELECT 
    COUNT(*) AS [TotalInvoiceItems],
    COUNT(CASE WHEN [PerformedServiceId] IS NOT NULL AND [ServiceTaskId] IS NOT NULL THEN 1 END) AS [ViolatingMutualExclusionCount],
    COUNT(CASE WHEN [PerformedServiceId] IS NOT NULL AND [SourceType] IS NOT NULL AND [SourceType] <> 'PerformedService' THEN 1 END) AS [ViolatingPerfSourceTypeCount],
    COUNT(CASE WHEN [ServiceTaskId] IS NOT NULL AND [SourceType] IS NOT NULL AND [SourceType] <> 'ServiceTask' THEN 1 END) AS [ViolatingTaskSourceTypeCount]
FROM [dbo].[InvoiceItems];

-- 2.8. Orphan Foreign Key Detection
SELECT 
    N'AppointmentDisruptions.DentistLeaveRequestId -> StaffLeaveRequests' AS [Relationship],
    COUNT(*) AS [OrphanCount]
FROM [dbo].[AppointmentDisruptions] ad
LEFT JOIN [dbo].[StaffLeaveRequests] slr ON ad.[DentistLeaveRequestId] = slr.[LeaveRequestId]
WHERE ad.[DentistLeaveRequestId] IS NOT NULL AND slr.[LeaveRequestId] IS NULL
UNION ALL
SELECT 
    N'Departments.LeadDentistUserId -> DentistProfiles' AS [Relationship],
    COUNT(*) AS [OrphanCount]
FROM [dbo].[Departments] d
LEFT JOIN [dbo].[DentistProfiles] dp ON d.[LeadDentistUserId] = dp.[UserId]
WHERE d.[LeadDentistUserId] IS NOT NULL AND dp.[UserId] IS NULL;

-- 2.9. User Identity & Account Status Integrity Verification
SELECT 
    COUNT(CASE WHEN [Status] = 'Unverified' THEN 1 END) AS [UnverifiedAccountCount],
    CASE WHEN COUNT(CASE WHEN [Status] = 'Unverified' THEN 1 END) = 0 THEN N'PASS (Zero unverified accounts)' ELSE N'FAIL' END AS [StatusAudit],
    COUNT(CASE WHEN [Status] NOT IN ('Active', 'Inactive', 'Locked') THEN 1 END) AS [InvalidStatusCount],
    CASE WHEN COUNT(CASE WHEN [Status] NOT IN ('Active', 'Inactive', 'Locked') THEN 1 END) = 0 THEN N'PASS (All accounts in valid states)' ELSE N'FAIL' END AS [StateIntegrityAudit]
FROM [dbo].[UserAccounts];
GO
