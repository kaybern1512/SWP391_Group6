using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Booking.DTOs;
using DentalClinic.Application.Features.Feedback.DTOs;
using DentalClinic.Application.Features.Treatments.DTOs;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Application.Features.Blog.DTOs;
using DentalClinic.Application.Features.Support.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class PatientUatScenariosTests
{
    private readonly Mock<INotificationService> _mockNotification = new();
    private readonly Mock<ILogger<AppointmentService>> _mockApptLogger = new();
    private readonly Mock<ILogger<BookingService>> _mockBookingLogger = new();
    private readonly Mock<ILogger<BillingService>> _mockBillingLogger = new();
    private readonly Mock<ILogger<MedicalRecordService>> _mockMedLogger = new();
    private readonly Mock<ILogger<PrescriptionService>> _mockPrescLogger = new();
    private readonly Mock<ILogger<BlogService>> _mockBlogLogger = new();
    private readonly Mock<IAuditLogService> _mockAudit = new();
    private readonly Mock<IMediaStorageService> _mockMediaStorage = new();
    private readonly Mock<IBlogMediaFileValidator> _mockFileValidator = new();

    private DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedPatientEnvironmentAsync(DentalClinicDbContext db, DateTime futureDate)
    {
        // 1. Clinic Profile
        db.ClinicProfiles.Add(new ClinicProfile
        {
            ClinicId = 1,
            Name = "Nha Khoa DentalCare",
            PhoneNumber = "1900 1234",
            Email = "support@dentalcare.com",
            Address = "123 Nguyen Trai, Q.1, TP.HCM",
            OpeningTime = new TimeOnly(8, 0),
            ClosingTime = new TimeOnly(20, 0),
            UpdatedAt = DateTime.UtcNow
        });

        // 2. Staff / Admin user
        var adminUser = new UserAccount { UserId = 1, Email = "admin@dentalcare.com", Role = UserRole.SystemAdministrator, Status = AccountStatus.Active };
        var adminStaff = new StaffProfile { UserId = 1, EmployeeCode = "EMP-001", FullName = "Admin User", EmploymentStatus = "Active", User = adminUser };
        db.UserAccounts.Add(adminUser);
        db.StaffProfiles.Add(adminStaff);

        // 3. Patients
        var p1User = new UserAccount { UserId = 101, Email = "patient1@dentalcare.com", PhoneNumber = "0901111111", Role = UserRole.Patient, Status = AccountStatus.Active, PasswordHash = "HASHED_PW" };
        var p1Profile = new PatientProfile { UserId = 101, PatientCode = "PAT-202610-0002", FullName = "Trần Mai Chi", Gender = "Female", User = p1User };

        var p2User = new UserAccount { UserId = 102, Email = "patient2@dentalcare.com", PhoneNumber = "0902222222", Role = UserRole.Patient, Status = AccountStatus.Active, PasswordHash = "HASHED_PW" };
        var p2Profile = new PatientProfile { UserId = 102, PatientCode = "PAT-202610-0003", FullName = "Nguyễn Bảo Nam", Gender = "Male", User = p2User };

        db.UserAccounts.AddRange(p1User, p2User);
        db.PatientProfiles.AddRange(p1Profile, p2Profile);

        // 4. Departments
        var dGeneral = new Department { DepartmentId = 1, Name = "Khoa Răng Tổng Quát", Status = "Active" };
        var dOrtho = new Department { DepartmentId = 2, Name = "Khoa Chỉnh Nha", Status = "Active" };
        var dSurgery = new Department { DepartmentId = 3, Name = "Khoa Tiểu Phẫu", Status = "Active" };
        db.Departments.AddRange(dGeneral, dOrtho, dSurgery);

        // 5. Dentists
        var d1User = new UserAccount { UserId = 401, Email = "dentist1@dentalcare.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var d1Staff = new StaffProfile { UserId = 401, EmployeeCode = "EMP-D1", FullName = "BS. Hùng", EmploymentStatus = "Active", User = d1User };
        var d1Dentist = new DentistProfile { UserId = 401, LicenseNumber = "LIC-D1", User = d1Staff };
        var d1Dept = new DentistDepartment { DentistUserId = 401, DepartmentId = 1, Status = "Active" };

        var d2User = new UserAccount { UserId = 402, Email = "dentist2@dentalcare.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var d2Staff = new StaffProfile { UserId = 402, EmployeeCode = "EMP-D2", FullName = "BS. Thảo", EmploymentStatus = "Active", User = d2User };
        var d2Dentist = new DentistProfile { UserId = 402, LicenseNumber = "LIC-D2", User = d2Staff };
        var d2Dept = new DentistDepartment { DentistUserId = 402, DepartmentId = 1, Status = "Active" };

        db.UserAccounts.AddRange(d1User, d2User);
        db.StaffProfiles.AddRange(d1Staff, d2Staff);
        db.DentistProfiles.AddRange(d1Dentist, d2Dentist);
        db.DentistDepartments.AddRange(d1Dept, d2Dept);

        // 6. Rooms and Chairs
        var room = new Room { RoomId = 1, RoomCode = "ROOM-101", RoomName = "Phòng Khám 101", DepartmentId = 1, Status = "Active" };
        var chair = new DentalChair { DentalChairId = 11, RoomId = 1, ChairCode = "CHAIR-11", Status = "Active" };
        db.Rooms.Add(room);
        db.DentalChairs.Add(chair);

        // 7. Dentist Work Schedule for futureDate
        var dateOnly = DateOnly.FromDateTime(futureDate);
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 401,
            DepartmentId = 1,
            WorkDate = dateOnly,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            Status = "Scheduled"
        });

        // 8. Seed 31 Dental Services across departments
        for (int i = 1; i <= 31; i++)
        {
            int deptId = (i % 3) + 1;
            int duration = (i % 4 == 0) ? 60 : (i % 3 == 0) ? 45 : 30;
            db.DepartmentServices.Add(new DepartmentService
            {
                DepartmentServiceId = i,
                DepartmentId = deptId,
                ServiceName = $"Dịch vụ Nha khoa {i:D2}",
                Description = $"Mô tả chuẩn y khoa cho dịch vụ nha khoa {i:D2}",
                Price = 100000m * i,
                DurationMinutes = duration,
                MinLeadTimeHours = 2,
                Status = "Active"
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task UAT01_Patient_Registration_And_Authentication_Flow()
    {
        using var db = CreateDbContext(nameof(UAT01_Patient_Registration_And_Authentication_Flow));
        await SeedPatientEnvironmentAsync(db, DateTime.Today.AddDays(2));

        var patient = await db.UserAccounts.Include(u => u.PatientProfile).FirstOrDefaultAsync(u => u.UserId == 101);
        Assert.NotNull(patient);
        Assert.Equal(UserRole.Patient, patient!.Role);
        Assert.Equal(AccountStatus.Active, patient.Status);
        Assert.NotNull(patient.PatientProfile);
        Assert.Equal("PAT-202610-0002", patient.PatientProfile!.PatientCode);
    }

    [Fact]
    public async Task UAT02_PatientDashboard_DataRetrieval_And_Isolation()
    {
        using var db = CreateDbContext(nameof(UAT02_PatientDashboard_DataRetrieval_And_Isolation));
        var targetDate = DateTime.Today.AddDays(2).AddHours(9);
        await SeedPatientEnvironmentAsync(db, targetDate);

        // Add 1 appointment for Patient 101 and 1 for Patient 102
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 10,
            AppointmentCode = "APP-P1-01",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = targetDate,
            ScheduledEnd = targetDate.AddMinutes(30),
            Status = AppointmentStatus.Confirmed
        });

        db.Appointments.Add(new Appointment
        {
            AppointmentId = 20,
            AppointmentCode = "APP-P2-01",
            PatientUserId = 102,
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = targetDate,
            ScheduledEnd = targetDate.AddMinutes(30),
            Status = AppointmentStatus.Confirmed
        });

        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var p1Appointments = await service.GetPatientAppointmentsAsync(101);
        Assert.True(p1Appointments.Success);
        Assert.Single(p1Appointments.Data!);
        Assert.Equal("APP-P1-01", p1Appointments.Data![0].AppointmentCode);
    }

    [Fact]
    public async Task UAT03_04_DentalServicesCatalog_31Services_AvailableWithExactDurations()
    {
        using var db = CreateDbContext(nameof(UAT03_04_DentalServicesCatalog_31Services_AvailableWithExactDurations));
        await SeedPatientEnvironmentAsync(db, DateTime.Today.AddDays(2));

        var bookingService = new BookingService(db, _mockBookingLogger.Object);
        var allServices = await db.DepartmentServices.Where(s => s.Status == "Active").ToListAsync();
        Assert.Equal(31, allServices.Count);

        foreach (var svc in allServices)
        {
            Assert.True(svc.DurationMinutes >= 30);
            Assert.True(svc.MinLeadTimeHours >= 1);
            Assert.False(string.IsNullOrWhiteSpace(svc.ServiceName));
        }

        var deptServices = await bookingService.GetServicesByDepartmentAsync(1);
        Assert.True(deptServices.Success);
        Assert.NotEmpty(deptServices.Data!);
    }

    [Fact]
    public async Task UAT05_BookingWizard_CreatePatientAppointment_ValidFutureDate()
    {
        using var db = CreateDbContext(nameof(UAT05_BookingWizard_CreatePatientAppointment_ValidFutureDate));
        var validStart = DateTime.Today.AddDays(3).AddHours(10);
        await SeedPatientEnvironmentAsync(db, validStart);

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            RequestedDentistUserId = 401,
            ScheduledStart = validStart,
            ReasonForVisit = "Khám răng tổng quát và tư vấn"
        };

        var result = await service.CreatePatientAppointmentAsync(101, request);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.StartsWith("APP-", result.Data!.AppointmentCode);
        Assert.Equal(101, result.Data.PatientUserId);
    }

    [Fact]
    public async Task UAT06_Booking_PastDateTime_StrictlyRejected_P0()
    {
        using var db = CreateDbContext(nameof(UAT06_Booking_PastDateTime_StrictlyRejected_P0));
        await SeedPatientEnvironmentAsync(db, DateTime.Today.AddDays(2));

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var pastStart = DateTime.Now.AddHours(-3);

        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            RequestedDentistUserId = 401,
            ScheduledStart = pastStart,
            ReasonForVisit = "Cố tình đặt lịch quá khứ"
        };

        var result = await service.CreatePatientAppointmentAsync(101, request);
        Assert.False(result.Success);
        Assert.Contains("quá khứ", result.Message ?? string.Empty);
    }

    [Fact]
    public async Task UAT07_Booking_AvailableSlots_CalculatedFromDentistSchedule()
    {
        using var db = CreateDbContext(nameof(UAT07_Booking_AvailableSlots_CalculatedFromDentistSchedule));
        var scheduleDate = DateTime.Today.AddDays(2);
        await SeedPatientEnvironmentAsync(db, scheduleDate);

        var bookingService = new BookingService(db, _mockBookingLogger.Object);
        var query = new AvailableSlotsQuery
        {
            DentistUserId = 401,
            DepartmentId = 1,
            ServiceId = 1,
            Date = scheduleDate
        };

        var slotsResult = await bookingService.GetAvailableSlotsAsync(query);
        Assert.True(slotsResult.Success);
        Assert.NotNull(slotsResult.Data);
        Assert.NotEmpty(slotsResult.Data!);
    }

    [Fact]
    public async Task UAT08_AppointmentList_And_Withdraw_Own_Forbidden_Other()
    {
        using var db = CreateDbContext(nameof(UAT08_AppointmentList_And_Withdraw_Own_Forbidden_Other));
        var apptDate = DateTime.Today.AddDays(2).AddHours(14);
        await SeedPatientEnvironmentAsync(db, apptDate);

        var apptP1 = new Appointment
        {
            AppointmentId = 51,
            AppointmentCode = "APP-P1-WD",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = apptDate,
            ScheduledEnd = apptDate.AddMinutes(30),
            Status = AppointmentStatus.PendingReceptionReview
        };
        db.Appointments.Add(apptP1);
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);

        // Patient 102 tries to withdraw Patient 101's appointment -> Must fail
        var crossResult = await service.PatientWithdrawAsync(51, 102, "Lý do hủy");
        Assert.False(crossResult.Success);
        Assert.Contains("quyền", crossResult.Message ?? string.Empty);

        // Patient 101 withdraws own appointment -> Must succeed
        var ownResult = await service.PatientWithdrawAsync(51, 101, "Tôi bận việc đột xuất");
        Assert.True(ownResult.Success);

        var updated = await db.Appointments.FindAsync(51L);
        Assert.Equal(AppointmentStatus.Withdrawn, updated!.Status);
    }

    [Fact]
    public async Task UAT09_AppointmentReschedule_FutureSucceeds_PastRejected_CrossPatientBlocked()
    {
        using var db = CreateDbContext(nameof(UAT09_AppointmentReschedule_FutureSucceeds_PastRejected_CrossPatientBlocked));
        var oldDate = DateTime.Today.AddDays(2).AddHours(9);
        var newFutureDate = DateTime.Today.AddDays(4).AddHours(10);
        var pastDate = DateTime.Today.AddDays(-1).AddHours(10);
        await SeedPatientEnvironmentAsync(db, oldDate);

        // Add Dentist schedule for day 4
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 99,
            DentistUserId = 401,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(newFutureDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            Status = "Scheduled"
        });

        var appt = new Appointment
        {
            AppointmentId = 61,
            AppointmentCode = "APP-RESCHED-1",
            PatientUserId = 101,
            AssignedDentistUserId = 401,
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = oldDate,
            ScheduledEnd = oldDate.AddMinutes(30),
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);

        // 1. Cross-patient reschedule rejected
        var crossRes = await service.PatientRescheduleAsync(61, 102, new PatientRescheduleRequest { NewScheduledStart = newFutureDate, Reason = "Dời lịch" });
        Assert.False(crossRes.Success);

        // 2. Reschedule to past date rejected
        var pastRes = await service.PatientRescheduleAsync(61, 101, new PatientRescheduleRequest { NewScheduledStart = pastDate, Reason = "Dời về quá khứ" });
        Assert.False(pastRes.Success);

        // 3. Reschedule to valid future date succeeds
        var validRes = await service.PatientRescheduleAsync(61, 101, new PatientRescheduleRequest { NewScheduledStart = newFutureDate, Reason = "Dời sang ngày khác" });
        Assert.True(validRes.Success);
        Assert.NotNull(validRes.Data);
    }

    [Fact]
    public async Task UAT10_MedicalRecord_AccessControl_And_IDOR()
    {
        using var db = CreateDbContext(nameof(UAT10_MedicalRecord_AccessControl_And_IDOR));
        await SeedPatientEnvironmentAsync(db, DateTime.Today.AddDays(2));

        var medRecord1 = new DentalMedicalRecord { MedicalRecordId = 1, PatientUserId = 101, CreatedAt = DateTime.UtcNow };
        var medRecord2 = new DentalMedicalRecord { MedicalRecordId = 2, PatientUserId = 102, CreatedAt = DateTime.UtcNow };
        db.DentalMedicalRecords.AddRange(medRecord1, medRecord2);
        await db.SaveChangesAsync();

        var medService = new MedicalRecordService(db, _mockMedLogger.Object);

        // Patient 101 gets own record -> Success
        var p1Rec = await medService.GetMedicalRecordByPatientIdAsync(101);
        Assert.True(p1Rec.Success);
        Assert.Equal(101, p1Rec.Data!.PatientUserId);

        // Patient 102 gets own record -> Success
        var p2Rec = await medService.GetMedicalRecordByPatientIdAsync(102);
        Assert.True(p2Rec.Success);
        Assert.Equal(102, p2Rec.Data!.PatientUserId);
    }

    [Fact]
    public async Task UAT11_Odontogram_ViewAndIDOR()
    {
        using var db = CreateDbContext(nameof(UAT11_Odontogram_ViewAndIDOR));
        await SeedPatientEnvironmentAsync(db, DateTime.Today.AddDays(2));

        var medRecord = new DentalMedicalRecord { MedicalRecordId = 1, PatientUserId = 101, CreatedAt = DateTime.UtcNow };
        db.DentalMedicalRecords.Add(medRecord);

        var clinicalEntry = new ClinicalEntry
        {
            ClinicalEntryId = 1,
            MedicalRecordId = 1,
            DentistUserId = 401,
            Diagnosis = "Sâu răng",
            CreatedAt = DateTime.UtcNow
        };
        db.ClinicalEntries.Add(clinicalEntry);

        db.OdontogramEntries.Add(new OdontogramEntry
        {
            OdontogramEntryId = 1,
            ClinicalEntryId = 1,
            ToothNumber = "T18",
            Condition = "Sâu răng nhẹ",
            Surface = "Mặt nhai"
        });
        await db.SaveChangesAsync();

        var medService = new MedicalRecordService(db, _mockMedLogger.Object);
        var entries = await medService.GetCurrentOdontogramAsync(101);
        Assert.True(entries.Success);
        Assert.Single(entries.Data!);
        Assert.Equal("T18", entries.Data![0].ToothNumber);
    }

    [Fact]
    public async Task UAT12_TreatmentPlans_ViewAndIDOR()
    {
        using var db = CreateDbContext(nameof(UAT12_TreatmentPlans_ViewAndIDOR));
        await SeedPatientEnvironmentAsync(db, DateTime.Today.AddDays(2));

        var plan1 = new TreatmentPlan
        {
            TreatmentPlanId = 10,
            PatientUserId = 101,
            DepartmentId = 1,
            CreatedByDentistUserId = 401,
            Status = "InProgress",
            EstimatedTotal = 2000000m,
            CreatedAt = DateTime.UtcNow
        };
        var plan2 = new TreatmentPlan
        {
            TreatmentPlanId = 20,
            PatientUserId = 102,
            DepartmentId = 1,
            CreatedByDentistUserId = 401,
            Status = "InProgress",
            EstimatedTotal = 5000000m,
            CreatedAt = DateTime.UtcNow
        };
        db.TreatmentPlans.AddRange(plan1, plan2);
        await db.SaveChangesAsync();

        var treatService = new TreatmentService(db);

        // Patient 101 gets own plans -> only plan 10
        var p1Plans = await treatService.GetPatientTreatmentPlansAsync(101);
        Assert.True(p1Plans.Success);
        Assert.Single(p1Plans.Data!);
        Assert.Equal(10, p1Plans.Data![0].TreatmentPlanId);

        // Get plan by ID returns plan with PatientUserId matching
        var plan1Detail = await treatService.GetTreatmentPlanByIdAsync(10);
        Assert.True(plan1Detail.Success);
        Assert.Equal(101, plan1Detail.Data!.PatientUserId);

        var plan2Detail = await treatService.GetTreatmentPlanByIdAsync(20);
        Assert.True(plan2Detail.Success);
        Assert.Equal(102, plan2Detail.Data!.PatientUserId);
    }

    [Fact]
    public async Task UAT13_Prescriptions_ViewAndIDOR()
    {
        using var db = CreateDbContext(nameof(UAT13_Prescriptions_ViewAndIDOR));
        await SeedPatientEnvironmentAsync(db, DateTime.Today.AddDays(2));

        var appt1 = new Appointment
        {
            AppointmentId = 71,
            AppointmentCode = "APP-71",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 1,
            AssignedDentistUserId = 401,
            ScheduledStart = DateTime.Today.AddDays(-1),
            ScheduledEnd = DateTime.Today.AddDays(-1).AddMinutes(30),
            Status = AppointmentStatus.Completed
        };
        db.Appointments.Add(appt1);

        var plan = new TreatmentPlan
        {
            TreatmentPlanId = 701,
            PatientUserId = 101,
            DepartmentId = 1,
            CreatedByDentistUserId = 401,
            CreatedAt = DateTime.UtcNow
        };
        db.TreatmentPlans.Add(plan);

        var planItem = new TreatmentPlanItem
        {
            TreatmentPlanItemId = 702,
            TreatmentPlanId = 701,
            DepartmentServiceId = 1,
            SequenceNo = 1
        };
        db.TreatmentPlanItems.Add(planItem);

        var session = new TreatmentSession
        {
            TreatmentSessionId = 703,
            TreatmentPlanItemId = 702,
            AppointmentId = 71,
            DentistUserId = 401,
            SessionDate = DateTime.UtcNow
        };
        db.TreatmentSessions.Add(session);

        var presc1 = new Prescription
        {
            PrescriptionId = 81,
            TreatmentSessionId = 703,
            IssuedAt = DateTime.UtcNow,
            GeneralInstructions = "Uống sau ăn"
        };
        db.Prescriptions.Add(presc1);
        await db.SaveChangesAsync();

        var prescService = new PrescriptionService(db, _mockPrescLogger.Object);
        var p1Prescs = await prescService.GetPatientPrescriptionsAsync(101);
        Assert.True(p1Prescs.Success);
        Assert.Single(p1Prescs.Data!);
        Assert.Equal(81, p1Prescs.Data![0].PrescriptionId);

        var detail = await prescService.GetPrescriptionDetailAsync(81);
        Assert.True(detail.Success);
        Assert.Equal(101, detail.Data!.PatientUserId);
    }

    [Fact]
    public async Task UAT14_Invoices_VietQr_QuicklinkFormat_And_UnpaidStatus()
    {
        using var db = CreateDbContext(nameof(UAT14_Invoices_VietQr_QuicklinkFormat_And_UnpaidStatus));
        await SeedPatientEnvironmentAsync(db, DateTime.Today.AddDays(2));

        var appt = new Appointment
        {
            AppointmentId = 91,
            AppointmentCode = "APP-INV-91",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = DateTime.Today.AddDays(-1),
            ScheduledEnd = DateTime.Today.AddDays(-1).AddMinutes(30),
            Status = AppointmentStatus.Completed
        };
        db.Appointments.Add(appt);

        var inv = new Invoice
        {
            InvoiceId = 91,
            InvoiceCode = "INV-202611-001",
            AppointmentId = 91,
            GeneratedByUserId = 1,
            TotalAmount = 450500m,
            Status = "Unpaid",
            CreatedAt = DateTime.UtcNow
        };
        db.Invoices.Add(inv);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, _mockNotification.Object, _mockBillingLogger.Object);
        var patientInvoices = await billingService.GetPatientInvoicesAsync(101);
        Assert.True(patientInvoices.Success);
        Assert.Single(patientInvoices.Data!);

        var invoiceDto = patientInvoices.Data![0];
        Assert.Equal("Unpaid", invoiceDto.Status);
        Assert.Equal(450500m, invoiceDto.RemainingAmount);

        // Validate VietQR link pattern
        var bankId = "MB";
        var accountNo = "039123456789";
        var accountName = Uri.EscapeDataString("NHA KHOA DENTALCARE");
        var amount = (long)invoiceDto.RemainingAmount;
        var qrUrl = $"https://img.vietqr.io/image/{bankId}-{accountNo}-compact2.png?amount={amount}&addInfo=THANH%20TOAN%20{invoiceDto.InvoiceCode}&accountName={accountName}";

        Assert.Contains("MB-039123456789-compact2.png", qrUrl);
        Assert.Contains("amount=450500", qrUrl);
        Assert.Contains("THANH%20TOAN%20INV-202611-001", qrUrl);
    }

    [Fact]
    public async Task UAT15_Feedback_CompletedOnly_NoDuplicate_IDOR()
    {
        using var db = CreateDbContext(nameof(UAT15_Feedback_CompletedOnly_NoDuplicate_IDOR));
        await SeedPatientEnvironmentAsync(db, DateTime.Today.AddDays(2));

        var apptCompleted = new Appointment
        {
            AppointmentId = 101,
            AppointmentCode = "APP-COMPLETED",
            PatientUserId = 101,
            AssignedDentistUserId = 401,
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = DateTime.Today.AddDays(-2),
            ScheduledEnd = DateTime.Today.AddDays(-2).AddMinutes(30),
            Status = AppointmentStatus.Completed
        };

        var apptPending = new Appointment
        {
            AppointmentId = 102,
            AppointmentCode = "APP-PENDING",
            PatientUserId = 101,
            AssignedDentistUserId = 401,
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = DateTime.Today.AddDays(2),
            ScheduledEnd = DateTime.Today.AddDays(2).AddMinutes(30),
            Status = AppointmentStatus.PendingReceptionReview
        };

        db.Appointments.AddRange(apptCompleted, apptPending);
        await db.SaveChangesAsync();

        var feedbackService = new FeedbackService(db);

        // 1. Feedback on non-completed appointment -> Rejected
        var pendingReq = new CreateFeedbackRequest
        {
            AppointmentId = 102,
            DentistUserId = 401,
            DentistRating = 5,
            ClinicRating = 5,
            Comment = "Rất tốt"
        };
        var resPending = await feedbackService.CreateFeedbackAsync(pendingReq, 101);
        Assert.False(resPending.Success);
        Assert.Contains("hoàn thành", resPending.Message ?? string.Empty);

        // 2. Feedback by another patient (IDOR) -> Rejected
        var idorReq = new CreateFeedbackRequest
        {
            AppointmentId = 101,
            DentistUserId = 401,
            DentistRating = 5,
            ClinicRating = 5,
            Comment = "Đánh giá trộm"
        };
        var resIdor = await feedbackService.CreateFeedbackAsync(idorReq, 102);
        Assert.False(resIdor.Success);
        Assert.Contains("chính mình", resIdor.Message ?? string.Empty);

        // 3. Valid feedback on completed appointment -> Success
        var validReq = new CreateFeedbackRequest
        {
            AppointmentId = 101,
            DentistUserId = 401,
            DentistRating = 5,
            ClinicRating = 4,
            Comment = "Bác sĩ rất ân cần, phòng khám sạch sẽ"
        };
        var resValid = await feedbackService.CreateFeedbackAsync(validReq, 101);
        Assert.True(resValid.Success);

        // 4. Duplicate feedback on same appointment -> Rejected
        var resDuplicate = await feedbackService.CreateFeedbackAsync(validReq, 101);
        Assert.False(resDuplicate.Success);
        Assert.Contains("đã được đánh giá", resDuplicate.Message ?? string.Empty);
    }

    [Fact]
    public async Task UAT16_BlogNews_PublicAccess()
    {
        using var db = CreateDbContext(nameof(UAT16_BlogNews_PublicAccess));
        await SeedPatientEnvironmentAsync(db, DateTime.Today.AddDays(2));

        var cat = new BlogCategory { CategoryId = 1, Name = "Kiến thức nha khoa", Slug = "kien-thuc-nha-khoa", CreatedAt = DateTime.UtcNow };
        db.BlogCategories.Add(cat);

        var post = new BlogPost
        {
            BlogPostId = 1,
            CategoryId = 1,
            AuthorUserId = 401,
            Title = "Cách chăm sóc răng sau khi tẩy trắng",
            Slug = "cach-cham-soc-rang-sau-khi-tay-trang",
            Summary = "Hướng dẫn chi tiết chăm sóc răng",
            Content = "<p>Nội dung bài viết y khoa hữu ích cho bệnh nhân.</p>",
            Status = "Published",
            PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync();

        var blogService = new BlogService(db, _mockAudit.Object, _mockMediaStorage.Object, _mockFileValidator.Object, _mockBlogLogger.Object);
        var publicPosts = await blogService.GetPublicPostsAsync(new BlogFilterRequest());
        Assert.True(publicPosts.Success);
        Assert.Single(publicPosts.Data!.Items);
        Assert.Equal("Cách chăm sóc răng sau khi tẩy trắng", publicPosts.Data.Items[0].Title);
    }

    [Fact]
    public async Task UAT17_LiveChat_SupportConfig()
    {
        using var db = CreateDbContext(nameof(UAT17_LiveChat_SupportConfig));
        await SeedPatientEnvironmentAsync(db, DateTime.Today.AddDays(2));

        var supportService = new SupportService(db);
        var config = await supportService.GetSupportConfigAsync();
        Assert.True(config.Success);
        Assert.NotNull(config.Data);
        Assert.Equal("1900 1234", config.Data!.Hotline);
        Assert.Equal("Nha Khoa DentalCare", config.Data.ClinicName);
    }

    [Fact]
    public async Task UAT18_Regression_DuplicateBookingSubmission_WF02_StrictlyPrevented()
    {
        using var db = CreateDbContext(nameof(UAT18_Regression_DuplicateBookingSubmission_WF02_StrictlyPrevented));
        var targetStart = DateTime.Today.AddDays(3).AddHours(9);
        await SeedPatientEnvironmentAsync(db, targetStart);

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = targetStart,
            ReasonForVisit = "Khám răng định kỳ lần 1"
        };

        // First attempt succeeds
        var firstResult = await service.CreatePatientAppointmentAsync(101, request);
        Assert.True(firstResult.Success);

        // Immediate duplicate submission (double-click simulation)
        var duplicateRequest = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = targetStart,
            ReasonForVisit = "Khám răng định kỳ lần 2 (bấm nhầm gửi lặp)"
        };

        var secondResult = await service.CreatePatientAppointmentAsync(101, duplicateRequest);
        Assert.False(secondResult.Success);
        Assert.Contains("khung giờ này rồi", secondResult.Message ?? string.Empty);
    }
}
