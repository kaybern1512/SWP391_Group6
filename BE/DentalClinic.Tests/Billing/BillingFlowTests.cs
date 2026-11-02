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
    public async Task ProcessPayment_FullPayment_ShouldPreserveClinicalStatus_AndMarkInvoicePaid()
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
            TransactionReference = "BANK-998877"
        };
        var payResult = await service.ProcessPaymentAsync(201, payReq);

        Assert.True(payResult.Success);
        Assert.Equal("Paid", payResult.Data!.Status);
        Assert.Equal(0m, payResult.Data.RemainingAmount);

        // P0-01 RULE: Payment processing must NOT silently override the clinical status!
        // Appointment was InService, and it MUST remain InService until Dentist completes it.
        var appt = await db.Appointments.FindAsync(501L);
        Assert.Equal(AppointmentStatus.InService, appt!.Status);
    }

    [Fact]
    public async Task Payment_CheckedInAppointment_FullyPaid_ShouldRemainCheckedIn_And_InvoicePaid()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);
        var appt = await db.Appointments.FindAsync(501L);
        appt!.Status = AppointmentStatus.CheckedIn;
        await db.SaveChangesAsync();

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);
        var createResult = await service.CreateInvoiceAsync(201, new CreateInvoiceRequest
        {
            AppointmentId = 501,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Khám lâm sàng", Quantity = 1, UnitPrice = 200000m }
            }
        });

        var payResult = await service.ProcessPaymentAsync(201, new ProcessPaymentRequest
        {
            InvoiceId = createResult.Data!.InvoiceId,
            Amount = 200000m,
            Method = "Cash"
        });

        Assert.True(payResult.Success);
        Assert.Equal("Paid", payResult.Data!.Status);

        var updatedAppt = await db.Appointments.FindAsync(501L);
        Assert.Equal(AppointmentStatus.CheckedIn, updatedAppt!.Status);
    }

    [Fact]
    public async Task Payment_InServiceAppointment_FullyPaid_ShouldRemainInService_And_InvoicePaid()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);
        var appt = await db.Appointments.FindAsync(501L);
        appt!.Status = AppointmentStatus.InService;
        await db.SaveChangesAsync();

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);
        var createResult = await service.CreateInvoiceAsync(201, new CreateInvoiceRequest
        {
            AppointmentId = 501,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Điều trị tủy", Quantity = 1, UnitPrice = 1200000m }
            }
        });

        var payResult = await service.ProcessPaymentAsync(201, new ProcessPaymentRequest
        {
            InvoiceId = createResult.Data!.InvoiceId,
            Amount = 1200000m,
            Method = "CreditCard"
        });

        Assert.True(payResult.Success);
        Assert.Equal("Paid", payResult.Data!.Status);

        var updatedAppt = await db.Appointments.FindAsync(501L);
        Assert.Equal(AppointmentStatus.InService, updatedAppt!.Status);
    }

    [Fact]
    public async Task Billing_CompletedAppointment_UnpaidInvoice_ShouldRemainCompleted_And_InvoiceUnpaid()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);
        var appt = await db.Appointments.FindAsync(501L);
        appt!.Status = AppointmentStatus.Completed;
        appt.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);
        var createResult = await service.CreateInvoiceAsync(201, new CreateInvoiceRequest
        {
            AppointmentId = 501,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Nhổ răng số 8", Quantity = 1, UnitPrice = 1500000m }
            }
        });

        Assert.True(createResult.Success);
        Assert.Equal("Unpaid", createResult.Data!.Status);

        var updatedAppt = await db.Appointments.FindAsync(501L);
        Assert.Equal(AppointmentStatus.Completed, updatedAppt!.Status);
    }

    [Fact]
    public async Task Billing_CompletedAppointment_PartiallyPaidInvoice_ShouldRemainCompleted_And_InvoicePartiallyPaid()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);
        var appt = await db.Appointments.FindAsync(501L);
        appt!.Status = AppointmentStatus.Completed;
        appt.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);
        var createResult = await service.CreateInvoiceAsync(201, new CreateInvoiceRequest
        {
            AppointmentId = 501,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Gắn mắc cài", Quantity = 1, UnitPrice = 5000000m }
            }
        });

        var payResult = await service.ProcessPaymentAsync(201, new ProcessPaymentRequest
        {
            InvoiceId = createResult.Data!.InvoiceId,
            Amount = 2000000m,
            Method = "BankTransfer"
        });

        Assert.True(payResult.Success);
        Assert.Equal("PartiallyPaid", payResult.Data!.Status);
        Assert.Equal(3000000m, payResult.Data.RemainingAmount);

        var updatedAppt = await db.Appointments.FindAsync(501L);
        Assert.Equal(AppointmentStatus.Completed, updatedAppt!.Status);
    }

    [Fact]
    public async Task Billing_CompletedAppointment_FullyPaidInvoice_ShouldRemainCompleted_And_InvoicePaid()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);
        var appt = await db.Appointments.FindAsync(501L);
        appt!.Status = AppointmentStatus.Completed;
        appt.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);
        var createResult = await service.CreateInvoiceAsync(201, new CreateInvoiceRequest
        {
            AppointmentId = 501,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Tẩy trắng răng", Quantity = 1, UnitPrice = 1800000m }
            }
        });

        var payResult = await service.ProcessPaymentAsync(201, new ProcessPaymentRequest
        {
            InvoiceId = createResult.Data!.InvoiceId,
            Amount = 1800000m,
            Method = "Cash"
        });

        Assert.True(payResult.Success);
        Assert.Equal("Paid", payResult.Data!.Status);
        Assert.Equal(0m, payResult.Data.RemainingAmount);

        var updatedAppt = await db.Appointments.FindAsync(501L);
        Assert.Equal(AppointmentStatus.Completed, updatedAppt!.Status);
    }

    [Fact]
    public async Task ProcessPayment_DuplicateTransactionReference_ShouldFail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);
        var createResult = await service.CreateInvoiceAsync(201, new CreateInvoiceRequest
        {
            AppointmentId = 501,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Cạo vôi", Quantity = 1, UnitPrice = 1000000m }
            }
        });

        var pay1 = await service.ProcessPaymentAsync(201, new ProcessPaymentRequest
        {
            InvoiceId = createResult.Data!.InvoiceId,
            Amount = 500000m,
            Method = "BankTransfer",
            TransactionReference = "REF-DUPLICATE-001"
        });
        Assert.True(pay1.Success);

        // Try submitting same transaction reference on second payment
        var pay2 = await service.ProcessPaymentAsync(201, new ProcessPaymentRequest
        {
            InvoiceId = createResult.Data!.InvoiceId,
            Amount = 500000m,
            Method = "BankTransfer",
            TransactionReference = "REF-DUPLICATE-001"
        });

        Assert.False(pay2.Success);
        Assert.Contains("đã được ghi nhận trước đó", pay2.Message);
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

    [Fact]
    public async Task CalculateTotal_DiscountWithoutTax_ShouldCalculateCorrectly()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);

        var request = new CreateInvoiceRequest
        {
            AppointmentId = 501,
            DiscountAmount = 100000m,
            TaxPercent = 0m,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Gói tổng quát", Quantity = 1, UnitPrice = 1000000m }
            }
        };

        var result = await service.CreateInvoiceAsync(201, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1000000m, result.Data.SubTotal);
        Assert.Equal(100000m, result.Data.DiscountAmount);
        Assert.Equal(0m, result.Data.TaxPercent);
        Assert.Equal(0m, result.Data.TaxAmount);
        Assert.Equal(900000m, result.Data.TotalAmount);
        Assert.Equal(900000m, result.Data.RemainingAmount);
    }

    [Fact]
    public async Task CalculateTotal_DiscountWith10PercentTax_ShouldCalculateTaxOnDiscountedSubtotal()
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
                new() { Description = "Gói tổng quát", Quantity = 1, UnitPrice = 1000000m }
            }
        };

        var result = await service.CreateInvoiceAsync(201, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1000000m, result.Data.SubTotal);
        Assert.Equal(100000m, result.Data.DiscountAmount);
        Assert.Equal(10m, result.Data.TaxPercent);
        // Tax is 10% of (1,000,000 - 100,000) = 90,000
        Assert.Equal(90000m, result.Data.TaxAmount);
        // Total = 900,000 + 90,000 = 990,000
        Assert.Equal(990000m, result.Data.TotalAmount);
        Assert.Equal(990000m, result.Data.RemainingAmount);
    }

    [Fact]
    public async Task CalculateTotal_DiscountExceedingSubtotal_ShouldClampDiscountAndResultInZeroTotal()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);

        var request = new CreateInvoiceRequest
        {
            AppointmentId = 501,
            DiscountAmount = 600000m, // Exceeds 500,000
            TaxPercent = 10m,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Khám tổng quát", Quantity = 1, UnitPrice = 500000m }
            }
        };

        var result = await service.CreateInvoiceAsync(201, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(500000m, result.Data.SubTotal);
        // Discount clamped to SubTotal
        Assert.Equal(500000m, result.Data.DiscountAmount);
        Assert.Equal(0m, result.Data.TaxAmount);
        Assert.Equal(0m, result.Data.TotalAmount);
        Assert.Equal(0m, result.Data.RemainingAmount);
        Assert.Equal("Paid", result.Data.Status);
        Assert.Empty(result.Data.Payments);
    }

    [Fact]
    public async Task CreateInvoice_FullDiscount_TotalZero_ShouldBePaid_WithoutFakePayment()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);

        var request = new CreateInvoiceRequest
        {
            AppointmentId = 501,
            DiscountAmount = 500000m, // Full 100% discount
            TaxPercent = 0m,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Khám và tư vấn miễn phí", Quantity = 1, UnitPrice = 500000m }
            }
        };

        var result = await service.CreateInvoiceAsync(201, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(500000m, result.Data.SubTotal);
        Assert.Equal(500000m, result.Data.DiscountAmount);
        Assert.Equal(0m, result.Data.TotalAmount);
        Assert.Equal(0m, result.Data.RemainingAmount);
        Assert.Equal(0m, result.Data.PaidAmount);
        Assert.Equal("Paid", result.Data.Status);
        Assert.Empty(result.Data.Payments); // Zero fake positive payments!
    }

    [Fact]
    public async Task CreateInvoice_NegativeDiscount_ShouldFailValidation()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);

        var request = new CreateInvoiceRequest
        {
            AppointmentId = 501,
            DiscountAmount = -50000m,
            TaxPercent = 10m,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Dịch vụ", Quantity = 1, UnitPrice = 500000m }
            }
        };

        var result = await service.CreateInvoiceAsync(201, request);

        Assert.False(result.Success);
        Assert.Contains("không thể âm", result.Message);
    }

    [Fact]
    public async Task CreateInvoice_InvalidTaxPercent_ShouldFailValidation()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);

        var requestNegativeTax = new CreateInvoiceRequest
        {
            AppointmentId = 501,
            DiscountAmount = 0m,
            TaxPercent = -5m,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Dịch vụ", Quantity = 1, UnitPrice = 500000m }
            }
        };

        var resultNeg = await service.CreateInvoiceAsync(201, requestNegativeTax);
        Assert.False(resultNeg.Success);
        Assert.Contains("từ 0% đến 100%", resultNeg.Message);

        var requestExcessiveTax = new CreateInvoiceRequest
        {
            AppointmentId = 501,
            DiscountAmount = 0m,
            TaxPercent = 105m,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Dịch vụ", Quantity = 1, UnitPrice = 500000m }
            }
        };

        var resultExcess = await service.CreateInvoiceAsync(201, requestExcessiveTax);
        Assert.False(resultExcess.Success);
        Assert.Contains("từ 0% đến 100%", resultExcess.Message);
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.PendingReceptionReview)]
    [InlineData(AppointmentStatus.PendingDepartmentReview)]
    [InlineData(AppointmentStatus.AwaitingPatientResponse)]
    [InlineData(AppointmentStatus.Rejected)]
    [InlineData(AppointmentStatus.Withdrawn)]
    [InlineData(AppointmentStatus.Expired)]
    public async Task CreateInvoice_NonBillableAppointmentStatus_ShouldFailValidation(string status)
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);
        var appt = await db.Appointments.FindAsync(501L);
        appt!.Status = status;
        await db.SaveChangesAsync();

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);

        var request = new CreateInvoiceRequest
        {
            AppointmentId = 501,
            DiscountAmount = 0m,
            TaxPercent = 0m,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Dịch vụ", Quantity = 1, UnitPrice = 500000m }
            }
        };

        var result = await service.CreateInvoiceAsync(201, request);

        Assert.False(result.Success);
        Assert.Contains("Không thể lập hóa đơn cho ca khám ở trạng thái", result.Message);
    }

    [Fact]
    public async Task ProcessPayment_TwoStagePartialToFullPayment_ShouldTransitionCorrectly()
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
                new() { Description = "Trám răng thẩm mỹ", Quantity = 3, UnitPrice = 500000m }
            }
        };
        var createResult = await service.CreateInvoiceAsync(201, createReq);
        Assert.True(createResult.Success);
        var invoiceId = createResult.Data!.InvoiceId;
        Assert.Equal(1500000m, createResult.Data.TotalAmount);
        Assert.Equal("Unpaid", createResult.Data.Status);

        // Stage 1: Pay 500,000 deposit
        var pay1 = await service.ProcessPaymentAsync(201, new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 500000m,
            Method = "Cash",
            TransactionReference = "DEP-001"
        });
        Assert.True(pay1.Success);
        Assert.Equal("PartiallyPaid", pay1.Data!.Status);
        Assert.Equal(500000m, pay1.Data.PaidAmount);
        Assert.Equal(1000000m, pay1.Data.RemainingAmount);

        // Stage 2: Settle remaining 1,000,000
        var pay2 = await service.ProcessPaymentAsync(201, new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 1000000m,
            Method = "BankTransfer",
            TransactionReference = "SETTLE-002"
        });
        Assert.True(pay2.Success);
        Assert.Equal("Paid", pay2.Data!.Status);
        Assert.Equal(1500000m, pay2.Data.PaidAmount);
        Assert.Equal(0m, pay2.Data.RemainingAmount);

        // Clinical status of appointment should not be modified
        var appt = await db.Appointments.FindAsync(501L);
        Assert.Equal(AppointmentStatus.InService, appt!.Status);
    }

    [Fact]
    public async Task GetPatientInvoices_ShouldIsolateInvoicesByPatient()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        // Seed second patient & appointment
        var patient2User = new UserAccount
        {
            UserId = 102,
            Email = "patient2@clinic.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patient2Profile = new PatientProfile
        {
            UserId = 102,
            PatientCode = "PAT-002",
            FullName = "Trần Thị B",
            EmergencyContact = "0911223344",
            CreatedAt = DateTime.UtcNow
        };
        var appt2 = new Appointment
        {
            AppointmentId = 502,
            AppointmentCode = "APT-20261013-002",
            PatientUserId = 102,
            DepartmentId = 1,
            RequestedServiceId = 1,
            ReasonForVisit = "Khám răng định kỳ",
            BookingSource = "Online",
            Status = AppointmentStatus.InService,
            ScheduledStart = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.UserAccounts.Add(patient2User);
        db.PatientProfiles.Add(patient2Profile);
        db.Appointments.Add(appt2);
        await db.SaveChangesAsync();

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);

        // Invoice for Patient 101
        await service.CreateInvoiceAsync(201, new CreateInvoiceRequest
        {
            AppointmentId = 501,
            DiscountAmount = 0,
            TaxPercent = 0,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Dịch vụ bệnh nhân 1", Quantity = 1, UnitPrice = 400000m }
            }
        });

        // Invoice for Patient 102
        await service.CreateInvoiceAsync(201, new CreateInvoiceRequest
        {
            AppointmentId = 502,
            DiscountAmount = 0,
            TaxPercent = 0,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Dịch vụ bệnh nhân 2", Quantity = 1, UnitPrice = 750000m }
            }
        });

        // Query patient 101
        var invoices101 = await service.GetPatientInvoicesAsync(101);
        Assert.True(invoices101.Success);
        Assert.Single(invoices101.Data!);
        Assert.Equal(501L, invoices101.Data![0].AppointmentId);
        Assert.Equal(101L, invoices101.Data![0].PatientUserId);

        // Query patient 102
        var invoices102 = await service.GetPatientInvoicesAsync(102);
        Assert.True(invoices102.Success);
        Assert.Single(invoices102.Data!);
        Assert.Equal(502L, invoices102.Data![0].AppointmentId);
        Assert.Equal(102L, invoices102.Data![0].PatientUserId);
    }

    [Fact]
    public async Task ReturningPatient_AcrossThreeVisits_PreservesLongitudinalHistory_AndSeparatesInvoices()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        // Seed 2 additional appointments for the SAME patient (UserId = 101)
        var appt2 = new Appointment
        {
            AppointmentId = 502,
            AppointmentCode = "APT-20261013-002",
            PatientUserId = 101, // SAME PATIENT
            DepartmentId = 1,
            RequestedServiceId = 1,
            ReasonForVisit = "Tái khám lần 2 - Hàn răng",
            BookingSource = "Receptionist",
            Status = AppointmentStatus.Completed,
            ScheduledStart = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(7)
        };

        var appt3 = new Appointment
        {
            AppointmentId = 503,
            AppointmentCode = "APT-20261013-003",
            PatientUserId = 101, // SAME PATIENT
            DepartmentId = 1,
            RequestedServiceId = 1,
            ReasonForVisit = "Tái khám lần 3 - Kiểm tra định kỳ",
            BookingSource = "Patient",
            Status = AppointmentStatus.Completed,
            ScheduledStart = DateTime.UtcNow.AddDays(14),
            CreatedAt = DateTime.UtcNow.AddDays(14)
        };

        db.Appointments.AddRange(appt2, appt3);

        // Treatment plan linking items to sessions across visits
        var plan = new TreatmentPlan
        {
            TreatmentPlanId = 1,
            PatientUserId = 101,
            DepartmentId = 1,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var planItem1 = new TreatmentPlanItem { TreatmentPlanItemId = 1, TreatmentPlanId = 1, ToothNumber = "36", SequenceNo = 1, Status = "Completed" };
        var planItem2 = new TreatmentPlanItem { TreatmentPlanItemId = 2, TreatmentPlanId = 1, ToothNumber = "36", SequenceNo = 2, Status = "Completed" };
        var planItem3 = new TreatmentPlanItem { TreatmentPlanItemId = 3, TreatmentPlanId = 1, ToothNumber = "36", SequenceNo = 3, Status = "Completed" };
        db.TreatmentPlans.Add(plan);
        db.TreatmentPlanItems.AddRange(planItem1, planItem2, planItem3);

        var session1 = new TreatmentSession { TreatmentSessionId = 1, TreatmentPlanItemId = 1, AppointmentId = 501, SessionDate = DateTime.UtcNow, TreatmentResult = "Mở tủy" };
        var session2 = new TreatmentSession { TreatmentSessionId = 2, TreatmentPlanItemId = 2, AppointmentId = 502, SessionDate = DateTime.UtcNow.AddDays(7), TreatmentResult = "Trám tủy" };
        var session3 = new TreatmentSession { TreatmentSessionId = 3, TreatmentPlanItemId = 3, AppointmentId = 503, SessionDate = DateTime.UtcNow.AddDays(14), TreatmentResult = "Bọc sứ" };
        db.TreatmentSessions.AddRange(session1, session2, session3);
        await db.SaveChangesAsync();

        var service = new BillingService(db, _mockNotification.Object, _mockLogger.Object);

        // Visit 1: Invoice & Payment
        var inv1 = await service.CreateInvoiceAsync(201, new CreateInvoiceRequest
        {
            AppointmentId = 501,
            Items = new List<CreateInvoiceItemRequest> { new() { Description = "Buổi 1: Mở tủy", Quantity = 1, UnitPrice = 800000m } }
        });
        await service.ProcessPaymentAsync(201, new ProcessPaymentRequest { InvoiceId = inv1.Data!.InvoiceId, Amount = 800000m, Method = "Cash" });

        // Visit 2: Invoice & Payment
        var inv2 = await service.CreateInvoiceAsync(201, new CreateInvoiceRequest
        {
            AppointmentId = 502,
            Items = new List<CreateInvoiceItemRequest> { new() { Description = "Buổi 2: Trám bít ống tủy", Quantity = 1, UnitPrice = 700000m } }
        });
        await service.ProcessPaymentAsync(201, new ProcessPaymentRequest { InvoiceId = inv2.Data!.InvoiceId, Amount = 700000m, Method = "BankTransfer" });

        // Visit 3: Invoice & Payment
        var inv3 = await service.CreateInvoiceAsync(201, new CreateInvoiceRequest
        {
            AppointmentId = 503,
            Items = new List<CreateInvoiceItemRequest> { new() { Description = "Buổi 3: Lắp mão răng sứ", Quantity = 1, UnitPrice = 2500000m } }
        });
        await service.ProcessPaymentAsync(201, new ProcessPaymentRequest { InvoiceId = inv3.Data!.InvoiceId, Amount = 2500000m, Method = "CreditCard" });

        // Verify longitudinal preservation
        var patientInvoices = await service.GetPatientInvoicesAsync(101);
        Assert.True(patientInvoices.Success);
        Assert.Equal(3, patientInvoices.Data!.Count);

        // Invoices are separated and distinct
        var invoiceIds = patientInvoices.Data.Select(i => i.InvoiceId).Distinct().ToList();
        Assert.Equal(3, invoiceIds.Count);

        // All 3 invoices are Paid
        Assert.All(patientInvoices.Data, i => Assert.Equal("Paid", i.Status));

        // Amounts are preserved independently
        Assert.Contains(patientInvoices.Data, i => i.AppointmentId == 501 && i.TotalAmount == 800000m);
        Assert.Contains(patientInvoices.Data, i => i.AppointmentId == 502 && i.TotalAmount == 700000m);
        Assert.Contains(patientInvoices.Data, i => i.AppointmentId == 503 && i.TotalAmount == 2500000m);

        // Single patient profile
        var profileCount = await db.PatientProfiles.CountAsync(p => p.UserId == 101);
        Assert.Equal(1, profileCount);
    }
}
