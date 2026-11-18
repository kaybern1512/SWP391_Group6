using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DentalClinic.Tests.ReceptionistWorkforce;

public class ReceptionistSafeBulkSchedulingTests
{
    private readonly Mock<IAuditLogService> _mockAudit = new();
    private readonly Mock<IReceptionistCoverageService> _mockCoverage = new();

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
                UserId = 200 + i,
                Email = $"receptionist{i}@dentalcare.test",
                Role = UserRole.Receptionist,
                Status = AccountStatus.Active
            });

            db.StaffProfiles.Add(new StaffProfile
            {
                UserId = 200 + i,
                FullName = $"Lễ Tân Số {i}",
                EmploymentStatus = "Active"
            });
        }

        // Add a dentist to verify role isolation (REC-BULK-13)
        db.UserAccounts.Add(new UserAccount
        {
            UserId = 301,
            Email = "dentist_iso@dentalcare.test",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        });
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            WorkScheduleId = 999,
            StaffUserId = 301,
            StaffRole = UserRole.Dentist,
            WorkDate = ClinicTimeHelper.TodayDateOnly.AddDays(5),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled",
            Notes = "Dentist original schedule"
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task RECBULK01_SingleReceptionist_MultiDay_AssignsSuccessfully()
    {
        var db = CreateDbContext("RECBULK01");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        var fromDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        var toDate = ClinicTimeHelper.TodayDateOnly.AddDays(4);

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201 },
            FromDate = fromDate,
            ToDate = toDate,
            DaysOfWeek = new List<int> { 1, 2, 3, 4, 5, 6, 7 },
            ShiftIdentifiers = new List<string> { "Morning" }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(3, preview.Data!.TotalProposedShifts);
        Assert.Equal(3, preview.Data!.ValidShiftsCount);

        var applyRes = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(applyRes.Success);

        var count = await db.StaffWorkSchedules
            .CountAsync(s => s.StaffUserId == 201 && s.StaffRole == UserRole.Receptionist && s.Status == "Scheduled");
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task RECBULK02_TwoReceptionists_MultiDay_AssignsSuccessfully()
    {
        var db = CreateDbContext("RECBULK02");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        var fromDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        var toDate = ClinicTimeHelper.TodayDateOnly.AddDays(3);

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201, 202 },
            FromDate = fromDate,
            ToDate = toDate,
            DaysOfWeek = new List<int> { 1, 2, 3, 4, 5, 6, 7 },
            ShiftIdentifiers = new List<string> { "Morning" }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(4, preview.Data!.TotalProposedShifts); // 2 staff * 2 days * 1 shift

        var applyRes = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(applyRes.Success);

        var count = await db.StaffWorkSchedules
            .CountAsync(s => (s.StaffUserId == 201 || s.StaffUserId == 202) && s.StaffRole == UserRole.Receptionist);
        Assert.Equal(4, count);
    }

    [Fact]
    public async Task RECBULK03_MorningShiftOnly_CreatesMorningCanonicalShifts()
    {
        var db = CreateDbContext("RECBULK03");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        var date = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int dow = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201 },
            FromDate = date,
            ToDate = date,
            DaysOfWeek = new List<int> { dow },
            ShiftIdentifiers = new List<string> { "Morning" }
        };

        await service.ApplyBulkScheduleAsync(request, 1);
        var shift = await db.StaffWorkSchedules.FirstOrDefaultAsync(s => s.StaffUserId == 201 && s.WorkDate == date);
        Assert.NotNull(shift);
        Assert.Equal(new TimeOnly(8, 0), shift.StartTime);
        Assert.Equal(new TimeOnly(12, 0), shift.EndTime);
    }

    [Fact]
    public async Task RECBULK04_AfternoonShiftOnly_CreatesAfternoonCanonicalShifts()
    {
        var db = CreateDbContext("RECBULK04");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        var date = ClinicTimeHelper.TodayDateOnly.AddDays(3);
        int dow = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201 },
            FromDate = date,
            ToDate = date,
            DaysOfWeek = new List<int> { dow },
            ShiftIdentifiers = new List<string> { "Afternoon" }
        };

        await service.ApplyBulkScheduleAsync(request, 1);
        var shift = await db.StaffWorkSchedules.FirstOrDefaultAsync(s => s.StaffUserId == 201 && s.WorkDate == date);
        Assert.NotNull(shift);
        Assert.Equal(new TimeOnly(13, 30), shift.StartTime);
        Assert.Equal(new TimeOnly(17, 30), shift.EndTime);
    }

    [Fact]
    public async Task RECBULK05_BothShifts_CreatesTwoShiftsPerDay()
    {
        var db = CreateDbContext("RECBULK05");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        var date = ClinicTimeHelper.TodayDateOnly.AddDays(4);
        int dow = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201 },
            FromDate = date,
            ToDate = date,
            DaysOfWeek = new List<int> { dow },
            ShiftIdentifiers = new List<string> { "Morning", "Afternoon" }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.Equal(2, preview.Data!.TotalProposedShifts);

        await service.ApplyBulkScheduleAsync(request, 1);
        var shifts = await db.StaffWorkSchedules
            .Where(s => s.StaffUserId == 201 && s.WorkDate == date)
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        Assert.Equal(2, shifts.Count);
        Assert.Equal(new TimeOnly(8, 0), shifts[0].StartTime);
        Assert.Equal(new TimeOnly(13, 30), shifts[1].StartTime);
    }

    [Fact]
    public async Task RECBULK07_DuplicateShift_NotInsertedTwice_Idempotent()
    {
        var db = CreateDbContext("RECBULK07");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        var date = ClinicTimeHelper.TodayDateOnly.AddDays(5);
        int dow = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201 },
            FromDate = date,
            ToDate = date,
            DaysOfWeek = new List<int> { dow },
            ShiftIdentifiers = new List<string> { "Morning" }
        };

        // First apply
        await service.ApplyBulkScheduleAsync(request, 1);

        // Second apply
        var preview2 = await service.PreviewBulkScheduleAsync(request);
        Assert.Equal(1, preview2.Data!.DuplicateShiftsCount);
        Assert.Equal(0, preview2.Data!.ValidShiftsCount);

        var apply2 = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(apply2.Success);

        // Still only 1 shift
        var count = await db.StaffWorkSchedules
            .CountAsync(s => s.StaffUserId == 201 && s.WorkDate == date);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RECBULK08_ApprovedLeave_ProtectedFromBulkAssignment()
    {
        var db = CreateDbContext("RECBULK08");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        var date = ClinicTimeHelper.TodayDateOnly.AddDays(6);
        int dow = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 501,
            StaffUserId = 201,
            StaffRole = UserRole.Receptionist,
            StartDate = date,
            EndDate = date,
            LeaveType = "Annual",
            Status = "Approved",
            Reason = "Nghỉ phép lễ tân đã duyệt"
        });
        await db.SaveChangesAsync();

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201 },
            FromDate = date,
            ToDate = date,
            DaysOfWeek = new List<int> { dow },
            ShiftIdentifiers = new List<string> { "Morning" }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.Equal(1, preview.Data!.LeaveConflictCount);
        Assert.Equal(0, preview.Data!.ValidShiftsCount);
        Assert.Equal("OnApprovedLeave", preview.Data.Items.First().Status);

        await service.ApplyBulkScheduleAsync(request, 1);

        // Zero shifts created
        var count = await db.StaffWorkSchedules
            .CountAsync(s => s.StaffUserId == 201 && s.WorkDate == date);
        Assert.Equal(0, count);

        // Leave request remains Approved and intact
        var leave = await db.StaffLeaveRequests.FindAsync(501L);
        Assert.NotNull(leave);
        Assert.Equal("Approved", leave.Status);
    }

    [Fact]
    public async Task RECBULK10_PastDate_ReturnsValidationFailure()
    {
        var db = CreateDbContext("RECBULK10");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        var pastDate = ClinicTimeHelper.TodayDateOnly.AddDays(-2);

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201 },
            FromDate = pastDate,
            ToDate = pastDate,
            DaysOfWeek = new List<int> { 1, 2, 3, 4, 5, 6, 7 },
            ShiftIdentifiers = new List<string> { "Morning" }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.False(preview.Success);
        Assert.Contains("quá khứ", preview.Message);
    }

    [Fact]
    public async Task RECBULK13_DentistSchedulesNotMutated_RoleIsolation()
    {
        var db = CreateDbContext("RECBULK13");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        var targetDate = ClinicTimeHelper.TodayDateOnly.AddDays(5);
        int dow = targetDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)targetDate.DayOfWeek;

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201 },
            FromDate = targetDate,
            ToDate = targetDate,
            DaysOfWeek = new List<int> { dow },
            ShiftIdentifiers = new List<string> { "Morning" }
        };

        await service.ApplyBulkScheduleAsync(request, 1);

        // Dentist schedule must remain completely untouched
        var dentistShift = await db.StaffWorkSchedules.FindAsync(999L);
        Assert.NotNull(dentistShift);
        Assert.Equal(301, dentistShift.StaffUserId);
        Assert.Equal(UserRole.Dentist, dentistShift.StaffRole);
        Assert.Equal("Scheduled", dentistShift.Status);
    }

    [Fact]
    public async Task RECBULK14_StandardConfig_2Staff_5Days_2Shifts_Calculates20ProposedShifts()
    {
        var db = CreateDbContext("RECBULK14");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        // Standard configuration per Addendum Section 2
        var fromDate = new DateOnly(2026, 11, 23); // Monday
        var toDate = new DateOnly(2026, 11, 27);   // Friday

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201, 202 },
            FromDate = fromDate,
            ToDate = toDate,
            DaysOfWeek = new List<int> { 1, 2, 3, 4, 5 },
            ShiftIdentifiers = new List<string> { "Morning", "Afternoon" }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        // 2 staff * 5 days * 2 shifts = exactly 20 proposed shifts
        Assert.Equal(20, preview.Data!.TotalProposedShifts);
        Assert.Equal(20, preview.Data.AvailableCount);
        Assert.Equal(0, preview.Data.AlreadyExistsCount);
        Assert.Equal(0, preview.Data.LeaveBlockedCount);

        var applyRes = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(applyRes.Success);
        Assert.Equal(20, applyRes.Data!.CreatedCount);
        Assert.Equal(20, applyRes.Data.RequestedCount);

        var count = await db.StaffWorkSchedules
            .CountAsync(s => (s.StaffUserId == 201 || s.StaffUserId == 202) && s.StaffRole == UserRole.Receptionist);
        Assert.Equal(20, count);
    }

    [Fact]
    public async Task RECBULK15_RealDatabaseConflictBreakdown_5SummaryCards()
    {
        var db = CreateDbContext("RECBULK15");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        var fromDate = new DateOnly(2026, 11, 23);
        var toDate = new DateOnly(2026, 11, 27);

        // 1. Seed an existing morning shift for 201 on 2026-11-23
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            StaffUserId = 201,
            StaffRole = UserRole.Receptionist,
            WorkDate = new DateOnly(2026, 11, 23),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });

        // 2. Seed an Approved Leave for 202 on 2026-11-24 (morning)
        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 601,
            StaffUserId = 202,
            StaffRole = UserRole.Receptionist,
            StartDate = new DateOnly(2026, 11, 24),
            EndDate = new DateOnly(2026, 11, 24),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            LeaveType = "Annual",
            Status = "Approved",
            Reason = "Nghỉ phép thường niên đã duyệt"
        });

        // 3. Seed a Pending Leave for 201 on 2026-11-25 (afternoon)
        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 602,
            StaffUserId = 201,
            StaffRole = UserRole.Receptionist,
            StartDate = new DateOnly(2026, 11, 25),
            EndDate = new DateOnly(2026, 11, 25),
            StartTime = new TimeOnly(13, 30),
            EndTime = new TimeOnly(17, 30),
            LeaveType = "Personal",
            Status = "Pending",
            Reason = "Đơn nghỉ phép đang chờ duyệt"
        });

        await db.SaveChangesAsync();

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201, 202 },
            FromDate = fromDate,
            ToDate = toDate,
            DaysOfWeek = new List<int> { 1, 2, 3, 4, 5 },
            ShiftIdentifiers = new List<string> { "Morning", "Afternoon" }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);

        // Verify the 5 summary cards
        Assert.Equal(20, preview.Data!.TotalProposedShifts);
        Assert.Equal(17, preview.Data.AvailableCount); // 20 - 1 existing - 1 approved - 1 pending = 17
        Assert.Equal(1, preview.Data.AlreadyExistsCount);
        Assert.Equal(2, preview.Data.LeaveBlockedCount); // 1 approved + 1 pending
        Assert.Equal(0, preview.Data.OtherConflictCount);

        // Verify breakdown items
        var existingItem = preview.Data.Items.First(i => i.ReceptionistUserId == 201 && i.WorkDate == new DateOnly(2026, 11, 23) && i.StartTime == new TimeOnly(8, 0));
        Assert.Equal("AlreadyExists", existingItem.Status);
        Assert.False(existingItem.CanApply);

        var approvedLeaveItem = preview.Data.Items.First(i => i.ReceptionistUserId == 202 && i.WorkDate == new DateOnly(2026, 11, 24) && i.StartTime == new TimeOnly(8, 0));
        Assert.Equal("OnApprovedLeave", approvedLeaveItem.Status);
        Assert.False(approvedLeaveItem.CanApply);

        var pendingLeaveItem = preview.Data.Items.First(i => i.ReceptionistUserId == 201 && i.WorkDate == new DateOnly(2026, 11, 25) && i.StartTime == new TimeOnly(13, 30));
        Assert.Equal("PendingLeave", pendingLeaveItem.Status);
        Assert.False(pendingLeaveItem.CanApply);

        // Apply
        var applyRes = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(applyRes.Success);
        Assert.Equal(17, applyRes.Data!.CreatedCount);
        Assert.Equal(1, applyRes.Data.AlreadyExistsCount);
        Assert.Equal(2, applyRes.Data.SkippedLeaveCount);

        // Verify Approved leave is preserved untouched
        var leave601 = await db.StaffLeaveRequests.FindAsync(601L);
        Assert.Equal("Approved", leave601!.Status);
    }

    [Fact]
    public async Task RECBULK16_ConcurrentLeaveApprovedBetweenPreviewAndApply_IsProtected()
    {
        var db = CreateDbContext("RECBULK16");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        var fromDate = new DateOnly(2026, 11, 23);
        var toDate = new DateOnly(2026, 11, 27);

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201 },
            FromDate = fromDate,
            ToDate = toDate,
            DaysOfWeek = new List<int> { 1, 2, 3, 4, 5 },
            ShiftIdentifiers = new List<string> { "Morning" }
        };

        // 1. Preview shows 5 available shifts
        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.Equal(5, preview.Data!.AvailableCount);

        // 2. Concurrently insert approved leave for 2026-11-25
        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 701,
            StaffUserId = 201,
            StaffRole = UserRole.Receptionist,
            StartDate = new DateOnly(2026, 11, 25),
            EndDate = new DateOnly(2026, 11, 25),
            LeaveType = "Annual",
            Status = "Approved",
            Reason = "Nghỉ phép tương tranh"
        });
        await db.SaveChangesAsync();

        // 3. Apply detects the concurrent leave inside the loop
        var applyRes = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(applyRes.Success);
        Assert.Equal(4, applyRes.Data!.CreatedCount); // Only 4 shifts created, 2026-11-25 was skipped
        Assert.Equal(1, applyRes.Data.SkippedLeaveCount);

        // Zero shifts on leave date
        var leaveDateShift = await db.StaffWorkSchedules
            .FirstOrDefaultAsync(s => s.StaffUserId == 201 && s.WorkDate == new DateOnly(2026, 11, 25));
        Assert.Null(leaveDateShift);
    }

    [Fact]
    public async Task RECBULK17_NegativeTests_ValidationGuards()
    {
        var db = CreateDbContext("RECBULK17");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        // Empty staff
        var reqEmptyStaff = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long>(),
            FromDate = ClinicTimeHelper.TodayDateOnly.AddDays(1),
            ToDate = ClinicTimeHelper.TodayDateOnly.AddDays(2),
            DaysOfWeek = new List<int> { 1, 2 },
            ShiftIdentifiers = new List<string> { "Morning" }
        };
        var p1 = await service.PreviewBulkScheduleAsync(reqEmptyStaff);
        Assert.False(p1.Success);

        // ToDate < FromDate
        var reqInvalidRange = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201 },
            FromDate = ClinicTimeHelper.TodayDateOnly.AddDays(5),
            ToDate = ClinicTimeHelper.TodayDateOnly.AddDays(2),
            DaysOfWeek = new List<int> { 1, 2 },
            ShiftIdentifiers = new List<string> { "Morning" }
        };
        var p2 = await service.PreviewBulkScheduleAsync(reqInvalidRange);
        Assert.False(p2.Success);

        // Empty DOW
        var reqEmptyDow = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201 },
            FromDate = ClinicTimeHelper.TodayDateOnly.AddDays(1),
            ToDate = ClinicTimeHelper.TodayDateOnly.AddDays(2),
            DaysOfWeek = new List<int>(),
            ShiftIdentifiers = new List<string> { "Morning" }
        };
        var p3 = await service.PreviewBulkScheduleAsync(reqEmptyDow);
        Assert.False(p3.Success);
    }

    [Fact]
    public async Task RECBULK18_InvalidStaff_LockedOrNonReceptionist_FlaggedAsInvalidStaff()
    {
        var db = CreateDbContext("RECBULK18");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        // Add a locked receptionist
        db.UserAccounts.Add(new UserAccount
        {
            UserId = 299,
            Email = "locked@dentalcare.test",
            Role = UserRole.Receptionist,
            Status = AccountStatus.Locked
        });
        await db.SaveChangesAsync();

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 299 },
            FromDate = ClinicTimeHelper.TodayDateOnly.AddDays(1),
            ToDate = ClinicTimeHelper.TodayDateOnly.AddDays(1),
            DaysOfWeek = new List<int> { 1, 2, 3, 4, 5, 6, 7 },
            ShiftIdentifiers = new List<string> { "Morning" }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.InvalidStaffCount);
        Assert.Equal("InvalidStaff", preview.Data.Items.First().Status);
        Assert.False(preview.Data.Items.First().CanApply);
    }

    [Fact]
    public async Task RECBULK19_Idempotent_ApplyTwice_ZeroDuplicatesCreated()
    {
        var db = CreateDbContext("RECBULK19");
        await SeedReceptionistsAsync(db);
        var service = new ReceptionistScheduleService(db, _mockCoverage.Object, _mockAudit.Object);

        var fromDate = new DateOnly(2026, 11, 23);
        var toDate = new DateOnly(2026, 11, 27);

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            ReceptionistUserIds = new List<long> { 201, 202 },
            FromDate = fromDate,
            ToDate = toDate,
            DaysOfWeek = new List<int> { 1, 2, 3, 4, 5 },
            ShiftIdentifiers = new List<string> { "Morning", "Afternoon" }
        };

        // First apply: 20 shifts created
        var res1 = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.Equal(20, res1.Data!.CreatedCount);

        // Second apply: 0 created, 20 already exist
        var res2 = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.Equal(0, res2.Data!.CreatedCount);
        Assert.Equal(20, res2.Data.AlreadyExistsCount);

        var total = await db.StaffWorkSchedules
            .CountAsync(s => (s.StaffUserId == 201 || s.StaffUserId == 202) && s.StaffRole == UserRole.Receptionist);
        Assert.Equal(20, total);
    }
}
