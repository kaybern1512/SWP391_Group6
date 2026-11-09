using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.DentistSchedule.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DentalClinic.Tests.DentistSchedule;

public class MonthlyDentistScheduleServiceTests
{
    private readonly Mock<IAuditLogService> _mockAuditLog;

    public MonthlyDentistScheduleServiceTests()
    {
        _mockAuditLog = new Mock<IAuditLogService>();
    }

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedDataAsync(DentalClinicDbContext db)
    {
        // 1. Dentist 1 (UserId = 10)
        db.UserAccounts.Add(new UserAccount
        {
            UserId = 10,
            Email = "dentist1@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        });
        db.StaffProfiles.Add(new StaffProfile
        {
            UserId = 10,
            EmployeeCode = "DENT-010",
            FullName = "Bác Sĩ Nguyễn Văn A",
            EmploymentStatus = "Active"
        });
        db.DentistProfiles.Add(new DentistProfile
        {
            UserId = 10,
            LicenseNumber = "LIC-010"
        });

        // 2. Dentist 2 (UserId = 20)
        db.UserAccounts.Add(new UserAccount
        {
            UserId = 20,
            Email = "dentist2@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        });
        db.StaffProfiles.Add(new StaffProfile
        {
            UserId = 20,
            EmployeeCode = "DENT-020",
            FullName = "Bác Sĩ Trần Thị B",
            EmploymentStatus = "Active"
        });
        db.DentistProfiles.Add(new DentistProfile
        {
            UserId = 20,
            LicenseNumber = "LIC-020"
        });

        // 3. Departments
        db.Departments.Add(new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Tổng Quát",
            Status = "Active"
        });
        db.Departments.Add(new Department
        {
            DepartmentId = 2,
            Name = "Khoa Chỉnh Nha & Niềng",
            Status = "Active"
        });

        // Department 1 Work Schedule (Open 08:00 - 18:00 every day)
        for (int dow = 0; dow <= 7; dow++)
        {
            db.DepartmentWorkSchedules.Add(new DepartmentWorkSchedule
            {
                DepartmentScheduleId = dow + 1,
                DepartmentId = 1,
                DayOfWeek = (byte)dow,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(18, 0),
                IsActive = true
            });
        }

        // 4. Dentist-Department Allocations
        // Dentist 1 assigned to Dept 1 and Dept 2
        db.DentistDepartments.Add(new DentistDepartment
        {
            DentistDepartmentId = 1,
            DentistUserId = 10,
            DepartmentId = 1,
            Status = "Active"
        });
        db.DentistDepartments.Add(new DentistDepartment
        {
            DentistDepartmentId = 2,
            DentistUserId = 10,
            DepartmentId = 2,
            Status = "Active"
        });

        // Dentist 2 assigned ONLY to Dept 2
        db.DentistDepartments.Add(new DentistDepartment
        {
            DentistDepartmentId = 3,
            DentistUserId = 20,
            DepartmentId = 2,
            Status = "Active"
        });

        await db.SaveChangesAsync();
    }

    // =========================================================================
    // 1. MONTHLY VIEW & KPI TESTS
    // =========================================================================

    [Fact]
    public async Task GetMonthlySchedule_CalculatesKPIsAndDaysAccurately()
    {
        var db = CreateDbContext("Monthly_KPIs");
        await SeedDataAsync(db);

        // Add 2 shifts for Dentist 1 in November 2026
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 2),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 2,
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 3),
            StartTime = new TimeOnly(13, 30),
            EndTime = new TimeOnly(17, 30),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var service = new DentistScheduleService(db, _mockAuditLog.Object);
        var filter = new MonthlyDentistScheduleFilterRequest
        {
            Year = 2026,
            Month = 11,
            DentistUserId = 10
        };

        var result = await service.GetMonthlyScheduleAsync(filter);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2026, result.Data!.Year);
        Assert.Equal(11, result.Data.Month);
        Assert.Equal(2, result.Data.TotalWorkingDays);
        Assert.Equal(8.0, result.Data.TotalWorkingHours); // 4 + 4 hours
        Assert.Equal(30, result.Data.Days.Count(d => d.IsCurrentMonth)); // Nov has 30 days
    }

    [Fact]
    public async Task GetMonthlySchedule_WithLeave_MarksDayAsOnLeave()
    {
        var db = CreateDbContext("Monthly_Leave");
        await SeedDataAsync(db);

        // Dentist 1 has leave on 2026-11-10
        db.DentistAvailabilities.Add(new DentistAvailability
        {
            AvailabilityId = 1,
            DentistUserId = 10,
            StartDateTime = new DateTime(2026, 11, 10, 8, 0, 0),
            EndDateTime = new DateTime(2026, 11, 10, 17, 0, 0),
            AvailabilityStatus = "OnLeave",
            Reason = "Nghỉ phép thường niên",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new DentistScheduleService(db, _mockAuditLog.Object);
        var filter = new MonthlyDentistScheduleFilterRequest
        {
            Year = 2026,
            Month = 11,
            DentistUserId = 10
        };

        var result = await service.GetMonthlyScheduleAsync(filter);

        Assert.True(result.Success);
        var leaveDay = result.Data!.Days.FirstOrDefault(d => d.Date == new DateOnly(2026, 11, 10));
        Assert.NotNull(leaveDay);
        Assert.True(leaveDay!.IsOnLeave);
        Assert.Equal("Nghỉ phép thường niên", leaveDay.LeaveReason);
        Assert.Equal(1, result.Data.TotalOffDays);
    }

    // =========================================================================
    // 2. BULK PREVIEW TESTS
    // =========================================================================

    [Fact]
    public async Task BulkPreviewSchedule_SingleDentist_WeekdaysOnly_ReturnsValidShifts()
    {
        var db = CreateDbContext("BulkPreview_Weekdays");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 10 },
            FromDate = new DateOnly(2026, 11, 2), // Mon
            ToDate = new DateOnly(2026, 11, 8),   // Sun
            DaysOfWeek = new List<int> { 1, 2, 3, 4, 5 }, // Mon - Fri (5 days)
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Note = "Sáng" }
            }
        };

        var result = await service.PreviewBulkScheduleAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data!.TotalProposedShifts);
        Assert.Equal(5, result.Data.ValidShiftsCount);
        Assert.Equal(0, result.Data.DuplicateShiftsCount);
    }

    [Fact]
    public async Task BulkPreviewSchedule_MultipleDentists_GeneratesMatrixShifts()
    {
        var db = CreateDbContext("BulkPreview_MultipleDentists");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        // Dept 2 has both Dentist 10 and 20 assigned
        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 2,
            DentistUserIds = new List<long> { 10, 20 },
            FromDate = new DateOnly(2026, 11, 2),
            ToDate = new DateOnly(2026, 11, 3), // 2 days
            DaysOfWeek = new List<int> { 1, 2 },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) },
                new() { StartTime = new TimeOnly(13, 30), EndTime = new TimeOnly(17, 30) }
            }
        };

        var result = await service.PreviewBulkScheduleAsync(request);

        Assert.True(result.Success);
        // 2 dentists * 2 days * 2 shifts = 8 proposed shifts
        Assert.Equal(8, result.Data!.TotalProposedShifts);
        Assert.Equal(8, result.Data.ValidShiftsCount);
    }

    [Fact]
    public async Task BulkPreviewSchedule_OverlappingShifts_DetectsOverlap()
    {
        var db = CreateDbContext("BulkPreview_Overlap");
        await SeedDataAsync(db);

        // Dentist 10 already has a shift on 2026-11-02 from 08:00 to 12:00
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 2),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        // Attempt to assign 10:00 - 14:00 on the same day
        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 10 },
            FromDate = new DateOnly(2026, 11, 2),
            ToDate = new DateOnly(2026, 11, 2),
            DaysOfWeek = new List<int> { 1 },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(14, 0) }
            }
        };

        var result = await service.PreviewBulkScheduleAsync(request);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.OverlapShiftsCount);
        Assert.Equal("Overlap", result.Data.Items[0].Status);
        Assert.False(result.Data.Items[0].CanApply);
    }

    [Fact]
    public async Task BulkPreviewSchedule_DentistOnLeave_DetectsLeaveConflict()
    {
        var db = CreateDbContext("BulkPreview_LeaveConflict");
        await SeedDataAsync(db);

        // Dentist 10 has OnLeave on 2026-11-03
        db.DentistAvailabilities.Add(new DentistAvailability
        {
            AvailabilityId = 1,
            DentistUserId = 10,
            StartDateTime = new DateTime(2026, 11, 3, 8, 0, 0),
            EndDateTime = new DateTime(2026, 11, 3, 17, 0, 0),
            AvailabilityStatus = "OnLeave",
            Reason = "Nghỉ ốm"
        });
        await db.SaveChangesAsync();

        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 10 },
            FromDate = new DateOnly(2026, 11, 3),
            ToDate = new DateOnly(2026, 11, 3),
            DaysOfWeek = new List<int> { 2 },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) }
            }
        };

        var result = await service.PreviewBulkScheduleAsync(request);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.LeaveConflictCount);
        Assert.Equal("LeaveConflict", result.Data.Items[0].Status);
    }

    [Fact]
    public async Task BulkPreviewSchedule_DentistNotAssignedToDept_DetectsDepartmentMismatch()
    {
        var db = CreateDbContext("BulkPreview_DeptMismatch");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        // Dentist 20 is NOT assigned to Dept 1
        var request = new BulkScheduleAssignmentRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 20 },
            FromDate = new DateOnly(2026, 11, 2),
            ToDate = new DateOnly(2026, 11, 2),
            DaysOfWeek = new List<int> { 1 },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) }
            }
        };

        var result = await service.PreviewBulkScheduleAsync(request);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.DepartmentMismatchCount);
        Assert.Equal("DepartmentMismatch", result.Data.Items[0].Status);
    }

    // =========================================================================
    // 3. BULK APPLY TESTS
    // =========================================================================

    [Fact]
    public async Task BulkApplySchedule_CreatesShiftsAndSkipsConflicts()
    {
        var db = CreateDbContext("BulkApply_SkipConflicts");
        await SeedDataAsync(db);

        // Existing shift on 2026-11-02
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 2),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 10 },
            FromDate = new DateOnly(2026, 11, 2),
            ToDate = new DateOnly(2026, 11, 3), // 2 days: day 1 duplicate, day 2 new
            DaysOfWeek = new List<int> { 1, 2 },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) }
            },
            SkipConflicts = true
        };

        var result = await service.ApplyBulkScheduleAsync(request, 1);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.CreatedCount);
        Assert.Equal(1, result.Data.SkippedCount);
    }

    [Fact]
    public async Task BulkApplySchedule_OverwriteExisting_ReplacesExistingShiftsWhenNoAppointments()
    {
        var db = CreateDbContext("BulkApply_Overwrite");
        await SeedDataAsync(db);

        // Old shift from 09:00 to 11:00
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 2),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(11, 0),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 10 },
            FromDate = new DateOnly(2026, 11, 2),
            ToDate = new DateOnly(2026, 11, 2),
            DaysOfWeek = new List<int> { 1 },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) }
            },
            OverwriteExisting = true
        };

        var result = await service.ApplyBulkScheduleAsync(request, 1);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.CreatedCount);

        // Verify the old shift was replaced
        var currentShifts = await db.DentistWorkSchedules.Where(s => s.DentistUserId == 10 && s.WorkDate == new DateOnly(2026, 11, 2) && s.Status == "Scheduled").ToListAsync();
        Assert.Single(currentShifts);
        Assert.Equal(new TimeOnly(8, 0), currentShifts[0].StartTime);
    }

    // =========================================================================
    // 4. COPY MONTH TESTS
    // =========================================================================

    [Fact]
    public async Task CopyMonthSchedulePreview_DayOfWeekPattern_MatchesCorrectDayOfWeek()
    {
        var db = CreateDbContext("CopyMonth_Preview");
        await SeedDataAsync(db);

        // Month 10: Mon 2026-10-05 (Monday) has a shift
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 10, 5), // Monday
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new CopyMonthScheduleRequest
        {
            SourceYear = 2026,
            SourceMonth = 10,
            TargetYear = 2026,
            TargetMonth = 11,
            DentistUserId = 10,
            DepartmentId = 1,
            Mode = "DayOfWeekPattern"
        };

        var result = await service.PreviewCopyMonthScheduleAsync(request);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.SourceShiftsCount);
        // In Nov 2026, there are 5 Mondays: 2, 9, 16, 23, 30 -> 5 proposed shifts
        Assert.Equal(5, result.Data.ProposedShiftsCount);
        Assert.Equal(5, result.Data.ValidCount);
    }

    [Fact]
    public async Task CopyMonthScheduleApply_CopiesShiftsToTargetMonthSuccessfully()
    {
        var db = CreateDbContext("CopyMonth_Apply");
        await SeedDataAsync(db);

        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 10, 15),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new CopyMonthScheduleRequest
        {
            SourceYear = 2026,
            SourceMonth = 10,
            TargetYear = 2026,
            TargetMonth = 11,
            DentistUserId = 10,
            DepartmentId = 1,
            Mode = "ExactDayMatch", // Copies 15th to 15th
            SkipConflicts = true
        };

        var result = await service.ApplyCopyMonthScheduleAsync(request, 1);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.CreatedCount);

        var targetShift = await db.DentistWorkSchedules.FirstOrDefaultAsync(s => s.DentistUserId == 10 && s.WorkDate == new DateOnly(2026, 11, 15));
        Assert.NotNull(targetShift);
        Assert.Equal(new TimeOnly(8, 0), targetShift!.StartTime);
    }

    // =========================================================================
    // 5. DAILY OVERRIDE & APPOINTMENT GUARD TESTS
    // =========================================================================

    [Fact]
    public async Task DailyOverrideSchedule_UpdatesShiftsForDay()
    {
        var db = CreateDbContext("DailyOverride_Update");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new DailyScheduleOverrideRequest
        {
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 5),
            IsDayOff = false,
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(11, 30), Note = "Ca 1" },
                new() { StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(16, 30), Note = "Ca 2" }
            }
        };

        var result = await service.ApplyDailyOverrideAsync(request, 1);

        Assert.True(result.Success);

        var dayShifts = await db.DentistWorkSchedules
            .Where(s => s.DentistUserId == 10 && s.WorkDate == new DateOnly(2026, 11, 5) && s.Status == "Scheduled")
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        Assert.Equal(2, dayShifts.Count);
        Assert.Equal(new TimeOnly(8, 0), dayShifts[0].StartTime);
        Assert.Equal(new TimeOnly(13, 0), dayShifts[1].StartTime);
    }

    [Fact]
    public async Task DailyOverrideSchedule_AppointmentGuard_RejectsRemovalWhenConfirmedAppointmentsExist()
    {
        var db = CreateDbContext("DailyOverride_ApptGuard");
        await SeedDataAsync(db);

        // 1. Existing shift
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 5),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });

        // 2. Patient Profile & Confirmed Appointment in that shift (09:00 - 10:00)
        db.PatientProfiles.Add(new PatientProfile
        {
            UserId = 100,
            PatientCode = "PAT-100",
            FullName = "Bệnh nhân Nguyễn Thị Khách",
            DateOfBirth = new DateOnly(1995, 5, 20),
            Gender = "Female"
        });
        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = 1,
            DepartmentId = 1,
            ServiceName = "Khám Răng Tổng Quát",
            DurationMinutes = 30,
            Price = 200000,
            Status = "Active"
        });
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 500,
            AppointmentCode = "APT-20261105-01",
            PatientUserId = 100,
            AssignedDentistUserId = 10,
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = new DateTime(2026, 11, 5, 9, 0, 0),
            ScheduledEnd = new DateTime(2026, 11, 5, 9, 30, 0),
            Status = "Confirmed",
            ReasonForVisit = "Đau răng",
            BookingSource = "Online"
        });
        await db.SaveChangesAsync();

        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        // Attempt to mark day off (which would orphan the confirmed appointment!)
        var request = new DailyScheduleOverrideRequest
        {
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 5),
            IsDayOff = true,
            LeaveReason = "Muốn nghỉ đột xuất"
        };

        var result = await service.ApplyDailyOverrideAsync(request, 1);

        // APPOINTMENT GUARD MUST BLOCK THIS!
        Assert.False(result.Success);
        Assert.NotNull(result.Message);
        Assert.NotEmpty(result.Message);
    }

    [Fact]
    public async Task DailyOverrideSchedule_MarksDayOffWithReason_WhenNoAppointments()
    {
        var db = CreateDbContext("DailyOverride_DayOffSafe");
        await SeedDataAsync(db);

        // Existing shift
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 6),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new DailyScheduleOverrideRequest
        {
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 6),
            IsDayOff = true,
            LeaveReason = "Hội thảo quốc tế"
        };

        var result = await service.ApplyDailyOverrideAsync(request, 1);

        Assert.True(result.Success);

        // Verify shift cancelled
        var cancelledShift = await db.DentistWorkSchedules.FirstOrDefaultAsync(s => s.DentistUserId == 10 && s.WorkDate == new DateOnly(2026, 11, 6));
        Assert.Equal("Cancelled", cancelledShift!.Status);

        // Verify availability record added
        var leave = await db.DentistAvailabilities.FirstOrDefaultAsync(a => a.DentistUserId == 10 && a.Reason == "Hội thảo quốc tế");
        Assert.NotNull(leave);
        Assert.Equal("OnLeave", leave!.AvailabilityStatus);
    }

    // =========================================================================
    // 6. BOOKING SYNCHRONIZATION & SINGLE SOURCE OF TRUTH TESTS
    // =========================================================================

    [Fact]
    public async Task BookingService_SynchronizesWithMonthlySchedule_ImmediatelyReflectsBulkAssignedSlots()
    {
        var db = CreateDbContext("BookingSync_Monthly");
        await SeedDataAsync(db);

        // Add 30-min Service
        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = 1,
            DepartmentId = 1,
            ServiceName = "Khám Răng Tổng Quát",
            DurationMinutes = 30,
            Price = 200000,
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var scheduleService = new DentistScheduleService(db, _mockAuditLog.Object);
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<BookingService>>();
        var bookingService = new BookingService(db, mockLogger.Object);

        var targetDate = DateTime.Today.AddDays(5);
        var targetDateOnly = DateOnly.FromDateTime(targetDate);
        int targetDow = targetDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)targetDate.DayOfWeek;

        // 1. Initially NO shifts on targetDate -> Booking returns 0 slots
        var initialSlotsQuery = new DentalClinic.Application.Features.Booking.DTOs.AvailableSlotsQuery
        {
            DepartmentId = 1,
            ServiceId = 1,
            Date = targetDate
        };
        var initialResult = await bookingService.GetAvailableSlotsAsync(initialSlotsQuery);
        Assert.True(initialResult.Success);
        Assert.Empty(initialResult.Data!);

        // 2. Admin applies bulk schedule for targetDate (08:00 - 10:00 -> 4 slots of 30 mins: 08:00, 08:30, 09:00, 09:30)
        var bulkRequest = new BulkScheduleApplyRequest
        {
            DepartmentId = 1,
            DentistUserIds = new List<long> { 10 },
            FromDate = targetDateOnly,
            ToDate = targetDateOnly,
            DaysOfWeek = new List<int> { targetDow },
            Shifts = new List<BulkScheduleShiftDto>
            {
                new() { StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(10, 0), Note = "Ca Sáng" }
            },
            SkipConflicts = true
        };
        var applyResult = await scheduleService.ApplyBulkScheduleAsync(bulkRequest, 1);
        Assert.True(applyResult.Success);
        Assert.Equal(1, applyResult.Data!.CreatedCount);

        // 3. Immediately query BookingService -> It MUST reflect the 4 slots! Single Source of Truth!
        var afterScheduleResult = await bookingService.GetAvailableSlotsAsync(initialSlotsQuery);
        Assert.True(afterScheduleResult.Success);
        Assert.Equal(4, afterScheduleResult.Data!.Count);
        Assert.Equal(new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, 8, 0, 0), afterScheduleResult.Data![0].StartTime);
        Assert.Equal(new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, 9, 30, 0), afterScheduleResult.Data![3].StartTime);

        // 4. Admin modifies the day via DailyOverride to mark as Day Off
        var overrideRequest = new DailyScheduleOverrideRequest
        {
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = targetDateOnly,
            IsDayOff = true,
            LeaveReason = "Bận việc gia đình"
        };
        var overrideResult = await scheduleService.ApplyDailyOverrideAsync(overrideRequest, 1);
        Assert.True(overrideResult.Success);

        // 5. BookingService immediately returns 0 slots (respects cancellation and leave!)
        var afterOverrideResult = await bookingService.GetAvailableSlotsAsync(initialSlotsQuery);
        Assert.True(afterOverrideResult.Success);
        Assert.Empty(afterOverrideResult.Data!);
    }

    [Fact]
    public void DentistScheduleController_RBAC_HasAuthorizeSystemAdministratorAttribute()
    {
        var controllerType = typeof(DentalClinic.Api.Controllers.DentistScheduleController);
        var authAttrs = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
        Assert.NotEmpty(authAttrs);

        var bulkApplyMethod = controllerType.GetMethod(nameof(DentalClinic.Api.Controllers.DentistScheduleController.ApplyBulkSchedule));
        Assert.NotNull(bulkApplyMethod);
        var bulkAuth = bulkApplyMethod!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();
        Assert.NotNull(bulkAuth);
        Assert.Equal(UserRole.SystemAdministrator, bulkAuth!.Roles);
    }
}

