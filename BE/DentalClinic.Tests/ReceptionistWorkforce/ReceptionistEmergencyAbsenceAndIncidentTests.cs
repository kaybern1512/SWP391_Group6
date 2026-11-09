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

public class ReceptionistEmergencyAbsenceAndIncidentTests
{
    private readonly Mock<INotificationService> _mockNotify = new();
    private readonly Mock<IAuditLogService> _mockAudit = new();

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedReceptionistsAsync(DentalClinicDbContext db)
    {
        for (long i = 1; i <= 4; i++)
        {
            db.UserAccounts.Add(new UserAccount
            {
                UserId = 400 + i,
                Email = $"receptionist{i}@dentalcare.com",
                Role = UserRole.Receptionist,
                Status = AccountStatus.Active
            });
            db.StaffProfiles.Add(new StaffProfile
            {
                UserId = 400 + i,
                EmployeeCode = $"REC-04{i}",
                FullName = $"Lễ Tân Khẩn Cấp {i}",
                EmploymentStatus = "Active"
            });
        }

        db.ReceptionistShiftTemplates.Add(new ReceptionistShiftTemplate
        {
            ShiftTemplateId = 1,
            TemplateName = "Ca Sáng",
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            MinStaffRequired = 2,
            IsActive = true,
            DisplayOrder = 1
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ReportEmergencyAbsence_CreatesIncidentEvenIfCoverageDropsBelowTwo()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var service = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);

        var targetDate = new DateOnly(2026, 12, 10);

        // Exactly 2 receptionists scheduled (401 and 402)
        var s1 = new ReceptionistWorkSchedule
        {
            ReceptionistScheduleId = 4001,
            ReceptionistUserId = 401,
            WorkDate = targetDate,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        };
        var s2 = new ReceptionistWorkSchedule
        {
            ReceptionistScheduleId = 4002,
            ReceptionistUserId = 402,
            WorkDate = targetDate,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.ReceptionistWorkSchedules.AddRange(s1, s2);
        await db.SaveChangesAsync();

        // 401 has sudden fever on the morning of duty and reports emergency absence
        var reportRes = await service.ReportEmergencyAbsenceAsync(new ReportEmergencyAbsenceRequest
        {
            ReceptionistUserId = 401,
            ScheduleId = 4001,
            Reason = "Sốt cao đột xuất không thể trực quầy"
        }, 401);

        Assert.True(reportRes.Success);
        Assert.NotNull(reportRes.Data);
        Assert.Equal("Open", reportRes.Data.Status);
        Assert.Equal("EmergencyAbsence", reportRes.Data.IncidentType);
        Assert.Equal(1, reportRes.Data.ShortageCount); // Only 1 remains available, minimum is 2

        // Verify schedule was updated to AbsenceReported
        var updatedSchedule = await db.ReceptionistWorkSchedules.FindAsync(4001L);
        Assert.Equal("AbsenceReported", updatedSchedule!.Status);

        // Incident exists in DB
        var incident = await db.ReceptionistCoverageIncidents.FindAsync(reportRes.Data.IncidentId);
        Assert.NotNull(incident);
        Assert.Equal(401, incident!.AffectedReceptionistUserId);
    }

    [Fact]
    public async Task ResolveIncident_AssignsReplacementAndMarksResolved()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var service = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);

        var targetDate = new DateOnly(2026, 12, 11);

        var s1 = new ReceptionistWorkSchedule
        {
            ReceptionistScheduleId = 5001,
            ReceptionistUserId = 401,
            WorkDate = targetDate,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.ReceptionistWorkSchedules.Add(s1);
        await db.SaveChangesAsync();

        var reportRes = await service.ReportEmergencyAbsenceAsync(new ReportEmergencyAbsenceRequest
        {
            ReceptionistUserId = 401,
            ScheduleId = 5001,
            Reason = "Xe hỏng giữa đường"
        }, 401);
        var incidentId = reportRes.Data!.IncidentId;

        // Admin resolves by assigning User 403 as replacement
        var resolveRes = await service.ResolveCoverageIncidentAsync(incidentId, new ResolveCoverageIncidentRequest
        {
            ReplacementReceptionistUserId = 403,
            ResolutionNotes = "Đã điều phối Lễ Tân 403 đến trực thay"
        }, 1);

        Assert.True(resolveRes.Success);

        var incidentInDb = await db.ReceptionistCoverageIncidents.FindAsync(incidentId);
        Assert.NotNull(incidentInDb);
        Assert.Equal("Resolved", incidentInDb!.Status);
        Assert.Equal(403, incidentInDb.ReplacementReceptionistUserId);

        // Verify replacement schedule was added
        var replacementSchedule = await db.ReceptionistWorkSchedules
            .FirstOrDefaultAsync(s => s.ReceptionistUserId == 403 && s.WorkDate == targetDate);
        Assert.NotNull(replacementSchedule);
        Assert.Equal("Scheduled", replacementSchedule!.Status);
    }

    [Fact]
    public async Task CheckShiftCoverageForDateAsync_DetectsUnderstaffing()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedReceptionistsAsync(db);

        var service = new ReceptionistCoverageService(db, _mockNotify.Object, _mockAudit.Object);

        var targetDate = new DateOnly(2026, 12, 12);

        // Only 1 receptionist scheduled 07:00 - 13:00 (understaffed because min is 2)
        db.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
        {
            ReceptionistUserId = 401,
            WorkDate = targetDate,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var coverageResults = await service.CheckShiftCoverageForDateAsync(targetDate);

        Assert.NotEmpty(coverageResults);
        Assert.Contains(coverageResults, c => c.IsUnderstaffed);
    }
}
