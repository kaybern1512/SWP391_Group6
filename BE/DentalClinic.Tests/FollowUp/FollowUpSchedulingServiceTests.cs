using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.FollowUp.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
}
