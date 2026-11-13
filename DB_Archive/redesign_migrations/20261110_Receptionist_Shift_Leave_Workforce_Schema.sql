-- ============================================================================
-- DENTALCARE - RECEPTIONIST SHIFT, LEAVE & WORKFORCE MANAGEMENT MIGRATION
-- Migration Date: 2026-11-10
-- Target Database: DentalCare_ReceptionistShift_Dev (or current tenant DB)
-- Description:
--   Creates dedicated tables, indexes, constraints, and initial shift templates
--   for Receptionist workforce scheduling, leave requests, shift swapping,
--   emergency incidents, and work handovers.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. ReceptionistShiftTemplates
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistShiftTemplates')
BEGIN
    CREATE TABLE dbo.ReceptionistShiftTemplates (
        ShiftTemplateId BIGINT IDENTITY(1,1) NOT NULL,
        TemplateName NVARCHAR(100) NOT NULL,
        StartTime TIME(0) NOT NULL,
        EndTime TIME(0) NOT NULL,
        MinStaffRequired INT NOT NULL CONSTRAINT DF_ReceptionistShiftTemplates_MinStaff DEFAULT (2),
        IsActive BIT NOT NULL CONSTRAINT DF_ReceptionistShiftTemplates_IsActive DEFAULT (1),
        DisplayOrder INT NOT NULL CONSTRAINT DF_ReceptionistShiftTemplates_DisplayOrder DEFAULT (1),
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ReceptionistShiftTemplates_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2(7) NULL,
        CONSTRAINT PK_ReceptionistShiftTemplates PRIMARY KEY CLUSTERED (ShiftTemplateId ASC),
        CONSTRAINT CK_ReceptionistShiftTemplates_Time CHECK (EndTime > StartTime),
        CONSTRAINT CK_ReceptionistShiftTemplates_MinStaff CHECK (MinStaffRequired >= 1)
    );
END
GO

-- 2. ReceptionistWorkSchedules
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistWorkSchedules')
BEGIN
    CREATE TABLE dbo.ReceptionistWorkSchedules (
        ReceptionistScheduleId BIGINT IDENTITY(1,1) NOT NULL,
        ReceptionistUserId BIGINT NOT NULL,
        ShiftTemplateId BIGINT NULL,
        WorkDate DATE NOT NULL,
        StartTime TIME(0) NOT NULL,
        EndTime TIME(0) NOT NULL,
        Status VARCHAR(30) NOT NULL CONSTRAINT DF_ReceptionistWorkSchedules_Status DEFAULT ('Scheduled'),
        IsPublished BIT NOT NULL CONSTRAINT DF_ReceptionistWorkSchedules_IsPublished DEFAULT (1),
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ReceptionistWorkSchedules_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2(7) NULL,
        CreatedBy BIGINT NOT NULL,
        CONSTRAINT PK_ReceptionistWorkSchedules PRIMARY KEY CLUSTERED (ReceptionistScheduleId ASC),
        CONSTRAINT FK_ReceptionistWorkSchedules_UserAccounts_Receptionist FOREIGN KEY (ReceptionistUserId)
            REFERENCES dbo.UserAccounts (UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_ReceptionistWorkSchedules_ShiftTemplates FOREIGN KEY (ShiftTemplateId)
            REFERENCES dbo.ReceptionistShiftTemplates (ShiftTemplateId) ON DELETE SET NULL,
        CONSTRAINT FK_ReceptionistWorkSchedules_UserAccounts_Creator FOREIGN KEY (CreatedBy)
            REFERENCES dbo.UserAccounts (UserId) ON DELETE NO ACTION,
        CONSTRAINT CK_ReceptionistWorkSchedules_Time CHECK (EndTime > StartTime),
        CONSTRAINT CK_ReceptionistWorkSchedules_Status CHECK (Status IN ('Scheduled', 'Cancelled', 'Completed', 'Swapped', 'AbsenceReported'))
    );

    CREATE NONCLUSTERED INDEX IX_ReceptionistWorkSchedules_WorkDate_Status
        ON dbo.ReceptionistWorkSchedules (WorkDate, Status)
        INCLUDE (ReceptionistUserId, StartTime, EndTime);

    CREATE NONCLUSTERED INDEX IX_ReceptionistWorkSchedules_User_WorkDate
        ON dbo.ReceptionistWorkSchedules (ReceptionistUserId, WorkDate);
END
GO

-- 3. ReceptionistLeaveRequests
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistLeaveRequests')
BEGIN
    CREATE TABLE dbo.ReceptionistLeaveRequests (
        LeaveRequestId BIGINT IDENTITY(1,1) NOT NULL,
        ReceptionistUserId BIGINT NOT NULL,
        LeaveType VARCHAR(30) NOT NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        StartTime TIME(0) NULL,
        EndTime TIME(0) NULL,
        Reason NVARCHAR(1000) NOT NULL,
        AdditionalNotes NVARCHAR(1000) NULL,
        Status VARCHAR(30) NOT NULL CONSTRAINT DF_ReceptionistLeaveRequests_Status DEFAULT ('Pending'),
        RejectionReason NVARCHAR(1000) NULL,
        ApprovedBy BIGINT NULL,
        ApprovedAt DATETIME2(7) NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ReceptionistLeaveRequests_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2(7) NULL,
        CONSTRAINT PK_ReceptionistLeaveRequests PRIMARY KEY CLUSTERED (LeaveRequestId ASC),
        CONSTRAINT FK_ReceptionistLeaveRequests_UserAccounts_Receptionist FOREIGN KEY (ReceptionistUserId)
            REFERENCES dbo.UserAccounts (UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_ReceptionistLeaveRequests_UserAccounts_ApprovedBy FOREIGN KEY (ApprovedBy)
            REFERENCES dbo.UserAccounts (UserId) ON DELETE NO ACTION,
        CONSTRAINT CK_ReceptionistLeaveRequests_Date CHECK (EndDate >= StartDate),
        CONSTRAINT CK_ReceptionistLeaveRequests_Status CHECK (Status IN ('Pending', 'Approved', 'Rejected', 'Withdrawn', 'Cancelled')),
        CONSTRAINT CK_ReceptionistLeaveRequests_LeaveType CHECK (LeaveType IN ('FullDay', 'HalfDayMorning', 'HalfDayAfternoon', 'Hourly', 'MultiDay'))
    );

    CREATE NONCLUSTERED INDEX IX_ReceptionistLeaveRequests_User_Status
        ON dbo.ReceptionistLeaveRequests (ReceptionistUserId, Status);

    CREATE NONCLUSTERED INDEX IX_ReceptionistLeaveRequests_Dates_Status
        ON dbo.ReceptionistLeaveRequests (StartDate, EndDate, Status);
END
GO

-- 4. ReceptionistLeaveRequestPeriods
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistLeaveRequestPeriods')
BEGIN
    CREATE TABLE dbo.ReceptionistLeaveRequestPeriods (
        PeriodId BIGINT IDENTITY(1,1) NOT NULL,
        LeaveRequestId BIGINT NOT NULL,
        LeaveDate DATE NOT NULL,
        StartTime TIME(0) NULL,
        EndTime TIME(0) NULL,
        Status VARCHAR(30) NOT NULL CONSTRAINT DF_ReceptionistLeaveRequestPeriods_Status DEFAULT ('Pending'),
        CONSTRAINT PK_ReceptionistLeaveRequestPeriods PRIMARY KEY CLUSTERED (PeriodId ASC),
        CONSTRAINT FK_ReceptionistLeaveRequestPeriods_LeaveRequest FOREIGN KEY (LeaveRequestId)
            REFERENCES dbo.ReceptionistLeaveRequests (LeaveRequestId) ON DELETE CASCADE,
        CONSTRAINT CK_ReceptionistLeaveRequestPeriods_Status CHECK (Status IN ('Pending', 'Approved', 'Rejected', 'Withdrawn', 'Cancelled'))
    );

    CREATE NONCLUSTERED INDEX IX_ReceptionistLeaveRequestPeriods_Date_Status
        ON dbo.ReceptionistLeaveRequestPeriods (LeaveDate, Status);
END
GO

-- 5. ReceptionistShiftSwapRequests
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistShiftSwapRequests')
BEGIN
    CREATE TABLE dbo.ReceptionistShiftSwapRequests (
        SwapRequestId BIGINT IDENTITY(1,1) NOT NULL,
        RequestingReceptionistUserId BIGINT NOT NULL,
        TargetReceptionistUserId BIGINT NOT NULL,
        SourceScheduleId BIGINT NOT NULL,
        TargetScheduleId BIGINT NULL,
        Reason NVARCHAR(1000) NOT NULL,
        Status VARCHAR(30) NOT NULL CONSTRAINT DF_ReceptionistShiftSwapRequests_Status DEFAULT ('PendingCounterparty'),
        CounterpartyResponseAt DATETIME2(7) NULL,
        CounterpartyNote NVARCHAR(500) NULL,
        AdminDecisionBy BIGINT NULL,
        AdminDecisionAt DATETIME2(7) NULL,
        AdminDecisionReason NVARCHAR(500) NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ReceptionistShiftSwapRequests_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2(7) NULL,
        CONSTRAINT PK_ReceptionistShiftSwapRequests PRIMARY KEY CLUSTERED (SwapRequestId ASC),
        CONSTRAINT FK_ReceptionistShiftSwapRequests_RequestingUser FOREIGN KEY (RequestingReceptionistUserId)
            REFERENCES dbo.UserAccounts (UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_ReceptionistShiftSwapRequests_TargetUser FOREIGN KEY (TargetReceptionistUserId)
            REFERENCES dbo.UserAccounts (UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_ReceptionistShiftSwapRequests_SourceSchedule FOREIGN KEY (SourceScheduleId)
            REFERENCES dbo.ReceptionistWorkSchedules (ReceptionistScheduleId) ON DELETE NO ACTION,
        CONSTRAINT FK_ReceptionistShiftSwapRequests_TargetSchedule FOREIGN KEY (TargetScheduleId)
            REFERENCES dbo.ReceptionistWorkSchedules (ReceptionistScheduleId) ON DELETE NO ACTION,
        CONSTRAINT FK_ReceptionistShiftSwapRequests_AdminUser FOREIGN KEY (AdminDecisionBy)
            REFERENCES dbo.UserAccounts (UserId) ON DELETE NO ACTION,
        CONSTRAINT CK_ReceptionistShiftSwapRequests_DiffUsers CHECK (RequestingReceptionistUserId <> TargetReceptionistUserId),
        CONSTRAINT CK_ReceptionistShiftSwapRequests_Status CHECK (Status IN ('PendingCounterparty', 'PendingAdminApproval', 'Approved', 'Rejected', 'Declined', 'Withdrawn', 'Expired'))
    );

    CREATE NONCLUSTERED INDEX IX_ReceptionistShiftSwapRequests_RequestingUser
        ON dbo.ReceptionistShiftSwapRequests (RequestingReceptionistUserId, Status);

    CREATE NONCLUSTERED INDEX IX_ReceptionistShiftSwapRequests_TargetUser
        ON dbo.ReceptionistShiftSwapRequests (TargetReceptionistUserId, Status);
END
GO

-- 6. ReceptionistCoverageIncidents
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistCoverageIncidents')
BEGIN
    CREATE TABLE dbo.ReceptionistCoverageIncidents (
        IncidentId BIGINT IDENTITY(1,1) NOT NULL,
        WorkDate DATE NOT NULL,
        ShiftStartTime TIME(0) NOT NULL,
        ShiftEndTime TIME(0) NOT NULL,
        AffectedReceptionistUserId BIGINT NULL,
        IncidentType VARCHAR(50) NOT NULL,
        PlannedStaffCount INT NOT NULL,
        AvailableStaffCount INT NOT NULL,
        RequiredMinimumCount INT NOT NULL CONSTRAINT DF_ReceptionistCoverageIncidents_MinStaff DEFAULT (2),
        ShortageCount INT NOT NULL,
        Reason NVARCHAR(1000) NOT NULL,
        ReportedBy BIGINT NOT NULL,
        ReportedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ReceptionistCoverageIncidents_ReportedAt DEFAULT (SYSUTCDATETIME()),
        Status VARCHAR(30) NOT NULL CONSTRAINT DF_ReceptionistCoverageIncidents_Status DEFAULT ('Open'),
        ResolutionNotes NVARCHAR(1000) NULL,
        ReplacementReceptionistUserId BIGINT NULL,
        ResolvedBy BIGINT NULL,
        ResolvedAt DATETIME2(7) NULL,
        CONSTRAINT PK_ReceptionistCoverageIncidents PRIMARY KEY CLUSTERED (IncidentId ASC),
        CONSTRAINT FK_ReceptionistCoverageIncidents_AffectedUser FOREIGN KEY (AffectedReceptionistUserId)
            REFERENCES dbo.UserAccounts (UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_ReceptionistCoverageIncidents_ReportedBy FOREIGN KEY (ReportedBy)
            REFERENCES dbo.UserAccounts (UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_ReceptionistCoverageIncidents_ReplacementUser FOREIGN KEY (ReplacementReceptionistUserId)
            REFERENCES dbo.UserAccounts (UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_ReceptionistCoverageIncidents_ResolvedBy FOREIGN KEY (ResolvedBy)
            REFERENCES dbo.UserAccounts (UserId) ON DELETE NO ACTION,
        CONSTRAINT CK_ReceptionistCoverageIncidents_Status CHECK (Status IN ('Open', 'Investigating', 'Resolved', 'Acknowledged'))
    );

    CREATE NONCLUSTERED INDEX IX_ReceptionistCoverageIncidents_WorkDate_Status
        ON dbo.ReceptionistCoverageIncidents (WorkDate, Status);
END
GO

-- 7. ReceptionistWorkHandovers
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceptionistWorkHandovers')
BEGIN
    CREATE TABLE dbo.ReceptionistWorkHandovers (
        HandoverId BIGINT IDENTITY(1,1) NOT NULL,
        FromReceptionistUserId BIGINT NOT NULL,
        ToReceptionistUserId BIGINT NULL,
        WorkDate DATE NOT NULL,
        ShiftId BIGINT NULL,
        HandoverType VARCHAR(50) NOT NULL,
        PendingAppointmentCount INT NOT NULL CONSTRAINT DF_ReceptionistWorkHandovers_PendingAppts DEFAULT (0),
        UnpaidInvoiceCount INT NOT NULL CONSTRAINT DF_ReceptionistWorkHandovers_UnpaidInvoices DEFAULT (0),
        ActiveChatCount INT NOT NULL CONSTRAINT DF_ReceptionistWorkHandovers_ActiveChats DEFAULT (0),
        Notes NVARCHAR(2000) NOT NULL,
        HandoverStatus VARCHAR(30) NOT NULL CONSTRAINT DF_ReceptionistWorkHandovers_Status DEFAULT ('Transferred'),
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ReceptionistWorkHandovers_CreatedAt DEFAULT (SYSUTCDATETIME()),
        AcknowledgedAt DATETIME2(7) NULL,
        CONSTRAINT PK_ReceptionistWorkHandovers PRIMARY KEY CLUSTERED (HandoverId ASC),
        CONSTRAINT FK_ReceptionistWorkHandovers_FromUser FOREIGN KEY (FromReceptionistUserId)
            REFERENCES dbo.UserAccounts (UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_ReceptionistWorkHandovers_ToUser FOREIGN KEY (ToReceptionistUserId)
            REFERENCES dbo.UserAccounts (UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_ReceptionistWorkHandovers_Shift FOREIGN KEY (ShiftId)
            REFERENCES dbo.ReceptionistWorkSchedules (ReceptionistScheduleId) ON DELETE SET NULL,
        CONSTRAINT CK_ReceptionistWorkHandovers_Status CHECK (HandoverStatus IN ('Transferred', 'Acknowledged', 'Completed'))
    );

    CREATE NONCLUSTERED INDEX IX_ReceptionistWorkHandovers_FromUser
        ON dbo.ReceptionistWorkHandovers (FromReceptionistUserId, WorkDate);
END
GO

-- 8. Seed Default Shift Templates
IF NOT EXISTS (SELECT 1 FROM dbo.ReceptionistShiftTemplates WHERE TemplateName = N'Ca Sáng (07:00 - 13:00)')
BEGIN
    INSERT INTO dbo.ReceptionistShiftTemplates (TemplateName, StartTime, EndTime, MinStaffRequired, IsActive, DisplayOrder)
    VALUES (N'Ca Sáng (07:00 - 13:00)', '07:00:00', '13:00:00', 2, 1, 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.ReceptionistShiftTemplates WHERE TemplateName = N'Ca Chiều (13:00 - 19:00)')
BEGIN
    INSERT INTO dbo.ReceptionistShiftTemplates (TemplateName, StartTime, EndTime, MinStaffRequired, IsActive, DisplayOrder)
    VALUES (N'Ca Chiều (13:00 - 19:00)', '13:00:00', '19:00:00', 2, 1, 2);
END

IF NOT EXISTS (SELECT 1 FROM dbo.ReceptionistShiftTemplates WHERE TemplateName = N'Ca Cả Ngày (07:00 - 19:00)')
BEGIN
    INSERT INTO dbo.ReceptionistShiftTemplates (TemplateName, StartTime, EndTime, MinStaffRequired, IsActive, DisplayOrder)
    VALUES (N'Ca Cả Ngày (07:00 - 19:00)', '07:00:00', '19:00:00', 2, 1, 3);
END
GO
