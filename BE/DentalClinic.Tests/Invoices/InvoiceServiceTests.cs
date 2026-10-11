using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Invoices.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DentalClinic.Tests.Invoices;

public class InvoiceServiceTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new DentalClinicDbContext(options);
    }

    private static async Task SeedInvoicesAsync(DentalClinicDbContext db)
    {
        var patientUser = new UserAccount
        {
            UserId = 101,
            Email = "patient1@dentalcare.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };

        var patientTwoUser = new UserAccount
        {
            UserId = 202,
            Email = "patient2@dentalcare.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };

        var receptionistUser = new UserAccount
        {
            UserId = 301,
            Email = "receptionist@dentalcare.com",
            Role = UserRole.Receptionist,
            Status = AccountStatus.Active
        };

        var patientProfile = new PatientProfile
        {
            UserId = 101,
            PatientCode = "PT-101",
            FullName = "Nguyễn Văn A",
            User = patientUser
        };

        var patientTwoProfile = new PatientProfile
        {
            UserId = 202,
            PatientCode = "PT-202",
            FullName = "Trần Thị B",
            User = patientTwoUser
        };

        var appointment1 = new Appointment
        {
            AppointmentId = 1001,
            AppointmentCode = "APT-1001",
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            ReasonForVisit = "Khám răng tổng quát",
            BookingSource = "WalkIn",
            ScheduledStart = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            PatientUser = patientProfile
        };

        var appointment2 = new Appointment
        {
            AppointmentId = 1002,
            AppointmentCode = "APT-1002",
            PatientUserId = 202,
            Status = AppointmentStatus.Completed,
            ReasonForVisit = "Tẩy trắng răng",
            BookingSource = "WalkIn",
            ScheduledStart = DateTime.UtcNow.AddDays(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            PatientUser = patientTwoProfile
        };

        var invoice1 = new Invoice
        {
            InvoiceId = 9001,
            InvoiceCode = "INV-9001",
            AppointmentId = 1001,
            GeneratedByUserId = 301,
            TotalAmount = 1200000m,
            Status = "Unpaid",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            Appointment = appointment1,
            InvoiceItems = new List<InvoiceItem>
            {
                new()
                {
                    InvoiceItemId = 1,
                    Description = "Khám tổng quát",
                    Quantity = 1,
                    UnitPrice = 350000m,
                    Amount = 350000m
                },
                new()
                {
                    InvoiceItemId = 2,
                    Description = "Tẩy trắng răng",
                    Quantity = 1,
                    UnitPrice = 850000m,
                    Amount = 850000m
                }
            }
        };

        var invoice2 = new Invoice
        {
            InvoiceId = 9002,
            InvoiceCode = "INV-9002",
            AppointmentId = 1002,
            GeneratedByUserId = 301,
            TotalAmount = 600000m,
            Status = "Paid",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            Appointment = appointment2,
            InvoiceItems = new List<InvoiceItem>
            {
                new()
                {
                    InvoiceItemId = 3,
                    Description = "Khám định kỳ",
                    Quantity = 1,
                    UnitPrice = 250000m,
                    Amount = 250000m
                },
                new()
                {
                    InvoiceItemId = 4,
                    Description = "Thuốc giảm đau",
                    Quantity = 1,
                    UnitPrice = 350000m,
                    Amount = 350000m
                }
            },
            Payments = new List<Payment>
            {
                new()
                {
                    PaymentId = 11,
                    InvoiceId = 9002,
                    PaidByPatientUserId = 202,
                    Method = "Tiền mặt",
                    Amount = 600000m,
                    TransactionReference = "TXN-PREV-9002",
                    Status = "Paid",
                    PaidAt = DateTime.UtcNow.AddDays(-2),
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    PaidByPatientUser = patientTwoProfile
                }
            }
        };

        db.UserAccounts.AddRange(patientUser, patientTwoUser, receptionistUser);
        db.PatientProfiles.AddRange(patientProfile, patientTwoProfile);
        db.Appointments.AddRange(appointment1, appointment2);
        db.Invoices.AddRange(invoice1, invoice2);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetInvoicesAsync_ForPatient_ReturnsOnlyOwnInvoices()
    {
        var db = CreateDbContext(nameof(GetInvoicesAsync_ForPatient_ReturnsOnlyOwnInvoices));
        await SeedInvoicesAsync(db);

        var service = new InvoiceService(db, NullLogger<InvoiceService>.Instance);

        var result = await service.GetInvoicesAsync(currentUserId: 101, currentRole: UserRole.Patient);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var invoices = result.Data!;
        Assert.Single(invoices);
        Assert.Equal("PT-101", invoices.Single().PatientCode);
    }

    [Fact]
    public async Task GetInvoicesAsync_ForDentist_IsDenied()
    {
        var db = CreateDbContext(nameof(GetInvoicesAsync_ForDentist_IsDenied));
        await SeedInvoicesAsync(db);
        var service = new InvoiceService(db, NullLogger<InvoiceService>.Instance);

        var result = await service.GetInvoicesAsync(currentUserId: 401, currentRole: UserRole.Dentist);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetInvoiceReportAsync_ForDepartmentManager_IsDenied()
    {
        var db = CreateDbContext(nameof(GetInvoiceReportAsync_ForDepartmentManager_IsDenied));
        await SeedInvoicesAsync(db);
        var service = new InvoiceService(db, NullLogger<InvoiceService>.Instance);

        var result = await service.GetInvoiceReportAsync(currentUserId: 301, currentRole: UserRole.DepartmentManager);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetInvoiceByIdAsync_WhenPatientDoesNotOwnInvoice_ReturnsFailure()
    {
        var db = CreateDbContext(nameof(GetInvoiceByIdAsync_WhenPatientDoesNotOwnInvoice_ReturnsFailure));
        await SeedInvoicesAsync(db);

        var service = new InvoiceService(db, NullLogger<InvoiceService>.Instance);

        var result = await service.GetInvoiceByIdAsync(9001, currentUserId: 202, currentRole: UserRole.Patient);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WhenReceptionistConfirms_ReturnsPaidInvoice()
    {
        var db = CreateDbContext(nameof(ConfirmPaymentAsync_WhenReceptionistConfirms_ReturnsPaidInvoice));
        await SeedInvoicesAsync(db);

        var service = new InvoiceService(db, NullLogger<InvoiceService>.Instance);

        var result = await service.ConfirmPaymentAsync(
            invoiceId: 9001,
            currentUserId: 301,
            currentRole: UserRole.Receptionist,
            request: new ConfirmPaymentRequest
            {
                PaymentMethod = "Tiền mặt",
                TransactionReference = "TXN-9001-REF"
            });

        Assert.True(result.Success);
        Assert.Equal("Paid", result.Data!.PaymentStatus);
        Assert.Equal("TXN-9001-REF", result.Data.PaymentReference);

        var invoice = await db.Invoices
            .Include(x => x.Payments)
            .SingleAsync(x => x.InvoiceId == 9001);

        Assert.Equal("Paid", invoice.Status);
        Assert.Single(invoice.Payments);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WhenInvoiceAlreadyPaid_ReturnsFailure()
    {
        var db = CreateDbContext(nameof(ConfirmPaymentAsync_WhenInvoiceAlreadyPaid_ReturnsFailure));
        await SeedInvoicesAsync(db);

        var service = new InvoiceService(db, NullLogger<InvoiceService>.Instance);

        var result = await service.ConfirmPaymentAsync(
            invoiceId: 9002,
            currentUserId: 301,
            currentRole: UserRole.Receptionist,
            request: new ConfirmPaymentRequest
            {
                PaymentMethod = "Chuyển khoản",
                TransactionReference = "TXN-9002-REPEAT"
            });

        Assert.False(result.Success);
        Assert.Contains("đã được thanh toán", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
