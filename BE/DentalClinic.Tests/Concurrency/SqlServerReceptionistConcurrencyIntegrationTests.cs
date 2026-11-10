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
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Concurrency;

[Collection("SqlServerReceptionistIntegration")]
public class SqlServerReceptionistConcurrencyIntegrationTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION")
        ?? "Server=.\\MSSQLSERVER01;Database=DentalCare_Integration_Dev;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";

    private readonly Mock<IAuditLogService> _mockAudit = new();
    private readonly Mock<INotificationService> _mockNotify = new();

    private static async Task<bool> IsSqlServerAvailableAsync()
    {
        try
        {
            using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static DentalClinicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task<List<long>> EnsureTestReceptionistsAsync(DentalClinicDbContext db)
    {
        var existing = await db.UserAccounts
            .Where(u => u.Role == "Receptionist" && u.Status == "Active")
            .OrderBy(u => u.UserId)
            .ToListAsync();

        while (existing.Count < 4)
        {
            int nextIdx = existing.Count + 1;
            var newAccount = new UserAccount
            {
                Email = $"receptionist{nextIdx}_{Guid.NewGuid():N}@dentalcare.com",
                PasswordHash = "AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==",
                Role = "Receptionist",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            db.UserAccounts.Add(newAccount);
            await db.SaveChangesAsync();

            var newProfile = new StaffProfile
            {
                UserId = newAccount.UserId,
                EmployeeCode = $"EMP-REC-{newAccount.UserId:D3}",
                FullName = $"Lễ Tân Test {newAccount.UserId}",
                EmploymentStatus = "Active",
                CreatedAt = DateTime.UtcNow
            };
            db.StaffProfiles.Add(newProfile);
            await db.SaveChangesAsync();

            existing.Add(newAccount);
        }

        return existing.Take(4).Select(u => u.UserId).ToList();
    }

    private static async Task CleanTestDataForDateRangeAsync(DateOnly fromDate, DateOnly toDate)
    {
        using var db = CreateDbContext();
        var leaves = await db.ReceptionistLeaveRequests
            .Where(l => l.StartDate <= toDate && l.EndDate >= fromDate)
            .ToListAsync();
        if (leaves.Any())
        {
            db.ReceptionistLeaveRequests.RemoveRange(leaves);
        }

        var swaps = await db.ReceptionistShiftSwapRequests
            .Where(s => (s.SourceSchedule != null && s.SourceSchedule.WorkDate >= fromDate && s.SourceSchedule.WorkDate <= toDate) ||
                        (s.TargetSchedule != null && s.TargetSchedule.WorkDate >= fromDate && s.TargetSchedule.WorkDate <= toDate))
            .ToListAsync();
        if (swaps.Any())
        {
            db.ReceptionistShiftSwapRequests.RemoveRange(swaps);
        }

        var incidents = await db.ReceptionistCoverageIncidents
            .Where(i => i.WorkDate >= fromDate && i.WorkDate <= toDate)
            .ToListAsync();
        if (incidents.Any())
        {
            db.ReceptionistCoverageIncidents.RemoveRange(incidents);
        }

        var schedules = await db.ReceptionistWorkSchedules
            .Where(s => s.WorkDate >= fromDate && s.WorkDate <= toDate)
            .ToListAsync();
        if (schedules.Any())
        {
            db.ReceptionistWorkSchedules.RemoveRange(schedules);
        }

        await db.SaveChangesAsync();
    }

    // =========================================================================
    // CASE 1: CONCURRENT LEAVE APPROVAL (RULE 1 MAX 2 ENFORCEMENT)
    // =========================================================================
    [Fact]
    public async Task Case1_ConcurrentLeaveApproval_GuaranteesAtMostTwoApprovedLeavesOnSqlServer()
    {
        if (!await IsSqlServerAvailableAsync()) return;

        var targetDate = new DateOnly(2026, 11, 20);
        await CleanTestDataForDateRangeAsync(targetDate, targetDate);

        long userA;
        long userB;
        long userC;
        long userD;

        long leaveBId;
        long leaveCId;

        // 1. Seed schedules: 4 staff working on 2026-11-20 (07:00 - 19:00)
        using (var dbSetup = CreateDbContext())
        {
            var users = await EnsureTestReceptionistsAsync(dbSetup);
            userA = users[0];
            userB = users[1];
            userC = users[2];
            userD = users[3];

            dbSetup.ReceptionistWorkSchedules.AddRange(
                new ReceptionistWorkSchedule { ReceptionistUserId = userA, WorkDate = targetDate, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(19, 0), Status = "Scheduled", CreatedBy = 1, CreatedAt = DateTime.UtcNow },
                new ReceptionistWorkSchedule { ReceptionistUserId = userB, WorkDate = targetDate, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(19, 0), Status = "Scheduled", CreatedBy = 1, CreatedAt = DateTime.UtcNow },
                new ReceptionistWorkSchedule { ReceptionistUserId = userC, WorkDate = targetDate, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(19, 0), Status = "Scheduled", CreatedBy = 1, CreatedAt = DateTime.UtcNow },
                new ReceptionistWorkSchedule { ReceptionistUserId = userD, WorkDate = targetDate, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(19, 0), Status = "Scheduled", CreatedBy = 1, CreatedAt = DateTime.UtcNow }
            );

            // 2. User A already has an APPROVED leave on 2026-11-20 (1st approved)
            var leaveA = new ReceptionistLeaveRequest
            {
                ReceptionistUserId = userA,
                LeaveType = "FullDay",
                StartDate = targetDate,
                EndDate = targetDate,
                Reason = "A đã được duyệt trước",
                Status = "Approved",
                ApprovedBy = 1,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            dbSetup.ReceptionistLeaveRequests.Add(leaveA);
            dbSetup.ReceptionistLeaveRequestPeriods.Add(new ReceptionistLeaveRequestPeriod
            {
                LeaveRequest = leaveA,
                LeaveDate = targetDate,
                Status = "Approved"
            });

            // 3. User B and User C submit pending leave requests
            var leaveB = new ReceptionistLeaveRequest
            {
                ReceptionistUserId = userB,
                LeaveType = "FullDay",
                StartDate = targetDate,
                EndDate = targetDate,
                Reason = "B xin nghỉ",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            var leaveC = new ReceptionistLeaveRequest
            {
                ReceptionistUserId = userC,
                LeaveType = "FullDay",
                StartDate = targetDate,
                EndDate = targetDate,
                Reason = "C xin nghỉ",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            dbSetup.ReceptionistLeaveRequests.AddRange(leaveB, leaveC);
            dbSetup.ReceptionistLeaveRequestPeriods.Add(new ReceptionistLeaveRequestPeriod { LeaveRequest = leaveB, LeaveDate = targetDate, Status = "Pending" });
            dbSetup.ReceptionistLeaveRequestPeriods.Add(new ReceptionistLeaveRequestPeriod { LeaveRequest = leaveC, LeaveDate = targetDate, Status = "Pending" });

            await dbSetup.SaveChangesAsync();
            leaveBId = leaveB.LeaveRequestId;
            leaveCId = leaveC.LeaveRequestId;
        }

        // 4. Concurrently execute Admin 1 approving B and Admin 2 approving C on real SQL Server
        int successCount = 0;
        int failCount = 0;
        var lockObj = new object();

        async Task ApproveLeaveAsync(long leaveId, long adminId)
        {
            using var db = CreateDbContext();
            var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
            var service = new ReceptionistLeaveService(db, coverage, _mockNotify.Object, _mockAudit.Object);

            var res = await service.ApproveLeaveRequestAsync(leaveId, adminId);
            lock (lockObj)
            {
                if (res.Success) successCount++;
                else failCount++;
            }
        }

        // Fire both approvals concurrently across real SQL Server connection sessions
        await Task.WhenAll(
            Task.Run(() => ApproveLeaveAsync(leaveBId, 1)),
            Task.Run(() => ApproveLeaveAsync(leaveCId, 1))
        );

        // 5. Verification on SQL Server: Exactly 1 must succeed, 1 must fail
        Assert.Equal(1, successCount);
        Assert.Equal(1, failCount);

        using (var dbVerify = CreateDbContext())
        {
            var approvedLeaves = await dbVerify.ReceptionistLeaveRequests
                .Where(l => l.StartDate <= targetDate && l.EndDate >= targetDate && l.Status == "Approved")
                .ToListAsync();

            // Total distinct approved users MUST BE EXACTLY 2 (User A + whichever won the race)
            var distinctUsers = approvedLeaves.Select(l => l.ReceptionistUserId).Distinct().ToList();
            Assert.Equal(2, distinctUsers.Count);
            Assert.Contains(userA, distinctUsers);

            var pendingLeaves = await dbVerify.ReceptionistLeaveRequests
                .Where(l => l.StartDate <= targetDate && l.EndDate >= targetDate && l.Status == "Pending")
                .ToListAsync();
            Assert.Single(pendingLeaves); // The loser remained Pending
        }
    }

    // =========================================================================
    // CASE 2: LEAVE APPROVAL VS SHIFT SWAP CONCURRENCY
    // =========================================================================
    [Fact]
    public async Task Case2_LeaveApprovalVsShiftSwap_MaintainsCoverageAndNoCorruptedSchedules()
    {
        if (!await IsSqlServerAvailableAsync()) return;

        var targetDate = new DateOnly(2026, 11, 21);
        await CleanTestDataForDateRangeAsync(targetDate, targetDate);

        long userB;
        long userC;
        long userD;

        long swapId;
        long leaveCId;

        using (var dbSetup = CreateDbContext())
        {
            var users = await EnsureTestReceptionistsAsync(dbSetup);
            userB = users[0];
            userC = users[1];
            userD = users[2];

            // B and C on Morning Shift (07:00 - 13:00)
            var sB = new ReceptionistWorkSchedule { ReceptionistUserId = userB, WorkDate = targetDate, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(13, 0), Status = "Scheduled", CreatedBy = 1, CreatedAt = DateTime.UtcNow };
            var sC = new ReceptionistWorkSchedule { ReceptionistUserId = userC, WorkDate = targetDate, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(13, 0), Status = "Scheduled", CreatedBy = 1, CreatedAt = DateTime.UtcNow };
            // D on Afternoon Shift (13:00 - 19:00)
            var sD = new ReceptionistWorkSchedule { ReceptionistUserId = userD, WorkDate = targetDate, StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(19, 0), Status = "Scheduled", CreatedBy = 1, CreatedAt = DateTime.UtcNow };

            dbSetup.ReceptionistWorkSchedules.AddRange(sB, sC, sD);
            await dbSetup.SaveChangesAsync();

            // B requests swap with D (swap morning with afternoon)
            var swap = new ReceptionistShiftSwapRequest
            {
                RequestingReceptionistUserId = userB,
                TargetReceptionistUserId = userD,
                SourceScheduleId = sB.ReceptionistScheduleId,
                TargetScheduleId = sD.ReceptionistScheduleId,
                Reason = "Đổi ca chiều",
                Status = "PendingAdminApproval", // D already agreed
                CreatedAt = DateTime.UtcNow
            };
            dbSetup.ReceptionistShiftSwapRequests.Add(swap);

            // C requests leave for Morning Shift
            var leaveC = new ReceptionistLeaveRequest
            {
                ReceptionistUserId = userC,
                LeaveType = "HalfDayMorning",
                StartDate = targetDate,
                EndDate = targetDate,
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(13, 0),
                Reason = "C xin nghỉ sáng",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            dbSetup.ReceptionistLeaveRequests.Add(leaveC);
            dbSetup.ReceptionistLeaveRequestPeriods.Add(new ReceptionistLeaveRequestPeriod { LeaveRequest = leaveC, LeaveDate = targetDate, Status = "Pending" });

            await dbSetup.SaveChangesAsync();
            swapId = swap.SwapRequestId;
            leaveCId = leaveC.LeaveRequestId;
        }

        // Concurrently run Admin 1 deciding swap and Admin 2 approving C's leave
        var t1 = Task.Run(async () =>
        {
            using var db = CreateDbContext();
            var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
            var swapService = new ReceptionistShiftSwapService(db, coverage, _mockNotify.Object, _mockAudit.Object);
            return await swapService.AdminDecideSwapRequestAsync(swapId, 1, new AdminDecideShiftSwapRequest { Approve = true, Reason = "Duyệt đổi ca" });
        });

        var t2 = Task.Run(async () =>
        {
            using var db = CreateDbContext();
            var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
            var leaveService = new ReceptionistLeaveService(db, coverage, _mockNotify.Object, _mockAudit.Object);
            return await leaveService.ApproveLeaveRequestAsync(leaveCId, 1);
        });

        await Task.WhenAll(t1, t2);

        // Verification: If leave C was approved, morning shift would have dropped below 2 unless D swapped in.
        // In either case, the database must have 0 overlapping/corrupted schedules for the same user.
        using (var dbVerify = CreateDbContext())
        {
            var userSchedules = await dbVerify.ReceptionistWorkSchedules
                .Where(s => s.WorkDate == targetDate && s.Status == "Scheduled")
                .ToListAsync();

            // Check no duplicate shifts for same user overlapping in time
            foreach (var group in userSchedules.GroupBy(s => s.ReceptionistUserId))
            {
                var list = group.OrderBy(s => s.StartTime).ToList();
                for (int i = 0; i < list.Count - 1; i++)
                {
                    Assert.True(list[i].EndTime <= list[i + 1].StartTime, "Schedules must not overlap for the same receptionist.");
                }
            }
        }
    }

    // =========================================================================
    // CASE 3: LEAVE APPROVAL VS BULK SCHEDULING CONCURRENCY
    // =========================================================================
    [Fact]
    public async Task Case3_LeaveApprovalVsBulkScheduling_ProtectsApprovedLeaveAndNoOrphanShifts()
    {
        if (!await IsSqlServerAvailableAsync()) return;

        var targetDate = new DateOnly(2026, 11, 22);
        await CleanTestDataForDateRangeAsync(targetDate, targetDate);

        long userB;
        long userC;
        long leaveBId;

        using (var dbSetup = CreateDbContext())
        {
            var users = await EnsureTestReceptionistsAsync(dbSetup);
            userB = users[0];
            userC = users[1];

            // User B has pending leave for whole day 2026-11-22
            var leaveB = new ReceptionistLeaveRequest
            {
                ReceptionistUserId = userB,
                LeaveType = "FullDay",
                StartDate = targetDate,
                EndDate = targetDate,
                Reason = "Nghỉ phép",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            dbSetup.ReceptionistLeaveRequests.Add(leaveB);
            dbSetup.ReceptionistLeaveRequestPeriods.Add(new ReceptionistLeaveRequestPeriod { LeaveRequest = leaveB, LeaveDate = targetDate, Status = "Pending" });

            // User C has a morning shift
            dbSetup.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
            {
                ReceptionistUserId = userC,
                WorkDate = targetDate,
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(13, 0),
                Status = "Scheduled",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            });

            await dbSetup.SaveChangesAsync();
            leaveBId = leaveB.LeaveRequestId;
        }

        // Concurrently run Admin 1 approving B's leave and Admin 2 applying bulk schedule
        var t1 = Task.Run(async () =>
        {
            using var db = CreateDbContext();
            var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
            var leaveService = new ReceptionistLeaveService(db, coverage, _mockNotify.Object, _mockAudit.Object);
            return await leaveService.ApproveLeaveRequestAsync(leaveBId, 1);
        });

        var t2 = Task.Run(async () =>
        {
            using var db = CreateDbContext();
            var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
            var scheduleService = new ReceptionistScheduleService(db, coverage, _mockAudit.Object);
            var req = new ReceptionistBulkScheduleAssignmentRequest
            {
                Year = 2026,
                Month = 11,
                ReceptionistUserIds = new List<long> { userB, userC },
                DaysOfWeek = new List<int> { (int)targetDate.DayOfWeek == 0 ? 7 : (int)targetDate.DayOfWeek },
                ShiftTemplateId = 1,
                OverwriteExisting = false
            };
            return await scheduleService.ApplyBulkScheduleAsync(req, 1);
        });

        await Task.WhenAll(t1, t2);

        using (var dbVerify = CreateDbContext())
        {
            var isLeaveApproved = await dbVerify.ReceptionistLeaveRequests
                .AnyAsync(l => l.LeaveRequestId == leaveBId && l.Status == "Approved");

            if (isLeaveApproved)
            {
                // If B's leave was approved, B must NOT be scheduled on targetDate
                var bSchedules = await dbVerify.ReceptionistWorkSchedules
                    .Where(s => s.ReceptionistUserId == userB && s.WorkDate == targetDate && s.Status == "Scheduled")
                    .ToListAsync();
                Assert.Empty(bSchedules);
            }
        }
    }

    // =========================================================================
    // CASE 4: DUPLICATE LEAVE SUBMISSION (DOUBLE CLICK PROTECTION)
    // =========================================================================
    [Fact]
    public async Task Case4_DuplicateLeaveSubmission_PreventsDuplicateRowsUnderConcurrentCalls()
    {
        if (!await IsSqlServerAvailableAsync()) return;

        var targetDate = new DateOnly(2026, 11, 23);
        await CleanTestDataForDateRangeAsync(targetDate, targetDate);

        long userB;

        using (var dbSetup = CreateDbContext())
        {
            var users = await EnsureTestReceptionistsAsync(dbSetup);
            userB = users[0];

            // Seed schedule for B so they can apply
            dbSetup.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
            {
                ReceptionistUserId = userB,
                WorkDate = targetDate,
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(13, 0),
                Status = "Scheduled",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            });
            await dbSetup.SaveChangesAsync();
        }

        var req = new CreateReceptionistLeaveRequest
        {
            LeaveType = "FullDay",
            StartDate = targetDate,
            EndDate = targetDate,
            Reason = "Đơn nghỉ kiểm tra double-click"
        };

        // Two identical calls submitted at the same exact millisecond
        var t1 = Task.Run(async () =>
        {
            using var db = CreateDbContext();
            var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
            var leaveService = new ReceptionistLeaveService(db, coverage, _mockNotify.Object, _mockAudit.Object);
            return await leaveService.CreateLeaveRequestAsync(userB, req);
        });

        var t2 = Task.Run(async () =>
        {
            using var db = CreateDbContext();
            var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
            var leaveService = new ReceptionistLeaveService(db, coverage, _mockNotify.Object, _mockAudit.Object);
            return await leaveService.CreateLeaveRequestAsync(userB, req);
        });

        var results = await Task.WhenAll(t1, t2);

        // Exactly one submission must succeed, and one must be blocked by duplicate check
        int createdCount = results.Count(r => r.Success);
        int blockedCount = results.Count(r => !r.Success);

        Assert.Equal(1, createdCount);
        Assert.Equal(1, blockedCount);

        using (var dbVerify = CreateDbContext())
        {
            var userLeavesInDb = await dbVerify.ReceptionistLeaveRequests
                .Where(l => l.ReceptionistUserId == userB && l.StartDate == targetDate)
                .ToListAsync();

            Assert.Single(userLeavesInDb); // Exactly 1 row in SQL Server, 0 duplicate records!
        }
    }
}
