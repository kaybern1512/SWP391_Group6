using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.FollowUp.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DentalClinic.Tests.FollowUp;

public class FollowUpSchedulingServiceTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedDoctorAndPatientAsync(DentalClinicDbContext db, long doctorId = 20, long patientId = 10)
    {
        // Doctor
        var docUser = new UserAccount
        {
            UserId = doctorId,
            Email = "doctorA@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var staffProfile = new StaffProfile
        {
            UserId = doctorId,
            FullName = "Bác Sĩ Nguyễn Văn A",
            EmployeeCode = "DOC001",
            EmploymentStatus = "Active",
            User = docUser
        };
        var docProfile = new DentistProfile
        {
            UserId = doctorId,
            Qualification = "Thạc sĩ",
            User = staffProfile
        };
        db.UserAccounts.Add(docUser);
        db.StaffProfiles.Add(staffProfile);
        db.DentistProfiles.Add(docProfile);

        // Patient
        var patUser = new UserAccount
        {
            UserId = patientId,
            Email = "patientB@test.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patProfile = new PatientProfile
        {
            UserId = patientId,
            PatientCode = "PAT001",
            FullName = "Bệnh Nhân Trần B",
            User = patUser
        };
        db.UserAccounts.Add(patUser);
        db.PatientProfiles.Add(patProfile);

        await db.SaveChangesAsync();
    }

    private static async Task<DateTime> SeedDoctorScheduleAsync(DentalClinicDbContext db, long doctorId = 20, int daysFromToday = 2)
    {
        var targetDate = DateTime.Today.AddDays(daysFromToday);
        db.DentistWorkSchedules.AddRange(
            new DentistWorkSchedule
            {
                DentistUserId = doctorId,
                DepartmentId = 1,
                WorkDate = DateOnly.FromDateTime(targetDate),
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                Status = "Scheduled"
            },
            new DentistWorkSchedule
            {
                DentistUserId = doctorId,
                DepartmentId = 1,
                WorkDate = DateOnly.FromDateTime(targetDate),
                StartTime = new TimeOnly(13, 0),
                EndTime = new TimeOnly(17, 0),
                Status = "Scheduled"
            }
        );
        await db.SaveChangesAsync();
        return targetDate;
    }

    [Fact]
    public async Task ValidateFollowUpSlot_PastTime_ReturnsFailPastTime()
    {
        var db = CreateDbContext(nameof(ValidateFollowUpSlot_PastTime_ReturnsFailPastTime));
        await SeedDoctorAndPatientAsync(db);
        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var pastStart = DateTime.Now.AddHours(-2);
        var pastEnd = pastStart.AddMinutes(30);

        var result = await service.ValidateFollowUpSlotAsync(20, 10, pastStart, pastEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.PastTime, result.FailureReason);
    }

    [Fact]
    public async Task ValidateFollowUpSlot_LessThan30MinBuffer_ReturnsFailTooCloseToPresent()
    {
        var db = CreateDbContext(nameof(ValidateFollowUpSlot_LessThan30MinBuffer_ReturnsFailTooCloseToPresent));
        await SeedDoctorAndPatientAsync(db);
        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var soonStart = DateTime.Now.AddMinutes(15);
        var soonEnd = soonStart.AddMinutes(30);

        var result = await service.ValidateFollowUpSlotAsync(20, 10, soonStart, soonEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.TooCloseToPresent, result.FailureReason);
    }

    [Fact]
    public async Task ValidateFollowUpSlot_InvalidDuration_ReturnsFailInvalidTimeRange()
    {
        var db = CreateDbContext(nameof(ValidateFollowUpSlot_InvalidDuration_ReturnsFailInvalidTimeRange));
        await SeedDoctorAndPatientAsync(db);
        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var futureStart = DateTime.Today.AddDays(2).AddHours(9);
        var invalidEnd = futureStart.AddMinutes(5); // Less than 10 minutes

        var result = await service.ValidateFollowUpSlotAsync(20, 10, futureStart, invalidEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.InvalidTimeRange, result.FailureReason);
    }

    [Fact]
    public async Task ValidateFollowUpSlot_NoWorkScheduleOnDate_ReturnsFailOutsideDoctorShift()
    {
        var db = CreateDbContext(nameof(ValidateFollowUpSlot_NoWorkScheduleOnDate_ReturnsFailOutsideDoctorShift));
        await SeedDoctorAndPatientAsync(db);
        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var futureStart = DateTime.Today.AddDays(3).AddHours(9);
        var futureEnd = futureStart.AddMinutes(30);

        var result = await service.ValidateFollowUpSlotAsync(20, 10, futureStart, futureEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.OutsideDoctorShift, result.FailureReason);
    }

    [Fact]
    public async Task ValidateFollowUpSlot_OutsideShiftHours_ReturnsFailOutsideDoctorShift()
    {
        var db = CreateDbContext(nameof(ValidateFollowUpSlot_OutsideShiftHours_ReturnsFailOutsideDoctorShift));
        await SeedDoctorAndPatientAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(targetDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // Requested 14:00 - 14:30 (outside 08:00 - 12:00)
        var afternoonStart = targetDate.AddHours(14);
        var afternoonEnd = afternoonStart.AddMinutes(30);

        var result = await service.ValidateFollowUpSlotAsync(20, 10, afternoonStart, afternoonEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.OutsideDoctorShift, result.FailureReason);
    }

    [Fact]
    public async Task ValidateFollowUpSlot_SpanningAcrossLunchBreak_ReturnsFailOutsideDoctorShift()
    {
        var db = CreateDbContext(nameof(ValidateFollowUpSlot_SpanningAcrossLunchBreak_ReturnsFailOutsideDoctorShift));
        await SeedDoctorAndPatientAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        // Morning shift 08:00 - 12:00, Afternoon shift 13:30 - 17:30
        db.DentistWorkSchedules.AddRange(
            new DentistWorkSchedule
            {
                DentistScheduleId = 1,
                DentistUserId = 20,
                DepartmentId = 1,
                WorkDate = DateOnly.FromDateTime(targetDate),
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                Status = "Scheduled"
            },
            new DentistWorkSchedule
            {
                DentistScheduleId = 2,
                DentistUserId = 20,
                DepartmentId = 1,
                WorkDate = DateOnly.FromDateTime(targetDate),
                StartTime = new TimeOnly(13, 30),
                EndTime = new TimeOnly(17, 30),
                Status = "Scheduled"
            }
        );
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // Spanning 11:45 to 12:30 (crosses lunch break 12:00 - 13:30)
        var crossStart = targetDate.AddHours(11).AddMinutes(45);
        var crossEnd = targetDate.AddHours(12).AddMinutes(30);

        var result = await service.ValidateFollowUpSlotAsync(20, 10, crossStart, crossEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.OutsideDoctorShift, result.FailureReason);
    }

    [Fact]
    public async Task ValidateFollowUpSlot_DoctorOnLeave_ReturnsFailDoctorUnavailableOrOnLeave()
    {
        var db = CreateDbContext(nameof(ValidateFollowUpSlot_DoctorOnLeave_ReturnsFailDoctorUnavailableOrOnLeave));
        await SeedDoctorAndPatientAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(targetDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });

        // Doctor is on leave during 09:00 - 11:00
        db.DentistAvailabilities.Add(new DentistAvailability
        {
            AvailabilityId = 1,
            DentistUserId = 20,
            StartDateTime = targetDate.AddHours(9),
            EndDateTime = targetDate.AddHours(11),
            AvailabilityStatus = DentistAvailabilityStatus.OnLeave,
            Reason = "Nghỉ phép cá nhân"
        });
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var slotStart = targetDate.AddHours(9).AddMinutes(30);
        var slotEnd = slotStart.AddMinutes(30);

        var result = await service.ValidateFollowUpSlotAsync(20, 10, slotStart, slotEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.DoctorUnavailableOrOnLeave, result.FailureReason);
    }

    [Fact]
    public async Task ValidateFollowUpSlot_DoctorConflictingAppointment_ReturnsFailDoctorSlotConflict()
    {
        var db = CreateDbContext(nameof(ValidateFollowUpSlot_DoctorConflictingAppointment_ReturnsFailDoctorSlotConflict));
        await SeedDoctorAndPatientAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(targetDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });

        // Existing Confirmed appointment from 09:00 to 09:45
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 50,
            AppointmentCode = "APT-50",
            PatientUserId = 999,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(45),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Khám răng định kỳ",
            BookingSource = BookingSource.Patient
        });
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // Overlapping slot 09:30 - 10:00 (start 09:30 < 09:45 && end 10:00 > 09:00)
        var slotStart = targetDate.AddHours(9).AddMinutes(30);
        var slotEnd = targetDate.AddHours(10);

        var result = await service.ValidateFollowUpSlotAsync(20, 10, slotStart, slotEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.DoctorSlotConflict, result.FailureReason);
    }

    [Fact]
    public async Task ValidateFollowUpSlot_DoctorCancelledAppointment_ReturnsSuccess()
    {
        var db = CreateDbContext(nameof(ValidateFollowUpSlot_DoctorCancelledAppointment_ReturnsSuccess));
        await SeedDoctorAndPatientAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(targetDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });

        // Cancelled appointment does NOT occupy slot
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 51,
            AppointmentCode = "APT-51",
            PatientUserId = 999,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(45),
            Status = AppointmentStatus.Cancelled,
            ReasonForVisit = "Đã hủy",
            BookingSource = BookingSource.Patient
        });
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var slotStart = targetDate.AddHours(9);
        var slotEnd = slotStart.AddMinutes(30);

        var result = await service.ValidateFollowUpSlotAsync(20, 10, slotStart, slotEnd);

        Assert.True(result.IsValid);
        Assert.Equal(SchedulingFailureReason.None, result.FailureReason);
    }

    [Fact]
    public async Task ValidateFollowUpSlot_PatientConflictingAppointment_ReturnsFailPatientSlotConflict()
    {
        var db = CreateDbContext(nameof(ValidateFollowUpSlot_PatientConflictingAppointment_ReturnsFailPatientSlotConflict));
        await SeedDoctorAndPatientAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(targetDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });

        // Patient 10 already has a Confirmed appointment with doctor 30 at 09:00 - 09:45
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 52,
            AppointmentCode = "APT-52",
            PatientUserId = 10,
            AssignedDentistUserId = 30,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(45),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Nhổ răng khôn",
            BookingSource = BookingSource.Patient
        });
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var slotStart = targetDate.AddHours(9).AddMinutes(15);
        var slotEnd = targetDate.AddHours(9).AddMinutes(45);

        var result = await service.ValidateFollowUpSlotAsync(20, 10, slotStart, slotEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.PatientSlotConflict, result.FailureReason);
    }

    [Fact]
    public async Task ValidateFollowUpSlot_ExcludeCurrentAppointment_ReturnsSuccess()
    {
        var db = CreateDbContext(nameof(ValidateFollowUpSlot_ExcludeCurrentAppointment_ReturnsSuccess));
        await SeedDoctorAndPatientAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(targetDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });

        // Current appointment #53 is being rescheduled to a slightly shifted time
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 53,
            AppointmentCode = "APT-53",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám niềng răng",
            BookingSource = BookingSource.Patient
        });
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // Rescheduling appointment 53 to 09:15 - 09:45
        var slotStart = targetDate.AddHours(9).AddMinutes(15);
        var slotEnd = targetDate.AddHours(9).AddMinutes(45);

        var result = await service.ValidateFollowUpSlotAsync(20, 10, slotStart, slotEnd, excludeAppointmentId: 53);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task GetDoctorAvailableSlots_GeneratesSlotsWithinShift_SkipsConflicts()
    {
        var db = CreateDbContext(nameof(GetDoctorAvailableSlots_GeneratesSlotsWithinShift_SkipsConflicts));
        await SeedDoctorAndPatientAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        // Shift: 08:00 - 10:00 (4 slots of 30 mins: 08:00, 08:30, 09:00, 09:30)
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(targetDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(10, 0),
            Status = "Scheduled"
        });

        // Appointment occupies 08:30 - 09:00
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 60,
            AppointmentCode = "APT-60",
            PatientUserId = 999,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddHours(8).AddMinutes(30),
            ScheduledEnd = targetDate.AddHours(9),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Trám răng",
            BookingSource = BookingSource.Patient
        });
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var response = await service.GetDoctorAvailableSlotsAsync(20, targetDate, 30);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        // Expecting 3 slots: 08:00, 09:00, 09:30 (08:30 is skipped)
        Assert.Equal(3, response.Data.Count);
        Assert.Equal(targetDate.AddHours(8), response.Data[0].StartTime);
        Assert.Equal(targetDate.AddHours(9), response.Data[1].StartTime);
        Assert.Equal(targetDate.AddHours(9).AddMinutes(30), response.Data[2].StartTime);
    }

    [Fact]
    public async Task CreateDoctorFollowUpAppointment_ValidRequest_CreatesAppointmentAndFollowUp()
    {
        var db = CreateDbContext(nameof(CreateDoctorFollowUpAppointment_ValidRequest_CreatesAppointmentAndFollowUp));
        await SeedDoctorAndPatientAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(targetDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });

        // Add a treatment plan and session for patient 10
        var plan = new TreatmentPlan
        {
            TreatmentPlanId = 1,
            PatientUserId = 10,
            CreatedByDentistUserId = 20,
            Status = "Active"
        };
        db.TreatmentPlans.Add(plan);
        var item = new TreatmentPlanItem
        {
            TreatmentPlanItemId = 1,
            TreatmentPlanId = 1,
            Status = "InProgress",
            EstimatedCost = 1000000
        };
        db.TreatmentPlanItems.Add(item);
        var session = new TreatmentSession
        {
            TreatmentSessionId = 1,
            TreatmentPlanItemId = 1,
            AppointmentId = 9999,
            DentistUserId = 20,
            SessionDate = DateTime.UtcNow.AddDays(-1),
            TreatmentResult = "Thành công"
        };
        db.TreatmentSessions.Add(session);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var request = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = 10,
            TreatmentSessionId = 1,
            DepartmentId = 1,
            ScheduledStart = targetDate.AddHours(8).AddMinutes(30),
            ScheduledEnd = targetDate.AddHours(9),
            Reason = "Kiểm tra tiến độ phục hồi sau nhổ răng"
        };

        var response = await service.CreateDoctorFollowUpAppointmentAsync(20, request);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(10, response.Data.PatientUserId);
        Assert.Equal(20, response.Data.DentistUserId);
        Assert.Equal(AppointmentStatus.Confirmed, response.Data.Status);

        // Verify database persistence
        var savedApt = await db.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == response.Data.AppointmentId);
        Assert.NotNull(savedApt);
        Assert.Equal(AppointmentStatus.Confirmed, savedApt.Status);

        var savedFollowUp = await db.FollowUpSchedules.FirstOrDefaultAsync(f => f.AppointmentId == savedApt.AppointmentId);
        Assert.NotNull(savedFollowUp);
        Assert.Equal("Scheduled", savedFollowUp.Status);
    }

    [Fact]
    public async Task CreateDoctorFollowUpAppointment_ConflictingSlot_Fails()
    {
        var db = CreateDbContext(nameof(CreateDoctorFollowUpAppointment_ConflictingSlot_Fails));
        await SeedDoctorAndPatientAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(targetDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });

        // Existing conflicting appointment for doctor
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 70,
            AppointmentCode = "APT-70",
            PatientUserId = 999,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Khám tổng quát",
            BookingSource = BookingSource.Patient
        });
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var request = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = 10,
            ScheduledStart = targetDate.AddHours(9).AddMinutes(15),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(45),
            Reason = "Tái khám"
        };

        var response = await service.CreateDoctorFollowUpAppointmentAsync(20, request);

        Assert.False(response.Success);
        Assert.Contains("trùng", response.Message);
    }

    [Fact]
    public async Task CreateDoctorFollowUpAppointment_PastTime_Fails()
    {
        var db = CreateDbContext(nameof(CreateDoctorFollowUpAppointment_PastTime_Fails));
        await SeedDoctorAndPatientAsync(db);

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var request = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = 10,
            ScheduledStart = DateTime.Today.AddDays(-1).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(-1).AddHours(9).AddMinutes(30),
            Reason = "Tái khám quá khứ"
        };

        var response = await service.CreateDoctorFollowUpAppointmentAsync(20, request);

        Assert.False(response.Success);
        Assert.Contains("quá khứ", response.Message);
    }

    // ==========================================
    // DAY 04: PATIENT CHANGE PROPOSALS TESTS
    // ==========================================

    [Fact]
    public async Task SubmitPatientChangeProposal_ValidReschedule_Success()
    {
        var db = CreateDbContext(nameof(SubmitPatientChangeProposal_ValidReschedule_Success));
        await SeedDoctorAndPatientAsync(db, doctorId: 20, patientId: 10);
        var targetDate = await SeedDoctorScheduleAsync(db, doctorId: 20, daysFromToday: 5);

        // Active confirmed appointment
        var appt = new Appointment
        {
            AppointmentId = 101,
            AppointmentCode = "APT-101",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám kiểm tra vết thương",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var req = new PatientFollowUpChangeRequest
        {
            AppointmentId = 101,
            ProposedStart = targetDate.AddHours(10),
            ProposedEnd = targetDate.AddHours(10).AddMinutes(30),
            Reason = "Bận việc đột xuất sáng 9h, xin dời sang 10h",
            IsRebook = false
        };

        var response = await service.SubmitPatientChangeProposalAsync(10, req);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(ProposalStatus.Pending, response.Data.Status);
        Assert.Equal(101, response.Data.AppointmentId);
        Assert.False(response.Data.IsRebook);
    }

    [Fact]
    public async Task SubmitPatientChangeProposal_UnauthorizedPatient_Fails()
    {
        var db = CreateDbContext(nameof(SubmitPatientChangeProposal_UnauthorizedPatient_Fails));
        await SeedDoctorAndPatientAsync(db, doctorId: 20, patientId: 10);
        var targetDate = await SeedDoctorScheduleAsync(db, doctorId: 20, daysFromToday: 5);

        var appt = new Appointment
        {
            AppointmentId = 102,
            AppointmentCode = "APT-102",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // Attacker patientId = 999 tries to propose on appointment belonging to patientId = 10
        var req = new PatientFollowUpChangeRequest
        {
            AppointmentId = 102,
            ProposedStart = targetDate.AddHours(10),
            ProposedEnd = targetDate.AddHours(10).AddMinutes(30),
            Reason = "IDOR attack attempt",
            IsRebook = false
        };

        var response = await service.SubmitPatientChangeProposalAsync(999, req);

        Assert.False(response.Success);
        Assert.Contains("không có quyền", response.Message);
    }

    [Fact]
    public async Task SubmitPatientChangeProposal_DuplicatePending_Fails()
    {
        var db = CreateDbContext(nameof(SubmitPatientChangeProposal_DuplicatePending_Fails));
        await SeedDoctorAndPatientAsync(db, doctorId: 20, patientId: 10);
        var targetDate = await SeedDoctorScheduleAsync(db, doctorId: 20, daysFromToday: 5);

        var appt = new Appointment
        {
            AppointmentId = 103,
            AppointmentCode = "APT-103",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);

        // Existing pending proposal
        db.AppointmentChangeProposals.Add(new AppointmentChangeProposal
        {
            AppointmentId = 103,
            ProposedByUserId = 10,
            ProposedStart = targetDate.AddHours(14),
            ProposedEnd = targetDate.AddHours(14).AddMinutes(30),
            Reason = "Đã gửi yêu cầu trước đó",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var req = new PatientFollowUpChangeRequest
        {
            AppointmentId = 103,
            ProposedStart = targetDate.AddHours(15),
            ProposedEnd = targetDate.AddHours(15).AddMinutes(30),
            Reason = "Gửi thêm yêu cầu thứ 2 khi cái 1 chưa duyệt",
            IsRebook = false
        };

        var response = await service.SubmitPatientChangeProposalAsync(10, req);

        Assert.False(response.Success);
        Assert.Contains("đang có một yêu cầu thay đổi đang chờ", response.Message);
    }

    [Fact]
    public async Task SubmitPatientChangeProposal_PastTime_Fails()
    {
        var db = CreateDbContext(nameof(SubmitPatientChangeProposal_PastTime_Fails));
        await SeedDoctorAndPatientAsync(db, doctorId: 20, patientId: 10);

        var appt = new Appointment
        {
            AppointmentId = 104,
            AppointmentCode = "APT-104",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = DateTime.Today.AddDays(3).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(3).AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var req = new PatientFollowUpChangeRequest
        {
            AppointmentId = 104,
            ProposedStart = DateTime.Today.AddDays(-1).AddHours(10),
            ProposedEnd = DateTime.Today.AddDays(-1).AddHours(10).AddMinutes(30),
            Reason = "Đề xuất thời gian quá khứ",
            IsRebook = false
        };

        var response = await service.SubmitPatientChangeProposalAsync(10, req);

        Assert.False(response.Success);
        Assert.Contains("ít nhất 30 phút", response.Message);
    }

    [Fact]
    public async Task SubmitPatientChangeProposal_ValidRebook_Success()
    {
        var db = CreateDbContext(nameof(SubmitPatientChangeProposal_ValidRebook_Success));
        await SeedDoctorAndPatientAsync(db, doctorId: 20, patientId: 10);
        var targetDate = await SeedDoctorScheduleAsync(db, doctorId: 20, daysFromToday: 6);

        // Cancelled appointment
        var appt = new Appointment
        {
            AppointmentId = 105,
            AppointmentCode = "APT-105",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddDays(-2).AddHours(9),
            ScheduledEnd = targetDate.AddDays(-2).AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Cancelled,
            ReasonForVisit = "Tái khám đã bị hủy",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var req = new PatientFollowUpChangeRequest
        {
            AppointmentId = 105,
            ProposedStart = targetDate.AddHours(9),
            ProposedEnd = targetDate.AddHours(9).AddMinutes(30),
            Reason = "Lần trước bận thi cử phải hủy, nay xin đặt lại lịch tái khám",
            IsRebook = true
        };

        var response = await service.SubmitPatientChangeProposalAsync(10, req);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.True(response.Data.IsRebook);
        Assert.Equal(ProposalStatus.Pending, response.Data.Status);
    }

    [Fact]
    public async Task WithdrawPatientProposal_Pending_Success()
    {
        var db = CreateDbContext(nameof(WithdrawPatientProposal_Pending_Success));
        await SeedDoctorAndPatientAsync(db, doctorId: 20, patientId: 10);

        var appt = new Appointment
        {
            AppointmentId = 106,
            AppointmentCode = "APT-106",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = DateTime.Today.AddDays(4).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(4).AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);

        var proposal = new AppointmentChangeProposal
        {
            ProposalId = 501,
            AppointmentId = 106,
            ProposedByUserId = 10,
            ProposedStart = DateTime.Today.AddDays(4).AddHours(14),
            ProposedEnd = DateTime.Today.AddDays(4).AddHours(14).AddMinutes(30),
            Reason = "Đổi lịch chiều",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Appointment = appt
        };
        db.AppointmentChangeProposals.Add(proposal);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var response = await service.WithdrawPatientProposalAsync(10, 501);

        Assert.True(response.Success);
        var updated = await db.AppointmentChangeProposals.FindAsync((long)501);
        Assert.NotNull(updated);
        Assert.Equal(ProposalStatus.Expired, updated.Status);
    }

    [Fact]
    public async Task WithdrawPatientProposal_AlreadyResolved_Fails()
    {
        var db = CreateDbContext(nameof(WithdrawPatientProposal_AlreadyResolved_Fails));
        await SeedDoctorAndPatientAsync(db, doctorId: 20, patientId: 10);

        var appt = new Appointment
        {
            AppointmentId = 107,
            AppointmentCode = "APT-107",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = DateTime.Today.AddDays(4).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(4).AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);

        var proposal = new AppointmentChangeProposal
        {
            ProposalId = 502,
            AppointmentId = 107,
            ProposedByUserId = 10,
            ProposedStart = DateTime.Today.AddDays(4).AddHours(14),
            ProposedEnd = DateTime.Today.AddDays(4).AddHours(14).AddMinutes(30),
            Reason = "Đã được duyệt",
            Status = ProposalStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            Appointment = appt
        };
        db.AppointmentChangeProposals.Add(proposal);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var response = await service.WithdrawPatientProposalAsync(10, 502);

        Assert.False(response.Success);
        Assert.Contains("Chỉ có thể rút yêu cầu đang ở trạng thái 'Chờ duyệt'", response.Message);
    }

    // ==========================================
    // DAY 05: DOCTOR APPROVAL & REJECTION TESTS
    // ==========================================

    [Fact]
    public async Task ResolveProposal_ApproveReschedule_UpdatesAppointmentAndProposal()
    {
        var db = CreateDbContext(nameof(ResolveProposal_ApproveReschedule_UpdatesAppointmentAndProposal));
        await SeedDoctorAndPatientAsync(db, doctorId: 20, patientId: 10);
        var targetDate = await SeedDoctorScheduleAsync(db, doctorId: 20, daysFromToday: 7);

        var appt = new Appointment
        {
            AppointmentId = 201,
            AppointmentCode = "APT-201",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddHours(8).AddMinutes(30),
            ScheduledEnd = targetDate.AddHours(9),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám sau nhổ răng",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);

        var proposal = new AppointmentChangeProposal
        {
            ProposalId = 601,
            AppointmentId = 201,
            ProposedByUserId = 10,
            ProposedDentistUserId = 20,
            ProposedStart = targetDate.AddHours(10),
            ProposedEnd = targetDate.AddHours(10).AddMinutes(30),
            Reason = "[Đổi lịch] Xin đổi sang 10h",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Appointment = appt
        };
        db.AppointmentChangeProposals.Add(proposal);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var resolveReq = new ResolveProposalRequest
        {
            ProposalId = 601,
            IsApproved = true
        };

        var response = await service.ResolveProposalAsync(20, resolveReq);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(ProposalStatus.Accepted, response.Data.Status);

        // Verify appointment was rescheduled
        var updatedAppt = await db.Appointments.FindAsync((long)201);
        Assert.NotNull(updatedAppt);
        Assert.Equal(targetDate.AddHours(10), updatedAppt.ScheduledStart);
        Assert.Equal(AppointmentStatus.Confirmed, updatedAppt.Status);
    }

    [Fact]
    public async Task ResolveProposal_ApproveRebook_CreatesNewAppointmentAndPreservesOld()
    {
        var db = CreateDbContext(nameof(ResolveProposal_ApproveRebook_CreatesNewAppointmentAndPreservesOld));
        await SeedDoctorAndPatientAsync(db, doctorId: 20, patientId: 10);
        var targetDate = await SeedDoctorScheduleAsync(db, doctorId: 20, daysFromToday: 8);

        // Cancelled old appointment
        var oldAppt = new Appointment
        {
            AppointmentId = 202,
            AppointmentCode = "APT-202",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddDays(-3).AddHours(9),
            ScheduledEnd = targetDate.AddDays(-3).AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Cancelled,
            ReasonForVisit = "Tái khám đã bị hủy",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(oldAppt);

        var proposal = new AppointmentChangeProposal
        {
            ProposalId = 602,
            AppointmentId = 202,
            ProposedByUserId = 10,
            ProposedDentistUserId = 20,
            ProposedStart = targetDate.AddHours(11),
            ProposedEnd = targetDate.AddHours(11).AddMinutes(30),
            Reason = "[Đặt lại lịch] Xin đặt lại lịch tái khám",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Appointment = oldAppt
        };
        db.AppointmentChangeProposals.Add(proposal);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var resolveReq = new ResolveProposalRequest
        {
            ProposalId = 602,
            IsApproved = true
        };

        var response = await service.ResolveProposalAsync(20, resolveReq);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(ProposalStatus.Accepted, response.Data.Status);
        Assert.True(response.Data.IsRebook);

        // Verify old appointment was preserved untouched
        var oldApptCheck = await db.Appointments.FindAsync((long)202);
        Assert.NotNull(oldApptCheck);
        Assert.Equal(AppointmentStatus.Cancelled, oldApptCheck.Status);

        // Verify a NEW appointment was created
        var newAppts = await db.Appointments.Where(a => a.AppointmentId != 202 && a.PatientUserId == 10).ToListAsync();
        Assert.Single(newAppts);
        var newAppt = newAppts.First();
        Assert.Equal(AppointmentStatus.Confirmed, newAppt.Status);
        Assert.Equal(targetDate.AddHours(11), newAppt.ScheduledStart);
    }

    [Fact]
    public async Task ResolveProposal_Reject_SetsRejectedAndPreservesAppointment()
    {
        var db = CreateDbContext(nameof(ResolveProposal_Reject_SetsRejectedAndPreservesAppointment));
        await SeedDoctorAndPatientAsync(db, doctorId: 20, patientId: 10);
        var targetDate = await SeedDoctorScheduleAsync(db, doctorId: 20, daysFromToday: 9);

        var originalStart = targetDate.AddHours(9);
        var appt = new Appointment
        {
            AppointmentId = 203,
            AppointmentCode = "APT-203",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = originalStart,
            ScheduledEnd = originalStart.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);

        var proposal = new AppointmentChangeProposal
        {
            ProposalId = 603,
            AppointmentId = 203,
            ProposedByUserId = 10,
            ProposedDentistUserId = 20,
            ProposedStart = targetDate.AddHours(15),
            ProposedEnd = targetDate.AddHours(15).AddMinutes(30),
            Reason = "[Đổi lịch] Xin đổi sang chiều",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Appointment = appt
        };
        db.AppointmentChangeProposals.Add(proposal);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var resolveReq = new ResolveProposalRequest
        {
            ProposalId = 603,
            IsApproved = false,
            RejectionReason = "Bác sĩ bận phẫu thuật ca phức tạp vào chiều hôm đó"
        };

        var response = await service.ResolveProposalAsync(20, resolveReq);

        Assert.True(response.Success);
        Assert.Equal(ProposalStatus.Rejected, response.Data!.Status);

        // Verify appointment was kept untouched
        var apptCheck = await db.Appointments.FindAsync((long)203);
        Assert.NotNull(apptCheck);
        Assert.Equal(originalStart, apptCheck.ScheduledStart);
        Assert.Equal(AppointmentStatus.Confirmed, apptCheck.Status);
    }

    [Fact]
    public async Task ResolveProposal_UnauthorizedDoctor_Fails()
    {
        var db = CreateDbContext(nameof(ResolveProposal_UnauthorizedDoctor_Fails));
        await SeedDoctorAndPatientAsync(db, doctorId: 20, patientId: 10);

        var appt = new Appointment
        {
            AppointmentId = 204,
            AppointmentCode = "APT-204",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = DateTime.Today.AddDays(5).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(5).AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);

        var proposal = new AppointmentChangeProposal
        {
            ProposalId = 604,
            AppointmentId = 204,
            ProposedByUserId = 10,
            ProposedDentistUserId = 20,
            ProposedStart = DateTime.Today.AddDays(5).AddHours(10),
            ProposedEnd = DateTime.Today.AddDays(5).AddHours(10).AddMinutes(30),
            Reason = "[Đổi lịch]",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Appointment = appt
        };
        db.AppointmentChangeProposals.Add(proposal);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // Different doctor ID = 888 tries to resolve proposal for doctor 20
        var resolveReq = new ResolveProposalRequest
        {
            ProposalId = 604,
            IsApproved = true
        };

        var response = await service.ResolveProposalAsync(888, resolveReq);

        Assert.False(response.Success);
        Assert.Contains("không có quyền", response.Message);
    }

    [Fact]
    public async Task ResolveProposal_NotPending_Fails()
    {
        var db = CreateDbContext(nameof(ResolveProposal_NotPending_Fails));
        await SeedDoctorAndPatientAsync(db, doctorId: 20, patientId: 10);

        var appt = new Appointment
        {
            AppointmentId = 205,
            AppointmentCode = "APT-205",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = DateTime.Today.AddDays(6).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(6).AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);

        var proposal = new AppointmentChangeProposal
        {
            ProposalId = 605,
            AppointmentId = 205,
            ProposedByUserId = 10,
            ProposedDentistUserId = 20,
            ProposedStart = DateTime.Today.AddDays(6).AddHours(10),
            ProposedEnd = DateTime.Today.AddDays(6).AddHours(10).AddMinutes(30),
            Reason = "[Đổi lịch]",
            Status = ProposalStatus.Accepted, // Already resolved
            CreatedAt = DateTime.UtcNow,
            Appointment = appt
        };
        db.AppointmentChangeProposals.Add(proposal);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var resolveReq = new ResolveProposalRequest
        {
            ProposalId = 605,
            IsApproved = true
        };

        var response = await service.ResolveProposalAsync(20, resolveReq);

        Assert.False(response.Success);
        Assert.Contains("đã được xử lý trước đó", response.Message);
    }

    [Fact]
    public async Task ResolveProposal_DoctorSlotConflict_FailsAndRollsBack()
    {
        var db = CreateDbContext(nameof(ResolveProposal_DoctorSlotConflict_FailsAndRollsBack));
        await SeedDoctorAndPatientAsync(db, doctorId: 20, patientId: 10);
        var targetDate = await SeedDoctorScheduleAsync(db, doctorId: 20, daysFromToday: 10);

        var appt = new Appointment
        {
            AppointmentId = 206,
            AppointmentCode = "APT-206",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddHours(8).AddMinutes(30),
            ScheduledEnd = targetDate.AddHours(9),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);

        // Another appointment booked into 10:00 - 10:30 by someone else
        var conflictingAppt = new Appointment
        {
            AppointmentId = 777,
            AppointmentCode = "APT-777",
            PatientUserId = 99,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddHours(10),
            ScheduledEnd = targetDate.AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Khám khác",
            BookingSource = BookingSource.Patient
        };
        db.Appointments.Add(conflictingAppt);

        var proposal = new AppointmentChangeProposal
        {
            ProposalId = 606,
            AppointmentId = 206,
            ProposedByUserId = 10,
            ProposedDentistUserId = 20,
            ProposedStart = targetDate.AddHours(10),
            ProposedEnd = targetDate.AddHours(10).AddMinutes(30),
            Reason = "[Đổi lịch] Muốn đổi sang 10h nhưng 10h vừa bị chiếm",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Appointment = appt
        };
        db.AppointmentChangeProposals.Add(proposal);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var resolveReq = new ResolveProposalRequest
        {
            ProposalId = 606,
            IsApproved = true
        };

        var response = await service.ResolveProposalAsync(20, resolveReq);

        Assert.False(response.Success);
        Assert.NotNull(response.Message);
        Assert.True(response.Message.Contains("trùng lịch") || response.Message.Contains("đã có cuộc hẹn") || response.Message.Contains("không còn khả dụng"));
    }

    [Fact]
    public async Task GetReceptionistReminders_ReturnsExpectedList_ExcludesCancelled()
    {
        using var db = CreateDbContext("Reminders_Test1");
        var today = DateTime.Today;

        var apptValid = new Appointment
        {
            AppointmentId = 801,
            AppointmentCode = "APT-REM-01",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = today.AddHours(9),
            ScheduledEnd = today.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám kiểm tra tủy răng",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(apptValid);

        var followUpValid = new FollowUpSchedule
        {
            FollowUpId = 901,
            AppointmentId = 801,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Scheduled",
            Reason = "Tái khám tủy",
            Appointment = apptValid
        };
        db.FollowUpSchedules.Add(followUpValid);

        var apptCancelled = new Appointment
        {
            AppointmentId = 802,
            AppointmentCode = "APT-REM-02",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = today.AddHours(11),
            ScheduledEnd = today.AddHours(11).AddMinutes(30),
            Status = AppointmentStatus.Cancelled,
            ReasonForVisit = "Lịch đã hủy",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(apptCancelled);

        var followUpCancelled = new FollowUpSchedule
        {
            FollowUpId = 902,
            AppointmentId = 802,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Scheduled",
            Reason = "Lịch hủy",
            Appointment = apptCancelled
        };
        db.FollowUpSchedules.Add(followUpCancelled);

        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var response = await service.GetReceptionistRemindersAsync();

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Contains(response.Data, r => r.FollowUpId == 901);
        Assert.DoesNotContain(response.Data, r => r.FollowUpId == 902);
    }

    [Fact]
    public async Task GetReceptionistReminders_CompletedFollowUpWithoutAppointment_IsExcludedFromReminders()
    {
        using var db = CreateDbContext("Reminders_CompletedUnlinked");
        var today = DateTime.Today;

        var followUpCompletedUnlinked = new FollowUpSchedule
        {
            FollowUpId = 905,
            AppointmentId = null,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Completed",
            Reason = "Đã hoàn thành tái khám"
        };
        db.FollowUpSchedules.Add(followUpCompletedUnlinked);

        var followUpPendingUnlinked = new FollowUpSchedule
        {
            FollowUpId = 906,
            AppointmentId = null,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Pending",
            Reason = "Cần gọi nhắc"
        };
        db.FollowUpSchedules.Add(followUpPendingUnlinked);

        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var response = await service.GetReceptionistRemindersAsync();

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Contains(response.Data, r => r.FollowUpId == 906);
        Assert.DoesNotContain(response.Data, r => r.FollowUpId == 905);
    }

    [Fact]
    public async Task GetReceptionistReminders_DateFilters_TodayAndTomorrow()
    {
        using var db = CreateDbContext("Reminders_Test2");
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var apptToday = new Appointment
        {
            AppointmentId = 811,
            AppointmentCode = "APT-TODAY",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = today.AddHours(14),
            ScheduledEnd = today.AddHours(14).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám hôm nay",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(apptToday);
        db.FollowUpSchedules.Add(new FollowUpSchedule
        {
            FollowUpId = 911,
            AppointmentId = 811,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Scheduled",
            Appointment = apptToday
        });

        var apptTomorrow = new Appointment
        {
            AppointmentId = 812,
            AppointmentCode = "APT-TOMORROW",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = tomorrow.AddHours(10),
            ScheduledEnd = tomorrow.AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám ngày mai",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(apptTomorrow);
        db.FollowUpSchedules.Add(new FollowUpSchedule
        {
            FollowUpId = 912,
            AppointmentId = 812,
            TreatmentSessionId = 1,
            RecommendedDate = tomorrow,
            Status = "Scheduled",
            Appointment = apptTomorrow
        });

        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var todayRes = await service.GetReceptionistRemindersAsync("today");
        Assert.True(todayRes.Success);
        Assert.Contains(todayRes.Data!, r => r.FollowUpId == 911);
        Assert.DoesNotContain(todayRes.Data!, r => r.FollowUpId == 912);

        var tomorrowRes = await service.GetReceptionistRemindersAsync("tomorrow");
        Assert.True(tomorrowRes.Success);
        Assert.Contains(tomorrowRes.Data!, r => r.FollowUpId == 912);
        Assert.DoesNotContain(tomorrowRes.Data!, r => r.FollowUpId == 911);
    }

    [Fact]
    public async Task UpdateReminderCallStatus_ValidCall_UpdatesStatusAndAuditLog()
    {
        using var db = CreateDbContext("Reminders_Test3");
        var today = DateTime.Today;

        var recUser = new UserAccount
        {
            UserId = 50,
            Email = "receptionist@dentalcare.com",
            Role = UserRole.Receptionist,
            Status = "Active"
        };
        var recStaff = new StaffProfile
        {
            UserId = 50,
            EmployeeCode = "REC01",
            FullName = "Lễ Tân Mai Hoa",
            EmploymentStatus = "Active"
        };
        db.UserAccounts.Add(recUser);
        db.StaffProfiles.Add(recStaff);

        var appt = new Appointment
        {
            AppointmentId = 821,
            AppointmentCode = "APT-CALL-01",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = today.AddHours(15),
            ScheduledEnd = today.AddHours(15).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám chỉnh nha",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 921,
            AppointmentId = 821,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Scheduled",
            Appointment = appt
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var request = new UpdateReminderCallStatusRequest
        {
            FollowUpId = 921,
            CallStatus = "Called",
            Note = "Bệnh nhân xác nhận sẽ có mặt đúng 15h"
        };

        var response = await service.UpdateReminderCallStatusAsync(50, request);

        Assert.True(response.Success);
        Assert.Equal("Called", response.Data?.CallStatus);
        Assert.Equal("Lễ Tân Mai Hoa", response.Data?.LastCallerName);
        Assert.Equal("Bệnh nhân xác nhận sẽ có mặt đúng 15h", response.Data?.CallNote);

        var updatedFollowUp = await db.FollowUpSchedules.FindAsync(921L);
        Assert.NotNull(updatedFollowUp);
        Assert.Equal("Scheduled", updatedFollowUp.Status);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == "921" && a.Action == "FollowUpReminderCall");
        Assert.NotNull(audit);
        Assert.Equal(50, audit.UserId);
        Assert.Contains("Called", audit.NewValues);
        Assert.Contains("Lễ Tân Mai Hoa", audit.NewValues);
    }

    [Fact]
    public async Task UpdateReminderCallStatus_Unreachable_UpdatesStatus()
    {
        using var db = CreateDbContext("Reminders_Test4");
        var today = DateTime.Today;

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 922,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Scheduled"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var request = new UpdateReminderCallStatusRequest
        {
            FollowUpId = 922,
            CallStatus = "Unreachable",
            Note = "Máy bận, đã gọi 2 lần không nghe"
        };

        var response = await service.UpdateReminderCallStatusAsync(50, request);

        Assert.True(response.Success);
        Assert.Equal("Unreachable", response.Data?.CallStatus);

        var updated = await db.FollowUpSchedules.FindAsync(922L);
        Assert.Equal("Scheduled", updated?.Status);
    }

    [Fact]
    public async Task UpdateReminderCallStatus_CancelledAppointment_Fails()
    {
        using var db = CreateDbContext("Reminders_Test5");
        var today = DateTime.Today;

        var appt = new Appointment
        {
            AppointmentId = 823,
            AppointmentCode = "APT-CANCELLED",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = today.AddHours(16),
            ScheduledEnd = today.AddHours(16).AddMinutes(30),
            Status = AppointmentStatus.Cancelled,
            ReasonForVisit = "Đã hủy",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 923,
            AppointmentId = 823,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Scheduled",
            Appointment = appt
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var request = new UpdateReminderCallStatusRequest
        {
            FollowUpId = 923,
            CallStatus = "Called"
        };

        var response = await service.UpdateReminderCallStatusAsync(50, request);

        Assert.False(response.Success);
        Assert.Contains("hủy", response.Message);
    }

    [Fact]
    public async Task UpdateReminderCallStatus_InvalidStatus_Fails()
    {
        using var db = CreateDbContext("Reminders_Test6");

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 924,
            TreatmentSessionId = 1,
            RecommendedDate = DateTime.Today,
            Status = "Scheduled"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var request = new UpdateReminderCallStatusRequest
        {
            FollowUpId = 924,
            CallStatus = "UnknownHackStatus"
        };

        var response = await service.UpdateReminderCallStatusAsync(50, request);

        Assert.False(response.Success);
        Assert.Contains("không hợp lệ", response.Message);
    }

    [Fact]
    public async Task UpdateReminderCallStatus_Pending_Called_PreservesPendingStatus()
    {
        using var db = CreateDbContext("Reminders_Test_Pending_Called");
        var today = DateTime.Today;

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 931,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Pending"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var request = new UpdateReminderCallStatusRequest
        {
            FollowUpId = 931,
            CallStatus = "Called",
            Note = "Nhắc bệnh nhân đặt lịch tái khám"
        };

        var response = await service.UpdateReminderCallStatusAsync(50, request);

        Assert.True(response.Success);
        Assert.Equal("Called", response.Data?.CallStatus);
        Assert.Equal("Pending", response.Data?.AppointmentStatus);

        var updated = await db.FollowUpSchedules.FindAsync(931L);
        Assert.NotNull(updated);
        Assert.Equal("Pending", updated.Status);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == "931" && a.Action == "FollowUpReminderCall");
        Assert.NotNull(audit);
        Assert.Contains("Called", audit.NewValues);
    }

    [Fact]
    public async Task UpdateReminderCallStatus_Confirmed_Called_PreservesConfirmedStatus()
    {
        using var db = CreateDbContext("Reminders_Test_Confirmed_Called");
        var today = DateTime.Today;

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 932,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Confirmed"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var request = new UpdateReminderCallStatusRequest
        {
            FollowUpId = 932,
            CallStatus = "Called",
            Note = "Đã gọi xác nhận"
        };

        var response = await service.UpdateReminderCallStatusAsync(50, request);

        Assert.True(response.Success);
        Assert.Equal("Called", response.Data?.CallStatus);
        Assert.Equal("Confirmed", response.Data?.AppointmentStatus);

        var updated = await db.FollowUpSchedules.FindAsync(932L);
        Assert.NotNull(updated);
        Assert.Equal("Confirmed", updated.Status);
    }

    [Fact]
    public async Task UpdateReminderCallStatus_CancelledFollowUp_FailsAndDoesNotMutate()
    {
        using var db = CreateDbContext("Reminders_Test_Cancelled_FollowUp");
        var today = DateTime.Today;

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 933,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Cancelled"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var request = new UpdateReminderCallStatusRequest
        {
            FollowUpId = 933,
            CallStatus = "Called"
        };

        var response = await service.UpdateReminderCallStatusAsync(50, request);

        Assert.False(response.Success);
        Assert.Contains("hủy", response.Message);

        var updated = await db.FollowUpSchedules.FindAsync(933L);
        Assert.NotNull(updated);
        Assert.Equal("Cancelled", updated.Status);
    }

    [Fact]
    public async Task UpdateReminderCallStatus_CompletedFollowUp_FailsAndDoesNotMutate()
    {
        using var db = CreateDbContext("Reminders_Test_Completed_FollowUp");
        var today = DateTime.Today;

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 934,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Completed"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var request = new UpdateReminderCallStatusRequest
        {
            FollowUpId = 934,
            CallStatus = "Called"
        };

        var response = await service.UpdateReminderCallStatusAsync(50, request);

        Assert.False(response.Success);
        Assert.Contains("hoàn tất", response.Message);

        var updated = await db.FollowUpSchedules.FindAsync(934L);
        Assert.NotNull(updated);
        Assert.Equal("Completed", updated.Status);
    }

    [Fact]
    public async Task UpdateReminderCallStatus_CompletedAppointmentLinked_Fails()
    {
        using var db = CreateDbContext("Reminders_Test_Completed_Appt");
        var today = DateTime.Today;

        var appt = new Appointment
        {
            AppointmentId = 835,
            AppointmentCode = "APT-COMPLETED-835",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = today.AddHours(14),
            ScheduledEnd = today.AddHours(14).AddMinutes(30),
            Status = AppointmentStatus.Completed,
            ReasonForVisit = "Đã khám xong",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 935,
            AppointmentId = 835,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Scheduled",
            Appointment = appt
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var request = new UpdateReminderCallStatusRequest
        {
            FollowUpId = 935,
            CallStatus = "Called"
        };

        var response = await service.UpdateReminderCallStatusAsync(50, request);

        Assert.False(response.Success);
        Assert.Contains("hoàn tất", response.Message);

        var updated = await db.FollowUpSchedules.FindAsync(935L);
        Assert.NotNull(updated);
        Assert.Equal("Scheduled", updated.Status);
    }

    [Fact]
    public async Task GetReceptionistReminders_ReadsCallStatusFromAuditLogs()
    {
        using var db = CreateDbContext("Reminders_Test_AuditLogRead");
        var today = DateTime.Today;

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 936,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Scheduled",
            Reason = "Tái khám niềng răng"
        };
        db.FollowUpSchedules.Add(followUp);

        var audit = new AuditLog
        {
            UserId = 50,
            Action = "FollowUpReminderCall",
            EntityName = "FollowUpSchedule",
            EntityId = "936",
            OldValues = "Scheduled",
            NewValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                CallStatus = "Called",
                Note = "Bệnh nhân xác nhận chiều nay tới",
                CalledAt = today.AddHours(8),
                CallerStaffName = "Lễ Tân Thảo"
            }),
            CreatedAt = today.AddHours(8)
        };
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var response = await service.GetReceptionistRemindersAsync("today");

        Assert.True(response.Success);
        var item = response.Data?.FirstOrDefault(r => r.FollowUpId == 936);
        Assert.NotNull(item);
        Assert.Equal("Called", item.CallStatus);
        Assert.Equal("Lễ Tân Thảo", item.LastCallerName);
        Assert.Equal("Scheduled", item.AppointmentStatus);
    }

    [Fact]
    public async Task GetReceptionistReminders_HistoricalLegacyStatus_FallbacksGracefully()
    {
        using var db = CreateDbContext("Reminders_Test_LegacyStatus");
        var today = DateTime.Today;

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 937,
            TreatmentSessionId = 1,
            RecommendedDate = today,
            Status = "Called",
            Reason = "Lịch cũ từ phiên bản trước"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var response = await service.GetReceptionistRemindersAsync("today");

        Assert.True(response.Success);
        var item = response.Data?.FirstOrDefault(r => r.FollowUpId == 937);
        Assert.NotNull(item);
        Assert.Equal("Called", item.CallStatus);
    }
}
