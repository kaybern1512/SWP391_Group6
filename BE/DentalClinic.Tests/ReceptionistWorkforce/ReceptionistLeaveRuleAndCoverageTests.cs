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

public class ReceptionistLeaveRuleAndCoverageTests
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

    private static async Task SeedReceptionistsAndTemplatesAsync(DentalClinicDbContext db)
    {
        for (long i = 1; i <= 5; i++)
        {
            db.UserAccounts.Add(new UserAccount
            {
                UserId = 200 + i,
                Email = $"receptionist{i}@dentalcare.com",
                Role = UserRole.Receptionist,
                Status = AccountStatus.Active
            });
            db.StaffProfiles.Add(new StaffProfile
            {
                UserId = 200 + i,
                EmployeeCode = $"REC-02{i}",
                FullName = $"Lễ Tân {i}",
                EmploymentStatus = "Active"
            });
        }

        db.ReceptionistShiftTemplates.AddRange(
            new ReceptionistShiftTemplate
            {
                ShiftTemplateId = 1,
                TemplateName = "Ca Sáng",
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(13, 0),
                MinStaffRequired = 2,
                IsActive = true,
                DisplayOrder = 1
            },
            new ReceptionistShiftTemplate
            {
                ShiftTemplateId = 2,
                TemplateName = "Ca Chiều",
                StartTime = new TimeOnly(13, 0),
                EndTime = new TimeOnly(19, 0),
                MinStaffRequired = 2,
                IsActive = true,
                DisplayOrder = 2
            }
        );

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Rule1_MaxTwoApprovedLeavesPerDay_ThirdReceptionistFails()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAndTemplatesAsync(db);

        var coverageService = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var leaveService = new ReceptionistLeaveService(db, coverageService, _mockNotify.Object, _mockAudit.Object);

        var targetDate = new DateOnly(2026, 12, 15);

        // Pre-schedule 4 receptionists so Rule 2 won't fail
        for (long i = 1; i <= 4; i++)
        {
            db.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
            {
                ReceptionistUserId = 200 + i,
                WorkDate = targetDate,
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(19, 0),
                Status = "Scheduled",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();

        // 1st Receptionist submits and gets approved
        var req1 = await leaveService.CreateLeaveRequestAsync(201, new CreateReceptionistLeaveRequest
        {
            LeaveType = "FullDay",
            StartDate = targetDate,
            EndDate = targetDate,
            Reason = "Nghỉ việc riêng"
        });
        Assert.True(req1.Success);
        var app1 = await leaveService.ApproveLeaveRequestAsync(req1.Data!.LeaveRequestId, 1);
        Assert.True(app1.Success);

        // 2nd Receptionist submits and gets approved
        var req2 = await leaveService.CreateLeaveRequestAsync(202, new CreateReceptionistLeaveRequest
        {
            LeaveType = "FullDay",
            StartDate = targetDate,
            EndDate = targetDate,
            Reason = "Khám sức khỏe"
        });
        Assert.True(req2.Success);
        var app2 = await leaveService.ApproveLeaveRequestAsync(req2.Data!.LeaveRequestId, 1);
        Assert.True(app2.Success);

        // 3rd Receptionist submits - try to approve -> MUST FAIL Rule 1
        var req3 = await leaveService.CreateLeaveRequestAsync(203, new CreateReceptionistLeaveRequest
        {
            LeaveType = "FullDay",
            StartDate = targetDate,
            EndDate = targetDate,
            Reason = "Việc gia đình"
        });
        Assert.True(req3.Success);
        var app3 = await leaveService.ApproveLeaveRequestAsync(req3.Data!.LeaveRequestId, 1);

        Assert.False(app3.Success);
        Assert.Contains("tối đa 2 lễ tân", app3.Message ?? "");

        // Verify database state: only 2 approved leaves exist
        var approvedCount = await db.ReceptionistLeaveRequests.CountAsync(l => l.Status == "Approved");
        Assert.Equal(2, approvedCount);

        var thirdReq = await db.ReceptionistLeaveRequests.FindAsync(req3.Data!.LeaveRequestId);
        Assert.Equal("Pending", thirdReq!.Status);
    }

    [Fact]
    public async Task Rule1_SameReceptionistMultipleLeavePeriods_CountsAsOne()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAndTemplatesAsync(db);

        var coverageService = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var leaveService = new ReceptionistLeaveService(db, coverageService, _mockNotify.Object, _mockAudit.Object);

        var targetDate = new DateOnly(2026, 12, 16);

        // Pre-schedule 4 receptionists for full day
        for (long i = 1; i <= 4; i++)
        {
            db.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
            {
                ReceptionistUserId = 200 + i,
                WorkDate = targetDate,
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(19, 0),
                Status = "Scheduled",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();

        // Receptionist 1 gets 2 hourly leaves approved on the same day (morning 8-10, afternoon 14-16)
        var req1a = await leaveService.CreateLeaveRequestAsync(201, new CreateReceptionistLeaveRequest
        {
            LeaveType = "Hourly",
            StartDate = targetDate,
            EndDate = targetDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(10, 0),
            Reason = "Việc sáng"
        });
        Assert.True(req1a.Success);
        var app1a = await leaveService.ApproveLeaveRequestAsync(req1a.Data!.LeaveRequestId, 1);
        Assert.True(app1a.Success);

        var req1b = await leaveService.CreateLeaveRequestAsync(201, new CreateReceptionistLeaveRequest
        {
            LeaveType = "Hourly",
            StartDate = targetDate,
            EndDate = targetDate,
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(16, 0),
            Reason = "Việc chiều"
        });
        Assert.True(req1b.Success);
        var app1b = await leaveService.ApproveLeaveRequestAsync(req1b.Data!.LeaveRequestId, 1);
        Assert.True(app1b.Success);

        // Receptionist 2 applies for full day leave -> Should SUCCEED because distinct users on leave = 1 (only User 201)
        var req2 = await leaveService.CreateLeaveRequestAsync(202, new CreateReceptionistLeaveRequest
        {
            LeaveType = "FullDay",
            StartDate = targetDate,
            EndDate = targetDate,
            Reason = "Nghỉ phép"
        });
        Assert.True(req2.Success);
        var app2 = await leaveService.ApproveLeaveRequestAsync(req2.Data!.LeaveRequestId, 1);
        Assert.True(app2.Success);

        // Now distinct users on leave = 2 (201 and 202). A 3rd user (203) MUST FAIL
        var req3 = await leaveService.CreateLeaveRequestAsync(203, new CreateReceptionistLeaveRequest
        {
            LeaveType = "FullDay",
            StartDate = targetDate,
            EndDate = targetDate,
            Reason = "Nghỉ phép 3"
        });
        Assert.True(req3.Success);
        var app3 = await leaveService.ApproveLeaveRequestAsync(req3.Data!.LeaveRequestId, 1);

        Assert.False(app3.Success);
        Assert.Contains("tối đa 2 lễ tân", app3.Message ?? "");
    }

    [Fact]
    public async Task Rule2_MinTwoAvailablePerShift_FailsWhenDroppingToOne()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAndTemplatesAsync(db);

        var coverageService = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var leaveService = new ReceptionistLeaveService(db, coverageService, _mockNotify.Object, _mockAudit.Object);

        var targetDate = new DateOnly(2026, 12, 17);

        // Only 2 receptionists scheduled for morning shift (07:00 - 13:00)
        db.ReceptionistWorkSchedules.AddRange(
            new ReceptionistWorkSchedule
            {
                ReceptionistUserId = 201,
                WorkDate = targetDate,
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(13, 0),
                Status = "Scheduled",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            },
            new ReceptionistWorkSchedule
            {
                ReceptionistUserId = 202,
                WorkDate = targetDate,
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(13, 0),
                Status = "Scheduled",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        // User 201 applies for leave -> If approved, only User 202 remains (1 available < 2 min required)
        var req = await leaveService.CreateLeaveRequestAsync(201, new CreateReceptionistLeaveRequest
        {
            LeaveType = "FullDay",
            StartDate = targetDate,
            EndDate = targetDate,
            Reason = "Xin nghỉ cá nhân"
        });
        Assert.True(req.Success);

        var app = await leaveService.ApproveLeaveRequestAsync(req.Data!.LeaveRequestId, 1);

        Assert.False(app.Success);
        Assert.Contains("tối thiểu 2 lễ tân", app.Message ?? "");
    }

    [Fact]
    public async Task Rule2_HourlySubIntervalCheck_FailsWhenSpecificHourDropsBelowTwo()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAndTemplatesAsync(db);

        var coverageService = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var leaveService = new ReceptionistLeaveService(db, coverageService, _mockNotify.Object, _mockAudit.Object);

        var targetDate = new DateOnly(2026, 12, 18);

        // 2 receptionists scheduled 07:00 - 13:00
        db.ReceptionistWorkSchedules.AddRange(
            new ReceptionistWorkSchedule
            {
                ReceptionistUserId = 201,
                WorkDate = targetDate,
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(13, 0),
                Status = "Scheduled",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            },
            new ReceptionistWorkSchedule
            {
                ReceptionistUserId = 202,
                WorkDate = targetDate,
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(13, 0),
                Status = "Scheduled",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        // User 201 asks for Hourly leave from 09:00 to 11:00
        var req = await leaveService.CreateLeaveRequestAsync(201, new CreateReceptionistLeaveRequest
        {
            LeaveType = "Hourly",
            StartDate = targetDate,
            EndDate = targetDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(11, 0),
            Reason = "Việc riêng 2 tiếng"
        });
        Assert.True(req.Success);

        var app = await leaveService.ApproveLeaveRequestAsync(req.Data!.LeaveRequestId, 1);

        Assert.False(app.Success);
        Assert.Contains("tối thiểu 2 lễ tân", app.Message ?? "");
    }

    [Fact]
    public async Task LeaveLifecycle_Withdraw_OnlyAllowedWhenPending()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAndTemplatesAsync(db);

        var coverageService = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var leaveService = new ReceptionistLeaveService(db, coverageService, _mockNotify.Object, _mockAudit.Object);

        var targetDate = new DateOnly(2026, 12, 20);

        // Pre-schedule user 201 on targetDate so they can request leave
        db.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
        {
            ReceptionistUserId = 201,
            WorkDate = targetDate,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var req = await leaveService.CreateLeaveRequestAsync(201, new CreateReceptionistLeaveRequest
        {
            LeaveType = "FullDay",
            StartDate = targetDate,
            EndDate = targetDate,
            Reason = "Xin nghỉ"
        });
        Assert.True(req.Success);

        // Withdraw while pending
        var withdrawRes = await leaveService.WithdrawLeaveRequestAsync(req.Data!.LeaveRequestId, 201);
        Assert.True(withdrawRes.Success);

        var leave = await db.ReceptionistLeaveRequests.FindAsync(req.Data!.LeaveRequestId);
        Assert.Equal("Withdrawn", leave!.Status);

        // Attempting to withdraw again must fail
        var secondWithdraw = await leaveService.WithdrawLeaveRequestAsync(req.Data!.LeaveRequestId, 201);
        Assert.False(secondWithdraw.Success);
    }

    [Fact]
    public async Task LeaveLifecycle_Reject_MarksStatusAndSavesReason()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAndTemplatesAsync(db);

        var coverageService = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var leaveService = new ReceptionistLeaveService(db, coverageService, _mockNotify.Object, _mockAudit.Object);

        var targetDate = new DateOnly(2026, 12, 21);

        // Pre-schedule user 201 on targetDate
        db.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
        {
            ReceptionistUserId = 201,
            WorkDate = targetDate,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var req = await leaveService.CreateLeaveRequestAsync(201, new CreateReceptionistLeaveRequest
        {
            LeaveType = "FullDay",
            StartDate = targetDate,
            EndDate = targetDate,
            Reason = "Xin nghỉ phép"
        });
        Assert.True(req.Success);

        var rejectRes = await leaveService.RejectLeaveRequestAsync(req.Data!.LeaveRequestId, "Không đủ nhân sự trực ngày đó", 1);
        Assert.True(rejectRes.Success);

        var leave = await db.ReceptionistLeaveRequests.FindAsync(req.Data!.LeaveRequestId);
        Assert.Equal("Rejected", leave!.Status);
        Assert.Equal("Không đủ nhân sự trực ngày đó", leave.RejectionReason);
    }
}
