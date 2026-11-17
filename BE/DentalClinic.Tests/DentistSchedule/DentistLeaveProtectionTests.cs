using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.DentistSchedule.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DentalClinic.Tests.DentistSchedule;

public class DentistLeaveProtectionTests
{
    private readonly Mock<IAuditLogService> _mockAudit = new();

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedBaselineAsync(DentalClinicDbContext db)
    {
        db.Departments.Add(new Department
        {
            DepartmentId = 1,
            Name = "Khoa Tổng Quát",
            Status = "Active"
        });

        for (long i = 1; i <= 3; i++)
        {
            db.UserAccounts.Add(new UserAccount
            {
                UserId = 10 + i,
                Email = $"dentist{i}@dentalcare.test",
                Role = UserRole.Dentist,
                Status = AccountStatus.Active
            });

            db.StaffProfiles.Add(new StaffProfile
            {
                UserId = 10 + i,
                FullName = $"BS. Nha Khoa {i}",
                EmploymentStatus = "Active"
            });

            db.DentistProfiles.Add(new DentistProfile
            {
                UserId = 10 + i,
                DepartmentId = 1,
                LicenseNumber = $"LIC-{i}",
                Qualification = "Chuyên khoa RHM"
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task LEAVE01_ApprovedLeave_RecognizedInPreview_MarkedOnApprovedLeave_CanApplyFalse()
    {
        var db = CreateDbContext("LEAVE01_Preview");
        await SeedBaselineAsync(db);
        var service = new DentistScheduleService(db, _mockAudit.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(3);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 101,
            StaffUserId = 11,
            StaffRole = UserRole.Dentist,
            StartDate = futureDate,
            EndDate = futureDate,
            LeaveType = "Annual",
            Status = "Approved",
            Reason = "Nghỉ phép thường niên"
        });
        await db.SaveChangesAsync();

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 11 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime, ShiftIdentifier = "Morning" }
            }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.LeaveConflictCount);

        var item = preview.Data.Items.First();
        Assert.Equal("OnApprovedLeave", item.Status);
        Assert.False(item.CanApply);
        Assert.Contains("NGHỈ PHÉP ĐÃ DUYỆT", item.Message);
    }

    [Fact]
    public async Task LEAVE02_ApprovedLeave_ApplyDoesNotCreateShift()
    {
        var db = CreateDbContext("LEAVE02_ApplyNoShift");
        await SeedBaselineAsync(db);
        var service = new DentistScheduleService(db, _mockAudit.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(4);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 102,
            StaffUserId = 11,
            StaffRole = UserRole.Dentist,
            StartDate = futureDate,
            EndDate = futureDate,
            LeaveType = "Annual",
            Status = "Approved",
            Reason = "Nghỉ kết hôn"
        });
        await db.SaveChangesAsync();

        var applyRequest = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 11 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime, ShiftIdentifier = "Morning" }
            },
            SkipConflicts = true
        };

        var result = await service.ApplyBulkScheduleAsync(applyRequest, 1);
        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.CreatedCount);

        // Verify database: 0 work schedules for dentist 11 on that date
        var scheduleCount = await db.StaffWorkSchedules
            .CountAsync(s => s.StaffUserId == 11 && s.WorkDate == futureDate);
        Assert.Equal(0, scheduleCount);
    }

    [Fact]
    public async Task LEAVE03_ApplyDoesNotUpdateOrDeleteStaffLeaveRequests()
    {
        var db = CreateDbContext("LEAVE03_ImmutableLeave");
        await SeedBaselineAsync(db);
        var service = new DentistScheduleService(db, _mockAudit.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(5);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        var originalLeave = new StaffLeaveRequest
        {
            LeaveRequestId = 103,
            StaffUserId = 11,
            StaffRole = UserRole.Dentist,
            StartDate = futureDate,
            EndDate = futureDate,
            LeaveType = "Annual",
            Status = "Approved",
            Reason = "Nghỉ việc gia đình",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.StaffLeaveRequests.Add(originalLeave);
        await db.SaveChangesAsync();

        var applyRequest = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 11 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime, ShiftIdentifier = "Morning" }
            },
            OverwriteExisting = true,
            SkipConflicts = true
        };

        await service.ApplyBulkScheduleAsync(applyRequest, 1);

        // Verify leave in DB remains exactly intact
        var leaveInDb = await db.StaffLeaveRequests.FindAsync(103L);
        Assert.NotNull(leaveInDb);
        Assert.Equal("Approved", leaveInDb.Status);
        Assert.Equal("Nghỉ việc gia đình", leaveInDb.Reason);
        Assert.Equal(futureDate, leaveInDb.StartDate);
        Assert.Equal(futureDate, leaveInDb.EndDate);
    }

    [Fact]
    public async Task LEAVE04_SkipConflictsTrue_PreservesApprovedLeave()
    {
        var db = CreateDbContext("LEAVE04_SkipConflictsPreserves");
        await SeedBaselineAsync(db);
        var service = new DentistScheduleService(db, _mockAudit.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(6);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 104,
            StaffUserId = 11,
            StaffRole = UserRole.Dentist,
            StartDate = futureDate,
            EndDate = futureDate,
            LeaveType = "Annual",
            Status = "Approved",
            Reason = "Đi khám bệnh"
        });
        await db.SaveChangesAsync();

        var applyRequest = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 11 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime, ShiftIdentifier = "Morning" },
                new() { StartTime = ClinicShiftConstants.AfternoonStartTime, EndTime = ClinicShiftConstants.AfternoonEndTime, ShiftIdentifier = "Afternoon" }
            },
            SkipConflicts = true
        };

        var result = await service.ApplyBulkScheduleAsync(applyRequest, 1);
        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.CreatedCount);
        Assert.Equal(2, result.Data!.SkippedCount);

        var leaveInDb = await db.StaffLeaveRequests.FindAsync(104L);
        Assert.NotNull(leaveInDb);
        Assert.Equal("Approved", leaveInDb.Status);
    }

    [Fact]
    public async Task LEAVE06_PendingLeave_RecognizedAsPendingLeaveConflict_CanApplyFalse()
    {
        var db = CreateDbContext("LEAVE06_PendingLeave");
        await SeedBaselineAsync(db);
        var service = new DentistScheduleService(db, _mockAudit.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(7);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 106,
            StaffUserId = 12,
            StaffRole = UserRole.Dentist,
            StartDate = futureDate,
            EndDate = futureDate,
            LeaveType = "Annual",
            Status = "Pending",
            Reason = "Đơn xin nghỉ chờ duyệt"
        });
        await db.SaveChangesAsync();

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 12 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime, ShiftIdentifier = "Morning" }
            }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.PendingLeaveConflictCount);

        var item = preview.Data.Items.First();
        Assert.Equal("PendingLeaveConflict", item.Status);
        Assert.False(item.CanApply);
        Assert.Contains("ĐANG CHỜ DUYỆT", item.Message);
    }

    [Fact]
    public async Task LEAVE07_Concurrency_LeaveApprovedBetweenPreviewAndApply_ApplyRechecksAndSkips()
    {
        var db = CreateDbContext("LEAVE07_Concurrency");
        await SeedBaselineAsync(db);
        var service = new DentistScheduleService(db, _mockAudit.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(8);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 13 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime, ShiftIdentifier = "Morning" }
            }
        };

        // Step 1: Preview runs when no leave exists yet -> Valid
        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.ValidShiftsCount);

        // Step 2: Concurrency gap: Dentist submits / Admin approves leave in the background
        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 107,
            StaffUserId = 13,
            StaffRole = UserRole.Dentist,
            StartDate = futureDate,
            EndDate = futureDate,
            LeaveType = "Annual",
            Status = "Approved",
            Reason = "Nghỉ đột xuất vừa được duyệt"
        });
        await db.SaveChangesAsync();

        // Step 3: Apply runs with SkipConflicts=true
        var applyRequest = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 13 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = request.Shifts,
            SkipConflicts = true
        };

        var applyResult = await service.ApplyBulkScheduleAsync(applyRequest, 1);
        Assert.True(applyResult.Success);
        Assert.Equal(0, applyResult.Data!.CreatedCount);

        // Verify zero work schedules inserted for dentist 13
        var schedules = await db.StaffWorkSchedules
            .Where(s => s.StaffUserId == 13 && s.WorkDate == futureDate)
            .ToListAsync();
        Assert.Empty(schedules);
    }

    [Fact]
    public async Task LEAVE08_PartialBatch_ValidShiftsCreated_LeaveShiftsSafelySkipped()
    {
        var db = CreateDbContext("LEAVE08_PartialBatch");
        await SeedBaselineAsync(db);
        var service = new DentistScheduleService(db, _mockAudit.Object);

        var date1 = ClinicTimeHelper.TodayDateOnly.AddDays(9);
        var date2 = ClinicTimeHelper.TodayDateOnly.AddDays(10);
        int dow1 = date1.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date1.DayOfWeek;
        int dow2 = date2.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date2.DayOfWeek;

        // Dentist 11 is on Approved leave on date1, but free on date2
        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 108,
            StaffUserId = 11,
            StaffRole = UserRole.Dentist,
            StartDate = date1,
            EndDate = date1,
            LeaveType = "Annual",
            Status = "Approved",
            Reason = "Nghỉ phép ngày 1"
        });
        await db.SaveChangesAsync();

        var applyRequest = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 11 },
            FromDate = date1,
            ToDate = date2,
            DaysOfWeek = new List<int> { dow1, dow2 },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime, ShiftIdentifier = "Morning" }
            },
            SkipConflicts = true
        };

        var result = await service.ApplyBulkScheduleAsync(applyRequest, 1);
        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.CreatedCount); // Exactly 1 created (on date2)

        // Date1 must have 0 shifts
        var shiftsDate1 = await db.StaffWorkSchedules.CountAsync(s => s.StaffUserId == 11 && s.WorkDate == date1);
        Assert.Equal(0, shiftsDate1);

        // Date2 must have 1 shift
        var shiftsDate2 = await db.StaffWorkSchedules.CountAsync(s => s.StaffUserId == 11 && s.WorkDate == date2);
        Assert.Equal(1, shiftsDate2);
    }
}
