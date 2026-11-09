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

public class ReceptionistWorkforceSchedulingTests
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
                UserId = 100 + i,
                Email = $"receptionist{i}@dentalcare.com",
                Role = UserRole.Receptionist,
                Status = AccountStatus.Active
            });
            db.StaffProfiles.Add(new StaffProfile
            {
                UserId = 100 + i,
                EmployeeCode = $"REC-00{i}",
                FullName = $"Lễ Tân Số {i}",
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
            },
            new ReceptionistShiftTemplate
            {
                ShiftTemplateId = 3,
                TemplateName = "Ca Cả Ngày",
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(19, 0),
                MinStaffRequired = 2,
                IsActive = true,
                DisplayOrder = 3
            }
        );

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateScheduleAsync_ValidSchedule_CreatesSuccessfully()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAndTemplatesAsync(db);

        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var service = new ReceptionistScheduleService(db, coverage, _mockAudit.Object);

        var request = new CreateReceptionistScheduleRequest
        {
            ReceptionistUserId = 101,
            ShiftTemplateId = 1,
            WorkDate = new DateOnly(2026, 12, 1),
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Notes = "Ca sáng chuẩn"
        };

        var result = await service.CreateScheduleAsync(request, 1);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(101, result.Data.ReceptionistUserId);
        Assert.Equal(new DateOnly(2026, 12, 1), result.Data.WorkDate);
        Assert.Equal("Scheduled", result.Data.Status);
    }

    [Fact]
    public async Task CreateScheduleAsync_EndTimeBeforeStartTime_ReturnsError()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAndTemplatesAsync(db);

        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var service = new ReceptionistScheduleService(db, coverage, _mockAudit.Object);

        var request = new CreateReceptionistScheduleRequest
        {
            ReceptionistUserId = 101,
            WorkDate = new DateOnly(2026, 12, 1),
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(10, 0)
        };

        var result = await service.CreateScheduleAsync(request, 1);

        Assert.False(result.Success);
        Assert.Contains("lớn hơn", result.Message ?? "");
    }

    [Fact]
    public async Task PreviewAndApplyBulkScheduleAsync_AppliesToDatesCorrectly()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAndTemplatesAsync(db);

        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var service = new ReceptionistScheduleService(db, coverage, _mockAudit.Object);

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            Year = 2026,
            Month = 12,
            ReceptionistUserIds = new List<long> { 101, 102 },
            DaysOfWeek = new List<int> { 1, 2, 3 }, // Mon, Tue, Wed
            ShiftTemplateId = 1,
            OverwriteExisting = false
        };

        var previewRes = await service.PreviewBulkScheduleAsync(request);
        Assert.True(previewRes.Success);
        Assert.NotNull(previewRes.Data);
        Assert.True(previewRes.Data.ProposedShiftsCount > 0);

        var applyRes = await service.ApplyBulkScheduleAsync(request, 1);
        Assert.True(applyRes.Success);

        var totalInDb = await db.ReceptionistWorkSchedules.CountAsync();
        Assert.True(totalInDb > 0);
    }

    [Fact]
    public async Task ApplyBulkScheduleAsync_OverwriteTrue_ReplacesExistingShifts()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAndTemplatesAsync(db);

        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var service = new ReceptionistScheduleService(db, coverage, _mockAudit.Object);

        // Pre-create 1 shift on Dec 1, 2026 (Tuesday = DayOfWeek 2)
        db.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
        {
            ReceptionistUserId = 101,
            WorkDate = new DateOnly(2026, 12, 1),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var request = new ReceptionistBulkScheduleAssignmentRequest
        {
            Year = 2026,
            Month = 12,
            ReceptionistUserIds = new List<long> { 101 },
            DaysOfWeek = new List<int> { 2 },
            ShiftTemplateId = 1,
            OverwriteExisting = true
        };

        var result = await service.ApplyBulkScheduleAsync(request, 1);

        Assert.True(result.Success);
        var shift = await db.ReceptionistWorkSchedules.FirstAsync(s => s.ReceptionistUserId == 101 && s.WorkDate == new DateOnly(2026, 12, 1) && s.Status == "Scheduled");
        Assert.Equal(new TimeOnly(7, 0), shift.StartTime);
    }

    [Fact]
    public async Task CopyMonthScheduleAsync_CopiesShiftsBetweenMonths()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAndTemplatesAsync(db);

        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var service = new ReceptionistScheduleService(db, coverage, _mockAudit.Object);

        // Seed 2 shifts in Dec 2026
        db.ReceptionistWorkSchedules.AddRange(
            new ReceptionistWorkSchedule
            {
                ReceptionistUserId = 101,
                WorkDate = new DateOnly(2026, 12, 1),
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(13, 0),
                Status = "Scheduled",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            },
            new ReceptionistWorkSchedule
            {
                ReceptionistUserId = 102,
                WorkDate = new DateOnly(2026, 12, 1),
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(13, 0),
                Status = "Scheduled",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        var copyReq = new ReceptionistCopyMonthScheduleRequest
        {
            SourceYear = 2026,
            SourceMonth = 12,
            TargetYear = 2027,
            TargetMonth = 1,
            OverwriteExisting = false
        };

        var copyRes = await service.CopyMonthScheduleAsync(copyReq, 1);

        Assert.True(copyRes.Success);
        var janShifts = await db.ReceptionistWorkSchedules
            .Where(s => s.WorkDate.Year == 2027 && s.WorkDate.Month == 1)
            .ToListAsync();
        Assert.Equal(2, janShifts.Count);
    }

    [Fact]
    public async Task DailyOverrideScheduleAsync_ReplacesAllShiftsForDate()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAndTemplatesAsync(db);

        var coverage = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);
        var service = new ReceptionistScheduleService(db, coverage, _mockAudit.Object);

        var targetDate = new DateOnly(2026, 12, 10);
        db.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
        {
            ReceptionistUserId = 101,
            WorkDate = targetDate,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var overrideReq = new ReceptionistDailyOverrideRequest
        {
            Date = targetDate,
            Shifts = new List<CreateReceptionistScheduleRequest>
            {
                new()
                {
                    ReceptionistUserId = 103,
                    WorkDate = targetDate,
                    StartTime = new TimeOnly(13, 0),
                    EndTime = new TimeOnly(19, 0),
                    Notes = "Ca chiều ghi đè"
                },
                new()
                {
                    ReceptionistUserId = 104,
                    WorkDate = targetDate,
                    StartTime = new TimeOnly(13, 0),
                    EndTime = new TimeOnly(19, 0),
                    Notes = "Ca chiều ghi đè"
                }
            }
        };

        var result = await service.ApplyDailyOverrideAsync(overrideReq, 1);

        Assert.True(result.Success);
        var dayShifts = await db.ReceptionistWorkSchedules
            .Where(s => s.WorkDate == targetDate && s.Status == "Scheduled")
            .ToListAsync();
        Assert.Equal(2, dayShifts.Count);
        Assert.All(dayShifts, s => Assert.True(s.ReceptionistUserId == 103 || s.ReceptionistUserId == 104));
    }
}
