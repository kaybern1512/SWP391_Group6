-- ==============================================================================
-- Migration: 20261031_AddQueueNumberUniqueIndex_And_RoomExclusivity.sql
-- Target Database: DentalClinicManagementDB
-- Target Instance: DESKTOP-1TUBGEU\SQLEXPRESS
-- Description: 
--   1. Strict SET options for indexed computed columns & filtered indexes.
--   2. Pre-migration validations (PasswordHash nullability, no duplicate QueueNumbers).
--   3. Adds [IsExclusive] BIT to [Rooms] (Default 0 = shared multi-chair).
--      Configures approved exclusive suites (ROOM-201, ROOM-202, ROOM-302, ROOM-402) to 1.
--      Leaves verified shared multi-chair rooms (ROOM-101, ROOM-102, ROOM-301, ROOM-401) at 0.
--   4. Adds deterministic persisted computed column [ScheduledDate] to [Appointments].
--   5. Adds filtered UNIQUE INDEX on ([DepartmentId], [ScheduledDate], [QueueNumber])
--      with valid base-column filter predicate ([ScheduledStart] IS NOT NULL).
--      Includes Cancelled appointments to enforce clinic policy of never reusing numbers.
--   6. Enhanced idempotency verifying is_unique, key column order, and filter predicate.
-- Safety: Non-destructive, idempotent, atomic transaction with rollback on error.
-- Verified on: Microsoft SQL Server 2019 & 2022
-- ==============================================================================

-- REQUIRED SQL SERVER SET OPTIONS FOR FILTERED INDEXES & INDEXED COMPUTED COLUMNS
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '==============================================================================';
    PRINT 'Starting migration: 20261031_AddQueueNumberUniqueIndex_And_RoomExclusivity...';
    PRINT '==============================================================================';

    -- --------------------------------------------------------------------------
    -- 1. PRE-MIGRATION VALIDATIONS
    -- --------------------------------------------------------------------------
    PRINT 'Step 1: Validating pre-conditions...';

    -- 1.1 Verify [UserAccounts].[PasswordHash] nullability
    IF EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[dbo].[UserAccounts]') 
          AND name = 'PasswordHash' 
          AND is_nullable = 0
    )
    BEGIN
        THROW 51002, 'Pre-migration check failed: [dbo].[UserAccounts].[PasswordHash] is NOT nullable. Walk-in administrative accounts require nullable PasswordHash.', 1;
    END
    PRINT '  - [UserAccounts].[PasswordHash] permits NULL: OK';

    -- 1.2 Verify no duplicate QueueNumbers exist in current appointment records
    IF EXISTS (
        SELECT 1
        FROM [dbo].[Appointments]
        WHERE [DepartmentId] IS NOT NULL 
          AND [ScheduledStart] IS NOT NULL 
          AND [QueueNumber] IS NOT NULL 
          AND [QueueNumber] <> ''
        GROUP BY [DepartmentId], CONVERT(date, [ScheduledStart]), [QueueNumber]
        HAVING COUNT(*) > 1
    )
    BEGIN
        THROW 51001, 'Pre-migration check failed: Duplicate QueueNumbers detected in existing [Appointments] data. Resolve duplicates before applying the unique index.', 1;
    END
    PRINT '  - Existing QueueNumber uniqueness: OK (no duplicates detected)';

    -- --------------------------------------------------------------------------
    -- 2. UPGRADE [dbo].[Rooms] SCHEMA AND EXCLUSIVITY SETTINGS
    -- --------------------------------------------------------------------------
    PRINT 'Step 2: Upgrading [dbo].[Rooms] schema and room exclusivity settings...';

    -- 2.1 Add [IsExclusive] column if missing
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[dbo].[Rooms]') 
          AND name = 'IsExclusive'
    )
    BEGIN
        ALTER TABLE [dbo].[Rooms]
        ADD [IsExclusive] BIT NOT NULL CONSTRAINT [DF_Rooms_IsExclusive] DEFAULT(0);
        PRINT '  - Column [IsExclusive] added with DEFAULT(0).';
    END
    ELSE
    BEGIN
        PRINT '  - Column [IsExclusive] already exists.';
    END

    -- 2.2 Configure approved exclusive rooms:
    -- Exclusive suites: ROOM-201, ROOM-202, ROOM-302, ROOM-402
    -- Verified shared multi-chair rooms: ROOM-101, ROOM-102, ROOM-301, ROOM-401 remain IsExclusive = 0
    EXEC(N'
        UPDATE [dbo].[Rooms]
        SET [IsExclusive] = 1
        WHERE [RoomCode] IN (''ROOM-201'', ''ROOM-202'', ''ROOM-302'', ''ROOM-402'')
          AND [IsExclusive] = 0;
    ');
    PRINT '  - Approved exclusive rooms (ROOM-201, ROOM-202, ROOM-302, ROOM-402) configured to [IsExclusive] = 1.';

    -- --------------------------------------------------------------------------
    -- 3. UPGRADE [dbo].[Appointments] SCHEMA AND UNIQUE INDEX
    -- --------------------------------------------------------------------------
    PRINT 'Step 3: Upgrading [dbo].[Appointments] schema and unique index...';

    -- 3.1 Add persisted computed column [ScheduledDate] if missing
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[dbo].[Appointments]') 
          AND name = 'ScheduledDate'
    )
    BEGIN
        ALTER TABLE [dbo].[Appointments]
        ADD [ScheduledDate] AS CONVERT(date, [ScheduledStart]) PERSISTED;
        PRINT '  - Computed column [ScheduledDate] added as PERSISTED.';
    END
    ELSE
    BEGIN
        PRINT '  - Computed column [ScheduledDate] already exists.';
    END

    -- 3.2 Enhanced Idempotency Check for Filtered Unique Index
    -- Verifies is_unique, key column order, and filter predicate specification
    DECLARE @needRecreateIndex BIT = 0;
    IF EXISTS (
        SELECT 1 FROM sys.indexes 
        WHERE name = N'UQ_Appointments_Department_Date_QueueNumber' 
          AND object_id = OBJECT_ID(N'[dbo].[Appointments]')
    )
    BEGIN
        DECLARE @isUnique bit, @hasFilter bit, @filterDef nvarchar(max);
        SELECT @isUnique = is_unique, @hasFilter = has_filter, @filterDef = filter_definition
        FROM sys.indexes 
        WHERE name = N'UQ_Appointments_Department_Date_QueueNumber' 
          AND object_id = OBJECT_ID(N'[dbo].[Appointments]');

        DECLARE @keyCols nvarchar(500);
        SELECT @keyCols = STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
        FROM sys.index_columns ic
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE ic.object_id = OBJECT_ID(N'[dbo].[Appointments]')
          AND ic.index_id = INDEXPROPERTY(OBJECT_ID(N'[dbo].[Appointments]'), N'UQ_Appointments_Department_Date_QueueNumber', 'IndexID')
          AND ic.is_included_column = 0;

        -- Check if existing index violates the required specification:
        -- - Must be unique
        -- - Must be filtered
        -- - Key columns must be exactly DepartmentId,ScheduledDate,QueueNumber
        -- - Filter predicate must NOT contain computed column [ScheduledDate]
        -- - Filter predicate must NOT contain [Status] filter (to protect Cancelled appointments)
        -- - Filter predicate must contain base column [ScheduledStart]
        IF @isUnique <> 1 
           OR @hasFilter <> 1 
           OR @keyCols <> 'DepartmentId,ScheduledDate,QueueNumber'
           OR @filterDef LIKE '%ScheduledDate%' 
           OR @filterDef LIKE '%Status%'
           OR @filterDef NOT LIKE '%ScheduledStart%'
        BEGIN
            PRINT '  - Existing index does not match verified specification. Dropping for recreation...';
            DROP INDEX [UQ_Appointments_Department_Date_QueueNumber] ON [dbo].[Appointments];
            SET @needRecreateIndex = 1;
        END
        ELSE
        BEGIN
            PRINT '  - Index [UQ_Appointments_Department_Date_QueueNumber] exists with verified specification. Skipping.';
        END
    END
    ELSE
    BEGIN
        SET @needRecreateIndex = 1;
    END

    IF @needRecreateIndex = 1
    BEGIN
        PRINT '  - Creating filtered UNIQUE INDEX [UQ_Appointments_Department_Date_QueueNumber]...';
        -- Predicate uses base column [ScheduledStart] IS NOT NULL (SQL Server prohibits computed columns in filter predicates)
        -- Predicate does NOT filter out Cancelled appointments (to enforce clinic policy of never reusing issued numbers)
        EXEC(N'
            CREATE UNIQUE NONCLUSTERED INDEX [UQ_Appointments_Department_Date_QueueNumber]
            ON [dbo].[Appointments] ([DepartmentId], [ScheduledDate], [QueueNumber])
            WHERE [DepartmentId] IS NOT NULL 
              AND [ScheduledStart] IS NOT NULL 
              AND [QueueNumber] IS NOT NULL 
              AND [QueueNumber] <> '''';
        ');
        PRINT '  - Filtered UNIQUE INDEX created successfully.';
    END

    COMMIT TRANSACTION;
    PRINT '==============================================================================';
    PRINT 'Migration completed and committed successfully!';
    PRINT '==============================================================================';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT 'Transaction rolled back due to error.';
    END
    DECLARE @errMsg nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @errNum int = ERROR_NUMBER();
    DECLARE @errSeverity int = ERROR_SEVERITY();
    DECLARE @errState int = ERROR_STATE();
    RAISERROR('Migration failed: %s (Error %d)', @errSeverity, @errState, @errMsg, @errNum);
END CATCH
GO

/*
-- ==============================================================================
-- ROLLBACK SCRIPT (Execute if rollback is required)
-- ==============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. Drop filtered unique index
    IF EXISTS (
        SELECT 1 FROM sys.indexes 
        WHERE name = N'UQ_Appointments_Department_Date_QueueNumber' 
          AND object_id = OBJECT_ID(N'[dbo].[Appointments]')
    )
    BEGIN
        DROP INDEX [UQ_Appointments_Department_Date_QueueNumber] ON [dbo].[Appointments];
        PRINT 'Index [UQ_Appointments_Department_Date_QueueNumber] dropped.';
    END

    -- 2. Drop computed column [ScheduledDate]
    IF EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[dbo].[Appointments]') 
          AND name = 'ScheduledDate'
    )
    BEGIN
        ALTER TABLE [dbo].[Appointments] DROP COLUMN [ScheduledDate];
        PRINT 'Column [ScheduledDate] dropped.';
    END

    -- 3. Drop default constraint and column [IsExclusive] on [Rooms]
    IF EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[dbo].[Rooms]') 
          AND name = 'IsExclusive'
    )
    BEGIN
        DECLARE @dfName nvarchar(256);
        SELECT @dfName = dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE c.object_id = OBJECT_ID(N'[dbo].[Rooms]') AND c.name = 'IsExclusive';

        IF @dfName IS NOT NULL
        BEGIN
            EXEC('ALTER TABLE [dbo].[Rooms] DROP CONSTRAINT [' + @dfName + '];');
            PRINT 'Constraint [' + @dfName + '] dropped.';
        END

        ALTER TABLE [dbo].[Rooms] DROP COLUMN [IsExclusive];
        PRINT 'Column [IsExclusive] dropped.';
    END

    COMMIT TRANSACTION;
    PRINT 'Rollback completed successfully!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH
GO
*/
