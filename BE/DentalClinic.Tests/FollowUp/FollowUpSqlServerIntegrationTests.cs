using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.FollowUp.DTOs;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DentalClinic.Tests.FollowUp;

public class FollowUpSqlServerIntegrationTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("DENTAL_CLINIC_TEST_CONNECTION_STRING")
        ?? $"Server={Environment.GetEnvironmentVariable("TEST_SQL_SERVER") ?? Environment.GetEnvironmentVariable("DENTAL_CLINIC_SQL_INSTANCE") ?? ".\\SQLEXPRESS"};Database=DentalClinic_FollowUp_QA;Trusted_Connection=True;MultipleActiveResultSets=false;TrustServerCertificate=True;";

    private static readonly HashSet<string> AllowedQaDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        "DentalClinic_FollowUp_QA",
        "DentalClinic_Disposable_PreDeployClone",
        "DentalClinic_QA"
    };

    private static string ValidateSafeTestDatabase(string connStr)
    {
        var builder = new SqlConnectionStringBuilder(connStr);
        var db = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(db) || !AllowedQaDatabases.Contains(db))
        {
            var sanitizedServer = builder.DataSource;
            throw new InvalidOperationException(
                $"STRICT QA GUARD VIOLATION: Database '{db}' on server '{sanitizedServer}' is NOT in the allowed QA whitelist [{string.Join(", ", AllowedQaDatabases)}]. " +
                "Integration tests are strictly restricted to dedicated QA databases to prevent any accidental write to production, staging, or shared databases.");
        }
        return db;
    }

    private async Task<DentalClinicDbContext> CreateVerifiedDbContextAsync()
    {
        var connStr = ConnectionString;
        var expectedDb = ValidateSafeTestDatabase(connStr);

        var builder = new SqlConnectionStringBuilder(connStr);
        var sanitizedInfo = $"Server={builder.DataSource}, Database={builder.InitialCatalog}";

        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(connStr)
            .Options;
        var db = new DentalClinicDbContext(options);

        bool canConnect = await db.Database.CanConnectAsync();
        Assert.True(canConnect, $"FAILED TO CONNECT to SQL Server QA database ({sanitizedInfo}). Integration tests must run against a real, reachable QA SQL Server.");

        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT DB_NAME();";
            var activeDb = (string?)await cmd.ExecuteScalarAsync();
            Assert.True(string.Equals(activeDb, expectedDb, StringComparison.OrdinalIgnoreCase),
                $"STRICT QA GUARD: Connected database '{activeDb}' via DB_NAME() does not match expected QA database '{expectedDb}'.");
        }
        await conn.CloseAsync();

        return db;
    }

    [Fact]
    public void SafetyCheck_ThrowsWhenConfiguredToUseProductionOrMasterDb()
    {
        var masterConnStr = "Server=localhost;Database=DentalClinicManagementDB;Trusted_Connection=True;";
        Assert.Throws<InvalidOperationException>(() => ValidateSafeTestDatabase(masterConnStr));

        var prodConnStr = "Server=sql.prod.domain;Database=DentalCare_Production;User Id=sa;Password=secret;";
        Assert.Throws<InvalidOperationException>(() => ValidateSafeTestDatabase(prodConnStr));

        var stagingConnStr = "Server=sql.staging.domain;Database=DentalClinic_Staging;User Id=sa;Password=secret;";
        Assert.Throws<InvalidOperationException>(() => ValidateSafeTestDatabase(stagingConnStr));

        var sysMasterStr = "Server=localhost;Database=master;Trusted_Connection=True;";
        Assert.Throws<InvalidOperationException>(() => ValidateSafeTestDatabase(sysMasterStr));
    }

    [Fact]
    public async Task SqlServer_CheckConstraint_IsActiveAndTrusted()
    {
        using var db = await CreateVerifiedDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT cc.name, cc.is_disabled, cc.is_not_trusted, cc.definition
            FROM sys.check_constraints cc
            WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]')
              AND cc.name = 'CK_FollowUpSchedules_Status';";

        using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Constraint [CK_FollowUpSchedules_Status] does not exist on table FollowUpSchedules.");

        bool isDisabled = reader.GetBoolean(reader.GetOrdinal("is_disabled"));
        bool isNotTrusted = reader.GetBoolean(reader.GetOrdinal("is_not_trusted"));
        string definition = reader.GetString(reader.GetOrdinal("definition"));

        Assert.False(isDisabled, "Constraint [CK_FollowUpSchedules_Status] must be enabled (is_disabled = 0).");
        Assert.False(isNotTrusted, "Constraint [CK_FollowUpSchedules_Status] must be trusted (is_not_trusted = 0).");
        Assert.Contains("Pending", definition);
        Assert.Contains("Confirmed", definition);
        Assert.Contains("Scheduled", definition);
        Assert.Contains("Completed", definition);
        Assert.Contains("Cancelled", definition);
        Assert.Contains("Proposed", definition);
        Assert.DoesNotContain("Called", definition);
        Assert.DoesNotContain("Unreachable", definition);
    }

    [Fact]
    public async Task SqlServer_ValidStatuses_CanBeSavedAndQueried()
    {
        using var db = await CreateVerifiedDbContextAsync();
        using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            var validStatuses = new[] { "Pending", "Confirmed", "Scheduled", "Completed", "Cancelled", "Proposed" };
            foreach (var status in validStatuses)
            {
                var followUp = new FollowUpSchedule
                {
                    TreatmentSessionId = 1,
                    RecommendedDate = DateTime.Today.AddDays(7),
                    Reason = $"Test valid status: {status}",
                    Status = status
                };
                db.FollowUpSchedules.Add(followUp);
                await db.SaveChangesAsync();

                Assert.True(followUp.FollowUpId > 0);
                var retrieved = await db.FollowUpSchedules.FindAsync(followUp.FollowUpId);
                Assert.NotNull(retrieved);
                Assert.Equal(status, retrieved.Status);
            }
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    [Fact]
    public async Task SqlServer_InvalidStatuses_CalledAndUnreachable_RejectedByCheckConstraint()
    {
        using var db = await CreateVerifiedDbContextAsync();

        var invalidStatuses = new[] { "Called", "Unreachable", "NotCalled", "InvalidStatus", "Hacked" };
        foreach (var status in invalidStatuses)
        {
            using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var followUp = new FollowUpSchedule
                {
                    TreatmentSessionId = 1,
                    RecommendedDate = DateTime.Today.AddDays(7),
                    Reason = $"Test invalid status: {status}",
                    Status = status
                };
                db.FollowUpSchedules.Add(followUp);

                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.NotNull(ex.InnerException);
                Assert.Contains("CK_FollowUpSchedules_Status", ex.InnerException.Message);
            }
            finally
            {
                await tx.RollbackAsync();
            }
        }
    }

    [Fact]
    public async Task SqlServer_UpdateReminderCallStatus_PreservesStatusAndPersistsAuditLog()
    {
        using var db = await CreateVerifiedDbContextAsync();
        using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            var followUp = new FollowUpSchedule
            {
                TreatmentSessionId = 1,
                RecommendedDate = DateTime.Today,
                Reason = "Test live reminder decoupling",
                Status = "Scheduled"
            };
            db.FollowUpSchedules.Add(followUp);
            await db.SaveChangesAsync();

            var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
            var req = new UpdateReminderCallStatusRequest
            {
                FollowUpId = followUp.FollowUpId,
                CallStatus = "Called",
                Note = "Bệnh nhân xác nhận chiều nay đến"
            };

            var receptionist = await db.UserAccounts.FirstOrDefaultAsync(u => u.Role == "Receptionist")
                ?? await db.UserAccounts.FirstAsync();
            var receptionistUserId = receptionist.UserId;

            var res = await service.UpdateReminderCallStatusAsync(receptionistUserId, req);
            Assert.True(res.Success);
            Assert.Equal("Called", res.Data?.CallStatus);
            Assert.Equal("Scheduled", res.Data?.AppointmentStatus);

            // Re-fetch from SQL Server
            var refreshed = await db.FollowUpSchedules.AsNoTracking().FirstOrDefaultAsync(f => f.FollowUpId == followUp.FollowUpId);
            Assert.NotNull(refreshed);
            Assert.Equal("Scheduled", refreshed.Status); // Clinical status MUST NOT be 'Called'!

            var audit = await db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.EntityId == followUp.FollowUpId.ToString() && a.Action == "FollowUpReminderCall");
            Assert.NotNull(audit);
            Assert.Contains("Called", audit.NewValues);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    [Fact]
    public async Task SqlServer_UpdateReminderCallStatus_CancelledFollowUp_FailsGracefully()
    {
        using var db = await CreateVerifiedDbContextAsync();
        using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            var followUp = new FollowUpSchedule
            {
                TreatmentSessionId = 1,
                RecommendedDate = DateTime.Today,
                Reason = "Cancelled follow up test",
                Status = "Cancelled"
            };
            db.FollowUpSchedules.Add(followUp);
            await db.SaveChangesAsync();

            var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
            var req = new UpdateReminderCallStatusRequest
            {
                FollowUpId = followUp.FollowUpId,
                CallStatus = "Called"
            };

            var receptionist = await db.UserAccounts.FirstOrDefaultAsync(u => u.Role == "Receptionist")
                ?? await db.UserAccounts.FirstAsync();
            var receptionistUserId = receptionist.UserId;

            var res = await service.UpdateReminderCallStatusAsync(receptionistUserId, req);
            Assert.False(res.Success);
            Assert.Contains("hủy", res.Message);

            var refreshed = await db.FollowUpSchedules.AsNoTracking().FirstOrDefaultAsync(f => f.FollowUpId == followUp.FollowUpId);
            Assert.NotNull(refreshed);
            Assert.Equal("Cancelled", refreshed.Status);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    [Fact]
    public async Task SqlServer_UpdateReminderCallStatus_CompletedFollowUp_FailsGracefully()
    {
        using var db = await CreateVerifiedDbContextAsync();
        using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            var followUp = new FollowUpSchedule
            {
                TreatmentSessionId = 1,
                RecommendedDate = DateTime.Today,
                Reason = "Completed follow up test",
                Status = "Completed"
            };
            db.FollowUpSchedules.Add(followUp);
            await db.SaveChangesAsync();

            var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
            var req = new UpdateReminderCallStatusRequest
            {
                FollowUpId = followUp.FollowUpId,
                CallStatus = "Called"
            };

            var receptionist = await db.UserAccounts.FirstOrDefaultAsync(u => u.Role == "Receptionist")
                ?? await db.UserAccounts.FirstAsync();
            var receptionistUserId = receptionist.UserId;

            var res = await service.UpdateReminderCallStatusAsync(receptionistUserId, req);
            Assert.False(res.Success);
            Assert.Contains("hoàn tất", res.Message);

            var refreshed = await db.FollowUpSchedules.AsNoTracking().FirstOrDefaultAsync(f => f.FollowUpId == followUp.FollowUpId);
            Assert.NotNull(refreshed);
            Assert.Equal("Completed", refreshed.Status);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    [Fact]
    public async Task SqlServer_Migration_DiscoversAndReplacesOldTableLevelConstraint_AndIsIdempotent()
    {
        using var db = await CreateVerifiedDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        var tableName = $"Test_Sim_FollowUp_{Guid.NewGuid():N}";
        try
        {
            // 1. Create simulated old schema table
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    CREATE TABLE [dbo].[{tableName}] (
                        FollowUpId BIGINT IDENTITY(1,1) PRIMARY KEY,
                        Status NVARCHAR(20) NOT NULL DEFAULT 'Pending'
                    );
                    -- Old table-level anonymous constraint
                    ALTER TABLE [dbo].[{tableName}] WITH CHECK ADD CHECK
                    (([Status]='Cancelled' OR [Status]='Completed' OR [Status]='Scheduled' OR [Status]='Pending'));
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. Discover and drop constraint on Status using QUOTENAME (handles parent_column_id = 0)
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    DECLARE @ConstraintName NVARCHAR(128);
                    DECLARE @DropSql NVARCHAR(MAX);

                    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT cc.name
                    FROM sys.check_constraints cc
                    WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[{tableName}]')
                      AND (
                          cc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[dbo].[{tableName}]'), 'Status', 'ColumnId')
                          OR (
                              cc.parent_column_id = 0 
                              AND (cc.definition LIKE '%[Status]%' OR cc.definition LIKE '%Status%')
                              AND NOT EXISTS (
                                  SELECT 1 FROM sys.columns other_c 
                                  WHERE other_c.object_id = cc.parent_object_id 
                                    AND other_c.name <> 'Status'
                                    AND (cc.definition LIKE '%[' + other_c.name + ']%' OR cc.definition LIKE '%' + other_c.name + '%')
                              )
                          )
                      );

                    OPEN cur;
                    FETCH NEXT FROM cur INTO @ConstraintName;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        SET @DropSql = N'ALTER TABLE [dbo].[{tableName}] DROP CONSTRAINT ' + QUOTENAME(@ConstraintName) + N';';
                        EXEC sp_executesql @DropSql;
                        FETCH NEXT FROM cur INTO @ConstraintName;
                    END
                    CLOSE cur;
                    DEALLOCATE cur;

                    ALTER TABLE [dbo].[{tableName}] WITH CHECK ADD CONSTRAINT [CK_{tableName}_Status]
                    CHECK ([Status] IN ('Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed'));
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // 3. Verify Confirmed and Proposed now accepted
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    INSERT INTO [dbo].[{tableName}] (Status) VALUES ('Confirmed');
                    INSERT INTO [dbo].[{tableName}] (Status) VALUES ('Proposed');
                    SELECT COUNT(*) FROM [dbo].[{tableName}] WHERE Status IN ('Confirmed', 'Proposed');
                ";
                var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                Assert.Equal(2, count);
            }

            // 4. Test Idempotency (run again)
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    IF EXISTS (
                        SELECT 1 FROM sys.check_constraints cc
                        WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[{tableName}]')
                          AND cc.name = 'CK_{tableName}_Status'
                          AND cc.definition LIKE '%Confirmed%'
                    )
                    BEGIN
                        ALTER TABLE [dbo].[{tableName}] WITH CHECK CHECK CONSTRAINT [CK_{tableName}_Status];
                    END
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // 5. Verify invalid status rejected
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"INSERT INTO [dbo].[{tableName}] (Status) VALUES ('Called');";
                var ex = await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
                Assert.Contains("CHECK constraint", ex.Message);
            }
        }
        finally
        {
            // Zero residue cleanup
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"IF OBJECT_ID(N'[dbo].[{tableName}]', N'U') IS NOT NULL DROP TABLE [dbo].[{tableName}];";
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task SqlServer_Migration_PreservesUnrelatedConstraintsOnOtherColumns()
    {
        using var db = await CreateVerifiedDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        var tableName = $"Test_Unrelated_{Guid.NewGuid():N}";
        try
        {
            // Create table with Status constraint AND unrelated constraint on Reason column
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    CREATE TABLE [dbo].[{tableName}] (
                        FollowUpId BIGINT IDENTITY(1,1) PRIMARY KEY,
                        Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
                        Reason NVARCHAR(200) NOT NULL
                    );
                    -- Old table-level constraint on Status
                    ALTER TABLE [dbo].[{tableName}] WITH CHECK ADD CONSTRAINT [CK_{tableName}_OldStatus] CHECK
                    (([Status]='Cancelled' OR [Status]='Completed' OR [Status]='Scheduled' OR [Status]='Pending'));

                    -- Unrelated constraint on Reason column
                    ALTER TABLE [dbo].[{tableName}] WITH CHECK ADD CONSTRAINT [CK_{tableName}_ReasonLength] CHECK
                    (LEN([Reason]) >= 5);
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // Run constraint replacement targeting Status exclusively
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    DECLARE @ConstraintName NVARCHAR(128);
                    DECLARE @DropSql NVARCHAR(MAX);

                    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT cc.name
                    FROM sys.check_constraints cc
                    WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[{tableName}]')
                      AND (
                          cc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[dbo].[{tableName}]'), 'Status', 'ColumnId')
                          OR (
                              cc.parent_column_id = 0 
                              AND (cc.definition LIKE '%[Status]%' OR cc.definition LIKE '%Status%')
                              AND NOT EXISTS (
                                  SELECT 1 FROM sys.columns other_c 
                                  WHERE other_c.object_id = cc.parent_object_id 
                                    AND other_c.name <> 'Status'
                                    AND (cc.definition LIKE '%[' + other_c.name + ']%' OR cc.definition LIKE '%' + other_c.name + '%')
                              )
                          )
                      );

                    OPEN cur;
                    FETCH NEXT FROM cur INTO @ConstraintName;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        SET @DropSql = N'ALTER TABLE [dbo].[{tableName}] DROP CONSTRAINT ' + QUOTENAME(@ConstraintName) + N';';
                        EXEC sp_executesql @DropSql;
                        FETCH NEXT FROM cur INTO @ConstraintName;
                    END
                    CLOSE cur;
                    DEALLOCATE cur;

                    ALTER TABLE [dbo].[{tableName}] WITH CHECK ADD CONSTRAINT [CK_{tableName}_Status]
                    CHECK ([Status] IN ('Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed'));
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // Verify that the unrelated constraint CK_ReasonLength was PRESERVED!
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    SELECT COUNT(*) FROM sys.check_constraints
                    WHERE parent_object_id = OBJECT_ID(N'[dbo].[{tableName}]')
                      AND name = 'CK_{tableName}_ReasonLength';
                ";
                var exists = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                Assert.Equal(1, exists);
            }

            // Verify unrelated constraint still actively enforces rule (Reason < 5 chars rejected)
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"INSERT INTO [dbo].[{tableName}] (Status, Reason) VALUES ('Pending', 'abc');";
                var ex = await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
                Assert.Contains("CK_" + tableName + "_ReasonLength", ex.Message);
            }
        }
        finally
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"IF OBJECT_ID(N'[dbo].[{tableName}]', N'U') IS NOT NULL DROP TABLE [dbo].[{tableName}];";
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task SqlServer_Rollback_AbortsWhenConfirmedOrProposedRowsExist_PreservingData()
    {
        using var db = await CreateVerifiedDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        var tableName = $"Test_Rollback_{Guid.NewGuid():N}";
        try
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    CREATE TABLE [dbo].[{tableName}] (
                        FollowUpId BIGINT IDENTITY(1,1) PRIMARY KEY,
                        Status NVARCHAR(20) NOT NULL
                    );
                    ALTER TABLE [dbo].[{tableName}] WITH CHECK ADD CONSTRAINT [CK_{tableName}_Status]
                    CHECK ([Status] IN ('Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed'));

                    INSERT INTO [dbo].[{tableName}] (Status) VALUES ('Confirmed');
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // Test rollback script safety preflight
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    DECLARE @ExtCount INT;
                    SELECT @ExtCount = COUNT(*) FROM [dbo].[{tableName}] WHERE [Status] IN ('Confirmed', 'Proposed');
                    IF @ExtCount > 0
                    BEGIN
                        RAISERROR(N'ROLLBACK ABORTED: Found records with extended status.', 16, 1);
                    END
                ";
                var ex = await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
                Assert.Contains("ROLLBACK ABORTED", ex.Message);
            }

            // Verify row still exists and is untouched
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT Status FROM [dbo].[{tableName}];";
                var status = (string)(await cmd.ExecuteScalarAsync() ?? "");
                Assert.Equal("Confirmed", status);
            }
        }
        finally
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"IF OBJECT_ID(N'[dbo].[{tableName}]', N'U') IS NOT NULL DROP TABLE [dbo].[{tableName}];";
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task SqlServer_Rollback_SucceedsWhenNoExtendedStatusesExist()
    {
        using var db = await CreateVerifiedDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        var tableName = $"Test_Rollback_Success_{Guid.NewGuid():N}";
        try
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    CREATE TABLE [dbo].[{tableName}] (
                        FollowUpId BIGINT IDENTITY(1,1) PRIMARY KEY,
                        Status NVARCHAR(20) NOT NULL
                    );
                    ALTER TABLE [dbo].[{tableName}] WITH CHECK ADD CONSTRAINT [CK_{tableName}_Status]
                    CHECK ([Status] IN ('Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed'));

                    -- Insert only legacy-compatible statuses
                    INSERT INTO [dbo].[{tableName}] (Status) VALUES ('Pending');
                    INSERT INTO [dbo].[{tableName}] (Status) VALUES ('Scheduled');
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // Execute rollback schema transformation
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    DECLARE @ExtCount INT;
                    SELECT @ExtCount = COUNT(*) FROM [dbo].[{tableName}] WHERE [Status] IN ('Confirmed', 'Proposed');
                    IF @ExtCount > 0
                    BEGIN
                        RAISERROR(N'ROLLBACK ABORTED', 16, 1);
                    END

                    ALTER TABLE [dbo].[{tableName}] DROP CONSTRAINT [CK_{tableName}_Status];

                    ALTER TABLE [dbo].[{tableName}] WITH CHECK ADD CHECK
                    (([Status]='Cancelled' OR [Status]='Completed' OR [Status]='Scheduled' OR [Status]='Pending'));
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // Verify that Confirmed is now rejected under restored legacy constraint
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"INSERT INTO [dbo].[{tableName}] (Status) VALUES ('Confirmed');";
                var ex = await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
                Assert.Contains("CHECK constraint", ex.Message);
            }
        }
        finally
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"IF OBJECT_ID(N'[dbo].[{tableName}]', N'U') IS NOT NULL DROP TABLE [dbo].[{tableName}];";
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task SqlServer_Migration_InfersClinicalStatusFromAppointment_CancelledAndCompleted()
    {
        using var db = await CreateVerifiedDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        var apptTable = $"Test_Appt_{Guid.NewGuid():N}";
        var followUpTable = $"Test_FUp_{Guid.NewGuid():N}";

        try
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    CREATE TABLE [dbo].[{apptTable}] (
                        AppointmentId BIGINT PRIMARY KEY,
                        Status VARCHAR(20) NOT NULL
                    );
                    CREATE TABLE [dbo].[{followUpTable}] (
                        FollowUpId BIGINT IDENTITY(1,1) PRIMARY KEY,
                        AppointmentId BIGINT NULL,
                        Status NVARCHAR(20) NOT NULL
                    );

                    INSERT INTO [dbo].[{apptTable}] VALUES (101, 'Cancelled'), (102, 'Completed'), (103, 'Confirmed');
                    INSERT INTO [dbo].[{followUpTable}] (AppointmentId, Status) VALUES 
                        (101, 'Called'),
                        (102, 'Unreachable'),
                        (103, 'Called'),
                        (NULL, 'Called');
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // Run clinical status inference logic from migration script
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    UPDATE f
                    SET f.[Status] = CASE 
                        WHEN f.[AppointmentId] IS NULL THEN 'Pending'
                        WHEN a.[Status] = 'Cancelled' THEN 'Cancelled'
                        WHEN a.[Status] = 'Completed' THEN 'Completed'
                        WHEN a.[Status] = 'Confirmed' THEN 'Confirmed'
                        ELSE 'Scheduled'
                    END
                    FROM [dbo].[{followUpTable}] f
                    LEFT JOIN [dbo].[{apptTable}] a ON f.[AppointmentId] = a.[AppointmentId]
                    WHERE f.[Status] IN ('Called', 'Unreachable');
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // Verify inferred statuses
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT Status FROM [dbo].[{followUpTable}] ORDER BY FollowUpId;";
                using var reader = await cmd.ExecuteReaderAsync();

                Assert.True(await reader.ReadAsync());
                Assert.Equal("Cancelled", reader.GetString(0)); // Linked to Cancelled appointment -> Cancelled

                Assert.True(await reader.ReadAsync());
                Assert.Equal("Completed", reader.GetString(0)); // Linked to Completed appointment -> Completed

                Assert.True(await reader.ReadAsync());
                Assert.Equal("Confirmed", reader.GetString(0)); // Linked to Confirmed appointment -> Confirmed

                Assert.True(await reader.ReadAsync());
                Assert.Equal("Pending", reader.GetString(0)); // Unlinked -> Pending
            }
        }
        finally
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    IF OBJECT_ID(N'[dbo].[{followUpTable}]', N'U') IS NOT NULL DROP TABLE [dbo].[{followUpTable}];
                    IF OBJECT_ID(N'[dbo].[{apptTable}]', N'U') IS NOT NULL DROP TABLE [dbo].[{apptTable}];
                ";
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task SqlServer_CallProvenance_AuthenticAuditLogPreserved_AndReconstructedYieldsNullTimestamp()
    {
        using var db = await CreateVerifiedDbContextAsync();
        using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            var genuineStaffName = "Lễ Tân Mai Phương";
            var genuinePastDate = new DateTime(2026, 10, 12, 11, 15, 0);

            // FollowUp 1: has authentic AuditLog
            var followUp1 = new FollowUpSchedule
            {
                TreatmentSessionId = 1,
                RecommendedDate = DateTime.Today,
                Reason = "Case A: Authentic log",
                Status = "Scheduled"
            };
            db.FollowUpSchedules.Add(followUp1);
            await db.SaveChangesAsync();

            var audit1 = new AuditLog
            {
                UserId = 2,
                Action = "FollowUpReminderCall",
                EntityName = "FollowUpSchedule",
                EntityId = followUp1.FollowUpId.ToString(),
                OldValues = "Scheduled",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new
                {
                    CallStatus = "Called",
                    CallerStaffName = genuineStaffName,
                    CalledAt = genuinePastDate,
                    Note = "Bệnh nhân xác nhận tuần sau đến"
                }),
                CreatedAt = genuinePastDate
            };
            db.AuditLogs.Add(audit1);

            // FollowUp 2: legacy orphan row with Reconstructed AuditLog (CalledAt = null, CallerStaffName = null)
            var followUp2 = new FollowUpSchedule
            {
                TreatmentSessionId = 1,
                RecommendedDate = DateTime.Today,
                Reason = "Case B: Reconstructed legacy",
                Status = "Scheduled"
            };
            db.FollowUpSchedules.Add(followUp2);
            await db.SaveChangesAsync();

            var audit2 = new AuditLog
            {
                UserId = null,
                Action = "FollowUpReminderCall",
                EntityName = "FollowUpSchedule",
                EntityId = followUp2.FollowUpId.ToString(),
                OldValues = "Called",
                NewValues = "{\"CallStatus\":\"Called\",\"Note\":\"Tái dựng từ trạng thái FollowUpSchedule.Status cũ\",\"CalledAt\":null,\"CallerStaffName\":null,\"IsHistoricalReconstruction\":true}",
                CreatedAt = DateTime.UtcNow
            };
            db.AuditLogs.Add(audit2);
            await db.SaveChangesAsync();

            var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
            var remindersRes = await service.GetReceptionistRemindersAsync("all");
            Assert.True(remindersRes.Success);
            Assert.NotNull(remindersRes.Data);

            // Case A assertions: authentic staff name and authentic timestamp are returned
            var item1 = remindersRes.Data.FirstOrDefault(r => r.FollowUpId == followUp1.FollowUpId);
            Assert.NotNull(item1);
            Assert.Equal("Called", item1.CallStatus);
            Assert.Equal(genuineStaffName, item1.LastCallerName);
            Assert.Equal(genuinePastDate, item1.LastCalledAt);

            // Case B assertions: reconstructed event returns CallStatus = "Called", but LastCalledAt is NULL and LastCallerName is NULL!
            var item2 = remindersRes.Data.FirstOrDefault(r => r.FollowUpId == followUp2.FollowUpId);
            Assert.NotNull(item2);
            Assert.Equal("Called", item2.CallStatus);
            Assert.Null(item2.LastCallerName); // Provenance safety: caller is unknown
            Assert.Null(item2.LastCalledAt);   // Provenance safety: timestamp is not faked!
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }
}
