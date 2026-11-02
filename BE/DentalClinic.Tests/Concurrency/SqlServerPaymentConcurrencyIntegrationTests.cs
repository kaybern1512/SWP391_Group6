using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Concurrency;

[Collection("SqlServerIntegration")]
public class SqlServerPaymentConcurrencyIntegrationTests
{
    private const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=DentalClinic_PaymentConcurrencyTests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

    private static async Task<bool> IsLocalDbAvailableAsync()
    {
        try
        {
            using var conn = new SqlConnection("Server=(localdb)\\mssqllocaldb;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5;");
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static DentalClinicDbContext CreateSqlServerDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task<(long PatientUserId, long StaffUserId, long AppointmentId)> SeedPaymentTestDataAsync(DentalClinicDbContext db)
    {
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var patientUser = new UserAccount { Email = "patient.pay@test.com", Role = UserRole.Patient, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var staffUser = new UserAccount { Email = "staff.pay@test.com", Role = UserRole.Receptionist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        db.UserAccounts.AddRange(patientUser, staffUser);
        await db.SaveChangesAsync();

        var patientProfile = new PatientProfile { UserId = patientUser.UserId, PatientCode = "PAT-PAY-01", FullName = "Nguyễn Văn Test", CreatedAt = DateTime.UtcNow };
        var staffProfile = new StaffProfile { UserId = staffUser.UserId, EmployeeCode = "STAFF-PAY-01", FullName = "Lễ Tân Thu Ngân", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };
        db.PatientProfiles.Add(patientProfile);
        db.StaffProfiles.Add(staffProfile);
        await db.SaveChangesAsync();

        var appt = new Appointment
        {
            AppointmentCode = "APP-PAY-001",
            PatientUserId = patientUser.UserId,
            Status = AppointmentStatus.InService,
            ReasonForVisit = "Kiểm tra thanh toán đồng thời",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        return (patientUser.UserId, staffUser.UserId, appt.AppointmentId);
    }

    [Fact]
    public async Task SqlServer_ConcurrentPayments_ExceedingTotal_OnlyOneSucceeds_NoOverpayment()
    {
        if (!await IsLocalDbAvailableAsync()) return;

        using var db = CreateSqlServerDbContext();
        var seed = await SeedPaymentTestDataAsync(db);

        var mockNotif = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<BillingService>>();

        // Create an invoice with TotalAmount = 1,000,000 VND
        var billingService = new BillingService(db, mockNotif.Object, mockLogger.Object);
        var createResult = await billingService.CreateInvoiceAsync(seed.StaffUserId, new CreateInvoiceRequest
        {
            AppointmentId = seed.AppointmentId,
            DiscountAmount = 0,
            TaxPercent = 0,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Thủ thuật nha khoa A", Quantity = 1, UnitPrice = 1000000m }
            }
        });

        Assert.True(createResult.Success);
        var invoiceId = createResult.Data!.InvoiceId;
        Assert.Equal(1000000m, createResult.Data.TotalAmount);

        // Submit TWO CONCURRENT PAYMENTS of 700,000 VND each using independent DbContext instances
        var task1 = Task.Run(async () =>
        {
            using var db1 = CreateSqlServerDbContext();
            var service1 = new BillingService(db1, mockNotif.Object, mockLogger.Object);
            return await service1.ProcessPaymentAsync(seed.StaffUserId, new ProcessPaymentRequest
            {
                InvoiceId = invoiceId,
                Amount = 700000m,
                Method = "Cash",
                TransactionReference = "TXN-CONC-001"
            });
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = CreateSqlServerDbContext();
            var service2 = new BillingService(db2, mockNotif.Object, mockLogger.Object);
            return await service2.ProcessPaymentAsync(seed.StaffUserId, new ProcessPaymentRequest
            {
                InvoiceId = invoiceId,
                Amount = 700000m,
                Method = "BankTransfer",
                TransactionReference = "TXN-CONC-002"
            });
        });

        var results = await Task.WhenAll(task1, task2);

        int successCount = results.Count(r => r.Success);
        int failCount = results.Count(r => !r.Success);

        // Exactly one must succeed and one must fail
        Assert.Equal(1, successCount);
        Assert.Equal(1, failCount);

        var failedResult = results.First(r => !r.Success);
        Assert.Contains("vượt quá", failedResult.Message);

        // Query database fresh to verify exact relational state
        using var verifyDb = CreateSqlServerDbContext();
        var payments = await verifyDb.Payments.Where(p => p.InvoiceId == invoiceId).ToListAsync();
        Assert.Single(payments); // Failed payment must NOT leave orphan or partial records!
        Assert.Equal(700000m, payments.First().Amount);
        Assert.Equal("Successful", payments.First().Status);

        var invoiceDb = await verifyDb.Invoices.FindAsync(invoiceId);
        Assert.Equal("PartiallyPaid", invoiceDb!.Status);
        Assert.Equal(1000000m, invoiceDb.TotalAmount);
    }

    [Fact]
    public async Task SqlServer_SimultaneousFinalPayments_OneSucceeds_OneFails_NoOverpayment()
    {
        if (!await IsLocalDbAvailableAsync()) return;

        using var db = CreateSqlServerDbContext();
        var seed = await SeedPaymentTestDataAsync(db);

        var mockNotif = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<BillingService>>();

        var billingService = new BillingService(db, mockNotif.Object, mockLogger.Object);
        var createResult = await billingService.CreateInvoiceAsync(seed.StaffUserId, new CreateInvoiceRequest
        {
            AppointmentId = seed.AppointmentId,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Dịch vụ B", Quantity = 1, UnitPrice = 1000000m }
            }
        });
        var invoiceId = createResult.Data!.InvoiceId;

        // Pay initial deposit 400,000 VND (remaining 600,000 VND)
        var depResult = await billingService.ProcessPaymentAsync(seed.StaffUserId, new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 400000m,
            Method = "Cash",
            TransactionReference = "DEP-001"
        });
        Assert.True(depResult.Success);
        Assert.Equal("PartiallyPaid", depResult.Data!.Status);

        // Now two concurrent payments of 600,000 VND each
        var task1 = Task.Run(async () =>
        {
            using var db1 = CreateSqlServerDbContext();
            var service1 = new BillingService(db1, mockNotif.Object, mockLogger.Object);
            return await service1.ProcessPaymentAsync(seed.StaffUserId, new ProcessPaymentRequest
            {
                InvoiceId = invoiceId,
                Amount = 600000m,
                Method = "Cash",
                TransactionReference = "FINAL-001"
            });
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = CreateSqlServerDbContext();
            var service2 = new BillingService(db2, mockNotif.Object, mockLogger.Object);
            return await service2.ProcessPaymentAsync(seed.StaffUserId, new ProcessPaymentRequest
            {
                InvoiceId = invoiceId,
                Amount = 600000m,
                Method = "CreditCard",
                TransactionReference = "FINAL-002"
            });
        });

        var results = await Task.WhenAll(task1, task2);

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, results.Count(r => !r.Success));

        using var verifyDb = CreateSqlServerDbContext();
        var invoiceDb = await verifyDb.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
        Assert.Equal("Paid", invoiceDb!.Status);
        Assert.Equal(2, invoiceDb.Payments.Count); // 400,000 + 600,000
        Assert.Equal(1000000m, invoiceDb.Payments.Sum(p => p.Amount));
    }

    [Fact]
    public async Task SqlServer_ConcurrentPayments_DuplicateTransactionReference_SecondFails()
    {
        if (!await IsLocalDbAvailableAsync()) return;

        using var db = CreateSqlServerDbContext();
        var seed = await SeedPaymentTestDataAsync(db);

        var mockNotif = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<BillingService>>();

        var billingService = new BillingService(db, mockNotif.Object, mockLogger.Object);
        var createResult = await billingService.CreateInvoiceAsync(seed.StaffUserId, new CreateInvoiceRequest
        {
            AppointmentId = seed.AppointmentId,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Dịch vụ C", Quantity = 1, UnitPrice = 1000000m }
            }
        });
        var invoiceId = createResult.Data!.InvoiceId;

        // Pay 200,000 with reference "TXN-DUP-TEST"
        var pay1 = await billingService.ProcessPaymentAsync(seed.StaffUserId, new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 200000m,
            Method = "BankTransfer",
            TransactionReference = "TXN-DUP-TEST"
        });
        Assert.True(pay1.Success);

        // Try submitting another payment with the identical TransactionReference
        var pay2 = await billingService.ProcessPaymentAsync(seed.StaffUserId, new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 200000m,
            Method = "BankTransfer",
            TransactionReference = "TXN-DUP-TEST"
        });

        Assert.False(pay2.Success);
        Assert.Contains("đã được ghi nhận trước đó", pay2.Message);

        using var verifyDb = CreateSqlServerDbContext();
        var paymentCount = await verifyDb.Payments.CountAsync(p => p.InvoiceId == invoiceId);
        Assert.Equal(1, paymentCount);
    }

    [Fact]
    public async Task SqlServer_PaymentProcessing_PreservesClinicalStatus_DoesNotForceCompleted()
    {
        if (!await IsLocalDbAvailableAsync()) return;

        using var db = CreateSqlServerDbContext();
        var seed = await SeedPaymentTestDataAsync(db);

        var mockNotif = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<BillingService>>();

        var billingService = new BillingService(db, mockNotif.Object, mockLogger.Object);
        var createResult = await billingService.CreateInvoiceAsync(seed.StaffUserId, new CreateInvoiceRequest
        {
            AppointmentId = seed.AppointmentId,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Khám tổng quát", Quantity = 1, UnitPrice = 500000m }
            }
        });
        var invoiceId = createResult.Data!.InvoiceId;

        // Pay 100% of the invoice
        var payResult = await billingService.ProcessPaymentAsync(seed.StaffUserId, new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 500000m,
            Method = "Cash"
        });

        Assert.True(payResult.Success);
        Assert.Equal("Paid", payResult.Data!.Status);

        // In real SQL Server, verify that Appointment.Status is STILL InService!
        using var verifyDb = CreateSqlServerDbContext();
        var apptDb = await verifyDb.Appointments.FindAsync(seed.AppointmentId);
        Assert.Equal(AppointmentStatus.InService, apptDb!.Status);
        Assert.Null(apptDb.CompletedAt); // Must not have CompletedAt populated by billing!
    }

    [Fact]
    public async Task SqlServer_ConcurrentInvoiceCreation_OnlyOneSucceeds_SecondFailsGracefully()
    {
        if (!await IsLocalDbAvailableAsync()) return;

        using var db = CreateSqlServerDbContext();
        var seed = await SeedPaymentTestDataAsync(db);

        var mockNotif = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<BillingService>>();

        var task1 = Task.Run(async () =>
        {
            using var db1 = CreateSqlServerDbContext();
            var service1 = new BillingService(db1, mockNotif.Object, mockLogger.Object);
            return await service1.CreateInvoiceAsync(seed.StaffUserId, new CreateInvoiceRequest
            {
                AppointmentId = seed.AppointmentId,
                DiscountAmount = 0,
                TaxPercent = 0,
                Items = new List<CreateInvoiceItemRequest>
                {
                    new() { Description = "Khám tổng quát 1", Quantity = 1, UnitPrice = 300000m }
                }
            });
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = CreateSqlServerDbContext();
            var service2 = new BillingService(db2, mockNotif.Object, mockLogger.Object);
            return await service2.CreateInvoiceAsync(seed.StaffUserId, new CreateInvoiceRequest
            {
                AppointmentId = seed.AppointmentId,
                DiscountAmount = 50000m,
                TaxPercent = 0,
                Items = new List<CreateInvoiceItemRequest>
                {
                    new() { Description = "Khám tổng quát 2", Quantity = 1, UnitPrice = 300000m }
                }
            });
        });

        var results = await Task.WhenAll(task1, task2);

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, results.Count(r => !r.Success));

        var failed = results.First(r => !r.Success);
        Assert.Contains("đã được lập hóa đơn", failed.Message);

        using var verifyDb = CreateSqlServerDbContext();
        var invoices = await verifyDb.Invoices.Where(i => i.AppointmentId == seed.AppointmentId).ToListAsync();
        Assert.Single(invoices);
    }
}
