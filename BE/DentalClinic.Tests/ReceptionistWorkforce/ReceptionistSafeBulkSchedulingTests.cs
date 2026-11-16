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
}
