using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DentalClinic.Tests.ReceptionistWorkforce;

public class ReceptionistShiftSwapTests
{
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
                Role = "Receptionist",
                Status = "Active"
            });
            db.StaffProfiles.Add(new StaffProfile
            {
                UserId = 300 + i,
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

        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, auditService);
        var service = new ReceptionistShiftSwapService(db, coverage, _mockNotify.Object, auditService);

        var date1 = ClinicTimeHelper.TodayDateOnly.AddDays(5);
        var date2 = ClinicTimeHelper.TodayDateOnly.AddDays(6);

        // Schedule 1: User 301 on date1 Morning
        var sched1 = new StaffWorkSchedule
        {
            WorkScheduleId = 1001,
            StaffUserId = 301,
            StaffRole = "Receptionist",
            WorkDate = date1,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow
        };

        // Schedule 2: User 302 on date2 Morning
        var sched2 = new StaffWorkSchedule
        {
            WorkScheduleId = 1002,
            StaffUserId = 302,
            StaffRole = "Receptionist",
            WorkDate = date2,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow
        };

        db.StaffWorkSchedules.AddRange(sched1, sched2);
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
        Assert.Equal("PendingCounterparty", proposeRes.Data!.Status);

        var swaps = await service.GetSwapRequestsAsync(new ReceptionistSwapFilterRequest());
        Assert.Single(swaps.Data!);
        Assert.Equal("PendingCounterparty", swaps.Data!.First().Status);

        // 2. Counterparty Responds (User 302 accepts)
        var respondRes = await service.RespondToSwapRequestAsync(swapId, 302, new RespondShiftSwapRequest
        {
            Accept = true,
            Note = "Ok tôi đổi được"
        });

        Assert.True(respondRes.Success);
        var swapsAfterPeer = await service.GetSwapRequestsAsync(new ReceptionistSwapFilterRequest());
        Assert.Equal("PendingAdminApproval", swapsAfterPeer.Data!.First().Status);

        // 3. Admin Approves Swap
        var adminDecideRes = await service.AdminDecideSwapRequestAsync(swapId, 1, new AdminDecideShiftSwapRequest
        {
            Approve = true,
            Reason = "Duyệt đổi ca"
        });

        Assert.True(adminDecideRes.Success);
        var swapsFinal = await service.GetSwapRequestsAsync(new ReceptionistSwapFilterRequest());
        Assert.Equal("Approved", swapsFinal.Data!.First().Status);

        // 4. Verify Atomic Swap of Schedules in Database
        var updatedSched1 = await db.StaffWorkSchedules.FindAsync(1001L);
        var updatedSched2 = await db.StaffWorkSchedules.FindAsync(1002L);

        Assert.Equal(302, updatedSched1!.StaffUserId); // Now 302 works on date1
        Assert.Equal(301, updatedSched2!.StaffUserId); // Now 301 works on date2
    }

    [Fact]
    public async Task ShiftSwap_OneWayCoverage_ProposeAcceptApprove_AssignsSuccessfully()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, auditService);
        var service = new ReceptionistShiftSwapService(db, coverage, _mockNotify.Object, auditService);

        var date1 = ClinicTimeHelper.TodayDateOnly.AddDays(7);

        var sched1 = new StaffWorkSchedule
        {
            WorkScheduleId = 1003,
            StaffUserId = 301,
            StaffRole = "Receptionist",
            WorkDate = date1,
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(17, 0),
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow
        };

        db.StaffWorkSchedules.Add(sched1);
        await db.SaveChangesAsync();

        var proposeRes = await service.CreateSwapRequestAsync(301, new CreateReceptionistShiftSwapRequest
        {
            SourceScheduleId = 1003,
            TargetReceptionistUserId = 302,
            TargetScheduleId = null, // One-way coverage
            Reason = "Nhờ trực giúp ca chiều"
        });

        Assert.True(proposeRes.Success);
        var swapId = proposeRes.Data!.SwapRequestId;

        await service.RespondToSwapRequestAsync(swapId, 302, new RespondShiftSwapRequest
        {
            Accept = true
        });

        var adminRes = await service.AdminDecideSwapRequestAsync(swapId, 1, new AdminDecideShiftSwapRequest
        {
            Approve = true
        });

        Assert.True(adminRes.Success);
        var updatedSched1 = await db.StaffWorkSchedules.FindAsync(1003L);
        Assert.Equal(302, updatedSched1!.StaffUserId);
    }

    [Fact]
    public async Task ShiftSwap_CounterpartyDeclines_StopsAtDeclined()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, auditService);
        var service = new ReceptionistShiftSwapService(db, coverage, _mockNotify.Object, auditService);

        var date1 = ClinicTimeHelper.TodayDateOnly.AddDays(8);

        var sched1 = new StaffWorkSchedule
        {
            WorkScheduleId = 2001,
            StaffUserId = 301,
            StaffRole = "Receptionist",
            WorkDate = date1,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffWorkSchedules.Add(sched1);
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
        var swaps = await service.GetSwapRequestsAsync(new ReceptionistSwapFilterRequest());
        Assert.Equal("Declined", swaps.Data!.First().Status);

        // Schedule remains with original user
        var unchangedSched = await db.StaffWorkSchedules.FindAsync(2001L);
        Assert.Equal(301, unchangedSched!.StaffUserId);
    }

    [Fact]
    public async Task ShiftSwap_NonTargetUserCannotRespond()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, auditService);
        var service = new ReceptionistShiftSwapService(db, coverage, _mockNotify.Object, auditService);

        var date1 = ClinicTimeHelper.TodayDateOnly.AddDays(9);

        var sched1 = new StaffWorkSchedule
        {
            WorkScheduleId = 3001,
            StaffUserId = 301,
            StaffRole = "Receptionist",
            WorkDate = date1,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffWorkSchedules.Add(sched1);
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

    [Fact]
    public async Task ShiftSwap_RequesterCanWithdraw()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, auditService);
        var service = new ReceptionistShiftSwapService(db, coverage, _mockNotify.Object, auditService);

        var date1 = ClinicTimeHelper.TodayDateOnly.AddDays(10);

        var sched1 = new StaffWorkSchedule
        {
            WorkScheduleId = 4001,
            StaffUserId = 301,
            StaffRole = "Receptionist",
            WorkDate = date1,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffWorkSchedules.Add(sched1);
        await db.SaveChangesAsync();

        var proposeRes = await service.CreateSwapRequestAsync(301, new CreateReceptionistShiftSwapRequest
        {
            SourceScheduleId = 4001,
            TargetReceptionistUserId = 302,
            Reason = "Đổi ca tạm thời"
        });

        var swapId = proposeRes.Data!.SwapRequestId;

        // Requester withdraws
        var withdrawRes = await service.WithdrawSwapRequestAsync(swapId, 301);
        Assert.True(withdrawRes.Success);

        var swaps = await service.GetSwapRequestsAsync(new ReceptionistSwapFilterRequest());
        Assert.Equal("Withdrawn", swaps.Data!.First().Status);

        // Counterparty tries to respond after withdrawal -> fails
        var respondRes = await service.RespondToSwapRequestAsync(swapId, 302, new RespondShiftSwapRequest { Accept = true });
        Assert.False(respondRes.Success);
    }

    [Fact]
    public async Task ShiftSwap_AdminCannotApprovePendingCounterparty()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, auditService);
        var service = new ReceptionistShiftSwapService(db, coverage, _mockNotify.Object, auditService);

        var date1 = ClinicTimeHelper.TodayDateOnly.AddDays(11);

        var sched1 = new StaffWorkSchedule
        {
            WorkScheduleId = 5001,
            StaffUserId = 301,
            StaffRole = "Receptionist",
            WorkDate = date1,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffWorkSchedules.Add(sched1);
        await db.SaveChangesAsync();

        var proposeRes = await service.CreateSwapRequestAsync(301, new CreateReceptionistShiftSwapRequest
        {
            SourceScheduleId = 5001,
            TargetReceptionistUserId = 302,
            Reason = "Đổi ca"
        });

        var swapId = proposeRes.Data!.SwapRequestId;

        // Admin attempts approval before peer accepts -> fails
        var adminRes = await service.AdminDecideSwapRequestAsync(swapId, 1, new AdminDecideShiftSwapRequest { Approve = true });
        Assert.False(adminRes.Success);
        Assert.Contains("chưa được đồng nghiệp chấp thuận", adminRes.Message ?? "");
    }

    [Fact]
    public async Task ShiftSwap_TargetHasApprovedLeave_Fails()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, auditService);
        var service = new ReceptionistShiftSwapService(db, coverage, _mockNotify.Object, auditService);

        var date1 = ClinicTimeHelper.TodayDateOnly.AddDays(12);

        var sched1 = new StaffWorkSchedule
        {
            WorkScheduleId = 6001,
            StaffUserId = 301,
            StaffRole = "Receptionist",
            WorkDate = date1,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffWorkSchedules.Add(sched1);

        // Add approved leave for 302 on date1
        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 7001,
            StaffUserId = 302,
            StaffRole = "Receptionist",
            LeaveType = "Annual",
            StartDate = date1,
            EndDate = date1,
            Status = "Approved",
            Reason = "Nghỉ phép thường niên",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var proposeRes = await service.CreateSwapRequestAsync(301, new CreateReceptionistShiftSwapRequest
        {
            SourceScheduleId = 6001,
            TargetReceptionistUserId = 302,
            Reason = "Đổi ca"
        });

        Assert.False(proposeRes.Success);
        Assert.Contains("nghỉ phép", proposeRes.Message ?? "");
    }

    [Fact]
    public async Task ShiftSwap_TargetHasConflictingShift_Fails()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, auditService);
        var service = new ReceptionistShiftSwapService(db, coverage, _mockNotify.Object, auditService);

        var date1 = ClinicTimeHelper.TodayDateOnly.AddDays(13);

        var sched1 = new StaffWorkSchedule
        {
            WorkScheduleId = 8001,
            StaffUserId = 301,
            StaffRole = "Receptionist",
            WorkDate = date1,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow
        };

        // User 302 already has an overlapping shift on date1
        var schedConflict = new StaffWorkSchedule
        {
            WorkScheduleId = 8002,
            StaffUserId = 302,
            StaffRole = "Receptionist",
            WorkDate = date1,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(16, 0),
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow
        };

        db.StaffWorkSchedules.AddRange(sched1, schedConflict);
        await db.SaveChangesAsync();

        var proposeRes = await service.CreateSwapRequestAsync(301, new CreateReceptionistShiftSwapRequest
        {
            SourceScheduleId = 8001,
            TargetReceptionistUserId = 302,
            Reason = "Đổi ca"
        });

        Assert.False(proposeRes.Success);
        Assert.Contains("trùng thời gian", proposeRes.Message ?? "");
    }

    [Fact]
    public async Task ShiftSwap_ScheduleHasAttendance_Fails()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, auditService);
        var service = new ReceptionistShiftSwapService(db, coverage, _mockNotify.Object, auditService);

        var date1 = ClinicTimeHelper.TodayDateOnly.AddDays(14);

        var sched1 = new StaffWorkSchedule
        {
            WorkScheduleId = 9001,
            StaffUserId = 301,
            StaffRole = "Receptionist",
            WorkDate = date1,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffWorkSchedules.Add(sched1);

        // Add attendance record for this schedule
        db.StaffAttendanceRecords.Add(new StaffAttendanceRecord
        {
            AttendanceRecordId = 9001,
            StaffUserId = 301,
            WorkScheduleId = 9001,
            WorkDate = date1,
            ClockInTime = date1.ToDateTime(new TimeOnly(7, 5)),
            Status = "OnTime",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var proposeRes = await service.CreateSwapRequestAsync(301, new CreateReceptionistShiftSwapRequest
        {
            SourceScheduleId = 9001,
            TargetReceptionistUserId = 302,
            Reason = "Đổi ca đã có chấm công"
        });
        var swapId = proposeRes.Data!.SwapRequestId;

        // Peer accepts
        await service.RespondToSwapRequestAsync(swapId, 302, new RespondShiftSwapRequest { Accept = true });

        // Admin approval must fail because attendance record exists
        var adminRes = await service.AdminDecideSwapRequestAsync(swapId, 1, new AdminDecideShiftSwapRequest { Approve = true });
        Assert.False(adminRes.Success);
        Assert.Contains("chấm công", adminRes.Message ?? "");
    }
}
