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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

namespace DentalClinic.Tests.BillingTests;

public class InvoiceAndPaymentIntegrityTests
{
    private static DentalClinicDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedBillingFixtureAsync(DentalClinicDbContext db)
    {
        var now = DateTime.UtcNow;

        var clinic = new ClinicProfile
        {
            ClinicId = 1,
            ClinicName = "Nha Khoa DentalCare",
            Address = "123 Nguyễn Tri Phương",
            Hotline = "028 3822 9999",
            Email = "contact@dentalcare.vn",
            OpeningTime = new TimeOnly(8, 0),
            ClosingTime = new TimeOnly(20, 0)
        };
        db.ClinicProfiles.Add(clinic);

        var dept = new Department { DepartmentId = 1, Name = "Nha khoa Tổng quát", Status = "Active", CreatedAt = now };
        db.Departments.Add(dept);

        var srv = new DepartmentService { DepartmentServiceId = 1, DepartmentId = 1, ServiceCode = "SRV-01", ServiceName = "Trám răng", Price = 300000m, DurationMinutes = 30, Status = "Active", CreatedAt = now };
        db.DepartmentServices.Add(srv);

        var med = new Medicine { MedicineId = 10, MedicineCode = "MED-01", MedicineName = "Paracetamol 500mg", Unit = "Viên", UnitPrice = 5000m, StockQuantity = 100, IsActive = true, CreatedAt = now };
        db.Medicines.Add(med);

        // Staff Receptionist: 1001
        var uStaff = new UserAccount { UserId = 1001, Role = UserRole.Receptionist, Status = "Active", CreatedAt = now };
        var stStaff = new StaffProfile { UserId = 1001, FullName = "Thu Ngân Tiếp Nhận", EmploymentStatus = "Active", CreatedAt = now };
        db.UserAccounts.Add(uStaff);
        db.StaffProfiles.Add(stStaff);

        // Patient: 2001
        var uPat = new UserAccount { UserId = 2001, Role = UserRole.Patient, Status = "Active", CreatedAt = now };
        var pat = new PatientProfile { UserId = 2001, FullName = "Bệnh Nhân Thanh Toán", Gender = "Nam", CreatedAt = now };
        db.UserAccounts.Add(uPat);
        db.PatientProfiles.Add(pat);

        await db.SaveChangesAsync();
    }

    #region TEST GROUP 4A: Invoice Sources & XOR Source Link

    [Fact]
    public async Task TestGroup4A_ValidPerformedServiceAndDispensedMedicine_CreatesInvoice()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 100, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity { AppointmentServiceId = 1000, AppointmentId = 100, DepartmentId = 1, DepartmentServiceId = 1, Status = AppointmentStatus.Completed, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession { SessionId = 10000, AppointmentId = 100, AppointmentServiceId = 1000, PatientUserId = 2001, DentistUserId = 1001, SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow };
        var perf = new PerformedService { PerformedServiceId = 5001, TreatmentSessionId = 10000, AppointmentId = 100, AppointmentServiceId = 1000, DepartmentServiceId = 1, Quantity = 1, UnitPrice = 300000m, CreatedAt = DateTime.UtcNow };

        var presc = new Prescription { PrescriptionId = 7001, TreatmentSessionId = 10000, AppointmentId = 100, PatientUserId = 2001, DentistUserId = 1001, Status = "Dispensed", CreatedAt = DateTime.UtcNow };
        var pItem = new PrescriptionItem { PrescriptionItemId = 9001, PrescriptionId = 7001, AppointmentId = 100, MedicineId = 10, Dosage = "500mg", Frequency = "Ngày 2 lần", Quantity = 10, QuantityDispensed = 10, UnitPriceAtDispense = 5000m, CreatedAt = DateTime.UtcNow };

        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(perf);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        var req = new CreateInvoiceRequest
        {
            AppointmentId = 100,
            DiscountAmount = 0,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = 5001, Description = "Trám răng Composite", Quantity = 1, UnitPrice = 300000m },
                new CreateInvoiceItemRequest { PrescriptionItemId = 9001, Description = "Paracetamol 500mg", Quantity = 10, UnitPrice = 5000m }
            }
        };

        var result = await billingService.CreateInvoiceAsync(1001, req);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(350000m, result.Data.TotalAmount); // 300k + 50k
        Assert.Equal(350000m, result.Data.RemainingAmount);
        Assert.Equal("Unpaid", result.Data.Status);

        var savedItems = await db.InvoiceItems.Where(i => i.InvoiceId == result.Data.InvoiceId).ToListAsync();
        Assert.Equal(2, savedItems.Count);
        // XOR assertion: exactly one source is set on each row
        Assert.All(savedItems, item => Assert.True((item.PerformedServiceId != null && item.PrescriptionItemId == null) ||
                                                   (item.PerformedServiceId == null && item.PrescriptionItemId != null)));
    }

    [Fact]
    public async Task TestGroup4A_UndispensedMedicine_RejectedFromInvoice()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 101, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession { SessionId = 10001, AppointmentId = 101, AppointmentServiceId = 1, PatientUserId = 2001, DentistUserId = 1001, SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow };
        var presc = new Prescription { PrescriptionId = 7002, TreatmentSessionId = 10001, AppointmentId = 101, PatientUserId = 2001, DentistUserId = 1001, Status = "Active", CreatedAt = DateTime.UtcNow };
        // QuantityDispensed = 0 (Prescribed but NOT yet dispensed from pharmacy)
        var pItem = new PrescriptionItem { PrescriptionItemId = 9002, PrescriptionId = 7002, AppointmentId = 101, MedicineId = 10, Dosage = "500mg", Frequency = "Ngày 2 lần", Quantity = 10, QuantityDispensed = 0, CreatedAt = DateTime.UtcNow };

        db.Appointments.Add(appt);
        db.TreatmentSessions.Add(session);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        var req = new CreateInvoiceRequest
        {
            AppointmentId = 101,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PrescriptionItemId = 9002, Description = "Thuốc chưa cấp phát", Quantity = 10, UnitPrice = 5000m }
            }
        };

        var result = await billingService.CreateInvoiceAsync(1001, req);

        Assert.False(result.Success);
        Assert.Contains("chưa được cấp phát", result.Message);
    }

    [Fact]
    public async Task TestGroup4A_BothOrNeitherSourceLink_Rejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 102, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        // Case 1: Both source IDs provided
        var reqBoth = new CreateInvoiceRequest
        {
            AppointmentId = 102,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = 1, PrescriptionItemId = 2, Description = "Item vi phạm cả 2 nguồn", Quantity = 1, UnitPrice = 100000m }
            }
        };
        var resBoth = await billingService.CreateInvoiceAsync(1001, reqBoth);
        Assert.False(resBoth.Success);
        Assert.Contains("không thể vừa là dịch vụ vừa là thuốc", resBoth.Message);

        // Case 2: Neither source ID provided
        var reqNeither = new CreateInvoiceRequest
        {
            AppointmentId = 102,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = null, PrescriptionItemId = null, Description = "Item không có nguồn", Quantity = 1, UnitPrice = 100000m }
            }
        };
        var resNeither = await billingService.CreateInvoiceAsync(1001, reqNeither);
        Assert.False(resNeither.Success);
        Assert.Contains("phải liên kết với dịch vụ", resNeither.Message);
    }

    #endregion

    #region TEST GROUP 4B: Discounts & Amount Calculations

    [Fact]
    public async Task TestGroup4B_DiscountAndFinalAmount_Consistent()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 103, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var perf = new PerformedService { PerformedServiceId = 5003, TreatmentSessionId = 1, AppointmentId = 103, AppointmentServiceId = 1, DepartmentServiceId = 1, Quantity = 1, UnitPrice = 500000m, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        // 500k total with 100k discount => FinalAmount = 400k
        var req = new CreateInvoiceRequest
        {
            AppointmentId = 103,
            DiscountAmount = 100000m,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = 5003, Description = "Điều trị tủy", Quantity = 1, UnitPrice = 500000m }
            }
        };

        var result = await billingService.CreateInvoiceAsync(1001, req);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var inv = await db.Invoices.FindAsync(result.Data.InvoiceId);
        Assert.Equal(500000m, inv!.TotalAmount);
        Assert.Equal(100000m, inv.DiscountAmount);
        Assert.Equal(400000m, inv.FinalAmount);
        Assert.Equal(inv.TotalAmount - inv.DiscountAmount, inv.FinalAmount);
    }

    #endregion

    #region TEST GROUP 4C: Payments & Zero-Total Invoices

    [Fact]
    public async Task TestGroup4C_DepositAndFinalPayment_Lifecycle()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 104, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var perf = new PerformedService { PerformedServiceId = 5004, TreatmentSessionId = 1, AppointmentId = 104, AppointmentServiceId = 1, DepartmentServiceId = 1, Quantity = 1, UnitPrice = 1000000m, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        var createRes = await billingService.CreateInvoiceAsync(1001, new CreateInvoiceRequest
        {
            AppointmentId = 104,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = 5004, Description = "Niềng răng đợt 1", Quantity = 1, UnitPrice = 1000000m }
            }
        });

        long invId = createRes.Data!.InvoiceId;

        // 1. Partial payment: 400k deposit
        var pay1 = await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest
        {
            InvoiceId = invId,
            Amount = 400000m,
            Method = "Cash"
        });
        Assert.True(pay1.Success);
        Assert.Equal("PartiallyPaid", pay1.Data!.Status);
        Assert.Equal(600000m, pay1.Data.RemainingAmount);

        // 2. Final payment: 600k
        var pay2 = await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest
        {
            InvoiceId = invId,
            Amount = 600000m,
            Method = "BankTransfer"
        });
        Assert.True(pay2.Success);
        Assert.Equal("Paid", pay2.Data!.Status);
        Assert.Equal(0m, pay2.Data.RemainingAmount);

        // 3. Attempt to pay again should be rejected
        var pay3 = await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest
        {
            InvoiceId = invId,
            Amount = 100000m,
            Method = "Cash"
        });
        Assert.False(pay3.Success);
        Assert.Contains("đã được thanh toán", pay3.Message);
    }

    [Fact]
    public async Task TestGroup4C_ZeroTotalInvoice_AutoMarkedPaidWithoutFakePaymentRow()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 105, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var perf = new PerformedService { PerformedServiceId = 5005, TreatmentSessionId = 1, AppointmentId = 105, AppointmentServiceId = 1, DepartmentServiceId = 1, Quantity = 1, UnitPrice = 100000m, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        // 100k with 100k discount (100% free) => FinalAmount = 0
        var result = await billingService.CreateInvoiceAsync(1001, new CreateInvoiceRequest
        {
            AppointmentId = 105,
            DiscountAmount = 100000m,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = 5005, Description = "Khám miễn phí", Quantity = 1, UnitPrice = 100000m }
            }
        });

        Assert.True(result.Success);
        Assert.Equal("Paid", result.Data!.Status);
        Assert.Equal(0m, result.Data.TotalAmount);

        // Verify NO fake Payment row was added! (CK_Payments_Amount requires Amount > 0)
        var paymentCount = await db.Payments.CountAsync(p => p.InvoiceId == result.Data.InvoiceId);
        Assert.Equal(0, paymentCount);
    }

    [Fact]
    public async Task TestGroup4C_InvalidOrNegativePaymentAmount_Rejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 106, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var perf = new PerformedService { PerformedServiceId = 5006, TreatmentSessionId = 1, AppointmentId = 106, AppointmentServiceId = 1, DepartmentServiceId = 1, Quantity = 1, UnitPrice = 200000m, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        var createRes = await billingService.CreateInvoiceAsync(1001, new CreateInvoiceRequest
        {
            AppointmentId = 106,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = 5006, Description = "Trám răng", Quantity = 1, UnitPrice = 200000m }
            }
        });

        long invId = createRes.Data!.InvoiceId;

        // Negative payment
        var payNeg = await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest
        {
            InvoiceId = invId,
            Amount = -50000m,
            Method = "Cash"
        });
        Assert.False(payNeg.Success);
        Assert.Contains("lớn hơn 0", payNeg.Message);

        // Zero payment
        var payZero = await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest
        {
            InvoiceId = invId,
            Amount = 0m,
            Method = "Cash"
        });
        Assert.False(payZero.Success);
        Assert.Contains("lớn hơn 0", payZero.Message);
    }

    #endregion

    #region TEST GROUP 4D: Refund Lifecycle, Net Payment & Financial Integrity

    [Fact]
    public async Task Test4_1_PartialRefund_RecalculatesNetPaidAndStatus_PartiallyPaid()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 110, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity { AppointmentServiceId = 1010, AppointmentId = 110, DepartmentId = 1, DepartmentServiceId = 1, Status = AppointmentStatus.Completed, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession { SessionId = 10010, AppointmentId = 110, AppointmentServiceId = 1010, PatientUserId = 2001, DentistUserId = 1001, SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow };
        var perf = new PerformedService { PerformedServiceId = 5010, TreatmentSessionId = 10010, AppointmentId = 110, AppointmentServiceId = 1010, DepartmentServiceId = 1, Quantity = 1, UnitPrice = 1000000m, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        var invRes = await billingService.CreateInvoiceAsync(1001, new CreateInvoiceRequest
        {
            AppointmentId = 110,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = 5010, Description = "Điều trị tủy", Quantity = 1, UnitPrice = 1000000m }
            }
        });
        long invId = invRes.Data!.InvoiceId;

        // Pay full 1,000,000
        var payRes = await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest
        {
            InvoiceId = invId,
            Amount = 1000000m,
            Method = "BankTransfer",
            TransactionReference = "TXN-FULL-01"
        });
        Assert.True(payRes.Success);
        Assert.Equal("Paid", payRes.Data!.Status);
        Assert.Equal(1000000m, payRes.Data!.PaidAmount);
        Assert.Equal(0m, payRes.Data!.RemainingAmount);

        // Refund 300,000 (Partial)
        var refRes = await billingService.ProcessRefundAsync(1001, new ProcessRefundRequest
        {
            InvoiceId = invId,
            Amount = 300000m,
            Method = "BankTransfer",
            Reason = "Hoàn lại tiền thừa do chiết khấu bổ sung"
        });
        Assert.True(refRes.Success);
        Assert.Equal("PartiallyPaid", refRes.Data!.Status);
        Assert.Equal(700000m, refRes.Data!.PaidAmount);
        Assert.Equal(300000m, refRes.Data!.RemainingAmount);
    }

    [Fact]
    public async Task Test4_2_FullRefund_SetsStatusToUnpaid_AllowsSubsequentPayment()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 111, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity { AppointmentServiceId = 1011, AppointmentId = 111, DepartmentId = 1, DepartmentServiceId = 1, Status = AppointmentStatus.Completed, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession { SessionId = 10011, AppointmentId = 111, AppointmentServiceId = 1011, PatientUserId = 2001, DentistUserId = 1001, SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow };
        var perf = new PerformedService { PerformedServiceId = 5011, TreatmentSessionId = 10011, AppointmentId = 111, AppointmentServiceId = 1011, DepartmentServiceId = 1, Quantity = 1, UnitPrice = 500000m, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        var invRes = await billingService.CreateInvoiceAsync(1001, new CreateInvoiceRequest
        {
            AppointmentId = 111,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = 5011, Description = "Nhổ răng", Quantity = 1, UnitPrice = 500000m }
            }
        });
        long invId = invRes.Data!.InvoiceId;

        // Pay full 500k
        await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest
        {
            InvoiceId = invId,
            Amount = 500000m,
            Method = "Cash",
            TransactionReference = "TXN-CASH-01"
        });

        // Refund full 500k
        var refRes = await billingService.ProcessRefundAsync(1001, new ProcessRefundRequest
        {
            InvoiceId = invId,
            Amount = 500000m,
            Method = "Cash",
            Reason = "Khách muốn đổi hình thức thanh toán thẻ"
        });
        Assert.True(refRes.Success);
        Assert.Equal("Unpaid", refRes.Data!.Status);
        Assert.Equal(0m, refRes.Data!.PaidAmount);
        Assert.Equal(500000m, refRes.Data!.RemainingAmount);

        // Crucial test: subsequent payment CAN be made because status is Unpaid, not Cancelled
        var rePayRes = await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest
        {
            InvoiceId = invId,
            Amount = 500000m,
            Method = "CreditCard",
            TransactionReference = "TXN-POS-02"
        });
        Assert.True(rePayRes.Success);
        Assert.Equal("Paid", rePayRes.Data!.Status);
        Assert.Equal(500000m, rePayRes.Data!.PaidAmount);
        Assert.Equal(0m, rePayRes.Data!.RemainingAmount);
    }

    [Fact]
    public async Task Test4_3_RefundOnUnpaidInvoice_Rejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 112, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity { AppointmentServiceId = 1012, AppointmentId = 112, DepartmentId = 1, DepartmentServiceId = 1, Status = AppointmentStatus.Completed, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession { SessionId = 10012, AppointmentId = 112, AppointmentServiceId = 1012, PatientUserId = 2001, DentistUserId = 1001, SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow };
        var perf = new PerformedService { PerformedServiceId = 5012, TreatmentSessionId = 10012, AppointmentId = 112, AppointmentServiceId = 1012, DepartmentServiceId = 1, Quantity = 1, UnitPrice = 200000m, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        var invRes = await billingService.CreateInvoiceAsync(1001, new CreateInvoiceRequest
        {
            AppointmentId = 112,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = 5012, Description = "Cạo vôi răng", Quantity = 1, UnitPrice = 200000m }
            }
        });
        long invId = invRes.Data!.InvoiceId;

        // Attempt refund without prior payment
        var refRes = await billingService.ProcessRefundAsync(1001, new ProcessRefundRequest
        {
            InvoiceId = invId,
            Amount = 100000m,
            Method = "Cash",
            Reason = "Thử hoàn tiền khi chưa thanh toán"
        });
        Assert.False(refRes.Success);
        Assert.Contains("chưa có khoản thanh toán thực tế", refRes.Message);
    }

    [Fact]
    public async Task Test4_4_RefundExceedingNetPaid_Rejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 113, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity { AppointmentServiceId = 1013, AppointmentId = 113, DepartmentId = 1, DepartmentServiceId = 1, Status = AppointmentStatus.Completed, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession { SessionId = 10013, AppointmentId = 113, AppointmentServiceId = 1013, PatientUserId = 2001, DentistUserId = 1001, SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow };
        var perf = new PerformedService { PerformedServiceId = 5013, TreatmentSessionId = 10013, AppointmentId = 113, AppointmentServiceId = 1013, DepartmentServiceId = 1, Quantity = 1, UnitPrice = 300000m, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        var invRes = await billingService.CreateInvoiceAsync(1001, new CreateInvoiceRequest
        {
            AppointmentId = 113,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = 5013, Description = "Trám răng", Quantity = 1, UnitPrice = 300000m }
            }
        });
        long invId = invRes.Data!.InvoiceId;

        // Pay deposit 100,000
        await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest
        {
            InvoiceId = invId,
            Amount = 100000m,
            Method = "Cash"
        });

        // Attempt refund 200,000 (exceeds 100k net paid)
        var refRes = await billingService.ProcessRefundAsync(1001, new ProcessRefundRequest
        {
            InvoiceId = invId,
            Amount = 200000m,
            Method = "Cash",
            Reason = "Hoàn vượt số tiền đã thu"
        });
        Assert.False(refRes.Success);
        Assert.Contains("vượt quá số tiền thực tế có thể hoàn", refRes.Message);
    }

    [Fact]
    public async Task Test4_5_DuplicateRefundTransactionReference_Rejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 114, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity { AppointmentServiceId = 1014, AppointmentId = 114, DepartmentId = 1, DepartmentServiceId = 1, Status = AppointmentStatus.Completed, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession { SessionId = 10014, AppointmentId = 114, AppointmentServiceId = 1014, PatientUserId = 2001, DentistUserId = 1001, SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow };
        var perf = new PerformedService { PerformedServiceId = 5014, TreatmentSessionId = 10014, AppointmentId = 114, AppointmentServiceId = 1014, DepartmentServiceId = 1, Quantity = 1, UnitPrice = 800000m, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        var invRes = await billingService.CreateInvoiceAsync(1001, new CreateInvoiceRequest
        {
            AppointmentId = 114,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = 5014, Description = "Tẩy trắng răng", Quantity = 1, UnitPrice = 800000m }
            }
        });
        long invId = invRes.Data!.InvoiceId;

        // Pay full 800k
        await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest
        {
            InvoiceId = invId,
            Amount = 800000m,
            Method = "BankTransfer",
            TransactionReference = "TXN-800"
        });

        // Refund 200k with reference REF-DUPLICATE
        var refRes1 = await billingService.ProcessRefundAsync(1001, new ProcessRefundRequest
        {
            InvoiceId = invId,
            Amount = 200000m,
            Method = "BankTransfer",
            TransactionReference = "REF-DUPLICATE",
            Reason = "Đợt 1"
        });
        Assert.True(refRes1.Success);

        // Attempt second refund with identical reference
        var refRes2 = await billingService.ProcessRefundAsync(1001, new ProcessRefundRequest
        {
            InvoiceId = invId,
            Amount = 100000m,
            Method = "BankTransfer",
            TransactionReference = "REF-DUPLICATE",
            Reason = "Đợt 2 lặp mã"
        });
        Assert.False(refRes2.Success);
        Assert.Contains("đã được ghi nhận trước đó", refRes2.Message);
    }

    [Fact]
    public async Task Test4_6_RefundAfterDeposit_RecalculatesBalanceAccurately()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 115, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity { AppointmentServiceId = 1015, AppointmentId = 115, DepartmentId = 1, DepartmentServiceId = 1, Status = AppointmentStatus.Completed, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession { SessionId = 10015, AppointmentId = 115, AppointmentServiceId = 1015, PatientUserId = 2001, DentistUserId = 1001, SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow };
        var perf = new PerformedService { PerformedServiceId = 5015, TreatmentSessionId = 10015, AppointmentId = 115, AppointmentServiceId = 1015, DepartmentServiceId = 1, Quantity = 1, UnitPrice = 1200000m, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        var invRes = await billingService.CreateInvoiceAsync(1001, new CreateInvoiceRequest
        {
            AppointmentId = 115,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = 5015, Description = "Làm răng sứ", Quantity = 1, UnitPrice = 1200000m }
            }
        });
        long invId = invRes.Data!.InvoiceId;

        // Deposit 400,000 -> PartiallyPaid
        var depRes = await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest
        {
            InvoiceId = invId,
            Amount = 400000m,
            Method = "Cash"
        });
        Assert.True(depRes.Success);
        Assert.Equal("PartiallyPaid", depRes.Data!.Status);
        Assert.Equal(400000m, depRes.Data!.PaidAmount);
        Assert.Equal(800000m, depRes.Data!.RemainingAmount);

        // Refund 150,000 of the deposit -> Still PartiallyPaid
        var refRes = await billingService.ProcessRefundAsync(1001, new ProcessRefundRequest
        {
            InvoiceId = invId,
            Amount = 150000m,
            Method = "Cash",
            Reason = "Hoàn bớt cọc"
        });
        Assert.True(refRes.Success);
        Assert.Equal("PartiallyPaid", refRes.Data!.Status);
        Assert.Equal(250000m, refRes.Data!.PaidAmount);
        Assert.Equal(950000m, refRes.Data!.RemainingAmount);

        // Now pay remaining 950,000 -> Becomes Paid
        var finalPay = await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest
        {
            InvoiceId = invId,
            Amount = 950000m,
            Method = "Cash"
        });
        Assert.True(finalPay.Success);
        Assert.Equal("Paid", finalPay.Data!.Status);
        Assert.Equal(1200000m, finalPay.Data!.PaidAmount);
        Assert.Equal(0m, finalPay.Data!.RemainingAmount);
    }

    [Fact]
    public async Task Test4_7_RefundAfterMultiplePayments_CalculatesNetCorrectly()
    {
        var db = CreateInMemoryDbContext();
        await SeedBillingFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 116, PatientUserId = 2001, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity { AppointmentServiceId = 1016, AppointmentId = 116, DepartmentId = 1, DepartmentServiceId = 1, Status = AppointmentStatus.Completed, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession { SessionId = 10016, AppointmentId = 116, AppointmentServiceId = 1016, PatientUserId = 2001, DentistUserId = 1001, SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow };
        var perf = new PerformedService { PerformedServiceId = 5016, TreatmentSessionId = 10016, AppointmentId = 116, AppointmentServiceId = 1016, DepartmentServiceId = 1, Quantity = 1, UnitPrice = 1000000m, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        var invRes = await billingService.CreateInvoiceAsync(1001, new CreateInvoiceRequest
        {
            AppointmentId = 116,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { PerformedServiceId = 5016, Description = "Chỉnh nha đợt 1", Quantity = 1, UnitPrice = 1000000m }
            }
        });
        long invId = invRes.Data!.InvoiceId;

        // Payment 1: 300,000 (Cash)
        await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest { InvoiceId = invId, Amount = 300000m, Method = "Cash" });
        // Payment 2: 400,000 (BankTransfer)
        await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest { InvoiceId = invId, Amount = 400000m, Method = "BankTransfer" });
        // Payment 3: 300,000 (CreditCard) -> Paid in full
        await billingService.ProcessPaymentAsync(1001, new ProcessPaymentRequest { InvoiceId = invId, Amount = 300000m, Method = "CreditCard" });

        // Total payments = 1,000,000
        var invAfterPaid = await billingService.GetInvoiceByIdAsync(invId);
        Assert.Equal(1000000m, invAfterPaid.Data!.PaidAmount);
        Assert.Equal("Paid", invAfterPaid.Data!.Status);

        // Refund 1: 200,000
        await billingService.ProcessRefundAsync(1001, new ProcessRefundRequest { InvoiceId = invId, Amount = 200000m, Method = "Cash", Reason = "Đợt 1" });
        // Refund 2: 500,000
        var refRes2 = await billingService.ProcessRefundAsync(1001, new ProcessRefundRequest { InvoiceId = invId, Amount = 500000m, Method = "BankTransfer", Reason = "Đợt 2" });

        // Net paid = 1,000,000 - 700,000 = 300,000
        Assert.True(refRes2.Success);
        Assert.Equal("PartiallyPaid", refRes2.Data!.Status);
        Assert.Equal(300000m, refRes2.Data!.PaidAmount);
        Assert.Equal(700000m, refRes2.Data!.RemainingAmount);
    }

    #endregion
}
