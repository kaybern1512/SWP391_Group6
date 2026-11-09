-- ============================================================================
-- DENTALCARE - RECEPTIONIST SHIFT, LEAVE & WORKFORCE MANAGEMENT MIGRATION ROLLBACK
-- ============================================================================

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistWorkHandovers')
    DROP TABLE dbo.ReceptionistWorkHandovers;
GO

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistCoverageIncidents')
    DROP TABLE dbo.ReceptionistCoverageIncidents;
GO

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistShiftSwapRequests')
    DROP TABLE dbo.ReceptionistShiftSwapRequests;
GO

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistLeaveRequestPeriods')
    DROP TABLE dbo.ReceptionistLeaveRequestPeriods;
GO

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistLeaveRequests')
    DROP TABLE dbo.ReceptionistLeaveRequests;
GO

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistWorkSchedules')
    DROP TABLE dbo.ReceptionistWorkSchedules;
GO

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistShiftTemplates')
    DROP TABLE dbo.ReceptionistShiftTemplates;
GO
