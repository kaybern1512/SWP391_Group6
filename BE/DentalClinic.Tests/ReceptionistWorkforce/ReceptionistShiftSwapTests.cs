using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DentalClinic.Tests.ReceptionistWorkforce;

public class ReceptionistShiftSwapTests
{
    private readonly Mock<IAuditLogService> _mockAudit = new();
    private readonly Mock<INotificationService> _mockNotify = new();

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedReceptionistsAsync(DentalClinicDbContext db)
    {
        for (long i = 1; i <= 3; i++)
        {
            db.UserAccounts.Add(new UserAccount
            {
                UserId = 300 + i,
                Email = $"receptionist{i}@dentalcare.com",
                Role = UserRole.Receptionist,
                Status = AccountStatus.Active
            });
            db.StaffProfiles.Add(new StaffProfile
            {
                UserId = 300 + i,
                EmployeeCode = $"REC-03{i}",
                FullName = $"Lễ Tân Đổi Ca {i}",
                EmploymentStatus = "Active"
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ShiftSwap_FullLifecycle_ProposeAcceptApprove_SwapsSuccessfully()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var service = new ReceptionistShiftSwapService(db, coverage, _mockNotify.Object, _mockAudit.Object);

        var date1 = new DateOnly(2026, 12, 1);
        var date2 = new DateOnly(2026, 12, 2);

        // Schedule 1: User 301 on Dec 1 Morning
        var sched1 = new ReceptionistWorkSchedule
        {
            ReceptionistScheduleId = 1001,
            ReceptionistUserId = 301,
            WorkDate = date1,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        };

        // Schedule 2: User 302 on Dec 2 Morning
        var sched2 = new ReceptionistWorkSchedule
        {
            ReceptionistScheduleId = 1002,
            ReceptionistUserId = 302,
            WorkDate = date2,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        };

        db.ReceptionistWorkSchedules.AddRange(sched1, sched2);
        await db.SaveChangesAsync();

        // 1. Propose Swap (User 301 proposes to 302)
        var proposeRes = await service.CreateSwapRequestAsync(301, new CreateReceptionistShiftSwapRequest
        {
            SourceScheduleId = 1001,
            TargetReceptionistUserId = 302,
            TargetScheduleId = 1002,
            Reason = "Trùng lịch học cá nhân"
        });

        Assert.True(proposeRes.Success);
        var swapId = proposeRes.Data!.SwapRequestId;
        var swapInDb = await db.ReceptionistShiftSwapRequests.FindAsync(swapId);
        Assert.Equal("PendingCounterparty", swapInDb!.Status);

        // 2. Counterparty Responds (User 302 accepts)
        var respondRes = await service.RespondToSwapRequestAsync(swapId, 302, new RespondShiftSwapRequest
        {
            Accept = true,
            Note = "Ok tôi đổi được"
        });

        Assert.True(respondRes.Success);
        var swapAfterResponse = await db.ReceptionistShiftSwapRequests.FindAsync(swapId);
        Assert.Equal("PendingAdminApproval", swapAfterResponse!.Status);

        // 3. Admin Approves Swap
        var adminDecideRes = await service.AdminDecideSwapRequestAsync(swapId, 1, new AdminDecideShiftSwapRequest
        {
            Approve = true,
            Reason = "Duyệt đổi ca"
        });

        Assert.True(adminDecideRes.Success);
        var swapFinal = await db.ReceptionistShiftSwapRequests.FindAsync(swapId);
        Assert.Equal("Approved", swapFinal!.Status);

        // 4. Verify Atomic Swap of Schedules in Database
        var updatedSched1 = await db.ReceptionistWorkSchedules.FindAsync(1001L);
        var updatedSched2 = await db.ReceptionistWorkSchedules.FindAsync(1002L);

        Assert.Equal(302, updatedSched1!.ReceptionistUserId); // Now 302 works on Dec 1
        Assert.Equal(301, updatedSched2!.ReceptionistUserId); // Now 301 works on Dec 2
    }

    [Fact]
    public async Task ShiftSwap_CounterpartyDeclines_StopsAtDeclined()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var service = new ReceptionistShiftSwapService(db, coverage, _mockNotify.Object, _mockAudit.Object);

        var sched1 = new ReceptionistWorkSchedule
        {
            ReceptionistScheduleId = 2001,
            ReceptionistUserId = 301,
            WorkDate = new DateOnly(2026, 12, 5),
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.ReceptionistWorkSchedules.Add(sched1);
        await db.SaveChangesAsync();

        var proposeRes = await service.CreateSwapRequestAsync(301, new CreateReceptionistShiftSwapRequest
        {
            SourceScheduleId = 2001,
            TargetReceptionistUserId = 302,
            Reason = "Nhờ trực giúp ca sáng"
        });

        var swapId = proposeRes.Data!.SwapRequestId;

        // Counterparty declines
        var respondRes = await service.RespondToSwapRequestAsync(swapId, 302, new RespondShiftSwapRequest
        {
            Accept = false,
            Note = "Bận việc gia đình không trực thay được"
        });

        Assert.True(respondRes.Success);
        var swap = await db.ReceptionistShiftSwapRequests.FindAsync(swapId);
        Assert.Equal("Declined", swap!.Status);

        // Schedule remains with original user
        var unchangedSched = await db.ReceptionistWorkSchedules.FindAsync(2001L);
        Assert.Equal(301, unchangedSched!.ReceptionistUserId);
    }

    [Fact]
    public async Task ShiftSwap_NonTargetUserCannotRespond()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var service = new ReceptionistShiftSwapService(db, coverage, _mockNotify.Object, _mockAudit.Object);

        var sched1 = new ReceptionistWorkSchedule
        {
            ReceptionistScheduleId = 3001,
            ReceptionistUserId = 301,
            WorkDate = new DateOnly(2026, 12, 7),
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.ReceptionistWorkSchedules.Add(sched1);
        await db.SaveChangesAsync();

        var proposeRes = await service.CreateSwapRequestAsync(301, new CreateReceptionistShiftSwapRequest
        {
            SourceScheduleId = 3001,
            TargetReceptionistUserId = 302,
            Reason = "Đổi ca"
        });

        var swapId = proposeRes.Data!.SwapRequestId;

        // User 303 attempts to respond instead of 302
        var respondRes = await service.RespondToSwapRequestAsync(swapId, 303, new RespondShiftSwapRequest
        {
            Accept = true
        });

        Assert.False(respondRes.Success);
        Assert.Contains("người nhận", respondRes.Message ?? "");
    }
}
