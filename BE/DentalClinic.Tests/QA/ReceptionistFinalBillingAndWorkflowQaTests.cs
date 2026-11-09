using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.Blog.DTOs;
using DentalClinic.Application.Features.Booking.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.QA;

public class ReceptionistFinalBillingAndWorkflowQaTests
{
    private readonly Mock<INotificationService> _mockNotification = new();
    private readonly Mock<ILogger<BillingService>> _mockBillingLogger = new();
    private readonly Mock<ILogger<AppointmentService>> _mockApptLogger = new();
    private readonly Mock<ILogger<BlogService>> _mockBlogLogger = new();
    private readonly Mock<IAuditLogService> _mockAudit = new();
    private readonly Mock<IMediaStorageService> _mockStorage = new();

    private DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task<(long PatientId, long StaffId, long DentistId, long DeptId, long ServiceId)> SeedBaseDataAsync(DentalClinicDbContext db)
    {
        // 1. Patient
        var patientUser = new UserAccount
        {
            UserId = 101,
            Email = "patient101@test.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = 101,
            PatientCode = "PAT-101",
            FullName = "Nguyễn Văn An",
            User = patientUser
        };
        db.UserAccounts.Add(patientUser);
        db.PatientProfiles.Add(patientProfile);

        // 2. Staff (Receptionist)
        var staffUser = new UserAccount
        {
            UserId = 201,
            Email = "receptionist201@test.com",
            Role = UserRole.Receptionist,
            Status = AccountStatus.Active
        };
        var staffProfile = new StaffProfile
        {
            UserId = 201,
            EmployeeCode = "EMP-REC-201",
            FullName = "Lê Thị Mai",
            EmploymentStatus = "Active",
            User = staffUser
        };
        db.UserAccounts.Add(staffUser);
        db.StaffProfiles.Add(staffProfile);

        // 3. Department
        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Tổng Quát",
            Status = "Active"
        };
        db.Departments.Add(dept);

        for (byte d = 1; d <= 7; d++)
        {
            db.DepartmentWorkSchedules.Add(new DepartmentWorkSchedule
            {
                DepartmentScheduleId = d,
                DepartmentId = 1,
                DayOfWeek = d,
                StartTime = new TimeOnly(6, 0),
                EndTime = new TimeOnly(23, 0),
                IsActive = true
            });
        }

        // 4. Dentist
        var dentistUser = new UserAccount
        {
            UserId = 301,
            Email = "dentist301@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var dentistStaff = new StaffProfile
        {
            UserId = 301,
            EmployeeCode = "EMP-DEN-301",
            FullName = "Bác Sĩ Trần Hùng",
            EmploymentStatus = "Active",
            User = dentistUser
        };
        var dentistProfile = new DentistProfile
        {
            UserId = 301,
            LicenseNumber = "LIC-301",
            User = dentistStaff
        };
        db.UserAccounts.Add(dentistUser);
        db.StaffProfiles.Add(dentistStaff);
        db.DentistProfiles.Add(dentistProfile);
        db.DentistDepartments.Add(new DentistDepartment
        {
            DentistUserId = 301,
            DepartmentId = 1,
            Status = "Active"
        });

        // 5. Room & Chair
        var room = new Room
        {
            RoomId = 10,
            DepartmentId = 1,
            RoomCode = "ROOM-101",
            RoomName = "Phòng Khám Tổng Quát",
            IsExclusive = false,
            Status = "Active"
        };
        var chair1 = new DentalChair
        {
            DentalChairId = 101,
            RoomId = 10,
            ChairCode = "CHAIR-1",
            Status = "Active"
        };
        var chair2 = new DentalChair
        {
            DentalChairId = 102,
            RoomId = 10,
            ChairCode = "CHAIR-2",
            Status = "Active"
        };
        db.Rooms.Add(room);
        db.DentalChairs.AddRange(chair1, chair2);

        // 6. Service
        var service = new DepartmentService
        {
            DepartmentServiceId = 1,
            DepartmentId = 1,
            ServiceName = "Khám Chuyên Khoa",
            DurationMinutes = 30,
            Price = 189500,
            Status = "Active",
            ServiceType = "Examination",
            MinLeadTimeHours = 2
        };
        db.DepartmentServices.Add(service);

        // 7. Dentist shift today & tomorrow
        var today = ClinicTimeHelper.TodayDateOnly;
        db.DentistWorkSchedules.AddRange(
            new DentistWorkSchedule
            {
                DentistScheduleId = 1,
                DentistUserId = 301,
                DepartmentId = 1,
                WorkDate = today,
                StartTime = new TimeOnly(6, 0),
                EndTime = new TimeOnly(23, 0),
                Status = "Scheduled"
            },
            new DentistWorkSchedule
            {
                DentistScheduleId = 2,
                DentistUserId = 301,
                DepartmentId = 1,
                WorkDate = today.AddDays(1),
                StartTime = new TimeOnly(6, 0),
                EndTime = new TimeOnly(23, 0),
                Status = "Scheduled"
            }
        );

        await db.SaveChangesAsync();
        return (101, 201, 301, 1, 1);
    }

    // ==========================================
    // ƯU TIÊN 1 – BILLING & VIETQR
    // ==========================================

    [Theory]
    [InlineData(189000)]
    [InlineData(189500)]
    [InlineData(200500)]
    [InlineData(99999)]
    public async Task P1_01_MonetaryPrecision_ExactVndAmounts_PreservedWithoutRoundingToThousand(decimal testPrice)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var appt = new Appointment
        {
            AppointmentCode = "APP-TEST-" + Guid.NewGuid().ToString("N")[..6],
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            AssignedDentistUserId = dentistId,
            RoomId = 10,
            DentalChairId = 101,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Khám răng",
            Status = AppointmentStatus.InService,
            ScheduledStart = ClinicTimeHelper.Today.AddHours(10),
            ScheduledEnd = ClinicTimeHelper.Today.AddHours(10).AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, _mockNotification.Object, _mockBillingLogger.Object);

        var createReq = new CreateInvoiceRequest
        {
            AppointmentId = appt.AppointmentId,
            DiscountAmount = 0,
            TaxPercent = 0,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest
                {
                    Description = "Dịch vụ thử nghiệm độ chính xác",
                    Quantity = 1,
                    UnitPrice = testPrice
                }
            }
        };

        var result = await billingService.CreateInvoiceAsync(staffId, createReq);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(testPrice, result.Data.TotalAmount);
        Assert.Equal(testPrice, result.Data.RemainingAmount);
        Assert.Equal(0, result.Data.PaidAmount);
        Assert.Equal("Unpaid", result.Data.Status);
    }

    [Fact]
    public async Task P1_02_InvoiceCreation_InitialStatusIsUnpaid_NotAutoMarkedPaid()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var appt = new Appointment
        {
            AppointmentCode = "APP-TEST-" + Guid.NewGuid().ToString("N")[..6],
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            AssignedDentistUserId = dentistId,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Khám răng",
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, _mockNotification.Object, _mockBillingLogger.Object);

        var invoiceRes = await billingService.CreateInvoiceAsync(staffId, new CreateInvoiceRequest
        {
            AppointmentId = appt.AppointmentId,
            DiscountAmount = 0,
            TaxPercent = 0,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { Description = "Khám nha khoa", Quantity = 1, UnitPrice = 200500 }
            }
        });

        Assert.True(invoiceRes.Success);
        var invoice = invoiceRes.Data!;

        // Fetch fresh invoice from DB: Status must be Unpaid
        var freshInvoice = await db.Invoices.FindAsync(invoice.InvoiceId);
        Assert.NotNull(freshInvoice);
        Assert.Equal("Unpaid", freshInvoice.Status);
        Assert.NotEqual("Paid", freshInvoice.Status);
        Assert.Equal(200500, freshInvoice.TotalAmount);
    }

    [Fact]
    public async Task P1_03_BillingPreparation_PrefersDentistPerformedServices_OverRequestedService()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        // Service performed by dentist (Nhổ răng 500k) vs Requested Service (Khám 189.5k)
        var extractionService = new DepartmentService
        {
            DepartmentServiceId = 99,
            DepartmentId = deptId,
            ServiceName = "Nhổ Răng Khôn Số 8",
            Price = 500000,
            Status = "Active"
        };
        db.DepartmentServices.Add(extractionService);

        var plan = new TreatmentPlan
        {
            TreatmentPlanId = 1,
            PatientUserId = patientId,
            DepartmentId = deptId,
            CreatedByDentistUserId = dentistId,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var planItem = new TreatmentPlanItem
        {
            TreatmentPlanItemId = 1,
            TreatmentPlanId = 1,
            DepartmentServiceId = 99,
            SequenceNo = 1,
            Status = "Completed"
        };
        db.TreatmentPlans.Add(plan);
        db.TreatmentPlanItems.Add(planItem);

        var appt = new Appointment
        {
            AppointmentCode = "APP-TEST-" + Guid.NewGuid().ToString("N")[..6],
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId, // 189,500
            AssignedDentistUserId = dentistId,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Đau răng số 8",
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        // Dentist recorded treatment session with actual performed service
        var session = new TreatmentSession
        {
            TreatmentSessionId = 501,
            TreatmentPlanItemId = 1,
            AppointmentId = appt.AppointmentId,
            DentistUserId = dentistId,
            SessionDate = DateTime.UtcNow,
            ClinicalNote = "Đã thực hiện nhổ răng khôn"
        };
        var performed = new PerformedService
        {
            PerformedServiceId = 701,
            TreatmentSessionId = 501,
            DepartmentServiceId = 99,
            Quantity = 1,
            UnitPrice = 500000,
            Amount = 500000,
            DepartmentService = extractionService
        };
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(performed);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, _mockNotification.Object, _mockBillingLogger.Object);

        var prepResult = await billingService.GetAppointmentBillingPreparationAsync(appt.AppointmentId);

        Assert.True(prepResult.Success);
        Assert.NotNull(prepResult.Data);

        // MUST contain performed service (500k), and NOT default to requested service (189.5k)
        Assert.Single(prepResult.Data.SuggestedItems);
        var item = prepResult.Data.SuggestedItems.First();
        Assert.Equal("Nhổ Răng Khôn Số 8", item.Description);
        Assert.Equal(500000, item.UnitPrice);
    }

    [Fact]
    public async Task P1_04_BillingPreparation_OnlyIncludesDispensedMedicines()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var appt = new Appointment
        {
            AppointmentCode = "APP-TEST-" + Guid.NewGuid().ToString("N")[..6],
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            AssignedDentistUserId = dentistId,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Khám & cấp thuốc",
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        // Medicines
        var med1 = new DentalClinic.Infrastructure.Persistence.Entities.Medicine { MedicineId = 1, MedicineCode = "MED-001", MedicineName = "Amoxicillin 500mg", Unit = "Viên" };
        var batch1 = new MedicineBatch { MedicineBatchId = 10, MedicineId = 1, BatchNumber = "BATCH-AMOX-01", ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddYears(1)) };
        db.Medicines.Add(med1);
        db.MedicineBatches.Add(batch1);

        // Dispensed dispensation
        var dispSuccess = new MedicineDispensation
        {
            DispensationId = 1001,
            AppointmentId = appt.AppointmentId,
            Status = "Dispensed",
            DispensedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        var dispItem1 = new MedicineDispensationItem
        {
            DispensationItemId = 2001,
            DispensationId = 1001,
            MedicineId = 1,
            MedicineBatchId = 10,
            QuantityDispensed = 10,
            UnitPriceSnapshot = 5000,
            Medicine = med1,
            MedicineBatch = batch1
        };
        dispSuccess.DispensationItems.Add(dispItem1);
        db.MedicineDispensations.Add(dispSuccess);
        db.MedicineDispensationItems.Add(dispItem1);

        // Pending dispensation (not yet dispensed by pharmacy)
        var dispPending = new MedicineDispensation
        {
            DispensationId = 1002,
            AppointmentId = appt.AppointmentId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        var dispItem2 = new MedicineDispensationItem
        {
            DispensationItemId = 2002,
            DispensationId = 1002,
            MedicineId = 1,
            MedicineBatchId = 10,
            QuantityDispensed = 5,
            UnitPriceSnapshot = 5000,
            Medicine = med1,
            MedicineBatch = batch1
        };
        dispPending.DispensationItems.Add(dispItem2);
        db.MedicineDispensations.Add(dispPending);
        db.MedicineDispensationItems.Add(dispItem2);

        await db.SaveChangesAsync();

        var billingService = new BillingService(db, _mockNotification.Object, _mockBillingLogger.Object);

        var prepResult = await billingService.GetAppointmentBillingPreparationAsync(appt.AppointmentId);

        Assert.True(prepResult.Success);
        Assert.NotNull(prepResult.Data);

        var medItems = prepResult.Data.SuggestedItems.Where(i => i.Description.StartsWith("[Thuốc]")).ToList();
        Assert.Single(medItems);
        Assert.Equal(10, medItems.First().Quantity); // ONLY 10 from dispensed batch, not 15!
        Assert.Contains("BATCH-AMOX-01", medItems.First().Description);
    }

    [Fact]
    public async Task P1_05_Deposit_PartialPayment_And_Balance_Tracking()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var appt = new Appointment
        {
            AppointmentCode = "APP-TEST-" + Guid.NewGuid().ToString("N")[..6],
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            AssignedDentistUserId = dentistId,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Điều trị cấy ghép",
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, _mockNotification.Object, _mockBillingLogger.Object);

        // Invoice total = 200500 VND
        var invRes = await billingService.CreateInvoiceAsync(staffId, new CreateInvoiceRequest
        {
            AppointmentId = appt.AppointmentId,
            DiscountAmount = 0,
            TaxPercent = 0,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { Description = "Gói điều trị răng", Quantity = 1, UnitPrice = 200500 }
            }
        });
        Assert.True(invRes.Success);
        var invoiceId = invRes.Data!.InvoiceId;

        // Step 1: Deposit 50000 VND
        var pay1 = await billingService.ProcessPaymentAsync(staffId, new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 50000,
            Method = "Cash",
            TransactionReference = "DEP-001"
        });
        Assert.True(pay1.Success);
        Assert.Equal("PartiallyPaid", pay1.Data!.Status);
        Assert.Equal(50000, pay1.Data.PaidAmount);
        Assert.Equal(150500, pay1.Data.RemainingAmount);

        // Step 2: Pay remaining 150500 VND
        var pay2 = await billingService.ProcessPaymentAsync(staffId, new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 150500,
            Method = "BankTransfer",
            TransactionReference = "TXN-002"
        });
        Assert.True(pay2.Success);
        Assert.Equal("Paid", pay2.Data!.Status);
        Assert.Equal(200500, pay2.Data.PaidAmount);
        Assert.Equal(0, pay2.Data.RemainingAmount);

        // Step 3: Extra payment rejected (already Paid)
        var pay3 = await billingService.ProcessPaymentAsync(staffId, new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 10000,
            Method = "Cash"
        });
        Assert.False(pay3.Success);
        Assert.NotNull(pay3.Message);
        Assert.Contains("đã được thanh toán", pay3.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task P1_06_DuplicatePaymentTransactionReference_StrictlyRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var appt = new Appointment
        {
            AppointmentCode = "APP-TEST-" + Guid.NewGuid().ToString("N")[..6],
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            AssignedDentistUserId = dentistId,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Khám răng",
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, _mockNotification.Object, _mockBillingLogger.Object);

        var invRes = await billingService.CreateInvoiceAsync(staffId, new CreateInvoiceRequest
        {
            AppointmentId = appt.AppointmentId,
            DiscountAmount = 0,
            TaxPercent = 0,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { Description = "Khám", Quantity = 1, UnitPrice = 189000 }
            }
        });
        Assert.True(invRes.Success);
        var invoiceId = invRes.Data!.InvoiceId;

        // Payment 1 with reference "VNPAY-12345"
        var pay1 = await billingService.ProcessPaymentAsync(staffId, new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 100000,
            Method = "BankTransfer",
            TransactionReference = "VNPAY-12345"
        });
        Assert.True(pay1.Success);

        // Payment 2 duplicate reference "VNPAY-12345"
        var pay2 = await billingService.ProcessPaymentAsync(staffId, new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Amount = 89000,
            Method = "BankTransfer",
            TransactionReference = "VNPAY-12345"
        });
        Assert.False(pay2.Success);
        Assert.Contains("đã được ghi nhận trước đó", pay2.Message);
    }

    // ==========================================
    // ƯU TIÊN 2 – APPOINTMENT WORKFLOW
    // ==========================================

    [Fact]
    public async Task P2_01_OnlineBooking_To_ReceptionistConfirmed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var apptService = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var tomorrow = ClinicTimeHelper.Today.AddDays(1);

        // 1. Patient online booking
        var bookRes = await apptService.CreatePatientAppointmentAsync(patientId, new CreateAppointmentRequest
        {
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            ScheduledStart = tomorrow.AddHours(9),
            ReasonForVisit = "Đau buốt răng"
        });
        Assert.True(bookRes.Success);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, bookRes.Data!.Status);
        Assert.Null(bookRes.Data.QueueNumber);

        // 2. Receptionist Confirms with Doctor, Room, Chair
        var confRes = await apptService.ManagerConfirmAsync(bookRes.Data.AppointmentId, staffId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = dentistId,
            RoomId = 10,
            DentalChairId = 101,
            ScheduledStart = tomorrow.AddHours(9),
            ScheduledEnd = tomorrow.AddHours(9).AddMinutes(30)
        });
        Assert.True(confRes.Success, confRes.Message);
        Assert.Equal(AppointmentStatus.Confirmed, confRes.Data!.Status);
        Assert.Equal(101, confRes.Data.DentalChairId);
        Assert.NotNull(confRes.Data.QueueNumber);
        Assert.StartsWith("Q", confRes.Data.QueueNumber);
    }

    [Fact]
    public async Task P2_02_WalkIn_DirectToConfirmed_WithAssignedResources()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var apptService = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var nowClinic = ClinicTimeHelper.Now;

        var walkInRes = await apptService.CreateReceptionistAppointmentAsync(staffId, new CreateReceptionistAppointmentRequest
        {
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            AssignedDentistUserId = dentistId,
            RoomId = 10,
            DentalChairId = 101,
            ScheduledStart = nowClinic.AddMinutes(5),
            ReasonForVisit = "Bệnh nhân vãng lai đau răng khẩn cấp",
            AutoConfirm = true
        });

        Assert.True(walkInRes.Success, walkInRes.Message);
        Assert.NotNull(walkInRes.Data);
        Assert.Equal(AppointmentStatus.Confirmed, walkInRes.Data.Status);
        Assert.Equal(BookingSource.Receptionist, walkInRes.Data.BookingSource);
    }

    [Fact]
    public async Task P2_03_ReceptionistCheckIn_IssuesUniqueMonotonicQueueNumber()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var today = ClinicTimeHelper.Today;

        var appt1 = new Appointment
        {
            AppointmentCode = "APP-CHK-01",
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            AssignedDentistUserId = dentistId,
            RoomId = 10,
            DentalChairId = 101,
            ScheduledStart = today.AddHours(9),
            ScheduledEnd = today.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Khám 1",
            CreatedAt = DateTime.UtcNow
        };
        var appt2 = new Appointment
        {
            AppointmentCode = "APP-CHK-02",
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            AssignedDentistUserId = dentistId,
            RoomId = 10,
            DentalChairId = 102,
            ScheduledStart = today.AddHours(10),
            ScheduledEnd = today.AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Khám 2",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.AddRange(appt1, appt2);
        await db.SaveChangesAsync();

        var apptService = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);

        // Check in 1
        var chk1 = await apptService.ReceptionistCheckInAsync(appt1.AppointmentId, staffId, "Check-in 1");
        Assert.True(chk1.Success, chk1.Message);

        // Check in 2
        var chk2 = await apptService.ReceptionistCheckInAsync(appt2.AppointmentId, staffId, "Check-in 2");
        Assert.True(chk2.Success, chk2.Message);

        var fresh1 = await db.Appointments.FindAsync(appt1.AppointmentId);
        var fresh2 = await db.Appointments.FindAsync(appt2.AppointmentId);

        Assert.Equal(AppointmentStatus.CheckedIn, fresh1!.Status);
        Assert.Equal(AppointmentStatus.CheckedIn, fresh2!.Status);

        Assert.Equal("Q001", fresh1.QueueNumber);
        Assert.Equal("Q002", fresh2.QueueNumber);
    }

    [Fact]
    public async Task P2_04_ResourceAllocation_ConflictDetection_AcrossFullDuration()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var tomorrow = ClinicTimeHelper.Today.AddDays(1);

        // Existing Confirmed appointment on Doctor 301 from 10:00 to 11:00
        var existingAppt = new Appointment
        {
            AppointmentCode = "APP-CONF-EXIST",
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            AssignedDentistUserId = dentistId,
            RoomId = 10,
            DentalChairId = 101,
            ScheduledStart = tomorrow.AddHours(10),
            ScheduledEnd = tomorrow.AddHours(11),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Đã xếp lịch",
            CreatedAt = DateTime.UtcNow
        };

        // Pending appointment waiting for review
        var pendingAppt = new Appointment
        {
            AppointmentCode = "APP-CONF-NEW",
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            Status = AppointmentStatus.PendingReceptionReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Đang chờ duyệt",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.AddRange(existingAppt, pendingAppt);
        await db.SaveChangesAsync();

        var apptService = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);

        // Attempt to confirm overlapping appointment (10:30 to 11:00) with same Doctor 301
        var confOverlap = await apptService.ManagerConfirmAsync(pendingAppt.AppointmentId, staffId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = dentistId,
            RoomId = 10,
            DentalChairId = 102, // Different chair, but SAME DOCTOR!
            ScheduledStart = tomorrow.AddHours(10).AddMinutes(30),
            ScheduledEnd = tomorrow.AddHours(11)
        });

        Assert.False(confOverlap.Success);
        Assert.NotNull(confOverlap.Message);
        Assert.Contains("lịch hẹn khác", confOverlap.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task P2_05_AppointmentLifecycle_NoShow_Cancel_Reschedule()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var apptService = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var tomorrow = ClinicTimeHelper.Today.AddDays(1);

        // 1. NoShow
        var apptNoShow = new Appointment
        {
            AppointmentCode = "APP-NS-01",
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            AssignedDentistUserId = dentistId,
            Status = AppointmentStatus.Confirmed,
            ScheduledStart = tomorrow.AddHours(9),
            ScheduledEnd = tomorrow.AddHours(9).AddMinutes(30),
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Khám",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(apptNoShow);
        await db.SaveChangesAsync();

        var nsRes = await apptService.ReceptionistMarkNoShowAsync(apptNoShow.AppointmentId, staffId, "Bệnh nhân không đến");
        Assert.True(nsRes.Success);
        var freshNoShow = await db.Appointments.FindAsync(apptNoShow.AppointmentId);
        Assert.Equal(AppointmentStatus.NoShow, freshNoShow!.Status);

        // Verify Strike was accumulated for booking eligibility
        var eligibility = await apptService.GetBookingEligibilityAsync(patientId);
        Assert.True(eligibility.Success);
        Assert.NotNull(eligibility.Data);
        Assert.True(eligibility.Data.StrikeCount > 0);

        // 2. Cancel Confirmed Appointment
        var apptCancel = new Appointment
        {
            AppointmentCode = "APP-CAN-01",
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            AssignedDentistUserId = dentistId,
            RoomId = 10,
            DentalChairId = 101,
            Status = AppointmentStatus.Confirmed,
            ScheduledStart = tomorrow.AddHours(14),
            ScheduledEnd = tomorrow.AddHours(14).AddMinutes(30),
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Khám",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(apptCancel);
        await db.SaveChangesAsync();

        var canRes = await apptService.PatientCancelAsync(apptCancel.AppointmentId, patientId, "Bận việc đột xuất");
        Assert.True(canRes.Success);
        var freshCancel = await db.Appointments.FindAsync(apptCancel.AppointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, freshCancel!.Status);

        // 3. Reschedule
        var apptResched = new Appointment
        {
            AppointmentCode = "APP-RE-01",
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            Status = AppointmentStatus.PendingReceptionReview,
            ScheduledStart = tomorrow.AddHours(15),
            ScheduledEnd = tomorrow.AddHours(15).AddMinutes(30),
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Khám ban đầu",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(apptResched);
        await db.SaveChangesAsync();

        var reschedRes = await apptService.PatientRescheduleAsync(apptResched.AppointmentId, patientId, new PatientRescheduleRequest
        {
            NewScheduledStart = tomorrow.AddHours(16),
            Reason = "Đổi sang giờ muộn hơn"
        });
        Assert.True(reschedRes.Success, reschedRes.Message);
        var freshOld = await db.Appointments.FindAsync(apptResched.AppointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, freshOld!.Status);
        Assert.NotNull(reschedRes.Data);
        Assert.NotEqual("APP-RE-01", reschedRes.Data.AppointmentCode);
    }

    [Fact]
    public async Task P2_06_ChangeProposal_AcceptAndReject()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var apptService = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var tomorrow = ClinicTimeHelper.Today.AddDays(1);

        var appt = new Appointment
        {
            AppointmentCode = "APP-PROP-01",
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            Status = AppointmentStatus.PendingReceptionReview,
            ScheduledStart = tomorrow.AddHours(10),
            ScheduledEnd = tomorrow.AddHours(10).AddMinutes(30),
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Khám",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        // 1. Receptionist proposes change
        var propRes = await apptService.ManagerProposeChangeAsync(appt.AppointmentId, staffId, new ManagerProposeChangeRequest
        {
            ProposedDentistUserId = dentistId,
            ProposedStart = tomorrow.AddHours(14),
            ProposedEnd = tomorrow.AddHours(14).AddMinutes(30),
            Reason = "Bác sĩ bận ca sáng, đề xuất chuyển sang 14:00"
        });
        Assert.True(propRes.Success);
        var freshAppt = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.AwaitingPatientResponse, freshAppt!.Status);

        // 2. Patient rejects proposal -> reverts to PendingReceptionReview
        var rejectRes = await apptService.PatientRespondProposalAsync(appt.AppointmentId, patientId, accept: false);
        Assert.True(rejectRes.Success);
        freshAppt = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, freshAppt!.Status);
    }

    // ==========================================
    // ƯU TIÊN 3 – BOOKING
    // ==========================================

    [Fact]
    public async Task P3_01_CreateAppointment_DirectlyWithPatientRole()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var apptService = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var tomorrow = ClinicTimeHelper.Today.AddDays(1);

        var res = await apptService.CreatePatientAppointmentAsync(patientId, new CreateAppointmentRequest
        {
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            ScheduledStart = tomorrow.AddHours(11),
            ReasonForVisit = "Khám trực tiếp qua Patient portal"
        });

        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, res.Data.Status);
        Assert.Null(res.Data.AssignedDentistUserId);
        Assert.Null(res.Data.RoomId);
        Assert.Null(res.Data.DentalChairId);
        Assert.Null(res.Data.QueueNumber);
    }

    [Fact]
    public void P3_02_DateTimeNormalization_Unspecified_Utc_Local()
    {
        // 1. Unspecified (from HTML input) -> preserved as clinic wall-clock time
        var unspecified = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Unspecified);
        var normUnspecified = ClinicTimeHelper.ToClinicTime(unspecified);
        Assert.Equal(unspecified, normUnspecified);

        // 2. Utc -> converted to Vietnam time (+7)
        var utcTime = new DateTime(2026, 11, 20, 3, 0, 0, DateTimeKind.Utc);
        var normUtc = ClinicTimeHelper.ToClinicTime(utcTime);
        Assert.Equal(10, normUtc.Hour); // 3:00 UTC + 7 hours = 10:00 Vietnam time

        // 3. ClinicTimeHelper.Now vs DateTime.UtcNow
        var clinicNow = ClinicTimeHelper.Now;
        var utcNow = DateTime.UtcNow;
        var diff = clinicNow - utcNow;
        Assert.Equal(7, Math.Round(diff.TotalHours));
    }

    [Fact]
    public async Task P3_03_SlotRaceCondition_SecondConfirmationFailsGracefully()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var tomorrow = ClinicTimeHelper.Today.AddDays(1);

        // Two appointments competing for the same slot
        var apptA = new Appointment
        {
            AppointmentCode = "APP-RACE-A",
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            Status = AppointmentStatus.PendingReceptionReview,
            ScheduledStart = tomorrow.AddHours(10),
            ScheduledEnd = tomorrow.AddHours(10).AddMinutes(30),
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Đua slot A",
            CreatedAt = DateTime.UtcNow
        };
        var apptB = new Appointment
        {
            AppointmentCode = "APP-RACE-B",
            PatientUserId = patientId,
            DepartmentId = deptId,
            RequestedServiceId = serviceId,
            Status = AppointmentStatus.PendingReceptionReview,
            ScheduledStart = tomorrow.AddHours(10),
            ScheduledEnd = tomorrow.AddHours(10).AddMinutes(30),
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Đua slot B",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.AddRange(apptA, apptB);
        await db.SaveChangesAsync();

        var apptService = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);

        // Receptionist confirms Appointment A first -> SUCCEEDS
        var confA = await apptService.ManagerConfirmAsync(apptA.AppointmentId, staffId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = dentistId,
            RoomId = 10,
            DentalChairId = 101,
            ScheduledStart = tomorrow.AddHours(10),
            ScheduledEnd = tomorrow.AddHours(10).AddMinutes(30)
        });
        Assert.True(confA.Success);

        // Receptionist attempts to confirm Appointment B with same doctor & chair -> SAFELY REJECTED
        var confB = await apptService.ManagerConfirmAsync(apptB.AppointmentId, staffId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = dentistId,
            RoomId = 10,
            DentalChairId = 101,
            ScheduledStart = tomorrow.AddHours(10),
            ScheduledEnd = tomorrow.AddHours(10).AddMinutes(30)
        });
        Assert.False(confB.Success);
        Assert.NotNull(confB.Message);
        Assert.Contains("lịch hẹn khác", confB.Message.ToLowerInvariant());
    }

    // ==========================================
    // ƯU TIÊN 4 – BLOG
    // ==========================================

    [Fact]
    public async Task P4_01_Receptionist_CanCreateDraft_And_SubmitPendingReview()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var cat = new BlogCategory { CategoryId = 1, Name = "Nha Khoa Thường Thức", Slug = "nha-khoa-thuong-thuc" };
        db.BlogCategories.Add(cat);
        await db.SaveChangesAsync();

        var blogValidator = new DentalClinic.Application.Common.Validation.BlogMediaFileValidator();
        var blogService = new BlogService(db, _mockAudit.Object, _mockStorage.Object, blogValidator, _mockBlogLogger.Object);

        // Create with PendingReview
        var res = await blogService.CreatePostAsync(staffId, new CreateBlogPostRequest
        {
            CategoryId = 1,
            Title = "Cách Chăm Sóc Răng Miệng Đúng Cách",
            Content = "<p>Nội dung chia sẻ từ lễ tân phòng khám.</p>",
            Status = "PendingReview"
        });

        Assert.True(res.Success, res.Message);
        Assert.NotNull(res.Data);
        Assert.Equal(BlogPostStatus.PendingReview, res.Data.Status);
    }

    [Fact]
    public async Task P4_02_Receptionist_CannotSelfPublish_ForcedToDraft()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var cat = new BlogCategory { CategoryId = 1, Name = "Tin Tức", Slug = "tin-tuc" };
        db.BlogCategories.Add(cat);
        await db.SaveChangesAsync();

        var blogValidator = new DentalClinic.Application.Common.Validation.BlogMediaFileValidator();
        var blogService = new BlogService(db, _mockAudit.Object, _mockStorage.Object, blogValidator, _mockBlogLogger.Object);

        // Receptionist attempts to create with status "Published"
        var res = await blogService.CreatePostAsync(staffId, new CreateBlogPostRequest
        {
            CategoryId = 1,
            Title = "Bài Viết Tự Xuất Bản Trái Phép",
            Content = "<p>Nội dung muốn tự public</p>",
            Status = BlogPostStatus.Published // Attempt to bypass!
        });

        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        // BlogService strictly enforces Draft for non-PendingReview creation
        Assert.Equal(BlogPostStatus.Draft, res.Data.Status);
        Assert.NotEqual(BlogPostStatus.Published, res.Data.Status);
    }

    [Fact]
    public async Task P4_03_AdminCanReviewAndPublish()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        var (patientId, staffId, dentistId, deptId, serviceId) = await SeedBaseDataAsync(db);

        var cat = new BlogCategory { CategoryId = 1, Name = "Kiến Thức", Slug = "kien-thuc" };
        db.BlogCategories.Add(cat);

        // Admin user
        var adminUser = new UserAccount { UserId = 999, Email = "admin@dentalcare.com", Role = UserRole.SystemAdministrator, Status = AccountStatus.Active };
        db.UserAccounts.Add(adminUser);

        // Post in PendingReview
        var post = new BlogPost
        {
            BlogPostId = 10,
            CategoryId = 1,
            AuthorUserId = staffId,
            Title = "Bài Viết Chờ Admin Duyệt",
            Slug = "bai-viet-cho-admin-duyet",
            Content = "<p>Nội dung hoàn chỉnh</p>",
            Status = BlogPostStatus.PendingReview,
            CreatedAt = DateTime.UtcNow
        };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync();

        var blogValidator = new DentalClinic.Application.Common.Validation.BlogMediaFileValidator();
        var blogService = new BlogService(db, _mockAudit.Object, _mockStorage.Object, blogValidator, _mockBlogLogger.Object);

        // Admin approves and publishes
        var reviewRes = await blogService.ReviewPostAsync(10, 999, new ReviewBlogPostRequest
        {
            Status = BlogPostStatus.Published,
            ReviewNotes = "Bài viết đạt tiêu chuẩn y khoa."
        });

        Assert.True(reviewRes.Success, reviewRes.Message);
        Assert.NotNull(reviewRes.Data);
        Assert.Equal(BlogPostStatus.Published, reviewRes.Data.Status);
        Assert.NotNull(reviewRes.Data.PublishedAt);
    }
}
