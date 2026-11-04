using System;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Application.Features.FollowUp.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DentalClinic.Tests.FollowUp;

public class FollowUpSqlServerIntegrationTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("DENTAL_CLINIC_TEST_CONNECTION_STRING")
        ?? "Server=.\\SQL2025_DUC;Database=DentalClinic_FollowUp_QA;Trusted_Connection=True;MultipleActiveResultSets=false;TrustServerCertificate=True;";

    private static async Task<bool> IsSqlServerAvailableAsync()
    {
        try
        {
            var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            using var db = new DentalClinicDbContext(options);
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private DentalClinicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    [Fact]
    public async Task SqlServer_ValidStatuses_CanBeSavedAndQueried()
    {
        if (!await IsSqlServerAvailableAsync()) return;

        using var db = CreateDbContext();
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
        if (!await IsSqlServerAvailableAsync()) return;

        using var db = CreateDbContext();

        var invalidStatuses = new[] { "Called", "Unreachable", "InvalidStatus" };
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
        if (!await IsSqlServerAvailableAsync()) return;

        using var db = CreateDbContext();
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
            Assert.Equal("Scheduled", refreshed.Status); // Status must NOT be 'Called'!

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
        if (!await IsSqlServerAvailableAsync()) return;

        using var db = CreateDbContext();
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
}
