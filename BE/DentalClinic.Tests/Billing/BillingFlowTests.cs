using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Billing;

public class BillingFlowTests
{
    private readonly Mock<ILogger<BillingService>> _mockLogger = new();
    private readonly Mock<INotificationService> _mockNotification = new();

    private DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedDataAsync(DentalClinicDbContext db)
    {
        // Staff / Receptionist
        var staffUser = new UserAccount
        {
            UserId = 201,
            Email = "receptionist@clinic.com",
            Role = UserRole.Receptionist,
            Status = AccountStatus.Active
        };
        var staffProfile = new StaffProfile
        {
            UserId = 201,
            EmployeeCode = "STAFF-001",
            FullName = "Lễ Tân Thu Ngân",
            EmploymentStatus = "Active",
            CreatedAt = DateTime.UtcNow
        };

        // Patient
        var patientUser = new UserAccount
        {
            UserId = 101,
            Email = "patient@clinic.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = 101,
            PatientCode = "PAT-001",
            FullName = "Nguyễn Văn A",
            EmergencyContact = "0987654321",
            CreatedAt = DateTime.UtcNow
        };

        // Department & Service
        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Hàm Mặt",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var service = new DepartmentService
        {
            DepartmentServiceId = 1,
            DepartmentId = 1,
            ServiceName = "Nhổ răng khôn",
            Price = 1500000m,
            Status = "Active"
        };

        // Appointment
        var appt = new Appointment
        {
            AppointmentId = 501,
            AppointmentCode = "APT-20261013-001",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 1,
            ReasonForVisit = "Đau răng khôn hàm dưới",
            BookingSource = "Online",
            Status = AppointmentStatus.InService,
            ScheduledStart = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        db.UserAccounts.AddRange(staffUser, patientUser);
        db.StaffProfiles.Add(staffProfile);
        db.PatientProfiles.Add(patientProfile);
        db.Departments.Add(dept);
        db.DepartmentServices.Add(service);
        db.Appointments.Add(appt);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateInvoice_ShouldCalculateTotalAndGenerateInvoiceCode_Successfully()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);

        var request = new CreateInvoiceRequest
        {
            AppointmentId = 501,
            DiscountAmount = 100000m,
            TaxPercent = 10m,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Nhổ răng khôn", Quantity = 1, UnitPrice = 1500000m },
                new() { Description = "Thuốc giảm đau", Quantity = 2, UnitPrice = 50000m }
            }
        };

        var result = await service.CreateInvoiceAsync(201, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.StartsWith("INV-", result.Data.InvoiceCode);
        // SubTotal = 1500000 + 100000 = 1600000
        // Discount = 100000 -> DiscountedSubTotal = 1500000
        // Tax 10% = 150000
        // Total = 1500000 + 150000 = 1650000
        Assert.Equal(1600000m, result.Data.SubTotal);
        Assert.Equal(100000m, result.Data.DiscountAmount);
        Assert.Equal(10m, result.Data.TaxPercent);
        Assert.Equal(150000m, result.Data.TaxAmount);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal(1650000m, result.Data.TotalAmount);
        Assert.Equal("Unpaid", result.Data.Status);
        Assert.Equal(1650000m, result.Data.RemainingAmount);
    }

    [Fact]
    public async Task ProcessPayment_PartiallyPaid_ShouldUpdateStatusAndRemaining()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);

        // Create invoice
        var createReq = new CreateInvoiceRequest
        {
            AppointmentId = 501,
            DiscountAmount = 0,
            TaxPercent = 0,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Trám răng", Quantity = 2, UnitPrice = 300000m }
            }
        };
        var createResult = await service.CreateInvoiceAsync(201, createReq);
        Assert.True(createResult.Success);
        var invoiceId = createResult.Data!.InvoiceId;

        // Pay deposit of 200,000 out of 600,000
        var payReq = new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 200000m,
            Method = "Cash",
            TransactionReference = "CASH-001"
        };

        var payResult = await service.ProcessPaymentAsync(201, payReq);

        Assert.True(payResult.Success);
        Assert.NotNull(payResult.Data);
        Assert.Equal("PartiallyPaid", payResult.Data.Status);
        Assert.Equal(200000m, payResult.Data.PaidAmount);
        Assert.Equal(400000m, payResult.Data.RemainingAmount);

        // Appointment should still be InService since not fully paid
        var appt = await db.Appointments.FindAsync(501L);
        Assert.Equal(AppointmentStatus.InService, appt!.Status);
    }

    [Fact]
    public async Task ProcessPayment_FullPayment_ShouldChangeAppointmentToCompleted()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);

        var createReq = new CreateInvoiceRequest
        {
            AppointmentId = 501,
            DiscountAmount = 0,
            TaxPercent = 0,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Cạo vôi răng", Quantity = 1, UnitPrice = 500000m }
            }
        };
        var createResult = await service.CreateInvoiceAsync(201, createReq);
        var invoiceId = createResult.Data!.InvoiceId;

        // Pay in full
        var payReq = new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 500000m,
            Method = "BankTransfer",
            TransactionReference = "VNPAY-998877"
        };
        var payResult = await service.ProcessPaymentAsync(201, payReq);

        Assert.True(payResult.Success);
        Assert.Equal("Paid", payResult.Data!.Status);
        Assert.Equal(0m, payResult.Data.RemainingAmount);

        // Appointment MUST be Completed
        var appt = await db.Appointments.FindAsync(501L);
        Assert.Equal(AppointmentStatus.Completed, appt!.Status);
        Assert.NotNull(appt.CompletedAt);

        // AppointmentStatusHistory should record this transition
        var history = await db.AppointmentStatusHistories.FirstOrDefaultAsync(h => h.AppointmentId == 501L);
        Assert.NotNull(history);
        Assert.Equal(AppointmentStatus.Completed, history.NewStatus);
    }

    [Fact]
    public async Task ProcessPayment_Overpayment_ShouldFail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);

        var createReq = new CreateInvoiceRequest
        {
            AppointmentId = 501,
            DiscountAmount = 0,
            TaxPercent = 0,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Cạo vôi", Quantity = 1, UnitPrice = 200000m }
            }
        };
        var createResult = await service.CreateInvoiceAsync(201, createReq);
        var invoiceId = createResult.Data!.InvoiceId;

        // Overpay
        var payReq = new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 500000m, // exceeds 200,000
            Method = "Cash"
        };
        var payResult = await service.ProcessPaymentAsync(201, payReq);

        Assert.False(payResult.Success);
        Assert.Contains("vượt quá", payResult.Message);
    }
}
