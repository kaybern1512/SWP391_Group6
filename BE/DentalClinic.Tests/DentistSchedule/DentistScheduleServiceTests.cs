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

public class DentistScheduleServiceTests
{
    private readonly Mock<IAuditLogService> _mockAuditLog;

    public DentistScheduleServiceTests()
    {
        _mockAuditLog = new Mock<IAuditLogService>();
    }

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedDataAsync(DentalClinicDbContext db)
    {
        var dentistUser = new UserAccount
        {
            UserId = 20,
            Email = "dentist@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var dentistStaff = new StaffProfile
        {
            UserId = 20,
            FullName = "Bác Sĩ Nguyễn Văn A",
            EmploymentStatus = "Active"
        };
        var dentistProfile = new DentistProfile
        {
            UserId = 20,
            DepartmentId = 1,
            LicenseNumber = "LIC-001"
        };

        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Hàm Mặt",
            Status = "Active"
        };

        db.UserAccounts.Add(dentistUser);
        db.StaffProfiles.Add(dentistStaff);
        db.DentistProfiles.Add(dentistProfile);
        db.Departments.Add(dept);

        await db.SaveChangesAsync();
    }

    // =========================================================================
    // EXISTING BASIC TESTS
    // =========================================================================

    [Fact]
    public async Task CreateSchedule_ValidRequest_ReturnsSuccess()
    {
        var db = CreateDbContext("Schedule_Valid");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new CreateDentistScheduleRequest
        {
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 20),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Note = "Ca sáng"
        };

        var result = await service.CreateScheduleAsync(request, 1);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(20, result.Data!.DentistUserId);
        Assert.Equal(1, result.Data.DepartmentId);
        Assert.Equal("Scheduled", result.Data.Status);
    }

    [Fact]
    public async Task CreateSchedule_StartTimeAfterEndTime_ReturnsFailure()
    {
        var db = CreateDbContext("Schedule_InvalidTime");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new CreateDentistScheduleRequest
        {
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 20),
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(10, 0)
        };

        var result = await service.CreateScheduleAsync(request, 1);

        Assert.False(result.Success);
        Assert.Contains("Giờ kết thúc", result.Message);
    }

    [Fact]
    public async Task CreateSchedule_DentistNotAssignedToDepartment_ReturnsFailure()
    {
        var db = CreateDbContext("Schedule_NotAssigned");
        await SeedDataAsync(db);

        db.Departments.Add(new Department { DepartmentId = 2, Name = "Khoa Chỉnh Nha", Status = "Active" });
        await db.SaveChangesAsync();

        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new CreateDentistScheduleRequest
        {
            DentistUserId = 20,
            DepartmentId = 2,
            WorkDate = new DateOnly(2026, 11, 20),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0)
        };

        var result = await service.CreateScheduleAsync(request, 1);

        Assert.False(result.Success);
        Assert.Contains("chưa được phân bổ", result.Message);
    }

    [Fact]
    public async Task CreateSchedule_OverlappingTime_ReturnsFailure()
    {
        var db = CreateDbContext("Schedule_Overlap");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        await service.CreateScheduleAsync(new CreateDentistScheduleRequest
        {
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 20),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0)
        }, 1);

        var overlapRequest = new CreateDentistScheduleRequest
        {
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 20),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(14, 0)
        };

        var result = await service.CreateScheduleAsync(overlapRequest, 1);

        Assert.False(result.Success);
        Assert.Contains("giao thoa thời gian", result.Message);
    }

    [Fact]
    public async Task CancelSchedule_ExistingSchedule_ReturnsSuccess()
    {
        var db = CreateDbContext("Schedule_Cancel");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var createResult = await service.CreateScheduleAsync(new CreateDentistScheduleRequest
        {
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 20),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0)
        }, 1);

        var cancelResult = await service.CancelScheduleAsync(createResult.Data!.DentistScheduleId, 1);

        Assert.True(cancelResult.Success);

        var updated = await service.GetScheduleByIdAsync(createResult.Data.DentistScheduleId);
        Assert.Equal("Cancelled", updated.Data!.Status);
    }

    [Fact]
    public async Task CreateAvailability_ValidRequest_ReturnsSuccess()
    {
        var db = CreateDbContext("Availability_Valid");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new CreateDentistAvailabilityRequest
        {
            DentistUserId = 20,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2),
            AvailabilityStatus = DentistAvailabilityStatus.OnLeave,
            Reason = "Nghỉ phép thường niên"
        };

        var result = await service.CreateAvailabilityAsync(request, 20, UserRole.Dentist);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(DentistAvailabilityStatus.OnLeave, result.Data!.AvailabilityStatus);
        Assert.Equal("Nghỉ phép thường niên", result.Data.Reason);
    }

    [Fact]
    public async Task DeleteAvailability_ValidRequest_ReturnsSuccess()
    {
        var db = CreateDbContext("Availability_Delete");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var createResult = await service.CreateAvailabilityAsync(new CreateDentistAvailabilityRequest
        {
            DentistUserId = 20,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2),
            AvailabilityStatus = DentistAvailabilityStatus.Busy,
            Reason = "Tham gia hội thảo"
        }, 20, UserRole.Dentist);

        var deleteResult = await service.DeleteAvailabilityAsync(createResult.Data!.AvailabilityId, 20, UserRole.Dentist);

        Assert.True(deleteResult.Success);
        var all = await service.GetMyAvailabilitiesAsync(20, null, null);
        Assert.Empty(all.Data!);
    }

    // =========================================================================
    // MANDATORY TEST SUITE: SCH-01 TO SCH-20 & BASELINE CALCULATION
    // =========================================================================

    [Fact]
    public async Task SCH01_BulkSchedule_MorningShift_AssignsSuccessfully()
    {
        var db = CreateDbContext("SCH01_MorningShift");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        var request = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime, Note = "Ca Sáng" }
            }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.ValidShiftsCount);

        var apply = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(apply.Success);
        Assert.Equal(1, apply.Data!.CreatedCount);

        var savedShift = await db.StaffWorkSchedules.FirstOrDefaultAsync(s => s.StaffUserId == 20 && s.WorkDate == futureDate);
        Assert.NotNull(savedShift);
        Assert.Equal(ClinicShiftConstants.MorningStartTime, savedShift!.StartTime);
        Assert.Equal(ClinicShiftConstants.MorningEndTime, savedShift.EndTime);
        Assert.Equal("Scheduled", savedShift.Status);
    }

    [Fact]
    public async Task SCH02_BulkSchedule_AfternoonShift_AssignsSuccessfully()
    {
        var db = CreateDbContext("SCH02_AfternoonShift");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        var request = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.AfternoonStartTime, EndTime = ClinicShiftConstants.AfternoonEndTime, Note = "Ca Chiều" }
            }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.ValidShiftsCount);

        var apply = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(apply.Success);
        Assert.Equal(1, apply.Data!.CreatedCount);

        var savedShift = await db.StaffWorkSchedules.FirstOrDefaultAsync(s => s.StaffUserId == 20 && s.WorkDate == futureDate);
        Assert.NotNull(savedShift);
        Assert.Equal(ClinicShiftConstants.AfternoonStartTime, savedShift!.StartTime);
        Assert.Equal(ClinicShiftConstants.AfternoonEndTime, savedShift.EndTime);
    }

    [Fact]
    public async Task SCH03_BulkSchedule_BothShifts_CreatesTwoShiftsPerDay()
    {
        var db = CreateDbContext("SCH03_BothShifts");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var startDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        var endDate = startDate.AddDays(1); // 2 consecutive days

        var request = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = startDate,
            ToDate = endDate,
            DaysOfWeek = new List<int> { 1, 2, 3, 4, 5, 6, 7 },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime },
                new() { StartTime = ClinicShiftConstants.AfternoonStartTime, EndTime = ClinicShiftConstants.AfternoonEndTime }
            }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(4, preview.Data!.TotalProposedShifts);
        Assert.Equal(4, preview.Data.ValidShiftsCount);

        var apply = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(apply.Success);
        Assert.Equal(4, apply.Data!.CreatedCount);

        var countDay1 = await db.StaffWorkSchedules.CountAsync(s => s.StaffUserId == 20 && s.WorkDate == startDate && s.Status == "Scheduled");
        var countDay2 = await db.StaffWorkSchedules.CountAsync(s => s.StaffUserId == 20 && s.WorkDate == endDate && s.Status == "Scheduled");
        Assert.Equal(2, countDay1);
        Assert.Equal(2, countDay2);
    }

    [Fact]
    public async Task SCH04_BulkSchedule_PastDate_ReturnsValidationFailure()
    {
        var db = CreateDbContext("SCH04_PastDate");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var pastDate = ClinicTimeHelper.TodayDateOnly.AddDays(-2);

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = pastDate,
            ToDate = pastDate,
            DaysOfWeek = new List<int> { 1, 2, 3, 4, 5, 6, 7 },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime }
            }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.False(preview.Success);
        Assert.Contains("quá khứ", preview.Message);
    }

    [Fact]
    public async Task SCH05_BulkSchedule_TodayStartedOrPastShift_MarksPastOrStarted()
    {
        var db = CreateDbContext("SCH05_TodayStarted");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var today = ClinicTimeHelper.TodayDateOnly;
        int dow = today.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)today.DayOfWeek;

        // Shift started before now
        var pastStartTime = TimeOnly.FromDateTime(ClinicTimeHelper.Now).AddHours(-1);
        var pastEndTime = pastStartTime.AddHours(2);

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = today,
            ToDate = today,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = pastStartTime, EndTime = pastEndTime }
            }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.PastOrStartedCount);
        var item = preview.Data.Items.First();
        Assert.Equal("PastOrStarted", item.Status);
        Assert.False(item.CanApply);
    }

    [Fact]
    public async Task SCH06_BulkSchedule_FromDateAfterToDate_ReturnsValidationFailure()
    {
        var db = CreateDbContext("SCH06_InvalidRange");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(5);

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate.AddDays(2),
            ToDate = futureDate, // ToDate < FromDate
            DaysOfWeek = new List<int> { 1, 2, 3 },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime }
            }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.False(preview.Success);
        Assert.Contains("nhỏ hơn", preview.Message);
    }

    [Fact]
    public async Task SCH07_BulkSchedule_EmptyDentistIds_ReturnsValidationFailure()
    {
        var db = CreateDbContext("SCH07_EmptyDentists");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long>(),
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { 1, 2, 3 },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime }
            }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.False(preview.Success);
        Assert.Contains("ít nhất một bác sĩ", preview.Message);
    }

    [Fact]
    public async Task SCH08_BulkSchedule_EmptyShifts_ReturnsValidationFailure()
    {
        var db = CreateDbContext("SCH08_EmptyShifts");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { 1, 2, 3 },
            Shifts = new List<BulkScheduleShiftDto>()
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.False(preview.Success);
        Assert.Contains("ít nhất một ca trực", preview.Message);
    }

    [Fact]
    public async Task SCH09_BulkSchedule_EmptyDaysOfWeek_ReturnsValidationFailure()
    {
        var db = CreateDbContext("SCH09_EmptyDaysOfWeek");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int>(),
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime }
            }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.False(preview.Success);
        Assert.Contains("ít nhất một ngày trong tuần", preview.Message);
    }

    [Fact]
    public async Task SCH10_BulkSchedule_ExactDuplicate_MarksDuplicateStatus()
    {
        var db = CreateDbContext("SCH10_Duplicate");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        // Existing shift in DB
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            StaffUserId = 20,
            StaffRole = UserRole.Dentist,
            WorkDate = futureDate,
            StartTime = ClinicShiftConstants.MorningStartTime,
            EndTime = ClinicShiftConstants.MorningEndTime,
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime }
            }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.DuplicateShiftsCount);
        var item = preview.Data.Items.First();
        Assert.Equal("Duplicate", item.Status);
        Assert.False(item.CanApply);
    }

    [Fact]
    public async Task SCH11_BulkSchedule_OverlappingShift_MarksOverlapStatus()
    {
        var db = CreateDbContext("SCH11_Overlap");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        // Existing shift 08:00 - 12:00
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            StaffUserId = 20,
            StaffRole = UserRole.Dentist,
            WorkDate = futureDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        // Overlapping request: 10:00 - 14:00
        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(14, 0) }
            },
            OverwriteExisting = false
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.OverlapShiftsCount);
        var item = preview.Data.Items.First();
        Assert.Equal("Overlap", item.Status);
        Assert.False(item.CanApply);
    }

    [Fact]
    public async Task SCH12_BulkSchedule_DentistOnLeave_MarksLeaveConflict()
    {
        var db = CreateDbContext("SCH12_LeaveConflict");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        // Approved Leave for Dentist 20 on that date
        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            StaffUserId = 20,
            StaffRole = UserRole.Dentist,
            StartDate = futureDate,
            EndDate = futureDate,
            Status = "Approved",
            LeaveType = "Annual",
            Reason = "Nghỉ phép cá nhân"
        });
        await db.SaveChangesAsync();

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime }
            }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.LeaveConflictCount);
        var item = preview.Data.Items.First();
        Assert.True(item.Status == "OnApprovedLeave" || item.Status == "LeaveConflict");
        Assert.False(item.CanApply);
    }

    [Fact]
    public async Task SCH13_BulkSchedule_DentistDepartmentMismatch_MarksDepartmentMismatch()
    {
        var db = CreateDbContext("SCH13_DeptMismatch");
        await SeedDataAsync(db);

        // Add department 2 ("Khoa Chỉnh Nha") where dentist 20 is not allocated
        db.Departments.Add(new Department { DepartmentId = 2, Name = "Khoa Chỉnh Nha", Status = "Active" });
        await db.SaveChangesAsync();

        var service = new DentistScheduleService(db, _mockAuditLog.Object);
        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 2, // Mismatch department
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime }
            }
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.DepartmentMismatchCount);
        var item = preview.Data.Items.First();
        Assert.Equal("DepartmentMismatch", item.Status);
        Assert.False(item.CanApply);
    }

    [Fact]
    public async Task SCH14_BulkSchedule_ConflictingAppointment_RejectsOverwriteWithAppointmentConflict()
    {
        var db = CreateDbContext("SCH14_ApptConflict");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        // Existing shift 08:00 - 12:00
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            StaffUserId = 20,
            StaffRole = UserRole.Dentist,
            WorkDate = futureDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });

        // Confirmed patient appointment from 08:30 to 09:30
        db.AppointmentServices.Add(new DentalClinic.Infrastructure.Persistence.Entities.AppointmentService
        {
            AppointmentServiceId = 1,
            AppointmentId = 100,
            AssignedDentistUserId = 20,
            ServiceDate = futureDate,
            ScheduledStart = futureDate.ToDateTime(new TimeOnly(8, 30)),
            ScheduledEnd = futureDate.ToDateTime(new TimeOnly(9, 30)),
            Status = AppointmentStatus.Confirmed
        });
        await db.SaveChangesAsync();

        // Try to overwrite with afternoon shift 13:30 - 17:30 (appointment 08:30 will be orphaned)
        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(14, 0) } // Overlaps old shift but leaves 08:30 appointment outside
            },
            OverwriteExisting = true
        };

        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.AppointmentConflictCount);
        var item = preview.Data.Items.First();
        Assert.Equal("AppointmentConflict", item.Status);
        Assert.False(item.CanApply);
    }

    [Fact]
    public async Task SCH15_BulkSchedule_PartialBatch_SkipConflictsTrue_AppliesValidShifts()
    {
        var db = CreateDbContext("SCH15_SkipConflicts");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        // Shift 1 already exists (Duplicate)
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            StaffUserId = 20,
            StaffRole = UserRole.Dentist,
            WorkDate = futureDate,
            StartTime = ClinicShiftConstants.MorningStartTime,
            EndTime = ClinicShiftConstants.MorningEndTime,
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        // Request assigns Morning (conflict) and Afternoon (valid)
        var request = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime },
                new() { StartTime = ClinicShiftConstants.AfternoonStartTime, EndTime = ClinicShiftConstants.AfternoonEndTime }
            },
            SkipConflicts = true
        };

        var apply = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(apply.Success);
        Assert.Equal(1, apply.Data!.CreatedCount);
        Assert.Equal(1, apply.Data.SkippedCount);

        var totalScheduled = await db.StaffWorkSchedules.CountAsync(s => s.StaffUserId == 20 && s.WorkDate == futureDate && s.Status == "Scheduled");
        Assert.Equal(2, totalScheduled); // 1 pre-existing + 1 new
    }

    [Fact]
    public async Task SCH16_BulkSchedule_Idempotency_SecondApplyCreatesNoDuplicates()
    {
        var db = CreateDbContext("SCH16_Idempotency");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        var request = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime }
            },
            SkipConflicts = true
        };

        // First apply: creates 1 shift
        var firstApply = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(firstApply.Success);
        Assert.Equal(1, firstApply.Data!.CreatedCount);

        // Second apply with identical request: detects duplicates and creates 0 new shifts
        var secondApply = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(secondApply.Success);
        Assert.Equal(0, secondApply.Data!.CreatedCount);
        Assert.Equal(1, secondApply.Data.SkippedCount);

        var total = await db.StaffWorkSchedules.CountAsync(s => s.StaffUserId == 20 && s.WorkDate == futureDate && s.Status == "Scheduled");
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task SCH17_BulkSchedule_StalePreview_SafelySkipsDuplicateOnApply()
    {
        var db = CreateDbContext("SCH17_StalePreview");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        var request = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime }
            },
            SkipConflicts = true
        };

        // Preview sees shift as Valid
        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.ValidShiftsCount);

        // Another actor inserts the same shift right before apply
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            StaffUserId = 20,
            StaffRole = UserRole.Dentist,
            WorkDate = futureDate,
            StartTime = ClinicShiftConstants.MorningStartTime,
            EndTime = ClinicShiftConstants.MorningEndTime,
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        // Apply executes: re-validates DB state and skips safely without error
        var apply = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(apply.Success);
        Assert.Equal(0, apply.Data!.CreatedCount);
        Assert.Equal(1, apply.Data.SkippedCount);
    }

    [Fact]
    public async Task SCH18_BulkSchedule_TransactionRollback_PreservesStateOnError()
    {
        var db = CreateDbContext("SCH18_Rollback");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        // Existing shift
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            StaffUserId = 20,
            StaffRole = UserRole.Dentist,
            WorkDate = futureDate,
            StartTime = ClinicShiftConstants.MorningStartTime,
            EndTime = ClinicShiftConstants.MorningEndTime,
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var initialCount = await db.StaffWorkSchedules.CountAsync();

        // Attempt apply with conflicts when SkipConflicts = false -> rejected cleanly
        var request = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime }
            },
            SkipConflicts = false
        };

        var result = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.False(result.Success);
        Assert.Contains("xung đột hoặc trùng lặp", result.Message);

        var finalCount = await db.StaffWorkSchedules.CountAsync();
        Assert.Equal(initialCount, finalCount);
    }

    [Fact]
    public async Task SCH19_BulkSchedule_CanonicalShifts_MatchesStandardClinicHours()
    {
        var db = CreateDbContext("SCH19_CanonicalShifts");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int dow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        var request = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { dow },
            ShiftIdentifiers = new List<string> { ClinicShiftConstants.Morning, ClinicShiftConstants.Afternoon }
        };

        var apply = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(apply.Success);
        Assert.Equal(2, apply.Data!.CreatedCount);

        var shifts = await db.StaffWorkSchedules
            .Where(s => s.StaffUserId == 20 && s.WorkDate == futureDate)
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        Assert.Equal(2, shifts.Count);
        Assert.Equal(new TimeOnly(8, 0), shifts[0].StartTime);
        Assert.Equal(new TimeOnly(12, 0), shifts[0].EndTime);
        Assert.Equal(new TimeOnly(13, 30), shifts[1].StartTime);
        Assert.Equal(new TimeOnly(17, 30), shifts[1].EndTime);
    }

    [Fact]
    public async Task SCH20_BulkSchedule_PreservesHistoricalData_DoesNotMutatePastShifts()
    {
        var db = CreateDbContext("SCH20_PreservesHistorical");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var pastDate = ClinicTimeHelper.TodayDateOnly.AddDays(-10);
        var futureDate = ClinicTimeHelper.TodayDateOnly.AddDays(2);
        int futureDow = futureDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)futureDate.DayOfWeek;

        // Seed historical past shift
        var oldShift = new StaffWorkSchedule
        {
            StaffUserId = 20,
            StaffRole = UserRole.Dentist,
            WorkDate = pastDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Completed",
            Notes = "Ca trực lịch sử"
        };
        db.StaffWorkSchedules.Add(oldShift);
        await db.SaveChangesAsync();

        var request = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = futureDate,
            ToDate = futureDate,
            DaysOfWeek = new List<int> { futureDow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime }
            }
        };

        var apply = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(apply.Success);

        var historicalCheck = await db.StaffWorkSchedules.AsNoTracking().FirstOrDefaultAsync(s => s.WorkDate == pastDate);
        Assert.NotNull(historicalCheck);
        Assert.Equal("Completed", historicalCheck!.Status);
        Assert.Equal("Ca trực lịch sử", historicalCheck.Notes);
    }

    [Fact]
    public async Task SCH_BaselineCalculation_20ShiftsCreated()
    {
        // Specification Baseline:
        // 1 doctor (UserId = 20), From 17/11/2026 to 30/11/2026 (14 calendar days, including 10 weekdays Mon-Fri).
        // Both shifts (Morning + Afternoon) = 2 shifts per working day.
        // Total expected shifts = 10 days * 2 shifts = exactly 20 shifts created.
        var db = CreateDbContext("SCH_BaselineCalculation");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var today = ClinicTimeHelper.TodayDateOnly;
        var daysUntilNextMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilNextMonday == 0) daysUntilNextMonday = 7;
        var fromDate = today.AddDays(daysUntilNextMonday);
        var toDate = fromDate.AddDays(13); // 2 full weeks = 14 calendar days, exactly 10 weekdays Mon-Fri
        var weekdaysMonToFri = new List<int> { 1, 2, 3, 4, 5 }; // Monday to Friday

        var request = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = fromDate,
            ToDate = toDate,
            DaysOfWeek = weekdaysMonToFri,
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = ClinicShiftConstants.MorningStartTime, EndTime = ClinicShiftConstants.MorningEndTime, Note = "Ca Sáng" },
                new() { StartTime = ClinicShiftConstants.AfternoonStartTime, EndTime = ClinicShiftConstants.AfternoonEndTime, Note = "Ca Chiều" }
            },
            OverwriteExisting = false,
            SkipConflicts = false
        };

        // 1. Preview verification
        var preview = await service.PreviewBulkScheduleAsync(request);
        Assert.True(preview.Success);
        Assert.NotNull(preview.Data);
        Assert.Equal(20, preview.Data!.TotalProposedShifts);
        Assert.Equal(20, preview.Data.ValidShiftsCount);
        Assert.Equal(0, preview.Data.DuplicateShiftsCount);
        Assert.Equal(0, preview.Data.OverlapShiftsCount);
        Assert.Equal(0, preview.Data.LeaveConflictCount);
        Assert.Equal(0, preview.Data.PastOrStartedCount);

        // 2. Apply verification
        var apply = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(apply.Success);
        Assert.NotNull(apply.Data);
        Assert.Equal(20, apply.Data!.CreatedCount);
        Assert.Equal(0, apply.Data.SkippedCount);

        // 3. Database persistence verification
        var savedShifts = await db.StaffWorkSchedules
            .Where(s => s.StaffUserId == 20 && s.WorkDate >= fromDate && s.WorkDate <= toDate && s.Status == "Scheduled")
            .ToListAsync();
        Assert.Equal(20, savedShifts.Count);

        // Verify all 10 weekdays have both shifts
        for (var d = fromDate; d <= toDate; d = d.AddDays(1))
        {
            int dow = d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;
            if (dow >= 1 && dow <= 5)
            {
                var dayShifts = savedShifts.Where(s => s.WorkDate == d).ToList();
                Assert.Equal(2, dayShifts.Count);
                Assert.Contains(dayShifts, s => s.StartTime == ClinicShiftConstants.MorningStartTime && s.EndTime == ClinicShiftConstants.MorningEndTime);
                Assert.Contains(dayShifts, s => s.StartTime == ClinicShiftConstants.AfternoonStartTime && s.EndTime == ClinicShiftConstants.AfternoonEndTime);
            }
        }
    }
}
