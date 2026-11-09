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

public class ReceptionistConcurrencyAndLockingTests
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

    [Fact]
    public void SqlAppLockHelper_Keys_FormatCorrectly()
    {
        var date = new DateOnly(2026, 12, 25);
        var coverageKey = SqlAppLockHelper.ReceptionistCoverageDateKey(date);
        var userDateKey = SqlAppLockHelper.ReceptionistUserDateKey(101, date);
        var leaveKey = SqlAppLockHelper.ReceptionistLeaveKey(999);
        var swapKey = SqlAppLockHelper.ReceptionistSwapKey(888);

        Assert.Equal("Lock_Receptionist_Coverage_20261225", coverageKey);
        Assert.Equal("Lock_Receptionist_101_20261225", userDateKey);
        Assert.Equal("Lock_ReceptionistLeave_999", leaveKey);
        Assert.Equal("Lock_ReceptionistSwap_888", swapKey);
    }

    [Fact]
    public async Task ConcurrentLeaveApprovals_GuaranteesAtMostTwoApprovedLeaves()
    {
        string dbName = Guid.NewGuid().ToString();

        // Seed 4 receptionists and pre-schedule them
        using (var dbSetup = CreateDbContext(dbName))
        {
            var targetDate = new DateOnly(2026, 12, 30);
            for (long i = 1; i <= 4; i++)
            {
                dbSetup.UserAccounts.Add(new UserAccount
                {
                    UserId = 600 + i,
                    Email = $"receptionist{i}@dentalcare.com",
                    Role = UserRole.Receptionist,
                    Status = AccountStatus.Active
                });
                dbSetup.StaffProfiles.Add(new StaffProfile
                {
                    UserId = 600 + i,
                    EmployeeCode = $"REC-06{i}",
                    FullName = $"Lễ Tân Đua {i}",
                    EmploymentStatus = "Active"
                });
                dbSetup.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
                {
                    ReceptionistUserId = 600 + i,
                    WorkDate = targetDate,
                    StartTime = new TimeOnly(7, 0),
                    EndTime = new TimeOnly(19, 0),
                    Status = "Scheduled",
                    CreatedBy = 1,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Already 1 approved leave for receptionist 601
            dbSetup.ReceptionistLeaveRequests.Add(new ReceptionistLeaveRequest
            {
                LeaveRequestId = 7001,
                ReceptionistUserId = 601,
                LeaveType = "FullDay",
                StartDate = targetDate,
                EndDate = targetDate,
                Reason = "Đã duyệt trước",
                Status = "Approved",
                CreatedAt = DateTime.UtcNow
            });
            dbSetup.ReceptionistLeaveRequestPeriods.Add(new ReceptionistLeaveRequestPeriod
            {
                PeriodId = 8001,
                LeaveRequestId = 7001,
                LeaveDate = targetDate,
                Status = "Approved"
            });

            // Two pending leave requests for receptionist 602 and 603
            dbSetup.ReceptionistLeaveRequests.AddRange(
                new ReceptionistLeaveRequest
                {
                    LeaveRequestId = 7002,
                    ReceptionistUserId = 602,
                    LeaveType = "FullDay",
                    StartDate = targetDate,
                    EndDate = targetDate,
                    Reason = "Đơn nghỉ 2",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                },
                new ReceptionistLeaveRequest
                {
                    LeaveRequestId = 7003,
                    ReceptionistUserId = 603,
                    LeaveType = "FullDay",
                    StartDate = targetDate,
                    EndDate = targetDate,
                    Reason = "Đơn nghỉ 3",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                }
            );
            dbSetup.ReceptionistLeaveRequestPeriods.AddRange(
                new ReceptionistLeaveRequestPeriod
                {
                    PeriodId = 8002,
                    LeaveRequestId = 7002,
                    LeaveDate = targetDate,
                    Status = "Pending"
                },
                new ReceptionistLeaveRequestPeriod
                {
                    PeriodId = 8003,
                    LeaveRequestId = 7003,
                    LeaveDate = targetDate,
                    Status = "Pending"
                }
            );

            await dbSetup.SaveChangesAsync();
        }

        // Now run 2 approval tasks concurrently
        var lockObj = new object();
        int successCount = 0;
        int failureCount = 0;

        async Task ApproveTask(long leaveRequestId)
        {
            using var db = CreateDbContext(dbName);
            var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
            var service = new ReceptionistLeaveService(db, coverage, _mockNotify.Object, _mockAudit.Object);

            var res = await service.ApproveLeaveRequestAsync(leaveRequestId, 1);
            lock (lockObj)
            {
                if (res.Success) successCount++;
                else failureCount++;
            }
        }

        // Simulate concurrent execution
        await Task.WhenAll(ApproveTask(7002), ApproveTask(7003));

        // Exactly one should succeed (taking total approved to 2), and one must fail (Rule 1 violation)
        using (var dbVerify = CreateDbContext(dbName))
        {
            var distinctApprovedUsers = await dbVerify.ReceptionistLeaveRequests
                .Where(l => l.Status == "Approved")
                .Select(l => l.ReceptionistUserId)
                .Distinct()
                .CountAsync();

            Assert.Equal(2, distinctApprovedUsers);
            Assert.Equal(1, successCount);
            Assert.Equal(1, failureCount);
        }
    }
}
