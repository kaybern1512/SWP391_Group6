using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DentalClinic.Api.Controllers;
using DentalClinic.Application.Features.FollowUp.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DentalClinic.Tests.FollowUp;

public class FollowUpAuthorizationAndUiTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedDataAsync(DentalClinicDbContext db)
    {
        var docUser = new UserAccount { UserId = 20, Email = "doc20@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var staffProfile = new StaffProfile { UserId = 20, FullName = "Bác Sĩ Nguyễn Văn 20", EmployeeCode = "DOC020", EmploymentStatus = "Active", User = docUser };
        var docProfile = new DentistProfile { UserId = 20, Qualification = "ThS", User = staffProfile };
        db.UserAccounts.Add(docUser);
        db.StaffProfiles.Add(staffProfile);
        db.DentistProfiles.Add(docProfile);

        var docOther = new UserAccount { UserId = 30, Email = "doc30@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var staffOther = new StaffProfile { UserId = 30, FullName = "Bác Sĩ Lê Văn 30", EmployeeCode = "DOC030", EmploymentStatus = "Active", User = docOther };
        var docOtherProfile = new DentistProfile { UserId = 30, Qualification = "ThS", User = staffOther };
        db.UserAccounts.Add(docOther);
        db.StaffProfiles.Add(staffOther);
        db.DentistProfiles.Add(docOtherProfile);

        var p10User = new UserAccount { UserId = 10, Email = "pat10@test.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var p10Profile = new PatientProfile { UserId = 10, PatientCode = "PAT010", FullName = "Bệnh Nhân Trần 10", User = p10User };
        db.UserAccounts.Add(p10User);
        db.PatientProfiles.Add(p10Profile);

        var p15User = new UserAccount { UserId = 15, Email = "pat15@test.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var p15Profile = new PatientProfile { UserId = 15, PatientCode = "PAT015", FullName = "Bệnh Nhân Lê 15", User = p15User };
        db.UserAccounts.Add(p15User);
        db.PatientProfiles.Add(p15Profile);

        var targetDate = DateTime.Today.AddDays(2);
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(targetDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(18, 0),
            Status = "Scheduled"
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task D01_PatientCannotModifyAnotherPatientsAppointment_IDOR_Prevented()
    {
        var db = CreateDbContext(nameof(D01_PatientCannotModifyAnotherPatientsAppointment_IDOR_Prevented));
        await SeedDataAsync(db);

        var targetDate = DateTime.Today.AddDays(2);

        // Appointment belongs to Patient 15
        var appt = new Appointment
        {
            AppointmentCode = "APT-PAT15-D01",
            PatientUserId = 15,
            AssignedDentistUserId = 20,
            DepartmentId = 1,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Appt patient 15",
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var req = new PatientFollowUpChangeRequest
        {
            AppointmentId = appt.AppointmentId,
            ProposedStart = targetDate.AddHours(14),
            ProposedEnd = targetDate.AddHours(14).AddMinutes(30),
            Reason = "Patient 10 maliciously trying to reschedule Patient 15's appt",
            IsRebook = false
        };

        // Patient 10 calls with Patient 15's appointmentId
        var result = await service.SubmitPatientChangeProposalAsync(10, req);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
    }

    [Fact]
    public async Task D02_DentistCannotResolveProposalOutsideAssignment_Prevented()
    {
        var db = CreateDbContext(nameof(D02_DentistCannotResolveProposalOutsideAssignment_Prevented));
        await SeedDataAsync(db);

        var targetDate = DateTime.Today.AddDays(2);

        var appt = new Appointment
        {
            AppointmentCode = "APT-PAT10-D02",
            PatientUserId = 10,
            AssignedDentistUserId = 20, // Assigned to Doctor 20
            DepartmentId = 1,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Appt patient 10",
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var prop = new AppointmentChangeProposal
        {
            AppointmentId = appt.AppointmentId,
            ProposedByUserId = 10,
            ProposedDepartmentId = 1,
            ProposedDentistUserId = 20,
            ProposedStart = targetDate.AddHours(14),
            ProposedEnd = targetDate.AddHours(14).AddMinutes(30),
            Reason = "Patient wants afternoon",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.AppointmentChangeProposals.Add(prop);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var resolveReq = new ResolveProposalRequest
        {
            ProposalId = prop.ProposalId,
            IsApproved = true
        };

        // Doctor 30 (unrelated doctor) attempts to resolve proposal
        var result = await service.ResolveProposalAsync(30, resolveReq);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
    }

    [Fact]
    public async Task D03_PatientCannotWithdrawAnotherPatientsProposal_IDOR_Prevented()
    {
        var db = CreateDbContext(nameof(D03_PatientCannotWithdrawAnotherPatientsProposal_IDOR_Prevented));
        await SeedDataAsync(db);

        var prop = new AppointmentChangeProposal
        {
            AppointmentId = 1,
            ProposedByUserId = 15,
            Reason = "Đổi lịch",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        // Dummy appointment for prop
        var appt = new Appointment
        {
            AppointmentId = 1,
            AppointmentCode = "APT-1",
            PatientUserId = 15,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Test",
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appt);
        db.AppointmentChangeProposals.Add(prop);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        // Patient 10 attempts to withdraw Patient 15's proposal
        var result = await service.WithdrawPatientProposalAsync(10, prop.ProposalId);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
    }

    [Fact]
    public async Task D04_DoubleClick_SimulatedSequentially_RejectsSecondRequest()
    {
        var db = CreateDbContext(nameof(D04_DoubleClick_SimulatedSequentially_RejectsSecondRequest));
        await SeedDataAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var req = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = 10,
            ScheduledStart = targetDate.AddHours(10),
            ScheduledEnd = targetDate.AddHours(10).AddMinutes(30),
            Reason = "Click 1"
        };

        var firstClick = await service.CreateDoctorFollowUpAppointmentAsync(20, req);
        Assert.True(firstClick.Success);

        // Immediate second click with same parameters
        var secondClick = await service.CreateDoctorFollowUpAppointmentAsync(20, req);
        Assert.False(secondClick.Success);
    }

    [Fact]
    public async Task D05_SlotOccupiedAfterViewing_ReturnsConflict()
    {
        var db = CreateDbContext(nameof(D05_SlotOccupiedAfterViewing_ReturnsConflict));
        await SeedDataAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // User views slots at 10:00
        var slotsRes = await service.GetDoctorAvailableSlotsAsync(20, targetDate, 30);
        Assert.True(slotsRes.Success);
        var slot10 = slotsRes.Data!.FirstOrDefault(s => s.StartTime.Hour == 10);
        Assert.NotNull(slot10);

        // Another appointment occupies 10:00 before user submits
        db.Appointments.Add(new Appointment
        {
            AppointmentCode = "APT-OTHER-D05",
            PatientUserId = 15,
            AssignedDentistUserId = 20,
            ScheduledStart = slot10.StartTime,
            ScheduledEnd = slot10.EndTime,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Other booking",
            Status = AppointmentStatus.Confirmed
        });
        await db.SaveChangesAsync();

        // User attempts booking
        var bookReq = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = 10,
            ScheduledStart = slot10.StartTime,
            ScheduledEnd = slot10.EndTime,
            Reason = "User booking attempt"
        };

        var bookRes = await service.CreateDoctorFollowUpAppointmentAsync(20, bookReq);
        Assert.False(bookRes.Success);
    }

    [Fact]
    public async Task D06_ConflictResponse_ContainsClearVietnameseMessage()
    {
        var db = CreateDbContext(nameof(D06_ConflictResponse_ContainsClearVietnameseMessage));
        await SeedDataAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        var start = targetDate.AddHours(10);
        var end = targetDate.AddHours(10).AddMinutes(30);

        db.Appointments.Add(new Appointment
        {
            AppointmentCode = "APT-OCCUPIED-D06",
            PatientUserId = 15,
            AssignedDentistUserId = 20,
            ScheduledStart = start,
            ScheduledEnd = end,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Occupied",
            Status = AppointmentStatus.Confirmed
        });
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var req = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = 10,
            ScheduledStart = start,
            ScheduledEnd = end,
            Reason = "Conflict test"
        };

        var result = await service.CreateDoctorFollowUpAppointmentAsync(20, req);
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        // Vietnamese user-friendly message
        Assert.Contains("trùng", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task D07_AvailableSlots_RefreshesAccuratelyAfterBooking()
    {
        var db = CreateDbContext(nameof(D07_AvailableSlots_RefreshesAccuratelyAfterBooking));
        await SeedDataAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // Before booking: slot 11:00 exists
        var slotsBefore = await service.GetDoctorAvailableSlotsAsync(20, targetDate, 30);
        Assert.Contains(slotsBefore.Data!, s => s.StartTime.Hour == 11 && s.StartTime.Minute == 0);

        // Book slot 11:00
        var req = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = 10,
            ScheduledStart = targetDate.AddHours(11),
            ScheduledEnd = targetDate.AddHours(11).AddMinutes(30),
            Reason = "Book 11:00"
        };
        var bookRes = await service.CreateDoctorFollowUpAppointmentAsync(20, req);
        Assert.True(bookRes.Success);

        // After booking: slot 11:00 no longer available
        var slotsAfter = await service.GetDoctorAvailableSlotsAsync(20, targetDate, 30);
        Assert.DoesNotContain(slotsAfter.Data!, s => s.StartTime.Hour == 11 && s.StartTime.Minute == 0);
    }

    [Fact]
    public void D08_AuthorizationAttributes_PreservedOnControllerEndpoints()
    {
        var controllerType = typeof(PostCareController);

        // Verify class has [Authorize]
        var classAuthorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(classAuthorize);

        // Verify doctor-book endpoint requires Dentist role
        var bookMethod = controllerType.GetMethod("DoctorBookFollowUp");
        Assert.NotNull(bookMethod);
        var bookAuth = bookMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(bookAuth);
        Assert.Contains(UserRole.Dentist, bookAuth.Roles);

        // Verify proposals endpoint requires Patient role
        var submitProposalMethod = controllerType.GetMethod("SubmitPatientProposal");
        Assert.NotNull(submitProposalMethod);
        var propAuth = submitProposalMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(propAuth);
        Assert.Contains(UserRole.Patient, propAuth.Roles);

        // Verify resolve endpoint requires Dentist role
        var resolveMethod = controllerType.GetMethod("ResolveProposal");
        Assert.NotNull(resolveMethod);
        var resAuth = resolveMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(resAuth);
        Assert.Contains(UserRole.Dentist, resAuth.Roles);
    }
}
