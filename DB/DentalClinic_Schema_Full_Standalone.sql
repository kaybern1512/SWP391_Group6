-- =====================================================================================
-- DENTALCARE CLINIC MANAGEMENT SYSTEM
-- SWP391 - GROUP 6
-- FULL CLEAN STANDALONE DATABASE SCHEMA (FINAL SIMPLIFIED ARCHITECTURE)
-- Target RDBMS: Microsoft SQL Server 2017 / 2019 / 2022 / LocalDB / SQLEXPRESS
-- Safety Policy: Environment-agnostic standalone DDL (No USE / CREATE / DROP DATABASE)
-- Architecture:
--   * Single Person Identity: UserAccounts.UserId across 4 Roles
--   * 1:N Multi-Service & Multi-Department Appointments (Appointments -> AppointmentServices)
--   * Structured Service Combos (ServiceCombos -> ServiceComboItems)
--   * Unified Staff Scheduling, Leave & Multi-Shift Attendance
--   * Single-Table Imaging Diagnostics & 3-Table Pharmacy Inventory/Dispensing
-- Object Metrics: 38 Tables (37 Business + 1 EF History), 409 Columns, 38 PKs, 91 FKs,
--                 80 CHECKs, 48 Unique Enforcements (39 UQ Constraints + 9 UQ Indexes), 1 View, 1 SP
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- -------------------------------------------------------------------------------------
-- PRE-EXECUTION SAFETY GUARD: BLOCK EXECUTION ON SYSTEM OR NON-EMPTY DATABASES
-- Does NOT create, select, drop, or alter any existing database.
-- -------------------------------------------------------------------------------------
DECLARE @CurrentDbName sysname = DB_NAME();

IF @CurrentDbName IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    RAISERROR(N'ABORTED: Cannot execute standalone schema on SQL Server system database [%s]. Please connect to a new, empty target database first.', 16, 1, @CurrentDbName);
    SET NOEXEC ON;
END;

IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
BEGIN
    RAISERROR(N'ABORTED: Current database [%s] is NOT empty. Refusing to execute standalone schema on an existing database.', 16, 1, @CurrentDbName);
    SET NOEXEC ON;
END;
GO


-- =====================================================================================
-- 01. CORE PLATFORM, AUTHENTICATION & SECURITY
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table 01: dbo.__EFMigrationsHistory
-- Empty schema history table (No artificial rows inserted prior to actual EF migrations)
-- -------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'__EFMigrationsHistory' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory] (
        [MigrationId]    [nvarchar](150) NOT NULL,
        [ProductVersion] [nvarchar](32)  NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED ([MigrationId] ASC)
    );
END;
GO

-- -------------------------------------------------------------------------------------
-- Table 02: dbo.ClinicProfile
-- Manages clinic-wide operating hours and slot configuration
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ClinicProfile] (
    [ClinicId]                         [int] NOT NULL,
    [ClinicName]                       [nvarchar](200) NOT NULL,
    [TaxCode]                          [varchar](50) NULL,
    [Hotline]                          [varchar](30) NOT NULL,
    [Email]                            [varchar](150) NOT NULL,
    [Address]                          [nvarchar](300) NOT NULL,
    [LogoUrl]                          [nvarchar](500) NULL,
    [OpeningTime]                      [time](7) NOT NULL,
    [ClosingTime]                      [time](7) NOT NULL,
    [SlotDurationMinutes]              [int] NOT NULL DEFAULT (30),
    [MaxConcurrentAppointmentsPerSlot] [int] NOT NULL DEFAULT (5),
    [Description]                      [nvarchar](1000) NULL,
    [UpdatedAt]                        [datetime2](7) NULL,

    CONSTRAINT [PK_ClinicProfile] PRIMARY KEY CLUSTERED ([ClinicId] ASC),
    CONSTRAINT [CK_ClinicProfile_SingleRow] CHECK ([ClinicId] = 1),
    CONSTRAINT [CK_ClinicProfile_OperatingHours] CHECK ([ClosingTime] > [OpeningTime]),
    CONSTRAINT [CK_ClinicProfile_SlotConfig] CHECK ([SlotDurationMinutes] > 0 AND [MaxConcurrentAppointmentsPerSlot] > 0)
);
GO

-- -------------------------------------------------------------------------------------
-- Table 03: dbo.UserAccounts
-- Single Person Identity (UserId) across 4 Official RBAC Roles
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[UserAccounts] (
    [UserId]           [bigint] IDENTITY(1,1) NOT NULL,
    [Email]            [varchar](150) NULL,
    [PhoneNumber]      [varchar](20) NULL,
    [PasswordHash]     [varchar](255) NULL,
    [Role]             [varchar](40) NOT NULL,
    [Status]           [varchar](30) NOT NULL DEFAULT ('Active'),
    [AvatarUrl]        [nvarchar](500) NULL,
    [LastLoginAt]      [datetime2](7) NULL,
    [FailedLoginCount] [int] NOT NULL DEFAULT (0),
    [LockoutEnd]       [datetime2](7) NULL,
    [CreatedAt]        [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]        [datetime2](7) NULL,

    CONSTRAINT [PK_UserAccounts] PRIMARY KEY CLUSTERED ([UserId] ASC),
    CONSTRAINT [CK_UserAccounts_FailedLogin] CHECK ([FailedLoginCount] >= 0),
    CONSTRAINT [CK_UserAccounts_Status] CHECK ([Status] IN ('Active', 'Inactive', 'Locked')),
    CONSTRAINT [CK_UserAccounts_Role] CHECK ([Role] IN ('Patient', 'Receptionist', 'Dentist', 'SystemAdministrator'))
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_UserAccounts_Email_NotNull]
    ON [dbo].[UserAccounts] ([Email] ASC)
    WHERE ([Email] IS NOT NULL);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_UserAccounts_Phone_NotNull]
    ON [dbo].[UserAccounts] ([PhoneNumber] ASC)
    WHERE ([PhoneNumber] IS NOT NULL);
GO

-- -------------------------------------------------------------------------------------
-- Table 04: dbo.AuditLogs
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[AuditLogs] (
    [AuditLogId] [bigint] IDENTITY(1,1) NOT NULL,
    [UserId]     [bigint] NULL,
    [Action]     [varchar](100) NOT NULL,
    [EntityName] [varchar](100) NOT NULL,
    [EntityId]   [varchar](100) NULL,
    [OldValues]  [nvarchar](max) NULL,
    [NewValues]  [nvarchar](max) NULL,
    [CreatedAt]  [datetime2](7) NOT NULL DEFAULT (sysdatetime()),

    CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([AuditLogId] ASC),
    CONSTRAINT [FK_AuditLogs_UserAccounts] FOREIGN KEY ([UserId]) REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

CREATE NONCLUSTERED INDEX [IX_AuditLogs_User_CreatedAt]
    ON [dbo].[AuditLogs] ([UserId] ASC, [CreatedAt] ASC);
GO


-- =====================================================================================
-- 02. CLINICAL DEPARTMENTS, PROFILES & FACILITY DIRECTORY
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table 05: dbo.Departments
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Departments] (
    [DepartmentId] [int] IDENTITY(1,1) NOT NULL,
    [Name]         [nvarchar](150) NOT NULL,
    [Description]  [nvarchar](500) NULL,
    [Status]       [varchar](30) NOT NULL DEFAULT ('Active'),
    [CreatedAt]    [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]    [datetime2](7) NULL,

    CONSTRAINT [PK_Departments] PRIMARY KEY CLUSTERED ([DepartmentId] ASC),
    CONSTRAINT [UQ_Departments_Name] UNIQUE NONCLUSTERED ([Name] ASC),
    CONSTRAINT [CK_Departments_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
GO

-- -------------------------------------------------------------------------------------
-- Table 06: dbo.PatientProfiles (UserId shared PK/FK to UserAccounts)
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[PatientProfiles] (
    [UserId]                [bigint] NOT NULL,
    [FullName]              [nvarchar](150) NOT NULL,
    [DateOfBirth]           [date] NULL,
    [Gender]                [varchar](20) NULL,
    [NationalId]            [varchar](30) NULL,
    [HealthInsuranceNumber] [varchar](50) NULL,
    [Address]               [nvarchar](300) NULL,
    [EmergencyContact]      [nvarchar](150) NULL,
    [CreatedAt]             [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]             [datetime2](7) NULL,

    CONSTRAINT [PK_PatientProfiles] PRIMARY KEY CLUSTERED ([UserId] ASC),
    CONSTRAINT [FK_PatientProfiles_UserAccounts] FOREIGN KEY ([UserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
    CONSTRAINT [CK_PatientProfiles_Gender] CHECK ([Gender] IS NULL OR [Gender] IN ('Male', 'Female', 'Other'))
);
GO

-- -------------------------------------------------------------------------------------
-- Table 07: dbo.StaffProfiles (UserId shared PK/FK to UserAccounts for Receptionist/Dentist)
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[StaffProfiles] (
    [UserId]           [bigint] NOT NULL,
    [FullName]         [nvarchar](150) NOT NULL,
    [EmploymentStatus] [varchar](30) NOT NULL DEFAULT ('Active'),
    [CreatedAt]        [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]        [datetime2](7) NULL,

    CONSTRAINT [PK_StaffProfiles] PRIMARY KEY CLUSTERED ([UserId] ASC),
    CONSTRAINT [FK_StaffProfiles_UserAccounts] FOREIGN KEY ([UserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
    CONSTRAINT [CK_StaffProfiles_Status] CHECK ([EmploymentStatus] IN ('Active', 'Inactive'))
);
GO

-- -------------------------------------------------------------------------------------
-- Table 08: dbo.DentistProfiles (UserId shared PK/FK to StaffProfiles; belongs to 1 Department)
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DentistProfiles] (
    [UserId]            [bigint] NOT NULL,
    [DepartmentId]      [int] NOT NULL,
    [LicenseNumber]     [varchar](80) NULL,
    [Qualification]     [nvarchar](300) NULL,
    [YearsOfExperience] [int] NULL,
    [Biography]         [nvarchar](1000) NULL,

    CONSTRAINT [PK_DentistProfiles] PRIMARY KEY CLUSTERED ([UserId] ASC),
    CONSTRAINT [UQ_DentistProfiles_User_Department] UNIQUE NONCLUSTERED ([UserId] ASC, [DepartmentId] ASC),
    CONSTRAINT [FK_DentistProfiles_StaffProfiles] FOREIGN KEY ([UserId]) REFERENCES [dbo].[StaffProfiles] ([UserId]),
    CONSTRAINT [FK_DentistProfiles_Departments] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Departments] ([DepartmentId]),
    CONSTRAINT [CK_DentistProfiles_Experience] CHECK ([YearsOfExperience] IS NULL OR [YearsOfExperience] >= 0)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_DentistProfiles_LicenseNumber_NotNull]
    ON [dbo].[DentistProfiles] ([LicenseNumber] ASC)
    WHERE ([LicenseNumber] IS NOT NULL);
GO

CREATE NONCLUSTERED INDEX [IX_DentistProfiles_DepartmentId]
    ON [dbo].[DentistProfiles] ([DepartmentId] ASC);
GO

-- -------------------------------------------------------------------------------------
-- Table 09: dbo.Rooms
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Rooms] (
    [RoomId]       [int] IDENTITY(1,1) NOT NULL,
    [DepartmentId] [int] NOT NULL,
    [RoomCode]     [varchar](50) NOT NULL,
    [RoomName]     [nvarchar](100) NOT NULL,
    [RoomType]     [varchar](50) NOT NULL,
    [Status]       [varchar](30) NOT NULL DEFAULT ('Active'),
    [CreatedAt]    [datetime2](7) NOT NULL DEFAULT (sysdatetime()),

    CONSTRAINT [PK_Rooms] PRIMARY KEY CLUSTERED ([RoomId] ASC),
    CONSTRAINT [UQ_Rooms_RoomCode] UNIQUE NONCLUSTERED ([RoomCode] ASC),
    CONSTRAINT [UQ_Rooms_Room_Department] UNIQUE NONCLUSTERED ([RoomId] ASC, [DepartmentId] ASC),
    CONSTRAINT [FK_Rooms_Departments] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Departments] ([DepartmentId]),
    CONSTRAINT [CK_Rooms_Status] CHECK ([Status] IN ('Active', 'Inactive', 'Maintenance'))
);
GO

-- -------------------------------------------------------------------------------------
-- Table 10: dbo.DentalChairs
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DentalChairs] (
    [DentalChairId] [int] IDENTITY(1,1) NOT NULL,
    [RoomId]        [int] NOT NULL,
    [ChairCode]     [varchar](50) NOT NULL,
    [ChairName]     [nvarchar](100) NOT NULL,
    [Status]        [varchar](30) NOT NULL DEFAULT ('Active'),
    [CreatedAt]     [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]     [datetime2](7) NULL,

    CONSTRAINT [PK_DentalChairs] PRIMARY KEY CLUSTERED ([DentalChairId] ASC),
    CONSTRAINT [UQ_DentalChairs_Room_ChairCode] UNIQUE NONCLUSTERED ([RoomId] ASC, [ChairCode] ASC),
    CONSTRAINT [UQ_DentalChairs_Chair_Room] UNIQUE NONCLUSTERED ([DentalChairId] ASC, [RoomId] ASC),
    CONSTRAINT [FK_DentalChairs_Rooms] FOREIGN KEY ([RoomId]) REFERENCES [dbo].[Rooms] ([RoomId]),
    CONSTRAINT [CK_DentalChairs_Status] CHECK ([Status] IN ('Active', 'Inactive', 'Maintenance'))
);
GO


-- =====================================================================================
-- 03. CLINICAL SERVICES & STRUCTURED PACKAGES (COMBOS)
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table 11: dbo.DepartmentServices
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DepartmentServices] (
    [DepartmentServiceId] [int] IDENTITY(1,1) NOT NULL,
    [DepartmentId]        [int] NOT NULL,
    [ServiceCode]         [varchar](50) NOT NULL,
    [ServiceName]         [nvarchar](200) NOT NULL,
    [Description]         [nvarchar](1000) NULL,
    [Price]               [decimal](18, 2) NOT NULL,
    [DurationMinutes]     [int] NOT NULL DEFAULT (30),
    [DiagnosticType]      [varchar](50) NULL,
    [Status]              [varchar](30) NOT NULL DEFAULT ('Active'),
    [CreatedAt]           [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]           [datetime2](7) NULL,

    CONSTRAINT [PK_DepartmentServices] PRIMARY KEY CLUSTERED ([DepartmentServiceId] ASC),
    CONSTRAINT [UQ_DepartmentServices_Code] UNIQUE NONCLUSTERED ([ServiceCode] ASC),
    CONSTRAINT [UQ_DepartmentServices_Dept_Name] UNIQUE NONCLUSTERED ([DepartmentId] ASC, [ServiceName] ASC),
    CONSTRAINT [UQ_DepartmentServices_Service_Department] UNIQUE NONCLUSTERED ([DepartmentServiceId] ASC, [DepartmentId] ASC),
    CONSTRAINT [FK_DepartmentServices_Departments] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Departments] ([DepartmentId]),
    CONSTRAINT [CK_DepartmentServices_Price] CHECK ([Price] >= 0),
    CONSTRAINT [CK_DepartmentServices_Duration] CHECK ([DurationMinutes] > 0),
    CONSTRAINT [CK_DepartmentServices_Status] CHECK ([Status] IN ('Active', 'Inactive'))
);
GO

-- -------------------------------------------------------------------------------------
-- Table 12: dbo.ServiceCombos
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ServiceCombos] (
    [ComboId]            [int] IDENTITY(1,1) NOT NULL,
    [ComboCode]          [varchar](50) NOT NULL,
    [ComboName]          [nvarchar](200) NOT NULL,
    [Description]        [nvarchar](1000) NULL,
    [TotalOriginalPrice] [decimal](18, 2) NOT NULL,
    [DiscountedPrice]    [decimal](18, 2) NOT NULL,
    [IsActive]           [bit] NOT NULL DEFAULT (1),
    [CreatedAt]          [datetime2](7) NOT NULL DEFAULT (sysdatetime()),

    CONSTRAINT [PK_ServiceCombos] PRIMARY KEY CLUSTERED ([ComboId] ASC),
    CONSTRAINT [UQ_ServiceCombos_Code] UNIQUE NONCLUSTERED ([ComboCode] ASC),
    CONSTRAINT [CK_ServiceCombos_Price] CHECK ([DiscountedPrice] >= 0 AND [TotalOriginalPrice] >= [DiscountedPrice])
);
GO

-- -------------------------------------------------------------------------------------
-- Table 13: dbo.ServiceComboItems
-- Maps ServiceCombos to multiple DepartmentServices (across one or more Departments)
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ServiceComboItems] (
    [ComboItemId]         [int] IDENTITY(1,1) NOT NULL,
    [ComboId]             [int] NOT NULL,
    [DepartmentServiceId] [int] NOT NULL,
    [Quantity]            [int] NOT NULL DEFAULT (1),
    [AllocatedUnitPrice]  [decimal](18, 2) NOT NULL,

    CONSTRAINT [PK_ServiceComboItems] PRIMARY KEY CLUSTERED ([ComboItemId] ASC),
    CONSTRAINT [UQ_ServiceComboItems_Combo_Service] UNIQUE NONCLUSTERED ([ComboId] ASC, [DepartmentServiceId] ASC),
    CONSTRAINT [UQ_ServiceComboItems_Item_Combo_Service] UNIQUE NONCLUSTERED ([ComboItemId] ASC, [ComboId] ASC, [DepartmentServiceId] ASC),
    CONSTRAINT [FK_ServiceComboItems_Combo] FOREIGN KEY ([ComboId]) REFERENCES [dbo].[ServiceCombos] ([ComboId]),
    CONSTRAINT [FK_ServiceComboItems_Service] FOREIGN KEY ([DepartmentServiceId]) REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId]),
    CONSTRAINT [CK_ServiceComboItems_Qty] CHECK ([Quantity] > 0),
    CONSTRAINT [CK_ServiceComboItems_Price] CHECK ([AllocatedUnitPrice] >= 0)
);
GO


-- =====================================================================================
-- 04. UNIFIED WORKFORCE, SCHEDULING & MULTI-SHIFT ATTENDANCE
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table 14: dbo.StaffWorkSchedules
-- Unified work schedule for Dentist & Receptionist (supports multiple shifts per day)
-- Room/Chair allocation is managed per service on AppointmentServices to avoid duplication
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[StaffWorkSchedules] (
    [WorkScheduleId] [bigint] IDENTITY(1,1) NOT NULL,
    [StaffUserId]    [bigint] NOT NULL,
    [StaffRole]      [varchar](40) NOT NULL,
    [WorkDate]       [date] NOT NULL,
    [StartTime]      [time](7) NOT NULL,
    [EndTime]        [time](7) NOT NULL,
    [Status]         [varchar](30) NOT NULL DEFAULT ('Scheduled'),
    [Notes]          [nvarchar](500) NULL,
    [CreatedAt]      [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]      [datetime2](7) NULL,

    CONSTRAINT [PK_StaffWorkSchedules] PRIMARY KEY CLUSTERED ([WorkScheduleId] ASC),
    CONSTRAINT [UQ_StaffWorkSchedules_User_Shift] UNIQUE NONCLUSTERED ([StaffUserId] ASC, [WorkDate] ASC, [StartTime] ASC, [EndTime] ASC),
    CONSTRAINT [UQ_StaffWorkSchedules_Id_User_Date] UNIQUE NONCLUSTERED ([WorkScheduleId] ASC, [StaffUserId] ASC, [WorkDate] ASC),
    CONSTRAINT [FK_StaffWorkSchedules_StaffProfiles] FOREIGN KEY ([StaffUserId]) REFERENCES [dbo].[StaffProfiles] ([UserId]),
    CONSTRAINT [CK_StaffWorkSchedules_Role] CHECK ([StaffRole] IN ('Dentist', 'Receptionist')),
    CONSTRAINT [CK_StaffWorkSchedules_Time] CHECK ([EndTime] > [StartTime]),
    CONSTRAINT [CK_StaffWorkSchedules_Status] CHECK ([Status] IN ('Scheduled', 'Completed', 'Cancelled', 'AbsenceReported'))
);
GO

CREATE NONCLUSTERED INDEX [IX_StaffWorkSchedules_Date_Role_Status]
    ON [dbo].[StaffWorkSchedules] ([WorkDate] ASC, [StaffRole] ASC, [Status] ASC);
GO

-- -------------------------------------------------------------------------------------
-- Table 15: dbo.StaffLeaveRequests
-- Unified leave request table for Dentist & Receptionist (full-day and partial-day)
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[StaffLeaveRequests] (
    [LeaveRequestId]   [bigint] IDENTITY(1,1) NOT NULL,
    [StaffUserId]      [bigint] NOT NULL,
    [StaffRole]        [varchar](40) NOT NULL,
    [LeaveType]        [varchar](50) NOT NULL,
    [StartDate]        [date] NOT NULL,
    [EndDate]          [date] NOT NULL,
    [StartTime]        [time](7) NULL,
    [EndTime]          [time](7) NULL,
    [Reason]           [nvarchar](500) NOT NULL,
    [Status]           [varchar](30) NOT NULL DEFAULT ('Pending'),
    [ReviewedByUserId] [bigint] NULL,
    [ReviewedAt]       [datetime2](7) NULL,
    [RejectionReason]  [nvarchar](500) NULL,
    [CreatedAt]        [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]        [datetime2](7) NULL,

    CONSTRAINT [PK_StaffLeaveRequests] PRIMARY KEY CLUSTERED ([LeaveRequestId] ASC),
    CONSTRAINT [FK_StaffLeaveRequests_StaffProfiles] FOREIGN KEY ([StaffUserId]) REFERENCES [dbo].[StaffProfiles] ([UserId]),
    CONSTRAINT [FK_StaffLeaveRequests_ReviewedBy] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
    CONSTRAINT [CK_StaffLeaveRequests_Role] CHECK ([StaffRole] IN ('Dentist', 'Receptionist')),
    CONSTRAINT [CK_StaffLeaveRequests_Dates] CHECK ([EndDate] >= [StartDate]),
    CONSTRAINT [CK_StaffLeaveRequests_Times] CHECK (([StartTime] IS NULL AND [EndTime] IS NULL) OR ([StartTime] IS NOT NULL AND [EndTime] IS NOT NULL AND [EndTime] > [StartTime])),
    CONSTRAINT [CK_StaffLeaveRequests_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected', 'Cancelled', 'Withdrawn'))
);
GO

CREATE NONCLUSTERED INDEX [IX_StaffLeaveRequests_User_Status_Dates]
    ON [dbo].[StaffLeaveRequests] ([StaffUserId] ASC, [Status] ASC, [StartDate] ASC, [EndDate] ASC);
GO

-- -------------------------------------------------------------------------------------
-- Table 16: dbo.StaffAttendanceRecords
-- Unified attendance supporting multiple shifts/clock-ins per staff member on the same day
-- Composite FK ensures StaffUserId and WorkDate match the referenced WorkScheduleId
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[StaffAttendanceRecords] (
    [AttendanceRecordId] [bigint] IDENTITY(1,1) NOT NULL,
    [StaffUserId]        [bigint] NOT NULL,
    [WorkScheduleId]     [bigint] NULL,
    [WorkDate]           [date] NOT NULL,
    [ClockInTime]        [datetime2](7) NULL,
    [ClockOutTime]       [datetime2](7) NULL,
    [Status]             [varchar](30) NOT NULL DEFAULT ('OnTime'),
    [LateMinutes]        [int] NOT NULL DEFAULT (0),
    [EarlyMinutes]       [int] NOT NULL DEFAULT (0),
    [CorrectionReason]   [nvarchar](500) NULL,
    [ReviewedByUserId]   [bigint] NULL,
    [ReviewedAt]         [datetime2](7) NULL,
    [Notes]              [nvarchar](500) NULL,
    [CreatedAt]          [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]          [datetime2](7) NULL,

    CONSTRAINT [PK_StaffAttendanceRecords] PRIMARY KEY CLUSTERED ([AttendanceRecordId] ASC),
    CONSTRAINT [FK_StaffAttendanceRecords_StaffProfiles] FOREIGN KEY ([StaffUserId]) REFERENCES [dbo].[StaffProfiles] ([UserId]),
    CONSTRAINT [FK_StaffAttendanceRecords_Schedule_User_Date] FOREIGN KEY ([WorkScheduleId], [StaffUserId], [WorkDate]) REFERENCES [dbo].[StaffWorkSchedules] ([WorkScheduleId], [StaffUserId], [WorkDate]),
    CONSTRAINT [FK_StaffAttendanceRecords_ReviewedBy] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
    CONSTRAINT [CK_StaffAttendanceRecords_Minutes] CHECK ([LateMinutes] >= 0 AND [EarlyMinutes] >= 0),
    CONSTRAINT [CK_StaffAttendanceRecords_ClockTimes] CHECK (
        ([ClockOutTime] IS NULL OR ([ClockInTime] IS NOT NULL AND [ClockOutTime] > [ClockInTime]))
        AND ([ClockInTime] IS NULL OR CONVERT([date], [ClockInTime]) = [WorkDate])
        AND ([ClockOutTime] IS NULL OR CONVERT([date], [ClockOutTime]) >= [WorkDate])
    ),
    CONSTRAINT [CK_StaffAttendanceRecords_Status] CHECK ([Status] IN ('OnTime', 'Late', 'EarlyDeparture', 'Absent', 'OnLeave'))
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_StaffAttendanceRecords_WorkScheduleId_NotNull]
    ON [dbo].[StaffAttendanceRecords] ([WorkScheduleId] ASC)
    WHERE ([WorkScheduleId] IS NOT NULL);
GO

CREATE NONCLUSTERED INDEX [IX_StaffAttendanceRecords_User_WorkDate]
    ON [dbo].[StaffAttendanceRecords] ([StaffUserId] ASC, [WorkDate] ASC);
GO


-- =====================================================================================
-- 05. ELECTRONIC DENTAL RECORDS & TREATMENT PLANNING
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table 17: dbo.DentalMedicalRecords
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DentalMedicalRecords] (
    [RecordId]           [bigint] IDENTITY(1,1) NOT NULL,
    [PatientUserId]      [bigint] NOT NULL,
    [MedicalHistory]     [nvarchar](max) NULL,
    [DentalHistory]      [nvarchar](max) NULL,
    [Allergies]          [nvarchar](500) NULL,
    [CurrentMedications] [nvarchar](500) NULL,
    [CreatedAt]          [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]          [datetime2](7) NULL,

    CONSTRAINT [PK_DentalMedicalRecords] PRIMARY KEY CLUSTERED ([RecordId] ASC),
    CONSTRAINT [UQ_DentalMedicalRecords_Patient] UNIQUE NONCLUSTERED ([PatientUserId] ASC),
    CONSTRAINT [UQ_DentalMedicalRecords_Record_Patient] UNIQUE NONCLUSTERED ([RecordId] ASC, [PatientUserId] ASC),
    CONSTRAINT [FK_DentalMedicalRecords_Patient] FOREIGN KEY ([PatientUserId]) REFERENCES [dbo].[PatientProfiles] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table 18: dbo.TreatmentPlans
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[TreatmentPlans] (
    [TreatmentPlanId]        [bigint] IDENTITY(1,1) NOT NULL,
    [MedicalRecordId]        [bigint] NOT NULL,
    [PatientUserId]          [bigint] NOT NULL,
    [CreatedByDentistUserId] [bigint] NOT NULL,
    [Title]                  [nvarchar](200) NOT NULL,
    [Diagnosis]              [nvarchar](1000) NULL,
    [EstimatedTotal]         [decimal](18, 2) NOT NULL DEFAULT (0),
    [Status]                 [varchar](30) NOT NULL DEFAULT ('Draft'),
    [CreatedAt]              [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [ApprovedAt]             [datetime2](7) NULL,
    [UpdatedAt]              [datetime2](7) NULL,

    CONSTRAINT [PK_TreatmentPlans] PRIMARY KEY CLUSTERED ([TreatmentPlanId] ASC),
    CONSTRAINT [UQ_TreatmentPlans_Id_Patient] UNIQUE NONCLUSTERED ([TreatmentPlanId] ASC, [PatientUserId] ASC),
    CONSTRAINT [FK_TreatmentPlans_Record_Patient] FOREIGN KEY ([MedicalRecordId], [PatientUserId]) REFERENCES [dbo].[DentalMedicalRecords] ([RecordId], [PatientUserId]),
    CONSTRAINT [FK_TreatmentPlans_Patient] FOREIGN KEY ([PatientUserId]) REFERENCES [dbo].[PatientProfiles] ([UserId]),
    CONSTRAINT [FK_TreatmentPlans_Dentist] FOREIGN KEY ([CreatedByDentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]),
    CONSTRAINT [CK_TreatmentPlans_Cost] CHECK ([EstimatedTotal] >= 0),
    CONSTRAINT [CK_TreatmentPlans_Status] CHECK ([Status] IN ('Draft', 'Planned', 'Active', 'InProgress', 'Completed', 'Paused', 'Cancelled'))
);
GO

CREATE NONCLUSTERED INDEX [IX_TreatmentPlans_Patient_Status]
    ON [dbo].[TreatmentPlans] ([PatientUserId] ASC, [Status] ASC);
GO

-- -------------------------------------------------------------------------------------
-- Table 19: dbo.TreatmentPlanItems
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[TreatmentPlanItems] (
    [PlanItemId]          [bigint] IDENTITY(1,1) NOT NULL,
    [TreatmentPlanId]     [bigint] NOT NULL,
    [DepartmentServiceId] [int] NOT NULL,
    [ToothNumber]         [varchar](20) NULL,
    [SequenceNo]          [int] NOT NULL,
    [EstimatedCost]       [decimal](18, 2) NOT NULL DEFAULT (0),
    [Status]              [varchar](30) NOT NULL DEFAULT ('Planned'),
    [Notes]               [nvarchar](500) NULL,
    [CreatedAt]           [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]           [datetime2](7) NULL,

    CONSTRAINT [PK_TreatmentPlanItems] PRIMARY KEY CLUSTERED ([PlanItemId] ASC),
    CONSTRAINT [UQ_TreatmentPlanItems_Seq] UNIQUE NONCLUSTERED ([TreatmentPlanId] ASC, [SequenceNo] ASC),
    CONSTRAINT [FK_TreatmentPlanItems_Plan] FOREIGN KEY ([TreatmentPlanId]) REFERENCES [dbo].[TreatmentPlans] ([TreatmentPlanId]),
    CONSTRAINT [FK_TreatmentPlanItems_Service] FOREIGN KEY ([DepartmentServiceId]) REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId]),
    CONSTRAINT [CK_TreatmentPlanItems_Seq] CHECK ([SequenceNo] > 0),
    CONSTRAINT [CK_TreatmentPlanItems_Cost] CHECK ([EstimatedCost] >= 0),
    CONSTRAINT [CK_TreatmentPlanItems_Status] CHECK ([Status] IN ('Planned', 'Pending', 'Scheduled', 'InProgress', 'Completed', 'Skipped', 'Cancelled'))
);
GO


-- =====================================================================================
-- 06. MULTI-SERVICE, MULTI-DEPARTMENT APPOINTMENTS
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table 20: dbo.Appointments
-- Master booking header. Does NOT store single-service/department/dentist/room/chair/queue.
-- Overall Status Sync Rule:
--   * 'PendingReceptionReview' (Default upon booking before reception confirms/allocates)
--   * 'Confirmed' (When all active AppointmentServices are confirmed)
--   * 'CheckedIn' (When patient checks in at reception)
--   * 'InProgress' (While any AppointmentService is Waiting / Called / InService)
--   * 'Completed' (When all non-cancelled AppointmentServices are Completed)
--   * 'Cancelled' / 'NoShow' (When all AppointmentServices are Cancelled / NoShow)
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Appointments] (
    [AppointmentId]      [bigint] IDENTITY(1,1) NOT NULL,
    [PatientUserId]      [bigint] NOT NULL,
    [ComboId]            [int] NULL,
    [BookingSource]      [varchar](30) NOT NULL DEFAULT ('Patient'),
    [ReasonForVisit]     [nvarchar](500) NULL,
    [Status]             [varchar](40) NOT NULL DEFAULT ('PendingReceptionReview'),
    [CreatedByUserId]    [bigint] NULL,
    [CreatedAt]          [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [ConfirmedAt]        [datetime2](7) NULL,
    [CheckedInAt]        [datetime2](7) NULL,
    [CompletedAt]        [datetime2](7) NULL,
    [CancelledAt]        [datetime2](7) NULL,
    [CancellationReason] [nvarchar](500) NULL,
    [UpdatedAt]          [datetime2](7) NULL,

    CONSTRAINT [PK_Appointments] PRIMARY KEY CLUSTERED ([AppointmentId] ASC),
    CONSTRAINT [UQ_Appointments_Id_Patient] UNIQUE NONCLUSTERED ([AppointmentId] ASC, [PatientUserId] ASC),
    CONSTRAINT [UQ_Appointments_Id_Combo] UNIQUE NONCLUSTERED ([AppointmentId] ASC, [ComboId] ASC),
    CONSTRAINT [FK_Appointments_Patient] FOREIGN KEY ([PatientUserId]) REFERENCES [dbo].[PatientProfiles] ([UserId]),
    CONSTRAINT [FK_Appointments_Combo] FOREIGN KEY ([ComboId]) REFERENCES [dbo].[ServiceCombos] ([ComboId]),
    CONSTRAINT [FK_Appointments_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
    CONSTRAINT [CK_Appointments_Source] CHECK ([BookingSource] IN ('Patient', 'Receptionist')),
    CONSTRAINT [CK_Appointments_Status] CHECK ([Status] IN (
        'PendingReceptionReview', 'Confirmed', 'CheckedIn', 'InProgress',
        'Completed', 'Cancelled', 'NoShow'
    ))
);
GO

CREATE NONCLUSTERED INDEX [IX_Appointments_Patient_Status]
    ON [dbo].[Appointments] ([PatientUserId] ASC, [Status] ASC);
GO

-- -------------------------------------------------------------------------------------
-- Table 21: dbo.AppointmentServices
-- 1:N Service items within an Appointment across one or multiple Departments.
-- Composite FKs strictly guarantee:
--   1. DepartmentId matches DepartmentServices.DepartmentId
--   2. ComboId & ComboItemId match Appointments.ComboId and ServiceComboItems (supports retail items with NULL ComboId/ComboItemId)
--   3. RequestedDentistUserId, AssignedDentistUserId, PendingDentistUserId belong to DepartmentId
--   4. RoomId belongs to DepartmentId, and DentalChairId belongs to RoomId
--   5. Confirmed/CheckedIn/Waiting/Called/InService/Completed require AssignedDentistUserId, RoomId, and DentalChairId
--   6. QueueNumber is unique per (DepartmentId, ServiceDate) using physical columns
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[AppointmentServices] (
    [AppointmentServiceId]   [bigint] IDENTITY(1,1) NOT NULL,
    [AppointmentId]          [bigint] NOT NULL,
    [DepartmentServiceId]    [int] NOT NULL,
    [DepartmentId]           [int] NOT NULL,
    [ComboId]                [int] NULL,
    [ComboItemId]            [int] NULL,
    [TreatmentPlanItemId]    [bigint] NULL,
    [RequestedDentistUserId] [bigint] NULL,
    [AssignedDentistUserId]  [bigint] NULL,
    [RoomId]                 [int] NULL,
    [DentalChairId]          [int] NULL,
    [ServiceDate]            [date] NOT NULL,
    [ScheduledStart]         [datetime2](7) NOT NULL,
    [ScheduledEnd]           [datetime2](7) NOT NULL,
    [QueueNumber]            [int] NULL,
    [UnitPrice]              [decimal](18, 2) NOT NULL,
    [Status]                 [varchar](40) NOT NULL DEFAULT ('PendingReceptionReview'),
    [PendingActionType]      [varchar](30) NULL,
    [PendingDentistUserId]   [bigint] NULL,
    [PendingScheduledStart]  [datetime2](7) NULL,
    [PendingScheduledEnd]    [datetime2](7) NULL,
    [Notes]                  [nvarchar](500) NULL,
    [CreatedAt]              [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]              [datetime2](7) NULL,

    CONSTRAINT [PK_AppointmentServices] PRIMARY KEY CLUSTERED ([AppointmentServiceId] ASC),
    CONSTRAINT [UQ_AppointmentServices_Id_Appt] UNIQUE NONCLUSTERED ([AppointmentServiceId] ASC, [AppointmentId] ASC),
    CONSTRAINT [UQ_AppointmentServices_Id_Appt_Dentist] UNIQUE NONCLUSTERED ([AppointmentServiceId] ASC, [AppointmentId] ASC, [AssignedDentistUserId] ASC),
    CONSTRAINT [UQ_AppointmentServices_Appt_Start] UNIQUE NONCLUSTERED ([AppointmentId] ASC, [ScheduledStart] ASC),
    CONSTRAINT [FK_AppointmentServices_Appointment] FOREIGN KEY ([AppointmentId]) REFERENCES [dbo].[Appointments] ([AppointmentId]),
    CONSTRAINT [FK_AppointmentServices_Appt_Combo] FOREIGN KEY ([AppointmentId], [ComboId]) REFERENCES [dbo].[Appointments] ([AppointmentId], [ComboId]),
    CONSTRAINT [FK_AppointmentServices_Service_Dept] FOREIGN KEY ([DepartmentServiceId], [DepartmentId]) REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId], [DepartmentId]),
    CONSTRAINT [FK_AppointmentServices_ComboItem_Combo_Service] FOREIGN KEY ([ComboItemId], [ComboId], [DepartmentServiceId]) REFERENCES [dbo].[ServiceComboItems] ([ComboItemId], [ComboId], [DepartmentServiceId]),
    CONSTRAINT [FK_AppointmentServices_PlanItem] FOREIGN KEY ([TreatmentPlanItemId]) REFERENCES [dbo].[TreatmentPlanItems] ([PlanItemId]),
    CONSTRAINT [FK_AppointmentServices_ReqDentist_Dept] FOREIGN KEY ([RequestedDentistUserId], [DepartmentId]) REFERENCES [dbo].[DentistProfiles] ([UserId], [DepartmentId]),
    CONSTRAINT [FK_AppointmentServices_AssDentist_Dept] FOREIGN KEY ([AssignedDentistUserId], [DepartmentId]) REFERENCES [dbo].[DentistProfiles] ([UserId], [DepartmentId]),
    CONSTRAINT [FK_AppointmentServices_PendingDentist_Dept] FOREIGN KEY ([PendingDentistUserId], [DepartmentId]) REFERENCES [dbo].[DentistProfiles] ([UserId], [DepartmentId]),
    CONSTRAINT [FK_AppointmentServices_Room_Dept] FOREIGN KEY ([RoomId], [DepartmentId]) REFERENCES [dbo].[Rooms] ([RoomId], [DepartmentId]),
    CONSTRAINT [FK_AppointmentServices_Chair_Room] FOREIGN KEY ([DentalChairId], [RoomId]) REFERENCES [dbo].[DentalChairs] ([DentalChairId], [RoomId]),

    CONSTRAINT [CK_AppointmentServices_ComboPair] CHECK (
        ([ComboId] IS NULL AND [ComboItemId] IS NULL)
        OR ([ComboId] IS NOT NULL AND [ComboItemId] IS NOT NULL)
    ),
    CONSTRAINT [CK_AppointmentServices_DateMatch] CHECK ([ServiceDate] = CONVERT([date], [ScheduledStart])),
    CONSTRAINT [CK_AppointmentServices_Time] CHECK ([ScheduledEnd] > [ScheduledStart]),
    CONSTRAINT [CK_AppointmentServices_ChairRequiresRoom] CHECK ([DentalChairId] IS NULL OR [RoomId] IS NOT NULL),
    CONSTRAINT [CK_AppointmentServices_ResourceAllocation] CHECK (
        [Status] NOT IN ('Confirmed', 'CheckedIn', 'Waiting', 'Called', 'InService', 'Completed')
        OR ([AssignedDentistUserId] IS NOT NULL AND [RoomId] IS NOT NULL AND [DentalChairId] IS NOT NULL)
    ),
    CONSTRAINT [CK_AppointmentServices_QueueRequired] CHECK (
        [Status] NOT IN ('CheckedIn', 'Waiting', 'Called', 'InService')
        OR [QueueNumber] IS NOT NULL
    ),
    CONSTRAINT [CK_AppointmentServices_QueueNumber] CHECK ([QueueNumber] IS NULL OR [QueueNumber] > 0),
    CONSTRAINT [CK_AppointmentServices_Price] CHECK ([UnitPrice] >= 0),
    CONSTRAINT [CK_AppointmentServices_Status] CHECK ([Status] IN (
        'PendingReceptionReview', 'Confirmed', 'CheckedIn', 'Waiting', 'Called',
        'InService', 'Completed', 'Cancelled', 'NoShow'
    )),
    CONSTRAINT [CK_AppointmentServices_PendingAction] CHECK ([PendingActionType] IS NULL OR [PendingActionType] IN ('Reschedule', 'ChangeDentist', 'ContactStaff')),
    CONSTRAINT [CK_AppointmentServices_PendingTime] CHECK ([PendingScheduledEnd] IS NULL OR [PendingScheduledStart] IS NULL OR [PendingScheduledEnd] > [PendingScheduledStart])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_AppointmentServices_Dept_Date_QueueNumber]
    ON [dbo].[AppointmentServices] ([DepartmentId] ASC, [ServiceDate] ASC, [QueueNumber] ASC)
    WHERE ([QueueNumber] IS NOT NULL);
GO

CREATE NONCLUSTERED INDEX [IX_AppointmentServices_Dentist_Schedule]
    ON [dbo].[AppointmentServices] ([AssignedDentistUserId] ASC, [ScheduledStart] ASC, [ScheduledEnd] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AppointmentServices_Chair_Schedule]
    ON [dbo].[AppointmentServices] ([DentalChairId] ASC, [ScheduledStart] ASC, [ScheduledEnd] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AppointmentServices_Dept_Date_Status]
    ON [dbo].[AppointmentServices] ([DepartmentId] ASC, [ServiceDate] ASC, [Status] ASC);
GO


-- =====================================================================================
-- 07. CLINICAL EXECUTION, ODONTOGRAM, REFERRALS & UNIFIED DIAGNOSTICS
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table 22: dbo.ClinicalEntries
-- Composite FKs ensure MedicalRecordId, AppointmentId, and AppointmentServiceId belong to the same PatientUserId
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ClinicalEntries] (
    [EntryId]              [bigint] IDENTITY(1,1) NOT NULL,
    [MedicalRecordId]      [bigint] NOT NULL,
    [PatientUserId]        [bigint] NOT NULL,
    [DentistUserId]        [bigint] NOT NULL,
    [AppointmentId]        [bigint] NULL,
    [AppointmentServiceId] [bigint] NULL,
    [ChiefComplaint]       [nvarchar](1000) NULL,
    [ClinicalExamination]  [nvarchar](max) NULL,
    [Diagnosis]            [nvarchar](1000) NULL,
    [TreatmentDone]        [nvarchar](max) NULL,
    [CreatedAt]            [datetime2](7) NOT NULL DEFAULT (sysdatetime()),

    CONSTRAINT [PK_ClinicalEntries] PRIMARY KEY CLUSTERED ([EntryId] ASC),
    CONSTRAINT [FK_ClinicalEntries_Record_Patient] FOREIGN KEY ([MedicalRecordId], [PatientUserId]) REFERENCES [dbo].[DentalMedicalRecords] ([RecordId], [PatientUserId]),
    CONSTRAINT [FK_ClinicalEntries_Patient] FOREIGN KEY ([PatientUserId]) REFERENCES [dbo].[PatientProfiles] ([UserId]),
    CONSTRAINT [FK_ClinicalEntries_Dentist] FOREIGN KEY ([DentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]),
    CONSTRAINT [FK_ClinicalEntries_Appointment_Patient] FOREIGN KEY ([AppointmentId], [PatientUserId]) REFERENCES [dbo].[Appointments] ([AppointmentId], [PatientUserId]),
    CONSTRAINT [FK_ClinicalEntries_ApptService_Appt] FOREIGN KEY ([AppointmentServiceId], [AppointmentId]) REFERENCES [dbo].[AppointmentServices] ([AppointmentServiceId], [AppointmentId]),
    CONSTRAINT [CK_ClinicalEntries_ServiceRequiresAppt] CHECK ([AppointmentServiceId] IS NULL OR [AppointmentId] IS NOT NULL)
);
GO

CREATE NONCLUSTERED INDEX [IX_ClinicalEntries_Record_CreatedAt]
    ON [dbo].[ClinicalEntries] ([MedicalRecordId] ASC, [CreatedAt] ASC);
GO

-- -------------------------------------------------------------------------------------
-- Table 23: dbo.OdontogramEntries
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[OdontogramEntries] (
    [OdontogramId]    [bigint] IDENTITY(1,1) NOT NULL,
    [MedicalRecordId] [bigint] NOT NULL,
    [DentistUserId]   [bigint] NOT NULL,
    [ToothNumber]     [varchar](10) NOT NULL,
    [Condition]       [varchar](50) NOT NULL,
    [Surface]         [varchar](20) NULL,
    [DiagnosisNotes]  [nvarchar](500) NULL,
    [IsActive]        [bit] NOT NULL DEFAULT (1),
    [CreatedAt]       [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]       [datetime2](7) NULL,

    CONSTRAINT [PK_OdontogramEntries] PRIMARY KEY CLUSTERED ([OdontogramId] ASC),
    CONSTRAINT [FK_OdontogramEntries_Record] FOREIGN KEY ([MedicalRecordId]) REFERENCES [dbo].[DentalMedicalRecords] ([RecordId]),
    CONSTRAINT [FK_OdontogramEntries_Dentist] FOREIGN KEY ([DentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table 24: dbo.TreatmentSessions
-- Linked to Appointment, AppointmentService, and PatientUserId for end-to-end clinical consistency
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[TreatmentSessions] (
    [SessionId]                  [bigint] IDENTITY(1,1) NOT NULL,
    [AppointmentId]              [bigint] NOT NULL,
    [AppointmentServiceId]       [bigint] NOT NULL,
    [PatientUserId]              [bigint] NOT NULL,
    [TreatmentPlanId]            [bigint] NULL,
    [DentistUserId]              [bigint] NOT NULL,
    [SessionDate]                [date] NOT NULL,
    [Notes]                      [nvarchar](max) NULL,
    [ClinicalObservations]       [nvarchar](max) NULL,
    [NextSessionRecommendations] [nvarchar](1000) NULL,
    [CreatedAt]                  [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]                  [datetime2](7) NULL,

    CONSTRAINT [PK_TreatmentSessions] PRIMARY KEY CLUSTERED ([SessionId] ASC),
    CONSTRAINT [UQ_TreatmentSessions_Id_Appt] UNIQUE NONCLUSTERED ([SessionId] ASC, [AppointmentId] ASC),
    CONSTRAINT [UQ_TreatmentSessions_Id_Patient] UNIQUE NONCLUSTERED ([SessionId] ASC, [PatientUserId] ASC),
    CONSTRAINT [UQ_TreatmentSessions_Id_Appt_Patient] UNIQUE NONCLUSTERED ([SessionId] ASC, [AppointmentId] ASC, [PatientUserId] ASC),
    CONSTRAINT [UQ_TreatmentSessions_Id_Appt_Service] UNIQUE NONCLUSTERED ([SessionId] ASC, [AppointmentId] ASC, [AppointmentServiceId] ASC),
    CONSTRAINT [FK_TreatmentSessions_Appt_Patient] FOREIGN KEY ([AppointmentId], [PatientUserId]) REFERENCES [dbo].[Appointments] ([AppointmentId], [PatientUserId]),
    CONSTRAINT [FK_TreatmentSessions_ApptService_Appt] FOREIGN KEY ([AppointmentServiceId], [AppointmentId]) REFERENCES [dbo].[AppointmentServices] ([AppointmentServiceId], [AppointmentId]),
    CONSTRAINT [FK_TreatmentSessions_Plan_Patient] FOREIGN KEY ([TreatmentPlanId], [PatientUserId]) REFERENCES [dbo].[TreatmentPlans] ([TreatmentPlanId], [PatientUserId]),
    CONSTRAINT [FK_TreatmentSessions_Patient] FOREIGN KEY ([PatientUserId]) REFERENCES [dbo].[PatientProfiles] ([UserId]),
    CONSTRAINT [FK_TreatmentSessions_Dentist] FOREIGN KEY ([DentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId])
);
GO

CREATE NONCLUSTERED INDEX [IX_TreatmentSessions_AppointmentService]
    ON [dbo].[TreatmentSessions] ([AppointmentServiceId] ASC);
GO

-- -------------------------------------------------------------------------------------
-- Table 25: dbo.PerformedServices
-- Composite FK guarantees AppointmentId and AppointmentServiceId match TreatmentSessions
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[PerformedServices] (
    [PerformedServiceId]   [bigint] IDENTITY(1,1) NOT NULL,
    [TreatmentSessionId]   [bigint] NOT NULL,
    [AppointmentId]        [bigint] NOT NULL,
    [AppointmentServiceId] [bigint] NOT NULL,
    [DepartmentServiceId]  [int] NOT NULL,
    [ToothNumber]          [varchar](20) NULL,
    [Quantity]             [int] NOT NULL DEFAULT (1),
    [UnitPrice]            [decimal](18, 2) NOT NULL,
    [Notes]                [nvarchar](500) NULL,
    [CreatedAt]            [datetime2](7) NOT NULL DEFAULT (sysdatetime()),

    CONSTRAINT [PK_PerformedServices] PRIMARY KEY CLUSTERED ([PerformedServiceId] ASC),
    CONSTRAINT [UQ_PerformedServices_Id_Appt] UNIQUE NONCLUSTERED ([PerformedServiceId] ASC, [AppointmentId] ASC),
    CONSTRAINT [FK_PerformedServices_Session_Appt_Service] FOREIGN KEY ([TreatmentSessionId], [AppointmentId], [AppointmentServiceId]) REFERENCES [dbo].[TreatmentSessions] ([SessionId], [AppointmentId], [AppointmentServiceId]),
    CONSTRAINT [FK_PerformedServices_Service] FOREIGN KEY ([DepartmentServiceId]) REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId]),
    CONSTRAINT [CK_PerformedServices_Qty] CHECK ([Quantity] > 0),
    CONSTRAINT [CK_PerformedServices_Price] CHECK ([UnitPrice] >= 0)
);
GO

CREATE NONCLUSTERED INDEX [IX_PerformedServices_Session]
    ON [dbo].[PerformedServices] ([TreatmentSessionId] ASC);
GO

-- -------------------------------------------------------------------------------------
-- Table 26: dbo.DepartmentReferrals
-- Inter-department referral linked to source/destination AppointmentServices
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DepartmentReferrals] (
    [ReferralId]                      [bigint] IDENTITY(1,1) NOT NULL,
    [TreatmentSessionId]              [bigint] NULL,
    [FromDepartmentId]                [int] NOT NULL,
    [ToDepartmentId]                  [int] NOT NULL,
    [RequestedServiceId]              [int] NULL,
    [AssignedDentistUserId]           [bigint] NULL,
    [ReviewedByUserId]                [bigint] NULL,
    [Reason]                          [nvarchar](500) NOT NULL,
    [ClinicalInstructions]            [nvarchar](1000) NULL,
    [Urgency]                         [varchar](20) NOT NULL DEFAULT ('Normal'),
    [Status]                          [varchar](30) NOT NULL DEFAULT ('Pending'),
    [SourceAppointmentServiceId]      [bigint] NULL,
    [DestinationAppointmentServiceId] [bigint] NULL,
    [CreatedAt]                       [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [ReviewedAt]                      [datetime2](7) NULL,

    CONSTRAINT [PK_DepartmentReferrals] PRIMARY KEY CLUSTERED ([ReferralId] ASC),
    CONSTRAINT [FK_DepartmentReferrals_Session] FOREIGN KEY ([TreatmentSessionId]) REFERENCES [dbo].[TreatmentSessions] ([SessionId]),
    CONSTRAINT [FK_DepartmentReferrals_FromDept] FOREIGN KEY ([FromDepartmentId]) REFERENCES [dbo].[Departments] ([DepartmentId]),
    CONSTRAINT [FK_DepartmentReferrals_ToDept] FOREIGN KEY ([ToDepartmentId]) REFERENCES [dbo].[Departments] ([DepartmentId]),
    CONSTRAINT [FK_DepartmentReferrals_Service_ToDept] FOREIGN KEY ([RequestedServiceId], [ToDepartmentId]) REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId], [DepartmentId]),
    CONSTRAINT [FK_DepartmentReferrals_Dentist_ToDept] FOREIGN KEY ([AssignedDentistUserId], [ToDepartmentId]) REFERENCES [dbo].[DentistProfiles] ([UserId], [DepartmentId]),
    CONSTRAINT [FK_DepartmentReferrals_ReviewedBy] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
    CONSTRAINT [FK_DepartmentReferrals_SrcApptService] FOREIGN KEY ([SourceAppointmentServiceId]) REFERENCES [dbo].[AppointmentServices] ([AppointmentServiceId]),
    CONSTRAINT [FK_DepartmentReferrals_DstApptService] FOREIGN KEY ([DestinationAppointmentServiceId]) REFERENCES [dbo].[AppointmentServices] ([AppointmentServiceId]),

    CONSTRAINT [CK_DepartmentReferrals_Depts] CHECK ([FromDepartmentId] <> [ToDepartmentId]),
    CONSTRAINT [CK_DepartmentReferrals_Urgency] CHECK ([Urgency] IN ('Normal', 'Urgent')),
    CONSTRAINT [CK_DepartmentReferrals_Status] CHECK ([Status] IN ('Pending', 'Accepted', 'Rejected', 'Assigned', 'Completed'))
);
GO

-- -------------------------------------------------------------------------------------
-- Table 27: dbo.Diagnostics
-- UNIFIED: Consolidates DiagnosticOrders and DiagnosticResults into a single table
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Diagnostics] (
    [DiagnosticId]            [bigint] IDENTITY(1,1) NOT NULL,
    [AppointmentServiceId]    [bigint] NOT NULL,
    [DepartmentServiceId]     [int] NOT NULL,
    [OrderingDentistUserId]   [bigint] NOT NULL,
    [ClinicalIndication]      [nvarchar](1000) NOT NULL,
    [Status]                  [varchar](30) NOT NULL DEFAULT ('Ordered'),
    [ImagingType]             [varchar](50) NULL,
    [FileUrl]                 [nvarchar](1000) NULL,
    [ThumbnailUrl]            [nvarchar](1000) NULL,
    [Findings]                [nvarchar](max) NULL,
    [ReviewedByDentistUserId] [bigint] NULL,
    [ReviewedAt]              [datetime2](7) NULL,
    [UploadedByUserId]        [bigint] NULL,
    [CreatedAt]               [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [CompletedAt]             [datetime2](7) NULL,

    CONSTRAINT [PK_Diagnostics] PRIMARY KEY CLUSTERED ([DiagnosticId] ASC),
    CONSTRAINT [FK_Diagnostics_AppointmentService] FOREIGN KEY ([AppointmentServiceId]) REFERENCES [dbo].[AppointmentServices] ([AppointmentServiceId]),
    CONSTRAINT [FK_Diagnostics_DepartmentService] FOREIGN KEY ([DepartmentServiceId]) REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId]),
    CONSTRAINT [FK_Diagnostics_OrderingDentist] FOREIGN KEY ([OrderingDentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]),
    CONSTRAINT [FK_Diagnostics_ReviewedByDentist] FOREIGN KEY ([ReviewedByDentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]),
    CONSTRAINT [FK_Diagnostics_UploadedByUser] FOREIGN KEY ([UploadedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
    CONSTRAINT [CK_Diagnostics_Status] CHECK ([Status] IN ('Ordered', 'InProgress', 'Completed', 'Cancelled'))
);
GO

CREATE NONCLUSTERED INDEX [IX_Diagnostics_AppointmentService_Status]
    ON [dbo].[Diagnostics] ([AppointmentServiceId] ASC, [Status] ASC);
GO


-- =====================================================================================
-- 08. SIMPLIFIED PHARMACY & DIRECT DENTIST DISPENSING (3 TABLES)
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table 28: dbo.Medicines
-- Direct stock management on Medicines (No batch/lot or stock-transaction tables)
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Medicines] (
    [MedicineId]        [bigint] IDENTITY(1,1) NOT NULL,
    [MedicineCode]      [varchar](50) NOT NULL,
    [MedicineName]      [nvarchar](200) NOT NULL,
    [ActiveIngredient]  [nvarchar](200) NULL,
    [Unit]              [nvarchar](50) NOT NULL,
    [UnitPrice]         [decimal](18, 2) NOT NULL,
    [StockQuantity]     [int] NOT NULL DEFAULT (0),
    [MinimumStockAlert] [int] NOT NULL DEFAULT (10),
    [UsageInstructions] [nvarchar](1000) NULL,
    [IsActive]          [bit] NOT NULL DEFAULT (1),
    [CreatedAt]         [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]         [datetime2](7) NULL,

    CONSTRAINT [PK_Medicines] PRIMARY KEY CLUSTERED ([MedicineId] ASC),
    CONSTRAINT [UQ_Medicines_Code] UNIQUE NONCLUSTERED ([MedicineCode] ASC),
    CONSTRAINT [CK_Medicines_UnitPrice] CHECK ([UnitPrice] >= 0),
    CONSTRAINT [CK_Medicines_StockQuantity] CHECK ([StockQuantity] >= 0),
    CONSTRAINT [CK_Medicines_StockAlert] CHECK ([MinimumStockAlert] >= 0)
);
GO

-- -------------------------------------------------------------------------------------
-- Table 29: dbo.Prescriptions
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Prescriptions] (
    [PrescriptionId]     [bigint] IDENTITY(1,1) NOT NULL,
    [TreatmentSessionId] [bigint] NOT NULL,
    [AppointmentId]      [bigint] NOT NULL,
    [DentistUserId]      [bigint] NOT NULL,
    [PatientUserId]      [bigint] NOT NULL,
    [Diagnosis]          [nvarchar](500) NULL,
    [Notes]              [nvarchar](1000) NULL,
    [Status]             [varchar](30) NOT NULL DEFAULT ('Active'),
    [CreatedAt]          [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]          [datetime2](7) NULL,

    CONSTRAINT [PK_Prescriptions] PRIMARY KEY CLUSTERED ([PrescriptionId] ASC),
    CONSTRAINT [UQ_Prescriptions_Session] UNIQUE NONCLUSTERED ([TreatmentSessionId] ASC),
    CONSTRAINT [UQ_Prescriptions_Id_Appt] UNIQUE NONCLUSTERED ([PrescriptionId] ASC, [AppointmentId] ASC),
    CONSTRAINT [FK_Prescriptions_Session_Appt_Patient] FOREIGN KEY ([TreatmentSessionId], [AppointmentId], [PatientUserId]) REFERENCES [dbo].[TreatmentSessions] ([SessionId], [AppointmentId], [PatientUserId]),
    CONSTRAINT [FK_Prescriptions_Dentist] FOREIGN KEY ([DentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]),
    CONSTRAINT [FK_Prescriptions_Patient] FOREIGN KEY ([PatientUserId]) REFERENCES [dbo].[PatientProfiles] ([UserId]),
    CONSTRAINT [CK_Prescriptions_Status] CHECK ([Status] IN ('Active', 'Dispensed', 'Cancelled'))
);
GO

-- -------------------------------------------------------------------------------------
-- Table 30: dbo.PrescriptionItems
-- Includes single-pass actual dispensing by Dentist (QuantityDispensed, UnitPriceAtDispense)
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[PrescriptionItems] (
    [PrescriptionItemId]  [bigint] IDENTITY(1,1) NOT NULL,
    [PrescriptionId]      [bigint] NOT NULL,
    [AppointmentId]       [bigint] NOT NULL,
    [MedicineId]          [bigint] NOT NULL,
    [Dosage]              [nvarchar](100) NOT NULL,
    [Frequency]           [nvarchar](100) NOT NULL,
    [Instructions]        [nvarchar](500) NULL,
    [Quantity]            [int] NOT NULL,
    [QuantityDispensed]   [int] NOT NULL DEFAULT (0),
    [DispensedAt]         [datetime2](7) NULL,
    [DispensedByUserId]   [bigint] NULL,
    [UnitPriceAtDispense] [decimal](18, 2) NULL,
    [CreatedAt]           [datetime2](7) NOT NULL DEFAULT (sysdatetime()),

    CONSTRAINT [PK_PrescriptionItems] PRIMARY KEY CLUSTERED ([PrescriptionItemId] ASC),
    CONSTRAINT [UQ_PrescriptionItems_Presc_Medicine] UNIQUE NONCLUSTERED ([PrescriptionId] ASC, [MedicineId] ASC),
    CONSTRAINT [UQ_PrescriptionItems_Id_Appt] UNIQUE NONCLUSTERED ([PrescriptionItemId] ASC, [AppointmentId] ASC),
    CONSTRAINT [FK_PrescriptionItems_Presc_Appt] FOREIGN KEY ([PrescriptionId], [AppointmentId]) REFERENCES [dbo].[Prescriptions] ([PrescriptionId], [AppointmentId]),
    CONSTRAINT [FK_PrescriptionItems_Medicine] FOREIGN KEY ([MedicineId]) REFERENCES [dbo].[Medicines] ([MedicineId]),
    CONSTRAINT [FK_PrescriptionItems_DispensedByDentist] FOREIGN KEY ([DispensedByUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]),
    CONSTRAINT [CK_PrescriptionItems_Qty] CHECK ([Quantity] > 0),
    CONSTRAINT [CK_PrescriptionItems_QtyDispensed] CHECK ([QuantityDispensed] >= 0 AND [QuantityDispensed] <= [Quantity]),
    CONSTRAINT [CK_PrescriptionItems_Price] CHECK ([UnitPriceAtDispense] IS NULL OR [UnitPriceAtDispense] >= 0),
    CONSTRAINT [CK_PrescriptionItems_DispenseAudit] CHECK (
        ([QuantityDispensed] = 0 AND [DispensedAt] IS NULL AND [DispensedByUserId] IS NULL)
        OR ([QuantityDispensed] > 0 AND [DispensedAt] IS NOT NULL AND [DispensedByUserId] IS NOT NULL AND [UnitPriceAtDispense] IS NOT NULL)
    )
);
GO


-- =====================================================================================
-- 09. BILLING, INVOICING & PAYMENTS
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table 31: dbo.Invoices
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Invoices] (
    [InvoiceId]         [bigint] IDENTITY(1,1) NOT NULL,
    [InvoiceCode]       [varchar](50) NOT NULL,
    [AppointmentId]     [bigint] NOT NULL,
    [GeneratedByUserId] [bigint] NOT NULL,
    [TotalAmount]       [decimal](18, 2) NOT NULL DEFAULT (0),
    [DiscountAmount]    [decimal](18, 2) NOT NULL DEFAULT (0),
    [FinalAmount]       [decimal](18, 2) NOT NULL DEFAULT (0),
    [Status]            [varchar](30) NOT NULL DEFAULT ('Unpaid'),
    [CreatedAt]         [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]         [datetime2](7) NULL,

    CONSTRAINT [PK_Invoices] PRIMARY KEY CLUSTERED ([InvoiceId] ASC),
    CONSTRAINT [UQ_Invoices_Code] UNIQUE NONCLUSTERED ([InvoiceCode] ASC),
    CONSTRAINT [UQ_Invoices_Id_Appt] UNIQUE NONCLUSTERED ([InvoiceId] ASC, [AppointmentId] ASC),
    CONSTRAINT [FK_Invoices_Appointments] FOREIGN KEY ([AppointmentId]) REFERENCES [dbo].[Appointments] ([AppointmentId]),
    CONSTRAINT [FK_Invoices_UserAccounts] FOREIGN KEY ([GeneratedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
    CONSTRAINT [CK_Invoices_Amounts] CHECK (
        [TotalAmount] >= 0
        AND [DiscountAmount] >= 0
        AND [DiscountAmount] <= [TotalAmount]
        AND [FinalAmount] = ([TotalAmount] - [DiscountAmount])
    ),
    CONSTRAINT [CK_Invoices_Status] CHECK ([Status] IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Cancelled'))
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_Invoices_AppointmentId]
    ON [dbo].[Invoices] ([AppointmentId] ASC);
GO

-- -------------------------------------------------------------------------------------
-- Table 32: dbo.InvoiceItems
-- Composite FKs ensure PerformedServiceId or PrescriptionItemId belongs to the Invoice's AppointmentId
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[InvoiceItems] (
    [InvoiceItemId]      [bigint] IDENTITY(1,1) NOT NULL,
    [InvoiceId]          [bigint] NOT NULL,
    [AppointmentId]      [bigint] NOT NULL,
    [PerformedServiceId] [bigint] NULL,
    [PrescriptionItemId] [bigint] NULL,
    [Description]        [nvarchar](500) NOT NULL,
    [Quantity]           [int] NOT NULL DEFAULT (1),
    [UnitPrice]          [decimal](18, 2) NOT NULL,
    [TotalAmount]        AS ([Quantity] * [UnitPrice]) PERSISTED,

    CONSTRAINT [PK_InvoiceItems] PRIMARY KEY CLUSTERED ([InvoiceItemId] ASC),
    CONSTRAINT [FK_InvoiceItems_Invoice_Appt] FOREIGN KEY ([InvoiceId], [AppointmentId]) REFERENCES [dbo].[Invoices] ([InvoiceId], [AppointmentId]),
    CONSTRAINT [FK_InvoiceItems_PerformedService_Appt] FOREIGN KEY ([PerformedServiceId], [AppointmentId]) REFERENCES [dbo].[PerformedServices] ([PerformedServiceId], [AppointmentId]),
    CONSTRAINT [FK_InvoiceItems_PrescriptionItem_Appt] FOREIGN KEY ([PrescriptionItemId], [AppointmentId]) REFERENCES [dbo].[PrescriptionItems] ([PrescriptionItemId], [AppointmentId]),
    CONSTRAINT [CK_InvoiceItems_Qty] CHECK ([Quantity] > 0),
    CONSTRAINT [CK_InvoiceItems_Price] CHECK ([UnitPrice] >= 0),
    CONSTRAINT [CK_InvoiceItems_SourceLink] CHECK (
        ([PerformedServiceId] IS NOT NULL AND [PrescriptionItemId] IS NULL)
        OR ([PerformedServiceId] IS NULL AND [PrescriptionItemId] IS NOT NULL)
    )
);
GO

CREATE NONCLUSTERED INDEX [IX_InvoiceItems_Invoice]
    ON [dbo].[InvoiceItems] ([InvoiceId] ASC);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_PerfService]
    ON [dbo].[InvoiceItems] ([PerformedServiceId] ASC)
    WHERE ([PerformedServiceId] IS NOT NULL);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_PrescriptionItem]
    ON [dbo].[InvoiceItems] ([PrescriptionItemId] ASC)
    WHERE ([PrescriptionItemId] IS NOT NULL);
GO

-- -------------------------------------------------------------------------------------
-- Table 33: dbo.Payments
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Payments] (
    [PaymentId]            [bigint] IDENTITY(1,1) NOT NULL,
    [InvoiceId]            [bigint] NOT NULL,
    [Amount]               [decimal](18, 2) NOT NULL,
    [PaymentType]          [varchar](30) NOT NULL DEFAULT ('Final'),
    [Method]               [varchar](30) NOT NULL DEFAULT ('Cash'),
    [TransactionReference] [varchar](100) NULL,
    [Status]               [varchar](30) NOT NULL DEFAULT ('Successful'),
    [ReceivedByUserId]     [bigint] NOT NULL,
    [PaymentDate]          [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [Notes]                [nvarchar](500) NULL,

    CONSTRAINT [PK_Payments] PRIMARY KEY CLUSTERED ([PaymentId] ASC),
    CONSTRAINT [FK_Payments_Invoices] FOREIGN KEY ([InvoiceId]) REFERENCES [dbo].[Invoices] ([InvoiceId]),
    CONSTRAINT [FK_Payments_Receiver] FOREIGN KEY ([ReceivedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
    CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [CK_Payments_Type] CHECK ([PaymentType] IN ('Deposit', 'Partial', 'Final', 'Refund')),
    CONSTRAINT [CK_Payments_Method] CHECK ([Method] IN ('Cash', 'CreditCard', 'BankTransfer', 'Online')),
    CONSTRAINT [CK_Payments_Status] CHECK ([Status] IN ('Pending', 'Successful', 'Failed', 'Cancelled'))
);
GO

CREATE NONCLUSTERED INDEX [IX_Payments_Invoice_Status]
    ON [dbo].[Payments] ([InvoiceId] ASC, [Status] ASC);
GO


-- =====================================================================================
-- 10. FOLLOW-UP RECOMMENDATIONS, MULTI-DENTIST FEEDBACK & NOTIFICATIONS
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table 34: dbo.FollowUpSchedules
-- Doctor's clinical follow-up recommendation only, NOT a booked appointment
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[FollowUpSchedules] (
    [FollowUpId]             [bigint] IDENTITY(1,1) NOT NULL,
    [TreatmentSessionId]     [bigint] NOT NULL,
    [DentistUserId]          [bigint] NOT NULL,
    [PatientUserId]          [bigint] NOT NULL,
    [RecommendedDate]        [date] NOT NULL,
    [RecommendedServiceId]   [int] NULL,
    [ClinicalReason]         [nvarchar](500) NOT NULL,
    [Status]                 [varchar](30) NOT NULL DEFAULT ('Proposed'),
    [ResultingAppointmentId] [bigint] NULL,
    [CreatedAt]              [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]              [datetime2](7) NULL,

    CONSTRAINT [PK_FollowUpSchedules] PRIMARY KEY CLUSTERED ([FollowUpId] ASC),
    CONSTRAINT [FK_FollowUpSchedules_Session_Patient] FOREIGN KEY ([TreatmentSessionId], [PatientUserId]) REFERENCES [dbo].[TreatmentSessions] ([SessionId], [PatientUserId]),
    CONSTRAINT [FK_FollowUpSchedules_Dentist] FOREIGN KEY ([DentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]),
    CONSTRAINT [FK_FollowUpSchedules_Patient] FOREIGN KEY ([PatientUserId]) REFERENCES [dbo].[PatientProfiles] ([UserId]),
    CONSTRAINT [FK_FollowUpSchedules_Service] FOREIGN KEY ([RecommendedServiceId]) REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId]),
    CONSTRAINT [FK_FollowUpSchedules_ResultAppt_Patient] FOREIGN KEY ([ResultingAppointmentId], [PatientUserId]) REFERENCES [dbo].[Appointments] ([AppointmentId], [PatientUserId]),
    CONSTRAINT [CK_FollowUpSchedules_Status] CHECK ([Status] IN ('Proposed', 'Scheduled', 'Completed', 'Overdue', 'Dismissed', 'Cancelled'))
);
GO

CREATE NONCLUSTERED INDEX [IX_FollowUpSchedules_Date_Status]
    ON [dbo].[FollowUpSchedules] ([RecommendedDate] ASC, [Status] ASC);
GO

-- -------------------------------------------------------------------------------------
-- Table 35: dbo.VisitFeedback
-- Composite FKs ensure PatientUserId matches Appointments and DentistUserId matches AssignedDentistUserId
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[VisitFeedback] (
    [FeedbackId]           [bigint] IDENTITY(1,1) NOT NULL,
    [AppointmentId]        [bigint] NOT NULL,
    [AppointmentServiceId] [bigint] NOT NULL,
    [PatientUserId]        [bigint] NOT NULL,
    [DentistUserId]        [bigint] NOT NULL,
    [ClinicRating]         [int] NOT NULL,
    [DentistRating]        [int] NOT NULL,
    [Comments]             [nvarchar](1000) NULL,
    [Status]               [varchar](30) NOT NULL DEFAULT ('Published'),
    [CreatedAt]            [datetime2](7) NOT NULL DEFAULT (sysdatetime()),

    CONSTRAINT [PK_VisitFeedback] PRIMARY KEY CLUSTERED ([FeedbackId] ASC),
    CONSTRAINT [UQ_VisitFeedback_ApptService] UNIQUE NONCLUSTERED ([AppointmentServiceId] ASC),
    CONSTRAINT [UQ_VisitFeedback_Appt_Dentist] UNIQUE NONCLUSTERED ([AppointmentId] ASC, [DentistUserId] ASC),
    CONSTRAINT [FK_VisitFeedback_Appt_Patient] FOREIGN KEY ([AppointmentId], [PatientUserId]) REFERENCES [dbo].[Appointments] ([AppointmentId], [PatientUserId]),
    CONSTRAINT [FK_VisitFeedback_ApptService_Appt_Dentist] FOREIGN KEY ([AppointmentServiceId], [AppointmentId], [DentistUserId]) REFERENCES [dbo].[AppointmentServices] ([AppointmentServiceId], [AppointmentId], [AssignedDentistUserId]),
    CONSTRAINT [FK_VisitFeedback_Patient] FOREIGN KEY ([PatientUserId]) REFERENCES [dbo].[PatientProfiles] ([UserId]),
    CONSTRAINT [FK_VisitFeedback_Dentist] FOREIGN KEY ([DentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]),
    CONSTRAINT [CK_VisitFeedback_Ratings] CHECK ([ClinicRating] BETWEEN 1 AND 5 AND [DentistRating] BETWEEN 1 AND 5),
    CONSTRAINT [CK_VisitFeedback_Status] CHECK ([Status] IN ('Draft', 'Published', 'Hidden'))
);
GO

-- -------------------------------------------------------------------------------------
-- Table 36: dbo.Notifications
-- Unified notifications & automated T-24h / T-8h reminders (supports per-service reminders via AppointmentServiceId)
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Notifications] (
    [NotificationId]       [bigint] IDENTITY(1,1) NOT NULL,
    [UserId]               [bigint] NOT NULL,
    [AppointmentId]        [bigint] NULL,
    [AppointmentServiceId] [bigint] NULL,
    [FollowUpScheduleId]   [bigint] NULL,
    [Type]                 [varchar](50) NOT NULL,
    [Channel]              [varchar](20) NOT NULL DEFAULT ('InApp'),
    [Subject]              [nvarchar](250) NOT NULL,
    [Content]              [nvarchar](max) NOT NULL,
    [ScheduledSendTime]    [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [DeliveryStatus]       [varchar](30) NOT NULL DEFAULT ('Pending'),
    [SentAt]               [datetime2](7) NULL,
    [RetryCount]           [int] NOT NULL DEFAULT (0),
    [NextRetryAt]          [datetime2](7) NULL,
    [ErrorMessage]         [nvarchar](500) NULL,
    [ReadAt]               [datetime2](7) NULL,
    [DeduplicationKey]     [varchar](150) NULL,
    [CreatedAt]            [datetime2](7) NOT NULL DEFAULT (sysdatetime()),

    CONSTRAINT [PK_Notifications] PRIMARY KEY CLUSTERED ([NotificationId] ASC),
    CONSTRAINT [FK_Notifications_UserAccounts] FOREIGN KEY ([UserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
    CONSTRAINT [FK_Notifications_Appointments] FOREIGN KEY ([AppointmentId]) REFERENCES [dbo].[Appointments] ([AppointmentId]),
    CONSTRAINT [FK_Notifications_ApptService_Appt] FOREIGN KEY ([AppointmentServiceId], [AppointmentId]) REFERENCES [dbo].[AppointmentServices] ([AppointmentServiceId], [AppointmentId]),
    CONSTRAINT [FK_Notifications_FollowUp] FOREIGN KEY ([FollowUpScheduleId]) REFERENCES [dbo].[FollowUpSchedules] ([FollowUpId]),
    CONSTRAINT [CK_Notifications_ServiceRequiresAppt] CHECK ([AppointmentServiceId] IS NULL OR [AppointmentId] IS NOT NULL),
    CONSTRAINT [CK_Notifications_RetryCount] CHECK ([RetryCount] >= 0),
    CONSTRAINT [CK_Notifications_Channel] CHECK ([Channel] IN ('InApp', 'Email', 'SMS')),
    CONSTRAINT [CK_Notifications_Status] CHECK ([DeliveryStatus] IN ('Pending', 'Sent', 'Failed', 'Read'))
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_Notifications_DeduplicationKey]
    ON [dbo].[Notifications] ([DeduplicationKey] ASC)
    WHERE ([DeduplicationKey] IS NOT NULL);
GO

CREATE NONCLUSTERED INDEX [IX_Notifications_User_Read_Delivery]
    ON [dbo].[Notifications] ([UserId] ASC, [DeliveryStatus] ASC, [ReadAt] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_Notifications_Scheduled_Pending]
    ON [dbo].[Notifications] ([ScheduledSendTime] ASC, [DeliveryStatus] ASC)
    WHERE ([DeliveryStatus] = 'Pending');
GO


-- =====================================================================================
-- 11. CONTENT & DENTAL HEALTH BLOG
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table 37: dbo.BlogCategories
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[BlogCategories] (
    [CategoryId]   [int] IDENTITY(1,1) NOT NULL,
    [Name]         [nvarchar](100) NOT NULL,
    [Slug]         [varchar](120) NOT NULL,
    [Description]  [nvarchar](500) NULL,
    [DisplayOrder] [int] NOT NULL DEFAULT (0),

    CONSTRAINT [PK_BlogCategories] PRIMARY KEY CLUSTERED ([CategoryId] ASC),
    CONSTRAINT [UQ_BlogCategories_Slug] UNIQUE NONCLUSTERED ([Slug] ASC)
);
GO

-- -------------------------------------------------------------------------------------
-- Table 38: dbo.BlogPosts
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[BlogPosts] (
    [BlogPostId]       [bigint] IDENTITY(1,1) NOT NULL,
    [CategoryId]       [int] NOT NULL,
    [AuthorUserId]     [bigint] NOT NULL,
    [Title]            [nvarchar](250) NOT NULL,
    [Slug]             [varchar](250) NOT NULL,
    [Summary]          [nvarchar](500) NULL,
    [Content]          [nvarchar](max) NOT NULL,
    [ThumbnailUrl]     [nvarchar](500) NULL,
    [VideoUrl]         [nvarchar](500) NULL,
    [EmbedVideoId]     [varchar](50) NULL,
    [ReadTimeMinutes]  [int] NOT NULL DEFAULT (5),
    [IsFeatured]       [bit] NOT NULL DEFAULT (0),
    [ViewCount]        [int] NOT NULL DEFAULT (0),
    [PostType]         [varchar](30) NOT NULL DEFAULT ('Blog'),
    [Status]           [varchar](30) NOT NULL DEFAULT ('Draft'),
    [RejectionReason]  [nvarchar](500) NULL,
    [ApprovedByUserId] [bigint] NULL,
    [ApprovedAt]       [datetime2](7) NULL,
    [PublishedAt]      [datetime2](7) NULL,
    [CreatedAt]        [datetime2](7) NOT NULL DEFAULT (sysdatetime()),
    [UpdatedAt]        [datetime2](7) NULL,

    CONSTRAINT [PK_BlogPosts] PRIMARY KEY CLUSTERED ([BlogPostId] ASC),
    CONSTRAINT [UQ_BlogPosts_Slug] UNIQUE NONCLUSTERED ([Slug] ASC),
    CONSTRAINT [FK_BlogPosts_Category] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[BlogCategories] ([CategoryId]),
    CONSTRAINT [FK_BlogPosts_Author] FOREIGN KEY ([AuthorUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
    CONSTRAINT [FK_BlogPosts_Approver] FOREIGN KEY ([ApprovedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
    CONSTRAINT [CK_BlogPosts_Metrics] CHECK ([ReadTimeMinutes] > 0 AND [ViewCount] >= 0),
    CONSTRAINT [CK_BlogPosts_Type] CHECK ([PostType] IN ('Blog', 'ClinicNews', 'Announcement')),
    CONSTRAINT [CK_BlogPosts_Status] CHECK ([Status] IN ('Draft', 'PendingReview', 'Published', 'Rejected', 'Archived'))
);
GO

CREATE NONCLUSTERED INDEX [IX_BlogPosts_Status_PublishedAt]
    ON [dbo].[BlogPosts] ([Status] ASC, [PublishedAt] DESC);
GO

CREATE NONCLUSTERED INDEX [IX_BlogPosts_Category]
    ON [dbo].[BlogPosts] ([CategoryId] ASC);
GO


-- =====================================================================================
-- 12. DATABASE VIEWS & STORED PROCEDURES
-- =====================================================================================

EXEC(N'CREATE VIEW dbo.vw_PublishedBlogPosts AS
SELECT
    bp.BlogPostId,
    bp.Title,
    bp.Slug,
    bp.Summary,
    bp.ThumbnailUrl,
    bp.VideoUrl,
    bp.EmbedVideoId,
    bp.ReadTimeMinutes,
    bp.IsFeatured,
    bp.ViewCount,
    bp.PublishedAt,
    bp.CreatedAt,
    bp.UpdatedAt,
    bc.CategoryId,
    bc.Name AS CategoryName,
    bc.Slug AS CategorySlug,
    ua.UserId AS AuthorUserId,
    COALESCE(sp.FullName, ua.Email) AS AuthorName,
    ua.AvatarUrl AS AuthorAvatarUrl,
    dp.Qualification,
    dp.YearsOfExperience
FROM dbo.BlogPosts bp
INNER JOIN dbo.BlogCategories bc ON bc.CategoryId = bp.CategoryId
INNER JOIN dbo.UserAccounts ua ON ua.UserId = bp.AuthorUserId
LEFT JOIN dbo.StaffProfiles sp ON sp.UserId = bp.AuthorUserId
LEFT JOIN dbo.DentistProfiles dp ON dp.UserId = bp.AuthorUserId
WHERE bp.Status = ''Published''');
GO

CREATE PROCEDURE dbo.sp_IncrementBlogPostView
    @Slug NVARCHAR(250)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BlogPosts
    SET    ViewCount = ViewCount + 1
    WHERE  Slug = @Slug
      AND  Status = 'Published';
END;
GO
