-- =========================================================================================
-- DENTAL CLINIC MANAGEMENT SYSTEM (SWP391 GROUP 6)
-- MASTER CLEAN DATABASE SCHEMA (44 TABLES, VIEWS, STORED PROCEDURES)
-- Target: Microsoft SQL Server 2017 / 2019 / 2022 / LocalDB / SQLEXPRESS
-- Database Name: DentalClinicManagementDB
-- Generated: 2026-11-03 02:08:47
-- =========================================================================================

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

-- =========================================================================================
-- PART 1: 44 DDL TABLE SCHEMAS, CONSTRAINTS & INDEXES
-- =========================================================================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[ClinicProfile](
	[ClinicId] [int] NOT NULL,
	[Name] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Address] [nvarchar](300) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PhoneNumber] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Email] [varchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Description] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[OpeningTime] [time](7) NULL,
	[ClosingTime] [time](7) NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_ClinicProfile] PRIMARY KEY CLUSTERED 
(
	[ClinicId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[UserAccounts](
	[UserId] [bigint] IDENTITY(1,1) NOT NULL,
	[Email] [varchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[PhoneNumber] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[PasswordHash] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Role] [varchar](40) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EmailVerifiedAt] [datetime2](7) NULL,
	[AvatarUrl] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[LastLoginAt] [datetime2](7) NULL,
	[FailedLoginCount] [int] NOT NULL,
	[LockoutEnd] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[PatientProfiles](
	[UserId] [bigint] NOT NULL,
	[PatientCode] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[FullName] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[DateOfBirth] [date] NULL,
	[Gender] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[NationalId] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[HealthInsuranceNumber] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Address] [nvarchar](300) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[EmergencyContact] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_PatientProfiles] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[PatientCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[StaffProfiles](
	[UserId] [bigint] NOT NULL,
	[EmployeeCode] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[FullName] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EmploymentStatus] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_StaffProfiles] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[EmployeeCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[DentistProfiles](
	[UserId] [bigint] NOT NULL,
	[LicenseNumber] [varchar](80) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Qualification] [nvarchar](300) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[YearsOfExperience] [int] NULL,
	[Biography] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
 CONSTRAINT [PK_DentistProfiles] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[Departments](
	[DepartmentId] [bigint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Description] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ManagerUserId] [bigint] NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[DepartmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[DentistDepartments](
	[DentistDepartmentId] [bigint] IDENTITY(1,1) NOT NULL,
	[DentistUserId] [bigint] NOT NULL,
	[DepartmentId] [bigint] NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AssignedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[DentistDepartmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_DentistDepartments] UNIQUE NONCLUSTERED 
(
	[DentistUserId] ASC,
	[DepartmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DepartmentWorkSchedules](
	[DepartmentScheduleId] [bigint] IDENTITY(1,1) NOT NULL,
	[DepartmentId] [bigint] NOT NULL,
	[DayOfWeek] [tinyint] NOT NULL,
	[StartTime] [time](7) NOT NULL,
	[EndTime] [time](7) NOT NULL,
	[IsActive] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[DepartmentScheduleId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_DepartmentWorkSchedules] UNIQUE NONCLUSTERED 
(
	[DepartmentId] ASC,
	[DayOfWeek] ASC,
	[StartTime] ASC,
	[EndTime] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[DentistWorkSchedules](
	[DentistScheduleId] [bigint] IDENTITY(1,1) NOT NULL,
	[DentistUserId] [bigint] NOT NULL,
	[DepartmentId] [bigint] NOT NULL,
	[WorkDate] [date] NOT NULL,
	[StartTime] [time](7) NOT NULL,
	[EndTime] [time](7) NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Note] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
	[DentistScheduleId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_DentistWorkSchedules] UNIQUE NONCLUSTERED 
(
	[DentistUserId] ASC,
	[DepartmentId] ASC,
	[WorkDate] ASC,
	[StartTime] ASC,
	[EndTime] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[DentistAvailability](
	[AvailabilityId] [bigint] IDENTITY(1,1) NOT NULL,
	[DentistUserId] [bigint] NOT NULL,
	[StartDateTime] [datetime2](7) NOT NULL,
	[EndDateTime] [datetime2](7) NULL,
	[AvailabilityStatus] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Reason] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[AvailabilityId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[Rooms](
	[RoomId] [bigint] IDENTITY(1,1) NOT NULL,
	[RoomCode] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RoomName] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[DepartmentId] [bigint] NULL,
	[IsExclusive] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[RoomId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[RoomCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[DentalChairs](
	[DentalChairId] [bigint] IDENTITY(1,1) NOT NULL,
	[RoomId] [bigint] NOT NULL,
	[ChairCode] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[DentalChairId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_DentalChairs] UNIQUE NONCLUSTERED 
(
	[RoomId] ASC,
	[ChairCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[DepartmentServices](
	[DepartmentServiceId] [bigint] IDENTITY(1,1) NOT NULL,
	[DepartmentId] [bigint] NOT NULL,
	[ServiceName] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Description] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[DurationMinutes] [int] NULL,
	[Price] [decimal](18, 2) NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[DepartmentServiceId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_DepartmentServices] UNIQUE NONCLUSTERED 
(
	[DepartmentId] ASC,
	[ServiceName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[Appointments](
	[AppointmentId] [bigint] IDENTITY(1,1) NOT NULL,
	[AppointmentCode] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PatientUserId] [bigint] NOT NULL,
	[DepartmentId] [bigint] NULL,
	[RequestedServiceId] [bigint] NULL,
	[RequestedDentistUserId] [bigint] NULL,
	[AssignedDentistUserId] [bigint] NULL,
	[RoomId] [bigint] NULL,
	[DentalChairId] [bigint] NULL,
	[ScheduledStart] [datetime2](7) NULL,
	[ScheduledEnd] [datetime2](7) NULL,
	[ReasonForVisit] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[BookingSource] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[QueueNumber] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Status] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedByUserId] [bigint] NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[ConfirmedAt] [datetime2](7) NULL,
	[CheckedInAt] [datetime2](7) NULL,
	[CompletedAt] [datetime2](7) NULL,
	[CancelledAt] [datetime2](7) NULL,
	[CancellationReason] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[ScheduledDate]  AS (CONVERT([date],[ScheduledStart])) PERSISTED,
PRIMARY KEY CLUSTERED 
(
	[AppointmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[AppointmentCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[AppointmentChangeProposals](
	[ProposalId] [bigint] IDENTITY(1,1) NOT NULL,
	[AppointmentId] [bigint] NOT NULL,
	[ProposedByUserId] [bigint] NOT NULL,
	[ProposedDepartmentId] [bigint] NULL,
	[ProposedDentistUserId] [bigint] NULL,
	[ProposedStart] [datetime2](7) NULL,
	[ProposedEnd] [datetime2](7) NULL,
	[Reason] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[RespondedAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[ProposalId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[AppointmentStatusHistories](
	[HistoryId] [bigint] IDENTITY(1,1) NOT NULL,
	[AppointmentId] [bigint] NOT NULL,
	[OldStatus] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[NewStatus] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ChangedByUserId] [bigint] NULL,
	[Note] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ChangedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[HistoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[AppointmentReminderJobs](
	[ReminderJobId] [bigint] IDENTITY(1,1) NOT NULL,
	[AppointmentId] [bigint] NOT NULL,
	[ReminderType] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ScheduledSendTime] [datetime2](7) NOT NULL,
	[RecipientEmail] [varchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SentAt] [datetime2](7) NULL,
	[ErrorMessage] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[RetryCount] [int] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[ReminderJobId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[Notifications](
	[NotificationId] [bigint] IDENTITY(1,1) NOT NULL,
	[UserId] [bigint] NOT NULL,
	[AppointmentId] [bigint] NULL,
	[Type] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Channel] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Subject] [nvarchar](250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Content] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[DeliveryStatus] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SentAt] [datetime2](7) NULL,
	[ReadAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[NotificationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[SupportChatMessages](
	[MessageId] [bigint] IDENTITY(1,1) NOT NULL,
	[ConversationId] [varchar](64) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SenderType] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SenderUserId] [bigint] NULL,
	[SenderName] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[MessageText] [nvarchar](2000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SentAt] [datetime2](7) NOT NULL,
	[IsRead] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[MessageId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DentalMedicalRecords](
	[MedicalRecordId] [bigint] IDENTITY(1,1) NOT NULL,
	[PatientUserId] [bigint] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[MedicalRecordId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[PatientUserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ClinicalEntries](
	[ClinicalEntryId] [bigint] IDENTITY(1,1) NOT NULL,
	[MedicalRecordId] [bigint] NOT NULL,
	[AppointmentId] [bigint] NULL,
	[DentistUserId] [bigint] NOT NULL,
	[Symptoms] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ClinicalFindings] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Diagnosis] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[DentalConditions] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ClinicalNotes] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[ClinicalEntryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[OdontogramEntries](
	[OdontogramEntryId] [bigint] IDENTITY(1,1) NOT NULL,
	[ClinicalEntryId] [bigint] NOT NULL,
	[ToothNumber] [varchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Surface] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Condition] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Note] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
	[OdontogramEntryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[ImagingRecords](
	[ImagingRecordId] [bigint] IDENTITY(1,1) NOT NULL,
	[ClinicalEntryId] [bigint] NOT NULL,
	[ImagingType] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[FileUrl] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ResultSummary] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[ImagingRecordId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[TreatmentPlans](
	[TreatmentPlanId] [bigint] IDENTITY(1,1) NOT NULL,
	[PatientUserId] [bigint] NOT NULL,
	[DepartmentId] [bigint] NOT NULL,
	[CreatedByDentistUserId] [bigint] NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EstimatedTotal] [decimal](18, 2) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[TreatmentPlanId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[TreatmentPlanItems](
	[TreatmentPlanItemId] [bigint] IDENTITY(1,1) NOT NULL,
	[TreatmentPlanId] [bigint] NOT NULL,
	[DepartmentServiceId] [bigint] NULL,
	[AssignedDentistUserId] [bigint] NULL,
	[ToothNumber] [varchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[SequenceNo] [int] NOT NULL,
	[ExpectedDate] [date] NULL,
	[EstimatedCost] [decimal](18, 2) NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Note] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
	[TreatmentPlanItemId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_TreatmentPlanItems_Sequence] UNIQUE NONCLUSTERED 
(
	[TreatmentPlanId] ASC,
	[SequenceNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TreatmentSessions](
	[TreatmentSessionId] [bigint] IDENTITY(1,1) NOT NULL,
	[TreatmentPlanItemId] [bigint] NOT NULL,
	[AppointmentId] [bigint] NOT NULL,
	[DentistUserId] [bigint] NOT NULL,
	[SessionDate] [datetime2](7) NOT NULL,
	[TreatmentResult] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ClinicalNote] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
	[TreatmentSessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[PerformedServices](
	[PerformedServiceId] [bigint] IDENTITY(1,1) NOT NULL,
	[TreatmentSessionId] [bigint] NOT NULL,
	[DepartmentServiceId] [bigint] NOT NULL,
	[Quantity] [int] NOT NULL,
	[UnitPrice] [decimal](18, 2) NOT NULL,
	[Amount]  AS ([Quantity]*[UnitPrice]) PERSISTED,
PRIMARY KEY CLUSTERED 
(
	[PerformedServiceId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Prescriptions](
	[PrescriptionId] [bigint] IDENTITY(1,1) NOT NULL,
	[TreatmentSessionId] [bigint] NOT NULL,
	[GeneralInstructions] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[IssuedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[PrescriptionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[TreatmentSessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PrescriptionItems](
	[PrescriptionItemId] [bigint] IDENTITY(1,1) NOT NULL,
	[PrescriptionId] [bigint] NOT NULL,
	[MedicationName] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Dosage] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Frequency] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Duration] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Instructions] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
	[PrescriptionItemId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[DepartmentReferrals](
	[ReferralId] [bigint] IDENTITY(1,1) NOT NULL,
	[TreatmentSessionId] [bigint] NOT NULL,
	[FromDepartmentId] [bigint] NOT NULL,
	[ToDepartmentId] [bigint] NOT NULL,
	[AssignedDentistUserId] [bigint] NULL,
	[ReviewedByUserId] [bigint] NULL,
	[Reason] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[ReviewedAt] [datetime2](7) NULL,
	[Urgency] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[ReferralId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[FollowUpSchedules](
	[FollowUpId] [bigint] IDENTITY(1,1) NOT NULL,
	[TreatmentSessionId] [bigint] NOT NULL,
	[RecommendedDate] [datetime2](7) NOT NULL,
	[Reason] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AppointmentId] [bigint] NULL,
PRIMARY KEY CLUSTERED 
(
	[FollowUpId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[Invoices](
	[InvoiceId] [bigint] IDENTITY(1,1) NOT NULL,
	[InvoiceCode] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AppointmentId] [bigint] NOT NULL,
	[GeneratedByUserId] [bigint] NOT NULL,
	[TotalAmount] [decimal](18, 2) NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[InvoiceId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[InvoiceCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[AppointmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[InvoiceItems](
	[InvoiceItemId] [bigint] IDENTITY(1,1) NOT NULL,
	[InvoiceId] [bigint] NOT NULL,
	[PerformedServiceId] [bigint] NULL,
	[Description] [nvarchar](250) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Quantity] [int] NOT NULL,
	[UnitPrice] [decimal](18, 2) NOT NULL,
	[Amount]  AS ([Quantity]*[UnitPrice]) PERSISTED,
PRIMARY KEY CLUSTERED 
(
	[InvoiceItemId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[Payments](
	[PaymentId] [bigint] IDENTITY(1,1) NOT NULL,
	[InvoiceId] [bigint] NOT NULL,
	[PaidByPatientUserId] [bigint] NOT NULL,
	[Method] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Amount] [decimal](18, 2) NOT NULL,
	[TransactionReference] [varchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PaidAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[PaymentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[VisitFeedback](
	[FeedbackId] [bigint] IDENTITY(1,1) NOT NULL,
	[AppointmentId] [bigint] NOT NULL,
	[DentistRating] [tinyint] NOT NULL,
	[ClinicRating] [tinyint] NOT NULL,
	[Comment] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[FeedbackId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[AppointmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[AuditLogs](
	[AuditLogId] [bigint] IDENTITY(1,1) NOT NULL,
	[UserId] [bigint] NULL,
	[Action] [varchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EntityName] [varchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EntityId] [varchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[OldValues] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[NewValues] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[AuditLogId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[BlogCategories](
	[CategoryId] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Slug] [varchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_BlogCategories] PRIMARY KEY CLUSTERED 
(
	[CategoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_BlogCategories_Slug] UNIQUE NONCLUSTERED 
(
	[Slug] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[BlogPosts](
	[BlogPostId] [bigint] IDENTITY(1,1) NOT NULL,
	[CategoryId] [int] NOT NULL,
	[AuthorUserId] [bigint] NOT NULL,
	[Title] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Slug] [varchar](250) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Summary] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Content] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ThumbnailUrl] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[VideoUrl] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[IsFeatured] [bit] NOT NULL,
	[ViewCount] [int] NOT NULL,
	[PublishedAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[EmbedVideoId]  AS (case when [VideoUrl] like '%youtube.com/embed/%' then substring([VideoUrl],charindex('/embed/',[VideoUrl])+(7),case when charindex('?',[VideoUrl],charindex('/embed/',[VideoUrl])+(7))>(0) then charindex('?',[VideoUrl],charindex('/embed/',[VideoUrl])+(7))-(charindex('/embed/',[VideoUrl])+(7)) else len([VideoUrl]) end) when [VideoUrl] like '%youtu.be/%' then substring([VideoUrl],charindex('youtu.be/',[VideoUrl])+(9),case when charindex('?',[VideoUrl],charindex('youtu.be/',[VideoUrl])+(9))>(0) then charindex('?',[VideoUrl],charindex('youtu.be/',[VideoUrl])+(9))-(charindex('youtu.be/',[VideoUrl])+(9)) else len([VideoUrl]) end) when [VideoUrl] like '%youtube.com/watch?v=%' then substring([VideoUrl],charindex('?v=',[VideoUrl])+(3),case when charindex('&',[VideoUrl],charindex('?v=',[VideoUrl])+(3))>(0) then charindex('&',[VideoUrl],charindex('?v=',[VideoUrl])+(3))-(charindex('?v=',[VideoUrl])+(3)) else len([VideoUrl]) end)  end) PERSISTED,
	[ReadTimeMinutes]  AS (case when [Content] IS NULL OR len([Content])<(250) then (1) else (len([Content])-len(replace([Content],' ','')))/(250)+(1) end) PERSISTED,
 CONSTRAINT [PK_BlogPosts] PRIMARY KEY CLUSTERED 
(
	[BlogPostId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_BlogPosts_Slug] UNIQUE NONCLUSTERED 
(
	[Slug] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[BlogPostMedia](
	[MediaId] [bigint] IDENTITY(1,1) NOT NULL,
	[BlogPostId] [bigint] NULL,
	[UploadedByUserId] [bigint] NOT NULL,
	[MediaType] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[FileUrl] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PublicId] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ThumbnailUrl] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ContentType] [varchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SizeBytes] [bigint] NOT NULL,
	[DurationSeconds] [int] NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_BlogPostMedia] PRIMARY KEY CLUSTERED 
(
	[MediaId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Medicines](
	[MedicineId] [bigint] IDENTITY(1,1) NOT NULL,
	[MedicineCode] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[MedicineName] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ActiveIngredient] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Concentration] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[DosageForm] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Unit] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[UnitPrice] [decimal](18, 2) NOT NULL,
	[MinimumStockAlert] [int] NOT NULL,
	[UsageInstructions] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Status] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_Medicines] PRIMARY KEY CLUSTERED 
(
	[MedicineId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Medicines_MedicineCode] UNIQUE NONCLUSTERED 
(
	[MedicineCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MedicineBatches](
	[MedicineBatchId] [bigint] IDENTITY(1,1) NOT NULL,
	[MedicineId] [bigint] NOT NULL,
	[BatchNumber] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ExpiryDate] [date] NOT NULL,
	[QuantityReceived] [int] NOT NULL,
	[QuantityAvailable] [int] NOT NULL,
	[CostPrice] [decimal](18, 2) NOT NULL,
	[Status] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ReceivedDate] [datetime2](7) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_MedicineBatches] PRIMARY KEY CLUSTERED 
(
	[MedicineBatchId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_MedicineBatches_Medicine_Batch] UNIQUE NONCLUSTERED 
(
	[MedicineId] ASC,
	[BatchNumber] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MedicineStockTransactions](
	[TransactionId] [bigint] IDENTITY(1,1) NOT NULL,
	[MedicineBatchId] [bigint] NOT NULL,
	[TransactionType] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Quantity] [int] NOT NULL,
	[QuantityBefore] [int] NOT NULL,
	[QuantityAfter] [int] NOT NULL,
	[Reason] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PerformedByUserId] [bigint] NOT NULL,
	[ReferenceCode] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_MedicineStockTransactions] PRIMARY KEY CLUSTERED 
(
	[TransactionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MedicineDispensations](
	[DispensationId] [bigint] IDENTITY(1,1) NOT NULL,
	[PrescriptionId] [bigint] NOT NULL,
	[AppointmentId] [bigint] NOT NULL,
	[PatientUserId] [bigint] NOT NULL,
	[DentistUserId] [bigint] NOT NULL,
	[DispensedAt] [datetime2](7) NOT NULL,
	[Notes] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Status] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_MedicineDispensations] PRIMARY KEY CLUSTERED 
(
	[DispensationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MedicineDispensationItems](
	[DispensationItemId] [bigint] IDENTITY(1,1) NOT NULL,
	[DispensationId] [bigint] NOT NULL,
	[PrescriptionItemId] [bigint] NULL,
	[MedicineId] [bigint] NOT NULL,
	[MedicineBatchId] [bigint] NOT NULL,
	[QuantityDispensed] [int] NOT NULL,
	[UnitPriceSnapshot] [decimal](18, 2) NOT NULL,
	[TotalPrice] [decimal](18, 2) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_MedicineDispensationItems] PRIMARY KEY CLUSTERED 
(
	[DispensationItemId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_UserAccounts_Email_NotNull] ON [dbo].[UserAccounts]
(
	[Email] ASC
)
WHERE ([Email] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_UserAccounts_Phone_NotNull] ON [dbo].[UserAccounts]
(
	[PhoneNumber] ASC
)
WHERE ([PhoneNumber] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_DentistProfiles_LicenseNumber_NotNull] ON [dbo].[DentistProfiles]
(
	[LicenseNumber] ASC
)
WHERE ([LicenseNumber] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_DentistDepartments_Department_Status] ON [dbo].[DentistDepartments]
(
	[DepartmentId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_DentistWorkSchedules_Dentist_Date] ON [dbo].[DentistWorkSchedules]
(
	[DentistUserId] ASC,
	[WorkDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_DentistAvailability_Dentist_Time] ON [dbo].[DentistAvailability]
(
	[DentistUserId] ASC,
	[StartDateTime] ASC,
	[EndDateTime] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Appointments_AssignedDentist_Start] ON [dbo].[Appointments]
(
	[AssignedDentistUserId] ASC,
	[ScheduledStart] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Appointments_Chair_Start] ON [dbo].[Appointments]
(
	[DentalChairId] ASC,
	[ScheduledStart] ASC
)
WHERE ([DentalChairId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_Appointments_Department_Start_Status] ON [dbo].[Appointments]
(
	[DepartmentId] ASC,
	[ScheduledStart] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_Appointments_Patient_Status] ON [dbo].[Appointments]
(
	[PatientUserId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Appointments_Room_Start] ON [dbo].[Appointments]
(
	[RoomId] ASC,
	[ScheduledStart] ASC
)
WHERE ([RoomId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ARITHABORT ON
SET CONCAT_NULL_YIELDS_NULL ON
SET QUOTED_IDENTIFIER ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
SET NUMERIC_ROUNDABORT OFF

GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_Appointments_Department_Date_QueueNumber] ON [dbo].[Appointments]
(
	[DepartmentId] ASC,
	[ScheduledDate] ASC,
	[QueueNumber] ASC
)
WHERE ([DepartmentId] IS NOT NULL AND [ScheduledStart] IS NOT NULL AND [QueueNumber] IS NOT NULL AND [QueueNumber]<>'')
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AppointmentChangeProposals_Appointment_Status] ON [dbo].[AppointmentChangeProposals]
(
	[AppointmentId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_AppointmentStatusHistories_Appointment_ChangedAt] ON [dbo].[AppointmentStatusHistories]
(
	[AppointmentId] ASC,
	[ChangedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AppointmentReminderJobs_Status_ScheduledTime] ON [dbo].[AppointmentReminderJobs]
(
	[Status] ASC,
	[ScheduledSendTime] ASC
)
INCLUDE([AppointmentId],[ReminderType],[RecipientEmail],[RetryCount]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_AppointmentReminderJobs_Appt_Type_Time] ON [dbo].[AppointmentReminderJobs]
(
	[AppointmentId] ASC,
	[ReminderType] ASC,
	[ScheduledSendTime] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Notifications_User_ReadAt] ON [dbo].[Notifications]
(
	[UserId] ASC,
	[ReadAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_SupportChatMessages_Conv] ON [dbo].[SupportChatMessages]
(
	[ConversationId] ASC,
	[SentAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_ClinicalEntries_Record_CreatedAt] ON [dbo].[ClinicalEntries]
(
	[MedicalRecordId] ASC,
	[CreatedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_TreatmentPlans_Patient_Status] ON [dbo].[TreatmentPlans]
(
	[PatientUserId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_TreatmentSessions_Appointment] ON [dbo].[TreatmentSessions]
(
	[AppointmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_PerformedServices_Session] ON [dbo].[PerformedServices]
(
	[TreatmentSessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_DepartmentReferrals_ToDepartment_Status] ON [dbo].[DepartmentReferrals]
(
	[ToDepartmentId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_FollowUpSchedules_Status_Date] ON [dbo].[FollowUpSchedules]
(
	[Status] ASC,
	[RecommendedDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_InvoiceItems_Invoice] ON [dbo].[InvoiceItems]
(
	[InvoiceId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_Payments_Invoice_Status] ON [dbo].[Payments]
(
	[InvoiceId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_AuditLogs_User_CreatedAt] ON [dbo].[AuditLogs]
(
	[UserId] ASC,
	[CreatedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_BlogPosts_AuthorUserId] ON [dbo].[BlogPosts]
(
	[AuthorUserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_BlogPosts_CategoryId] ON [dbo].[BlogPosts]
(
	[CategoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_BlogPosts_IsFeatured] ON [dbo].[BlogPosts]
(
	[IsFeatured] ASC,
	[Status] ASC,
	[PublishedAt] DESC
)
INCLUDE([Title],[Slug],[ThumbnailUrl],[CategoryId],[AuthorUserId],[ViewCount],[Summary]) 
WHERE ([IsFeatured]=(1))
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_BlogPosts_Status_PublishedAt] ON [dbo].[BlogPosts]
(
	[Status] ASC,
	[PublishedAt] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_BlogPosts_Title_Summary] ON [dbo].[BlogPosts]
(
	[Status] ASC,
	[PublishedAt] DESC
)
INCLUDE([Title],[Slug],[ThumbnailUrl],[CategoryId],[AuthorUserId],[ViewCount],[Summary],[IsFeatured]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_BlogPosts_ViewCount] ON [dbo].[BlogPosts]
(
	[Status] ASC,
	[ViewCount] DESC
)
INCLUDE([Title],[Slug],[ThumbnailUrl],[PublishedAt],[CategoryId]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_BlogPostMedia_BlogPostId] ON [dbo].[BlogPostMedia]
(
	[BlogPostId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_BlogPostMedia_CreatedAt] ON [dbo].[BlogPostMedia]
(
	[CreatedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_BlogPostMedia_UploadedByUserId] ON [dbo].[BlogPostMedia]
(
	[UploadedByUserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_MedicineBatches_MedicineId_Expiry] ON [dbo].[MedicineBatches]
(
	[MedicineId] ASC,
	[ExpiryDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_MedicineBatches_Status_Available] ON [dbo].[MedicineBatches]
(
	[Status] ASC,
	[QuantityAvailable] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_MedicineStockTransactions_BatchId] ON [dbo].[MedicineStockTransactions]
(
	[MedicineBatchId] ASC,
	[CreatedAt] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_MedicineDispensations_Appt_Prescription] ON [dbo].[MedicineDispensations]
(
	[AppointmentId] ASC,
	[PrescriptionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_MedicineDispensationItems_Dispensation] ON [dbo].[MedicineDispensationItems]
(
	[DispensationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[ClinicProfile] ADD  DEFAULT (sysdatetime()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[UserAccounts] ADD  DEFAULT ('Unverified') FOR [Status]
GO
ALTER TABLE [dbo].[UserAccounts] ADD  DEFAULT ((0)) FOR [FailedLoginCount]
GO
ALTER TABLE [dbo].[UserAccounts] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[PatientProfiles] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[StaffProfiles] ADD  DEFAULT ('Active') FOR [EmploymentStatus]
GO
ALTER TABLE [dbo].[StaffProfiles] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Departments] ADD  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[Departments] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[DentistDepartments] ADD  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[DentistDepartments] ADD  DEFAULT (sysdatetime()) FOR [AssignedAt]
GO
ALTER TABLE [dbo].[DepartmentWorkSchedules] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[DentistWorkSchedules] ADD  DEFAULT ('Scheduled') FOR [Status]
GO
ALTER TABLE [dbo].[DentistAvailability] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Rooms] ADD  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[Rooms] ADD  CONSTRAINT [DF_Rooms_IsExclusive]  DEFAULT ((0)) FOR [IsExclusive]
GO
ALTER TABLE [dbo].[DentalChairs] ADD  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[DepartmentServices] ADD  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[Appointments] ADD  DEFAULT ('PendingReceptionReview') FOR [Status]
GO
ALTER TABLE [dbo].[Appointments] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[AppointmentChangeProposals] ADD  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[AppointmentChangeProposals] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[AppointmentStatusHistories] ADD  DEFAULT (sysdatetime()) FOR [ChangedAt]
GO
ALTER TABLE [dbo].[AppointmentReminderJobs] ADD  DEFAULT ('Scheduled') FOR [Status]
GO
ALTER TABLE [dbo].[AppointmentReminderJobs] ADD  DEFAULT ((0)) FOR [RetryCount]
GO
ALTER TABLE [dbo].[AppointmentReminderJobs] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Notifications] ADD  DEFAULT ('InApp') FOR [Channel]
GO
ALTER TABLE [dbo].[Notifications] ADD  DEFAULT ('Pending') FOR [DeliveryStatus]
GO
ALTER TABLE [dbo].[SupportChatMessages] ADD  DEFAULT (sysdatetime()) FOR [SentAt]
GO
ALTER TABLE [dbo].[SupportChatMessages] ADD  DEFAULT ((0)) FOR [IsRead]
GO
ALTER TABLE [dbo].[DentalMedicalRecords] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ClinicalEntries] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ImagingRecords] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[TreatmentPlans] ADD  DEFAULT ('Planned') FOR [Status]
GO
ALTER TABLE [dbo].[TreatmentPlans] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[TreatmentPlanItems] ADD  DEFAULT ('Planned') FOR [Status]
GO
ALTER TABLE [dbo].[TreatmentSessions] ADD  DEFAULT (sysdatetime()) FOR [SessionDate]
GO
ALTER TABLE [dbo].[PerformedServices] ADD  DEFAULT ((1)) FOR [Quantity]
GO
ALTER TABLE [dbo].[Prescriptions] ADD  DEFAULT (sysdatetime()) FOR [IssuedAt]
GO
ALTER TABLE [dbo].[DepartmentReferrals] ADD  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[DepartmentReferrals] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[DepartmentReferrals] ADD  CONSTRAINT [DF_DepartmentReferrals_Urgency]  DEFAULT ('Normal') FOR [Urgency]
GO
ALTER TABLE [dbo].[FollowUpSchedules] ADD  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[Invoices] ADD  DEFAULT ('Unpaid') FOR [Status]
GO
ALTER TABLE [dbo].[Invoices] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[InvoiceItems] ADD  DEFAULT ((1)) FOR [Quantity]
GO
ALTER TABLE [dbo].[Payments] ADD  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[Payments] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[VisitFeedback] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[AuditLogs] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[BlogCategories] ADD  CONSTRAINT [DF_BlogCategories_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[BlogPosts] ADD  CONSTRAINT [DF_BlogPosts_Status]  DEFAULT ('Draft') FOR [Status]
GO
ALTER TABLE [dbo].[BlogPosts] ADD  CONSTRAINT [DF_BlogPosts_IsFeatured]  DEFAULT ((0)) FOR [IsFeatured]
GO
ALTER TABLE [dbo].[BlogPosts] ADD  CONSTRAINT [DF_BlogPosts_ViewCount]  DEFAULT ((0)) FOR [ViewCount]
GO
ALTER TABLE [dbo].[BlogPosts] ADD  CONSTRAINT [DF_BlogPosts_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[BlogPostMedia] ADD  CONSTRAINT [DF_BlogPostMedia_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Medicines] ADD  CONSTRAINT [DF_Medicines_MinAlert]  DEFAULT ((20)) FOR [MinimumStockAlert]
GO
ALTER TABLE [dbo].[Medicines] ADD  CONSTRAINT [DF_Medicines_Status]  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[Medicines] ADD  CONSTRAINT [DF_Medicines_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[MedicineBatches] ADD  CONSTRAINT [DF_MedicineBatches_CostPrice]  DEFAULT ((0)) FOR [CostPrice]
GO
ALTER TABLE [dbo].[MedicineBatches] ADD  CONSTRAINT [DF_MedicineBatches_Status]  DEFAULT ('Available') FOR [Status]
GO
ALTER TABLE [dbo].[MedicineBatches] ADD  CONSTRAINT [DF_MedicineBatches_ReceivedDate]  DEFAULT (sysutcdatetime()) FOR [ReceivedDate]
GO
ALTER TABLE [dbo].[MedicineBatches] ADD  CONSTRAINT [DF_MedicineBatches_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[MedicineStockTransactions] ADD  CONSTRAINT [DF_MedicineStockTransactions_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[MedicineDispensations] ADD  CONSTRAINT [DF_MedicineDispensations_DispensedAt]  DEFAULT (sysutcdatetime()) FOR [DispensedAt]
GO
ALTER TABLE [dbo].[MedicineDispensations] ADD  CONSTRAINT [DF_MedicineDispensations_Status]  DEFAULT ('Dispensed') FOR [Status]
GO
ALTER TABLE [dbo].[MedicineDispensations] ADD  CONSTRAINT [DF_MedicineDispensations_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[MedicineDispensationItems] ADD  CONSTRAINT [DF_MedicineDispensationItems_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[PatientProfiles]  WITH CHECK ADD  CONSTRAINT [FK_PatientProfiles_UserAccounts] FOREIGN KEY([UserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[PatientProfiles] CHECK CONSTRAINT [FK_PatientProfiles_UserAccounts]
GO
ALTER TABLE [dbo].[StaffProfiles]  WITH CHECK ADD  CONSTRAINT [FK_StaffProfiles_UserAccounts] FOREIGN KEY([UserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[StaffProfiles] CHECK CONSTRAINT [FK_StaffProfiles_UserAccounts]
GO
ALTER TABLE [dbo].[DentistProfiles]  WITH CHECK ADD  CONSTRAINT [FK_DentistProfiles_StaffProfiles] FOREIGN KEY([UserId])
REFERENCES [dbo].[StaffProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DentistProfiles] CHECK CONSTRAINT [FK_DentistProfiles_StaffProfiles]
GO
ALTER TABLE [dbo].[Departments]  WITH CHECK ADD  CONSTRAINT [FK_Departments_ManagerUser] FOREIGN KEY([ManagerUserId])
REFERENCES [dbo].[StaffProfiles] ([UserId])
GO
ALTER TABLE [dbo].[Departments] CHECK CONSTRAINT [FK_Departments_ManagerUser]
GO
ALTER TABLE [dbo].[DentistDepartments]  WITH CHECK ADD  CONSTRAINT [FK_DentistDepartments_Dentist] FOREIGN KEY([DentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DentistDepartments] CHECK CONSTRAINT [FK_DentistDepartments_Dentist]
GO
ALTER TABLE [dbo].[DentistDepartments]  WITH CHECK ADD  CONSTRAINT [FK_DentistDepartments_Department] FOREIGN KEY([DepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[DentistDepartments] CHECK CONSTRAINT [FK_DentistDepartments_Department]
GO
ALTER TABLE [dbo].[DepartmentWorkSchedules]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentWorkSchedules_Department] FOREIGN KEY([DepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[DepartmentWorkSchedules] CHECK CONSTRAINT [FK_DepartmentWorkSchedules_Department]
GO
ALTER TABLE [dbo].[DentistWorkSchedules]  WITH CHECK ADD  CONSTRAINT [FK_DentistWorkSchedules_Dentist] FOREIGN KEY([DentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DentistWorkSchedules] CHECK CONSTRAINT [FK_DentistWorkSchedules_Dentist]
GO
ALTER TABLE [dbo].[DentistWorkSchedules]  WITH CHECK ADD  CONSTRAINT [FK_DentistWorkSchedules_Department] FOREIGN KEY([DepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[DentistWorkSchedules] CHECK CONSTRAINT [FK_DentistWorkSchedules_Department]
GO
ALTER TABLE [dbo].[DentistAvailability]  WITH CHECK ADD  CONSTRAINT [FK_DentistAvailability_Dentist] FOREIGN KEY([DentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DentistAvailability] CHECK CONSTRAINT [FK_DentistAvailability_Dentist]
GO
ALTER TABLE [dbo].[Rooms]  WITH CHECK ADD  CONSTRAINT [FK_Rooms_Department] FOREIGN KEY([DepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[Rooms] CHECK CONSTRAINT [FK_Rooms_Department]
GO
ALTER TABLE [dbo].[DentalChairs]  WITH CHECK ADD  CONSTRAINT [FK_DentalChairs_Room] FOREIGN KEY([RoomId])
REFERENCES [dbo].[Rooms] ([RoomId])
GO
ALTER TABLE [dbo].[DentalChairs] CHECK CONSTRAINT [FK_DentalChairs_Room]
GO
ALTER TABLE [dbo].[DepartmentServices]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentServices_Department] FOREIGN KEY([DepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[DepartmentServices] CHECK CONSTRAINT [FK_DepartmentServices_Department]
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [FK_Appointments_AssignedDentist] FOREIGN KEY([AssignedDentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [FK_Appointments_AssignedDentist]
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [FK_Appointments_Chair] FOREIGN KEY([DentalChairId])
REFERENCES [dbo].[DentalChairs] ([DentalChairId])
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [FK_Appointments_Chair]
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [FK_Appointments_CreatedBy] FOREIGN KEY([CreatedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [FK_Appointments_CreatedBy]
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [FK_Appointments_Department] FOREIGN KEY([DepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [FK_Appointments_Department]
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [FK_Appointments_Patient] FOREIGN KEY([PatientUserId])
REFERENCES [dbo].[PatientProfiles] ([UserId])
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [FK_Appointments_Patient]
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [FK_Appointments_RequestedDentist] FOREIGN KEY([RequestedDentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [FK_Appointments_RequestedDentist]
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [FK_Appointments_Room] FOREIGN KEY([RoomId])
REFERENCES [dbo].[Rooms] ([RoomId])
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [FK_Appointments_Room]
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [FK_Appointments_Service] FOREIGN KEY([RequestedServiceId])
REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId])
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [FK_Appointments_Service]
GO
ALTER TABLE [dbo].[AppointmentChangeProposals]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentChangeProposals_Appointment] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[AppointmentChangeProposals] CHECK CONSTRAINT [FK_AppointmentChangeProposals_Appointment]
GO
ALTER TABLE [dbo].[AppointmentChangeProposals]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentChangeProposals_Dentist] FOREIGN KEY([ProposedDentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[AppointmentChangeProposals] CHECK CONSTRAINT [FK_AppointmentChangeProposals_Dentist]
GO
ALTER TABLE [dbo].[AppointmentChangeProposals]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentChangeProposals_Department] FOREIGN KEY([ProposedDepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[AppointmentChangeProposals] CHECK CONSTRAINT [FK_AppointmentChangeProposals_Department]
GO
ALTER TABLE [dbo].[AppointmentChangeProposals]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentChangeProposals_User] FOREIGN KEY([ProposedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[AppointmentChangeProposals] CHECK CONSTRAINT [FK_AppointmentChangeProposals_User]
GO
ALTER TABLE [dbo].[AppointmentStatusHistories]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentStatusHistories_Appointment] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[AppointmentStatusHistories] CHECK CONSTRAINT [FK_AppointmentStatusHistories_Appointment]
GO
ALTER TABLE [dbo].[AppointmentStatusHistories]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentStatusHistories_User] FOREIGN KEY([ChangedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[AppointmentStatusHistories] CHECK CONSTRAINT [FK_AppointmentStatusHistories_User]
GO
ALTER TABLE [dbo].[AppointmentReminderJobs]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentReminderJobs_Appointments] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AppointmentReminderJobs] CHECK CONSTRAINT [FK_AppointmentReminderJobs_Appointments]
GO
ALTER TABLE [dbo].[Notifications]  WITH CHECK ADD  CONSTRAINT [FK_Notifications_Appointment] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[Notifications] CHECK CONSTRAINT [FK_Notifications_Appointment]
GO
ALTER TABLE [dbo].[Notifications]  WITH CHECK ADD  CONSTRAINT [FK_Notifications_User] FOREIGN KEY([UserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[Notifications] CHECK CONSTRAINT [FK_Notifications_User]
GO
ALTER TABLE [dbo].[SupportChatMessages]  WITH CHECK ADD  CONSTRAINT [FK_SupportChatMessages_User] FOREIGN KEY([SenderUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[SupportChatMessages] CHECK CONSTRAINT [FK_SupportChatMessages_User]
GO
ALTER TABLE [dbo].[DentalMedicalRecords]  WITH CHECK ADD  CONSTRAINT [FK_DentalMedicalRecords_Patient] FOREIGN KEY([PatientUserId])
REFERENCES [dbo].[PatientProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DentalMedicalRecords] CHECK CONSTRAINT [FK_DentalMedicalRecords_Patient]
GO
ALTER TABLE [dbo].[ClinicalEntries]  WITH CHECK ADD  CONSTRAINT [FK_ClinicalEntries_Appointment] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[ClinicalEntries] CHECK CONSTRAINT [FK_ClinicalEntries_Appointment]
GO
ALTER TABLE [dbo].[ClinicalEntries]  WITH CHECK ADD  CONSTRAINT [FK_ClinicalEntries_Dentist] FOREIGN KEY([DentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[ClinicalEntries] CHECK CONSTRAINT [FK_ClinicalEntries_Dentist]
GO
ALTER TABLE [dbo].[ClinicalEntries]  WITH CHECK ADD  CONSTRAINT [FK_ClinicalEntries_Record] FOREIGN KEY([MedicalRecordId])
REFERENCES [dbo].[DentalMedicalRecords] ([MedicalRecordId])
GO
ALTER TABLE [dbo].[ClinicalEntries] CHECK CONSTRAINT [FK_ClinicalEntries_Record]
GO
ALTER TABLE [dbo].[OdontogramEntries]  WITH CHECK ADD  CONSTRAINT [FK_OdontogramEntries_ClinicalEntry] FOREIGN KEY([ClinicalEntryId])
REFERENCES [dbo].[ClinicalEntries] ([ClinicalEntryId])
GO
ALTER TABLE [dbo].[OdontogramEntries] CHECK CONSTRAINT [FK_OdontogramEntries_ClinicalEntry]
GO
ALTER TABLE [dbo].[ImagingRecords]  WITH CHECK ADD  CONSTRAINT [FK_ImagingRecords_ClinicalEntry] FOREIGN KEY([ClinicalEntryId])
REFERENCES [dbo].[ClinicalEntries] ([ClinicalEntryId])
GO
ALTER TABLE [dbo].[ImagingRecords] CHECK CONSTRAINT [FK_ImagingRecords_ClinicalEntry]
GO
ALTER TABLE [dbo].[TreatmentPlans]  WITH CHECK ADD  CONSTRAINT [FK_TreatmentPlans_Dentist] FOREIGN KEY([CreatedByDentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[TreatmentPlans] CHECK CONSTRAINT [FK_TreatmentPlans_Dentist]
GO
ALTER TABLE [dbo].[TreatmentPlans]  WITH CHECK ADD  CONSTRAINT [FK_TreatmentPlans_Department] FOREIGN KEY([DepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[TreatmentPlans] CHECK CONSTRAINT [FK_TreatmentPlans_Department]
GO
ALTER TABLE [dbo].[TreatmentPlans]  WITH CHECK ADD  CONSTRAINT [FK_TreatmentPlans_Patient] FOREIGN KEY([PatientUserId])
REFERENCES [dbo].[PatientProfiles] ([UserId])
GO
ALTER TABLE [dbo].[TreatmentPlans] CHECK CONSTRAINT [FK_TreatmentPlans_Patient]
GO
ALTER TABLE [dbo].[TreatmentPlanItems]  WITH CHECK ADD  CONSTRAINT [FK_TreatmentPlanItems_Dentist] FOREIGN KEY([AssignedDentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[TreatmentPlanItems] CHECK CONSTRAINT [FK_TreatmentPlanItems_Dentist]
GO
ALTER TABLE [dbo].[TreatmentPlanItems]  WITH CHECK ADD  CONSTRAINT [FK_TreatmentPlanItems_Plan] FOREIGN KEY([TreatmentPlanId])
REFERENCES [dbo].[TreatmentPlans] ([TreatmentPlanId])
GO
ALTER TABLE [dbo].[TreatmentPlanItems] CHECK CONSTRAINT [FK_TreatmentPlanItems_Plan]
GO
ALTER TABLE [dbo].[TreatmentPlanItems]  WITH CHECK ADD  CONSTRAINT [FK_TreatmentPlanItems_Service] FOREIGN KEY([DepartmentServiceId])
REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId])
GO
ALTER TABLE [dbo].[TreatmentPlanItems] CHECK CONSTRAINT [FK_TreatmentPlanItems_Service]
GO
ALTER TABLE [dbo].[TreatmentSessions]  WITH CHECK ADD  CONSTRAINT [FK_TreatmentSessions_Appointment] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[TreatmentSessions] CHECK CONSTRAINT [FK_TreatmentSessions_Appointment]
GO
ALTER TABLE [dbo].[TreatmentSessions]  WITH CHECK ADD  CONSTRAINT [FK_TreatmentSessions_Dentist] FOREIGN KEY([DentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[TreatmentSessions] CHECK CONSTRAINT [FK_TreatmentSessions_Dentist]
GO
ALTER TABLE [dbo].[TreatmentSessions]  WITH CHECK ADD  CONSTRAINT [FK_TreatmentSessions_Item] FOREIGN KEY([TreatmentPlanItemId])
REFERENCES [dbo].[TreatmentPlanItems] ([TreatmentPlanItemId])
GO
ALTER TABLE [dbo].[TreatmentSessions] CHECK CONSTRAINT [FK_TreatmentSessions_Item]
GO
ALTER TABLE [dbo].[PerformedServices]  WITH CHECK ADD  CONSTRAINT [FK_PerformedServices_Service] FOREIGN KEY([DepartmentServiceId])
REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId])
GO
ALTER TABLE [dbo].[PerformedServices] CHECK CONSTRAINT [FK_PerformedServices_Service]
GO
ALTER TABLE [dbo].[PerformedServices]  WITH CHECK ADD  CONSTRAINT [FK_PerformedServices_Session] FOREIGN KEY([TreatmentSessionId])
REFERENCES [dbo].[TreatmentSessions] ([TreatmentSessionId])
GO
ALTER TABLE [dbo].[PerformedServices] CHECK CONSTRAINT [FK_PerformedServices_Session]
GO
ALTER TABLE [dbo].[Prescriptions]  WITH CHECK ADD  CONSTRAINT [FK_Prescriptions_TreatmentSession] FOREIGN KEY([TreatmentSessionId])
REFERENCES [dbo].[TreatmentSessions] ([TreatmentSessionId])
GO
ALTER TABLE [dbo].[Prescriptions] CHECK CONSTRAINT [FK_Prescriptions_TreatmentSession]
GO
ALTER TABLE [dbo].[PrescriptionItems]  WITH CHECK ADD  CONSTRAINT [FK_PrescriptionItems_Prescription] FOREIGN KEY([PrescriptionId])
REFERENCES [dbo].[Prescriptions] ([PrescriptionId])
GO
ALTER TABLE [dbo].[PrescriptionItems] CHECK CONSTRAINT [FK_PrescriptionItems_Prescription]
GO
ALTER TABLE [dbo].[DepartmentReferrals]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentReferrals_AssignedDentist] FOREIGN KEY([AssignedDentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DepartmentReferrals] CHECK CONSTRAINT [FK_DepartmentReferrals_AssignedDentist]
GO
ALTER TABLE [dbo].[DepartmentReferrals]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentReferrals_FromDepartment] FOREIGN KEY([FromDepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[DepartmentReferrals] CHECK CONSTRAINT [FK_DepartmentReferrals_FromDepartment]
GO
ALTER TABLE [dbo].[DepartmentReferrals]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentReferrals_ReviewedByUser] FOREIGN KEY([ReviewedByUserId])
REFERENCES [dbo].[StaffProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DepartmentReferrals] CHECK CONSTRAINT [FK_DepartmentReferrals_ReviewedByUser]
GO
ALTER TABLE [dbo].[DepartmentReferrals]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentReferrals_ToDepartment] FOREIGN KEY([ToDepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[DepartmentReferrals] CHECK CONSTRAINT [FK_DepartmentReferrals_ToDepartment]
GO
ALTER TABLE [dbo].[DepartmentReferrals]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentReferrals_TreatmentSession] FOREIGN KEY([TreatmentSessionId])
REFERENCES [dbo].[TreatmentSessions] ([TreatmentSessionId])
GO
ALTER TABLE [dbo].[DepartmentReferrals] CHECK CONSTRAINT [FK_DepartmentReferrals_TreatmentSession]
GO
ALTER TABLE [dbo].[FollowUpSchedules]  WITH CHECK ADD  CONSTRAINT [FK_FollowUpSchedules_Appointment] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[FollowUpSchedules] CHECK CONSTRAINT [FK_FollowUpSchedules_Appointment]
GO
ALTER TABLE [dbo].[FollowUpSchedules]  WITH CHECK ADD  CONSTRAINT [FK_FollowUpSchedules_TreatmentSession] FOREIGN KEY([TreatmentSessionId])
REFERENCES [dbo].[TreatmentSessions] ([TreatmentSessionId])
GO
ALTER TABLE [dbo].[FollowUpSchedules] CHECK CONSTRAINT [FK_FollowUpSchedules_TreatmentSession]
GO
ALTER TABLE [dbo].[Invoices]  WITH CHECK ADD  CONSTRAINT [FK_Invoices_Appointment] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[Invoices] CHECK CONSTRAINT [FK_Invoices_Appointment]
GO
ALTER TABLE [dbo].[Invoices]  WITH CHECK ADD  CONSTRAINT [FK_Invoices_GeneratedByUser] FOREIGN KEY([GeneratedByUserId])
REFERENCES [dbo].[StaffProfiles] ([UserId])
GO
ALTER TABLE [dbo].[Invoices] CHECK CONSTRAINT [FK_Invoices_GeneratedByUser]
GO
ALTER TABLE [dbo].[InvoiceItems]  WITH CHECK ADD  CONSTRAINT [FK_InvoiceItems_Invoice] FOREIGN KEY([InvoiceId])
REFERENCES [dbo].[Invoices] ([InvoiceId])
GO
ALTER TABLE [dbo].[InvoiceItems] CHECK CONSTRAINT [FK_InvoiceItems_Invoice]
GO
ALTER TABLE [dbo].[InvoiceItems]  WITH CHECK ADD  CONSTRAINT [FK_InvoiceItems_PerformedService] FOREIGN KEY([PerformedServiceId])
REFERENCES [dbo].[PerformedServices] ([PerformedServiceId])
GO
ALTER TABLE [dbo].[InvoiceItems] CHECK CONSTRAINT [FK_InvoiceItems_PerformedService]
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD  CONSTRAINT [FK_Payments_Invoice] FOREIGN KEY([InvoiceId])
REFERENCES [dbo].[Invoices] ([InvoiceId])
GO
ALTER TABLE [dbo].[Payments] CHECK CONSTRAINT [FK_Payments_Invoice]
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD  CONSTRAINT [FK_Payments_Patient] FOREIGN KEY([PaidByPatientUserId])
REFERENCES [dbo].[PatientProfiles] ([UserId])
GO
ALTER TABLE [dbo].[Payments] CHECK CONSTRAINT [FK_Payments_Patient]
GO
ALTER TABLE [dbo].[VisitFeedback]  WITH CHECK ADD  CONSTRAINT [FK_VisitFeedback_Appointment] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[VisitFeedback] CHECK CONSTRAINT [FK_VisitFeedback_Appointment]
GO
ALTER TABLE [dbo].[AuditLogs]  WITH CHECK ADD  CONSTRAINT [FK_AuditLogs_User] FOREIGN KEY([UserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[AuditLogs] CHECK CONSTRAINT [FK_AuditLogs_User]
GO
ALTER TABLE [dbo].[BlogPosts]  WITH CHECK ADD  CONSTRAINT [FK_BlogPosts_BlogCategories] FOREIGN KEY([CategoryId])
REFERENCES [dbo].[BlogCategories] ([CategoryId])
GO
ALTER TABLE [dbo].[BlogPosts] CHECK CONSTRAINT [FK_BlogPosts_BlogCategories]
GO
ALTER TABLE [dbo].[BlogPosts]  WITH CHECK ADD  CONSTRAINT [FK_BlogPosts_UserAccounts] FOREIGN KEY([AuthorUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[BlogPosts] CHECK CONSTRAINT [FK_BlogPosts_UserAccounts]
GO
ALTER TABLE [dbo].[BlogPostMedia]  WITH CHECK ADD  CONSTRAINT [FK_BlogPostMedia_BlogPosts] FOREIGN KEY([BlogPostId])
REFERENCES [dbo].[BlogPosts] ([BlogPostId])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[BlogPostMedia] CHECK CONSTRAINT [FK_BlogPostMedia_BlogPosts]
GO
ALTER TABLE [dbo].[BlogPostMedia]  WITH CHECK ADD  CONSTRAINT [FK_BlogPostMedia_UserAccounts] FOREIGN KEY([UploadedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[BlogPostMedia] CHECK CONSTRAINT [FK_BlogPostMedia_UserAccounts]
GO
ALTER TABLE [dbo].[MedicineBatches]  WITH CHECK ADD  CONSTRAINT [FK_MedicineBatches_Medicines] FOREIGN KEY([MedicineId])
REFERENCES [dbo].[Medicines] ([MedicineId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[MedicineBatches] CHECK CONSTRAINT [FK_MedicineBatches_Medicines]
GO
ALTER TABLE [dbo].[MedicineStockTransactions]  WITH CHECK ADD  CONSTRAINT [FK_MedicineStockTransactions_Batches] FOREIGN KEY([MedicineBatchId])
REFERENCES [dbo].[MedicineBatches] ([MedicineBatchId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[MedicineStockTransactions] CHECK CONSTRAINT [FK_MedicineStockTransactions_Batches]
GO
ALTER TABLE [dbo].[MedicineStockTransactions]  WITH CHECK ADD  CONSTRAINT [FK_MedicineStockTransactions_Users] FOREIGN KEY([PerformedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[MedicineStockTransactions] CHECK CONSTRAINT [FK_MedicineStockTransactions_Users]
GO
ALTER TABLE [dbo].[MedicineDispensations]  WITH CHECK ADD  CONSTRAINT [FK_MedicineDispensations_Appointments] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[MedicineDispensations] CHECK CONSTRAINT [FK_MedicineDispensations_Appointments]
GO
ALTER TABLE [dbo].[MedicineDispensations]  WITH CHECK ADD  CONSTRAINT [FK_MedicineDispensations_Dentist] FOREIGN KEY([DentistUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[MedicineDispensations] CHECK CONSTRAINT [FK_MedicineDispensations_Dentist]
GO
ALTER TABLE [dbo].[MedicineDispensations]  WITH CHECK ADD  CONSTRAINT [FK_MedicineDispensations_Patient] FOREIGN KEY([PatientUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[MedicineDispensations] CHECK CONSTRAINT [FK_MedicineDispensations_Patient]
GO
ALTER TABLE [dbo].[MedicineDispensations]  WITH CHECK ADD  CONSTRAINT [FK_MedicineDispensations_Prescriptions] FOREIGN KEY([PrescriptionId])
REFERENCES [dbo].[Prescriptions] ([PrescriptionId])
GO
ALTER TABLE [dbo].[MedicineDispensations] CHECK CONSTRAINT [FK_MedicineDispensations_Prescriptions]
GO
ALTER TABLE [dbo].[MedicineDispensationItems]  WITH CHECK ADD  CONSTRAINT [FK_MedicineDispensationItems_Batches] FOREIGN KEY([MedicineBatchId])
REFERENCES [dbo].[MedicineBatches] ([MedicineBatchId])
GO
ALTER TABLE [dbo].[MedicineDispensationItems] CHECK CONSTRAINT [FK_MedicineDispensationItems_Batches]
GO
ALTER TABLE [dbo].[MedicineDispensationItems]  WITH CHECK ADD  CONSTRAINT [FK_MedicineDispensationItems_Dispensation] FOREIGN KEY([DispensationId])
REFERENCES [dbo].[MedicineDispensations] ([DispensationId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[MedicineDispensationItems] CHECK CONSTRAINT [FK_MedicineDispensationItems_Dispensation]
GO
ALTER TABLE [dbo].[MedicineDispensationItems]  WITH CHECK ADD  CONSTRAINT [FK_MedicineDispensationItems_Medicines] FOREIGN KEY([MedicineId])
REFERENCES [dbo].[Medicines] ([MedicineId])
GO
ALTER TABLE [dbo].[MedicineDispensationItems] CHECK CONSTRAINT [FK_MedicineDispensationItems_Medicines]
GO
ALTER TABLE [dbo].[MedicineDispensationItems]  WITH CHECK ADD  CONSTRAINT [FK_MedicineDispensationItems_PrescriptionItem] FOREIGN KEY([PrescriptionItemId])
REFERENCES [dbo].[PrescriptionItems] ([PrescriptionItemId])
GO
ALTER TABLE [dbo].[MedicineDispensationItems] CHECK CONSTRAINT [FK_MedicineDispensationItems_PrescriptionItem]
GO
ALTER TABLE [dbo].[ClinicProfile]  WITH CHECK ADD CHECK  (([ClinicId]=(1)))
GO
ALTER TABLE [dbo].[ClinicProfile]  WITH CHECK ADD  CONSTRAINT [CK_ClinicProfile_OpenClose] CHECK  (([ClosingTime] IS NULL OR [OpeningTime] IS NULL OR [ClosingTime]>[OpeningTime]))
GO
ALTER TABLE [dbo].[ClinicProfile] CHECK CONSTRAINT [CK_ClinicProfile_OpenClose]
GO
ALTER TABLE [dbo].[UserAccounts]  WITH CHECK ADD CHECK  (([FailedLoginCount]>=(0)))
GO
ALTER TABLE [dbo].[UserAccounts]  WITH CHECK ADD CHECK  (([Status]='Inactive' OR [Status]='Locked' OR [Status]='Active' OR [Status]='Unverified'))
GO
ALTER TABLE [dbo].[UserAccounts]  WITH CHECK ADD CHECK  (([Role]='SystemAdministrator' OR [Role]='DepartmentManager' OR [Role]='Dentist' OR [Role]='Receptionist' OR [Role]='Patient'))
GO
ALTER TABLE [dbo].[PatientProfiles]  WITH CHECK ADD CHECK  (([Gender] IS NULL OR ([Gender]='Other' OR [Gender]='Female' OR [Gender]='Male')))
GO
ALTER TABLE [dbo].[StaffProfiles]  WITH CHECK ADD CHECK  (([EmploymentStatus]='Inactive' OR [EmploymentStatus]='Active'))
GO
ALTER TABLE [dbo].[DentistProfiles]  WITH CHECK ADD CHECK  (([YearsOfExperience] IS NULL OR [YearsOfExperience]>=(0)))
GO
ALTER TABLE [dbo].[Departments]  WITH CHECK ADD CHECK  (([Status]='Inactive' OR [Status]='Active'))
GO
ALTER TABLE [dbo].[DentistDepartments]  WITH CHECK ADD CHECK  (([Status]='Inactive' OR [Status]='Active'))
GO
ALTER TABLE [dbo].[DepartmentWorkSchedules]  WITH CHECK ADD CHECK  (([DayOfWeek]>=(1) AND [DayOfWeek]<=(7)))
GO
ALTER TABLE [dbo].[DepartmentWorkSchedules]  WITH CHECK ADD  CONSTRAINT [CK_DepartmentWorkSchedules_Time] CHECK  (([EndTime]>[StartTime]))
GO
ALTER TABLE [dbo].[DepartmentWorkSchedules] CHECK CONSTRAINT [CK_DepartmentWorkSchedules_Time]
GO
ALTER TABLE [dbo].[DentistWorkSchedules]  WITH CHECK ADD CHECK  (([Status]='Cancelled' OR [Status]='Scheduled'))
GO
ALTER TABLE [dbo].[DentistWorkSchedules]  WITH CHECK ADD  CONSTRAINT [CK_DentistWorkSchedules_Time] CHECK  (([EndTime]>[StartTime]))
GO
ALTER TABLE [dbo].[DentistWorkSchedules] CHECK CONSTRAINT [CK_DentistWorkSchedules_Time]
GO
ALTER TABLE [dbo].[DentistAvailability]  WITH CHECK ADD CHECK  (([AvailabilityStatus]='Unavailable' OR [AvailabilityStatus]='OnLeave' OR [AvailabilityStatus]='Busy' OR [AvailabilityStatus]='Available'))
GO
ALTER TABLE [dbo].[DentistAvailability]  WITH CHECK ADD  CONSTRAINT [CK_DentistAvailability_Time] CHECK  (([EndDateTime] IS NULL OR [EndDateTime]>[StartDateTime]))
GO
ALTER TABLE [dbo].[DentistAvailability] CHECK CONSTRAINT [CK_DentistAvailability_Time]
GO
ALTER TABLE [dbo].[Rooms]  WITH CHECK ADD CHECK  (([Status]='Maintenance' OR [Status]='Inactive' OR [Status]='Active'))
GO
ALTER TABLE [dbo].[DentalChairs]  WITH CHECK ADD CHECK  (([Status]='Maintenance' OR [Status]='Inactive' OR [Status]='Active'))
GO
ALTER TABLE [dbo].[DepartmentServices]  WITH CHECK ADD CHECK  (([Status]='Inactive' OR [Status]='Active'))
GO
ALTER TABLE [dbo].[DepartmentServices]  WITH CHECK ADD  CONSTRAINT [CK_DepartmentServices_Duration] CHECK  (([DurationMinutes] IS NULL OR [DurationMinutes]>(0)))
GO
ALTER TABLE [dbo].[DepartmentServices] CHECK CONSTRAINT [CK_DepartmentServices_Duration]
GO
ALTER TABLE [dbo].[DepartmentServices]  WITH CHECK ADD  CONSTRAINT [CK_DepartmentServices_Price] CHECK  (([Price]>=(0)))
GO
ALTER TABLE [dbo].[DepartmentServices] CHECK CONSTRAINT [CK_DepartmentServices_Price]
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD CHECK  (([BookingSource]='Receptionist' OR [BookingSource]='Patient'))
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD CHECK  (([Status]='NoShow' OR [Status]='Cancelled' OR [Status]='Expired' OR [Status]='Withdrawn' OR [Status]='Rejected' OR [Status]='Completed' OR [Status]='InService' OR [Status]='Called' OR [Status]='Waiting' OR [Status]='CheckedIn' OR [Status]='Confirmed' OR [Status]='AwaitingPatientResponse' OR [Status]='PendingDepartmentReview' OR [Status]='PendingReceptionReview'))
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [CK_Appointments_Schedule] CHECK  (([ScheduledEnd] IS NULL OR [ScheduledStart] IS NULL OR [ScheduledEnd]>[ScheduledStart]))
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [CK_Appointments_Schedule]
GO
ALTER TABLE [dbo].[AppointmentChangeProposals]  WITH CHECK ADD CHECK  (([Status]='Expired' OR [Status]='Rejected' OR [Status]='Accepted' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[AppointmentChangeProposals]  WITH CHECK ADD  CONSTRAINT [CK_AppointmentChangeProposals_Time] CHECK  (([ProposedEnd] IS NULL OR [ProposedStart] IS NULL OR [ProposedEnd]>[ProposedStart]))
GO
ALTER TABLE [dbo].[AppointmentChangeProposals] CHECK CONSTRAINT [CK_AppointmentChangeProposals_Time]
GO
ALTER TABLE [dbo].[Notifications]  WITH CHECK ADD CHECK  (([Channel]='Email' OR [Channel]='InApp'))
GO
ALTER TABLE [dbo].[Notifications]  WITH CHECK ADD CHECK  (([DeliveryStatus]='Failed' OR [DeliveryStatus]='Sent' OR [DeliveryStatus]='Pending'))
GO
ALTER TABLE [dbo].[TreatmentPlans]  WITH CHECK ADD CHECK  (([Status]='Cancelled' OR [Status]='Completed' OR [Status]='InProgress' OR [Status]='Planned'))
GO
ALTER TABLE [dbo].[TreatmentPlans]  WITH CHECK ADD  CONSTRAINT [CK_TreatmentPlans_EstimatedTotal] CHECK  (([EstimatedTotal] IS NULL OR [EstimatedTotal]>=(0)))
GO
ALTER TABLE [dbo].[TreatmentPlans] CHECK CONSTRAINT [CK_TreatmentPlans_EstimatedTotal]
GO
ALTER TABLE [dbo].[TreatmentPlanItems]  WITH CHECK ADD CHECK  (([Status]='Cancelled' OR [Status]='Completed' OR [Status]='InProgress' OR [Status]='Planned'))
GO
ALTER TABLE [dbo].[TreatmentPlanItems]  WITH CHECK ADD  CONSTRAINT [CK_TreatmentPlanItems_Cost] CHECK  (([EstimatedCost] IS NULL OR [EstimatedCost]>=(0)))
GO
ALTER TABLE [dbo].[TreatmentPlanItems] CHECK CONSTRAINT [CK_TreatmentPlanItems_Cost]
GO
ALTER TABLE [dbo].[TreatmentPlanItems]  WITH CHECK ADD  CONSTRAINT [CK_TreatmentPlanItems_Sequence] CHECK  (([SequenceNo]>(0)))
GO
ALTER TABLE [dbo].[TreatmentPlanItems] CHECK CONSTRAINT [CK_TreatmentPlanItems_Sequence]
GO
ALTER TABLE [dbo].[PerformedServices]  WITH CHECK ADD  CONSTRAINT [CK_PerformedServices_Quantity] CHECK  (([Quantity]>(0)))
GO
ALTER TABLE [dbo].[PerformedServices] CHECK CONSTRAINT [CK_PerformedServices_Quantity]
GO
ALTER TABLE [dbo].[PerformedServices]  WITH CHECK ADD  CONSTRAINT [CK_PerformedServices_UnitPrice] CHECK  (([UnitPrice]>=(0)))
GO
ALTER TABLE [dbo].[PerformedServices] CHECK CONSTRAINT [CK_PerformedServices_UnitPrice]
GO
ALTER TABLE [dbo].[DepartmentReferrals]  WITH CHECK ADD CHECK  (([Status]='Completed' OR [Status]='Assigned' OR [Status]='Rejected' OR [Status]='Accepted' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[DepartmentReferrals]  WITH CHECK ADD  CONSTRAINT [CK_DepartmentReferrals_DifferentDepartment] CHECK  (([FromDepartmentId]<>[ToDepartmentId]))
GO
ALTER TABLE [dbo].[DepartmentReferrals] CHECK CONSTRAINT [CK_DepartmentReferrals_DifferentDepartment]
GO
ALTER TABLE [dbo].[DepartmentReferrals]  WITH CHECK ADD  CONSTRAINT [CK_DepartmentReferrals_Urgency] CHECK  (([Urgency]='Urgent' OR [Urgency]='Normal'))
GO
ALTER TABLE [dbo].[DepartmentReferrals] CHECK CONSTRAINT [CK_DepartmentReferrals_Urgency]
GO
ALTER TABLE [dbo].[FollowUpSchedules]  WITH CHECK ADD CHECK  (([Status]='Cancelled' OR [Status]='Completed' OR [Status]='Scheduled' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[Invoices]  WITH CHECK ADD CHECK  (([Status]='Cancelled' OR [Status]='Paid' OR [Status]='PartiallyPaid' OR [Status]='Unpaid'))
GO
ALTER TABLE [dbo].[Invoices]  WITH CHECK ADD  CONSTRAINT [CK_Invoices_Total] CHECK  (([TotalAmount]>=(0)))
GO
ALTER TABLE [dbo].[Invoices] CHECK CONSTRAINT [CK_Invoices_Total]
GO
ALTER TABLE [dbo].[InvoiceItems]  WITH CHECK ADD  CONSTRAINT [CK_InvoiceItems_Quantity] CHECK  (([Quantity]>(0)))
GO
ALTER TABLE [dbo].[InvoiceItems] CHECK CONSTRAINT [CK_InvoiceItems_Quantity]
GO
ALTER TABLE [dbo].[InvoiceItems]  WITH CHECK ADD  CONSTRAINT [CK_InvoiceItems_UnitPrice] CHECK  (([UnitPrice]>=(0)))
GO
ALTER TABLE [dbo].[InvoiceItems] CHECK CONSTRAINT [CK_InvoiceItems_UnitPrice]
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD CHECK  (([Method]='BankTransfer' OR [Method]='Online'))
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD CHECK  (([Status]='Cancelled' OR [Status]='Failed' OR [Status]='Successful' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD  CONSTRAINT [CK_Payments_Amount] CHECK  (([Amount]>(0)))
GO
ALTER TABLE [dbo].[Payments] CHECK CONSTRAINT [CK_Payments_Amount]
GO
ALTER TABLE [dbo].[VisitFeedback]  WITH CHECK ADD CHECK  (([ClinicRating]>=(1) AND [ClinicRating]<=(5)))
GO
ALTER TABLE [dbo].[VisitFeedback]  WITH CHECK ADD CHECK  (([DentistRating]>=(1) AND [DentistRating]<=(5)))
GO
ALTER TABLE [dbo].[BlogPosts]  WITH CHECK ADD  CONSTRAINT [CK_BlogPosts_Status] CHECK  (([Status]='Archived' OR [Status]='Published' OR [Status]='PendingReview' OR [Status]='Draft'))
GO
ALTER TABLE [dbo].[BlogPosts] CHECK CONSTRAINT [CK_BlogPosts_Status]
GO
ALTER TABLE [dbo].[BlogPostMedia]  WITH CHECK ADD  CONSTRAINT [CK_BlogPostMedia_Type] CHECK  (([MediaType]='Video' OR [MediaType]='Image'))
GO
ALTER TABLE [dbo].[BlogPostMedia] CHECK CONSTRAINT [CK_BlogPostMedia_Type]
GO
ALTER TABLE [dbo].[Medicines]  WITH CHECK ADD  CONSTRAINT [CHK_Medicines_MinAlert] CHECK  (([MinimumStockAlert]>=(0)))
GO
ALTER TABLE [dbo].[Medicines] CHECK CONSTRAINT [CHK_Medicines_MinAlert]
GO
ALTER TABLE [dbo].[Medicines]  WITH CHECK ADD  CONSTRAINT [CHK_Medicines_UnitPrice] CHECK  (([UnitPrice]>=(0)))
GO
ALTER TABLE [dbo].[Medicines] CHECK CONSTRAINT [CHK_Medicines_UnitPrice]
GO
ALTER TABLE [dbo].[MedicineBatches]  WITH CHECK ADD  CONSTRAINT [CHK_MedicineBatches_CostPrice] CHECK  (([CostPrice]>=(0)))
GO
ALTER TABLE [dbo].[MedicineBatches] CHECK CONSTRAINT [CHK_MedicineBatches_CostPrice]
GO
ALTER TABLE [dbo].[MedicineBatches]  WITH CHECK ADD  CONSTRAINT [CHK_MedicineBatches_Qty] CHECK  (([QuantityReceived]>=(0) AND [QuantityAvailable]>=(0) AND [QuantityAvailable]<=[QuantityReceived]))
GO
ALTER TABLE [dbo].[MedicineBatches] CHECK CONSTRAINT [CHK_MedicineBatches_Qty]
GO
ALTER TABLE [dbo].[MedicineStockTransactions]  WITH CHECK ADD  CONSTRAINT [CHK_MedicineStockTransactions_Qty] CHECK  (([Quantity]>(0)))
GO
ALTER TABLE [dbo].[MedicineStockTransactions] CHECK CONSTRAINT [CHK_MedicineStockTransactions_Qty]
GO
ALTER TABLE [dbo].[MedicineDispensationItems]  WITH CHECK ADD  CONSTRAINT [CHK_MedicineDispensationItems_Qty] CHECK  (([QuantityDispensed]>(0)))
GO
ALTER TABLE [dbo].[MedicineDispensationItems] CHECK CONSTRAINT [CHK_MedicineDispensationItems_Qty]
GO

-- =========================================================================================
-- PART 2: DATABASE VIEWS
-- =========================================================================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE VIEW dbo.vw_PublishedBlogPosts AS
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
WHERE bp.Status = 'Published';



GO

-- =========================================================================================
-- PART 3: STORED PROCEDURES
-- =========================================================================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE dbo.sp_CleanupOrphanBlogPostMedia
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.BlogPostMedia
    WHERE BlogPostId IS NULL
      AND CreatedAt < DATEADD(HOUR, -24, SYSDATETIME());
END;



GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
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

PRINT N'>>> DENTAL CLINIC CLEAN SCHEMA INITIALIZED SUCCESSFULLY (44 TABLES, VIEWS & PROCEDURES)!';
GO
