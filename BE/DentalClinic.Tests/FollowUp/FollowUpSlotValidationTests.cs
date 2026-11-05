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

public class FollowUpSlotValidationTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static Appointment CreateTestAppointment(
        string code,
        long patientId,
        long dentistId,
        DateTime start,
        DateTime end,
        string status = AppointmentStatus.Confirmed)
    {
        return new Appointment
        {
            AppointmentCode = code,
            PatientUserId = patientId,
            AssignedDentistUserId = dentistId,
            ScheduledStart = start,
            ScheduledEnd = end,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Tái khám định kỳ",
            Status = status
        };
    }

    private static async Task SeedDoctorAndPatientAsync(
        DentalClinicDbContext db,
        long doctorId = 20,
        long patientId = 10,
        int daysFromNow = 2)
    {
        var docUser = new UserAccount
        {
            UserId = doctorId,
            Email = $"doctor_{doctorId}@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var staffProfile = new StaffProfile
        {
            UserId = doctorId,
            FullName = $"Bác Sĩ Nguyễn Văn {doctorId}",
            EmployeeCode = $"DOC{doctorId}",
            EmploymentStatus = "Active",
            User = docUser
        };
        var docProfile = new DentistProfile
        {
            UserId = doctorId,
            Qualification = "Chuyên khoa I",
            User = staffProfile
        };
        db.UserAccounts.Add(docUser);
        db.StaffProfiles.Add(staffProfile);
        db.DentistProfiles.Add(docProfile);

        var patUser = new UserAccount
        {
            UserId = patientId,
            Email = $"patient_{patientId}@test.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patProfile = new PatientProfile
        {
            UserId = patientId,
            PatientCode = $"PAT{patientId}",
            FullName = $"Bệnh Nhân Trần {patientId}",
            User = patUser
        };
        db.UserAccounts.Add(patUser);
        db.PatientProfiles.Add(patProfile);

        // Working schedule for doctor from 08:00 to 17:00
        var targetDate = DateTime.Today.AddDays(daysFromNow);
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistUserId = doctorId,
            WorkDate = DateOnly.FromDateTime(targetDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            Status = "Scheduled"
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A01_ExactFullSlotOverlap_ReturnsConflict()
    {
        var db = CreateDbContext(nameof(A01_ExactFullSlotOverlap_ReturnsConflict));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var existingStart = slotDate.AddHours(10);
        var existingEnd = slotDate.AddHours(10).AddMinutes(30);

        db.Appointments.Add(CreateTestAppointment("APT-EXIST-01", 999, 20, existingStart, existingEnd));
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var result = await service.ValidateFollowUpSlotAsync(20, 10, existingStart, existingEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.DoctorSlotConflict, result.FailureReason);
    }

    [Fact]
    public async Task A02_PartialOverlap_AtStart_ReturnsConflict()
    {
        var db = CreateDbContext(nameof(A02_PartialOverlap_AtStart_ReturnsConflict));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var existingStart = slotDate.AddHours(10);
        var existingEnd = slotDate.AddHours(10).AddMinutes(30);

        db.Appointments.Add(CreateTestAppointment("APT-EXIST-02", 999, 20, existingStart, existingEnd));
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        // New slot starts before existing and ends inside existing: 09:45 - 10:15
        var newStart = existingStart.AddMinutes(-15);
        var newEnd = existingStart.AddMinutes(15);
        var result = await service.ValidateFollowUpSlotAsync(20, 10, newStart, newEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.DoctorSlotConflict, result.FailureReason);
    }

    [Fact]
    public async Task A03_PartialOverlap_AtEnd_ReturnsConflict()
    {
        var db = CreateDbContext(nameof(A03_PartialOverlap_AtEnd_ReturnsConflict));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var existingStart = slotDate.AddHours(10);
        var existingEnd = slotDate.AddHours(10).AddMinutes(30);

        db.Appointments.Add(CreateTestAppointment("APT-EXIST-03", 999, 20, existingStart, existingEnd));
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        // New slot starts inside existing and ends after: 10:15 - 10:45
        var newStart = existingStart.AddMinutes(15);
        var newEnd = existingEnd.AddMinutes(15);
        var result = await service.ValidateFollowUpSlotAsync(20, 10, newStart, newEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.DoctorSlotConflict, result.FailureReason);
    }

    [Fact]
    public async Task A04_NewSlotEnclosesExistingSlot_ReturnsConflict()
    {
        var db = CreateDbContext(nameof(A04_NewSlotEnclosesExistingSlot_ReturnsConflict));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var existingStart = slotDate.AddHours(10).AddMinutes(15);
        var existingEnd = slotDate.AddHours(10).AddMinutes(45);

        db.Appointments.Add(CreateTestAppointment("APT-EXIST-04", 999, 20, existingStart, existingEnd));
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        // New slot fully encloses existing: 10:00 - 11:00
        var newStart = slotDate.AddHours(10);
        var newEnd = slotDate.AddHours(11);
        var result = await service.ValidateFollowUpSlotAsync(20, 10, newStart, newEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.DoctorSlotConflict, result.FailureReason);
    }

    [Fact]
    public async Task A05_ExistingSlotEnclosesNewSlot_ReturnsConflict()
    {
        var db = CreateDbContext(nameof(A05_ExistingSlotEnclosesNewSlot_ReturnsConflict));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var existingStart = slotDate.AddHours(10);
        var existingEnd = slotDate.AddHours(11);

        db.Appointments.Add(CreateTestAppointment("APT-EXIST-05", 999, 20, existingStart, existingEnd));
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        // New slot is strictly inside existing: 10:15 - 10:45
        var newStart = slotDate.AddHours(10).AddMinutes(15);
        var newEnd = slotDate.AddHours(10).AddMinutes(45);
        var result = await service.ValidateFollowUpSlotAsync(20, 10, newStart, newEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.DoctorSlotConflict, result.FailureReason);
    }

    [Fact]
    public async Task A06_AdjacentSlots_TouchingAtBoundary_IsValid()
    {
        var db = CreateDbContext(nameof(A06_AdjacentSlots_TouchingAtBoundary_IsValid));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var existingStart = slotDate.AddHours(10);
        var existingEnd = slotDate.AddHours(10).AddMinutes(30);

        db.Appointments.Add(CreateTestAppointment("APT-EXIST-06", 999, 20, existingStart, existingEnd));
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // Slot immediately before: 09:30 - 10:00 (ends exactly when existing starts)
        var beforeResult = await service.ValidateFollowUpSlotAsync(20, 10, slotDate.AddHours(9).AddMinutes(30), existingStart);
        Assert.True(beforeResult.IsValid);

        // Slot immediately after: 10:30 - 11:00 (starts exactly when existing ends)
        var afterResult = await service.ValidateFollowUpSlotAsync(20, 10, existingEnd, slotDate.AddHours(11));
        Assert.True(afterResult.IsValid);
    }

    [Fact]
    public async Task A07_OneMinuteOverlap_ReturnsConflict()
    {
        var db = CreateDbContext(nameof(A07_OneMinuteOverlap_ReturnsConflict));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var existingStart = slotDate.AddHours(10);
        var existingEnd = slotDate.AddHours(10).AddMinutes(30);

        db.Appointments.Add(CreateTestAppointment("APT-EXIST-07", 999, 20, existingStart, existingEnd));
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        // Overlap by exactly 1 minute: 10:29 - 10:59
        var newStart = existingEnd.AddMinutes(-1);
        var newEnd = newStart.AddMinutes(30);
        var result = await service.ValidateFollowUpSlotAsync(20, 10, newStart, newEnd);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.DoctorSlotConflict, result.FailureReason);
    }

    [Fact]
    public async Task A08_SamePatient_DifferentDoctor_SameSlot_ReturnsPatientConflict()
    {
        var db = CreateDbContext(nameof(A08_SamePatient_DifferentDoctor_SameSlot_ReturnsPatientConflict));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);
        await SeedDoctorAndPatientAsync(db, 25, 999, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var start = slotDate.AddHours(10);
        var end = slotDate.AddHours(10).AddMinutes(30);

        // Patient 10 already has appointment with Doctor 25
        db.Appointments.Add(CreateTestAppointment("APT-PAT-08", 10, 25, start, end));
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        // Trying to book same Patient 10 with Doctor 20 at same time
        var result = await service.ValidateFollowUpSlotAsync(20, 10, start, end);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.PatientSlotConflict, result.FailureReason);
    }

    [Fact]
    public async Task A09_SameDoctor_DifferentPatient_SameSlot_ReturnsDoctorConflict()
    {
        var db = CreateDbContext(nameof(A09_SameDoctor_DifferentPatient_SameSlot_ReturnsDoctorConflict));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);
        await SeedDoctorAndPatientAsync(db, 999, 15, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var start = slotDate.AddHours(10);
        var end = slotDate.AddHours(10).AddMinutes(30);

        // Doctor 20 already has appointment with Patient 15
        db.Appointments.Add(CreateTestAppointment("APT-DOC-09", 15, 20, start, end));
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        // Trying to book Patient 10 with Doctor 20 at same time
        var result = await service.ValidateFollowUpSlotAsync(20, 10, start, end);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.DoctorSlotConflict, result.FailureReason);
    }

    [Fact]
    public async Task A10_CancelledAppointment_DoesNotBlockSlot()
    {
        var db = CreateDbContext(nameof(A10_CancelledAppointment_DoesNotBlockSlot));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var start = slotDate.AddHours(10);
        var end = slotDate.AddHours(10).AddMinutes(30);

        // Cancelled appointment at this slot
        db.Appointments.Add(CreateTestAppointment("APT-CANCELLED-10", 999, 20, start, end, AppointmentStatus.Cancelled));
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var result = await service.ValidateFollowUpSlotAsync(20, 10, start, end);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A11_CompletedAppointment_BlocksSlot_FromRebookingSameSlot()
    {
        var db = CreateDbContext(nameof(A11_CompletedAppointment_BlocksSlot_FromRebookingSameSlot));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var start = slotDate.AddHours(10);
        var end = slotDate.AddHours(10).AddMinutes(30);

        // A completed appointment occupies the slot and prevents rebooking
        db.Appointments.Add(CreateTestAppointment("APT-COMPLETED-11", 999, 20, start, end, AppointmentStatus.Completed));
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var result = await service.ValidateFollowUpSlotAsync(20, 10, start, end);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.DoctorSlotConflict, result.FailureReason);
    }

    [Fact]
    public async Task A11B_InServiceAppointment_BlocksSlot()
    {
        var db = CreateDbContext(nameof(A11B_InServiceAppointment_BlocksSlot));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var start = slotDate.AddHours(10);
        var end = slotDate.AddHours(10).AddMinutes(30);

        // An in-service appointment occupies slot
        db.Appointments.Add(CreateTestAppointment("APT-INSERVICE-11B", 999, 20, start, end, AppointmentStatus.InService));
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var result = await service.ValidateFollowUpSlotAsync(20, 10, start, end);

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.DoctorSlotConflict, result.FailureReason);
    }

    [Fact]
    public async Task A12_OutsideShift_OrDuringDoctorLeave_ReturnsUnavailability()
    {
        var db = CreateDbContext(nameof(A12_OutsideShift_OrDuringDoctorLeave_ReturnsUnavailability));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // 1. Before shift starts (07:00 - 07:30, shift is 08:00 - 17:00)
        var beforeShiftResult = await service.ValidateFollowUpSlotAsync(
            20, 10, slotDate.AddHours(7), slotDate.AddHours(7).AddMinutes(30));
        Assert.False(beforeShiftResult.IsValid);
        Assert.Equal(SchedulingFailureReason.OutsideDoctorShift, beforeShiftResult.FailureReason);

        // 2. Doctor on leave during 14:00 - 16:00
        db.DentistAvailabilities.Add(new DentistAvailability
        {
            DentistUserId = 20,
            StartDateTime = slotDate.AddHours(14),
            EndDateTime = slotDate.AddHours(16),
            AvailabilityStatus = DentistAvailabilityStatus.OnLeave,
            Reason = "Nghỉ phép cá nhân"
        });
        await db.SaveChangesAsync();

        var duringLeaveResult = await service.ValidateFollowUpSlotAsync(
            20, 10, slotDate.AddHours(14).AddMinutes(30), slotDate.AddHours(15));
        Assert.False(duringLeaveResult.IsValid);
        Assert.Equal(SchedulingFailureReason.DoctorUnavailableOrOnLeave, duringLeaveResult.FailureReason);
    }

    [Fact]
    public async Task A13_BoundaryDuration_10And240Minutes()
    {
        var db = CreateDbContext(nameof(A13_BoundaryDuration_10And240Minutes));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // Exactly 10 minutes: Valid
        var res10 = await service.ValidateFollowUpSlotAsync(
            20, 10, slotDate.AddHours(9), slotDate.AddHours(9).AddMinutes(10));
        Assert.True(res10.IsValid);

        // Exactly 240 minutes (4 hours): Valid (09:00 - 13:00)
        var res240 = await service.ValidateFollowUpSlotAsync(
            20, 10, slotDate.AddHours(9), slotDate.AddHours(13));
        Assert.True(res240.IsValid);

        // 9 minutes: Invalid
        var res9 = await service.ValidateFollowUpSlotAsync(
            20, 10, slotDate.AddHours(9), slotDate.AddHours(9).AddMinutes(9));
        Assert.False(res9.IsValid);
        Assert.Equal(SchedulingFailureReason.InvalidTimeRange, res9.FailureReason);

        // 241 minutes: Invalid
        var res241 = await service.ValidateFollowUpSlotAsync(
            20, 10, slotDate.AddHours(9), slotDate.AddHours(9).AddMinutes(241));
        Assert.False(res241.IsValid);
        Assert.Equal(SchedulingFailureReason.InvalidTimeRange, res241.FailureReason);
    }

    [Fact]
    public async Task A14_DurationZero_Negative_OrEndBeforeStart_ReturnsInvalidTimeRange()
    {
        var db = CreateDbContext(nameof(A14_DurationZero_Negative_OrEndBeforeStart_ReturnsInvalidTimeRange));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // End == Start (0 duration)
        var resZero = await service.ValidateFollowUpSlotAsync(
            20, 10, slotDate.AddHours(10), slotDate.AddHours(10));
        Assert.False(resZero.IsValid);
        Assert.Equal(SchedulingFailureReason.InvalidTimeRange, resZero.FailureReason);

        // End < Start (negative duration)
        var resNeg = await service.ValidateFollowUpSlotAsync(
            20, 10, slotDate.AddHours(10), slotDate.AddHours(9));
        Assert.False(resNeg.IsValid);
        Assert.Equal(SchedulingFailureReason.InvalidTimeRange, resNeg.FailureReason);
    }

    [Fact]
    public async Task A15_MidnightCrossing_ReturnsOutsideShift()
    {
        var db = CreateDbContext(nameof(A15_MidnightCrossing_ReturnsOutsideShift));
        await SeedDoctorAndPatientAsync(db, 20, 10, 2);

        var slotDate = DateTime.Today.AddDays(2);
        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // Crossing midnight: 23:30 to next day 00:30
        var start = slotDate.AddHours(23).AddMinutes(30);
        var end = slotDate.AddDays(1).AddMinutes(30);
        var res = await service.ValidateFollowUpSlotAsync(20, 10, start, end);

        Assert.False(res.IsValid);
        Assert.Equal(SchedulingFailureReason.OutsideDoctorShift, res.FailureReason);
    }
}
