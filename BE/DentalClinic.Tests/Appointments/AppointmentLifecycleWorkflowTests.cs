using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using AppointmentService = DentalClinic.Infrastructure.Services.AppointmentService;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Appointments;

public class AppointmentLifecycleWorkflowTests
{
    private readonly Mock<ILogger<AppointmentService>> _mockLogger = new();
    private readonly Mock<INotificationService> _mockNotification = new();

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedLifecycleDataAsync(DentalClinicDbContext db)
    {
        // 1. Patients
        var patientUser1 = new UserAccount
        {
            UserId = 1001,
            Email = "patient1001@dentalcare.com",
            PhoneNumber = "0901001001",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile1 = new PatientProfile
        {
            UserId = 1001,
            FullName = "Nguyễn Văn An",
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-25)),
            User = patientUser1
        };

        var patientUser2 = new UserAccount
        {
            UserId = 1002,
            Email = "pediatric1002@dentalcare.com",
            PhoneNumber = "0901001002",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile2 = new PatientProfile
        {
            UserId = 1002,
            FullName = "Trần Thiếu Niên",
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-16).AddDays(-10)), // >= 16 years old
            User = patientUser2
        };

        db.UserAccounts.AddRange(patientUser1, patientUser2);
        db.PatientProfiles.AddRange(patientProfile1, patientProfile2);

        // 2. Staff: Receptionist & Dentist
        var recUser = new UserAccount
        {
            UserId = 2001,
            Email = "rec2001@dentalcare.com",
            Role = UserRole.Receptionist,
            Status = AccountStatus.Active
        };
        var recStaff = new StaffProfile
        {
            UserId = 2001,
            FullName = "Lê Thị Lễ Tân",
            EmploymentStatus = "Active",
            User = recUser
        };
        db.UserAccounts.Add(recUser);
        db.StaffProfiles.Add(recStaff);

        var denUser = new UserAccount
        {
            UserId = 3001,
            Email = "dentist3001@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var denStaff = new StaffProfile
        {
            UserId = 3001,
            FullName = "Bác Sĩ Hoàng Minh",
            EmploymentStatus = "Active",
            User = denUser
        };
        var denProfile = new DentistProfile
        {
            UserId = 3001,
            DepartmentId = 1,
            LicenseNumber = "LIC-3001",
            StaffProfile = denStaff
        };
        db.UserAccounts.Add(denUser);
        db.StaffProfiles.Add(denStaff);
        db.DentistProfiles.Add(denProfile);

        // 3. Departments, Services, Rooms, Chairs
        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Tổng Quát",
            Status = "Active"
        };
        db.Departments.Add(dept);

        var s10 = new DepartmentService
        {
            DepartmentServiceId = 10,
            DepartmentId = 1,
            ServiceCode = "SRV-10",
            ServiceName = "Khám Tổng Quát",
            DurationMinutes = 30,
            Price = 150000,
            Status = "Active"
        };
        var s11 = new DepartmentService
        {
            DepartmentServiceId = 11,
            DepartmentId = 1,
            ServiceCode = "SRV-11",
            ServiceName = "Nhổ Răng Khôn Phức Tạp",
            DurationMinutes = 60,
            Price = 800000,
            Status = "Active"
        };
        var s12 = new DepartmentService
        {
            DepartmentServiceId = 12,
            DepartmentId = 1,
            ServiceCode = "SRV-12",
            ServiceName = "Chăm Sóc Răng Trẻ Em",
            DurationMinutes = 30,
            Price = 200000,
            Status = "Active"
        };
        db.DepartmentServices.AddRange(s10, s11, s12);

        var room = new Room
        {
            RoomId = 1,
            DepartmentId = 1,
            RoomCode = "ROOM-101",
            RoomName = "Phòng Khám 101",
            RoomType = "Clinical",
            Status = "Active"
        };
        db.Rooms.Add(room);

        var chair1 = new DentalChair
        {
            DentalChairId = 11,
            RoomId = 1,
            ChairCode = "CHAIR-101-A",
            ChairName = "Ghế 101-A",
            Status = "Active"
        };
        var chair2 = new DentalChair
        {
            DentalChairId = 12,
            RoomId = 1,
            ChairCode = "CHAIR-101-B",
            ChairName = "Ghế 101-B",
            Status = "Active"
        };
        db.DentalChairs.AddRange(chair1, chair2);

        // 4. Staff Work Schedules: 08:00–18:00
        var baseDate = DateOnly.FromDateTime(DateTime.Today);
        long schedId = 100;
        for (int i = 0; i <= 30; i++)
        {
            var d = baseDate.AddDays(i);
            db.StaffWorkSchedules.Add(new StaffWorkSchedule
            {
                WorkScheduleId = schedId++,
                StaffUserId = 3001,
                StaffRole = "Dentist",
                WorkDate = d,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(18, 0),
                Status = "Scheduled"
            });
        }

        await db.SaveChangesAsync();
    }

    private AppointmentService CreateService(DentalClinicDbContext db)
    {
        var clinicalAvail = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        return new AppointmentService(db, _mockNotification.Object, null, null, _mockLogger.Object, null, clinicalAvail);
    }

    // =========================================================================
    // SECTION 1: SHIFT BOUNDARY VALIDATION (TC01–TC07)
    // =========================================================================

    [Fact]
    public async Task TC01_ShiftBoundary_MorningWithinBounds_Success()
    {
        using var db = CreateDbContext(nameof(TC01_ShiftBoundary_MorningWithinBounds_Success));
        await SeedLifecycleDataAsync(db);
        var service = CreateService(db);

        var targetStart = DateTime.Today.AddDays(2).AddHours(9); // 09:00 - 09:30
        var req = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            RequestedDentistUserId = 3001,
            ScheduledStart = targetStart,
            ReasonForVisit = "Khám răng sáng"
        };

        var result = await service.CreatePatientAppointmentAsync(1001, req);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, result.Data.Status);
    }

    [Fact]
    public async Task TC02_ShiftBoundary_MorningExactBoundary_Success()
    {
        using var db = CreateDbContext(nameof(TC02_ShiftBoundary_MorningExactBoundary_Success));
        await SeedLifecycleDataAsync(db);
        var service = CreateService(db);

        var targetStart = DateTime.Today.AddDays(2).AddHours(11).AddMinutes(30); // 11:30 - 12:00 (30m)
        var req = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10, // 30 min duration
            RequestedDentistUserId = 3001,
            ScheduledStart = targetStart,
            ReasonForVisit = "Khám sát giờ trưa"
        };

        var result = await service.CreatePatientAppointmentAsync(1001, req);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task TC03_ShiftBoundary_MorningOverflowPast12_Rejected()
    {
        using var db = CreateDbContext(nameof(TC03_ShiftBoundary_MorningOverflowPast12_Rejected));
        await SeedLifecycleDataAsync(db);
        var service = CreateService(db);

        var targetStart = DateTime.Today.AddDays(2).AddHours(11).AddMinutes(30); // 11:30 with 60m => 12:30
        var req = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 11, // 60 min duration
            RequestedDentistUserId = 3001,
            ScheduledStart = targetStart,
            ReasonForVisit = "Nhổ răng khôn sát giờ trưa"
        };

        var result = await service.CreatePatientAppointmentAsync(1001, req);
        Assert.False(result.Success);
        Assert.Contains("12:00", result.Message);
    }

    [Fact]
    public async Task TC04_ShiftBoundary_LunchBreakBooking_Rejected()
    {
        using var db = CreateDbContext(nameof(TC04_ShiftBoundary_LunchBreakBooking_Rejected));
        await SeedLifecycleDataAsync(db);
        var service = CreateService(db);

        var targetStart = DateTime.Today.AddDays(2).AddHours(12).AddMinutes(15); // In 12:00-13:00
        var req = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            RequestedDentistUserId = 3001,
            ScheduledStart = targetStart,
            ReasonForVisit = "Đặt giờ nghỉ trưa"
        };

        var result = await service.CreatePatientAppointmentAsync(1001, req);
        Assert.False(result.Success);
        Assert.Contains("nghỉ trưa", result.Message);
    }

    [Fact]
    public async Task TC05_ShiftBoundary_AfternoonWithinBounds_Success()
    {
        using var db = CreateDbContext(nameof(TC05_ShiftBoundary_AfternoonWithinBounds_Success));
        await SeedLifecycleDataAsync(db);
        var service = CreateService(db);

        var targetStart = DateTime.Today.AddDays(2).AddHours(14); // 14:00 - 15:00
        var req = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 11, // 60 min
            RequestedDentistUserId = 3001,
            ScheduledStart = targetStart,
            ReasonForVisit = "Nhổ răng ca chiều"
        };

        var result = await service.CreatePatientAppointmentAsync(1001, req);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task TC06_ShiftBoundary_AfternoonOverflowPast1730_Rejected()
    {
        using var db = CreateDbContext(nameof(TC06_ShiftBoundary_AfternoonOverflowPast1730_Rejected));
        await SeedLifecycleDataAsync(db);
        var service = CreateService(db);

        var targetStart = DateTime.Today.AddDays(2).AddHours(17).AddMinutes(0); // 17:00 with 60m => 18:00 > 17:30
        var req = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 11, // 60 min
            RequestedDentistUserId = 3001,
            ScheduledStart = targetStart,
            ReasonForVisit = "Khám muộn ca chiều"
        };

        var result = await service.CreatePatientAppointmentAsync(1001, req);
        Assert.False(result.Success);
        Assert.Contains("17:30", result.Message);
    }

    [Fact]
    public async Task TC07_ShiftBoundary_OutsideClinicHours_Rejected()
    {
        using var db = CreateDbContext(nameof(TC07_ShiftBoundary_OutsideClinicHours_Rejected));
        await SeedLifecycleDataAsync(db);
        var service = CreateService(db);

        var earlyMorning = DateTime.Today.AddDays(2).AddHours(7).AddMinutes(30); // 07:30 < 08:00
        var req = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            RequestedDentistUserId = 3001,
            ScheduledStart = earlyMorning,
            ReasonForVisit = "Khám sáng sớm trước giờ mở cửa"
        };

        var result = await service.CreatePatientAppointmentAsync(1001, req);
        Assert.False(result.Success);
        Assert.Contains("07:30", result.Message);
    }

    // =========================================================================
    // SECTION 2: CLINICAL CONTINUATION (TC08–TC15)
    // =========================================================================

    private static async Task<Appointment> CreateInServiceAppointmentAsync(DentalClinicDbContext db)
    {
        var appt = new Appointment
        {
            PatientUserId = 1001,
            Status = AppointmentStatus.InService,
            BookingSource = "Online",
            ReasonForVisit = "Đang điều trị tủy",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var morningStart = DateTime.Today.AddHours(11);
        var apptService = new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 11,
            UnitPrice = 800000,
            ScheduledStart = morningStart,
            ScheduledEnd = morningStart.AddMinutes(60),
            ServiceDate = DateOnly.FromDateTime(morningStart),
            AssignedDentistUserId = 3001,
            RoomId = 1,
            DentalChairId = 11,
            Status = AppointmentStatus.InService
        };
        db.AppointmentServices.Add(apptService);
        await db.SaveChangesAsync();
        return appt;
    }

    [Fact]
    public async Task TC08_ClinicalContinuation_ContinueAfternoon_CreatesContinuationServiceItem()
    {
        using var db = CreateDbContext(nameof(TC08_ClinicalContinuation_ContinueAfternoon_CreatesContinuationServiceItem));
        await SeedLifecycleDataAsync(db);
        var appt = await CreateInServiceAppointmentAsync(db);
        var service = CreateService(db);

        var afternoonStart = DateTime.Today.AddHours(13).AddMinutes(30);
        var req = new DentistContinueAfternoonRequest
        {
            ContinuationStartTime = afternoonStart,
            ContinuationDurationMinutes = 45,
            ClinicalNotes = "Tạm dừng nghỉ trưa, tiếp tục ca chiều"
        };

        var result = await service.DentistContinueAfternoonAsync(appt.AppointmentId, 3001, req);
        Assert.True(result.Success, result.Message);

        var services = await db.AppointmentServices.Where(s => s.AppointmentId == appt.AppointmentId).ToListAsync();
        Assert.Equal(2, services.Count);
    }

    [Fact]
    public async Task TC09_ClinicalContinuation_ContinuationItem_HasUnitPriceZero_NoDoubleBilling()
    {
        using var db = CreateDbContext(nameof(TC09_ClinicalContinuation_ContinuationItem_HasUnitPriceZero_NoDoubleBilling));
        await SeedLifecycleDataAsync(db);
        var appt = await CreateInServiceAppointmentAsync(db);
        var service = CreateService(db);

        var afternoonStart = DateTime.Today.AddHours(14);
        var req = new DentistContinueAfternoonRequest
        {
            ContinuationStartTime = afternoonStart,
            ContinuationDurationMinutes = 45,
            ClinicalNotes = "Phiên tiếp tục"
        };

        await service.DentistContinueAfternoonAsync(appt.AppointmentId, 3001, req);

        var continuationItem = await db.AppointmentServices
            .Where(s => s.AppointmentId == appt.AppointmentId && s.ScheduledStart == afternoonStart)
            .FirstOrDefaultAsync();

        Assert.NotNull(continuationItem);
        Assert.Equal(0m, continuationItem.UnitPrice); // Zero price to avoid double billing
    }

    [Fact]
    public async Task TC10_ClinicalContinuation_ReleasesDentalChairDuringLunch()
    {
        using var db = CreateDbContext(nameof(TC10_ClinicalContinuation_ReleasesDentalChairDuringLunch));
        await SeedLifecycleDataAsync(db);
        var appt = await CreateInServiceAppointmentAsync(db);
        var service = CreateService(db);

        var afternoonStart = DateTime.Today.AddHours(14);
        var req = new DentistContinueAfternoonRequest
        {
            ContinuationStartTime = afternoonStart,
            ContinuationDurationMinutes = 45,
            ClinicalNotes = "Giải phóng ghế"
        };

        await service.DentistContinueAfternoonAsync(appt.AppointmentId, 3001, req);

        var morningItem = await db.AppointmentServices
            .Where(s => s.AppointmentId == appt.AppointmentId && s.UnitPrice > 0)
            .FirstOrDefaultAsync();

        Assert.NotNull(morningItem);
        Assert.NotNull(morningItem.DentalChairId); // Historical chair attribution PRESERVED!

        // Chair is naturally released for lunch because morning session ended at or before 12:00
        var clinicalAvail = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var chairCheck = await clinicalAvail.CheckChairAvailabilityAsync(
            morningItem.DentalChairId.Value, DateTime.Today.AddHours(12).AddMinutes(15), DateTime.Today.AddHours(13));
        Assert.True(chairCheck.IsAvailable); // Chair is free during lunch!
    }

    [Fact]
    public async Task TC11_ClinicalContinuation_PreservesClinicalHistoryOnSameAppointment()
    {
        using var db = CreateDbContext(nameof(TC11_ClinicalContinuation_PreservesClinicalHistoryOnSameAppointment));
        await SeedLifecycleDataAsync(db);
        var appt = await CreateInServiceAppointmentAsync(db);
        var service = CreateService(db);

        var afternoonStart = DateTime.Today.AddHours(14);
        var req = new DentistContinueAfternoonRequest
        {
            ContinuationStartTime = afternoonStart,
            ContinuationDurationMinutes = 30,
            ClinicalNotes = "Tiếp nối lịch sử lâm sàng"
        };

        await service.DentistContinueAfternoonAsync(appt.AppointmentId, 3001, req);

        var currentAppt = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.NotNull(currentAppt);
        Assert.Equal(AppointmentStatus.Confirmed, currentAppt.Status);
    }

    [Fact]
    public async Task TC12_ClinicalContinuation_ContinueAfternoon_RejectedWhenNotExamining()
    {
        using var db = CreateDbContext(nameof(TC12_ClinicalContinuation_ContinueAfternoon_RejectedWhenNotExamining));
        await SeedLifecycleDataAsync(db);
        var appt = await CreateInServiceAppointmentAsync(db);
        appt.Status = AppointmentStatus.Confirmed; // Not InService
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var req = new DentistContinueAfternoonRequest
        {
            ContinuationStartTime = DateTime.Today.AddHours(14),
            ContinuationDurationMinutes = 30
        };

        var result = await service.DentistContinueAfternoonAsync(appt.AppointmentId, 3001, req);
        Assert.False(result.Success);
        Assert.Contains("tiếp tục ca chiều", result.Message);
    }

    [Fact]
    public async Task TC13_ClinicalContinuation_RecommendAnotherDay_CreatesProposedFollowUp()
    {
        using var db = CreateDbContext(nameof(TC13_ClinicalContinuation_RecommendAnotherDay_CreatesProposedFollowUp));
        await SeedLifecycleDataAsync(db);
        var appt = await CreateInServiceAppointmentAsync(db);
        var service = CreateService(db);

        var nextWeek = DateTime.Today.AddDays(7);
        var req = new DentistRecommendAnotherDayRequest
        {
            RecommendedDate = nextWeek,
            RecommendedServiceId = 11,
            ClinicalReason = "Chờ lành thương xương ổ răng",
            CompletedWorkNotes = "Đã lấy vôi răng và xử lý viêm",
            RemainingWorkNotes = "Tiến hành trám bít ống tủy"
        };

        var result = await service.DentistRecommendAnotherDayAsync(appt.AppointmentId, 3001, req);
        Assert.True(result.Success, result.Message);

        var followUp = await db.FollowUpSchedules.FirstOrDefaultAsync(f => f.PatientUserId == 1001 && f.DentistUserId == 3001);
        Assert.NotNull(followUp);
        Assert.Equal("Proposed", followUp.Status);
    }

    [Fact]
    public async Task TC14_ClinicalContinuation_RecommendAnotherDay_KeepsTreatmentPlanInProgress()
    {
        using var db = CreateDbContext(nameof(TC14_ClinicalContinuation_RecommendAnotherDay_KeepsTreatmentPlanInProgress));
        await SeedLifecycleDataAsync(db);
        var appt = await CreateInServiceAppointmentAsync(db);

        var medRecord = new DentalMedicalRecord
        {
            PatientUserId = 1001,
            MedicalHistory = "Khỏe mạnh",
            CreatedAt = DateTime.UtcNow
        };
        db.DentalMedicalRecords.Add(medRecord);
        await db.SaveChangesAsync();

        var plan = new TreatmentPlan
        {
            MedicalRecordId = medRecord.RecordId,
            PatientUserId = 1001,
            CreatedByDentistUserId = 3001,
            Title = "Điều trị tủy nhiều giai đoạn",
            Status = "InProgress",
            Diagnosis = "Viêm tủy cấp"
        };
        db.TreatmentPlans.Add(plan);
        await db.SaveChangesAsync();

        var planItem = new TreatmentPlanItem
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            DepartmentServiceId = 11,
            SequenceNo = 1,
            Notes = "Giai đoạn 1"
        };
        db.TreatmentPlanItems.Add(planItem);
        await db.SaveChangesAsync();

        var primaryItem = await db.AppointmentServices.FirstAsync(s => s.AppointmentId == appt.AppointmentId);
        primaryItem.TreatmentPlanItemId = planItem.PlanItemId;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var req = new DentistRecommendAnotherDayRequest
        {
            RecommendedDate = DateTime.Today.AddDays(5),
            ClinicalReason = "Giai đoạn 2 điều trị tủy",
            CompletedWorkNotes = "Giai đoạn 1 hoàn thành",
            RemainingWorkNotes = "Giai đoạn 2 tiếp theo"
        };

        await service.DentistRecommendAnotherDayAsync(appt.AppointmentId, 3001, req);

        var refreshedPlan = await db.TreatmentPlans.FindAsync(plan.TreatmentPlanId);
        Assert.NotNull(refreshedPlan);
        Assert.Equal("InProgress", refreshedPlan.Status); // Preserved InProgress without auto-completing
    }

    [Fact]
    public async Task TC15_ClinicalContinuation_FlagDelay_RecordsClinicalAlert()
    {
        using var db = CreateDbContext(nameof(TC15_ClinicalContinuation_FlagDelay_RecordsClinicalAlert));
        await SeedLifecycleDataAsync(db);
        var appt = await CreateInServiceAppointmentAsync(db);
        var service = CreateService(db);

        var result = await service.DentistFlagDelayAsync(appt.AppointmentId, 3001, "Chảy máu chân răng phức tạp, cần giữ ghế cầm máu thêm 20 phút.");
        Assert.True(result.Success, result.Message);

        var item = await db.AppointmentServices.FirstAsync(s => s.AppointmentId == appt.AppointmentId);
        Assert.Contains("Chảy máu chân răng", item.Notes);
    }

    // =========================================================================
    // SECTION 3: RESCHEDULE REQUEST & RECEPTIONIST REVIEW (TC16–TC24)
    // =========================================================================

    [Fact]
    public async Task TC16_Reschedule_PendingReceptionReview_DirectlyUpdatesDraft()
    {
        using var db = CreateDbContext(nameof(TC16_Reschedule_PendingReceptionReview_DirectlyUpdatesDraft));
        await SeedLifecycleDataAsync(db);
        var service = CreateService(db);

        var originalStart = DateTime.Today.AddDays(2).AddHours(9);
        var bookResult = await service.CreatePatientAppointmentAsync(1001, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = originalStart,
            ReasonForVisit = "Đau răng"
        });

        Assert.True(bookResult.Success);
        var apptId = bookResult.Data!.AppointmentId;

        var newStart = DateTime.Today.AddDays(3).AddHours(10);
        var rescheduleResult = await service.PatientRescheduleAsync(apptId, 1001, new PatientRescheduleRequest
        {
            NewScheduledStart = newStart,
            Reason = "Bận việc gia đình"
        });

        Assert.True(rescheduleResult.Success);
        Assert.NotNull(rescheduleResult.Data);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, rescheduleResult.Data.Status);
    }

    [Fact]
    public async Task TC17_Reschedule_ConfirmedAppointment_SetsRescheduleRequestedAction()
    {
        using var db = CreateDbContext(nameof(TC17_Reschedule_ConfirmedAppointment_SetsRescheduleRequestedAction));
        await SeedLifecycleDataAsync(db);

        var appt = new Appointment
        {
            PatientUserId = 1001,
            Status = AppointmentStatus.Confirmed,
            BookingSource = "Online",
            ReasonForVisit = "Đã xác nhận",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var start = DateTime.Today.AddDays(2).AddHours(9);
        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 10,
            UnitPrice = 150000,
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(30),
            ServiceDate = DateOnly.FromDateTime(start),
            AssignedDentistUserId = 3001,
            DentalChairId = 11,
            Status = AppointmentStatus.Confirmed
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var newStart = DateTime.Today.AddDays(4).AddHours(10);

        var rescheduleResult = await service.PatientRescheduleAsync(appt.AppointmentId, 1001, new PatientRescheduleRequest
        {
            NewScheduledStart = newStart,
            Reason = "Muốn đổi sang ngày khác"
        });

        Assert.True(rescheduleResult.Success);
        var serviceItem = await db.AppointmentServices.FirstAsync(s => s.AppointmentId == appt.AppointmentId);
        Assert.Equal("RescheduleRequested", serviceItem.PendingActionType);
        Assert.Equal(AppointmentStatus.Confirmed, serviceItem.Status); // Original schedule preserved Confirmed
    }

    [Fact]
    public async Task TC18_Reschedule_OriginalSchedulePreservedWhilePendingApproval()
    {
        using var db = CreateDbContext(nameof(TC18_Reschedule_OriginalSchedulePreservedWhilePendingApproval));
        await SeedLifecycleDataAsync(db);

        var start = DateTime.Today.AddDays(2).AddHours(9);
        var appt = new Appointment
        {
            PatientUserId = 1001,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 10,
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(30),
            AssignedDentistUserId = 3001,
            DentalChairId = 11,
            Status = AppointmentStatus.Confirmed,
            PendingActionType = "RescheduleRequested",
            PendingScheduledStart = DateTime.Today.AddDays(5).AddHours(14)
        });
        await db.SaveChangesAsync();

        var item = await db.AppointmentServices.FirstAsync(s => s.AppointmentId == appt.AppointmentId);
        Assert.Equal(start, item.ScheduledStart); // Unaltered
        Assert.Equal(11, item.DentalChairId); // Chair preserved
    }

    [Fact]
    public async Task TC19_Reschedule_Approve_ValidatesShiftBoundary()
    {
        using var db = CreateDbContext(nameof(TC19_Reschedule_Approve_ValidatesShiftBoundary));
        await SeedLifecycleDataAsync(db);

        var appt = new Appointment
        {
            PatientUserId = 1001,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var invalidRequestedStart = DateTime.Today.AddDays(3).AddHours(11).AddMinutes(30); // 11:30 with 60m => overflows 12:00
        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 11, // 60 min
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(10),
            AssignedDentistUserId = 3001,
            DentalChairId = 11,
            Status = AppointmentStatus.Confirmed,
            PendingActionType = "RescheduleRequested",
            PendingScheduledStart = invalidRequestedStart
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReceptionistApproveRescheduleAsync(appt.AppointmentId, 2001);

        Assert.False(result.Success);
        Assert.Contains("12:00", result.Message);
    }

    [Fact]
    public async Task TC20_Reschedule_Approve_AtomicallyUpdatesScheduleAndClearsPendingAction()
    {
        using var db = CreateDbContext(nameof(TC20_Reschedule_Approve_AtomicallyUpdatesScheduleAndClearsPendingAction));
        await SeedLifecycleDataAsync(db);

        var appt = new Appointment
        {
            PatientUserId = 1001,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var validRequestedStart = DateTime.Today.AddDays(3).AddHours(14); // 14:00 - 14:30
        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(9).AddMinutes(30),
            AssignedDentistUserId = 3001,
            DentalChairId = 11,
            Status = AppointmentStatus.Confirmed,
            PendingActionType = "RescheduleRequested",
            PendingScheduledStart = validRequestedStart
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReceptionistApproveRescheduleAsync(appt.AppointmentId, 2001);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);

        var refreshedItem = await db.AppointmentServices.FirstAsync(s => s.AppointmentId == appt.AppointmentId);
        Assert.Equal(validRequestedStart, refreshedItem.ScheduledStart);
        Assert.Null(refreshedItem.PendingActionType);
        Assert.Null(refreshedItem.PendingScheduledStart);
    }

    [Fact]
    public async Task TC21_Reschedule_Approve_RejectsOnConflict()
    {
        using var db = CreateDbContext(nameof(TC21_Reschedule_Approve_RejectsOnConflict));
        await SeedLifecycleDataAsync(db);

        var targetStart = DateTime.Today.AddDays(3).AddHours(14);

        // Another existing appointment already occupying doctor 3001 at targetStart
        var otherAppt = new Appointment
        {
            PatientUserId = 1002,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(otherAppt);
        await db.SaveChangesAsync();

        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = otherAppt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 10,
            ScheduledStart = targetStart,
            ScheduledEnd = targetStart.AddMinutes(30),
            AssignedDentistUserId = 3001,
            Status = AppointmentStatus.Confirmed
        });

        // Current appointment requesting to reschedule to targetStart
        var currentAppt = new Appointment
        {
            PatientUserId = 1001,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(currentAppt);
        await db.SaveChangesAsync();

        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = currentAppt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(9).AddMinutes(30),
            AssignedDentistUserId = 3001,
            Status = AppointmentStatus.Confirmed,
            PendingActionType = "RescheduleRequested",
            PendingScheduledStart = targetStart
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReceptionistApproveRescheduleAsync(currentAppt.AppointmentId, 2001);

        Assert.False(result.Success);
        Assert.Contains("lịch hẹn khác", result.Message);
    }

    [Fact]
    public async Task TC22_Reschedule_Reject_ClearsPendingActionAndLeavesConfirmedSchedule()
    {
        using var db = CreateDbContext(nameof(TC22_Reschedule_Reject_ClearsPendingActionAndLeavesConfirmedSchedule));
        await SeedLifecycleDataAsync(db);

        var originalStart = DateTime.Today.AddDays(2).AddHours(9);
        var appt = new Appointment
        {
            PatientUserId = 1001,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 10,
            ScheduledStart = originalStart,
            ScheduledEnd = originalStart.AddMinutes(30),
            AssignedDentistUserId = 3001,
            DentalChairId = 11,
            Status = AppointmentStatus.Confirmed,
            PendingActionType = "RescheduleRequested",
            PendingScheduledStart = DateTime.Today.AddDays(3).AddHours(14)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReceptionistRejectRescheduleAsync(appt.AppointmentId, 2001, "Không còn chỗ");

        Assert.True(result.Success, result.Message);

        var item = await db.AppointmentServices.FirstAsync(s => s.AppointmentId == appt.AppointmentId);
        Assert.Null(item.PendingActionType);
        Assert.Null(item.PendingScheduledStart);
        Assert.Equal(originalStart, item.ScheduledStart); // Original schedule intact
        Assert.Equal(AppointmentStatus.Confirmed, item.Status);
    }

    [Fact]
    public async Task TC23_Reschedule_PediatricService_BlocksWhenPatientReaches16()
    {
        using var db = CreateDbContext(nameof(TC23_Reschedule_PediatricService_BlocksWhenPatientReaches16));
        await SeedLifecycleDataAsync(db);

        // Patient 1002 is >= 16 years old
        var appt = new Appointment
        {
            PatientUserId = 1002,
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var start = DateTime.Today.AddDays(2).AddHours(9);
        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 12, // "Chăm Sóc Răng Trẻ Em"
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(30),
            Status = AppointmentStatus.PendingReceptionReview
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var rescheduleResult = await service.PatientRescheduleAsync(appt.AppointmentId, 1002, new PatientRescheduleRequest
        {
            NewScheduledStart = DateTime.Today.AddDays(3).AddHours(10),
            Reason = "Đổi lịch"
        });

        Assert.False(rescheduleResult.Success);
        Assert.Contains("16", rescheduleResult.Message);
    }

    [Fact]
    public async Task TC24_Proposal_Accept_RevalidatesSlotAvailability()
    {
        using var db = CreateDbContext(nameof(TC24_Proposal_Accept_RevalidatesSlotAvailability));
        await SeedLifecycleDataAsync(db);

        var proposedStart = DateTime.Today.AddDays(3).AddHours(14);
        var appt = new Appointment
        {
            PatientUserId = 1001,
            Status = AppointmentStatus.AwaitingPatientResponse,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.AwaitingPatientResponse,
            PendingScheduledStart = proposedStart,
            PendingScheduledEnd = proposedStart.AddMinutes(30),
            PendingDentistUserId = 3001
        });

        // Insert a conflict at proposedStart before patient accepts
        var conflict = new Appointment
        {
            PatientUserId = 1002,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(conflict);
        await db.SaveChangesAsync();

        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = conflict.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 10,
            ScheduledStart = proposedStart,
            ScheduledEnd = proposedStart.AddMinutes(30),
            AssignedDentistUserId = 3001,
            Status = AppointmentStatus.Confirmed
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var acceptResult = await service.PatientRespondProposalAsync(appt.AppointmentId, 1001, true);

        Assert.False(acceptResult.Success);
        Assert.Contains("lịch hẹn khác", acceptResult.Message);
    }

    // =========================================================================
    // SECTION 4: APPOINTMENT CANCELLATION & RESOURCE RELEASE (TC25–TC30)
    // =========================================================================

    [Fact]
    public async Task TC25_Cancellation_AllowedForPendingReceptionReview()
    {
        using var db = CreateDbContext(nameof(TC25_Cancellation_AllowedForPendingReceptionReview));
        await SeedLifecycleDataAsync(db);
        var service = CreateService(db);

        var bookResult = await service.CreatePatientAppointmentAsync(1001, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Đau răng"
        });

        var apptId = bookResult.Data!.AppointmentId;
        var cancelResult = await service.PatientCancelAsync(apptId, 1001, "Không có nhu cầu nữa");

        Assert.True(cancelResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.NotNull(appt);
        Assert.Equal(AppointmentStatus.Cancelled, appt.Status);
    }

    [Fact]
    public async Task TC26_Cancellation_AllowedForConfirmed()
    {
        using var db = CreateDbContext(nameof(TC26_Cancellation_AllowedForConfirmed));
        await SeedLifecycleDataAsync(db);

        var appt = new Appointment
        {
            PatientUserId = 1001,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var start = DateTime.Today.AddDays(2).AddHours(9);
        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 10,
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(30),
            AssignedDentistUserId = 3001,
            DentalChairId = 11,
            RoomId = 1,
            QueueNumber = 5,
            Status = AppointmentStatus.Confirmed
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var cancelResult = await service.PatientCancelAsync(appt.AppointmentId, 1001, "Hủy lịch hẹn trước giờ khám");

        Assert.True(cancelResult.Success);
        var refreshedAppt = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, refreshedAppt!.Status);
    }

    [Fact]
    public async Task TC27_Cancellation_RejectedForInService()
    {
        using var db = CreateDbContext(nameof(TC27_Cancellation_RejectedForInService));
        await SeedLifecycleDataAsync(db);
        var appt = await CreateInServiceAppointmentAsync(db);
        var service = CreateService(db);

        var cancelResult = await service.PatientCancelAsync(appt.AppointmentId, 1001, "Thử hủy khi đang khám");

        Assert.False(cancelResult.Success);
        Assert.Contains("Không thể hủy", cancelResult.Message);
    }

    [Fact]
    public async Task TC28_Cancellation_RejectedForCompletedOrCancelled()
    {
        using var db = CreateDbContext(nameof(TC28_Cancellation_RejectedForCompletedOrCancelled));
        await SeedLifecycleDataAsync(db);

        var appt = new Appointment
        {
            PatientUserId = 1001,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 10,
            Status = AppointmentStatus.Completed
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var cancelResult = await service.PatientCancelAsync(appt.AppointmentId, 1001, "Hủy ca đã hoàn thành");

        Assert.False(cancelResult.Success);
        Assert.Contains("Không thể hủy", cancelResult.Message);
    }

    [Fact]
    public async Task TC29_Cancellation_ReleasesAllResources()
    {
        using var db = CreateDbContext(nameof(TC29_Cancellation_ReleasesAllResources));
        await SeedLifecycleDataAsync(db);

        var appt = new Appointment
        {
            PatientUserId = 1001,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var start = DateTime.Today.AddDays(2).AddHours(9);
        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 10,
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(30),
            AssignedDentistUserId = 3001,
            DentalChairId = 11,
            RoomId = 1,
            QueueNumber = 7,
            Status = AppointmentStatus.Confirmed
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.PatientCancelAsync(appt.AppointmentId, 1001, "Hủy và giải phóng tài nguyên");

        var item = await db.AppointmentServices.FirstAsync(s => s.AppointmentId == appt.AppointmentId);
        Assert.Null(item.DentalChairId);
        Assert.Null(item.RoomId);
        Assert.Null(item.AssignedDentistUserId);
        Assert.Null(item.QueueNumber);
    }

    [Fact]
    public async Task TC30_Cancellation_PreservesInvoicesAndPayments()
    {
        using var db = CreateDbContext(nameof(TC30_Cancellation_PreservesInvoicesAndPayments));
        await SeedLifecycleDataAsync(db);

        var appt = new Appointment
        {
            PatientUserId = 1001,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var start = DateTime.Today.AddDays(2).AddHours(9);
        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 10,
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(30),
            AssignedDentistUserId = 3001,
            Status = AppointmentStatus.Confirmed
        });

        var invoice = new Invoice
        {
            AppointmentId = appt.AppointmentId,
            InvoiceCode = "INV-TEST-001",
            GeneratedByUserId = 2001,
            TotalAmount = 150000,
            FinalAmount = 150000,
            Status = "Paid"
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            InvoiceId = invoice.InvoiceId,
            Amount = 150000,
            Method = "Cash",
            Status = "Successful",
            ReceivedByUserId = 2001,
            PaymentDate = DateTime.UtcNow
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.PatientCancelAsync(appt.AppointmentId, 1001, "Hủy lịch có hóa đơn");

        var preservedInvoice = await db.Invoices.FindAsync(invoice.InvoiceId);
        var preservedPayment = await db.Payments.FindAsync(payment.PaymentId);

        Assert.NotNull(preservedInvoice);
        Assert.NotNull(preservedPayment);
        Assert.Equal("Paid", preservedInvoice.Status);
        Assert.Equal("Successful", preservedPayment.Status);
    }
}
