using DentalClinic.Application.Features.ClinicalCore.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Tests.ClinicalCore;

public class ClinicalCoreWorkflowTests
{
    [Fact]
    public async Task GetPrescriptionsForPatientAsync_OnlyReturnsOwnedPatientPrescriptions()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        var service = new ClinicalCoreService(db);

        var ownResult = await service.GetPrescriptionsForPatientAsync(
            scenario.PatientId, scenario.PatientId, UserRole.Patient);
        var otherResult = await service.GetPrescriptionsForPatientAsync(
            scenario.PatientId, scenario.OtherPatientId, UserRole.Patient);

        Assert.True(ownResult.Success);
        Assert.Single(ownResult.Data!);
        Assert.False(otherResult.Success);
    }

    [Fact]
    public async Task DepartmentManager_CanAcceptAndAssignReferralToDentistInReceivingDepartment()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        var service = new ClinicalCoreService(db);

        var review = await service.ReviewDepartmentReferralAsync(
            scenario.ReferralId,
            new ReviewDepartmentReferralRequest { Accept = true },
            scenario.ManagerId,
            UserRole.DepartmentManager);
        var assign = await service.AssignDepartmentReferralAsync(
            scenario.ReferralId,
            new AssignDepartmentReferralRequest { DentistUserId = scenario.DentistId },
            scenario.ManagerId,
            UserRole.DepartmentManager);

        Assert.True(review.Success);
        Assert.Equal("Accepted", review.Data!.Status);
        Assert.Equal(scenario.ManagerId, review.Data.ReviewedByUserId);
        Assert.True(assign.Success);
        Assert.Equal("Assigned", assign.Data!.Status);
        Assert.Equal(scenario.DentistId, assign.Data.AssignedDentistUserId);
    }

    [Fact]
    public async Task CompletingLastTreatmentPlanItem_CompletesParentPlan()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        var service = new ClinicalCoreService(db);

        var result = await service.UpdateTreatmentPlanItemStatusAsync(
            scenario.TreatmentPlanItemId,
            new UpdateTreatmentPlanItemStatusRequest { Status = "Completed" },
            scenario.DentistId,
            UserRole.Dentist);

        Assert.True(result.Success);
        Assert.Equal("Completed", result.Data!.Status);
        Assert.Equal("Completed", Assert.Single(result.Data.Items).Status);
    }

    [Fact]
    public async Task LinkFollowUpAppointmentAsync_RequiresAppointmentOwnershipAndSetsScheduledStatus()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        var service = new ClinicalCoreService(db);

        var unauthorized = await service.LinkFollowUpAppointmentAsync(
            scenario.FollowUpId,
            new LinkFollowUpAppointmentRequest { AppointmentId = scenario.AppointmentId },
            scenario.OtherPatientId,
            UserRole.Patient);
        var linked = await service.LinkFollowUpAppointmentAsync(
            scenario.FollowUpId,
            new LinkFollowUpAppointmentRequest { AppointmentId = scenario.AppointmentId },
            scenario.PatientId,
            UserRole.Patient);

        Assert.False(unauthorized.Success);
        Assert.True(linked.Success);
        Assert.Equal("Scheduled", linked.Data!.Status);
        Assert.Equal(scenario.AppointmentId, linked.Data.AppointmentId);
        Assert.Equal("APT-CLINICAL", linked.Data.AppointmentCode);
    }

    private static DentalClinicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task<Scenario> SeedScenarioAsync(DentalClinicDbContext db)
    {
        const long patientId = 101;
        const long otherPatientId = 202;
        const long managerId = 301;
        const long dentistId = 401;

        var patientAccount = new UserAccount { UserId = patientId, Email = "patient@test.local", Role = UserRole.Patient, Status = AccountStatus.Active };
        var otherPatientAccount = new UserAccount { UserId = otherPatientId, Email = "other@test.local", Role = UserRole.Patient, Status = AccountStatus.Active };
        var managerAccount = new UserAccount { UserId = managerId, Email = "manager@test.local", Role = UserRole.DepartmentManager, Status = AccountStatus.Active };
        var dentistAccount = new UserAccount { UserId = dentistId, Email = "dentist@test.local", Role = UserRole.Dentist, Status = AccountStatus.Active };

        var patient = new PatientProfile
        {
            UserId = patientId,
            PatientCode = "PT-101",
            FullName = "Test Patient",
            User = patientAccount
        };
        var otherPatient = new PatientProfile
        {
            UserId = otherPatientId,
            PatientCode = "PT-202",
            FullName = "Other Patient",
            User = otherPatientAccount
        };
        var manager = new StaffProfile
        {
            UserId = managerId,
            EmployeeCode = "EMP-301",
            FullName = "Department Manager",
            EmploymentStatus = "Active",
            CreatedAt = DateTime.UtcNow,
            User = managerAccount
        };
        var dentistStaff = new StaffProfile
        {
            UserId = dentistId,
            EmployeeCode = "EMP-401",
            FullName = "Assigned Dentist",
            EmploymentStatus = "Active",
            CreatedAt = DateTime.UtcNow,
            User = dentistAccount
        };
        var dentist = new DentistProfile { UserId = dentistId, User = dentistStaff };

        var fromDepartment = new Department
        {
            DepartmentId = 501,
            Name = "General Dentistry",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var receivingDepartment = new Department
        {
            DepartmentId = 502,
            Name = "Endodontics",
            ManagerUserId = managerId,
            ManagerUser = manager,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var departmentAssignment = new DentistDepartment
        {
            DentistDepartmentId = 1,
            DentistUserId = dentistId,
            DentistUser = dentist,
            DepartmentId = receivingDepartment.DepartmentId,
            Department = receivingDepartment,
            Status = "Active",
            AssignedAt = DateTime.UtcNow
        };

        var plan = new TreatmentPlan
        {
            TreatmentPlanId = 601,
            PatientUserId = patientId,
            PatientUser = patient,
            DepartmentId = fromDepartment.DepartmentId,
            Department = fromDepartment,
            CreatedByDentistUserId = dentistId,
            CreatedByDentistUser = dentist,
            Status = "InProgress",
            CreatedAt = DateTime.UtcNow
        };
        var planItem = new TreatmentPlanItem
        {
            TreatmentPlanItemId = 701,
            TreatmentPlanId = plan.TreatmentPlanId,
            TreatmentPlan = plan,
            SequenceNo = 1,
            Status = "InProgress"
        };
        var appointment = new Appointment
        {
            AppointmentId = 801,
            AppointmentCode = "APT-CLINICAL",
            PatientUserId = patientId,
            PatientUser = patient,
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Treatment follow-up",
            BookingSource = "Online",
            ScheduledStart = DateTime.UtcNow.AddDays(10),
            CreatedAt = DateTime.UtcNow
        };
        var session = new TreatmentSession
        {
            TreatmentSessionId = 901,
            TreatmentPlanItemId = planItem.TreatmentPlanItemId,
            TreatmentPlanItem = planItem,
            AppointmentId = appointment.AppointmentId,
            Appointment = appointment,
            DentistUserId = dentistId,
            DentistUser = dentist,
            SessionDate = DateTime.UtcNow
        };
        var prescription = new Prescription
        {
            PrescriptionId = 1001,
            TreatmentSessionId = session.TreatmentSessionId,
            TreatmentSession = session,
            IssuedAt = DateTime.UtcNow,
            PrescriptionItems = new List<PrescriptionItem>
            {
                new() { PrescriptionItemId = 1101, MedicationName = "Medication", PrescriptionId = 1001 }
            }
        };
        var referral = new DepartmentReferral
        {
            ReferralId = 1201,
            TreatmentSessionId = session.TreatmentSessionId,
            TreatmentSession = session,
            FromDepartmentId = fromDepartment.DepartmentId,
            FromDepartment = fromDepartment,
            ToDepartmentId = receivingDepartment.DepartmentId,
            ToDepartment = receivingDepartment,
            Reason = "Specialist review",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        var followUp = new FollowUpSchedule
        {
            FollowUpId = 1301,
            TreatmentSessionId = session.TreatmentSessionId,
            TreatmentSession = session,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending"
        };

        await db.AddRangeAsync(
            patientAccount, otherPatientAccount, managerAccount, dentistAccount,
            patient, otherPatient, manager, dentistStaff, dentist,
            fromDepartment, receivingDepartment, departmentAssignment,
            plan, planItem, appointment, session, prescription, referral, followUp);
        await db.SaveChangesAsync();

        return new Scenario(
            patientId,
            otherPatientId,
            managerId,
            dentistId,
            referral.ReferralId,
            followUp.FollowUpId,
            appointment.AppointmentId,
            planItem.TreatmentPlanItemId);
    }

    private sealed record Scenario(
        long PatientId,
        long OtherPatientId,
        long ManagerId,
        long DentistId,
        long ReferralId,
        long FollowUpId,
        long AppointmentId,
        long TreatmentPlanItemId);
}
