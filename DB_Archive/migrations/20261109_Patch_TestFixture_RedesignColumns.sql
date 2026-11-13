-- Schema patch for automated integration test fixtures restoring from baseline backups
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DepartmentServices') AND name = 'ServiceType')
    ALTER TABLE [dbo].[DepartmentServices] ADD [ServiceType] NVARCHAR(50) NOT NULL CONSTRAINT DF_DS_ServiceType DEFAULT ('Treatment');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DepartmentServices') AND name = 'RequiresExclusiveRoom')
    ALTER TABLE [dbo].[DepartmentServices] ADD [RequiresExclusiveRoom] BIT NOT NULL CONSTRAINT DF_DS_RequiresExclusiveRoom DEFAULT (0);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DepartmentServices') AND name = 'RequiresConsultationFirst')
    ALTER TABLE [dbo].[DepartmentServices] ADD [RequiresConsultationFirst] BIT NOT NULL CONSTRAINT DF_DS_RequiresConsultationFirst DEFAULT (0);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DepartmentServices') AND name = 'MinLeadTimeHours')
    ALTER TABLE [dbo].[DepartmentServices] ADD [MinLeadTimeHours] INT NOT NULL CONSTRAINT DF_DS_MinLeadTimeHours DEFAULT (2);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DepartmentServices') AND name = 'BookingInstructions')
    ALTER TABLE [dbo].[DepartmentServices] ADD [BookingInstructions] NVARCHAR(1000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BlogPosts') AND name = 'PostType')
    ALTER TABLE [dbo].[BlogPosts] ADD [PostType] NVARCHAR(50) NOT NULL CONSTRAINT DF_BP_PostType DEFAULT ('Blog');

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BlogPosts') AND name = 'RejectionReason')
    ALTER TABLE [dbo].[BlogPosts] ADD [RejectionReason] NVARCHAR(1000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BlogPosts') AND name = 'ApprovedByUserId')
    ALTER TABLE [dbo].[BlogPosts] ADD [ApprovedByUserId] BIGINT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BlogPosts') AND name = 'ApprovedAt')
    ALTER TABLE [dbo].[BlogPosts] ADD [ApprovedAt] DATETIME2 NULL;
