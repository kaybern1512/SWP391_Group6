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
}
