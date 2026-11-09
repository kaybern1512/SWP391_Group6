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

public class ReceptionistWorkHandoverTests
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

    private static async Task SeedHandoverTestDataAsync(DentalClinicDbContext db)
    {
        // 2 Receptionists
        for (long i = 1; i <= 2; i++)
        {
            db.UserAccounts.Add(new UserAccount
            {
                UserId = 500 + i,
                Email = $"receptionist{i}@dentalcare.com",
                Role = UserRole.Receptionist,
                Status = AccountStatus.Active
            });
            db.StaffProfiles.Add(new StaffProfile
            {
                UserId = 500 + i,
                EmployeeCode = $"REC-05{i}",
                FullName = $"Lễ Tân Bàn Giao {i}",
                EmploymentStatus = "Active"
            });
        }

        // 1 Patient
        db.UserAccounts.Add(new UserAccount
        {
            UserId = 601,
            Email = "patient1@test.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        });
        db.PatientProfiles.Add(new PatientProfile
        {
            UserId = 601,
            PatientCode = "PAT-601",
            FullName = "Bệnh Nhân Nguyễn Văn B"
        });

        // 1 Department & Service
        db.Departments.Add(new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Tổng Quát",
            Status = "Active"
        });
        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = 1,
            DepartmentId = 1,
            ServiceName = "Khám Răng Tổng Quát",
            Price = 100000,
            Status = "Active"
        });

        // 1 Dentist
        db.UserAccounts.Add(new UserAccount
        {
            UserId = 701,
            Email = "dentist@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        });
        db.DentistProfiles.Add(new DentistProfile
        {
            UserId = 701,
            LicenseNumber = "DENT-001"
        });

        // 1 Pending appointment for today
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 8001,
            AppointmentCode = "APT-202612-001",
            PatientUserId = 601,
            RequestedDentistUserId = 701,
            RequestedServiceId = 1,
            Status = AppointmentStatus.PendingReceptionReview,
            ReasonForVisit = "Khám răng định kỳ",
            BookingSource = "Online",
            ScheduledStart = DateTime.Today.AddHours(9),
            ScheduledEnd = DateTime.Today.AddHours(10),
            CreatedAt = DateTime.UtcNow
        });

        // 1 Unpaid invoice
        db.Invoices.Add(new Invoice
        {
            InvoiceId = 9001,
            AppointmentId = 8001,
            InvoiceCode = "INV-202612-001",
            TotalAmount = 500000,
            GeneratedByUserId = 501,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetCurrentHandoverSummaryAsync_ReturnsAccurateCounters()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedHandoverTestDataAsync(db);

        var service = new ReceptionistHandoverService(db, _mockNotify.Object, _mockAudit.Object);

        var result = await service.GetCurrentHandoverSummaryAsync();

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.PendingAppointmentRequests >= 1);
        Assert.True(result.Data.UnpaidInvoicesCount >= 1);
        Assert.Contains(result.Data.SampleUnpaidInvoices, i => i.InvoiceCode == "INV-202612-001");
    }

    [Fact]
    public async Task HandoverLifecycle_CreateAndAcknowledge_UpdatesStatus()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedHandoverTestDataAsync(db);

        var service = new ReceptionistHandoverService(db, _mockNotify.Object, _mockAudit.Object);

        // User 501 creates handover
        var createRes = await service.CreateHandoverAsync(new CreateWorkHandoverRequest
        {
            ToReceptionistUserId = 502,
            WorkDate = DateOnly.FromDateTime(DateTime.Today),
            HandoverType = "ShiftEnd",
            Notes = "Còn 1 bệnh nhân chờ thanh toán 500k, chú ý bàn giao lại viện phí"
        }, 501);

        Assert.True(createRes.Success);
        var handoverId = createRes.Data!.HandoverId;
        Assert.Equal("Transferred", createRes.Data.HandoverStatus);

        // User 501 tries to acknowledge their own handover -> Should FAIL
        var selfAck = await service.AcknowledgeHandoverAsync(handoverId, 501);
        Assert.False(selfAck.Success);
        Assert.Contains("không thể tự tiếp nhận", selfAck.Message ?? "");

        // User 502 acknowledges handover -> Should SUCCEED
        var ackRes = await service.AcknowledgeHandoverAsync(handoverId, 502);
        Assert.True(ackRes.Success);
        var handoverInDb = await db.ReceptionistWorkHandovers.FindAsync(handoverId);
        Assert.Equal("Acknowledged", handoverInDb!.HandoverStatus);
        Assert.NotNull(handoverInDb.AcknowledgedAt);
    }
}
