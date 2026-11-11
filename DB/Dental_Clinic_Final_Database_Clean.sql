-- =========================================================================================
-- DENTAL CLINIC MANAGEMENT SYSTEM (SWP391 GROUP 6)
-- MASTER CLEAN DATABASE SCHEMA (69 TABLES, VIEWS, STORED PROCEDURES)
-- Target: Microsoft SQL Server 2017 / 2019 / 2022 / LocalDB / SQLEXPRESS
-- Roles: Patient, Receptionist, Dentist, SystemAdministrator (Strict 4 Roles)
-- Generated: 2026-11-12 01:16:02
-- =========================================================================================

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

-- =========================================================================================
-- PART 1: 69 DDL TABLE SCHEMAS, CONSTRAINTS & INDEXES
-- =========================================================================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[__EFMigrationsHistory](
	[MigrationId] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ProductVersion] [nvarchar](32) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
 CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED 
(
	[MigrationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

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
CREATE TABLE [dbo].[AppointmentDisruptions](
	[DisruptionId] [bigint] IDENTITY(1,1) NOT NULL,
	[AppointmentId] [bigint] NOT NULL,
	[DentistLeaveRequestId] [bigint] NOT NULL,
	[OriginalDentistUserId] [bigint] NOT NULL,
	[OriginalScheduledStart] [datetime2](7) NOT NULL,
	[OriginalScheduledEnd] [datetime2](7) NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PatientChoice] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ResolvedAppointmentId] [bigint] NULL,
	[NewDentistUserId] [bigint] NULL,
	[ResolvedByUserId] [bigint] NULL,
	[ResolvedAt] [datetime2](7) NULL,
	[StaffNotes] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[EmailSentAt] [datetime2](7) NULL,
	[EmailDeliveryStatus] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[EmailFailureReason] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[EmailRetryCount] [int] NOT NULL,
	[ActionToken] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[TokenExpiresAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_AppointmentDisruptions] PRIMARY KEY CLUSTERED 
(
	[DisruptionId] ASC
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
	[TreatmentPlanItemId] [bigint] NULL,
	[ComboCode] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ComboId] [bigint] NULL,
	[CareEpisodeId] [bigint] NULL,
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
CREATE TABLE [dbo].[AttendancePeriodClosures](
	[ClosureId] [bigint] IDENTITY(1,1) NOT NULL,
	[Year] [int] NOT NULL,
	[Month] [int] NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[TotalStaffCount] [int] NOT NULL,
	[TotalWorkedHours] [decimal](10, 2) NOT NULL,
	[TotalScheduledShifts] [int] NOT NULL,
	[TotalCompletedShifts] [int] NOT NULL,
	[TotalMissingPunches] [int] NOT NULL,
	[FinalizedByUserId] [bigint] NULL,
	[FinalizedAtUtc] [datetime2](7) NULL,
	[ReopenedByUserId] [bigint] NULL,
	[ReopenedAtUtc] [datetime2](7) NULL,
	[ReopenReason] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[SnapshotDataJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_AttendancePeriodClosures] PRIMARY KEY CLUSTERED 
(
	[ClosureId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_AttendancePeriodClosures_Year_Month] UNIQUE NONCLUSTERED 
(
	[Year] ASC,
	[Month] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AttendancePolicies](
	[PolicyId] [bigint] IDENTITY(1,1) NOT NULL,
	[LateGracePeriodMinutes] [int] NOT NULL,
	[EarlyLeaveGracePeriodMinutes] [int] NOT NULL,
	[StandardWorkDayHours] [decimal](4, 2) NOT NULL,
	[AutoMissingPunchAfterHours] [int] NOT NULL,
	[AllowSelfCorrectionRequest] [bit] NOT NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[UpdatedAtUtc] [datetime2](7) NULL,
 CONSTRAINT [PK_AttendancePolicies] PRIMARY KEY CLUSTERED 
(
	[PolicyId] ASC
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
	[PostType] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RejectionReason] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ApprovedByUserId] [bigint] NULL,
	[ApprovedAt] [datetime2](7) NULL,
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
CREATE TABLE [dbo].[CareEpisodes](
	[CareEpisodeId] [bigint] IDENTITY(1,1) NOT NULL,
	[EpisodeCode] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PatientUserId] [bigint] NOT NULL,
	[RootAppointmentId] [bigint] NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[OpenedAtUtc] [datetime2](7) NOT NULL,
	[ClosedAtUtc] [datetime2](7) NULL,
	[CreatedByUserId] [bigint] NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[UpdatedAtUtc] [datetime2](7) NULL,
	[Notes] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
 CONSTRAINT [PK_CareEpisodes] PRIMARY KEY CLUSTERED 
(
	[CareEpisodeId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_CareEpisodes_EpisodeCode] UNIQUE NONCLUSTERED 
(
	[EpisodeCode] ASC
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
CREATE TABLE [dbo].[ClinicalConsultations](
	[ConsultationId] [bigint] IDENTITY(1,1) NOT NULL,
	[SourceAppointmentId] [bigint] NOT NULL,
	[PatientUserId] [bigint] NOT NULL,
	[RequestingDentistUserId] [bigint] NOT NULL,
	[ConsultedDentistUserId] [bigint] NOT NULL,
	[ConsultationReason] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ClinicalQuestion] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Urgency] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[DentistOpinion] [nvarchar](2000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Recommendations] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[RequestedAtUtc] [datetime2](7) NOT NULL,
	[AnsweredAtUtc] [datetime2](7) NULL,
 CONSTRAINT [PK_ClinicalConsultations] PRIMARY KEY CLUSTERED 
(
	[ConsultationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
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
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[DentistLeaveRequests](
	[LeaveRequestId] [bigint] IDENTITY(1,1) NOT NULL,
	[DentistUserId] [bigint] NOT NULL,
	[LeaveType] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[StartDate] [date] NOT NULL,
	[EndDate] [date] NOT NULL,
	[StartTime] [time](7) NULL,
	[EndTime] [time](7) NULL,
	[Reason] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Notes] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ReviewedByUserId] [bigint] NULL,
	[ReviewedAt] [datetime2](7) NULL,
	[RejectionReason] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_DentistLeaveRequests] PRIMARY KEY CLUSTERED 
(
	[LeaveRequestId] ASC
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
	[HasRadiationSafetyCert] [bit] NOT NULL,
	[RadiationCertNumber] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[RadiationCertExpiryDate] [date] NULL,
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
CREATE TABLE [dbo].[DepartmentReferrals](
	[ReferralId] [bigint] IDENTITY(1,1) NOT NULL,
	[TreatmentSessionId] [bigint] NULL,
	[FromDepartmentId] [bigint] NOT NULL,
	[ToDepartmentId] [bigint] NOT NULL,
	[AssignedDentistUserId] [bigint] NULL,
	[ReviewedByUserId] [bigint] NULL,
	[Reason] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[ReviewedAt] [datetime2](7) NULL,
	[Urgency] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ClinicalInstructions] [nvarchar](2000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[SourceAppointmentId] [bigint] NULL,
	[DestinationAppointmentId] [bigint] NULL,
	[CareEpisodeId] [bigint] NULL,
	[RequestedServiceId] [bigint] NULL,
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
CREATE TABLE [dbo].[DepartmentServices](
	[DepartmentServiceId] [bigint] IDENTITY(1,1) NOT NULL,
	[DepartmentId] [bigint] NOT NULL,
	[ServiceName] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Description] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[DurationMinutes] [int] NULL,
	[Price] [decimal](18, 2) NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ServiceType] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RequiresExclusiveRoom] [bit] NOT NULL,
	[RequiresConsultationFirst] [bit] NOT NULL,
	[MinLeadTimeHours] [int] NOT NULL,
	[BookingInstructions] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
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
CREATE TABLE [dbo].[DiagnosticOrders](
	[DiagnosticOrderId] [bigint] IDENTITY(1,1) NOT NULL,
	[AppointmentId] [bigint] NOT NULL,
	[PatientUserId] [bigint] NOT NULL,
	[DentistUserId] [bigint] NOT NULL,
	[DiagnosticType] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ClinicalIndication] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[IsExternal] [bit] NOT NULL,
	[ExternalFacilityName] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[BilledAtUtc] [datetime2](7) NULL,
	[InvoiceItemId] [bigint] NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[UpdatedAtUtc] [datetime2](7) NULL,
 CONSTRAINT [PK_DiagnosticOrders] PRIMARY KEY CLUSTERED 
(
	[DiagnosticOrderId] ASC
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
CREATE TABLE [dbo].[DiagnosticResults](
	[DiagnosticResultId] [bigint] IDENTITY(1,1) NOT NULL,
	[DiagnosticOrderId] [bigint] NOT NULL,
	[ImagingSessionId] [bigint] NULL,
	[SourceType] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EquipmentId] [bigint] NULL,
	[ExternalFacilityName] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[FileUrl] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ThumbnailUrl] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ImageFormat] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Findings] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ReviewedByDentistUserId] [bigint] NULL,
	[ReviewedAtUtc] [datetime2](7) NULL,
	[ReviewNotes] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[IsReviewed] [bit] NOT NULL,
	[UploadedByUserId] [bigint] NOT NULL,
	[PerformedAtUtc] [datetime2](7) NULL,
	[UploadedAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_DiagnosticResults] PRIMARY KEY CLUSTERED 
(
	[DiagnosticResultId] ASC
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
CREATE TABLE [dbo].[EpisodeServiceTasks](
	[ServiceTaskId] [bigint] IDENTITY(1,1) NOT NULL,
	[TaskCode] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CareEpisodeId] [bigint] NOT NULL,
	[AppointmentId] [bigint] NOT NULL,
	[DepartmentServiceId] [bigint] NOT NULL,
	[PerformingDentistUserId] [bigint] NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Quantity] [int] NOT NULL,
	[UnitPriceSnapshot] [decimal](18, 2) NOT NULL,
	[EstimatedDurationMinutes] [int] NOT NULL,
	[PlannedAtUtc] [datetime2](7) NOT NULL,
	[StartedAtUtc] [datetime2](7) NULL,
	[CompletedAtUtc] [datetime2](7) NULL,
	[CancelledAtUtc] [datetime2](7) NULL,
	[CancellationReason] [nvarchar](250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ClinicalRecordId] [bigint] NULL,
	[Notes] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[UpdatedAtUtc] [datetime2](7) NULL,
 CONSTRAINT [PK_EpisodeServiceTasks] PRIMARY KEY CLUSTERED 
(
	[ServiceTaskId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_EpisodeServiceTasks_TaskCode] UNIQUE NONCLUSTERED 
(
	[TaskCode] ASC
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
CREATE TABLE [dbo].[ImagingEquipments](
	[EquipmentId] [bigint] IDENTITY(1,1) NOT NULL,
	[RoomId] [bigint] NOT NULL,
	[EquipmentCode] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EquipmentName] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EquipmentType] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SupportedDiagnosticTypes] [nvarchar](250) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[IsActive] [bit] NOT NULL,
	[OperationalStatus] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AvailableFrom] [time](7) NULL,
	[AvailableTo] [time](7) NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[UpdatedAtUtc] [datetime2](7) NULL,
 CONSTRAINT [PK_ImagingEquipments] PRIMARY KEY CLUSTERED 
(
	[EquipmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_ImagingEquipments_Code] UNIQUE NONCLUSTERED 
(
	[EquipmentCode] ASC
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
CREATE TABLE [dbo].[ImagingServiceConfigs](
	[ConfigId] [bigint] IDENTITY(1,1) NOT NULL,
	[DiagnosticType] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ServiceName] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SlotDurationMinutes] [int] NOT NULL,
	[BufferMinutes] [int] NOT NULL,
	[Price] [decimal](18, 2) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[DepartmentServiceId] [bigint] NULL,
 CONSTRAINT [PK_ImagingServiceConfigs] PRIMARY KEY CLUSTERED 
(
	[ConfigId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_ImagingServiceConfigs_Type] UNIQUE NONCLUSTERED 
(
	[DiagnosticType] ASC
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
CREATE TABLE [dbo].[ImagingSessions](
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
	[QueueNumber] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Priority] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CheckInTimeUtc] [datetime2](7) NULL,
	[EstimatedWaitMinutes] [int] NULL,
	[DelayNotificationSent] [bit] NOT NULL,
	[EscalationAlertSent] [bit] NOT NULL,
	[Notes] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedByUserId] [bigint] NOT NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[UpdatedAtUtc] [datetime2](7) NULL,
 CONSTRAINT [PK_ImagingSessions] PRIMARY KEY CLUSTERED 
(
	[ImagingSessionId] ASC
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
	[AppointmentId] [bigint] NULL,
	[PerformingDentistUserId] [bigint] NULL,
	[ServiceTaskId] [bigint] NULL,
	[SourceType] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[SourceId] [bigint] NULL,
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
CREATE TABLE [dbo].[Invoices](
	[InvoiceId] [bigint] IDENTITY(1,1) NOT NULL,
	[InvoiceCode] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AppointmentId] [bigint] NOT NULL,
	[GeneratedByUserId] [bigint] NOT NULL,
	[TotalAmount] [decimal](18, 2) NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CareEpisodeId] [bigint] NULL,
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
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[ReceptionistCoverageIncidents](
	[IncidentId] [bigint] IDENTITY(1,1) NOT NULL,
	[WorkDate] [date] NOT NULL,
	[ShiftStartTime] [time](0) NOT NULL,
	[ShiftEndTime] [time](0) NOT NULL,
	[AffectedReceptionistUserId] [bigint] NULL,
	[IncidentType] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PlannedStaffCount] [int] NOT NULL,
	[AvailableStaffCount] [int] NOT NULL,
	[RequiredMinimumCount] [int] NOT NULL,
	[ShortageCount] [int] NOT NULL,
	[Reason] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ReportedBy] [bigint] NOT NULL,
	[ReportedAt] [datetime2](7) NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ResolutionNotes] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ReplacementReceptionistUserId] [bigint] NULL,
	[ResolvedBy] [bigint] NULL,
	[ResolvedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_ReceptionistCoverageIncidents] PRIMARY KEY CLUSTERED 
(
	[IncidentId] ASC
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
CREATE TABLE [dbo].[ReceptionistLeaveRequestPeriods](
	[PeriodId] [bigint] IDENTITY(1,1) NOT NULL,
	[LeaveRequestId] [bigint] NOT NULL,
	[LeaveDate] [date] NOT NULL,
	[StartTime] [time](0) NULL,
	[EndTime] [time](0) NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
 CONSTRAINT [PK_ReceptionistLeaveRequestPeriods] PRIMARY KEY CLUSTERED 
(
	[PeriodId] ASC
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
CREATE TABLE [dbo].[ReceptionistLeaveRequests](
	[LeaveRequestId] [bigint] IDENTITY(1,1) NOT NULL,
	[ReceptionistUserId] [bigint] NOT NULL,
	[LeaveType] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[StartDate] [date] NOT NULL,
	[EndDate] [date] NOT NULL,
	[StartTime] [time](0) NULL,
	[EndTime] [time](0) NULL,
	[Reason] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AdditionalNotes] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RejectionReason] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ApprovedBy] [bigint] NULL,
	[ApprovedAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_ReceptionistLeaveRequests] PRIMARY KEY CLUSTERED 
(
	[LeaveRequestId] ASC
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
CREATE TABLE [dbo].[ReceptionistShiftSwapRequests](
	[SwapRequestId] [bigint] IDENTITY(1,1) NOT NULL,
	[RequestingReceptionistUserId] [bigint] NOT NULL,
	[TargetReceptionistUserId] [bigint] NOT NULL,
	[SourceScheduleId] [bigint] NOT NULL,
	[TargetScheduleId] [bigint] NULL,
	[Reason] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CounterpartyResponseAt] [datetime2](7) NULL,
	[CounterpartyNote] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AdminDecisionBy] [bigint] NULL,
	[AdminDecisionAt] [datetime2](7) NULL,
	[AdminDecisionReason] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_ReceptionistShiftSwapRequests] PRIMARY KEY CLUSTERED 
(
	[SwapRequestId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ReceptionistShiftTemplates](
	[ShiftTemplateId] [bigint] IDENTITY(1,1) NOT NULL,
	[TemplateName] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[StartTime] [time](0) NOT NULL,
	[EndTime] [time](0) NOT NULL,
	[MinStaffRequired] [int] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[DisplayOrder] [int] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_ReceptionistShiftTemplates] PRIMARY KEY CLUSTERED 
(
	[ShiftTemplateId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[ReceptionistWorkHandovers](
	[HandoverId] [bigint] IDENTITY(1,1) NOT NULL,
	[FromReceptionistUserId] [bigint] NOT NULL,
	[ToReceptionistUserId] [bigint] NULL,
	[WorkDate] [date] NOT NULL,
	[ShiftId] [bigint] NULL,
	[HandoverType] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PendingAppointmentCount] [int] NOT NULL,
	[UnpaidInvoiceCount] [int] NOT NULL,
	[ActiveChatCount] [int] NOT NULL,
	[Notes] [nvarchar](2000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[HandoverStatus] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[AcknowledgedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_ReceptionistWorkHandovers] PRIMARY KEY CLUSTERED 
(
	[HandoverId] ASC
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
CREATE TABLE [dbo].[ReceptionistWorkSchedules](
	[ReceptionistScheduleId] [bigint] IDENTITY(1,1) NOT NULL,
	[ReceptionistUserId] [bigint] NOT NULL,
	[ShiftTemplateId] [bigint] NULL,
	[WorkDate] [date] NOT NULL,
	[StartTime] [time](0) NOT NULL,
	[EndTime] [time](0) NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[IsPublished] [bit] NOT NULL,
	[Notes] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[CreatedBy] [bigint] NOT NULL,
 CONSTRAINT [PK_ReceptionistWorkSchedules] PRIMARY KEY CLUSTERED 
(
	[ReceptionistScheduleId] ASC
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
	[RoomType] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[MaxConcurrentPatients] [int] NOT NULL,
	[MaxWaitingPatients] [int] NOT NULL,
	[TargetMaxWaitMinutes] [int] NOT NULL,
	[EscalationWaitMinutes] [int] NOT NULL,
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
CREATE TABLE [dbo].[ServiceCombos](
	[ComboId] [bigint] IDENTITY(1,1) NOT NULL,
	[ComboCode] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Title] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Tagline] [nvarchar](300) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Badge] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[IconClass] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Description] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[DepartmentId] [bigint] NOT NULL,
	[PrimaryServiceId] [bigint] NOT NULL,
	[OriginalPrice] [decimal](18, 2) NOT NULL,
	[ComboPrice] [decimal](18, 2) NOT NULL,
	[DurationMinutes] [int] NOT NULL,
	[IncludedServices] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Benefits] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[ComboId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_ServiceCombos_ComboCode] UNIQUE NONCLUSTERED 
(
	[ComboCode] ASC
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
CREATE TABLE [dbo].[StaffAttendanceCorrectionRequests](
	[CorrectionRequestId] [bigint] IDENTITY(1,1) NOT NULL,
	[AttendanceId] [bigint] NOT NULL,
	[UserId] [bigint] NOT NULL,
	[WorkDate] [date] NOT NULL,
	[RequestedClockInUtc] [datetime2](7) NULL,
	[RequestedClockOutUtc] [datetime2](7) NULL,
	[Reason] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Explanation] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AdminNote] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ReviewedByUserId] [bigint] NULL,
	[ReviewedAtUtc] [datetime2](7) NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_StaffAttendanceCorrectionRequests] PRIMARY KEY CLUSTERED 
(
	[CorrectionRequestId] ASC
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
CREATE TABLE [dbo].[StaffAttendanceEvents](
	[EventId] [bigint] IDENTITY(1,1) NOT NULL,
	[AttendanceId] [bigint] NOT NULL,
	[UserId] [bigint] NOT NULL,
	[EventType] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EventTimeUtc] [datetime2](7) NOT NULL,
	[Source] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[IpAddress] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Note] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
 CONSTRAINT [PK_StaffAttendanceEvents] PRIMARY KEY CLUSTERED 
(
	[EventId] ASC
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
CREATE TABLE [dbo].[StaffAttendanceRecords](
	[AttendanceId] [bigint] IDENTITY(1,1) NOT NULL,
	[UserId] [bigint] NOT NULL,
	[WorkDate] [date] NOT NULL,
	[ShiftIdentifier] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Role] [varchar](40) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ScheduledStartTime] [time](7) NOT NULL,
	[ScheduledEndTime] [time](7) NOT NULL,
	[ClockInTimeUtc] [datetime2](7) NULL,
	[ClockOutTimeUtc] [datetime2](7) NULL,
	[ActualWorkedMinutes] [int] NOT NULL,
	[LateMinutes] [int] NOT NULL,
	[EarlyDepartureMinutes] [int] NOT NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[IsApprovedLeave] [bit] NOT NULL,
	[LeaveHours] [decimal](5, 2) NOT NULL,
	[Notes] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[IsFinalized] [bit] NOT NULL,
	[FinalizedAtUtc] [datetime2](7) NULL,
	[FinalizedByUserId] [bigint] NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[UpdatedAtUtc] [datetime2](7) NULL,
	[RowVersion] [timestamp] NOT NULL,
 CONSTRAINT [PK_StaffAttendanceRecords] PRIMARY KEY CLUSTERED 
(
	[AttendanceId] ASC
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
CREATE TABLE [dbo].[SupportChatMessages](
	[MessageId] [bigint] IDENTITY(1,1) NOT NULL,
	[ConversationId] [nvarchar](64) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SenderType] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
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
CREATE TABLE [dbo].[VisitFeedback](
	[FeedbackId] [bigint] IDENTITY(1,1) NOT NULL,
	[AppointmentId] [bigint] NOT NULL,
	[DentistRating] [tinyint] NOT NULL,
	[ClinicRating] [tinyint] NOT NULL,
	[Comment] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[DentistUserId] [bigint] NULL,
	[PatientUserId] [bigint] NULL,
	[Status] [varchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AdminNotes] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AdminRespondedByUserId] [bigint] NULL,
	[AdminResponse] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UpdatedAt] [datetime2](7) NULL,
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
SET ANSI_PADDING OFF
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[XrayDentistRosters](
	[RosterId] [bigint] IDENTITY(1,1) NOT NULL,
	[RoomId] [bigint] NOT NULL,
	[EquipmentId] [bigint] NOT NULL,
	[DentistUserId] [bigint] NOT NULL,
	[BackupDentistUserId] [bigint] NULL,
	[WorkDate] [date] NOT NULL,
	[ShiftType] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[StartTime] [time](0) NOT NULL,
	[EndTime] [time](0) NOT NULL,
	[Status] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Notes] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedByUserId] [bigint] NOT NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[UpdatedAtUtc] [datetime2](7) NULL,
 CONSTRAINT [PK_XrayDentistRosters] PRIMARY KEY CLUSTERED 
(
	[RosterId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AppointmentChangeProposals_Appointment_Status] ON [dbo].[AppointmentChangeProposals]
(
	[AppointmentId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_AppointmentDisruptions_AppointmentId] ON [dbo].[AppointmentDisruptions]
(
	[AppointmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_AppointmentDisruptions_LeaveRequestId] ON [dbo].[AppointmentDisruptions]
(
	[DentistLeaveRequestId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AppointmentDisruptions_Status] ON [dbo].[AppointmentDisruptions]
(
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_AppointmentDisruptions_ActionToken] ON [dbo].[AppointmentDisruptions]
(
	[ActionToken] ASC
)
WHERE ([ActionToken] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_AppointmentDisruptions_Appt_Leave] ON [dbo].[AppointmentDisruptions]
(
	[AppointmentId] ASC,
	[DentistLeaveRequestId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
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
CREATE NONCLUSTERED INDEX [IX_Appointments_AssignedDentist_Start] ON [dbo].[Appointments]
(
	[AssignedDentistUserId] ASC,
	[ScheduledStart] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Appointments_CareEpisodeId] ON [dbo].[Appointments]
(
	[CareEpisodeId] ASC
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
CREATE NONCLUSTERED INDEX [IX_Appointments_TreatmentPlanItemId] ON [dbo].[Appointments]
(
	[TreatmentPlanItemId] ASC
)
WHERE ([TreatmentPlanItemId] IS NOT NULL)
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
CREATE NONCLUSTERED INDEX [IX_AppointmentStatusHistories_Appointment_ChangedAt] ON [dbo].[AppointmentStatusHistories]
(
	[AppointmentId] ASC,
	[ChangedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_AuditLogs_User_CreatedAt] ON [dbo].[AuditLogs]
(
	[UserId] ASC,
	[CreatedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
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
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_CareEpisodes_Patient_Status] ON [dbo].[CareEpisodes]
(
	[PatientUserId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_CareEpisodes_RootAppointment] ON [dbo].[CareEpisodes]
(
	[RootAppointmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_ClinicalConsultations_ConsultedDentist] ON [dbo].[ClinicalConsultations]
(
	[ConsultedDentistUserId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_ClinicalConsultations_RequestingDentist] ON [dbo].[ClinicalConsultations]
(
	[RequestingDentistUserId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_ClinicalConsultations_SourceAppt] ON [dbo].[ClinicalConsultations]
(
	[SourceAppointmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_ClinicalEntries_Record_CreatedAt] ON [dbo].[ClinicalEntries]
(
	[MedicalRecordId] ASC,
	[CreatedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_DentistAvailability_Dentist_Time] ON [dbo].[DentistAvailability]
(
	[DentistUserId] ASC,
	[StartDateTime] ASC,
	[EndDateTime] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_DentistDepartments_Department_Status] ON [dbo].[DentistDepartments]
(
	[DepartmentId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_DentistLeaveRequests_Dentist_Status_Dates] ON [dbo].[DentistLeaveRequests]
(
	[DentistUserId] ASC,
	[Status] ASC,
	[StartDate] ASC,
	[EndDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_DentistLeaveRequests_Status] ON [dbo].[DentistLeaveRequests]
(
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
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
CREATE NONCLUSTERED INDEX [IX_DentistWorkSchedules_Dentist_Date] ON [dbo].[DentistWorkSchedules]
(
	[DentistUserId] ASC,
	[WorkDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_DepartmentReferrals_CareEpisodeId] ON [dbo].[DepartmentReferrals]
(
	[CareEpisodeId] ASC
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
CREATE NONCLUSTERED INDEX [IX_DiagnosticOrders_AppointmentId] ON [dbo].[DiagnosticOrders]
(
	[AppointmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_DiagnosticOrders_Status] ON [dbo].[DiagnosticOrders]
(
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_DiagnosticResults_Order] ON [dbo].[DiagnosticResults]
(
	[DiagnosticOrderId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_EpisodeServiceTasks_AppointmentId] ON [dbo].[EpisodeServiceTasks]
(
	[AppointmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_EpisodeServiceTasks_Episode_Status] ON [dbo].[EpisodeServiceTasks]
(
	[CareEpisodeId] ASC,
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
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_ImagingSessions_Equipment_Interval] ON [dbo].[ImagingSessions]
(
	[EquipmentId] ASC,
	[ScheduledStartUtc] ASC,
	[ScheduledEndUtc] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_ImagingSessions_Order] ON [dbo].[ImagingSessions]
(
	[DiagnosticOrderId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_ImagingSessions_Room_Status] ON [dbo].[ImagingSessions]
(
	[ImagingRoomId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_InvoiceItems_Invoice] ON [dbo].[InvoiceItems]
(
	[InvoiceId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_Invoice_Source] ON [dbo].[InvoiceItems]
(
	[InvoiceId] ASC,
	[SourceType] ASC,
	[SourceId] ASC
)
WHERE ([SourceType] IS NOT NULL AND [SourceId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Invoices_CareEpisodeId] ON [dbo].[Invoices]
(
	[CareEpisodeId] ASC
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
CREATE NONCLUSTERED INDEX [IX_MedicineDispensationItems_Dispensation] ON [dbo].[MedicineDispensationItems]
(
	[DispensationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_MedicineDispensations_Appt_Prescription] ON [dbo].[MedicineDispensations]
(
	[AppointmentId] ASC,
	[PrescriptionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_MedicineStockTransactions_BatchId] ON [dbo].[MedicineStockTransactions]
(
	[MedicineBatchId] ASC,
	[CreatedAt] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Notifications_User_ReadAt] ON [dbo].[Notifications]
(
	[UserId] ASC,
	[ReadAt] ASC
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
CREATE NONCLUSTERED INDEX [IX_PerformedServices_Session] ON [dbo].[PerformedServices]
(
	[TreatmentSessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_ReceptionistCoverageIncidents_WorkDate_Status] ON [dbo].[ReceptionistCoverageIncidents]
(
	[WorkDate] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_ReceptionistLeaveRequestPeriods_Date_Status] ON [dbo].[ReceptionistLeaveRequestPeriods]
(
	[LeaveDate] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_ReceptionistLeaveRequests_Dates_Status] ON [dbo].[ReceptionistLeaveRequests]
(
	[StartDate] ASC,
	[EndDate] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_ReceptionistLeaveRequests_User_Status] ON [dbo].[ReceptionistLeaveRequests]
(
	[ReceptionistUserId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_ReceptionistShiftSwapRequests_RequestingUser] ON [dbo].[ReceptionistShiftSwapRequests]
(
	[RequestingReceptionistUserId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_ReceptionistShiftSwapRequests_TargetUser] ON [dbo].[ReceptionistShiftSwapRequests]
(
	[TargetReceptionistUserId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_ReceptionistWorkHandovers_FromUser] ON [dbo].[ReceptionistWorkHandovers]
(
	[FromReceptionistUserId] ASC,
	[WorkDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_ReceptionistWorkSchedules_User_WorkDate] ON [dbo].[ReceptionistWorkSchedules]
(
	[ReceptionistUserId] ASC,
	[WorkDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_ReceptionistWorkSchedules_WorkDate_Status] ON [dbo].[ReceptionistWorkSchedules]
(
	[WorkDate] ASC,
	[Status] ASC
)
INCLUDE([ReceptionistUserId],[StartTime],[EndTime]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_StaffAttendanceCorrection_Status_CreatedAt] ON [dbo].[StaffAttendanceCorrectionRequests]
(
	[Status] ASC,
	[CreatedAtUtc] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_StaffAttendanceCorrection_UserId_WorkDate] ON [dbo].[StaffAttendanceCorrectionRequests]
(
	[UserId] ASC,
	[WorkDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_StaffAttendanceEvents_AttendanceId_EventTime] ON [dbo].[StaffAttendanceEvents]
(
	[AttendanceId] ASC,
	[EventTimeUtc] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_StaffAttendanceRecords_IsFinalized_WorkDate] ON [dbo].[StaffAttendanceRecords]
(
	[IsFinalized] ASC,
	[WorkDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_StaffAttendanceRecords_User_Date_Shift] ON [dbo].[StaffAttendanceRecords]
(
	[UserId] ASC,
	[WorkDate] ASC,
	[ShiftIdentifier] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_StaffAttendanceRecords_WorkDate_Role_Status] ON [dbo].[StaffAttendanceRecords]
(
	[WorkDate] ASC,
	[Role] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_SupportChatMessages_Conversation_SentAt] ON [dbo].[SupportChatMessages]
(
	[ConversationId] ASC,
	[SentAt] ASC
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
CREATE NONCLUSTERED INDEX [IX_VisitFeedback_Dentist_Status] ON [dbo].[VisitFeedback]
(
	[DentistUserId] ASC,
	[Status] ASC
)
INCLUDE([DentistRating],[ClinicRating],[CreatedAt]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_XrayDentistRosters_Dentist_Date] ON [dbo].[XrayDentistRosters]
(
	[DentistUserId] ASC,
	[WorkDate] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_XrayDentistRosters_Room_Date] ON [dbo].[XrayDentistRosters]
(
	[RoomId] ASC,
	[WorkDate] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[AppointmentChangeProposals] ADD  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[AppointmentChangeProposals] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[AppointmentDisruptions] ADD  CONSTRAINT [DF_AppointmentDisruptions_Status]  DEFAULT ('PendingPatientChoice') FOR [Status]
GO
ALTER TABLE [dbo].[AppointmentDisruptions] ADD  CONSTRAINT [DF_AppointmentDisruptions_EmailStatus]  DEFAULT ('Pending') FOR [EmailDeliveryStatus]
GO
ALTER TABLE [dbo].[AppointmentDisruptions] ADD  CONSTRAINT [DF_AppointmentDisruptions_EmailRetryCount]  DEFAULT ((0)) FOR [EmailRetryCount]
GO
ALTER TABLE [dbo].[AppointmentDisruptions] ADD  CONSTRAINT [DF_AppointmentDisruptions_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[AppointmentReminderJobs] ADD  DEFAULT ('Scheduled') FOR [Status]
GO
ALTER TABLE [dbo].[AppointmentReminderJobs] ADD  DEFAULT ((0)) FOR [RetryCount]
GO
ALTER TABLE [dbo].[AppointmentReminderJobs] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Appointments] ADD  DEFAULT ('PendingReceptionReview') FOR [Status]
GO
ALTER TABLE [dbo].[Appointments] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[AppointmentStatusHistories] ADD  DEFAULT (sysdatetime()) FOR [ChangedAt]
GO
ALTER TABLE [dbo].[AttendancePeriodClosures] ADD  CONSTRAINT [DF_AttendancePeriodClosures_Status]  DEFAULT ('Open') FOR [Status]
GO
ALTER TABLE [dbo].[AttendancePeriodClosures] ADD  CONSTRAINT [DF_AttendancePeriodClosures_StaffCount]  DEFAULT ((0)) FOR [TotalStaffCount]
GO
ALTER TABLE [dbo].[AttendancePeriodClosures] ADD  CONSTRAINT [DF_AttendancePeriodClosures_WorkedHours]  DEFAULT ((0)) FOR [TotalWorkedHours]
GO
ALTER TABLE [dbo].[AttendancePeriodClosures] ADD  CONSTRAINT [DF_AttendancePeriodClosures_ScheduledShifts]  DEFAULT ((0)) FOR [TotalScheduledShifts]
GO
ALTER TABLE [dbo].[AttendancePeriodClosures] ADD  CONSTRAINT [DF_AttendancePeriodClosures_CompletedShifts]  DEFAULT ((0)) FOR [TotalCompletedShifts]
GO
ALTER TABLE [dbo].[AttendancePeriodClosures] ADD  CONSTRAINT [DF_AttendancePeriodClosures_MissingPunches]  DEFAULT ((0)) FOR [TotalMissingPunches]
GO
ALTER TABLE [dbo].[AttendancePeriodClosures] ADD  CONSTRAINT [DF_AttendancePeriodClosures_CreatedAtUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedAtUtc]
GO
ALTER TABLE [dbo].[AttendancePolicies] ADD  CONSTRAINT [DF_AttendancePolicies_LateGrace]  DEFAULT ((10)) FOR [LateGracePeriodMinutes]
GO
ALTER TABLE [dbo].[AttendancePolicies] ADD  CONSTRAINT [DF_AttendancePolicies_EarlyGrace]  DEFAULT ((10)) FOR [EarlyLeaveGracePeriodMinutes]
GO
ALTER TABLE [dbo].[AttendancePolicies] ADD  CONSTRAINT [DF_AttendancePolicies_StandardDayHours]  DEFAULT ((8.00)) FOR [StandardWorkDayHours]
GO
ALTER TABLE [dbo].[AttendancePolicies] ADD  CONSTRAINT [DF_AttendancePolicies_AutoMissing]  DEFAULT ((4)) FOR [AutoMissingPunchAfterHours]
GO
ALTER TABLE [dbo].[AttendancePolicies] ADD  CONSTRAINT [DF_AttendancePolicies_AllowSelfCorrection]  DEFAULT ((1)) FOR [AllowSelfCorrectionRequest]
GO
ALTER TABLE [dbo].[AttendancePolicies] ADD  CONSTRAINT [DF_AttendancePolicies_CreatedAtUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedAtUtc]
GO
ALTER TABLE [dbo].[AuditLogs] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[BlogCategories] ADD  CONSTRAINT [DF_BlogCategories_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[BlogPostMedia] ADD  CONSTRAINT [DF_BlogPostMedia_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[BlogPosts] ADD  CONSTRAINT [DF_BlogPosts_Status]  DEFAULT ('Draft') FOR [Status]
GO
ALTER TABLE [dbo].[BlogPosts] ADD  CONSTRAINT [DF_BlogPosts_IsFeatured]  DEFAULT ((0)) FOR [IsFeatured]
GO
ALTER TABLE [dbo].[BlogPosts] ADD  CONSTRAINT [DF_BlogPosts_ViewCount]  DEFAULT ((0)) FOR [ViewCount]
GO
ALTER TABLE [dbo].[BlogPosts] ADD  CONSTRAINT [DF_BlogPosts_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[BlogPosts] ADD  CONSTRAINT [DF_BlogPosts_PostType]  DEFAULT ('Blog') FOR [PostType]
GO
ALTER TABLE [dbo].[CareEpisodes] ADD  CONSTRAINT [DF_CareEpisodes_Status]  DEFAULT ('Open') FOR [Status]
GO
ALTER TABLE [dbo].[CareEpisodes] ADD  CONSTRAINT [DF_CareEpisodes_OpenedAtUtc]  DEFAULT (sysutcdatetime()) FOR [OpenedAtUtc]
GO
ALTER TABLE [dbo].[CareEpisodes] ADD  CONSTRAINT [DF_CareEpisodes_CreatedAtUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedAtUtc]
GO
ALTER TABLE [dbo].[ClinicalConsultations] ADD  CONSTRAINT [DF_ClinicalConsultations_Urgency]  DEFAULT ('Normal') FOR [Urgency]
GO
ALTER TABLE [dbo].[ClinicalConsultations] ADD  CONSTRAINT [DF_ClinicalConsultations_Status]  DEFAULT ('Requested') FOR [Status]
GO
ALTER TABLE [dbo].[ClinicalConsultations] ADD  CONSTRAINT [DF_ClinicalConsultations_RequestedAt]  DEFAULT (sysutcdatetime()) FOR [RequestedAtUtc]
GO
ALTER TABLE [dbo].[ClinicalEntries] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ClinicProfile] ADD  DEFAULT (sysdatetime()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[DentalChairs] ADD  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[DentalMedicalRecords] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[DentistAvailability] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[DentistDepartments] ADD  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[DentistDepartments] ADD  DEFAULT (sysdatetime()) FOR [AssignedAt]
GO
ALTER TABLE [dbo].[DentistLeaveRequests] ADD  CONSTRAINT [DF_DentistLeaveRequests_Status]  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[DentistLeaveRequests] ADD  CONSTRAINT [DF_DentistLeaveRequests_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[DentistProfiles] ADD  CONSTRAINT [DF_DentistProfiles_HasRadCert]  DEFAULT ((0)) FOR [HasRadiationSafetyCert]
GO
ALTER TABLE [dbo].[DentistWorkSchedules] ADD  DEFAULT ('Scheduled') FOR [Status]
GO
ALTER TABLE [dbo].[DepartmentReferrals] ADD  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[DepartmentReferrals] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[DepartmentReferrals] ADD  CONSTRAINT [DF_DepartmentReferrals_Urgency]  DEFAULT ('Normal') FOR [Urgency]
GO
ALTER TABLE [dbo].[Departments] ADD  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[Departments] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[DepartmentServices] ADD  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[DepartmentServices] ADD  CONSTRAINT [DF_DepartmentServices_ServiceType]  DEFAULT ('Treatment') FOR [ServiceType]
GO
ALTER TABLE [dbo].[DepartmentServices] ADD  CONSTRAINT [DF_DepartmentServices_RequiresExclusiveRoom]  DEFAULT ((0)) FOR [RequiresExclusiveRoom]
GO
ALTER TABLE [dbo].[DepartmentServices] ADD  CONSTRAINT [DF_DepartmentServices_RequiresConsultationFirst]  DEFAULT ((0)) FOR [RequiresConsultationFirst]
GO
ALTER TABLE [dbo].[DepartmentServices] ADD  CONSTRAINT [DF_DepartmentServices_MinLeadTimeHours]  DEFAULT ((2)) FOR [MinLeadTimeHours]
GO
ALTER TABLE [dbo].[DepartmentWorkSchedules] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[DiagnosticOrders] ADD  CONSTRAINT [DF_DiagnosticOrders_IsExternal]  DEFAULT ((0)) FOR [IsExternal]
GO
ALTER TABLE [dbo].[DiagnosticOrders] ADD  CONSTRAINT [DF_DiagnosticOrders_Status]  DEFAULT ('Ordered') FOR [Status]
GO
ALTER TABLE [dbo].[DiagnosticOrders] ADD  CONSTRAINT [DF_DiagnosticOrders_CreatedAtUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedAtUtc]
GO
ALTER TABLE [dbo].[DiagnosticResults] ADD  CONSTRAINT [DF_DiagnosticResults_Source]  DEFAULT ('Internal') FOR [SourceType]
GO
ALTER TABLE [dbo].[DiagnosticResults] ADD  CONSTRAINT [DF_DiagnosticResults_Format]  DEFAULT ('JPEG') FOR [ImageFormat]
GO
ALTER TABLE [dbo].[DiagnosticResults] ADD  CONSTRAINT [DF_DiagnosticResults_IsReviewed]  DEFAULT ((0)) FOR [IsReviewed]
GO
ALTER TABLE [dbo].[DiagnosticResults] ADD  CONSTRAINT [DF_DiagnosticResults_UploadedAtUtc]  DEFAULT (sysutcdatetime()) FOR [UploadedAtUtc]
GO
ALTER TABLE [dbo].[EpisodeServiceTasks] ADD  CONSTRAINT [DF_EpisodeServiceTasks_Status]  DEFAULT ('Planned') FOR [Status]
GO
ALTER TABLE [dbo].[EpisodeServiceTasks] ADD  CONSTRAINT [DF_EpisodeServiceTasks_Quantity]  DEFAULT ((1)) FOR [Quantity]
GO
ALTER TABLE [dbo].[EpisodeServiceTasks] ADD  CONSTRAINT [DF_EpisodeServiceTasks_EstimatedDurationMinutes]  DEFAULT ((30)) FOR [EstimatedDurationMinutes]
GO
ALTER TABLE [dbo].[EpisodeServiceTasks] ADD  CONSTRAINT [DF_EpisodeServiceTasks_PlannedAtUtc]  DEFAULT (sysutcdatetime()) FOR [PlannedAtUtc]
GO
ALTER TABLE [dbo].[EpisodeServiceTasks] ADD  CONSTRAINT [DF_EpisodeServiceTasks_CreatedAtUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedAtUtc]
GO
ALTER TABLE [dbo].[FollowUpSchedules] ADD  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[ImagingEquipments] ADD  CONSTRAINT [DF_ImagingEquipments_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[ImagingEquipments] ADD  CONSTRAINT [DF_ImagingEquipments_OperationalStatus]  DEFAULT ('Operational') FOR [OperationalStatus]
GO
ALTER TABLE [dbo].[ImagingEquipments] ADD  CONSTRAINT [DF_ImagingEquipments_AvailableFrom]  DEFAULT ('07:00:00') FOR [AvailableFrom]
GO
ALTER TABLE [dbo].[ImagingEquipments] ADD  CONSTRAINT [DF_ImagingEquipments_AvailableTo]  DEFAULT ('21:00:00') FOR [AvailableTo]
GO
ALTER TABLE [dbo].[ImagingEquipments] ADD  CONSTRAINT [DF_ImagingEquipments_CreatedAtUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedAtUtc]
GO
ALTER TABLE [dbo].[ImagingRecords] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ImagingServiceConfigs] ADD  CONSTRAINT [DF_ImagingServiceConfigs_Buffer]  DEFAULT ((0)) FOR [BufferMinutes]
GO
ALTER TABLE [dbo].[ImagingServiceConfigs] ADD  CONSTRAINT [DF_ImagingServiceConfigs_Price]  DEFAULT ((0)) FOR [Price]
GO
ALTER TABLE [dbo].[ImagingServiceConfigs] ADD  CONSTRAINT [DF_ImagingServiceConfigs_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[ImagingSessions] ADD  CONSTRAINT [DF_ImagingSessions_Priority]  DEFAULT ('Normal') FOR [Priority]
GO
ALTER TABLE [dbo].[ImagingSessions] ADD  CONSTRAINT [DF_ImagingSessions_Status]  DEFAULT ('Scheduled') FOR [Status]
GO
ALTER TABLE [dbo].[ImagingSessions] ADD  CONSTRAINT [DF_ImagingSessions_DelayNotif]  DEFAULT ((0)) FOR [DelayNotificationSent]
GO
ALTER TABLE [dbo].[ImagingSessions] ADD  CONSTRAINT [DF_ImagingSessions_EscalationAlert]  DEFAULT ((0)) FOR [EscalationAlertSent]
GO
ALTER TABLE [dbo].[ImagingSessions] ADD  CONSTRAINT [DF_ImagingSessions_CreatedAtUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedAtUtc]
GO
ALTER TABLE [dbo].[InvoiceItems] ADD  DEFAULT ((1)) FOR [Quantity]
GO
ALTER TABLE [dbo].[Invoices] ADD  DEFAULT ('Unpaid') FOR [Status]
GO
ALTER TABLE [dbo].[Invoices] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[MedicineBatches] ADD  CONSTRAINT [DF_MedicineBatches_CostPrice]  DEFAULT ((0)) FOR [CostPrice]
GO
ALTER TABLE [dbo].[MedicineBatches] ADD  CONSTRAINT [DF_MedicineBatches_Status]  DEFAULT ('Available') FOR [Status]
GO
ALTER TABLE [dbo].[MedicineBatches] ADD  CONSTRAINT [DF_MedicineBatches_ReceivedDate]  DEFAULT (sysutcdatetime()) FOR [ReceivedDate]
GO
ALTER TABLE [dbo].[MedicineBatches] ADD  CONSTRAINT [DF_MedicineBatches_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[MedicineDispensationItems] ADD  CONSTRAINT [DF_MedicineDispensationItems_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[MedicineDispensations] ADD  CONSTRAINT [DF_MedicineDispensations_DispensedAt]  DEFAULT (sysutcdatetime()) FOR [DispensedAt]
GO
ALTER TABLE [dbo].[MedicineDispensations] ADD  CONSTRAINT [DF_MedicineDispensations_Status]  DEFAULT ('Dispensed') FOR [Status]
GO
ALTER TABLE [dbo].[MedicineDispensations] ADD  CONSTRAINT [DF_MedicineDispensations_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Medicines] ADD  CONSTRAINT [DF_Medicines_MinAlert]  DEFAULT ((20)) FOR [MinimumStockAlert]
GO
ALTER TABLE [dbo].[Medicines] ADD  CONSTRAINT [DF_Medicines_Status]  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[Medicines] ADD  CONSTRAINT [DF_Medicines_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[MedicineStockTransactions] ADD  CONSTRAINT [DF_MedicineStockTransactions_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Notifications] ADD  DEFAULT ('InApp') FOR [Channel]
GO
ALTER TABLE [dbo].[Notifications] ADD  DEFAULT ('Pending') FOR [DeliveryStatus]
GO
ALTER TABLE [dbo].[PatientProfiles] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Payments] ADD  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[Payments] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[PerformedServices] ADD  DEFAULT ((1)) FOR [Quantity]
GO
ALTER TABLE [dbo].[Prescriptions] ADD  DEFAULT (sysdatetime()) FOR [IssuedAt]
GO
ALTER TABLE [dbo].[ReceptionistCoverageIncidents] ADD  CONSTRAINT [DF_ReceptionistCoverageIncidents_MinStaff]  DEFAULT ((2)) FOR [RequiredMinimumCount]
GO
ALTER TABLE [dbo].[ReceptionistCoverageIncidents] ADD  CONSTRAINT [DF_ReceptionistCoverageIncidents_ReportedAt]  DEFAULT (sysutcdatetime()) FOR [ReportedAt]
GO
ALTER TABLE [dbo].[ReceptionistCoverageIncidents] ADD  CONSTRAINT [DF_ReceptionistCoverageIncidents_Status]  DEFAULT ('Open') FOR [Status]
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequestPeriods] ADD  CONSTRAINT [DF_ReceptionistLeaveRequestPeriods_Status]  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequests] ADD  CONSTRAINT [DF_ReceptionistLeaveRequests_Status]  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequests] ADD  CONSTRAINT [DF_ReceptionistLeaveRequests_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests] ADD  CONSTRAINT [DF_ReceptionistShiftSwapRequests_Status]  DEFAULT ('PendingCounterparty') FOR [Status]
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests] ADD  CONSTRAINT [DF_ReceptionistShiftSwapRequests_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ReceptionistShiftTemplates] ADD  CONSTRAINT [DF_ReceptionistShiftTemplates_MinStaff]  DEFAULT ((2)) FOR [MinStaffRequired]
GO
ALTER TABLE [dbo].[ReceptionistShiftTemplates] ADD  CONSTRAINT [DF_ReceptionistShiftTemplates_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[ReceptionistShiftTemplates] ADD  CONSTRAINT [DF_ReceptionistShiftTemplates_DisplayOrder]  DEFAULT ((1)) FOR [DisplayOrder]
GO
ALTER TABLE [dbo].[ReceptionistShiftTemplates] ADD  CONSTRAINT [DF_ReceptionistShiftTemplates_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ReceptionistWorkHandovers] ADD  CONSTRAINT [DF_ReceptionistWorkHandovers_PendingAppts]  DEFAULT ((0)) FOR [PendingAppointmentCount]
GO
ALTER TABLE [dbo].[ReceptionistWorkHandovers] ADD  CONSTRAINT [DF_ReceptionistWorkHandovers_UnpaidInvoices]  DEFAULT ((0)) FOR [UnpaidInvoiceCount]
GO
ALTER TABLE [dbo].[ReceptionistWorkHandovers] ADD  CONSTRAINT [DF_ReceptionistWorkHandovers_ActiveChats]  DEFAULT ((0)) FOR [ActiveChatCount]
GO
ALTER TABLE [dbo].[ReceptionistWorkHandovers] ADD  CONSTRAINT [DF_ReceptionistWorkHandovers_Status]  DEFAULT ('Transferred') FOR [HandoverStatus]
GO
ALTER TABLE [dbo].[ReceptionistWorkHandovers] ADD  CONSTRAINT [DF_ReceptionistWorkHandovers_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ReceptionistWorkSchedules] ADD  CONSTRAINT [DF_ReceptionistWorkSchedules_Status]  DEFAULT ('Scheduled') FOR [Status]
GO
ALTER TABLE [dbo].[ReceptionistWorkSchedules] ADD  CONSTRAINT [DF_ReceptionistWorkSchedules_IsPublished]  DEFAULT ((1)) FOR [IsPublished]
GO
ALTER TABLE [dbo].[ReceptionistWorkSchedules] ADD  CONSTRAINT [DF_ReceptionistWorkSchedules_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Rooms] ADD  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[Rooms] ADD  CONSTRAINT [DF_Rooms_IsExclusive]  DEFAULT ((0)) FOR [IsExclusive]
GO
ALTER TABLE [dbo].[Rooms] ADD  CONSTRAINT [DF_Rooms_RoomType]  DEFAULT ('Treatment') FOR [RoomType]
GO
ALTER TABLE [dbo].[Rooms] ADD  CONSTRAINT [DF_Rooms_MaxConcurrentPatients]  DEFAULT ((1)) FOR [MaxConcurrentPatients]
GO
ALTER TABLE [dbo].[Rooms] ADD  CONSTRAINT [DF_Rooms_MaxWaitingPatients]  DEFAULT ((3)) FOR [MaxWaitingPatients]
GO
ALTER TABLE [dbo].[Rooms] ADD  CONSTRAINT [DF_Rooms_TargetMaxWaitMinutes]  DEFAULT ((20)) FOR [TargetMaxWaitMinutes]
GO
ALTER TABLE [dbo].[Rooms] ADD  CONSTRAINT [DF_Rooms_EscalationWaitMinutes]  DEFAULT ((30)) FOR [EscalationWaitMinutes]
GO
ALTER TABLE [dbo].[ServiceCombos] ADD  DEFAULT ((45)) FOR [DurationMinutes]
GO
ALTER TABLE [dbo].[ServiceCombos] ADD  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[ServiceCombos] ADD  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[StaffAttendanceCorrectionRequests] ADD  CONSTRAINT [DF_StaffAttendanceCorrection_Status]  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[StaffAttendanceCorrectionRequests] ADD  CONSTRAINT [DF_StaffAttendanceCorrection_CreatedAtUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedAtUtc]
GO
ALTER TABLE [dbo].[StaffAttendanceEvents] ADD  CONSTRAINT [DF_StaffAttendanceEvents_EventTimeUtc]  DEFAULT (sysutcdatetime()) FOR [EventTimeUtc]
GO
ALTER TABLE [dbo].[StaffAttendanceEvents] ADD  CONSTRAINT [DF_StaffAttendanceEvents_Source]  DEFAULT ('WebPortal') FOR [Source]
GO
ALTER TABLE [dbo].[StaffAttendanceRecords] ADD  CONSTRAINT [DF_StaffAttendanceRecords_WorkedMins]  DEFAULT ((0)) FOR [ActualWorkedMinutes]
GO
ALTER TABLE [dbo].[StaffAttendanceRecords] ADD  CONSTRAINT [DF_StaffAttendanceRecords_LateMins]  DEFAULT ((0)) FOR [LateMinutes]
GO
ALTER TABLE [dbo].[StaffAttendanceRecords] ADD  CONSTRAINT [DF_StaffAttendanceRecords_EarlyMins]  DEFAULT ((0)) FOR [EarlyDepartureMinutes]
GO
ALTER TABLE [dbo].[StaffAttendanceRecords] ADD  CONSTRAINT [DF_StaffAttendanceRecords_Status]  DEFAULT ('NotStarted') FOR [Status]
GO
ALTER TABLE [dbo].[StaffAttendanceRecords] ADD  CONSTRAINT [DF_StaffAttendanceRecords_IsLeave]  DEFAULT ((0)) FOR [IsApprovedLeave]
GO
ALTER TABLE [dbo].[StaffAttendanceRecords] ADD  CONSTRAINT [DF_StaffAttendanceRecords_LeaveHours]  DEFAULT ((0)) FOR [LeaveHours]
GO
ALTER TABLE [dbo].[StaffAttendanceRecords] ADD  CONSTRAINT [DF_StaffAttendanceRecords_IsFinalized]  DEFAULT ((0)) FOR [IsFinalized]
GO
ALTER TABLE [dbo].[StaffAttendanceRecords] ADD  CONSTRAINT [DF_StaffAttendanceRecords_CreatedAtUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedAtUtc]
GO
ALTER TABLE [dbo].[StaffProfiles] ADD  DEFAULT ('Active') FOR [EmploymentStatus]
GO
ALTER TABLE [dbo].[StaffProfiles] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[SupportChatMessages] ADD  DEFAULT (sysdatetime()) FOR [SentAt]
GO
ALTER TABLE [dbo].[SupportChatMessages] ADD  DEFAULT ((0)) FOR [IsRead]
GO
ALTER TABLE [dbo].[TreatmentPlanItems] ADD  DEFAULT ('Planned') FOR [Status]
GO
ALTER TABLE [dbo].[TreatmentPlans] ADD  DEFAULT ('Planned') FOR [Status]
GO
ALTER TABLE [dbo].[TreatmentPlans] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[TreatmentSessions] ADD  DEFAULT (sysdatetime()) FOR [SessionDate]
GO
ALTER TABLE [dbo].[UserAccounts] ADD  DEFAULT ('Unverified') FOR [Status]
GO
ALTER TABLE [dbo].[UserAccounts] ADD  DEFAULT ((0)) FOR [FailedLoginCount]
GO
ALTER TABLE [dbo].[UserAccounts] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[VisitFeedback] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[VisitFeedback] ADD  CONSTRAINT [DF_VisitFeedback_Status]  DEFAULT ('Approved') FOR [Status]
GO
ALTER TABLE [dbo].[XrayDentistRosters] ADD  CONSTRAINT [DF_XrayDentistRosters_ShiftType]  DEFAULT (N'Morning') FOR [ShiftType]
GO
ALTER TABLE [dbo].[XrayDentistRosters] ADD  CONSTRAINT [DF_XrayDentistRosters_Status]  DEFAULT (N'Active') FOR [Status]
GO
ALTER TABLE [dbo].[XrayDentistRosters] ADD  CONSTRAINT [DF_XrayDentistRosters_CreatedAtUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedAtUtc]
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
ALTER TABLE [dbo].[AppointmentDisruptions]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentDisruptions_Appointment] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[AppointmentDisruptions] CHECK CONSTRAINT [FK_AppointmentDisruptions_Appointment]
GO
ALTER TABLE [dbo].[AppointmentDisruptions]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentDisruptions_LeaveRequest] FOREIGN KEY([DentistLeaveRequestId])
REFERENCES [dbo].[DentistLeaveRequests] ([LeaveRequestId])
GO
ALTER TABLE [dbo].[AppointmentDisruptions] CHECK CONSTRAINT [FK_AppointmentDisruptions_LeaveRequest]
GO
ALTER TABLE [dbo].[AppointmentDisruptions]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentDisruptions_NewDentist] FOREIGN KEY([NewDentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[AppointmentDisruptions] CHECK CONSTRAINT [FK_AppointmentDisruptions_NewDentist]
GO
ALTER TABLE [dbo].[AppointmentDisruptions]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentDisruptions_OriginalDentist] FOREIGN KEY([OriginalDentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[AppointmentDisruptions] CHECK CONSTRAINT [FK_AppointmentDisruptions_OriginalDentist]
GO
ALTER TABLE [dbo].[AppointmentDisruptions]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentDisruptions_ResolvedAppointment] FOREIGN KEY([ResolvedAppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[AppointmentDisruptions] CHECK CONSTRAINT [FK_AppointmentDisruptions_ResolvedAppointment]
GO
ALTER TABLE [dbo].[AppointmentDisruptions]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentDisruptions_ResolvedByUser] FOREIGN KEY([ResolvedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[AppointmentDisruptions] CHECK CONSTRAINT [FK_AppointmentDisruptions_ResolvedByUser]
GO
ALTER TABLE [dbo].[AppointmentReminderJobs]  WITH CHECK ADD  CONSTRAINT [FK_AppointmentReminderJobs_Appointments] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AppointmentReminderJobs] CHECK CONSTRAINT [FK_AppointmentReminderJobs_Appointments]
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [FK_Appointments_AssignedDentist] FOREIGN KEY([AssignedDentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [FK_Appointments_AssignedDentist]
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [FK_Appointments_CareEpisodes] FOREIGN KEY([CareEpisodeId])
REFERENCES [dbo].[CareEpisodes] ([CareEpisodeId])
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [FK_Appointments_CareEpisodes]
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
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [FK_Appointments_ServiceCombos] FOREIGN KEY([ComboId])
REFERENCES [dbo].[ServiceCombos] ([ComboId])
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [FK_Appointments_ServiceCombos]
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [FK_Appointments_TreatmentPlanItems_TreatmentPlanItemId] FOREIGN KEY([TreatmentPlanItemId])
REFERENCES [dbo].[TreatmentPlanItems] ([TreatmentPlanItemId])
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [FK_Appointments_TreatmentPlanItems_TreatmentPlanItemId]
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
ALTER TABLE [dbo].[AttendancePeriodClosures]  WITH CHECK ADD  CONSTRAINT [FK_AttendancePeriodClosures_Finalizer] FOREIGN KEY([FinalizedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[AttendancePeriodClosures] CHECK CONSTRAINT [FK_AttendancePeriodClosures_Finalizer]
GO
ALTER TABLE [dbo].[AttendancePeriodClosures]  WITH CHECK ADD  CONSTRAINT [FK_AttendancePeriodClosures_Reopener] FOREIGN KEY([ReopenedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[AttendancePeriodClosures] CHECK CONSTRAINT [FK_AttendancePeriodClosures_Reopener]
GO
ALTER TABLE [dbo].[AuditLogs]  WITH CHECK ADD  CONSTRAINT [FK_AuditLogs_User] FOREIGN KEY([UserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[AuditLogs] CHECK CONSTRAINT [FK_AuditLogs_User]
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
ALTER TABLE [dbo].[BlogPosts]  WITH CHECK ADD  CONSTRAINT [FK_BlogPosts_ApprovedByUser] FOREIGN KEY([ApprovedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[BlogPosts] CHECK CONSTRAINT [FK_BlogPosts_ApprovedByUser]
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
ALTER TABLE [dbo].[CareEpisodes]  WITH CHECK ADD  CONSTRAINT [FK_CareEpisodes_PatientUser] FOREIGN KEY([PatientUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[CareEpisodes] CHECK CONSTRAINT [FK_CareEpisodes_PatientUser]
GO
ALTER TABLE [dbo].[CareEpisodes]  WITH CHECK ADD  CONSTRAINT [FK_CareEpisodes_RootAppointment] FOREIGN KEY([RootAppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[CareEpisodes] CHECK CONSTRAINT [FK_CareEpisodes_RootAppointment]
GO
ALTER TABLE [dbo].[ClinicalConsultations]  WITH CHECK ADD  CONSTRAINT [FK_ClinicalConsultations_Appointment] FOREIGN KEY([SourceAppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[ClinicalConsultations] CHECK CONSTRAINT [FK_ClinicalConsultations_Appointment]
GO
ALTER TABLE [dbo].[ClinicalConsultations]  WITH CHECK ADD  CONSTRAINT [FK_ClinicalConsultations_ConsultedDentist] FOREIGN KEY([ConsultedDentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[ClinicalConsultations] CHECK CONSTRAINT [FK_ClinicalConsultations_ConsultedDentist]
GO
ALTER TABLE [dbo].[ClinicalConsultations]  WITH CHECK ADD  CONSTRAINT [FK_ClinicalConsultations_Patient] FOREIGN KEY([PatientUserId])
REFERENCES [dbo].[PatientProfiles] ([UserId])
GO
ALTER TABLE [dbo].[ClinicalConsultations] CHECK CONSTRAINT [FK_ClinicalConsultations_Patient]
GO
ALTER TABLE [dbo].[ClinicalConsultations]  WITH CHECK ADD  CONSTRAINT [FK_ClinicalConsultations_RequestingDentist] FOREIGN KEY([RequestingDentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[ClinicalConsultations] CHECK CONSTRAINT [FK_ClinicalConsultations_RequestingDentist]
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
ALTER TABLE [dbo].[DentalChairs]  WITH CHECK ADD  CONSTRAINT [FK_DentalChairs_Room] FOREIGN KEY([RoomId])
REFERENCES [dbo].[Rooms] ([RoomId])
GO
ALTER TABLE [dbo].[DentalChairs] CHECK CONSTRAINT [FK_DentalChairs_Room]
GO
ALTER TABLE [dbo].[DentalMedicalRecords]  WITH CHECK ADD  CONSTRAINT [FK_DentalMedicalRecords_Patient] FOREIGN KEY([PatientUserId])
REFERENCES [dbo].[PatientProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DentalMedicalRecords] CHECK CONSTRAINT [FK_DentalMedicalRecords_Patient]
GO
ALTER TABLE [dbo].[DentistAvailability]  WITH CHECK ADD  CONSTRAINT [FK_DentistAvailability_Dentist] FOREIGN KEY([DentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DentistAvailability] CHECK CONSTRAINT [FK_DentistAvailability_Dentist]
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
ALTER TABLE [dbo].[DentistLeaveRequests]  WITH CHECK ADD  CONSTRAINT [FK_DentistLeaveRequests_Dentist] FOREIGN KEY([DentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DentistLeaveRequests] CHECK CONSTRAINT [FK_DentistLeaveRequests_Dentist]
GO
ALTER TABLE [dbo].[DentistLeaveRequests]  WITH CHECK ADD  CONSTRAINT [FK_DentistLeaveRequests_ReviewedByUser] FOREIGN KEY([ReviewedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[DentistLeaveRequests] CHECK CONSTRAINT [FK_DentistLeaveRequests_ReviewedByUser]
GO
ALTER TABLE [dbo].[DentistProfiles]  WITH CHECK ADD  CONSTRAINT [FK_DentistProfiles_StaffProfiles] FOREIGN KEY([UserId])
REFERENCES [dbo].[StaffProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DentistProfiles] CHECK CONSTRAINT [FK_DentistProfiles_StaffProfiles]
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
ALTER TABLE [dbo].[DepartmentReferrals]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentReferrals_AssignedDentist] FOREIGN KEY([AssignedDentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DepartmentReferrals] CHECK CONSTRAINT [FK_DepartmentReferrals_AssignedDentist]
GO
ALTER TABLE [dbo].[DepartmentReferrals]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentReferrals_CareEpisodes] FOREIGN KEY([CareEpisodeId])
REFERENCES [dbo].[CareEpisodes] ([CareEpisodeId])
GO
ALTER TABLE [dbo].[DepartmentReferrals] CHECK CONSTRAINT [FK_DepartmentReferrals_CareEpisodes]
GO
ALTER TABLE [dbo].[DepartmentReferrals]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentReferrals_DepartmentServices] FOREIGN KEY([RequestedServiceId])
REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId])
GO
ALTER TABLE [dbo].[DepartmentReferrals] CHECK CONSTRAINT [FK_DepartmentReferrals_DepartmentServices]
GO
ALTER TABLE [dbo].[DepartmentReferrals]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentReferrals_DestinationAppointment] FOREIGN KEY([DestinationAppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[DepartmentReferrals] CHECK CONSTRAINT [FK_DepartmentReferrals_DestinationAppointment]
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
ALTER TABLE [dbo].[DepartmentReferrals]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentReferrals_SourceAppointment] FOREIGN KEY([SourceAppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[DepartmentReferrals] CHECK CONSTRAINT [FK_DepartmentReferrals_SourceAppointment]
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
ALTER TABLE [dbo].[Departments]  WITH CHECK ADD  CONSTRAINT [FK_Departments_ManagerUser] FOREIGN KEY([ManagerUserId])
REFERENCES [dbo].[StaffProfiles] ([UserId])
GO
ALTER TABLE [dbo].[Departments] CHECK CONSTRAINT [FK_Departments_ManagerUser]
GO
ALTER TABLE [dbo].[DepartmentServices]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentServices_Department] FOREIGN KEY([DepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[DepartmentServices] CHECK CONSTRAINT [FK_DepartmentServices_Department]
GO
ALTER TABLE [dbo].[DepartmentWorkSchedules]  WITH CHECK ADD  CONSTRAINT [FK_DepartmentWorkSchedules_Department] FOREIGN KEY([DepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[DepartmentWorkSchedules] CHECK CONSTRAINT [FK_DepartmentWorkSchedules_Department]
GO
ALTER TABLE [dbo].[DiagnosticOrders]  WITH CHECK ADD  CONSTRAINT [FK_DiagnosticOrders_Appointment] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[DiagnosticOrders] CHECK CONSTRAINT [FK_DiagnosticOrders_Appointment]
GO
ALTER TABLE [dbo].[DiagnosticOrders]  WITH CHECK ADD  CONSTRAINT [FK_DiagnosticOrders_Dentist] FOREIGN KEY([DentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DiagnosticOrders] CHECK CONSTRAINT [FK_DiagnosticOrders_Dentist]
GO
ALTER TABLE [dbo].[DiagnosticOrders]  WITH CHECK ADD  CONSTRAINT [FK_DiagnosticOrders_Patient] FOREIGN KEY([PatientUserId])
REFERENCES [dbo].[PatientProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DiagnosticOrders] CHECK CONSTRAINT [FK_DiagnosticOrders_Patient]
GO
ALTER TABLE [dbo].[DiagnosticResults]  WITH CHECK ADD  CONSTRAINT [FK_DiagnosticResults_Equipment] FOREIGN KEY([EquipmentId])
REFERENCES [dbo].[ImagingEquipments] ([EquipmentId])
GO
ALTER TABLE [dbo].[DiagnosticResults] CHECK CONSTRAINT [FK_DiagnosticResults_Equipment]
GO
ALTER TABLE [dbo].[DiagnosticResults]  WITH CHECK ADD  CONSTRAINT [FK_DiagnosticResults_Order] FOREIGN KEY([DiagnosticOrderId])
REFERENCES [dbo].[DiagnosticOrders] ([DiagnosticOrderId])
GO
ALTER TABLE [dbo].[DiagnosticResults] CHECK CONSTRAINT [FK_DiagnosticResults_Order]
GO
ALTER TABLE [dbo].[DiagnosticResults]  WITH CHECK ADD  CONSTRAINT [FK_DiagnosticResults_Reviewer] FOREIGN KEY([ReviewedByDentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[DiagnosticResults] CHECK CONSTRAINT [FK_DiagnosticResults_Reviewer]
GO
ALTER TABLE [dbo].[DiagnosticResults]  WITH CHECK ADD  CONSTRAINT [FK_DiagnosticResults_Session] FOREIGN KEY([ImagingSessionId])
REFERENCES [dbo].[ImagingSessions] ([ImagingSessionId])
GO
ALTER TABLE [dbo].[DiagnosticResults] CHECK CONSTRAINT [FK_DiagnosticResults_Session]
GO
ALTER TABLE [dbo].[DiagnosticResults]  WITH CHECK ADD  CONSTRAINT [FK_DiagnosticResults_UploadedBy] FOREIGN KEY([UploadedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[DiagnosticResults] CHECK CONSTRAINT [FK_DiagnosticResults_UploadedBy]
GO
ALTER TABLE [dbo].[EpisodeServiceTasks]  WITH CHECK ADD  CONSTRAINT [FK_EpisodeServiceTasks_Appointments] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[EpisodeServiceTasks] CHECK CONSTRAINT [FK_EpisodeServiceTasks_Appointments]
GO
ALTER TABLE [dbo].[EpisodeServiceTasks]  WITH CHECK ADD  CONSTRAINT [FK_EpisodeServiceTasks_CareEpisodes] FOREIGN KEY([CareEpisodeId])
REFERENCES [dbo].[CareEpisodes] ([CareEpisodeId])
GO
ALTER TABLE [dbo].[EpisodeServiceTasks] CHECK CONSTRAINT [FK_EpisodeServiceTasks_CareEpisodes]
GO
ALTER TABLE [dbo].[EpisodeServiceTasks]  WITH CHECK ADD  CONSTRAINT [FK_EpisodeServiceTasks_DentistUser] FOREIGN KEY([PerformingDentistUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[EpisodeServiceTasks] CHECK CONSTRAINT [FK_EpisodeServiceTasks_DentistUser]
GO
ALTER TABLE [dbo].[EpisodeServiceTasks]  WITH CHECK ADD  CONSTRAINT [FK_EpisodeServiceTasks_DepartmentServices] FOREIGN KEY([DepartmentServiceId])
REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId])
GO
ALTER TABLE [dbo].[EpisodeServiceTasks] CHECK CONSTRAINT [FK_EpisodeServiceTasks_DepartmentServices]
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
ALTER TABLE [dbo].[ImagingEquipments]  WITH CHECK ADD  CONSTRAINT [FK_ImagingEquipments_Room] FOREIGN KEY([RoomId])
REFERENCES [dbo].[Rooms] ([RoomId])
GO
ALTER TABLE [dbo].[ImagingEquipments] CHECK CONSTRAINT [FK_ImagingEquipments_Room]
GO
ALTER TABLE [dbo].[ImagingRecords]  WITH CHECK ADD  CONSTRAINT [FK_ImagingRecords_ClinicalEntry] FOREIGN KEY([ClinicalEntryId])
REFERENCES [dbo].[ClinicalEntries] ([ClinicalEntryId])
GO
ALTER TABLE [dbo].[ImagingRecords] CHECK CONSTRAINT [FK_ImagingRecords_ClinicalEntry]
GO
ALTER TABLE [dbo].[ImagingServiceConfigs]  WITH CHECK ADD  CONSTRAINT [FK_ImagingServiceConfigs_DepartmentService] FOREIGN KEY([DepartmentServiceId])
REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId])
GO
ALTER TABLE [dbo].[ImagingServiceConfigs] CHECK CONSTRAINT [FK_ImagingServiceConfigs_DepartmentService]
GO
ALTER TABLE [dbo].[ImagingSessions]  WITH CHECK ADD  CONSTRAINT [FK_ImagingSessions_Appointment] FOREIGN KEY([SourceAppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[ImagingSessions] CHECK CONSTRAINT [FK_ImagingSessions_Appointment]
GO
ALTER TABLE [dbo].[ImagingSessions]  WITH CHECK ADD  CONSTRAINT [FK_ImagingSessions_CreatedBy] FOREIGN KEY([CreatedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ImagingSessions] CHECK CONSTRAINT [FK_ImagingSessions_CreatedBy]
GO
ALTER TABLE [dbo].[ImagingSessions]  WITH CHECK ADD  CONSTRAINT [FK_ImagingSessions_Equipment] FOREIGN KEY([EquipmentId])
REFERENCES [dbo].[ImagingEquipments] ([EquipmentId])
GO
ALTER TABLE [dbo].[ImagingSessions] CHECK CONSTRAINT [FK_ImagingSessions_Equipment]
GO
ALTER TABLE [dbo].[ImagingSessions]  WITH CHECK ADD  CONSTRAINT [FK_ImagingSessions_Operator] FOREIGN KEY([AssignedOperatorUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ImagingSessions] CHECK CONSTRAINT [FK_ImagingSessions_Operator]
GO
ALTER TABLE [dbo].[ImagingSessions]  WITH CHECK ADD  CONSTRAINT [FK_ImagingSessions_Order] FOREIGN KEY([DiagnosticOrderId])
REFERENCES [dbo].[DiagnosticOrders] ([DiagnosticOrderId])
GO
ALTER TABLE [dbo].[ImagingSessions] CHECK CONSTRAINT [FK_ImagingSessions_Order]
GO
ALTER TABLE [dbo].[ImagingSessions]  WITH CHECK ADD  CONSTRAINT [FK_ImagingSessions_Patient] FOREIGN KEY([PatientUserId])
REFERENCES [dbo].[PatientProfiles] ([UserId])
GO
ALTER TABLE [dbo].[ImagingSessions] CHECK CONSTRAINT [FK_ImagingSessions_Patient]
GO
ALTER TABLE [dbo].[ImagingSessions]  WITH CHECK ADD  CONSTRAINT [FK_ImagingSessions_Room] FOREIGN KEY([ImagingRoomId])
REFERENCES [dbo].[Rooms] ([RoomId])
GO
ALTER TABLE [dbo].[ImagingSessions] CHECK CONSTRAINT [FK_ImagingSessions_Room]
GO
ALTER TABLE [dbo].[InvoiceItems]  WITH CHECK ADD  CONSTRAINT [FK_InvoiceItems_Appointments] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[InvoiceItems] CHECK CONSTRAINT [FK_InvoiceItems_Appointments]
GO
ALTER TABLE [dbo].[InvoiceItems]  WITH CHECK ADD  CONSTRAINT [FK_InvoiceItems_DentistUser] FOREIGN KEY([PerformingDentistUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[InvoiceItems] CHECK CONSTRAINT [FK_InvoiceItems_DentistUser]
GO
ALTER TABLE [dbo].[InvoiceItems]  WITH CHECK ADD  CONSTRAINT [FK_InvoiceItems_EpisodeServiceTasks] FOREIGN KEY([ServiceTaskId])
REFERENCES [dbo].[EpisodeServiceTasks] ([ServiceTaskId])
GO
ALTER TABLE [dbo].[InvoiceItems] CHECK CONSTRAINT [FK_InvoiceItems_EpisodeServiceTasks]
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
ALTER TABLE [dbo].[Invoices]  WITH CHECK ADD  CONSTRAINT [FK_Invoices_Appointment] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[Invoices] CHECK CONSTRAINT [FK_Invoices_Appointment]
GO
ALTER TABLE [dbo].[Invoices]  WITH CHECK ADD  CONSTRAINT [FK_Invoices_CareEpisodes] FOREIGN KEY([CareEpisodeId])
REFERENCES [dbo].[CareEpisodes] ([CareEpisodeId])
GO
ALTER TABLE [dbo].[Invoices] CHECK CONSTRAINT [FK_Invoices_CareEpisodes]
GO
ALTER TABLE [dbo].[Invoices]  WITH CHECK ADD  CONSTRAINT [FK_Invoices_GeneratedByUser] FOREIGN KEY([GeneratedByUserId])
REFERENCES [dbo].[StaffProfiles] ([UserId])
GO
ALTER TABLE [dbo].[Invoices] CHECK CONSTRAINT [FK_Invoices_GeneratedByUser]
GO
ALTER TABLE [dbo].[MedicineBatches]  WITH CHECK ADD  CONSTRAINT [FK_MedicineBatches_Medicines] FOREIGN KEY([MedicineId])
REFERENCES [dbo].[Medicines] ([MedicineId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[MedicineBatches] CHECK CONSTRAINT [FK_MedicineBatches_Medicines]
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
ALTER TABLE [dbo].[OdontogramEntries]  WITH CHECK ADD  CONSTRAINT [FK_OdontogramEntries_ClinicalEntry] FOREIGN KEY([ClinicalEntryId])
REFERENCES [dbo].[ClinicalEntries] ([ClinicalEntryId])
GO
ALTER TABLE [dbo].[OdontogramEntries] CHECK CONSTRAINT [FK_OdontogramEntries_ClinicalEntry]
GO
ALTER TABLE [dbo].[PatientProfiles]  WITH CHECK ADD  CONSTRAINT [FK_PatientProfiles_UserAccounts] FOREIGN KEY([UserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[PatientProfiles] CHECK CONSTRAINT [FK_PatientProfiles_UserAccounts]
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
ALTER TABLE [dbo].[PrescriptionItems]  WITH CHECK ADD  CONSTRAINT [FK_PrescriptionItems_Prescription] FOREIGN KEY([PrescriptionId])
REFERENCES [dbo].[Prescriptions] ([PrescriptionId])
GO
ALTER TABLE [dbo].[PrescriptionItems] CHECK CONSTRAINT [FK_PrescriptionItems_Prescription]
GO
ALTER TABLE [dbo].[Prescriptions]  WITH CHECK ADD  CONSTRAINT [FK_Prescriptions_TreatmentSession] FOREIGN KEY([TreatmentSessionId])
REFERENCES [dbo].[TreatmentSessions] ([TreatmentSessionId])
GO
ALTER TABLE [dbo].[Prescriptions] CHECK CONSTRAINT [FK_Prescriptions_TreatmentSession]
GO
ALTER TABLE [dbo].[ReceptionistCoverageIncidents]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistCoverageIncidents_AffectedUser] FOREIGN KEY([AffectedReceptionistUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ReceptionistCoverageIncidents] CHECK CONSTRAINT [FK_ReceptionistCoverageIncidents_AffectedUser]
GO
ALTER TABLE [dbo].[ReceptionistCoverageIncidents]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistCoverageIncidents_ReplacementUser] FOREIGN KEY([ReplacementReceptionistUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ReceptionistCoverageIncidents] CHECK CONSTRAINT [FK_ReceptionistCoverageIncidents_ReplacementUser]
GO
ALTER TABLE [dbo].[ReceptionistCoverageIncidents]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistCoverageIncidents_ReportedBy] FOREIGN KEY([ReportedBy])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ReceptionistCoverageIncidents] CHECK CONSTRAINT [FK_ReceptionistCoverageIncidents_ReportedBy]
GO
ALTER TABLE [dbo].[ReceptionistCoverageIncidents]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistCoverageIncidents_ResolvedBy] FOREIGN KEY([ResolvedBy])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ReceptionistCoverageIncidents] CHECK CONSTRAINT [FK_ReceptionistCoverageIncidents_ResolvedBy]
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequestPeriods]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistLeaveRequestPeriods_LeaveRequest] FOREIGN KEY([LeaveRequestId])
REFERENCES [dbo].[ReceptionistLeaveRequests] ([LeaveRequestId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequestPeriods] CHECK CONSTRAINT [FK_ReceptionistLeaveRequestPeriods_LeaveRequest]
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequests]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistLeaveRequests_UserAccounts_ApprovedBy] FOREIGN KEY([ApprovedBy])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequests] CHECK CONSTRAINT [FK_ReceptionistLeaveRequests_UserAccounts_ApprovedBy]
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequests]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistLeaveRequests_UserAccounts_Receptionist] FOREIGN KEY([ReceptionistUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequests] CHECK CONSTRAINT [FK_ReceptionistLeaveRequests_UserAccounts_Receptionist]
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistShiftSwapRequests_AdminUser] FOREIGN KEY([AdminDecisionBy])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests] CHECK CONSTRAINT [FK_ReceptionistShiftSwapRequests_AdminUser]
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistShiftSwapRequests_RequestingUser] FOREIGN KEY([RequestingReceptionistUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests] CHECK CONSTRAINT [FK_ReceptionistShiftSwapRequests_RequestingUser]
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistShiftSwapRequests_SourceSchedule] FOREIGN KEY([SourceScheduleId])
REFERENCES [dbo].[ReceptionistWorkSchedules] ([ReceptionistScheduleId])
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests] CHECK CONSTRAINT [FK_ReceptionistShiftSwapRequests_SourceSchedule]
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistShiftSwapRequests_TargetSchedule] FOREIGN KEY([TargetScheduleId])
REFERENCES [dbo].[ReceptionistWorkSchedules] ([ReceptionistScheduleId])
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests] CHECK CONSTRAINT [FK_ReceptionistShiftSwapRequests_TargetSchedule]
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistShiftSwapRequests_TargetUser] FOREIGN KEY([TargetReceptionistUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests] CHECK CONSTRAINT [FK_ReceptionistShiftSwapRequests_TargetUser]
GO
ALTER TABLE [dbo].[ReceptionistWorkHandovers]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistWorkHandovers_FromUser] FOREIGN KEY([FromReceptionistUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ReceptionistWorkHandovers] CHECK CONSTRAINT [FK_ReceptionistWorkHandovers_FromUser]
GO
ALTER TABLE [dbo].[ReceptionistWorkHandovers]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistWorkHandovers_Shift] FOREIGN KEY([ShiftId])
REFERENCES [dbo].[ReceptionistWorkSchedules] ([ReceptionistScheduleId])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[ReceptionistWorkHandovers] CHECK CONSTRAINT [FK_ReceptionistWorkHandovers_Shift]
GO
ALTER TABLE [dbo].[ReceptionistWorkHandovers]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistWorkHandovers_ToUser] FOREIGN KEY([ToReceptionistUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ReceptionistWorkHandovers] CHECK CONSTRAINT [FK_ReceptionistWorkHandovers_ToUser]
GO
ALTER TABLE [dbo].[ReceptionistWorkSchedules]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistWorkSchedules_ShiftTemplates] FOREIGN KEY([ShiftTemplateId])
REFERENCES [dbo].[ReceptionistShiftTemplates] ([ShiftTemplateId])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[ReceptionistWorkSchedules] CHECK CONSTRAINT [FK_ReceptionistWorkSchedules_ShiftTemplates]
GO
ALTER TABLE [dbo].[ReceptionistWorkSchedules]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistWorkSchedules_UserAccounts_Creator] FOREIGN KEY([CreatedBy])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ReceptionistWorkSchedules] CHECK CONSTRAINT [FK_ReceptionistWorkSchedules_UserAccounts_Creator]
GO
ALTER TABLE [dbo].[ReceptionistWorkSchedules]  WITH CHECK ADD  CONSTRAINT [FK_ReceptionistWorkSchedules_UserAccounts_Receptionist] FOREIGN KEY([ReceptionistUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[ReceptionistWorkSchedules] CHECK CONSTRAINT [FK_ReceptionistWorkSchedules_UserAccounts_Receptionist]
GO
ALTER TABLE [dbo].[Rooms]  WITH CHECK ADD  CONSTRAINT [FK_Rooms_Department] FOREIGN KEY([DepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[Rooms] CHECK CONSTRAINT [FK_Rooms_Department]
GO
ALTER TABLE [dbo].[ServiceCombos]  WITH CHECK ADD  CONSTRAINT [FK_ServiceCombos_Departments] FOREIGN KEY([DepartmentId])
REFERENCES [dbo].[Departments] ([DepartmentId])
GO
ALTER TABLE [dbo].[ServiceCombos] CHECK CONSTRAINT [FK_ServiceCombos_Departments]
GO
ALTER TABLE [dbo].[ServiceCombos]  WITH CHECK ADD  CONSTRAINT [FK_ServiceCombos_DepartmentServices] FOREIGN KEY([PrimaryServiceId])
REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId])
GO
ALTER TABLE [dbo].[ServiceCombos] CHECK CONSTRAINT [FK_ServiceCombos_DepartmentServices]
GO
ALTER TABLE [dbo].[StaffAttendanceCorrectionRequests]  WITH CHECK ADD  CONSTRAINT [FK_StaffAttendanceCorrection_Attendance] FOREIGN KEY([AttendanceId])
REFERENCES [dbo].[StaffAttendanceRecords] ([AttendanceId])
GO
ALTER TABLE [dbo].[StaffAttendanceCorrectionRequests] CHECK CONSTRAINT [FK_StaffAttendanceCorrection_Attendance]
GO
ALTER TABLE [dbo].[StaffAttendanceCorrectionRequests]  WITH CHECK ADD  CONSTRAINT [FK_StaffAttendanceCorrection_Reviewer] FOREIGN KEY([ReviewedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[StaffAttendanceCorrectionRequests] CHECK CONSTRAINT [FK_StaffAttendanceCorrection_Reviewer]
GO
ALTER TABLE [dbo].[StaffAttendanceCorrectionRequests]  WITH CHECK ADD  CONSTRAINT [FK_StaffAttendanceCorrection_User] FOREIGN KEY([UserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[StaffAttendanceCorrectionRequests] CHECK CONSTRAINT [FK_StaffAttendanceCorrection_User]
GO
ALTER TABLE [dbo].[StaffAttendanceEvents]  WITH CHECK ADD  CONSTRAINT [FK_StaffAttendanceEvents_Attendance] FOREIGN KEY([AttendanceId])
REFERENCES [dbo].[StaffAttendanceRecords] ([AttendanceId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[StaffAttendanceEvents] CHECK CONSTRAINT [FK_StaffAttendanceEvents_Attendance]
GO
ALTER TABLE [dbo].[StaffAttendanceEvents]  WITH CHECK ADD  CONSTRAINT [FK_StaffAttendanceEvents_User] FOREIGN KEY([UserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[StaffAttendanceEvents] CHECK CONSTRAINT [FK_StaffAttendanceEvents_User]
GO
ALTER TABLE [dbo].[StaffAttendanceRecords]  WITH CHECK ADD  CONSTRAINT [FK_StaffAttendanceRecords_FinalizedByUser] FOREIGN KEY([FinalizedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[StaffAttendanceRecords] CHECK CONSTRAINT [FK_StaffAttendanceRecords_FinalizedByUser]
GO
ALTER TABLE [dbo].[StaffAttendanceRecords]  WITH CHECK ADD  CONSTRAINT [FK_StaffAttendanceRecords_UserAccounts] FOREIGN KEY([UserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[StaffAttendanceRecords] CHECK CONSTRAINT [FK_StaffAttendanceRecords_UserAccounts]
GO
ALTER TABLE [dbo].[StaffProfiles]  WITH CHECK ADD  CONSTRAINT [FK_StaffProfiles_UserAccounts] FOREIGN KEY([UserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[StaffProfiles] CHECK CONSTRAINT [FK_StaffProfiles_UserAccounts]
GO
ALTER TABLE [dbo].[SupportChatMessages]  WITH CHECK ADD  CONSTRAINT [FK_SupportChatMessages_User] FOREIGN KEY([SenderUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[SupportChatMessages] CHECK CONSTRAINT [FK_SupportChatMessages_User]
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
ALTER TABLE [dbo].[VisitFeedback]  WITH CHECK ADD  CONSTRAINT [FK_VisitFeedback_AdminRespondedBy] FOREIGN KEY([AdminRespondedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[VisitFeedback] CHECK CONSTRAINT [FK_VisitFeedback_AdminRespondedBy]
GO
ALTER TABLE [dbo].[VisitFeedback]  WITH CHECK ADD  CONSTRAINT [FK_VisitFeedback_Appointment] FOREIGN KEY([AppointmentId])
REFERENCES [dbo].[Appointments] ([AppointmentId])
GO
ALTER TABLE [dbo].[VisitFeedback] CHECK CONSTRAINT [FK_VisitFeedback_Appointment]
GO
ALTER TABLE [dbo].[VisitFeedback]  WITH CHECK ADD  CONSTRAINT [FK_VisitFeedback_DentistUser] FOREIGN KEY([DentistUserId])
REFERENCES [dbo].[DentistProfiles] ([UserId])
GO
ALTER TABLE [dbo].[VisitFeedback] CHECK CONSTRAINT [FK_VisitFeedback_DentistUser]
GO
ALTER TABLE [dbo].[VisitFeedback]  WITH CHECK ADD  CONSTRAINT [FK_VisitFeedback_PatientUser] FOREIGN KEY([PatientUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[VisitFeedback] CHECK CONSTRAINT [FK_VisitFeedback_PatientUser]
GO
ALTER TABLE [dbo].[XrayDentistRosters]  WITH CHECK ADD  CONSTRAINT [FK_XrayDentistRosters_BackupDentist] FOREIGN KEY([BackupDentistUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[XrayDentistRosters] CHECK CONSTRAINT [FK_XrayDentistRosters_BackupDentist]
GO
ALTER TABLE [dbo].[XrayDentistRosters]  WITH CHECK ADD  CONSTRAINT [FK_XrayDentistRosters_CreatedBy] FOREIGN KEY([CreatedByUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[XrayDentistRosters] CHECK CONSTRAINT [FK_XrayDentistRosters_CreatedBy]
GO
ALTER TABLE [dbo].[XrayDentistRosters]  WITH CHECK ADD  CONSTRAINT [FK_XrayDentistRosters_Dentist] FOREIGN KEY([DentistUserId])
REFERENCES [dbo].[UserAccounts] ([UserId])
GO
ALTER TABLE [dbo].[XrayDentistRosters] CHECK CONSTRAINT [FK_XrayDentistRosters_Dentist]
GO
ALTER TABLE [dbo].[XrayDentistRosters]  WITH CHECK ADD  CONSTRAINT [FK_XrayDentistRosters_Equipment] FOREIGN KEY([EquipmentId])
REFERENCES [dbo].[ImagingEquipments] ([EquipmentId])
GO
ALTER TABLE [dbo].[XrayDentistRosters] CHECK CONSTRAINT [FK_XrayDentistRosters_Equipment]
GO
ALTER TABLE [dbo].[XrayDentistRosters]  WITH CHECK ADD  CONSTRAINT [FK_XrayDentistRosters_Room] FOREIGN KEY([RoomId])
REFERENCES [dbo].[Rooms] ([RoomId])
GO
ALTER TABLE [dbo].[XrayDentistRosters] CHECK CONSTRAINT [FK_XrayDentistRosters_Room]
GO
ALTER TABLE [dbo].[AppointmentChangeProposals]  WITH CHECK ADD CHECK  (([Status]='Expired' OR [Status]='Rejected' OR [Status]='Accepted' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[AppointmentChangeProposals]  WITH CHECK ADD  CONSTRAINT [CK_AppointmentChangeProposals_Time] CHECK  (([ProposedEnd] IS NULL OR [ProposedStart] IS NULL OR [ProposedEnd]>[ProposedStart]))
GO
ALTER TABLE [dbo].[AppointmentChangeProposals] CHECK CONSTRAINT [CK_AppointmentChangeProposals_Time]
GO
ALTER TABLE [dbo].[AppointmentDisruptions]  WITH CHECK ADD  CONSTRAINT [CK_AppointmentDisruptions_PatientChoice] CHECK  (([PatientChoice] IS NULL OR ([PatientChoice]='ContactStaff' OR [PatientChoice]='Reschedule' OR [PatientChoice]='ChangeDoctor')))
GO
ALTER TABLE [dbo].[AppointmentDisruptions] CHECK CONSTRAINT [CK_AppointmentDisruptions_PatientChoice]
GO
ALTER TABLE [dbo].[AppointmentDisruptions]  WITH CHECK ADD  CONSTRAINT [CK_AppointmentDisruptions_Status] CHECK  (([Status]='Cancelled' OR [Status]='ResolvedByStaff' OR [Status]='NeedsStaffAction' OR [Status]='Rescheduled' OR [Status]='DoctorChanged' OR [Status]='PendingPatientChoice'))
GO
ALTER TABLE [dbo].[AppointmentDisruptions] CHECK CONSTRAINT [CK_AppointmentDisruptions_Status]
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD CHECK  (([BookingSource]='Receptionist' OR [BookingSource]='Patient'))
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD CHECK  (([Status]='NoShow' OR [Status]='Cancelled' OR [Status]='Expired' OR [Status]='Withdrawn' OR [Status]='Rejected' OR [Status]='Completed' OR [Status]='InService' OR [Status]='Called' OR [Status]='Waiting' OR [Status]='CheckedIn' OR [Status]='Confirmed' OR [Status]='AwaitingPatientResponse' OR [Status]='PendingDepartmentReview' OR [Status]='PendingReceptionReview'))
GO
ALTER TABLE [dbo].[Appointments]  WITH CHECK ADD  CONSTRAINT [CK_Appointments_Schedule] CHECK  (([ScheduledEnd] IS NULL OR [ScheduledStart] IS NULL OR [ScheduledEnd]>[ScheduledStart]))
GO
ALTER TABLE [dbo].[Appointments] CHECK CONSTRAINT [CK_Appointments_Schedule]
GO
ALTER TABLE [dbo].[AttendancePeriodClosures]  WITH CHECK ADD  CONSTRAINT [CK_AttendancePeriodClosures_Month] CHECK  (([Month]>=(1) AND [Month]<=(12)))
GO
ALTER TABLE [dbo].[AttendancePeriodClosures] CHECK CONSTRAINT [CK_AttendancePeriodClosures_Month]
GO
ALTER TABLE [dbo].[AttendancePeriodClosures]  WITH CHECK ADD  CONSTRAINT [CK_AttendancePeriodClosures_Status] CHECK  (([Status]='Reopened' OR [Status]='Locked' OR [Status]='Finalized' OR [Status]='Open'))
GO
ALTER TABLE [dbo].[AttendancePeriodClosures] CHECK CONSTRAINT [CK_AttendancePeriodClosures_Status]
GO
ALTER TABLE [dbo].[BlogPostMedia]  WITH CHECK ADD  CONSTRAINT [CK_BlogPostMedia_Type] CHECK  (([MediaType]='Video' OR [MediaType]='Image'))
GO
ALTER TABLE [dbo].[BlogPostMedia] CHECK CONSTRAINT [CK_BlogPostMedia_Type]
GO
ALTER TABLE [dbo].[BlogPosts]  WITH CHECK ADD  CONSTRAINT [CK_BlogPosts_PostType] CHECK  (([PostType]='Announcement' OR [PostType]='ClinicNews' OR [PostType]='Blog'))
GO
ALTER TABLE [dbo].[BlogPosts] CHECK CONSTRAINT [CK_BlogPosts_PostType]
GO
ALTER TABLE [dbo].[BlogPosts]  WITH CHECK ADD  CONSTRAINT [CK_BlogPosts_Status] CHECK  (([Status]='Rejected' OR [Status]='Archived' OR [Status]='Published' OR [Status]='PendingReview' OR [Status]='Draft'))
GO
ALTER TABLE [dbo].[BlogPosts] CHECK CONSTRAINT [CK_BlogPosts_Status]
GO
ALTER TABLE [dbo].[ClinicalConsultations]  WITH CHECK ADD  CONSTRAINT [CK_ClinicalConsultations_Status] CHECK  (([Status]='Cancelled' OR [Status]='Answered' OR [Status]='InReview' OR [Status]='Requested'))
GO
ALTER TABLE [dbo].[ClinicalConsultations] CHECK CONSTRAINT [CK_ClinicalConsultations_Status]
GO
ALTER TABLE [dbo].[ClinicalConsultations]  WITH CHECK ADD  CONSTRAINT [CK_ClinicalConsultations_Urgency] CHECK  (([Urgency]='Urgent' OR [Urgency]='Normal'))
GO
ALTER TABLE [dbo].[ClinicalConsultations] CHECK CONSTRAINT [CK_ClinicalConsultations_Urgency]
GO
ALTER TABLE [dbo].[ClinicProfile]  WITH CHECK ADD CHECK  (([ClinicId]=(1)))
GO
ALTER TABLE [dbo].[ClinicProfile]  WITH CHECK ADD  CONSTRAINT [CK_ClinicProfile_OpenClose] CHECK  (([ClosingTime] IS NULL OR [OpeningTime] IS NULL OR [ClosingTime]>[OpeningTime]))
GO
ALTER TABLE [dbo].[ClinicProfile] CHECK CONSTRAINT [CK_ClinicProfile_OpenClose]
GO
ALTER TABLE [dbo].[DentalChairs]  WITH CHECK ADD CHECK  (([Status]='Maintenance' OR [Status]='Inactive' OR [Status]='Active'))
GO
ALTER TABLE [dbo].[DentistAvailability]  WITH CHECK ADD CHECK  (([AvailabilityStatus]='Unavailable' OR [AvailabilityStatus]='OnLeave' OR [AvailabilityStatus]='Busy' OR [AvailabilityStatus]='Available'))
GO
ALTER TABLE [dbo].[DentistAvailability]  WITH CHECK ADD  CONSTRAINT [CK_DentistAvailability_Time] CHECK  (([EndDateTime] IS NULL OR [EndDateTime]>[StartDateTime]))
GO
ALTER TABLE [dbo].[DentistAvailability] CHECK CONSTRAINT [CK_DentistAvailability_Time]
GO
ALTER TABLE [dbo].[DentistDepartments]  WITH CHECK ADD CHECK  (([Status]='Inactive' OR [Status]='Active'))
GO
ALTER TABLE [dbo].[DentistLeaveRequests]  WITH CHECK ADD  CONSTRAINT [CK_DentistLeaveRequests_Status] CHECK  (([Status]='Withdrawn' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[DentistLeaveRequests] CHECK CONSTRAINT [CK_DentistLeaveRequests_Status]
GO
ALTER TABLE [dbo].[DentistProfiles]  WITH CHECK ADD CHECK  (([YearsOfExperience] IS NULL OR [YearsOfExperience]>=(0)))
GO
ALTER TABLE [dbo].[DentistWorkSchedules]  WITH CHECK ADD CHECK  (([Status]='Cancelled' OR [Status]='Scheduled'))
GO
ALTER TABLE [dbo].[DentistWorkSchedules]  WITH CHECK ADD  CONSTRAINT [CK_DentistWorkSchedules_Time] CHECK  (([EndTime]>[StartTime]))
GO
ALTER TABLE [dbo].[DentistWorkSchedules] CHECK CONSTRAINT [CK_DentistWorkSchedules_Time]
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
ALTER TABLE [dbo].[Departments]  WITH CHECK ADD CHECK  (([Status]='Inactive' OR [Status]='Active'))
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
ALTER TABLE [dbo].[DepartmentWorkSchedules]  WITH CHECK ADD CHECK  (([DayOfWeek]>=(1) AND [DayOfWeek]<=(7)))
GO
ALTER TABLE [dbo].[DepartmentWorkSchedules]  WITH CHECK ADD  CONSTRAINT [CK_DepartmentWorkSchedules_Time] CHECK  (([EndTime]>[StartTime]))
GO
ALTER TABLE [dbo].[DepartmentWorkSchedules] CHECK CONSTRAINT [CK_DepartmentWorkSchedules_Time]
GO
ALTER TABLE [dbo].[DiagnosticOrders]  WITH CHECK ADD  CONSTRAINT [CK_DiagnosticOrders_Status] CHECK  (([Status]='Cancelled' OR [Status]='Completed' OR [Status]='InProgress' OR [Status]='Scheduled' OR [Status]='Ordered'))
GO
ALTER TABLE [dbo].[DiagnosticOrders] CHECK CONSTRAINT [CK_DiagnosticOrders_Status]
GO
ALTER TABLE [dbo].[DiagnosticResults]  WITH CHECK ADD  CONSTRAINT [CK_DiagnosticResults_Source] CHECK  (([SourceType]='External' OR [SourceType]='Internal'))
GO
ALTER TABLE [dbo].[DiagnosticResults] CHECK CONSTRAINT [CK_DiagnosticResults_Source]
GO
ALTER TABLE [dbo].[FollowUpSchedules]  WITH CHECK ADD  CONSTRAINT [CK_FollowUpSchedules_Status] CHECK  (([Status]='Proposed' OR [Status]='Confirmed' OR [Status]='Pending' OR [Status]='Scheduled' OR [Status]='Completed' OR [Status]='Cancelled'))
GO
ALTER TABLE [dbo].[FollowUpSchedules] CHECK CONSTRAINT [CK_FollowUpSchedules_Status]
GO
ALTER TABLE [dbo].[ImagingEquipments]  WITH CHECK ADD  CONSTRAINT [CK_ImagingEquipments_Status] CHECK  (([OperationalStatus]='OutOfService' OR [OperationalStatus]='Maintenance' OR [OperationalStatus]='Operational'))
GO
ALTER TABLE [dbo].[ImagingEquipments] CHECK CONSTRAINT [CK_ImagingEquipments_Status]
GO
ALTER TABLE [dbo].[ImagingSessions]  WITH CHECK ADD  CONSTRAINT [CK_ImagingSessions_Priority] CHECK  (([Priority]='Urgent' OR [Priority]='Normal'))
GO
ALTER TABLE [dbo].[ImagingSessions] CHECK CONSTRAINT [CK_ImagingSessions_Priority]
GO
ALTER TABLE [dbo].[ImagingSessions]  WITH CHECK ADD  CONSTRAINT [CK_ImagingSessions_Status] CHECK  (([Status]='UnableToPerform' OR [Status]='NeedsReschedule' OR [Status]='EquipmentUnavailable' OR [Status]='Cancelled' OR [Status]='Completed' OR [Status]='ResultAvailable' OR [Status]='Acquired' OR [Status]='InProgress' OR [Status]='Waiting' OR [Status]='CheckedIn' OR [Status]='Scheduled'))
GO
ALTER TABLE [dbo].[ImagingSessions] CHECK CONSTRAINT [CK_ImagingSessions_Status]
GO
ALTER TABLE [dbo].[InvoiceItems]  WITH CHECK ADD  CONSTRAINT [CK_InvoiceItems_Quantity] CHECK  (([Quantity]>(0)))
GO
ALTER TABLE [dbo].[InvoiceItems] CHECK CONSTRAINT [CK_InvoiceItems_Quantity]
GO
ALTER TABLE [dbo].[InvoiceItems]  WITH CHECK ADD  CONSTRAINT [CK_InvoiceItems_UnitPrice] CHECK  (([UnitPrice]>=(0)))
GO
ALTER TABLE [dbo].[InvoiceItems] CHECK CONSTRAINT [CK_InvoiceItems_UnitPrice]
GO
ALTER TABLE [dbo].[Invoices]  WITH CHECK ADD CHECK  (([Status]='Cancelled' OR [Status]='Paid' OR [Status]='PartiallyPaid' OR [Status]='Unpaid'))
GO
ALTER TABLE [dbo].[Invoices]  WITH CHECK ADD  CONSTRAINT [CK_Invoices_Total] CHECK  (([TotalAmount]>=(0)))
GO
ALTER TABLE [dbo].[Invoices] CHECK CONSTRAINT [CK_Invoices_Total]
GO
ALTER TABLE [dbo].[MedicineBatches]  WITH CHECK ADD  CONSTRAINT [CHK_MedicineBatches_CostPrice] CHECK  (([CostPrice]>=(0)))
GO
ALTER TABLE [dbo].[MedicineBatches] CHECK CONSTRAINT [CHK_MedicineBatches_CostPrice]
GO
ALTER TABLE [dbo].[MedicineBatches]  WITH CHECK ADD  CONSTRAINT [CHK_MedicineBatches_Qty] CHECK  (([QuantityReceived]>=(0) AND [QuantityAvailable]>=(0) AND [QuantityAvailable]<=[QuantityReceived]))
GO
ALTER TABLE [dbo].[MedicineBatches] CHECK CONSTRAINT [CHK_MedicineBatches_Qty]
GO
ALTER TABLE [dbo].[MedicineDispensationItems]  WITH CHECK ADD  CONSTRAINT [CHK_MedicineDispensationItems_Qty] CHECK  (([QuantityDispensed]>(0)))
GO
ALTER TABLE [dbo].[MedicineDispensationItems] CHECK CONSTRAINT [CHK_MedicineDispensationItems_Qty]
GO
ALTER TABLE [dbo].[Medicines]  WITH CHECK ADD  CONSTRAINT [CHK_Medicines_MinAlert] CHECK  (([MinimumStockAlert]>=(0)))
GO
ALTER TABLE [dbo].[Medicines] CHECK CONSTRAINT [CHK_Medicines_MinAlert]
GO
ALTER TABLE [dbo].[Medicines]  WITH CHECK ADD  CONSTRAINT [CHK_Medicines_UnitPrice] CHECK  (([UnitPrice]>=(0)))
GO
ALTER TABLE [dbo].[Medicines] CHECK CONSTRAINT [CHK_Medicines_UnitPrice]
GO
ALTER TABLE [dbo].[MedicineStockTransactions]  WITH CHECK ADD  CONSTRAINT [CHK_MedicineStockTransactions_Qty] CHECK  (([Quantity]>(0)))
GO
ALTER TABLE [dbo].[MedicineStockTransactions] CHECK CONSTRAINT [CHK_MedicineStockTransactions_Qty]
GO
ALTER TABLE [dbo].[Notifications]  WITH CHECK ADD CHECK  (([Channel]='Email' OR [Channel]='InApp'))
GO
ALTER TABLE [dbo].[Notifications]  WITH CHECK ADD  CONSTRAINT [CK_Notifications_DeliveryStatus] CHECK  (([DeliveryStatus]='Read' OR [DeliveryStatus]='Failed' OR [DeliveryStatus]='Sent' OR [DeliveryStatus]='Pending'))
GO
ALTER TABLE [dbo].[Notifications] CHECK CONSTRAINT [CK_Notifications_DeliveryStatus]
GO
ALTER TABLE [dbo].[PatientProfiles]  WITH CHECK ADD CHECK  (([Gender] IS NULL OR ([Gender]='Other' OR [Gender]='Female' OR [Gender]='Male')))
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD CHECK  (([Status]='Cancelled' OR [Status]='Failed' OR [Status]='Successful' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD  CONSTRAINT [CK_Payments_Amount] CHECK  (([Amount]>(0)))
GO
ALTER TABLE [dbo].[Payments] CHECK CONSTRAINT [CK_Payments_Amount]
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD  CONSTRAINT [CK_Payments_Method] CHECK  (([Method]='Online' OR [Method]='CreditCard' OR [Method]='BankTransfer' OR [Method]='Cash'))
GO
ALTER TABLE [dbo].[Payments] CHECK CONSTRAINT [CK_Payments_Method]
GO
ALTER TABLE [dbo].[PerformedServices]  WITH CHECK ADD  CONSTRAINT [CK_PerformedServices_Quantity] CHECK  (([Quantity]>(0)))
GO
ALTER TABLE [dbo].[PerformedServices] CHECK CONSTRAINT [CK_PerformedServices_Quantity]
GO
ALTER TABLE [dbo].[PerformedServices]  WITH CHECK ADD  CONSTRAINT [CK_PerformedServices_UnitPrice] CHECK  (([UnitPrice]>=(0)))
GO
ALTER TABLE [dbo].[PerformedServices] CHECK CONSTRAINT [CK_PerformedServices_UnitPrice]
GO
ALTER TABLE [dbo].[ReceptionistCoverageIncidents]  WITH CHECK ADD  CONSTRAINT [CK_ReceptionistCoverageIncidents_Status] CHECK  (([Status]='Acknowledged' OR [Status]='Resolved' OR [Status]='Investigating' OR [Status]='Open'))
GO
ALTER TABLE [dbo].[ReceptionistCoverageIncidents] CHECK CONSTRAINT [CK_ReceptionistCoverageIncidents_Status]
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequestPeriods]  WITH CHECK ADD  CONSTRAINT [CK_ReceptionistLeaveRequestPeriods_Status] CHECK  (([Status]='Cancelled' OR [Status]='Withdrawn' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequestPeriods] CHECK CONSTRAINT [CK_ReceptionistLeaveRequestPeriods_Status]
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequests]  WITH CHECK ADD  CONSTRAINT [CK_ReceptionistLeaveRequests_Date] CHECK  (([EndDate]>=[StartDate]))
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequests] CHECK CONSTRAINT [CK_ReceptionistLeaveRequests_Date]
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequests]  WITH CHECK ADD  CONSTRAINT [CK_ReceptionistLeaveRequests_LeaveType] CHECK  (([LeaveType]='MultiDay' OR [LeaveType]='Hourly' OR [LeaveType]='HalfDayAfternoon' OR [LeaveType]='HalfDayMorning' OR [LeaveType]='FullDay'))
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequests] CHECK CONSTRAINT [CK_ReceptionistLeaveRequests_LeaveType]
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequests]  WITH CHECK ADD  CONSTRAINT [CK_ReceptionistLeaveRequests_Status] CHECK  (([Status]='Cancelled' OR [Status]='Withdrawn' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[ReceptionistLeaveRequests] CHECK CONSTRAINT [CK_ReceptionistLeaveRequests_Status]
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests]  WITH CHECK ADD  CONSTRAINT [CK_ReceptionistShiftSwapRequests_DiffUsers] CHECK  (([RequestingReceptionistUserId]<>[TargetReceptionistUserId]))
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests] CHECK CONSTRAINT [CK_ReceptionistShiftSwapRequests_DiffUsers]
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests]  WITH CHECK ADD  CONSTRAINT [CK_ReceptionistShiftSwapRequests_Status] CHECK  (([Status]='Expired' OR [Status]='Withdrawn' OR [Status]='Declined' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='PendingAdminApproval' OR [Status]='PendingCounterparty'))
GO
ALTER TABLE [dbo].[ReceptionistShiftSwapRequests] CHECK CONSTRAINT [CK_ReceptionistShiftSwapRequests_Status]
GO
ALTER TABLE [dbo].[ReceptionistShiftTemplates]  WITH CHECK ADD  CONSTRAINT [CK_ReceptionistShiftTemplates_MinStaff] CHECK  (([MinStaffRequired]>=(1)))
GO
ALTER TABLE [dbo].[ReceptionistShiftTemplates] CHECK CONSTRAINT [CK_ReceptionistShiftTemplates_MinStaff]
GO
ALTER TABLE [dbo].[ReceptionistShiftTemplates]  WITH CHECK ADD  CONSTRAINT [CK_ReceptionistShiftTemplates_Time] CHECK  (([EndTime]>[StartTime]))
GO
ALTER TABLE [dbo].[ReceptionistShiftTemplates] CHECK CONSTRAINT [CK_ReceptionistShiftTemplates_Time]
GO
ALTER TABLE [dbo].[ReceptionistWorkHandovers]  WITH CHECK ADD  CONSTRAINT [CK_ReceptionistWorkHandovers_Status] CHECK  (([HandoverStatus]='Completed' OR [HandoverStatus]='Acknowledged' OR [HandoverStatus]='Transferred'))
GO
ALTER TABLE [dbo].[ReceptionistWorkHandovers] CHECK CONSTRAINT [CK_ReceptionistWorkHandovers_Status]
GO
ALTER TABLE [dbo].[ReceptionistWorkSchedules]  WITH CHECK ADD  CONSTRAINT [CK_ReceptionistWorkSchedules_Status] CHECK  (([Status]='AbsenceReported' OR [Status]='Swapped' OR [Status]='Completed' OR [Status]='Cancelled' OR [Status]='Scheduled'))
GO
ALTER TABLE [dbo].[ReceptionistWorkSchedules] CHECK CONSTRAINT [CK_ReceptionistWorkSchedules_Status]
GO
ALTER TABLE [dbo].[ReceptionistWorkSchedules]  WITH CHECK ADD  CONSTRAINT [CK_ReceptionistWorkSchedules_Time] CHECK  (([EndTime]>[StartTime]))
GO
ALTER TABLE [dbo].[ReceptionistWorkSchedules] CHECK CONSTRAINT [CK_ReceptionistWorkSchedules_Time]
GO
ALTER TABLE [dbo].[Rooms]  WITH CHECK ADD CHECK  (([Status]='Maintenance' OR [Status]='Inactive' OR [Status]='Active'))
GO
ALTER TABLE [dbo].[StaffAttendanceCorrectionRequests]  WITH CHECK ADD  CONSTRAINT [CK_StaffAttendanceCorrection_Status] CHECK  (([Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[StaffAttendanceCorrectionRequests] CHECK CONSTRAINT [CK_StaffAttendanceCorrection_Status]
GO
ALTER TABLE [dbo].[StaffAttendanceEvents]  WITH CHECK ADD  CONSTRAINT [CK_StaffAttendanceEvents_Type] CHECK  (([EventType]='PeriodReopened' OR [EventType]='PeriodFinalized' OR [EventType]='CorrectionApplied' OR [EventType]='ClockOut' OR [EventType]='ClockIn'))
GO
ALTER TABLE [dbo].[StaffAttendanceEvents] CHECK CONSTRAINT [CK_StaffAttendanceEvents_Type]
GO
ALTER TABLE [dbo].[StaffAttendanceRecords]  WITH CHECK ADD  CONSTRAINT [CK_StaffAttendanceRecords_Status] CHECK  (([Status]='UnexpectedAbsence' OR [Status]='ApprovedLeave' OR [Status]='LateAndEarlyDeparture' OR [Status]='EarlyDeparture' OR [Status]='Late' OR [Status]='OnTime' OR [Status]='Corrected' OR [Status]='Verified' OR [Status]='NeedsReview' OR [Status]='MissingClockOut' OR [Status]='ClockedOut' OR [Status]='ClockedIn' OR [Status]='NotStarted'))
GO
ALTER TABLE [dbo].[StaffAttendanceRecords] CHECK CONSTRAINT [CK_StaffAttendanceRecords_Status]
GO
ALTER TABLE [dbo].[StaffProfiles]  WITH CHECK ADD CHECK  (([EmploymentStatus]='Inactive' OR [EmploymentStatus]='Active'))
GO
ALTER TABLE [dbo].[TreatmentPlanItems]  WITH CHECK ADD  CONSTRAINT [CK_TreatmentPlanItems_Cost] CHECK  (([EstimatedCost] IS NULL OR [EstimatedCost]>=(0)))
GO
ALTER TABLE [dbo].[TreatmentPlanItems] CHECK CONSTRAINT [CK_TreatmentPlanItems_Cost]
GO
ALTER TABLE [dbo].[TreatmentPlanItems]  WITH CHECK ADD  CONSTRAINT [CK_TreatmentPlanItems_Sequence] CHECK  (([SequenceNo]>(0)))
GO
ALTER TABLE [dbo].[TreatmentPlanItems] CHECK CONSTRAINT [CK_TreatmentPlanItems_Sequence]
GO
ALTER TABLE [dbo].[TreatmentPlanItems]  WITH CHECK ADD  CONSTRAINT [CK_TreatmentPlanItems_Status] CHECK  (([Status]='Active' OR [Status]='Pending' OR [Status]='Planned' OR [Status]='InProgress' OR [Status]='Completed' OR [Status]='Cancelled'))
GO
ALTER TABLE [dbo].[TreatmentPlanItems] CHECK CONSTRAINT [CK_TreatmentPlanItems_Status]
GO
ALTER TABLE [dbo].[TreatmentPlans]  WITH CHECK ADD  CONSTRAINT [CK_TreatmentPlans_EstimatedTotal] CHECK  (([EstimatedTotal] IS NULL OR [EstimatedTotal]>=(0)))
GO
ALTER TABLE [dbo].[TreatmentPlans] CHECK CONSTRAINT [CK_TreatmentPlans_EstimatedTotal]
GO
ALTER TABLE [dbo].[TreatmentPlans]  WITH CHECK ADD  CONSTRAINT [CK_TreatmentPlans_Status] CHECK  (([Status]='Draft' OR [Status]='Active' OR [Status]='Planned' OR [Status]='InProgress' OR [Status]='Completed' OR [Status]='Cancelled'))
GO
ALTER TABLE [dbo].[TreatmentPlans] CHECK CONSTRAINT [CK_TreatmentPlans_Status]
GO
ALTER TABLE [dbo].[UserAccounts]  WITH CHECK ADD CHECK  (([FailedLoginCount]>=(0)))
GO
ALTER TABLE [dbo].[UserAccounts]  WITH CHECK ADD CHECK  (([Status]='Inactive' OR [Status]='Locked' OR [Status]='Active' OR [Status]='Unverified'))
GO
ALTER TABLE [dbo].[UserAccounts]  WITH CHECK ADD  CONSTRAINT [CK_UserAccounts_Role] CHECK  (([Role]='Patient' OR [Role]='Receptionist' OR [Role]='Dentist' OR [Role]='SystemAdministrator'))
GO
ALTER TABLE [dbo].[UserAccounts] CHECK CONSTRAINT [CK_UserAccounts_Role]
GO
ALTER TABLE [dbo].[VisitFeedback]  WITH CHECK ADD CHECK  (([ClinicRating]>=(1) AND [ClinicRating]<=(5)))
GO
ALTER TABLE [dbo].[VisitFeedback]  WITH CHECK ADD CHECK  (([DentistRating]>=(1) AND [DentistRating]<=(5)))
GO
ALTER TABLE [dbo].[XrayDentistRosters]  WITH CHECK ADD  CONSTRAINT [CK_XrayDentistRosters_ShiftType] CHECK  (([ShiftType]=N'Custom' OR [ShiftType]=N'Evening' OR [ShiftType]=N'Afternoon' OR [ShiftType]=N'Morning'))
GO
ALTER TABLE [dbo].[XrayDentistRosters] CHECK CONSTRAINT [CK_XrayDentistRosters_ShiftType]
GO
ALTER TABLE [dbo].[XrayDentistRosters]  WITH CHECK ADD  CONSTRAINT [CK_XrayDentistRosters_Status] CHECK  (([Status]=N'Replaced' OR [Status]=N'Cancelled' OR [Status]=N'OnLeave' OR [Status]=N'Active'))
GO
ALTER TABLE [dbo].[XrayDentistRosters] CHECK CONSTRAINT [CK_XrayDentistRosters_Status]
GO
ALTER TABLE [dbo].[XrayDentistRosters]  WITH CHECK ADD  CONSTRAINT [CK_XrayDentistRosters_Time] CHECK  (([EndTime]>[StartTime]))
GO
ALTER TABLE [dbo].[XrayDentistRosters] CHECK CONSTRAINT [CK_XrayDentistRosters_Time]
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

PRINT N'>>> DENTAL CLINIC CLEAN SCHEMA INITIALIZED SUCCESSFULLY (69 TABLES, VIEWS & PROCEDURES)!';
GO
