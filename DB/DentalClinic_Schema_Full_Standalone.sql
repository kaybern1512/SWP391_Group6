-- =====================================================================================
-- DENTALCARE CLINIC MANAGEMENT SYSTEM
-- SWP391 - GROUP 6
-- FULL STANDALONE DATABASE SCHEMA (PROFESSIONAL SSMS EDITION)
-- Target RDBMS: Microsoft SQL Server 2017 / 2019 / 2022 / LocalDB / SQLEXPRESS
-- Target Database Default: DentalCare_SWP391
-- Architecture: Strictly 4 Official Roles (Patient, Receptionist, Dentist, SystemAdministrator)
-- Total Database Objects: 72 Tables (71 Business Tables + 1 EF History), 72 PKs, 170 FKs, 
--                        87 Checks, 27 Unique Constraints, 1 View, 2 Stored Procedures
-- Includes: Unified Clinical Workflow (17 columns) & Admin/Dentist Service Workflows (3 tables)
-- =====================================================================================

USE [master];
GO

-- -------------------------------------------------------------------------------------
-- DATABASE CREATION & FAIL-FAST TARGET SAFETY GUARD
-- -------------------------------------------------------------------------------------
IF DB_NAME() IN (N'master', N'tempdb', N'model', N'msdb')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'DentalCare_SWP391')
    BEGIN
        PRINT N'>>> Creating target database [DentalCare_SWP391] with standard collation...';
        CREATE DATABASE [DentalCare_SWP391] COLLATE SQL_Latin1_General_CP1_CI_AS;
    END
    ELSE
    BEGIN
        PRINT N'>>> Target database [DentalCare_SWP391] already exists. Verifying it is clean...';
    END
END
GO

USE [DentalCare_SWP391];
GO

-- Safety Guard Layer 2, 3, 4: Reject system databases, protected databases, or non-empty databases
IF DB_NAME() IN (N'master', N'tempdb', N'model', N'msdb')
   OR LOWER(DB_NAME()) IN (
       N'dentalclinicmanagementdb',
       N'dentalcare_integration_dev',
       N'dentalcare_release_clean_qa',
       N'dentalcare_restore_verification_qa'
   )
   OR EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
BEGIN
    PRINT N'========================================================================================';
    PRINT N'FATAL SAFETY ERROR: Target database [' + DB_NAME() + N'] is invalid, protected, or contains existing user tables!';
    PRINT N'Standalone schema requires an empty target database. Script execution halted via SET NOEXEC ON.';
    PRINT N'========================================================================================';
    RAISERROR(N'FATAL SAFETY ERROR: Target database is invalid, protected, or contains existing user tables! Execution safely halted.', 16, 1);
    SET NOEXEC ON;
END
GO

-- -------------------------------------------------------------------------------------
-- SESSION ENVIRONMENT SETTINGS (Single Standard Declaration)
-- -------------------------------------------------------------------------------------
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- =====================================================================================
-- 01. SYSTEM, IDENTITY & AUDIT
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table: dbo.__EFMigrationsHistory
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[__EFMigrationsHistory] (
    [MigrationId] [nvarchar](150) NOT NULL,
    [ProductVersion] [nvarchar](32) NOT NULL,

    CONSTRAINT [PK___EFMigrationsHistory]
        PRIMARY KEY CLUSTERED ([MigrationId] ASC)
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ClinicProfile
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ClinicProfile] (
    [ClinicId] [int] NOT NULL,
    [Name] [nvarchar](200) NOT NULL,
    [Address] [nvarchar](300) NOT NULL,
    [PhoneNumber] [varchar](20) NULL,
    [Email] [varchar](150) NULL,
    [Description] [nvarchar](1000) NULL,
    [OpeningTime] [time](7) NULL,
    [ClosingTime] [time](7) NULL,
    [UpdatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),

    CONSTRAINT [PK_ClinicProfile]
        PRIMARY KEY CLUSTERED ([ClinicId] ASC),

    CHECK (([ClinicId]=(1))),

    CONSTRAINT [CK_ClinicProfile_OpenClose]
        CHECK (([ClosingTime] IS NULL OR [OpeningTime] IS NULL OR [ClosingTime]>[OpeningTime]))
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.UserAccounts
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[UserAccounts] (
    [UserId] [bigint] IDENTITY(1,1) NOT NULL,
    [Email] [varchar](150) NULL,
    [PhoneNumber] [varchar](20) NULL,
    [PasswordHash] [varchar](255) NULL,
    [Role] [varchar](40) NOT NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Unverified'),
    [EmailVerifiedAt] [datetime2](7) NULL,
    [AvatarUrl] [nvarchar](500) NULL,
    [LastLoginAt] [datetime2](7) NULL,
    [FailedLoginCount] [int] NOT NULL
        DEFAULT (0),
    [LockoutEnd] [datetime2](7) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_UserAccounts]
        PRIMARY KEY CLUSTERED ([UserId] ASC),

    CHECK (([FailedLoginCount]>=(0))),

    CHECK (([Status]='Inactive' OR [Status]='Locked' OR [Status]='Active' OR [Status]='Unverified')),

    CONSTRAINT [CK_UserAccounts_Role]
        CHECK (([Role]='Patient' OR [Role]='Receptionist' OR [Role]='Dentist' OR [Role]='SystemAdministrator'))
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.AuditLogs
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[AuditLogs] (
    [AuditLogId] [bigint] IDENTITY(1,1) NOT NULL,
    [UserId] [bigint] NULL,
    [Action] [varchar](100) NOT NULL,
    [EntityName] [varchar](100) NOT NULL,
    [EntityId] [varchar](100) NULL,
    [OldValues] [nvarchar](max) NULL,
    [NewValues] [nvarchar](max) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),

    CONSTRAINT [PK_AuditLogs]
        PRIMARY KEY CLUSTERED ([AuditLogId] ASC),

    CONSTRAINT [FK_AuditLogs_User]
        FOREIGN KEY ([UserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO


-- =====================================================================================
-- 02. CLINICAL DIRECTORY, PROFILES & DEPARTMENTS
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table: dbo.PatientProfiles
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[PatientProfiles] (
    [UserId] [bigint] NOT NULL,
    [PatientCode] [varchar](30) NOT NULL,
    [FullName] [nvarchar](150) NOT NULL,
    [DateOfBirth] [date] NULL,
    [Gender] [varchar](20) NULL,
    [NationalId] [varchar](30) NULL,
    [HealthInsuranceNumber] [varchar](50) NULL,
    [Address] [nvarchar](300) NULL,
    [EmergencyContact] [nvarchar](150) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_PatientProfiles]
        PRIMARY KEY CLUSTERED ([UserId] ASC),

    UNIQUE NONCLUSTERED ([PatientCode] ASC),

    CHECK (([Gender] IS NULL OR ([Gender]='Other' OR [Gender]='Female' OR [Gender]='Male'))),

    CONSTRAINT [FK_PatientProfiles_UserAccounts]
        FOREIGN KEY ([UserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.StaffProfiles
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[StaffProfiles] (
    [UserId] [bigint] NOT NULL,
    [EmployeeCode] [varchar](30) NOT NULL,
    [FullName] [nvarchar](150) NOT NULL,
    [EmploymentStatus] [varchar](30) NOT NULL
        DEFAULT ('Active'),
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_StaffProfiles]
        PRIMARY KEY CLUSTERED ([UserId] ASC),

    UNIQUE NONCLUSTERED ([EmployeeCode] ASC),

    CHECK (([EmploymentStatus]='Inactive' OR [EmploymentStatus]='Active')),

    CONSTRAINT [FK_StaffProfiles_UserAccounts]
        FOREIGN KEY ([UserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.DentistProfiles
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DentistProfiles] (
    [UserId] [bigint] NOT NULL,
    [LicenseNumber] [varchar](80) NULL,
    [Qualification] [nvarchar](300) NULL,
    [YearsOfExperience] [int] NULL,
    [Biography] [nvarchar](1000) NULL,
    [HasRadiationSafetyCert] [bit] NOT NULL
        CONSTRAINT [DF_DentistProfiles_HasRadCert] DEFAULT (0),
    [RadiationCertNumber] [nvarchar](100) NULL,
    [RadiationCertExpiryDate] [date] NULL,

    CONSTRAINT [PK_DentistProfiles]
        PRIMARY KEY CLUSTERED ([UserId] ASC),

    CHECK (([YearsOfExperience] IS NULL OR [YearsOfExperience]>=(0))),

    CONSTRAINT [FK_DentistProfiles_StaffProfiles]
        FOREIGN KEY ([UserId])
        REFERENCES [dbo].[StaffProfiles] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.Departments
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Departments] (
    [DepartmentId] [bigint] IDENTITY(1,1) NOT NULL,
    [Name] [nvarchar](150) NOT NULL,
    [Description] [nvarchar](1000) NULL,
    [ManagerUserId] [bigint] NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Active'),
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_Departments]
        PRIMARY KEY CLUSTERED ([DepartmentId] ASC),

    UNIQUE NONCLUSTERED ([Name] ASC),

    CHECK (([Status]='Inactive' OR [Status]='Active')),

    CONSTRAINT [FK_Departments_ManagerUser]
        FOREIGN KEY ([ManagerUserId])
        REFERENCES [dbo].[StaffProfiles] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.DentistDepartments
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DentistDepartments] (
    [DentistDepartmentId] [bigint] IDENTITY(1,1) NOT NULL,
    [DentistUserId] [bigint] NOT NULL,
    [DepartmentId] [bigint] NOT NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Active'),
    [AssignedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),

    CONSTRAINT [PK_DentistDepartments]
        PRIMARY KEY CLUSTERED ([DentistDepartmentId] ASC),

    CONSTRAINT [UQ_DentistDepartments]
        UNIQUE NONCLUSTERED ([DentistUserId] ASC, [DepartmentId] ASC),

    CHECK (([Status]='Inactive' OR [Status]='Active')),

    CONSTRAINT [FK_DentistDepartments_Dentist]
        FOREIGN KEY ([DentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_DentistDepartments_Department]
        FOREIGN KEY ([DepartmentId])
        REFERENCES [dbo].[Departments] ([DepartmentId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.Rooms
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Rooms] (
    [RoomId] [bigint] IDENTITY(1,1) NOT NULL,
    [RoomCode] [varchar](30) NOT NULL,
    [RoomName] [nvarchar](100) NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Active'),
    [DepartmentId] [bigint] NULL,
    [IsExclusive] [bit] NOT NULL
        CONSTRAINT [DF_Rooms_IsExclusive] DEFAULT (0),
    [RoomType] [nvarchar](50) NOT NULL
        CONSTRAINT [DF_Rooms_RoomType] DEFAULT ('Treatment'),
    [MaxConcurrentPatients] [int] NOT NULL
        CONSTRAINT [DF_Rooms_MaxConcurrentPatients] DEFAULT (1),
    [MaxWaitingPatients] [int] NOT NULL
        CONSTRAINT [DF_Rooms_MaxWaitingPatients] DEFAULT (3),
    [TargetMaxWaitMinutes] [int] NOT NULL
        CONSTRAINT [DF_Rooms_TargetMaxWaitMinutes] DEFAULT (20),
    [EscalationWaitMinutes] [int] NOT NULL
        CONSTRAINT [DF_Rooms_EscalationWaitMinutes] DEFAULT (30),

    CONSTRAINT [PK_Rooms]
        PRIMARY KEY CLUSTERED ([RoomId] ASC),

    UNIQUE NONCLUSTERED ([RoomCode] ASC),

    CHECK (([Status]='Maintenance' OR [Status]='Inactive' OR [Status]='Active')),

    CONSTRAINT [FK_Rooms_Department]
        FOREIGN KEY ([DepartmentId])
        REFERENCES [dbo].[Departments] ([DepartmentId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.DentalChairs
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DentalChairs] (
    [DentalChairId] [bigint] IDENTITY(1,1) NOT NULL,
    [RoomId] [bigint] NOT NULL,
    [ChairCode] [varchar](30) NOT NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Active'),

    CONSTRAINT [PK_DentalChairs]
        PRIMARY KEY CLUSTERED ([DentalChairId] ASC),

    CONSTRAINT [UQ_DentalChairs]
        UNIQUE NONCLUSTERED ([RoomId] ASC, [ChairCode] ASC),

    CONSTRAINT [UQ_DentalChairs_Room_Chair]
        UNIQUE NONCLUSTERED ([RoomId] ASC, [DentalChairId] ASC),

    CHECK (([Status]='Maintenance' OR [Status]='Inactive' OR [Status]='Active')),

    CONSTRAINT [FK_DentalChairs_Room]
        FOREIGN KEY ([RoomId])
        REFERENCES [dbo].[Rooms] ([RoomId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.DepartmentServices
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DepartmentServices] (
    [DepartmentServiceId] [bigint] IDENTITY(1,1) NOT NULL,
    [DepartmentId] [bigint] NOT NULL,
    [ServiceName] [nvarchar](200) NOT NULL,
    [ServiceCode] [nvarchar](50) NULL,
    [Description] [nvarchar](1000) NULL,
    [DurationMinutes] [int] NULL,
    [Price] [decimal](18, 2) NOT NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_DepartmentServices_Status] DEFAULT ('Active'),
    [ServiceType] [varchar](30) NOT NULL
        CONSTRAINT [DF_DepartmentServices_ServiceType] DEFAULT ('Treatment'),
    [RequiresExclusiveRoom] [bit] NOT NULL
        CONSTRAINT [DF_DepartmentServices_RequiresExclusiveRoom] DEFAULT ((0)),
    [RequiresConsultationFirst] [bit] NOT NULL
        CONSTRAINT [DF_DepartmentServices_RequiresConsultationFirst] DEFAULT ((0)),
    [MinLeadTimeHours] [int] NOT NULL
        CONSTRAINT [DF_DepartmentServices_MinLeadTimeHours] DEFAULT ((2)),
    [BookingInstructions] [nvarchar](1000) NULL,
    [RequiresTreatmentPlan] [bit] NOT NULL
        CONSTRAINT [DF_DepartmentServices_RequiresTreatmentPlan] DEFAULT ((0)),
    [RequiresFollowUp] [bit] NOT NULL
        CONSTRAINT [DF_DepartmentServices_RequiresFollowUp] DEFAULT ((0)),

    CONSTRAINT [PK_DepartmentServices]
        PRIMARY KEY CLUSTERED ([DepartmentServiceId] ASC),

    CONSTRAINT [UQ_DepartmentServices]
        UNIQUE NONCLUSTERED ([DepartmentId] ASC, [ServiceName] ASC),

    CONSTRAINT [UQ_DepartmentServices_Department_Service]
        UNIQUE NONCLUSTERED ([DepartmentId] ASC, [DepartmentServiceId] ASC),

    CHECK (([Status]='Inactive' OR [Status]='Active')),

    CONSTRAINT [CK_DepartmentServices_Duration]
        CHECK (([DurationMinutes] IS NULL OR [DurationMinutes]>(0))),

    CONSTRAINT [CK_DepartmentServices_Price]
        CHECK ([Price]>=(0)),

    CONSTRAINT [FK_DepartmentServices_Department]
        FOREIGN KEY ([DepartmentId])
        REFERENCES [dbo].[Departments] ([DepartmentId])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_DepartmentServices_ServiceCode]
    ON [dbo].[DepartmentServices]([ServiceCode])
    WHERE [ServiceCode] IS NOT NULL;
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.DepartmentWorkSchedules
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DepartmentWorkSchedules] (
    [DepartmentScheduleId] [bigint] IDENTITY(1,1) NOT NULL,
    [DepartmentId] [bigint] NOT NULL,
    [DayOfWeek] [tinyint] NOT NULL,
    [StartTime] [time](7) NOT NULL,
    [EndTime] [time](7) NOT NULL,
    [IsActive] [bit] NOT NULL
        DEFAULT (1),

    CONSTRAINT [PK_DepartmentWorkSchedules]
        PRIMARY KEY CLUSTERED ([DepartmentScheduleId] ASC),

    CONSTRAINT [UQ_DepartmentWorkSchedules]
        UNIQUE NONCLUSTERED ([DepartmentId] ASC, [DayOfWeek] ASC, [StartTime] ASC, [EndTime] ASC),

    CHECK (([DayOfWeek]>=(1) AND [DayOfWeek]<=(7))),

    CONSTRAINT [CK_DepartmentWorkSchedules_Time]
        CHECK (([EndTime]>[StartTime])),

    CONSTRAINT [FK_DepartmentWorkSchedules_Department]
        FOREIGN KEY ([DepartmentId])
        REFERENCES [dbo].[Departments] ([DepartmentId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ImagingEquipments
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ImagingEquipments] (
    [EquipmentId] [bigint] IDENTITY(1,1) NOT NULL,
    [RoomId] [bigint] NOT NULL,
    [EquipmentCode] [varchar](50) NOT NULL,
    [EquipmentName] [nvarchar](150) NOT NULL,
    [EquipmentType] [varchar](50) NOT NULL,
    [SupportedDiagnosticTypes] [nvarchar](250) NOT NULL,
    [IsActive] [bit] NOT NULL
        CONSTRAINT [DF_ImagingEquipments_IsActive] DEFAULT (1),
    [OperationalStatus] [varchar](30) NOT NULL
        CONSTRAINT [DF_ImagingEquipments_OperationalStatus] DEFAULT ('Operational'),
    [AvailableFrom] [time](7) NULL
        CONSTRAINT [DF_ImagingEquipments_AvailableFrom] DEFAULT ('07:00:00'),
    [AvailableTo] [time](7) NULL
        CONSTRAINT [DF_ImagingEquipments_AvailableTo] DEFAULT ('21:00:00'),
    [CreatedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_ImagingEquipments_CreatedAtUtc] DEFAULT (sysutcdatetime()),
    [UpdatedAtUtc] [datetime2](7) NULL,

    CONSTRAINT [PK_ImagingEquipments]
        PRIMARY KEY CLUSTERED ([EquipmentId] ASC),

    CONSTRAINT [UQ_ImagingEquipments_Code]
        UNIQUE NONCLUSTERED ([EquipmentCode] ASC),

    CONSTRAINT [UQ_ImagingEquipments_Room_Equipment]
        UNIQUE NONCLUSTERED ([RoomId] ASC, [EquipmentId] ASC),

    CONSTRAINT [CK_ImagingEquipments_Status]
        CHECK (([OperationalStatus]='OutOfService' OR [OperationalStatus]='Maintenance' OR [OperationalStatus]='Operational')),

    CONSTRAINT [FK_ImagingEquipments_Room]
        FOREIGN KEY ([RoomId])
        REFERENCES [dbo].[Rooms] ([RoomId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ImagingServiceConfigs
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ImagingServiceConfigs] (
    [ConfigId] [bigint] IDENTITY(1,1) NOT NULL,
    [DiagnosticType] [varchar](50) NOT NULL,
    [ServiceName] [nvarchar](150) NOT NULL,
    [SlotDurationMinutes] [int] NOT NULL,
    [BufferMinutes] [int] NOT NULL
        CONSTRAINT [DF_ImagingServiceConfigs_Buffer] DEFAULT (0),
    [Price] [decimal](18, 2) NOT NULL
        CONSTRAINT [DF_ImagingServiceConfigs_Price] DEFAULT (0),
    [IsActive] [bit] NOT NULL
        CONSTRAINT [DF_ImagingServiceConfigs_IsActive] DEFAULT (1),
    [DepartmentServiceId] [bigint] NULL,

    CONSTRAINT [PK_ImagingServiceConfigs]
        PRIMARY KEY CLUSTERED ([ConfigId] ASC),

    CONSTRAINT [UQ_ImagingServiceConfigs_Type]
        UNIQUE NONCLUSTERED ([DiagnosticType] ASC),

    CONSTRAINT [FK_ImagingServiceConfigs_DepartmentService]
        FOREIGN KEY ([DepartmentServiceId])
        REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ServiceCombos
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ServiceCombos] (
    [ComboId] [bigint] IDENTITY(1,1) NOT NULL,
    [ComboCode] [varchar](50) NOT NULL,
    [Title] [nvarchar](200) NOT NULL,
    [Tagline] [nvarchar](300) NULL,
    [Badge] [nvarchar](50) NULL,
    [IconClass] [varchar](50) NULL,
    [Description] [nvarchar](1000) NULL,
    [DepartmentId] [bigint] NOT NULL,
    [PrimaryServiceId] [bigint] NOT NULL,
    [OriginalPrice] [decimal](18, 2) NOT NULL,
    [ComboPrice] [decimal](18, 2) NOT NULL,
    [DurationMinutes] [int] NOT NULL
        DEFAULT (45),
    [IncludedServices] [nvarchar](max) NULL,
    [Benefits] [nvarchar](max) NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Active'),
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysutcdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_ServiceCombos]
        PRIMARY KEY CLUSTERED ([ComboId] ASC),

    CONSTRAINT [UQ_ServiceCombos_ComboCode]
        UNIQUE NONCLUSTERED ([ComboCode] ASC),

    CONSTRAINT [FK_ServiceCombos_Departments]
        FOREIGN KEY ([DepartmentId])
        REFERENCES [dbo].[Departments] ([DepartmentId]),

    CONSTRAINT [FK_ServiceCombos_DepartmentServices]
        FOREIGN KEY ([PrimaryServiceId])
        REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ServiceWorkflows (Admin Service Workflow Configuration)
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ServiceWorkflows] (
    [WorkflowId] [bigint] IDENTITY(1,1) NOT NULL,
    [DepartmentServiceId] [bigint] NOT NULL,
    [WorkflowName] [nvarchar](200) NOT NULL,
    [Version] [int] NOT NULL
        CONSTRAINT [DF_ServiceWorkflows_Version] DEFAULT ((1)),
    [IsActive] [bit] NOT NULL
        CONSTRAINT [DF_ServiceWorkflows_IsActive] DEFAULT ((0)),
    [ApprovalStatus] [nvarchar](50) NOT NULL
        CONSTRAINT [DF_ServiceWorkflows_ApprovalStatus] DEFAULT ('Draft'),
    [Description] [nvarchar](1000) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_ServiceWorkflows_CreatedAt] DEFAULT (getutcdate()),
    [UpdatedAt] [datetime2](7) NULL,
    [CreatedByUserId] [bigint] NULL,
    [ApprovedByUserId] [bigint] NULL,
    [ApprovedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_ServiceWorkflows]
        PRIMARY KEY CLUSTERED ([WorkflowId] ASC),

    CONSTRAINT [FK_ServiceWorkflows_DepartmentServices]
        FOREIGN KEY ([DepartmentServiceId])
        REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_ServiceWorkflows_DepartmentServiceId_IsActive]
    ON [dbo].[ServiceWorkflows]([DepartmentServiceId], [IsActive]);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ServiceWorkflowSteps (Procedural Steps for Clinical Tasks)
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ServiceWorkflowSteps] (
    [StepId] [bigint] IDENTITY(1,1) NOT NULL,
    [WorkflowId] [bigint] NOT NULL,
    [StepName] [nvarchar](200) NOT NULL,
    [Description] [nvarchar](1000) NULL,
    [StepOrder] [int] NOT NULL,
    [IsRequired] [bit] NOT NULL
        CONSTRAINT [DF_ServiceWorkflowSteps_IsRequired] DEFAULT ((1)),
    [CompletionRequirement] [nvarchar](1000) NULL,
    [ExpectedDataType] [nvarchar](50) NOT NULL
        CONSTRAINT [DF_ServiceWorkflowSteps_DataType] DEFAULT ('None'),
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_ServiceWorkflowSteps_CreatedAt] DEFAULT (getutcdate()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_ServiceWorkflowSteps]
        PRIMARY KEY CLUSTERED ([StepId] ASC),

    CONSTRAINT [FK_ServiceWorkflowSteps_ServiceWorkflows]
        FOREIGN KEY ([WorkflowId])
        REFERENCES [dbo].[ServiceWorkflows] ([WorkflowId]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_ServiceWorkflowSteps_WorkflowId_StepOrder]
    ON [dbo].[ServiceWorkflowSteps]([WorkflowId], [StepOrder]);
GO


-- =====================================================================================
-- 03. WORKFORCE, ATTENDANCE & ROSTER SCHEDULING
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table: dbo.AttendancePolicies
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[AttendancePolicies] (
    [PolicyId] [bigint] IDENTITY(1,1) NOT NULL,
    [LateGracePeriodMinutes] [int] NOT NULL
        CONSTRAINT [DF_AttendancePolicies_LateGrace] DEFAULT (10),
    [EarlyLeaveGracePeriodMinutes] [int] NOT NULL
        CONSTRAINT [DF_AttendancePolicies_EarlyGrace] DEFAULT (10),
    [StandardWorkDayHours] [decimal](4, 2) NOT NULL
        CONSTRAINT [DF_AttendancePolicies_StandardDayHours] DEFAULT (8.00),
    [AutoMissingPunchAfterHours] [int] NOT NULL
        CONSTRAINT [DF_AttendancePolicies_AutoMissing] DEFAULT (4),
    [AllowSelfCorrectionRequest] [bit] NOT NULL
        CONSTRAINT [DF_AttendancePolicies_AllowSelfCorrection] DEFAULT (1),
    [CreatedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_AttendancePolicies_CreatedAtUtc] DEFAULT (sysutcdatetime()),
    [UpdatedAtUtc] [datetime2](7) NULL,

    CONSTRAINT [PK_AttendancePolicies]
        PRIMARY KEY CLUSTERED ([PolicyId] ASC)
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.AttendancePeriodClosures
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[AttendancePeriodClosures] (
    [ClosureId] [bigint] IDENTITY(1,1) NOT NULL,
    [Year] [int] NOT NULL,
    [Month] [int] NOT NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_AttendancePeriodClosures_Status] DEFAULT ('Open'),
    [TotalStaffCount] [int] NOT NULL
        CONSTRAINT [DF_AttendancePeriodClosures_StaffCount] DEFAULT (0),
    [TotalWorkedHours] [decimal](10, 2) NOT NULL
        CONSTRAINT [DF_AttendancePeriodClosures_WorkedHours] DEFAULT (0),
    [TotalScheduledShifts] [int] NOT NULL
        CONSTRAINT [DF_AttendancePeriodClosures_ScheduledShifts] DEFAULT (0),
    [TotalCompletedShifts] [int] NOT NULL
        CONSTRAINT [DF_AttendancePeriodClosures_CompletedShifts] DEFAULT (0),
    [TotalMissingPunches] [int] NOT NULL
        CONSTRAINT [DF_AttendancePeriodClosures_MissingPunches] DEFAULT (0),
    [FinalizedByUserId] [bigint] NULL,
    [FinalizedAtUtc] [datetime2](7) NULL,
    [ReopenedByUserId] [bigint] NULL,
    [ReopenedAtUtc] [datetime2](7) NULL,
    [ReopenReason] [nvarchar](1000) NULL,
    [SnapshotDataJson] [nvarchar](max) NULL,
    [CreatedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_AttendancePeriodClosures_CreatedAtUtc] DEFAULT (sysutcdatetime()),

    CONSTRAINT [PK_AttendancePeriodClosures]
        PRIMARY KEY CLUSTERED ([ClosureId] ASC),

    CONSTRAINT [UQ_AttendancePeriodClosures_Year_Month]
        UNIQUE NONCLUSTERED ([Year] ASC, [Month] ASC),

    CONSTRAINT [CK_AttendancePeriodClosures_Month]
        CHECK (([Month]>=(1) AND [Month]<=(12))),

    CONSTRAINT [CK_AttendancePeriodClosures_Status]
        CHECK (([Status]='Reopened' OR [Status]='Locked' OR [Status]='Finalized' OR [Status]='Open')),

    CONSTRAINT [FK_AttendancePeriodClosures_Finalizer]
        FOREIGN KEY ([FinalizedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_AttendancePeriodClosures_Reopener]
        FOREIGN KEY ([ReopenedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.StaffAttendanceRecords
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[StaffAttendanceRecords] (
    [AttendanceId] [bigint] IDENTITY(1,1) NOT NULL,
    [UserId] [bigint] NOT NULL,
    [WorkDate] [date] NOT NULL,
    [ShiftIdentifier] [nvarchar](100) NOT NULL,
    [Role] [varchar](40) NOT NULL,
    [ScheduledStartTime] [time](7) NOT NULL,
    [ScheduledEndTime] [time](7) NOT NULL,
    [ClockInTimeUtc] [datetime2](7) NULL,
    [ClockOutTimeUtc] [datetime2](7) NULL,
    [ActualWorkedMinutes] [int] NOT NULL
        CONSTRAINT [DF_StaffAttendanceRecords_WorkedMins] DEFAULT (0),
    [LateMinutes] [int] NOT NULL
        CONSTRAINT [DF_StaffAttendanceRecords_LateMins] DEFAULT (0),
    [EarlyDepartureMinutes] [int] NOT NULL
        CONSTRAINT [DF_StaffAttendanceRecords_EarlyMins] DEFAULT (0),
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_StaffAttendanceRecords_Status] DEFAULT ('NotStarted'),
    [IsApprovedLeave] [bit] NOT NULL
        CONSTRAINT [DF_StaffAttendanceRecords_IsLeave] DEFAULT (0),
    [LeaveHours] [decimal](5, 2) NOT NULL
        CONSTRAINT [DF_StaffAttendanceRecords_LeaveHours] DEFAULT (0),
    [Notes] [nvarchar](1000) NULL,
    [IsFinalized] [bit] NOT NULL
        CONSTRAINT [DF_StaffAttendanceRecords_IsFinalized] DEFAULT (0),
    [FinalizedAtUtc] [datetime2](7) NULL,
    [FinalizedByUserId] [bigint] NULL,
    [CreatedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_StaffAttendanceRecords_CreatedAtUtc] DEFAULT (sysutcdatetime()),
    [UpdatedAtUtc] [datetime2](7) NULL,
    [RowVersion] [timestamp] NOT NULL,

    CONSTRAINT [PK_StaffAttendanceRecords]
        PRIMARY KEY CLUSTERED ([AttendanceId] ASC),

    CONSTRAINT [CK_StaffAttendanceRecords_Status]
        CHECK (([Status]='UnexpectedAbsence' OR [Status]='ApprovedLeave' OR [Status]='LateAndEarlyDeparture' OR [Status]='EarlyDeparture' OR [Status]='Late' OR [Status]='OnTime' OR [Status]='Corrected' OR [Status]='Verified' OR [Status]='NeedsReview' OR [Status]='MissingClockOut' OR [Status]='ClockedOut' OR [Status]='ClockedIn' OR [Status]='NotStarted')),

    CONSTRAINT [FK_StaffAttendanceRecords_FinalizedByUser]
        FOREIGN KEY ([FinalizedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_StaffAttendanceRecords_UserAccounts]
        FOREIGN KEY ([UserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.StaffAttendanceEvents
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[StaffAttendanceEvents] (
    [EventId] [bigint] IDENTITY(1,1) NOT NULL,
    [AttendanceId] [bigint] NOT NULL,
    [UserId] [bigint] NOT NULL,
    [EventType] [varchar](30) NOT NULL,
    [EventTimeUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_StaffAttendanceEvents_EventTimeUtc] DEFAULT (sysutcdatetime()),
    [Source] [varchar](50) NOT NULL
        CONSTRAINT [DF_StaffAttendanceEvents_Source] DEFAULT ('WebPortal'),
    [IpAddress] [varchar](50) NULL,
    [Note] [nvarchar](1000) NULL,

    CONSTRAINT [PK_StaffAttendanceEvents]
        PRIMARY KEY CLUSTERED ([EventId] ASC),

    CONSTRAINT [CK_StaffAttendanceEvents_Type]
        CHECK (([EventType]='PeriodReopened' OR [EventType]='PeriodFinalized' OR [EventType]='CorrectionApplied' OR [EventType]='ClockOut' OR [EventType]='ClockIn')),

    CONSTRAINT [FK_StaffAttendanceEvents_Attendance]
        FOREIGN KEY ([AttendanceId])
        REFERENCES [dbo].[StaffAttendanceRecords] ([AttendanceId]) ON DELETE CASCADE,

    CONSTRAINT [FK_StaffAttendanceEvents_User]
        FOREIGN KEY ([UserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.StaffAttendanceCorrectionRequests
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[StaffAttendanceCorrectionRequests] (
    [CorrectionRequestId] [bigint] IDENTITY(1,1) NOT NULL,
    [AttendanceId] [bigint] NOT NULL,
    [UserId] [bigint] NOT NULL,
    [WorkDate] [date] NOT NULL,
    [RequestedClockInUtc] [datetime2](7) NULL,
    [RequestedClockOutUtc] [datetime2](7) NULL,
    [Reason] [nvarchar](200) NOT NULL,
    [Explanation] [nvarchar](1000) NOT NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_StaffAttendanceCorrection_Status] DEFAULT ('Pending'),
    [AdminNote] [nvarchar](1000) NULL,
    [ReviewedByUserId] [bigint] NULL,
    [ReviewedAtUtc] [datetime2](7) NULL,
    [CreatedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_StaffAttendanceCorrection_CreatedAtUtc] DEFAULT (sysutcdatetime()),

    CONSTRAINT [PK_StaffAttendanceCorrectionRequests]
        PRIMARY KEY CLUSTERED ([CorrectionRequestId] ASC),

    CONSTRAINT [CK_StaffAttendanceCorrection_Status]
        CHECK (([Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending')),

    CONSTRAINT [FK_StaffAttendanceCorrection_Attendance]
        FOREIGN KEY ([AttendanceId])
        REFERENCES [dbo].[StaffAttendanceRecords] ([AttendanceId]),

    CONSTRAINT [FK_StaffAttendanceCorrection_Reviewer]
        FOREIGN KEY ([ReviewedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_StaffAttendanceCorrection_User]
        FOREIGN KEY ([UserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ReceptionistShiftTemplates
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ReceptionistShiftTemplates] (
    [ShiftTemplateId] [bigint] IDENTITY(1,1) NOT NULL,
    [TemplateName] [nvarchar](100) NOT NULL,
    [StartTime] [time](0) NOT NULL,
    [EndTime] [time](0) NOT NULL,
    [MinStaffRequired] [int] NOT NULL
        CONSTRAINT [DF_ReceptionistShiftTemplates_MinStaff] DEFAULT (2),
    [IsActive] [bit] NOT NULL
        CONSTRAINT [DF_ReceptionistShiftTemplates_IsActive] DEFAULT (1),
    [DisplayOrder] [int] NOT NULL
        CONSTRAINT [DF_ReceptionistShiftTemplates_DisplayOrder] DEFAULT (1),
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_ReceptionistShiftTemplates_CreatedAt] DEFAULT (sysutcdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_ReceptionistShiftTemplates]
        PRIMARY KEY CLUSTERED ([ShiftTemplateId] ASC),

    CONSTRAINT [CK_ReceptionistShiftTemplates_MinStaff]
        CHECK (([MinStaffRequired]>=(1))),

    CONSTRAINT [CK_ReceptionistShiftTemplates_Time]
        CHECK (([EndTime]>[StartTime]))
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ReceptionistWorkSchedules
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ReceptionistWorkSchedules] (
    [ReceptionistScheduleId] [bigint] IDENTITY(1,1) NOT NULL,
    [ReceptionistUserId] [bigint] NOT NULL,
    [ShiftTemplateId] [bigint] NULL,
    [WorkDate] [date] NOT NULL,
    [StartTime] [time](0) NOT NULL,
    [EndTime] [time](0) NOT NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_ReceptionistWorkSchedules_Status] DEFAULT ('Scheduled'),
    [IsPublished] [bit] NOT NULL
        CONSTRAINT [DF_ReceptionistWorkSchedules_IsPublished] DEFAULT (1),
    [Notes] [nvarchar](500) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_ReceptionistWorkSchedules_CreatedAt] DEFAULT (sysutcdatetime()),
    [UpdatedAt] [datetime2](7) NULL,
    [CreatedBy] [bigint] NOT NULL,

    CONSTRAINT [PK_ReceptionistWorkSchedules]
        PRIMARY KEY CLUSTERED ([ReceptionistScheduleId] ASC),

    CONSTRAINT [CK_ReceptionistWorkSchedules_Status]
        CHECK (([Status]='AbsenceReported' OR [Status]='Swapped' OR [Status]='Completed' OR [Status]='Cancelled' OR [Status]='Scheduled')),

    CONSTRAINT [CK_ReceptionistWorkSchedules_Time]
        CHECK (([EndTime]>[StartTime])),

    CONSTRAINT [FK_ReceptionistWorkSchedules_ShiftTemplates]
        FOREIGN KEY ([ShiftTemplateId])
        REFERENCES [dbo].[ReceptionistShiftTemplates] ([ShiftTemplateId]) ON DELETE SET NULL,

    CONSTRAINT [FK_ReceptionistWorkSchedules_UserAccounts_Creator]
        FOREIGN KEY ([CreatedBy])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_ReceptionistWorkSchedules_UserAccounts_Receptionist]
        FOREIGN KEY ([ReceptionistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ReceptionistLeaveRequests
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ReceptionistLeaveRequests] (
    [LeaveRequestId] [bigint] IDENTITY(1,1) NOT NULL,
    [ReceptionistUserId] [bigint] NOT NULL,
    [LeaveType] [varchar](30) NOT NULL,
    [StartDate] [date] NOT NULL,
    [EndDate] [date] NOT NULL,
    [StartTime] [time](0) NULL,
    [EndTime] [time](0) NULL,
    [Reason] [nvarchar](1000) NOT NULL,
    [AdditionalNotes] [nvarchar](1000) NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_ReceptionistLeaveRequests_Status] DEFAULT ('Pending'),
    [RejectionReason] [nvarchar](1000) NULL,
    [ApprovedBy] [bigint] NULL,
    [ApprovedAt] [datetime2](7) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_ReceptionistLeaveRequests_CreatedAt] DEFAULT (sysutcdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_ReceptionistLeaveRequests]
        PRIMARY KEY CLUSTERED ([LeaveRequestId] ASC),

    CONSTRAINT [CK_ReceptionistLeaveRequests_Date]
        CHECK (([EndDate]>=[StartDate])),

    CONSTRAINT [CK_ReceptionistLeaveRequests_LeaveType]
        CHECK (([LeaveType]='MultiDay' OR [LeaveType]='Hourly' OR [LeaveType]='HalfDayAfternoon' OR [LeaveType]='HalfDayMorning' OR [LeaveType]='FullDay')),

    CONSTRAINT [CK_ReceptionistLeaveRequests_Status]
        CHECK (([Status]='Cancelled' OR [Status]='Withdrawn' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending')),

    CONSTRAINT [FK_ReceptionistLeaveRequests_UserAccounts_ApprovedBy]
        FOREIGN KEY ([ApprovedBy])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_ReceptionistLeaveRequests_UserAccounts_Receptionist]
        FOREIGN KEY ([ReceptionistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ReceptionistLeaveRequestPeriods
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ReceptionistLeaveRequestPeriods] (
    [PeriodId] [bigint] IDENTITY(1,1) NOT NULL,
    [LeaveRequestId] [bigint] NOT NULL,
    [LeaveDate] [date] NOT NULL,
    [StartTime] [time](0) NULL,
    [EndTime] [time](0) NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_ReceptionistLeaveRequestPeriods_Status] DEFAULT ('Pending'),

    CONSTRAINT [PK_ReceptionistLeaveRequestPeriods]
        PRIMARY KEY CLUSTERED ([PeriodId] ASC),

    CONSTRAINT [CK_ReceptionistLeaveRequestPeriods_Status]
        CHECK (([Status]='Cancelled' OR [Status]='Withdrawn' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending')),

    CONSTRAINT [FK_ReceptionistLeaveRequestPeriods_LeaveRequest]
        FOREIGN KEY ([LeaveRequestId])
        REFERENCES [dbo].[ReceptionistLeaveRequests] ([LeaveRequestId]) ON DELETE CASCADE
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ReceptionistShiftSwapRequests
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ReceptionistShiftSwapRequests] (
    [SwapRequestId] [bigint] IDENTITY(1,1) NOT NULL,
    [RequestingReceptionistUserId] [bigint] NOT NULL,
    [TargetReceptionistUserId] [bigint] NOT NULL,
    [SourceScheduleId] [bigint] NOT NULL,
    [TargetScheduleId] [bigint] NULL,
    [Reason] [nvarchar](1000) NOT NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_ReceptionistShiftSwapRequests_Status] DEFAULT ('PendingCounterparty'),
    [CounterpartyResponseAt] [datetime2](7) NULL,
    [CounterpartyNote] [nvarchar](500) NULL,
    [AdminDecisionBy] [bigint] NULL,
    [AdminDecisionAt] [datetime2](7) NULL,
    [AdminDecisionReason] [nvarchar](500) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_ReceptionistShiftSwapRequests_CreatedAt] DEFAULT (sysutcdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_ReceptionistShiftSwapRequests]
        PRIMARY KEY CLUSTERED ([SwapRequestId] ASC),

    CONSTRAINT [CK_ReceptionistShiftSwapRequests_DiffUsers]
        CHECK (([RequestingReceptionistUserId]<>[TargetReceptionistUserId])),

    CONSTRAINT [CK_ReceptionistShiftSwapRequests_Status]
        CHECK (([Status]='Expired' OR [Status]='Withdrawn' OR [Status]='Declined' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='PendingAdminApproval' OR [Status]='PendingCounterparty')),

    CONSTRAINT [FK_ReceptionistShiftSwapRequests_AdminUser]
        FOREIGN KEY ([AdminDecisionBy])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_ReceptionistShiftSwapRequests_RequestingUser]
        FOREIGN KEY ([RequestingReceptionistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_ReceptionistShiftSwapRequests_SourceSchedule]
        FOREIGN KEY ([SourceScheduleId])
        REFERENCES [dbo].[ReceptionistWorkSchedules] ([ReceptionistScheduleId]),

    CONSTRAINT [FK_ReceptionistShiftSwapRequests_TargetSchedule]
        FOREIGN KEY ([TargetScheduleId])
        REFERENCES [dbo].[ReceptionistWorkSchedules] ([ReceptionistScheduleId]),

    CONSTRAINT [FK_ReceptionistShiftSwapRequests_TargetUser]
        FOREIGN KEY ([TargetReceptionistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ReceptionistWorkHandovers
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ReceptionistWorkHandovers] (
    [HandoverId] [bigint] IDENTITY(1,1) NOT NULL,
    [FromReceptionistUserId] [bigint] NOT NULL,
    [ToReceptionistUserId] [bigint] NULL,
    [WorkDate] [date] NOT NULL,
    [ShiftId] [bigint] NULL,
    [HandoverType] [varchar](50) NOT NULL,
    [PendingAppointmentCount] [int] NOT NULL
        CONSTRAINT [DF_ReceptionistWorkHandovers_PendingAppts] DEFAULT (0),
    [UnpaidInvoiceCount] [int] NOT NULL
        CONSTRAINT [DF_ReceptionistWorkHandovers_UnpaidInvoices] DEFAULT (0),
    [ActiveChatCount] [int] NOT NULL
        CONSTRAINT [DF_ReceptionistWorkHandovers_ActiveChats] DEFAULT (0),
    [Notes] [nvarchar](2000) NOT NULL,
    [HandoverStatus] [varchar](30) NOT NULL
        CONSTRAINT [DF_ReceptionistWorkHandovers_Status] DEFAULT ('Transferred'),
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_ReceptionistWorkHandovers_CreatedAt] DEFAULT (sysutcdatetime()),
    [AcknowledgedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_ReceptionistWorkHandovers]
        PRIMARY KEY CLUSTERED ([HandoverId] ASC),

    CONSTRAINT [CK_ReceptionistWorkHandovers_Status]
        CHECK (([HandoverStatus]='Completed' OR [HandoverStatus]='Acknowledged' OR [HandoverStatus]='Transferred')),

    CONSTRAINT [FK_ReceptionistWorkHandovers_FromUser]
        FOREIGN KEY ([FromReceptionistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_ReceptionistWorkHandovers_Shift]
        FOREIGN KEY ([ShiftId])
        REFERENCES [dbo].[ReceptionistWorkSchedules] ([ReceptionistScheduleId]) ON DELETE SET NULL,

    CONSTRAINT [FK_ReceptionistWorkHandovers_ToUser]
        FOREIGN KEY ([ToReceptionistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ReceptionistCoverageIncidents
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ReceptionistCoverageIncidents] (
    [IncidentId] [bigint] IDENTITY(1,1) NOT NULL,
    [WorkDate] [date] NOT NULL,
    [ShiftStartTime] [time](0) NOT NULL,
    [ShiftEndTime] [time](0) NOT NULL,
    [AffectedReceptionistUserId] [bigint] NULL,
    [IncidentType] [varchar](50) NOT NULL,
    [PlannedStaffCount] [int] NOT NULL,
    [AvailableStaffCount] [int] NOT NULL,
    [RequiredMinimumCount] [int] NOT NULL
        CONSTRAINT [DF_ReceptionistCoverageIncidents_MinStaff] DEFAULT (2),
    [ShortageCount] [int] NOT NULL,
    [Reason] [nvarchar](1000) NOT NULL,
    [ReportedBy] [bigint] NOT NULL,
    [ReportedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_ReceptionistCoverageIncidents_ReportedAt] DEFAULT (sysutcdatetime()),
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_ReceptionistCoverageIncidents_Status] DEFAULT ('Open'),
    [ResolutionNotes] [nvarchar](1000) NULL,
    [ReplacementReceptionistUserId] [bigint] NULL,
    [ResolvedBy] [bigint] NULL,
    [ResolvedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_ReceptionistCoverageIncidents]
        PRIMARY KEY CLUSTERED ([IncidentId] ASC),

    CONSTRAINT [CK_ReceptionistCoverageIncidents_Status]
        CHECK (([Status]='Acknowledged' OR [Status]='Resolved' OR [Status]='Investigating' OR [Status]='Open')),

    CONSTRAINT [FK_ReceptionistCoverageIncidents_AffectedUser]
        FOREIGN KEY ([AffectedReceptionistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_ReceptionistCoverageIncidents_ReplacementUser]
        FOREIGN KEY ([ReplacementReceptionistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_ReceptionistCoverageIncidents_ReportedBy]
        FOREIGN KEY ([ReportedBy])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_ReceptionistCoverageIncidents_ResolvedBy]
        FOREIGN KEY ([ResolvedBy])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.DentistWorkSchedules
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DentistWorkSchedules] (
    [DentistScheduleId] [bigint] IDENTITY(1,1) NOT NULL,
    [DentistUserId] [bigint] NOT NULL,
    [DepartmentId] [bigint] NOT NULL,
    [WorkDate] [date] NOT NULL,
    [StartTime] [time](7) NOT NULL,
    [EndTime] [time](7) NOT NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Scheduled'),
    [Note] [nvarchar](500) NULL,

    CONSTRAINT [PK_DentistWorkSchedules]
        PRIMARY KEY CLUSTERED ([DentistScheduleId] ASC),

    CONSTRAINT [UQ_DentistWorkSchedules]
        UNIQUE NONCLUSTERED ([DentistUserId] ASC, [DepartmentId] ASC, [WorkDate] ASC, [StartTime] ASC, [EndTime] ASC),

    CHECK (([Status]='Cancelled' OR [Status]='Scheduled')),

    CONSTRAINT [CK_DentistWorkSchedules_Time]
        CHECK (([EndTime]>[StartTime])),

    CONSTRAINT [FK_DentistWorkSchedules_Dentist]
        FOREIGN KEY ([DentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_DentistWorkSchedules_Department]
        FOREIGN KEY ([DepartmentId])
        REFERENCES [dbo].[Departments] ([DepartmentId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.DentistAvailability
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DentistAvailability] (
    [AvailabilityId] [bigint] IDENTITY(1,1) NOT NULL,
    [DentistUserId] [bigint] NOT NULL,
    [StartDateTime] [datetime2](7) NOT NULL,
    [EndDateTime] [datetime2](7) NULL,
    [AvailabilityStatus] [varchar](30) NOT NULL,
    [Reason] [nvarchar](500) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),

    CONSTRAINT [PK_DentistAvailability]
        PRIMARY KEY CLUSTERED ([AvailabilityId] ASC),

    CHECK (([AvailabilityStatus]='Unavailable' OR [AvailabilityStatus]='OnLeave' OR [AvailabilityStatus]='Busy' OR [AvailabilityStatus]='Available')),

    CONSTRAINT [CK_DentistAvailability_Time]
        CHECK (([EndDateTime] IS NULL OR [EndDateTime]>[StartDateTime])),

    CONSTRAINT [FK_DentistAvailability_Dentist]
        FOREIGN KEY ([DentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.DentistLeaveRequests
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DentistLeaveRequests] (
    [LeaveRequestId] [bigint] IDENTITY(1,1) NOT NULL,
    [DentistUserId] [bigint] NOT NULL,
    [LeaveType] [nvarchar](50) NOT NULL,
    [StartDate] [date] NOT NULL,
    [EndDate] [date] NOT NULL,
    [StartTime] [time](7) NULL,
    [EndTime] [time](7) NULL,
    [Reason] [nvarchar](500) NOT NULL,
    [Notes] [nvarchar](500) NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_DentistLeaveRequests_Status] DEFAULT ('Pending'),
    [ReviewedByUserId] [bigint] NULL,
    [ReviewedAt] [datetime2](7) NULL,
    [RejectionReason] [nvarchar](500) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_DentistLeaveRequests_CreatedAt] DEFAULT (sysutcdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_DentistLeaveRequests]
        PRIMARY KEY CLUSTERED ([LeaveRequestId] ASC),

    CONSTRAINT [CK_DentistLeaveRequests_Status]
        CHECK (([Status]='Withdrawn' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending')),

    CONSTRAINT [FK_DentistLeaveRequests_Dentist]
        FOREIGN KEY ([DentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_DentistLeaveRequests_ReviewedByUser]
        FOREIGN KEY ([ReviewedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.XrayDentistRosters
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[XrayDentistRosters] (
    [RosterId] [bigint] IDENTITY(1,1) NOT NULL,
    [RoomId] [bigint] NOT NULL,
    [EquipmentId] [bigint] NOT NULL,
    [DentistUserId] [bigint] NOT NULL,
    [BackupDentistUserId] [bigint] NULL,
    [WorkDate] [date] NOT NULL,
    [ShiftType] [nvarchar](20) NOT NULL
        CONSTRAINT [DF_XrayDentistRosters_ShiftType] DEFAULT (N'Morning'),
    [StartTime] [time](0) NOT NULL,
    [EndTime] [time](0) NOT NULL,
    [Status] [nvarchar](30) NOT NULL
        CONSTRAINT [DF_XrayDentistRosters_Status] DEFAULT (N'Active'),
    [Notes] [nvarchar](500) NULL,
    [CreatedByUserId] [bigint] NOT NULL,
    [CreatedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_XrayDentistRosters_CreatedAtUtc] DEFAULT (sysutcdatetime()),
    [UpdatedAtUtc] [datetime2](7) NULL,

    CONSTRAINT [PK_XrayDentistRosters]
        PRIMARY KEY CLUSTERED ([RosterId] ASC),

    CONSTRAINT [CK_XrayDentistRosters_ShiftType]
        CHECK (([ShiftType]=N'Custom' OR [ShiftType]=N'Evening' OR [ShiftType]=N'Afternoon' OR [ShiftType]=N'Morning')),

    CONSTRAINT [CK_XrayDentistRosters_Status]
        CHECK (([Status]=N'Replaced' OR [Status]=N'Cancelled' OR [Status]=N'OnLeave' OR [Status]=N'Active')),

    CONSTRAINT [CK_XrayDentistRosters_Time]
        CHECK (([EndTime]>[StartTime])),

    CONSTRAINT [FK_XrayDentistRosters_BackupDentist]
        FOREIGN KEY ([BackupDentistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_XrayDentistRosters_CreatedBy]
        FOREIGN KEY ([CreatedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_XrayDentistRosters_Dentist]
        FOREIGN KEY ([DentistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_XrayDentistRosters_Equipment]
        FOREIGN KEY ([EquipmentId])
        REFERENCES [dbo].[ImagingEquipments] ([EquipmentId]),

    CONSTRAINT [FK_XrayDentistRosters_Room]
        FOREIGN KEY ([RoomId])
        REFERENCES [dbo].[Rooms] ([RoomId])
);
GO


-- =====================================================================================
-- 04. CLINICAL FOUNDATIONS & TREATMENT PLANS
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table: dbo.DentalMedicalRecords
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DentalMedicalRecords] (
    [MedicalRecordId] [bigint] IDENTITY(1,1) NOT NULL,
    [PatientUserId] [bigint] NOT NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_DentalMedicalRecords]
        PRIMARY KEY CLUSTERED ([MedicalRecordId] ASC),

    UNIQUE NONCLUSTERED ([PatientUserId] ASC),

    CONSTRAINT [FK_DentalMedicalRecords_Patient]
        FOREIGN KEY ([PatientUserId])
        REFERENCES [dbo].[PatientProfiles] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.TreatmentPlans
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[TreatmentPlans] (
    [TreatmentPlanId] [bigint] IDENTITY(1,1) NOT NULL,
    [PatientUserId] [bigint] NOT NULL,
    [DepartmentId] [bigint] NOT NULL,
    [CreatedByDentistUserId] [bigint] NOT NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Planned'),
    [EstimatedTotal] [decimal](18, 2) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [UpdatedAt] [datetime2](7) NULL,
    [Notes] [nvarchar](max) NULL,
    [CancellationReason] [nvarchar](max) NULL,
    [DiagnosisSummary] [nvarchar](max) NULL,
    [ActivatedAt] [datetime2](7) NULL,
    [CompletedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_TreatmentPlans]
        PRIMARY KEY CLUSTERED ([TreatmentPlanId] ASC),

    CONSTRAINT [CK_TreatmentPlans_EstimatedTotal]
        CHECK (([EstimatedTotal] IS NULL OR [EstimatedTotal]>=(0))),

    CONSTRAINT [CK_TreatmentPlans_Status]
        CHECK ([Status] IN ('Draft', 'Active', 'Completed', 'Paused', 'Cancelled', 'Planned', 'InProgress', 'In_Progress')),

    CONSTRAINT [FK_TreatmentPlans_Dentist]
        FOREIGN KEY ([CreatedByDentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_TreatmentPlans_Department]
        FOREIGN KEY ([DepartmentId])
        REFERENCES [dbo].[Departments] ([DepartmentId]),

    CONSTRAINT [FK_TreatmentPlans_Patient]
        FOREIGN KEY ([PatientUserId])
        REFERENCES [dbo].[PatientProfiles] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.TreatmentPlanItems
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[TreatmentPlanItems] (
    [TreatmentPlanItemId] [bigint] IDENTITY(1,1) NOT NULL,
    [TreatmentPlanId] [bigint] NOT NULL,
    [DepartmentServiceId] [bigint] NULL,
    [AssignedDentistUserId] [bigint] NULL,
    [ToothNumber] [varchar](10) NULL,
    [SequenceNo] [int] NOT NULL,
    [ExpectedDate] [date] NULL,
    [EstimatedCost] [decimal](18, 2) NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Planned'),
    [Note] [nvarchar](500) NULL,
    [StageName] [nvarchar](200) NULL,
    [ResultSummary] [nvarchar](max) NULL,
    [CompletedAt] [datetime2](7) NULL,
    [SkippedReason] [nvarchar](max) NULL,

    CONSTRAINT [PK_TreatmentPlanItems]
        PRIMARY KEY CLUSTERED ([TreatmentPlanItemId] ASC),

    CONSTRAINT [UQ_TreatmentPlanItems_Sequence]
        UNIQUE NONCLUSTERED ([TreatmentPlanId] ASC, [SequenceNo] ASC),

    CONSTRAINT [CK_TreatmentPlanItems_Cost]
        CHECK (([EstimatedCost] IS NULL OR [EstimatedCost]>=(0))),

    CONSTRAINT [CK_TreatmentPlanItems_Sequence]
        CHECK (([SequenceNo]>(0))),

    CONSTRAINT [CK_TreatmentPlanItems_Status]
        CHECK ([Status] IN ('NotStarted', 'Scheduled', 'InProgress', 'In_Progress', 'Completed', 'Skipped', 'Active', 'Pending', 'Planned', 'Cancelled')),

    CONSTRAINT [FK_TreatmentPlanItems_Dentist]
        FOREIGN KEY ([AssignedDentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_TreatmentPlanItems_Plan]
        FOREIGN KEY ([TreatmentPlanId])
        REFERENCES [dbo].[TreatmentPlans] ([TreatmentPlanId]),

    CONSTRAINT [FK_TreatmentPlanItems_Service]
        FOREIGN KEY ([DepartmentServiceId])
        REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId])
);
GO


-- =====================================================================================
-- 05. APPOINTMENTS & BOOKING MANAGEMENT
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table: dbo.Appointments
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Appointments] (
    [AppointmentId] [bigint] IDENTITY(1,1) NOT NULL,
    [AppointmentCode] [varchar](30) NOT NULL,
    [PatientUserId] [bigint] NOT NULL,
    [DepartmentId] [bigint] NULL,
    [RequestedServiceId] [bigint] NULL,
    [RequestedDentistUserId] [bigint] NULL,
    [AssignedDentistUserId] [bigint] NULL,
    [RoomId] [bigint] NULL,
    [DentalChairId] [bigint] NULL,
    [ScheduledStart] [datetime2](7) NULL,
    [ScheduledEnd] [datetime2](7) NULL,
    [ReasonForVisit] [nvarchar](1000) NOT NULL,
    [BookingSource] [varchar](20) NOT NULL,
    [QueueNumber] [varchar](30) NULL,
    [Status] [varchar](50) NOT NULL
        DEFAULT ('PendingReceptionReview'),
    [CreatedByUserId] [bigint] NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [ConfirmedAt] [datetime2](7) NULL,
    [CheckedInAt] [datetime2](7) NULL,
    [CompletedAt] [datetime2](7) NULL,
    [CancelledAt] [datetime2](7) NULL,
    [CancellationReason] [nvarchar](500) NULL,
    [UpdatedAt] [datetime2](7) NULL,
    [ScheduledDate]  AS (CONVERT([date],[ScheduledStart])) PERSISTED,
    [TreatmentPlanItemId] [bigint] NULL,
    [ComboCode] [varchar](50) NULL,
    [ComboId] [bigint] NULL,
    [CareEpisodeId] [bigint] NULL,

    CONSTRAINT [PK_Appointments]
        PRIMARY KEY CLUSTERED ([AppointmentId] ASC),

    UNIQUE NONCLUSTERED ([AppointmentCode] ASC),

    CHECK (([BookingSource]='Receptionist' OR [BookingSource]='Patient')),

    CHECK (([Status]='NoShow' OR [Status]='Cancelled' OR [Status]='Expired' OR [Status]='Withdrawn' OR [Status]='Rejected' OR [Status]='Completed' OR [Status]='InService' OR [Status]='Called' OR [Status]='Waiting' OR [Status]='CheckedIn' OR [Status]='Confirmed' OR [Status]='AwaitingPatientResponse' OR [Status]='PendingDepartmentReview' OR [Status]='PendingReceptionReview')),

    CONSTRAINT [CK_Appointments_Schedule]
        CHECK (([ScheduledEnd] IS NULL OR [ScheduledStart] IS NULL OR [ScheduledEnd]>[ScheduledStart])),

    CONSTRAINT [UQ_Appointments_Patient_Appointment]
        UNIQUE NONCLUSTERED ([PatientUserId] ASC, [AppointmentId] ASC),

    CONSTRAINT [CK_Appointments_ChairRequiresRoom]
        CHECK (([DentalChairId] IS NULL OR [RoomId] IS NOT NULL)),

    CONSTRAINT [FK_Appointments_DepartmentService_Composite]
        FOREIGN KEY ([DepartmentId], [RequestedServiceId])
        REFERENCES [dbo].[DepartmentServices] ([DepartmentId], [DepartmentServiceId]),

    CONSTRAINT [FK_Appointments_RoomChair_Composite]
        FOREIGN KEY ([RoomId], [DentalChairId])
        REFERENCES [dbo].[DentalChairs] ([RoomId], [DentalChairId]),

    CONSTRAINT [FK_Appointments_AssignedDentist]
        FOREIGN KEY ([AssignedDentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_Appointments_Chair]
        FOREIGN KEY ([DentalChairId])
        REFERENCES [dbo].[DentalChairs] ([DentalChairId]),

    CONSTRAINT [FK_Appointments_CreatedBy]
        FOREIGN KEY ([CreatedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_Appointments_Department]
        FOREIGN KEY ([DepartmentId])
        REFERENCES [dbo].[Departments] ([DepartmentId]),

    CONSTRAINT [FK_Appointments_Patient]
        FOREIGN KEY ([PatientUserId])
        REFERENCES [dbo].[PatientProfiles] ([UserId]),

    CONSTRAINT [FK_Appointments_RequestedDentist]
        FOREIGN KEY ([RequestedDentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_Appointments_Room]
        FOREIGN KEY ([RoomId])
        REFERENCES [dbo].[Rooms] ([RoomId]),

    CONSTRAINT [FK_Appointments_Service]
        FOREIGN KEY ([RequestedServiceId])
        REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId]),

    CONSTRAINT [FK_Appointments_ServiceCombos]
        FOREIGN KEY ([ComboId])
        REFERENCES [dbo].[ServiceCombos] ([ComboId]),

    CONSTRAINT [FK_Appointments_TreatmentPlanItems_TreatmentPlanItemId]
        FOREIGN KEY ([TreatmentPlanItemId])
        REFERENCES [dbo].[TreatmentPlanItems] ([TreatmentPlanItemId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.AppointmentChangeProposals
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[AppointmentChangeProposals] (
    [ProposalId] [bigint] IDENTITY(1,1) NOT NULL,
    [AppointmentId] [bigint] NOT NULL,
    [ProposedByUserId] [bigint] NOT NULL,
    [ProposedDepartmentId] [bigint] NULL,
    [ProposedDentistUserId] [bigint] NULL,
    [ProposedStart] [datetime2](7) NULL,
    [ProposedEnd] [datetime2](7) NULL,
    [Reason] [nvarchar](1000) NOT NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Pending'),
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [RespondedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_AppointmentChangeProposals]
        PRIMARY KEY CLUSTERED ([ProposalId] ASC),

    CHECK (([Status]='Expired' OR [Status]='Rejected' OR [Status]='Accepted' OR [Status]='Pending')),

    CONSTRAINT [CK_AppointmentChangeProposals_Time]
        CHECK (([ProposedEnd] IS NULL OR [ProposedStart] IS NULL OR [ProposedEnd]>[ProposedStart])),

    CONSTRAINT [FK_AppointmentChangeProposals_Appointment]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_AppointmentChangeProposals_Dentist]
        FOREIGN KEY ([ProposedDentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_AppointmentChangeProposals_Department]
        FOREIGN KEY ([ProposedDepartmentId])
        REFERENCES [dbo].[Departments] ([DepartmentId]),

    CONSTRAINT [FK_AppointmentChangeProposals_User]
        FOREIGN KEY ([ProposedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.AppointmentDisruptions
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[AppointmentDisruptions] (
    [DisruptionId] [bigint] IDENTITY(1,1) NOT NULL,
    [AppointmentId] [bigint] NOT NULL,
    [DentistLeaveRequestId] [bigint] NOT NULL,
    [OriginalDentistUserId] [bigint] NOT NULL,
    [OriginalScheduledStart] [datetime2](7) NOT NULL,
    [OriginalScheduledEnd] [datetime2](7) NOT NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_AppointmentDisruptions_Status] DEFAULT ('PendingPatientChoice'),
    [PatientChoice] [varchar](30) NULL,
    [ResolvedAppointmentId] [bigint] NULL,
    [NewDentistUserId] [bigint] NULL,
    [ResolvedByUserId] [bigint] NULL,
    [ResolvedAt] [datetime2](7) NULL,
    [StaffNotes] [nvarchar](500) NULL,
    [EmailSentAt] [datetime2](7) NULL,
    [EmailDeliveryStatus] [varchar](30) NULL
        CONSTRAINT [DF_AppointmentDisruptions_EmailStatus] DEFAULT ('Pending'),
    [EmailFailureReason] [nvarchar](500) NULL,
    [EmailRetryCount] [int] NOT NULL
        CONSTRAINT [DF_AppointmentDisruptions_EmailRetryCount] DEFAULT (0),
    [ActionToken] [nvarchar](100) NULL,
    [TokenExpiresAt] [datetime2](7) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_AppointmentDisruptions_CreatedAt] DEFAULT (sysutcdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_AppointmentDisruptions]
        PRIMARY KEY CLUSTERED ([DisruptionId] ASC),

    CONSTRAINT [CK_AppointmentDisruptions_PatientChoice]
        CHECK (([PatientChoice] IS NULL OR ([PatientChoice]='ContactStaff' OR [PatientChoice]='Reschedule' OR [PatientChoice]='ChangeDoctor'))),

    CONSTRAINT [CK_AppointmentDisruptions_Status]
        CHECK (([Status]='Cancelled' OR [Status]='ResolvedByStaff' OR [Status]='NeedsStaffAction' OR [Status]='Rescheduled' OR [Status]='DoctorChanged' OR [Status]='PendingPatientChoice')),

    CONSTRAINT [FK_AppointmentDisruptions_Appointment]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_AppointmentDisruptions_LeaveRequest]
        FOREIGN KEY ([DentistLeaveRequestId])
        REFERENCES [dbo].[DentistLeaveRequests] ([LeaveRequestId]),

    CONSTRAINT [FK_AppointmentDisruptions_NewDentist]
        FOREIGN KEY ([NewDentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_AppointmentDisruptions_OriginalDentist]
        FOREIGN KEY ([OriginalDentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_AppointmentDisruptions_ResolvedAppointment]
        FOREIGN KEY ([ResolvedAppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_AppointmentDisruptions_ResolvedByUser]
        FOREIGN KEY ([ResolvedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.AppointmentReminderJobs
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[AppointmentReminderJobs] (
    [ReminderJobId] [bigint] IDENTITY(1,1) NOT NULL,
    [AppointmentId] [bigint] NOT NULL,
    [ReminderType] [varchar](20) NOT NULL,
    [ScheduledSendTime] [datetime2](7) NOT NULL,
    [RecipientEmail] [varchar](150) NOT NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Scheduled'),
    [SentAt] [datetime2](7) NULL,
    [ErrorMessage] [nvarchar](1000) NULL,
    [RetryCount] [int] NOT NULL
        DEFAULT (0),
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_AppointmentReminderJobs]
        PRIMARY KEY CLUSTERED ([ReminderJobId] ASC),

    CONSTRAINT [FK_AppointmentReminderJobs_Appointments]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]) ON DELETE CASCADE
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.AppointmentStatusHistories
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[AppointmentStatusHistories] (
    [HistoryId] [bigint] IDENTITY(1,1) NOT NULL,
    [AppointmentId] [bigint] NOT NULL,
    [OldStatus] [varchar](50) NULL,
    [NewStatus] [varchar](50) NOT NULL,
    [ChangedByUserId] [bigint] NULL,
    [Note] [nvarchar](500) NULL,
    [ChangedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),

    CONSTRAINT [PK_AppointmentStatusHistories]
        PRIMARY KEY CLUSTERED ([HistoryId] ASC),

    CONSTRAINT [FK_AppointmentStatusHistories_Appointment]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_AppointmentStatusHistories_User]
        FOREIGN KEY ([ChangedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO


-- =====================================================================================
-- 06. CARE EPISODES, CLINICAL ENCOUNTERS & MEDICAL RECORDS
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table: dbo.CareEpisodes
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[CareEpisodes] (
    [CareEpisodeId] [bigint] IDENTITY(1,1) NOT NULL,
    [EpisodeCode] [varchar](30) NOT NULL,
    [PatientUserId] [bigint] NOT NULL,
    [RootAppointmentId] [bigint] NOT NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_CareEpisodes_Status] DEFAULT ('Open'),
    [OpenedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_CareEpisodes_OpenedAtUtc] DEFAULT (sysutcdatetime()),
    [ClosedAtUtc] [datetime2](7) NULL,
    [CreatedByUserId] [bigint] NULL,
    [CreatedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_CareEpisodes_CreatedAtUtc] DEFAULT (sysutcdatetime()),
    [UpdatedAtUtc] [datetime2](7) NULL,
    [Notes] [nvarchar](500) NULL,

    CONSTRAINT [PK_CareEpisodes]
        PRIMARY KEY CLUSTERED ([CareEpisodeId] ASC),

    CONSTRAINT [UQ_CareEpisodes_EpisodeCode]
        UNIQUE NONCLUSTERED ([EpisodeCode] ASC),

    CONSTRAINT [FK_CareEpisodes_PatientUser]
        FOREIGN KEY ([PatientUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_CareEpisodes_RootAppointment]
        FOREIGN KEY ([RootAppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_CareEpisodes_RootAppointment_Patient_Composite]
        FOREIGN KEY ([PatientUserId], [RootAppointmentId])
        REFERENCES [dbo].[Appointments] ([PatientUserId], [AppointmentId]),

    CONSTRAINT [UQ_CareEpisodes_Patient_Episode]
        UNIQUE NONCLUSTERED ([PatientUserId] ASC, [CareEpisodeId] ASC)
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ClinicalConsultations
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ClinicalConsultations] (
    [ConsultationId] [bigint] IDENTITY(1,1) NOT NULL,
    [SourceAppointmentId] [bigint] NOT NULL,
    [PatientUserId] [bigint] NOT NULL,
    [RequestingDentistUserId] [bigint] NOT NULL,
    [ConsultedDentistUserId] [bigint] NOT NULL,
    [ConsultationReason] [nvarchar](500) NOT NULL,
    [ClinicalQuestion] [nvarchar](1000) NOT NULL,
    [Urgency] [varchar](20) NOT NULL
        CONSTRAINT [DF_ClinicalConsultations_Urgency] DEFAULT ('Normal'),
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_ClinicalConsultations_Status] DEFAULT ('Requested'),
    [DentistOpinion] [nvarchar](2000) NULL,
    [Recommendations] [nvarchar](1000) NULL,
    [RequestedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_ClinicalConsultations_RequestedAt] DEFAULT (sysutcdatetime()),
    [AnsweredAtUtc] [datetime2](7) NULL,

    CONSTRAINT [PK_ClinicalConsultations]
        PRIMARY KEY CLUSTERED ([ConsultationId] ASC),

    CONSTRAINT [CK_ClinicalConsultations_Status]
        CHECK (([Status]='Cancelled' OR [Status]='Answered' OR [Status]='InReview' OR [Status]='Requested')),

    CONSTRAINT [CK_ClinicalConsultations_Urgency]
        CHECK (([Urgency]='Urgent' OR [Urgency]='Normal')),

    CONSTRAINT [FK_ClinicalConsultations_Appointment]
        FOREIGN KEY ([SourceAppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_ClinicalConsultations_ConsultedDentist]
        FOREIGN KEY ([ConsultedDentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_ClinicalConsultations_Patient]
        FOREIGN KEY ([PatientUserId])
        REFERENCES [dbo].[PatientProfiles] ([UserId]),

    CONSTRAINT [FK_ClinicalConsultations_RequestingDentist]
        FOREIGN KEY ([RequestingDentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ClinicalEntries
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ClinicalEntries] (
    [ClinicalEntryId] [bigint] IDENTITY(1,1) NOT NULL,
    [MedicalRecordId] [bigint] NOT NULL,
    [AppointmentId] [bigint] NULL,
    [DentistUserId] [bigint] NOT NULL,
    [Symptoms] [nvarchar](max) NULL,
    [ClinicalFindings] [nvarchar](max) NULL,
    [Diagnosis] [nvarchar](max) NULL,
    [DentalConditions] [nvarchar](max) NULL,
    [ClinicalNotes] [nvarchar](max) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_ClinicalEntries]
        PRIMARY KEY CLUSTERED ([ClinicalEntryId] ASC),

    CONSTRAINT [FK_ClinicalEntries_Appointment]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_ClinicalEntries_Dentist]
        FOREIGN KEY ([DentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_ClinicalEntries_Record]
        FOREIGN KEY ([MedicalRecordId])
        REFERENCES [dbo].[DentalMedicalRecords] ([MedicalRecordId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.OdontogramEntries
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[OdontogramEntries] (
    [OdontogramEntryId] [bigint] IDENTITY(1,1) NOT NULL,
    [ClinicalEntryId] [bigint] NOT NULL,
    [ToothNumber] [varchar](10) NOT NULL,
    [Surface] [varchar](30) NULL,
    [Condition] [nvarchar](200) NOT NULL,
    [Note] [nvarchar](500) NULL,

    CONSTRAINT [PK_OdontogramEntries]
        PRIMARY KEY CLUSTERED ([OdontogramEntryId] ASC),

    CONSTRAINT [FK_OdontogramEntries_ClinicalEntry]
        FOREIGN KEY ([ClinicalEntryId])
        REFERENCES [dbo].[ClinicalEntries] ([ClinicalEntryId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.TreatmentSessions
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[TreatmentSessions] (
    [TreatmentSessionId] [bigint] IDENTITY(1,1) NOT NULL,
    [TreatmentPlanItemId] [bigint] NULL,
    [AppointmentId] [bigint] NOT NULL,
    [DentistUserId] [bigint] NOT NULL,
    [SessionDate] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [TreatmentResult] [nvarchar](max) NULL,
    [ClinicalNote] [nvarchar](max) NULL,

    CONSTRAINT [PK_TreatmentSessions]
        PRIMARY KEY CLUSTERED ([TreatmentSessionId] ASC),

    CONSTRAINT [FK_TreatmentSessions_Appointment]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_TreatmentSessions_Dentist]
        FOREIGN KEY ([DentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_TreatmentSessions_Item]
        FOREIGN KEY ([TreatmentPlanItemId])
        REFERENCES [dbo].[TreatmentPlanItems] ([TreatmentPlanItemId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.PerformedServices
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[PerformedServices] (
    [PerformedServiceId] [bigint] IDENTITY(1,1) NOT NULL,
    [TreatmentSessionId] [bigint] NOT NULL,
    [DepartmentServiceId] [bigint] NOT NULL,
    [Quantity] [int] NOT NULL
        DEFAULT (1),
    [UnitPrice] [decimal](18, 2) NOT NULL,
    [Amount]  AS ([Quantity]*[UnitPrice]) PERSISTED,

    CONSTRAINT [PK_PerformedServices]
        PRIMARY KEY CLUSTERED ([PerformedServiceId] ASC),

    CONSTRAINT [CK_PerformedServices_Quantity]
        CHECK (([Quantity]>(0))),

    CONSTRAINT [CK_PerformedServices_UnitPrice]
        CHECK (([UnitPrice]>=(0))),

    CONSTRAINT [FK_PerformedServices_Service]
        FOREIGN KEY ([DepartmentServiceId])
        REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId]),

    CONSTRAINT [FK_PerformedServices_Session]
        FOREIGN KEY ([TreatmentSessionId])
        REFERENCES [dbo].[TreatmentSessions] ([TreatmentSessionId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.EpisodeServiceTasks
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[EpisodeServiceTasks] (
    [ServiceTaskId] [bigint] IDENTITY(1,1) NOT NULL,
    [TaskCode] [varchar](30) NOT NULL,
    [CareEpisodeId] [bigint] NOT NULL,
    [AppointmentId] [bigint] NOT NULL,
    [DepartmentServiceId] [bigint] NOT NULL,
    [PerformingDentistUserId] [bigint] NOT NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_EpisodeServiceTasks_Status] DEFAULT ('Planned'),
    [Quantity] [int] NOT NULL
        CONSTRAINT [DF_EpisodeServiceTasks_Quantity] DEFAULT ((1)),
    [UnitPriceSnapshot] [decimal](18, 2) NOT NULL,
    [EstimatedDurationMinutes] [int] NOT NULL
        CONSTRAINT [DF_EpisodeServiceTasks_EstimatedDurationMinutes] DEFAULT ((30)),
    [PlannedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_EpisodeServiceTasks_PlannedAtUtc] DEFAULT (sysutcdatetime()),
    [StartedAtUtc] [datetime2](7) NULL,
    [CompletedAtUtc] [datetime2](7) NULL,
    [CancelledAtUtc] [datetime2](7) NULL,
    [CancellationReason] [nvarchar](250) NULL,
    [ClinicalRecordId] [bigint] NULL,
    [AppliedWorkflowId] [bigint] NULL,
    [AppliedWorkflowVersion] [int] NULL,
    [Notes] [nvarchar](500) NULL,
    [CreatedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_EpisodeServiceTasks_CreatedAtUtc] DEFAULT (sysutcdatetime()),
    [UpdatedAtUtc] [datetime2](7) NULL,

    CONSTRAINT [PK_EpisodeServiceTasks]
        PRIMARY KEY CLUSTERED ([ServiceTaskId] ASC),

    CONSTRAINT [UQ_EpisodeServiceTasks_TaskCode]
        UNIQUE NONCLUSTERED ([TaskCode] ASC),

    CONSTRAINT [CK_EpisodeServiceTasks_Quantity]
        CHECK ([Quantity]>(0)),

    CONSTRAINT [CK_EpisodeServiceTasks_Status]
        CHECK ([Status]='Cancelled' OR [Status]='Completed' OR [Status]='InProgress' OR [Status]='Pending' OR [Status]='Planned'),

    CONSTRAINT [CK_EpisodeServiceTasks_UnitPriceSnapshot]
        CHECK ([UnitPriceSnapshot]>=(0)),

    CONSTRAINT [FK_EpisodeServiceTasks_Appointments]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_EpisodeServiceTasks_CareEpisodes]
        FOREIGN KEY ([CareEpisodeId])
        REFERENCES [dbo].[CareEpisodes] ([CareEpisodeId]),

    CONSTRAINT [FK_EpisodeServiceTasks_DentistUser]
        FOREIGN KEY ([PerformingDentistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_EpisodeServiceTasks_DepartmentServices]
        FOREIGN KEY ([DepartmentServiceId])
        REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId]),

    CONSTRAINT [FK_EpisodeServiceTasks_ServiceWorkflows]
        FOREIGN KEY ([AppliedWorkflowId])
        REFERENCES [dbo].[ServiceWorkflows] ([WorkflowId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.EpisodeServiceTaskStepProgresses (Individual Workflow Step Execution Progress)
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[EpisodeServiceTaskStepProgresses] (
    [ProgressId] [bigint] IDENTITY(1,1) NOT NULL,
    [ServiceTaskId] [bigint] NOT NULL,
    [StepId] [bigint] NOT NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_StepProgress_Status] DEFAULT ('Pending'),
    [ResultValue] [nvarchar](max) NULL,
    [Notes] [nvarchar](1000) NULL,
    [EvidenceUrl] [nvarchar](500) NULL,
    [CompletedByDentistUserId] [bigint] NULL,
    [StartedAtUtc] [datetime2](7) NULL,
    [CompletedAtUtc] [datetime2](7) NULL,
    [CreatedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_StepProgress_CreatedAtUtc] DEFAULT (sysutcdatetime()),
    [UpdatedAtUtc] [datetime2](7) NULL,

    CONSTRAINT [PK_EpisodeServiceTaskStepProgresses]
        PRIMARY KEY CLUSTERED ([ProgressId] ASC),

    CONSTRAINT [UQ_StepProgress_Task_Step]
        UNIQUE NONCLUSTERED ([ServiceTaskId], [StepId]),

    CONSTRAINT [FK_StepProgress_ServiceTasks]
        FOREIGN KEY ([ServiceTaskId])
        REFERENCES [dbo].[EpisodeServiceTasks] ([ServiceTaskId]) ON DELETE CASCADE,

    CONSTRAINT [FK_StepProgress_WorkflowSteps]
        FOREIGN KEY ([StepId])
        REFERENCES [dbo].[ServiceWorkflowSteps] ([StepId]),

    CONSTRAINT [FK_StepProgress_DentistUser]
        FOREIGN KEY ([CompletedByDentistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

CREATE NONCLUSTERED INDEX [IX_StepProgress_Task] ON [dbo].[EpisodeServiceTaskStepProgresses] ([ServiceTaskId]);
GO
CREATE NONCLUSTERED INDEX [IX_StepProgress_Step] ON [dbo].[EpisodeServiceTaskStepProgresses] ([StepId]);
GO


-- =====================================================================================
-- 07. DIAGNOSTIC IMAGING & RADIOLOGY
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table: dbo.DiagnosticOrders
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DiagnosticOrders] (
    [DiagnosticOrderId] [bigint] IDENTITY(1,1) NOT NULL,
    [AppointmentId] [bigint] NOT NULL,
    [PatientUserId] [bigint] NOT NULL,
    [DentistUserId] [bigint] NOT NULL,
    [DiagnosticType] [varchar](50) NOT NULL,
    [ClinicalIndication] [nvarchar](500) NOT NULL,
    [IsExternal] [bit] NOT NULL
        CONSTRAINT [DF_DiagnosticOrders_IsExternal] DEFAULT (0),
    [ExternalFacilityName] [nvarchar](200) NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_DiagnosticOrders_Status] DEFAULT ('Ordered'),
    [BilledAtUtc] [datetime2](7) NULL,
    [InvoiceItemId] [bigint] NULL,
    [CreatedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_DiagnosticOrders_CreatedAtUtc] DEFAULT (sysutcdatetime()),
    [UpdatedAtUtc] [datetime2](7) NULL,

    CONSTRAINT [PK_DiagnosticOrders]
        PRIMARY KEY CLUSTERED ([DiagnosticOrderId] ASC),

    CONSTRAINT [CK_DiagnosticOrders_Status]
        CHECK (([Status]='Cancelled' OR [Status]='Completed' OR [Status]='InProgress' OR [Status]='Scheduled' OR [Status]='Ordered')),

    CONSTRAINT [FK_DiagnosticOrders_Appointment]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_DiagnosticOrders_Dentist]
        FOREIGN KEY ([DentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_DiagnosticOrders_Patient]
        FOREIGN KEY ([PatientUserId])
        REFERENCES [dbo].[PatientProfiles] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ImagingSessions
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ImagingSessions] (
    [ImagingSessionId] [bigint] IDENTITY(1,1) NOT NULL,
    [DiagnosticOrderId] [bigint] NOT NULL,
    [SourceAppointmentId] [bigint] NOT NULL,
    [PatientUserId] [bigint] NOT NULL,
    [ImagingRoomId] [bigint] NOT NULL,
    [EquipmentId] [bigint] NOT NULL,
    [ScheduledStartUtc] [datetime2](7) NOT NULL,
    [ScheduledEndUtc] [datetime2](7) NOT NULL,
    [ActualStartUtc] [datetime2](7) NULL,
    [ActualEndUtc] [datetime2](7) NULL,
    [AssignedOperatorUserId] [bigint] NULL,
    [QueueNumber] [varchar](20) NULL,
    [Priority] [varchar](20) NOT NULL
        CONSTRAINT [DF_ImagingSessions_Priority] DEFAULT ('Normal'),
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_ImagingSessions_Status] DEFAULT ('Scheduled'),
    [CheckInTimeUtc] [datetime2](7) NULL,
    [EstimatedWaitMinutes] [int] NULL,
    [DelayNotificationSent] [bit] NOT NULL
        CONSTRAINT [DF_ImagingSessions_DelayNotif] DEFAULT (0),
    [EscalationAlertSent] [bit] NOT NULL
        CONSTRAINT [DF_ImagingSessions_EscalationAlert] DEFAULT (0),
    [Notes] [nvarchar](500) NULL,
    [CreatedByUserId] [bigint] NOT NULL,
    [CreatedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_ImagingSessions_CreatedAtUtc] DEFAULT (sysutcdatetime()),
    [UpdatedAtUtc] [datetime2](7) NULL,

    CONSTRAINT [PK_ImagingSessions]
        PRIMARY KEY CLUSTERED ([ImagingSessionId] ASC),

    CONSTRAINT [CK_ImagingSessions_Priority]
        CHECK (([Priority]='Urgent' OR [Priority]='Normal')),

    CONSTRAINT [CK_ImagingSessions_Status]
        CHECK (([Status]='UnableToPerform' OR [Status]='NeedsReschedule' OR [Status]='EquipmentUnavailable' OR [Status]='Cancelled' OR [Status]='Completed' OR [Status]='ResultAvailable' OR [Status]='Acquired' OR [Status]='InProgress' OR [Status]='Waiting' OR [Status]='CheckedIn' OR [Status]='Scheduled')),

    CONSTRAINT [FK_ImagingSessions_Appointment]
        FOREIGN KEY ([SourceAppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_ImagingSessions_CreatedBy]
        FOREIGN KEY ([CreatedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_ImagingSessions_Equipment]
        FOREIGN KEY ([EquipmentId])
        REFERENCES [dbo].[ImagingEquipments] ([EquipmentId]),

    CONSTRAINT [FK_ImagingSessions_RoomEquipment_Composite]
        FOREIGN KEY ([ImagingRoomId], [EquipmentId])
        REFERENCES [dbo].[ImagingEquipments] ([RoomId], [EquipmentId]),

    CONSTRAINT [FK_ImagingSessions_Operator]
        FOREIGN KEY ([AssignedOperatorUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_ImagingSessions_Order]
        FOREIGN KEY ([DiagnosticOrderId])
        REFERENCES [dbo].[DiagnosticOrders] ([DiagnosticOrderId]),

    CONSTRAINT [FK_ImagingSessions_Patient]
        FOREIGN KEY ([PatientUserId])
        REFERENCES [dbo].[PatientProfiles] ([UserId]),

    CONSTRAINT [FK_ImagingSessions_Room]
        FOREIGN KEY ([ImagingRoomId])
        REFERENCES [dbo].[Rooms] ([RoomId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.DiagnosticResults
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DiagnosticResults] (
    [DiagnosticResultId] [bigint] IDENTITY(1,1) NOT NULL,
    [DiagnosticOrderId] [bigint] NOT NULL,
    [ImagingSessionId] [bigint] NULL,
    [SourceType] [varchar](20) NOT NULL
        CONSTRAINT [DF_DiagnosticResults_Source] DEFAULT ('Internal'),
    [EquipmentId] [bigint] NULL,
    [ExternalFacilityName] [nvarchar](200) NULL,
    [FileUrl] [nvarchar](500) NOT NULL,
    [ThumbnailUrl] [nvarchar](500) NULL,
    [ImageFormat] [varchar](20) NOT NULL
        CONSTRAINT [DF_DiagnosticResults_Format] DEFAULT ('JPEG'),
    [Findings] [nvarchar](1000) NULL,
    [ReviewedByDentistUserId] [bigint] NULL,
    [ReviewedAtUtc] [datetime2](7) NULL,
    [ReviewNotes] [nvarchar](1000) NULL,
    [IsReviewed] [bit] NOT NULL
        CONSTRAINT [DF_DiagnosticResults_IsReviewed] DEFAULT (0),
    [UploadedByUserId] [bigint] NOT NULL,
    [PerformedAtUtc] [datetime2](7) NULL,
    [UploadedAtUtc] [datetime2](7) NOT NULL
        CONSTRAINT [DF_DiagnosticResults_UploadedAtUtc] DEFAULT (sysutcdatetime()),

    CONSTRAINT [PK_DiagnosticResults]
        PRIMARY KEY CLUSTERED ([DiagnosticResultId] ASC),

    CONSTRAINT [CK_DiagnosticResults_Source]
        CHECK (([SourceType]='External' OR [SourceType]='Internal')),

    CONSTRAINT [FK_DiagnosticResults_Equipment]
        FOREIGN KEY ([EquipmentId])
        REFERENCES [dbo].[ImagingEquipments] ([EquipmentId]),

    CONSTRAINT [FK_DiagnosticResults_Order]
        FOREIGN KEY ([DiagnosticOrderId])
        REFERENCES [dbo].[DiagnosticOrders] ([DiagnosticOrderId]),

    CONSTRAINT [FK_DiagnosticResults_Reviewer]
        FOREIGN KEY ([ReviewedByDentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_DiagnosticResults_Session]
        FOREIGN KEY ([ImagingSessionId])
        REFERENCES [dbo].[ImagingSessions] ([ImagingSessionId]),

    CONSTRAINT [FK_DiagnosticResults_UploadedBy]
        FOREIGN KEY ([UploadedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.ImagingRecords
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[ImagingRecords] (
    [ImagingRecordId] [bigint] IDENTITY(1,1) NOT NULL,
    [ClinicalEntryId] [bigint] NOT NULL,
    [ImagingType] [varchar](50) NOT NULL,
    [FileUrl] [nvarchar](500) NULL,
    [ResultSummary] [nvarchar](max) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),

    CONSTRAINT [PK_ImagingRecords]
        PRIMARY KEY CLUSTERED ([ImagingRecordId] ASC),

    CONSTRAINT [FK_ImagingRecords_ClinicalEntry]
        FOREIGN KEY ([ClinicalEntryId])
        REFERENCES [dbo].[ClinicalEntries] ([ClinicalEntryId])
);
GO


-- =====================================================================================
-- 08. PHARMACY & INVENTORY MANAGEMENT
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table: dbo.Medicines
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Medicines] (
    [MedicineId] [bigint] IDENTITY(1,1) NOT NULL,
    [MedicineCode] [nvarchar](50) NOT NULL,
    [MedicineName] [nvarchar](200) NOT NULL,
    [ActiveIngredient] [nvarchar](200) NULL,
    [Concentration] [nvarchar](100) NULL,
    [DosageForm] [nvarchar](100) NULL,
    [Unit] [nvarchar](50) NOT NULL,
    [UnitPrice] [decimal](18, 2) NOT NULL,
    [MinimumStockAlert] [int] NOT NULL
        CONSTRAINT [DF_Medicines_MinAlert] DEFAULT (20),
    [UsageInstructions] [nvarchar](500) NULL,
    [Status] [nvarchar](50) NOT NULL
        CONSTRAINT [DF_Medicines_Status] DEFAULT ('Active'),
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_Medicines_CreatedAt] DEFAULT (sysutcdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_Medicines]
        PRIMARY KEY CLUSTERED ([MedicineId] ASC),

    CONSTRAINT [UQ_Medicines_MedicineCode]
        UNIQUE NONCLUSTERED ([MedicineCode] ASC),

    CONSTRAINT [CHK_Medicines_MinAlert]
        CHECK (([MinimumStockAlert]>=(0))),

    CONSTRAINT [CHK_Medicines_UnitPrice]
        CHECK (([UnitPrice]>=(0)))
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.MedicineBatches
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[MedicineBatches] (
    [MedicineBatchId] [bigint] IDENTITY(1,1) NOT NULL,
    [MedicineId] [bigint] NOT NULL,
    [BatchNumber] [nvarchar](100) NOT NULL,
    [ExpiryDate] [date] NOT NULL,
    [QuantityReceived] [int] NOT NULL,
    [QuantityAvailable] [int] NOT NULL,
    [CostPrice] [decimal](18, 2) NOT NULL
        CONSTRAINT [DF_MedicineBatches_CostPrice] DEFAULT (0),
    [Status] [nvarchar](50) NOT NULL
        CONSTRAINT [DF_MedicineBatches_Status] DEFAULT ('Available'),
    [ReceivedDate] [datetime2](7) NOT NULL
        CONSTRAINT [DF_MedicineBatches_ReceivedDate] DEFAULT (sysutcdatetime()),
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_MedicineBatches_CreatedAt] DEFAULT (sysutcdatetime()),
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_MedicineBatches]
        PRIMARY KEY CLUSTERED ([MedicineBatchId] ASC),

    CONSTRAINT [UQ_MedicineBatches_Medicine_Batch]
        UNIQUE NONCLUSTERED ([MedicineId] ASC, [BatchNumber] ASC),

    CONSTRAINT [UQ_MedicineBatches_Medicine_BatchId]
        UNIQUE NONCLUSTERED ([MedicineId] ASC, [MedicineBatchId] ASC),

    CONSTRAINT [CHK_MedicineBatches_CostPrice]
        CHECK (([CostPrice]>=(0))),

    CONSTRAINT [CHK_MedicineBatches_Qty]
        CHECK (([QuantityReceived]>=(0) AND [QuantityAvailable]>=(0) AND [QuantityAvailable]<=[QuantityReceived])),

    CONSTRAINT [FK_MedicineBatches_Medicines]
        FOREIGN KEY ([MedicineId])
        REFERENCES [dbo].[Medicines] ([MedicineId]) ON DELETE CASCADE
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.MedicineStockTransactions
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[MedicineStockTransactions] (
    [TransactionId] [bigint] IDENTITY(1,1) NOT NULL,
    [MedicineBatchId] [bigint] NOT NULL,
    [TransactionType] [nvarchar](50) NOT NULL,
    [Quantity] [int] NOT NULL,
    [QuantityBefore] [int] NOT NULL,
    [QuantityAfter] [int] NOT NULL,
    [Reason] [nvarchar](500) NOT NULL,
    [PerformedByUserId] [bigint] NOT NULL,
    [ReferenceCode] [nvarchar](100) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_MedicineStockTransactions_CreatedAt] DEFAULT (sysutcdatetime()),

    CONSTRAINT [PK_MedicineStockTransactions]
        PRIMARY KEY CLUSTERED ([TransactionId] ASC),

    CONSTRAINT [CHK_MedicineStockTransactions_Qty]
        CHECK (([Quantity]>(0))),

    CONSTRAINT [FK_MedicineStockTransactions_Batches]
        FOREIGN KEY ([MedicineBatchId])
        REFERENCES [dbo].[MedicineBatches] ([MedicineBatchId]) ON DELETE CASCADE,

    CONSTRAINT [FK_MedicineStockTransactions_Users]
        FOREIGN KEY ([PerformedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.Prescriptions
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Prescriptions] (
    [PrescriptionId] [bigint] IDENTITY(1,1) NOT NULL,
    [TreatmentSessionId] [bigint] NOT NULL,
    [GeneralInstructions] [nvarchar](max) NULL,
    [IssuedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),

    CONSTRAINT [PK_Prescriptions]
        PRIMARY KEY CLUSTERED ([PrescriptionId] ASC),

    UNIQUE NONCLUSTERED ([TreatmentSessionId] ASC),

    CONSTRAINT [FK_Prescriptions_TreatmentSession]
        FOREIGN KEY ([TreatmentSessionId])
        REFERENCES [dbo].[TreatmentSessions] ([TreatmentSessionId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.PrescriptionItems
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[PrescriptionItems] (
    [PrescriptionItemId] [bigint] IDENTITY(1,1) NOT NULL,
    [PrescriptionId] [bigint] NOT NULL,
    [MedicationName] [nvarchar](200) NOT NULL,
    [Dosage] [nvarchar](100) NULL,
    [Frequency] [nvarchar](100) NULL,
    [Duration] [nvarchar](100) NULL,
    [Instructions] [nvarchar](500) NULL,

    CONSTRAINT [PK_PrescriptionItems]
        PRIMARY KEY CLUSTERED ([PrescriptionItemId] ASC),

    CONSTRAINT [FK_PrescriptionItems_Prescription]
        FOREIGN KEY ([PrescriptionId])
        REFERENCES [dbo].[Prescriptions] ([PrescriptionId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.MedicineDispensations
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[MedicineDispensations] (
    [DispensationId] [bigint] IDENTITY(1,1) NOT NULL,
    [PrescriptionId] [bigint] NOT NULL,
    [AppointmentId] [bigint] NOT NULL,
    [PatientUserId] [bigint] NOT NULL,
    [DentistUserId] [bigint] NOT NULL,
    [DispensedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_MedicineDispensations_DispensedAt] DEFAULT (sysutcdatetime()),
    [Notes] [nvarchar](500) NULL,
    [Status] [nvarchar](50) NOT NULL
        CONSTRAINT [DF_MedicineDispensations_Status] DEFAULT ('Dispensed'),
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_MedicineDispensations_CreatedAt] DEFAULT (sysutcdatetime()),

    CONSTRAINT [PK_MedicineDispensations]
        PRIMARY KEY CLUSTERED ([DispensationId] ASC),

    CONSTRAINT [FK_MedicineDispensations_Appointments]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_MedicineDispensations_Dentist]
        FOREIGN KEY ([DentistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_MedicineDispensations_Patient]
        FOREIGN KEY ([PatientUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_MedicineDispensations_Prescriptions]
        FOREIGN KEY ([PrescriptionId])
        REFERENCES [dbo].[Prescriptions] ([PrescriptionId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.MedicineDispensationItems
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[MedicineDispensationItems] (
    [DispensationItemId] [bigint] IDENTITY(1,1) NOT NULL,
    [DispensationId] [bigint] NOT NULL,
    [PrescriptionItemId] [bigint] NULL,
    [MedicineId] [bigint] NOT NULL,
    [MedicineBatchId] [bigint] NOT NULL,
    [QuantityDispensed] [int] NOT NULL,
    [UnitPriceSnapshot] [decimal](18, 2) NOT NULL,
    [TotalPrice] [decimal](18, 2) NOT NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_MedicineDispensationItems_CreatedAt] DEFAULT (sysutcdatetime()),

    CONSTRAINT [PK_MedicineDispensationItems]
        PRIMARY KEY CLUSTERED ([DispensationItemId] ASC),

    CONSTRAINT [CHK_MedicineDispensationItems_Qty]
        CHECK (([QuantityDispensed]>(0))),

    CONSTRAINT [FK_MedicineDispensationItems_Batches]
        FOREIGN KEY ([MedicineBatchId])
        REFERENCES [dbo].[MedicineBatches] ([MedicineBatchId]),

    CONSTRAINT [FK_MedicineDispensationItems_MedicineBatch_Composite]
        FOREIGN KEY ([MedicineId], [MedicineBatchId])
        REFERENCES [dbo].[MedicineBatches] ([MedicineId], [MedicineBatchId]),

    CONSTRAINT [FK_MedicineDispensationItems_Dispensation]
        FOREIGN KEY ([DispensationId])
        REFERENCES [dbo].[MedicineDispensations] ([DispensationId]) ON DELETE CASCADE,

    CONSTRAINT [FK_MedicineDispensationItems_Medicines]
        FOREIGN KEY ([MedicineId])
        REFERENCES [dbo].[Medicines] ([MedicineId]),

    CONSTRAINT [FK_MedicineDispensationItems_PrescriptionItem]
        FOREIGN KEY ([PrescriptionItemId])
        REFERENCES [dbo].[PrescriptionItems] ([PrescriptionItemId])
);
GO


-- =====================================================================================
-- 09. INVOICES, BILLING & PAYMENTS
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table: dbo.Invoices
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Invoices] (
    [InvoiceId] [bigint] IDENTITY(1,1) NOT NULL,
    [InvoiceCode] [varchar](30) NOT NULL,
    [AppointmentId] [bigint] NULL,
    [GeneratedByUserId] [bigint] NOT NULL,
    [TotalAmount] [decimal](18, 2) NOT NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Unpaid'),
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [CareEpisodeId] [bigint] NULL,

    CONSTRAINT [PK_Invoices]
        PRIMARY KEY CLUSTERED ([InvoiceId] ASC),

    UNIQUE NONCLUSTERED ([InvoiceCode] ASC),

    CHECK (([Status]='Cancelled' OR [Status]='Paid' OR [Status]='PartiallyPaid' OR [Status]='Unpaid')),

    CONSTRAINT [CK_Invoices_ParentReference]
        CHECK (([AppointmentId] IS NOT NULL OR [CareEpisodeId] IS NOT NULL)),

    CONSTRAINT [CK_Invoices_Total]
        CHECK (([TotalAmount]>=(0))),

    CONSTRAINT [FK_Invoices_Appointment]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_Invoices_CareEpisodes]
        FOREIGN KEY ([CareEpisodeId])
        REFERENCES [dbo].[CareEpisodes] ([CareEpisodeId]),

    CONSTRAINT [FK_Invoices_GeneratedByUser]
        FOREIGN KEY ([GeneratedByUserId])
        REFERENCES [dbo].[StaffProfiles] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.InvoiceItems
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[InvoiceItems] (
    [InvoiceItemId] [bigint] IDENTITY(1,1) NOT NULL,
    [InvoiceId] [bigint] NOT NULL,
    [PerformedServiceId] [bigint] NULL,
    [Description] [nvarchar](250) NOT NULL,
    [Quantity] [int] NOT NULL
        DEFAULT (1),
    [UnitPrice] [decimal](18, 2) NOT NULL,
    [Amount]  AS ([Quantity]*[UnitPrice]) PERSISTED,
    [AppointmentId] [bigint] NULL,
    [PerformingDentistUserId] [bigint] NULL,
    [ServiceTaskId] [bigint] NULL,
    [SourceType] [varchar](50) NULL,
    [SourceId] [bigint] NULL,

    CONSTRAINT [PK_InvoiceItems]
        PRIMARY KEY CLUSTERED ([InvoiceItemId] ASC),

    CONSTRAINT [CK_InvoiceItems_Quantity]
        CHECK (([Quantity]>(0))),

    CONSTRAINT [CK_InvoiceItems_UnitPrice]
        CHECK (([UnitPrice]>=(0))),

    CONSTRAINT [FK_InvoiceItems_Appointments]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_InvoiceItems_DentistUser]
        FOREIGN KEY ([PerformingDentistUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_InvoiceItems_EpisodeServiceTasks]
        FOREIGN KEY ([ServiceTaskId])
        REFERENCES [dbo].[EpisodeServiceTasks] ([ServiceTaskId]),

    CONSTRAINT [FK_InvoiceItems_Invoice]
        FOREIGN KEY ([InvoiceId])
        REFERENCES [dbo].[Invoices] ([InvoiceId]),

    CONSTRAINT [FK_InvoiceItems_PerformedService]
        FOREIGN KEY ([PerformedServiceId])
        REFERENCES [dbo].[PerformedServices] ([PerformedServiceId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.Payments
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Payments] (
    [PaymentId] [bigint] IDENTITY(1,1) NOT NULL,
    [InvoiceId] [bigint] NOT NULL,
    [PaidByPatientUserId] [bigint] NOT NULL,
    [Method] [varchar](30) NOT NULL,
    [Amount] [decimal](18, 2) NOT NULL,
    [TransactionReference] [varchar](100) NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Pending'),
    [PaidAt] [datetime2](7) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),

    CONSTRAINT [PK_Payments]
        PRIMARY KEY CLUSTERED ([PaymentId] ASC),

    CHECK (([Status]='Cancelled' OR [Status]='Failed' OR [Status]='Successful' OR [Status]='Pending')),

    CONSTRAINT [CK_Payments_Amount]
        CHECK (([Amount]>(0))),

    CONSTRAINT [CK_Payments_Method]
        CHECK (([Method]='Online' OR [Method]='CreditCard' OR [Method]='BankTransfer' OR [Method]='Cash')),

    CONSTRAINT [FK_Payments_Invoice]
        FOREIGN KEY ([InvoiceId])
        REFERENCES [dbo].[Invoices] ([InvoiceId]),

    CONSTRAINT [FK_Payments_Patient]
        FOREIGN KEY ([PaidByPatientUserId])
        REFERENCES [dbo].[PatientProfiles] ([UserId])
);
GO


-- =====================================================================================
-- 10. PATIENT CARE, REFERRALS, FEEDBACK & COMMUNICATIONS
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- Table: dbo.DepartmentReferrals
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[DepartmentReferrals] (
    [ReferralId] [bigint] IDENTITY(1,1) NOT NULL,
    [TreatmentSessionId] [bigint] NULL,
    [FromDepartmentId] [bigint] NOT NULL,
    [ToDepartmentId] [bigint] NOT NULL,
    [AssignedDentistUserId] [bigint] NULL,
    [ReviewedByUserId] [bigint] NULL,
    [Reason] [nvarchar](1000) NOT NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Pending'),
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [ReviewedAt] [datetime2](7) NULL,
    [Urgency] [varchar](20) NOT NULL
        CONSTRAINT [DF_DepartmentReferrals_Urgency] DEFAULT ('Normal'),
    [ClinicalInstructions] [nvarchar](2000) NULL,
    [SourceAppointmentId] [bigint] NULL,
    [DestinationAppointmentId] [bigint] NULL,
    [CareEpisodeId] [bigint] NULL,
    [RequestedServiceId] [bigint] NULL,

    CONSTRAINT [PK_DepartmentReferrals]
        PRIMARY KEY CLUSTERED ([ReferralId] ASC),

    CHECK (([Status]='Completed' OR [Status]='Assigned' OR [Status]='Rejected' OR [Status]='Accepted' OR [Status]='Pending')),

    CONSTRAINT [CK_DepartmentReferrals_DifferentDepartment]
        CHECK (([FromDepartmentId]<>[ToDepartmentId])),

    CONSTRAINT [CK_DepartmentReferrals_Urgency]
        CHECK (([Urgency]='Urgent' OR [Urgency]='Normal')),

    CONSTRAINT [FK_DepartmentReferrals_AssignedDentist]
        FOREIGN KEY ([AssignedDentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_DepartmentReferrals_CareEpisodes]
        FOREIGN KEY ([CareEpisodeId])
        REFERENCES [dbo].[CareEpisodes] ([CareEpisodeId]),

    CONSTRAINT [FK_DepartmentReferrals_DepartmentServices]
        FOREIGN KEY ([RequestedServiceId])
        REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId]),

    CONSTRAINT [FK_DepartmentReferrals_DestinationAppointment]
        FOREIGN KEY ([DestinationAppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_DepartmentReferrals_FromDepartment]
        FOREIGN KEY ([FromDepartmentId])
        REFERENCES [dbo].[Departments] ([DepartmentId]),

    CONSTRAINT [FK_DepartmentReferrals_ReviewedByUser]
        FOREIGN KEY ([ReviewedByUserId])
        REFERENCES [dbo].[StaffProfiles] ([UserId]),

    CONSTRAINT [FK_DepartmentReferrals_SourceAppointment]
        FOREIGN KEY ([SourceAppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_DepartmentReferrals_ToDepartment]
        FOREIGN KEY ([ToDepartmentId])
        REFERENCES [dbo].[Departments] ([DepartmentId]),

    CONSTRAINT [FK_DepartmentReferrals_TreatmentSession]
        FOREIGN KEY ([TreatmentSessionId])
        REFERENCES [dbo].[TreatmentSessions] ([TreatmentSessionId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.FollowUpSchedules
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[FollowUpSchedules] (
    [FollowUpId] [bigint] IDENTITY(1,1) NOT NULL,
    [TreatmentSessionId] [bigint] NULL,
    [PatientUserId] [bigint] NULL,
    [DentistUserId] [bigint] NULL,
    [TreatmentPlanId] [bigint] NULL,
    [TreatmentPlanStageId] [bigint] NULL,
    [FollowUpReasonCategory] [nvarchar](100) NULL,
    [RecommendedDate] [datetime2](7) NOT NULL,
    [Reason] [nvarchar](500) NULL,
    [Status] [varchar](30) NOT NULL
        DEFAULT ('Pending'),
    [AppointmentId] [bigint] NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_FollowUpSchedules_CreatedAt] DEFAULT (getutcdate()),

    CONSTRAINT [PK_FollowUpSchedules]
        PRIMARY KEY CLUSTERED ([FollowUpId] ASC),

    CONSTRAINT [CK_FollowUpSchedules_Status]
        CHECK ([Status] IN ('Pending', 'Booked', 'Overdue', 'Dismissed', 'Completed', 'Proposed', 'Confirmed', 'Scheduled', 'Cancelled')),

    CONSTRAINT [FK_FollowUpSchedules_Appointment]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_FollowUpSchedules_TreatmentSession]
        FOREIGN KEY ([TreatmentSessionId])
        REFERENCES [dbo].[TreatmentSessions] ([TreatmentSessionId]),

    CONSTRAINT [FK_FollowUpSchedules_PatientUser]
        FOREIGN KEY ([PatientUserId])
        REFERENCES [dbo].[PatientProfiles] ([UserId]),

    CONSTRAINT [FK_FollowUpSchedules_DentistUser]
        FOREIGN KEY ([DentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_FollowUpSchedules_TreatmentPlan]
        FOREIGN KEY ([TreatmentPlanId])
        REFERENCES [dbo].[TreatmentPlans] ([TreatmentPlanId]),

    CONSTRAINT [FK_FollowUpSchedules_TreatmentPlanStage]
        FOREIGN KEY ([TreatmentPlanStageId])
        REFERENCES [dbo].[TreatmentPlanItems] ([TreatmentPlanItemId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.VisitFeedback
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[VisitFeedback] (
    [FeedbackId] [bigint] IDENTITY(1,1) NOT NULL,
    [AppointmentId] [bigint] NOT NULL,
    [DentistRating] [tinyint] NOT NULL,
    [ClinicRating] [tinyint] NOT NULL,
    [Comment] [nvarchar](1000) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [DentistUserId] [bigint] NULL,
    [PatientUserId] [bigint] NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_VisitFeedback_Status] DEFAULT ('Approved'),
    [AdminNotes] [nvarchar](1000) NULL,
    [AdminRespondedByUserId] [bigint] NULL,
    [AdminResponse] [nvarchar](1000) NULL,
    [UpdatedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_VisitFeedback]
        PRIMARY KEY CLUSTERED ([FeedbackId] ASC),

    UNIQUE NONCLUSTERED ([AppointmentId] ASC),

    CHECK (([ClinicRating]>=(1) AND [ClinicRating]<=(5))),

    CHECK (([DentistRating]>=(1) AND [DentistRating]<=(5))),

    CONSTRAINT [FK_VisitFeedback_AdminRespondedBy]
        FOREIGN KEY ([AdminRespondedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_VisitFeedback_Appointment]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_VisitFeedback_DentistUser]
        FOREIGN KEY ([DentistUserId])
        REFERENCES [dbo].[DentistProfiles] ([UserId]),

    CONSTRAINT [FK_VisitFeedback_PatientUser]
        FOREIGN KEY ([PatientUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.Notifications
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[Notifications] (
    [NotificationId] [bigint] IDENTITY(1,1) NOT NULL,
    [UserId] [bigint] NOT NULL,
    [AppointmentId] [bigint] NULL,
    [Type] [varchar](50) NOT NULL,
    [Channel] [varchar](30) NOT NULL
        DEFAULT ('InApp'),
    [Subject] [nvarchar](250) NULL,
    [Content] [nvarchar](max) NOT NULL,
    [DeliveryStatus] [varchar](30) NOT NULL
        DEFAULT ('Pending'),
    [SentAt] [datetime2](7) NULL,
    [ReadAt] [datetime2](7) NULL,

    CONSTRAINT [PK_Notifications]
        PRIMARY KEY CLUSTERED ([NotificationId] ASC),

    CHECK (([Channel]='Email' OR [Channel]='InApp')),

    CONSTRAINT [CK_Notifications_DeliveryStatus]
        CHECK (([DeliveryStatus]='Read' OR [DeliveryStatus]='Failed' OR [DeliveryStatus]='Sent' OR [DeliveryStatus]='Pending')),

    CONSTRAINT [FK_Notifications_Appointment]
        FOREIGN KEY ([AppointmentId])
        REFERENCES [dbo].[Appointments] ([AppointmentId]),

    CONSTRAINT [FK_Notifications_User]
        FOREIGN KEY ([UserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.SupportChatMessages
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[SupportChatMessages] (
    [MessageId] [bigint] IDENTITY(1,1) NOT NULL,
    [ConversationId] [nvarchar](64) NOT NULL,
    [SenderType] [nvarchar](20) NOT NULL,
    [SenderUserId] [bigint] NULL,
    [SenderName] [nvarchar](100) NOT NULL,
    [MessageText] [nvarchar](2000) NOT NULL,
    [SentAt] [datetime2](7) NOT NULL
        DEFAULT (sysdatetime()),
    [IsRead] [bit] NOT NULL
        DEFAULT (0),

    CONSTRAINT [PK_SupportChatMessages]
        PRIMARY KEY CLUSTERED ([MessageId] ASC),

    CONSTRAINT [FK_SupportChatMessages_User]
        FOREIGN KEY ([SenderUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]) ON DELETE SET NULL
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.BlogCategories
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[BlogCategories] (
    [CategoryId] [int] IDENTITY(1,1) NOT NULL,
    [Name] [nvarchar](100) NOT NULL,
    [Slug] [varchar](150) NOT NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_BlogCategories_CreatedAt] DEFAULT (sysdatetime()),

    CONSTRAINT [PK_BlogCategories]
        PRIMARY KEY CLUSTERED ([CategoryId] ASC),

    CONSTRAINT [UQ_BlogCategories_Slug]
        UNIQUE NONCLUSTERED ([Slug] ASC)
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.BlogPosts
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[BlogPosts] (
    [BlogPostId] [bigint] IDENTITY(1,1) NOT NULL,
    [CategoryId] [int] NOT NULL,
    [AuthorUserId] [bigint] NOT NULL,
    [Title] [nvarchar](200) NOT NULL,
    [Slug] [varchar](250) NOT NULL,
    [Summary] [nvarchar](1000) NULL,
    [Content] [nvarchar](max) NOT NULL,
    [ThumbnailUrl] [nvarchar](500) NULL,
    [VideoUrl] [nvarchar](500) NULL,
    [Status] [varchar](30) NOT NULL
        CONSTRAINT [DF_BlogPosts_Status] DEFAULT ('Draft'),
    [IsFeatured] [bit] NOT NULL
        CONSTRAINT [DF_BlogPosts_IsFeatured] DEFAULT (0),
    [ViewCount] [int] NOT NULL
        CONSTRAINT [DF_BlogPosts_ViewCount] DEFAULT (0),
    [PublishedAt] [datetime2](7) NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_BlogPosts_CreatedAt] DEFAULT (sysdatetime()),
    [UpdatedAt] [datetime2](7) NULL,
    [EmbedVideoId]  AS (case when [VideoUrl] like '%youtube.com/embed/%' then substring([VideoUrl],charindex('/embed/',[VideoUrl])+(7),case when charindex('?',[VideoUrl],charindex('/embed/',[VideoUrl])+(7))>(0) then charindex('?',[VideoUrl],charindex('/embed/',[VideoUrl])+(7))-(charindex('/embed/',[VideoUrl])+(7)) else len([VideoUrl]) end) when [VideoUrl] like '%youtu.be/%' then substring([VideoUrl],charindex('youtu.be/',[VideoUrl])+(9),case when charindex('?',[VideoUrl],charindex('youtu.be/',[VideoUrl])+(9))>(0) then charindex('?',[VideoUrl],charindex('youtu.be/',[VideoUrl])+(9))-(charindex('youtu.be/',[VideoUrl])+(9)) else len([VideoUrl]) end) when [VideoUrl] like '%youtube.com/watch?v=%' then substring([VideoUrl],charindex('?v=',[VideoUrl])+(3),case when charindex('&',[VideoUrl],charindex('?v=',[VideoUrl])+(3))>(0) then charindex('&',[VideoUrl],charindex('?v=',[VideoUrl])+(3))-(charindex('?v=',[VideoUrl])+(3)) else len([VideoUrl]) end)  end) PERSISTED,
    [ReadTimeMinutes]  AS (case when [Content] IS NULL OR len([Content])<(250) then (1) else (len([Content])-len(replace([Content],' ','')))/(250)+(1) end) PERSISTED,
    [PostType] [varchar](30) NOT NULL
        CONSTRAINT [DF_BlogPosts_PostType] DEFAULT ('Blog'),
    [RejectionReason] [nvarchar](1000) NULL,
    [ApprovedByUserId] [bigint] NULL,
    [ApprovedAt] [datetime2](7) NULL,

    CONSTRAINT [PK_BlogPosts]
        PRIMARY KEY CLUSTERED ([BlogPostId] ASC),

    CONSTRAINT [UQ_BlogPosts_Slug]
        UNIQUE NONCLUSTERED ([Slug] ASC),

    CONSTRAINT [CK_BlogPosts_PostType]
        CHECK (([PostType]='Announcement' OR [PostType]='ClinicNews' OR [PostType]='Blog')),

    CONSTRAINT [CK_BlogPosts_Status]
        CHECK (([Status]='Rejected' OR [Status]='Archived' OR [Status]='Published' OR [Status]='PendingReview' OR [Status]='Draft')),

    CONSTRAINT [FK_BlogPosts_ApprovedByUser]
        FOREIGN KEY ([ApprovedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]),

    CONSTRAINT [FK_BlogPosts_BlogCategories]
        FOREIGN KEY ([CategoryId])
        REFERENCES [dbo].[BlogCategories] ([CategoryId]),

    CONSTRAINT [FK_BlogPosts_UserAccounts]
        FOREIGN KEY ([AuthorUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId]) ON DELETE CASCADE
);
GO

-- -------------------------------------------------------------------------------------
-- Table: dbo.BlogPostMedia
-- -------------------------------------------------------------------------------------
CREATE TABLE [dbo].[BlogPostMedia] (
    [MediaId] [bigint] IDENTITY(1,1) NOT NULL,
    [BlogPostId] [bigint] NULL,
    [UploadedByUserId] [bigint] NOT NULL,
    [MediaType] [varchar](20) NOT NULL,
    [FileUrl] [nvarchar](500) NOT NULL,
    [PublicId] [nvarchar](200) NOT NULL,
    [ThumbnailUrl] [nvarchar](500) NULL,
    [ContentType] [varchar](100) NOT NULL,
    [SizeBytes] [bigint] NOT NULL,
    [DurationSeconds] [int] NULL,
    [CreatedAt] [datetime2](7) NOT NULL
        CONSTRAINT [DF_BlogPostMedia_CreatedAt] DEFAULT (sysdatetime()),

    CONSTRAINT [PK_BlogPostMedia]
        PRIMARY KEY CLUSTERED ([MediaId] ASC),

    CONSTRAINT [CK_BlogPostMedia_Type]
        CHECK (([MediaType]='Video' OR [MediaType]='Image')),

    CONSTRAINT [FK_BlogPostMedia_BlogPosts]
        FOREIGN KEY ([BlogPostId])
        REFERENCES [dbo].[BlogPosts] ([BlogPostId]) ON DELETE SET NULL,

    CONSTRAINT [FK_BlogPostMedia_UserAccounts]
        FOREIGN KEY ([UploadedByUserId])
        REFERENCES [dbo].[UserAccounts] ([UserId])
);
GO


-- =====================================================================================
-- 11. CIRCULAR FOREIGN KEY CONSTRAINTS (ARCHITECTURAL EXCEPTION)
-- Note: Appointments and CareEpisodes have a mutual cyclic foreign key dependency:
--   - CareEpisodes.RootAppointmentId -> Appointments.AppointmentId (Inlined in CREATE TABLE)
--   - Appointments.CareEpisodeId     -> CareEpisodes.CareEpisodeId (Resolved here via ALTER TABLE)
-- =====================================================================================

ALTER TABLE [dbo].[Appointments] WITH CHECK ADD CONSTRAINT [FK_Appointments_CareEpisodes]
    FOREIGN KEY ([CareEpisodeId]) REFERENCES [dbo].[CareEpisodes] ([CareEpisodeId]);
GO

ALTER TABLE [dbo].[Appointments] WITH CHECK ADD CONSTRAINT [FK_Appointments_CareEpisode_Patient_Composite]
    FOREIGN KEY ([PatientUserId], [CareEpisodeId]) REFERENCES [dbo].[CareEpisodes] ([PatientUserId], [CareEpisodeId]);
GO


-- =====================================================================================
-- 12. PERFORMANCE & FILTERED INDEXES
-- =====================================================================================

-- Billing Integrity P0 Filtered Unique Indexes
CREATE UNIQUE NONCLUSTERED INDEX [UQ_Invoices_CareEpisodeId]
    ON [dbo].[Invoices] ([CareEpisodeId] ASC)
    WHERE [CareEpisodeId] IS NOT NULL;
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_Invoices_AppointmentId]
    ON [dbo].[Invoices] ([AppointmentId] ASC)
    WHERE [AppointmentId] IS NOT NULL AND [CareEpisodeId] IS NULL;
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_ServiceTaskId]
    ON [dbo].[InvoiceItems] ([ServiceTaskId] ASC)
    WHERE [ServiceTaskId] IS NOT NULL;
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_PerformedServiceId]
    ON [dbo].[InvoiceItems] ([PerformedServiceId] ASC)
    WHERE [PerformedServiceId] IS NOT NULL;
GO

CREATE NONCLUSTERED INDEX [IX_AppointmentChangeProposals_Appointment_Status]
    ON [dbo].[AppointmentChangeProposals] ([AppointmentId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AppointmentDisruptions_AppointmentId]
    ON [dbo].[AppointmentDisruptions] ([AppointmentId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AppointmentDisruptions_LeaveRequestId]
    ON [dbo].[AppointmentDisruptions] ([DentistLeaveRequestId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AppointmentDisruptions_Status]
    ON [dbo].[AppointmentDisruptions] ([Status] ASC);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_AppointmentDisruptions_ActionToken]
    ON [dbo].[AppointmentDisruptions] ([ActionToken] ASC)
    WHERE ([ActionToken] IS NOT NULL);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_AppointmentDisruptions_Appt_Leave]
    ON [dbo].[AppointmentDisruptions] ([AppointmentId] ASC, [DentistLeaveRequestId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AppointmentReminderJobs_Status_ScheduledTime]
    ON [dbo].[AppointmentReminderJobs] ([Status] ASC, [ScheduledSendTime] ASC);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_AppointmentReminderJobs_Appt_Type_Time]
    ON [dbo].[AppointmentReminderJobs] ([AppointmentId] ASC, [ReminderType] ASC, [ScheduledSendTime] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_Appointments_AssignedDentist_Start]
    ON [dbo].[Appointments] ([AssignedDentistUserId] ASC, [ScheduledStart] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_Appointments_CareEpisodeId]
    ON [dbo].[Appointments] ([CareEpisodeId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_Appointments_Chair_Start]
    ON [dbo].[Appointments] ([DentalChairId] ASC, [ScheduledStart] ASC)
    WHERE ([DentalChairId] IS NOT NULL);
GO

CREATE NONCLUSTERED INDEX [IX_Appointments_Department_Start_Status]
    ON [dbo].[Appointments] ([DepartmentId] ASC, [ScheduledStart] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_Appointments_Patient_Status]
    ON [dbo].[Appointments] ([PatientUserId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_Appointments_Room_Start]
    ON [dbo].[Appointments] ([RoomId] ASC, [ScheduledStart] ASC)
    WHERE ([RoomId] IS NOT NULL);
GO

CREATE NONCLUSTERED INDEX [IX_Appointments_TreatmentPlanItemId]
    ON [dbo].[Appointments] ([TreatmentPlanItemId] ASC)
    WHERE ([TreatmentPlanItemId] IS NOT NULL);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_Appointments_Department_Date_QueueNumber]
    ON [dbo].[Appointments] ([DepartmentId] ASC, [ScheduledDate] ASC, [QueueNumber] ASC)
    WHERE ([DepartmentId] IS NOT NULL AND [ScheduledStart] IS NOT NULL AND [QueueNumber] IS NOT NULL AND [QueueNumber]<>'');
GO

CREATE NONCLUSTERED INDEX [IX_AppointmentStatusHistories_Appointment_ChangedAt]
    ON [dbo].[AppointmentStatusHistories] ([AppointmentId] ASC, [ChangedAt] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AuditLogs_User_CreatedAt]
    ON [dbo].[AuditLogs] ([UserId] ASC, [CreatedAt] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_BlogPostMedia_BlogPostId]
    ON [dbo].[BlogPostMedia] ([BlogPostId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_BlogPostMedia_CreatedAt]
    ON [dbo].[BlogPostMedia] ([CreatedAt] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_BlogPostMedia_UploadedByUserId]
    ON [dbo].[BlogPostMedia] ([UploadedByUserId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_BlogPosts_AuthorUserId]
    ON [dbo].[BlogPosts] ([AuthorUserId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_BlogPosts_CategoryId]
    ON [dbo].[BlogPosts] ([CategoryId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_BlogPosts_IsFeatured]
    ON [dbo].[BlogPosts] ([IsFeatured] ASC, [Status] ASC, [PublishedAt] DESC);
GO

CREATE NONCLUSTERED INDEX [IX_BlogPosts_Status_PublishedAt]
    ON [dbo].[BlogPosts] ([Status] ASC, [PublishedAt] DESC);
GO

CREATE NONCLUSTERED INDEX [IX_BlogPosts_Title_Summary]
    ON [dbo].[BlogPosts] ([Status] ASC, [PublishedAt] DESC);
GO

CREATE NONCLUSTERED INDEX [IX_BlogPosts_ViewCount]
    ON [dbo].[BlogPosts] ([Status] ASC, [ViewCount] DESC);
GO

CREATE NONCLUSTERED INDEX [IX_CareEpisodes_Patient_Status]
    ON [dbo].[CareEpisodes] ([PatientUserId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_CareEpisodes_RootAppointment]
    ON [dbo].[CareEpisodes] ([RootAppointmentId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ClinicalConsultations_ConsultedDentist]
    ON [dbo].[ClinicalConsultations] ([ConsultedDentistUserId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ClinicalConsultations_RequestingDentist]
    ON [dbo].[ClinicalConsultations] ([RequestingDentistUserId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ClinicalConsultations_SourceAppt]
    ON [dbo].[ClinicalConsultations] ([SourceAppointmentId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ClinicalEntries_Record_CreatedAt]
    ON [dbo].[ClinicalEntries] ([MedicalRecordId] ASC, [CreatedAt] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_DentistAvailability_Dentist_Time]
    ON [dbo].[DentistAvailability] ([DentistUserId] ASC, [StartDateTime] ASC, [EndDateTime] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_DentistDepartments_Department_Status]
    ON [dbo].[DentistDepartments] ([DepartmentId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_DentistLeaveRequests_Dentist_Status_Dates]
    ON [dbo].[DentistLeaveRequests] ([DentistUserId] ASC, [Status] ASC, [StartDate] ASC, [EndDate] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_DentistLeaveRequests_Status]
    ON [dbo].[DentistLeaveRequests] ([Status] ASC);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_DentistProfiles_LicenseNumber_NotNull]
    ON [dbo].[DentistProfiles] ([LicenseNumber] ASC)
    WHERE ([LicenseNumber] IS NOT NULL);
GO

CREATE NONCLUSTERED INDEX [IX_DentistWorkSchedules_Dentist_Date]
    ON [dbo].[DentistWorkSchedules] ([DentistUserId] ASC, [WorkDate] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_DepartmentReferrals_CareEpisodeId]
    ON [dbo].[DepartmentReferrals] ([CareEpisodeId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_DepartmentReferrals_ToDepartment_Status]
    ON [dbo].[DepartmentReferrals] ([ToDepartmentId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticOrders_AppointmentId]
    ON [dbo].[DiagnosticOrders] ([AppointmentId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticOrders_Status]
    ON [dbo].[DiagnosticOrders] ([Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticResults_Order]
    ON [dbo].[DiagnosticResults] ([DiagnosticOrderId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_EpisodeServiceTasks_AppointmentId]
    ON [dbo].[EpisodeServiceTasks] ([AppointmentId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_EpisodeServiceTasks_Episode_Status]
    ON [dbo].[EpisodeServiceTasks] ([CareEpisodeId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_FollowUpSchedules_Status_Date]
    ON [dbo].[FollowUpSchedules] ([Status] ASC, [RecommendedDate] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ImagingSessions_Equipment_Interval]
    ON [dbo].[ImagingSessions] ([EquipmentId] ASC, [ScheduledStartUtc] ASC, [ScheduledEndUtc] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ImagingSessions_Order]
    ON [dbo].[ImagingSessions] ([DiagnosticOrderId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ImagingSessions_Room_Status]
    ON [dbo].[ImagingSessions] ([ImagingRoomId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_InvoiceItems_Invoice]
    ON [dbo].[InvoiceItems] ([InvoiceId] ASC);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_Invoice_Source]
    ON [dbo].[InvoiceItems] ([InvoiceId] ASC, [SourceType] ASC, [SourceId] ASC)
    WHERE ([SourceType] IS NOT NULL AND [SourceId] IS NOT NULL);
GO

CREATE NONCLUSTERED INDEX [IX_Invoices_CareEpisodeId]
    ON [dbo].[Invoices] ([CareEpisodeId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_MedicineBatches_MedicineId_Expiry]
    ON [dbo].[MedicineBatches] ([MedicineId] ASC, [ExpiryDate] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_MedicineBatches_Status_Available]
    ON [dbo].[MedicineBatches] ([Status] ASC, [QuantityAvailable] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_MedicineDispensationItems_Dispensation]
    ON [dbo].[MedicineDispensationItems] ([DispensationId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_MedicineDispensations_Appt_Prescription]
    ON [dbo].[MedicineDispensations] ([AppointmentId] ASC, [PrescriptionId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_MedicineStockTransactions_BatchId]
    ON [dbo].[MedicineStockTransactions] ([MedicineBatchId] ASC, [CreatedAt] DESC);
GO

CREATE NONCLUSTERED INDEX [IX_Notifications_User_ReadAt]
    ON [dbo].[Notifications] ([UserId] ASC, [ReadAt] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_Payments_Invoice_Status]
    ON [dbo].[Payments] ([InvoiceId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_PerformedServices_Session]
    ON [dbo].[PerformedServices] ([TreatmentSessionId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ReceptionistCoverageIncidents_WorkDate_Status]
    ON [dbo].[ReceptionistCoverageIncidents] ([WorkDate] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ReceptionistLeaveRequestPeriods_Date_Status]
    ON [dbo].[ReceptionistLeaveRequestPeriods] ([LeaveDate] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ReceptionistLeaveRequests_Dates_Status]
    ON [dbo].[ReceptionistLeaveRequests] ([StartDate] ASC, [EndDate] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ReceptionistLeaveRequests_User_Status]
    ON [dbo].[ReceptionistLeaveRequests] ([ReceptionistUserId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ReceptionistShiftSwapRequests_RequestingUser]
    ON [dbo].[ReceptionistShiftSwapRequests] ([RequestingReceptionistUserId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ReceptionistShiftSwapRequests_TargetUser]
    ON [dbo].[ReceptionistShiftSwapRequests] ([TargetReceptionistUserId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ReceptionistWorkHandovers_FromUser]
    ON [dbo].[ReceptionistWorkHandovers] ([FromReceptionistUserId] ASC, [WorkDate] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ReceptionistWorkSchedules_User_WorkDate]
    ON [dbo].[ReceptionistWorkSchedules] ([ReceptionistUserId] ASC, [WorkDate] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ReceptionistWorkSchedules_WorkDate_Status]
    ON [dbo].[ReceptionistWorkSchedules] ([WorkDate] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_StaffAttendanceCorrection_Status_CreatedAt]
    ON [dbo].[StaffAttendanceCorrectionRequests] ([Status] ASC, [CreatedAtUtc] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_StaffAttendanceCorrection_UserId_WorkDate]
    ON [dbo].[StaffAttendanceCorrectionRequests] ([UserId] ASC, [WorkDate] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_StaffAttendanceEvents_AttendanceId_EventTime]
    ON [dbo].[StaffAttendanceEvents] ([AttendanceId] ASC, [EventTimeUtc] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_StaffAttendanceRecords_IsFinalized_WorkDate]
    ON [dbo].[StaffAttendanceRecords] ([IsFinalized] ASC, [WorkDate] ASC);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_StaffAttendanceRecords_User_Date_Shift]
    ON [dbo].[StaffAttendanceRecords] ([UserId] ASC, [WorkDate] ASC, [ShiftIdentifier] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_StaffAttendanceRecords_WorkDate_Role_Status]
    ON [dbo].[StaffAttendanceRecords] ([WorkDate] ASC, [Role] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_SupportChatMessages_Conversation_SentAt]
    ON [dbo].[SupportChatMessages] ([ConversationId] ASC, [SentAt] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_TreatmentPlans_Patient_Status]
    ON [dbo].[TreatmentPlans] ([PatientUserId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_TreatmentSessions_Appointment]
    ON [dbo].[TreatmentSessions] ([AppointmentId] ASC);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_UserAccounts_Email_NotNull]
    ON [dbo].[UserAccounts] ([Email] ASC)
    WHERE ([Email] IS NOT NULL);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_UserAccounts_Phone_NotNull]
    ON [dbo].[UserAccounts] ([PhoneNumber] ASC)
    WHERE ([PhoneNumber] IS NOT NULL);
GO

CREATE NONCLUSTERED INDEX [IX_VisitFeedback_Dentist_Status]
    ON [dbo].[VisitFeedback] ([DentistUserId] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_XrayDentistRosters_Dentist_Date]
    ON [dbo].[XrayDentistRosters] ([DentistUserId] ASC, [WorkDate] ASC, [Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_XrayDentistRosters_Room_Date]
    ON [dbo].[XrayDentistRosters] ([RoomId] ASC, [WorkDate] ASC, [Status] ASC);
GO


-- =====================================================================================
-- 13. DATABASE VIEWS
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
    bc.Name      AS CategoryName,
    bc.Slug      AS CategorySlug,
    ua.UserId    AS AuthorUserId,
    COALESCE(sp.FullName, ua.Email) AS AuthorName,
    ua.AvatarUrl AS AuthorAvatarUrl,
    dp.Qualification,
    dp.YearsOfExperience
FROM dbo.BlogPosts bp
INNER JOIN dbo.BlogCategories bc ON bc.CategoryId = bp.CategoryId
INNER JOIN dbo.UserAccounts   ua ON ua.UserId     = bp.AuthorUserId
LEFT  JOIN dbo.StaffProfiles  sp ON sp.UserId     = bp.AuthorUserId
LEFT  JOIN dbo.DentistProfiles dp ON dp.UserId    = bp.AuthorUserId
WHERE bp.Status = ''Published''');
GO


-- =====================================================================================
-- 14. STORED PROCEDURES
-- =====================================================================================

CREATE PROCEDURE dbo.sp_CleanupOrphanBlogPostMedia
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.BlogPostMedia
    WHERE BlogPostId IS NULL
      AND CreatedAt < DATEADD(HOUR, -24, SYSDATETIME());
END;
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

PRINT N'>>> DENTALCARE FULL CLEAN STANDALONE DATABASE SCHEMA INITIALIZED SUCCESSFULLY (69 TABLES, 164 FKS, VIEWS & PROCEDURES)!';
GO

SET NOEXEC OFF;
GO
