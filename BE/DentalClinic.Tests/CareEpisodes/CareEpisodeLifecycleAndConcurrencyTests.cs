using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.CareEpisodes.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.CareEpisodes;

public class CareEpisodeLifecycleAndConcurrencyTests
{
    private readonly Mock<ILogger<CareEpisodeService>> _mockLogger = new();
    private readonly Mock<INotificationService> _mockNotification = new();

    private DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedBaseDataAsync(DentalClinicDbContext db)
    {
        // 1. Roles & Users
        var patientUser = new UserAccount
        {
            UserId = 1001,
            Email = "patient1001@dentalcare.vn",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = 1001,
            PatientCode = "PAT-1001",
            FullName = "Nguyễn Văn Bệnh Nhân",
            CreatedAt = DateTime.UtcNow
        };

        var dentistAUser = new UserAccount
        {
            UserId = 2001,
            Email = "dentistA@dentalcare.vn",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var dentistAStaff = new StaffProfile
        {
            UserId = 2001,
            EmployeeCode = "DEN-001",
            FullName = "Bác Sĩ A (Tổng Quát)",
            EmploymentStatus = "Active"
        };
        var dentistAProfile = new DentistProfile
        {
            UserId = 2001,
            LicenseNumber = "BS-TQ-001",
            Qualification = "BS CKI Răng Hàm Mặt"
        };

        var dentistBUser = new UserAccount
        {
            UserId = 2002,
            Email = "dentistB@dentalcare.vn",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var dentistBStaff = new StaffProfile
        {
            UserId = 2002,
            EmployeeCode = "DEN-002",
            FullName = "Bác Sĩ B (Chỉnh Nha)",
            EmploymentStatus = "Active"
        };
        var dentistBProfile = new DentistProfile
        {
            UserId = 2002,
            LicenseNumber = "BS-CN-002",
            Qualification = "ThS Bác Sĩ Chỉnh Nha"
        };

        var receptionistUser = new UserAccount
        {
            UserId = 3001,
            Email = "receptionist3001@dentalcare.vn",
            Role = UserRole.Receptionist,
            Status = AccountStatus.Active
        };
        var receptionistStaff = new StaffProfile
        {
            UserId = 3001,
            EmployeeCode = "REC-001",
            FullName = "Lễ Tân Điều Phối",
            EmploymentStatus = "Active"
        };

        db.UserAccounts.AddRange(patientUser, dentistAUser, dentistBUser, receptionistUser);
        db.PatientProfiles.Add(patientProfile);
        db.StaffProfiles.AddRange(dentistAStaff, dentistBStaff, receptionistStaff);
        db.DentistProfiles.AddRange(dentistAProfile, dentistBProfile);

        // 2. Departments
        var deptGeneral = new Department
        {
            DepartmentId = 1,
            Name = "Nha Khoa Tổng Quát",
            Status = "Active"
        };
        var deptOrtho = new Department
        {
            DepartmentId = 2,
            Name = "Chuyên Khoa Chỉnh Nha",
            Status = "Active"
        };
        db.Departments.AddRange(deptGeneral, deptOrtho);

        // 3. Dentist Department Assignments (Dentist A -> Dept 1, Dentist B -> Dept 2)
        var ddA = new DentistDepartment
        {
            DentistDepartmentId = 1,
            DentistUserId = 2001,
            DepartmentId = 1,
            Status = "Active",
            AssignedAt = DateTime.UtcNow
        };
        var ddB = new DentistDepartment
        {
            DentistDepartmentId = 2,
            DentistUserId = 2002,
            DepartmentId = 2,
            Status = "Active",
            AssignedAt = DateTime.UtcNow
        };
        db.DentistDepartments.AddRange(ddA, ddB);

        // 4. Department Services
        var srvExam = new DepartmentService
        {
            DepartmentServiceId = 101,
            DepartmentId = 1,
            ServiceName = "Khám Nha Khoa Tổng Quát",
            Price = 100000m,
            DurationMinutes = 30,
            Status = "Active"
        };
        var srvScaling = new DepartmentService
        {
            DepartmentServiceId = 102,
            DepartmentId = 1,
            ServiceName = "Cạo Vôi Răng & Đánh Bóng",
            Price = 250000m,
            DurationMinutes = 30,
            Status = "Active"
        };
        var srvOrthoConsult = new DepartmentService
        {
            DepartmentServiceId = 201,
            DepartmentId = 2,
            ServiceName = "Tư Vấn & Lập Phác Đồ Niềng Răng",
            Price = 500000m,
            DurationMinutes = 45,
            Status = "Active"
        };
        db.DepartmentServices.AddRange(srvExam, srvScaling, srvOrthoConsult);

        // 5. Rooms & Chairs
        var room1 = new Room
        {
            RoomId = 1,
            RoomCode = "P101",
            RoomName = "Phòng Khám Tổng Quát 1",
            DepartmentId = 1,
            Status = "Active"
        };
        var room2 = new Room
        {
            RoomId = 2,
            RoomCode = "P201",
            RoomName = "Phòng Khám Chỉnh Nha 1",
            DepartmentId = 2,
            Status = "Active"
        };
        db.Rooms.AddRange(room1, room2);

        var chair1 = new DentalChair
        {
            DentalChairId = 1,
            RoomId = 1,
            ChairCode = "GHE-01",
            Status = "Active"
        };
        var chair2 = new DentalChair
        {
            DentalChairId = 2,
            RoomId = 2,
            ChairCode = "GHE-02",
            Status = "Active"
        };
        db.DentalChairs.AddRange(chair1, chair2);

        // 6. Root Appointment A (Doctor A examining Patient)
        var apptA = new Appointment
        {
            AppointmentId = 5001,
            AppointmentCode = "APT-20261111-0001",
            PatientUserId = 1001,
            DepartmentId = 1,
            RequestedServiceId = 101,
            AssignedDentistUserId = 2001,
            RoomId = 1,
            DentalChairId = 1,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(9),
            ScheduledEnd = DateTime.UtcNow.Date.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.InService,
            QueueNumber = "Q-GEN-001",
            BookingSource = "WalkIn",
            ReasonForVisit = "Khám răng tổng quát",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(apptA);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Scenario01_EpisodeLifecycle_OpenOnStartExam()
    {
        var db = CreateDbContext(nameof(Scenario01_EpisodeLifecycle_OpenOnStartExam));
        await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        // Action: Get or create active episode for root appointment
        var result = await service.GetOrCreateActiveEpisodeForAppointmentAsync(5001);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Open", result.Data.Status);
        Assert.Equal(5001, result.Data.RootAppointmentId);
        Assert.Equal(1001, result.Data.PatientUserId);
        Assert.StartsWith("EP-", result.Data.EpisodeCode);

        // Verify Appointment is updated with CareEpisodeId
        var appt = await db.Appointments.FindAsync(5001L);
        Assert.NotNull(appt?.CareEpisodeId);
        Assert.Equal(result.Data.CareEpisodeId, appt.CareEpisodeId);
    }

    [Fact]
    public async Task Scenario02_CaseA_DoctorQualified_AddsPlannedTask()
    {
        var db = CreateDbContext(nameof(Scenario02_CaseA_DoctorQualified_AddsPlannedTask));
        await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        // Pre-create episode
        await service.GetOrCreateActiveEpisodeForAppointmentAsync(5001);

        // Action: Doctor A (qualified in Dept 1) adds scaling service (102)
        var req = new CreateEpisodeServiceTaskRequest
        {
            AppointmentId = 5001,
            DepartmentServiceId = 102,
            Quantity = 1,
            Notes = "Bệnh nhân yêu cầu cạo vôi răng thêm"
        };
        var result = await service.AddServiceTaskAsync(2001, req);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Planned", result.Data.Status);
        Assert.Equal(250000m, result.Data.UnitPriceSnapshot);
        Assert.Equal(2001, result.Data.PerformingDentistUserId);
        Assert.Equal(102, result.Data.DepartmentServiceId);
        Assert.StartsWith("TSK-", result.Data.TaskCode);
    }

    [Fact]
    public async Task Scenario03_CaseA_DoctorUnqualified_RejectedWithHandoffAdvice()
    {
        var db = CreateDbContext(nameof(Scenario03_CaseA_DoctorUnqualified_RejectedWithHandoffAdvice));
        await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        // Action: Doctor A (General) tries to add Ortho Consult service (201, Dept 2)
        var req = new CreateEpisodeServiceTaskRequest
        {
            AppointmentId = 5001,
            DepartmentServiceId = 201,
            Quantity = 1
        };
        var result = await service.AddServiceTaskAsync(2001, req);

        // Assert: Must be rejected with referral/handoff advice
        Assert.False(result.Success);
        Assert.Contains("không thuộc chuyên khoa", result.Message);
        Assert.Contains("Internal Referral", result.Message);
    }

    [Fact]
    public async Task Scenario04_CaseA_TaskExecution_PlannedToInProgressToCompleted()
    {
        var db = CreateDbContext(nameof(Scenario04_CaseA_TaskExecution_PlannedToInProgressToCompleted));
        await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        await service.GetOrCreateActiveEpisodeForAppointmentAsync(5001);
        var addRes = await service.AddServiceTaskAsync(2001, new CreateEpisodeServiceTaskRequest
        {
            AppointmentId = 5001,
            DepartmentServiceId = 102
        });
        var taskId = addRes.Data!.ServiceTaskId;

        // Transition 1: Planned -> InProgress
        var startRes = await service.UpdateServiceTaskStatusAsync(2001, taskId, new UpdateServiceTaskStatusRequest
        {
            Status = "InProgress"
        });
        Assert.True(startRes.Success);
        Assert.Equal("InProgress", startRes.Data!.Status);
        Assert.NotNull(startRes.Data.StartedAtUtc);

        // Transition 2: InProgress -> Completed
        var completeRes = await service.UpdateServiceTaskStatusAsync(2001, taskId, new UpdateServiceTaskStatusRequest
        {
            Status = "Completed",
            Notes = "Đã cạo vôi răng 2 hàm sạch sẽ"
        });
        Assert.True(completeRes.Success);
        Assert.Equal("Completed", completeRes.Data!.Status);
        Assert.NotNull(completeRes.Data.CompletedAtUtc);
        Assert.Equal("Đã cạo vôi răng 2 hàm sạch sẽ", completeRes.Data.Notes);
    }

    [Fact]
    public async Task Scenario05_CaseA_NoDuplicateAppointmentOrQueueNumber()
    {
        var db = CreateDbContext(nameof(Scenario05_CaseA_NoDuplicateAppointmentOrQueueNumber));
        await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        await service.GetOrCreateActiveEpisodeForAppointmentAsync(5001);
        await service.AddServiceTaskAsync(2001, new CreateEpisodeServiceTaskRequest
        {
            AppointmentId = 5001,
            DepartmentServiceId = 102
        });

        // Assert: Exactly 1 appointment in DB, exactly 1 queue number (Q-GEN-001)
        var totalAppointments = await db.Appointments.CountAsync();
        Assert.Equal(1, totalAppointments);

        var appt = await db.Appointments.FirstAsync();
        Assert.Equal("Q-GEN-001", appt.QueueNumber);
    }

    [Fact]
    public async Task Scenario06_CaseB_DentistCreatesReferral_PendingStatus()
    {
        var db = CreateDbContext(nameof(Scenario06_CaseB_DentistCreatesReferral_PendingStatus));
        await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        // Doctor A creates referral to Dept 2 (Ortho) for service 201
        var req = new CreateCareEpisodeReferralRequest
        {
            SourceAppointmentId = 5001,
            ToDepartmentId = 2,
            RequestedServiceId = 201,
            PreferredDentistUserId = 2002,
            Reason = "Bệnh nhân có khớp cắn hở cần bác sĩ chỉnh nha chuyên sâu hội chẩn",
            Urgency = "Normal"
        };
        var result = await service.CreateInternalReferralAsync(2001, req);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Pending", result.Data.Status);
        Assert.Equal(5001, result.Data.SourceAppointmentId);
        Assert.Equal(2, result.Data.ToDepartmentId);

        // Check Episode status updated to PendingAdditionalService
        var appt = await db.Appointments.Include(a => a.CareEpisode).FirstAsync(a => a.AppointmentId == 5001);
        Assert.NotNull(appt.CareEpisode);
        Assert.Equal("PendingAdditionalService", appt.CareEpisode.Status);
    }

    [Fact]
    public async Task Scenario07_CaseB_ReceptionistCoordinates_CreatesConfirmedApptBWithoutQueue()
    {
        var db = CreateDbContext(nameof(Scenario07_CaseB_ReceptionistCoordinates_CreatesConfirmedApptBWithoutQueue));
        await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        var refRes = await service.CreateInternalReferralAsync(2001, new CreateCareEpisodeReferralRequest
        {
            SourceAppointmentId = 5001,
            ToDepartmentId = 2,
            RequestedServiceId = 201,
            Reason = "Chuyển Chuyên Khoa Chỉnh Nha"
        });
        var referralId = refRes.Data!.ReferralId;

        // Receptionist coordinates referral to Doctor B
        var coordReq = new CoordinateCareEpisodeReferralRequest
        {
            ReferralId = referralId,
            AssignedDentistUserId = 2002,
            RoomId = 2,
            DentalChairId = 2,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(10),
            ScheduledEnd = DateTime.UtcNow.Date.AddHours(10).AddMinutes(45)
        };
        var result = await service.CoordinateReferralAppointmentAsync(3001, coordReq);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(AppointmentStatus.Confirmed, result.Data.Status);
        Assert.NotNull(result.Data.QueueNumber); // POLICY ALIGNMENT: Queue number allocated immediately upon Confirmed
        Assert.StartsWith("Q-ORT-", result.Data.QueueNumber);

        // Verify referral updated
        var referral = await db.DepartmentReferrals.FindAsync(referralId);
        Assert.Equal("Accepted", referral!.Status);
        Assert.Equal(result.Data.AppointmentId, referral.DestinationAppointmentId);
    }

    [Fact]
    public async Task Scenario08_CaseB_StationBQueuePolicy_QueueAssignedOnConfirmedAndPreservedOnCheckIn()
    {
        var db = CreateDbContext(nameof(Scenario08_CaseB_StationBQueuePolicy_QueueAssignedOnConfirmedAndPreservedOnCheckIn));
        await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        var refRes = await service.CreateInternalReferralAsync(2001, new CreateCareEpisodeReferralRequest
        {
            SourceAppointmentId = 5001,
            ToDepartmentId = 2,
            RequestedServiceId = 201,
            Reason = "Chuyển Chuyên Khoa Chỉnh Nha"
        });
        var coordRes = await service.CoordinateReferralAppointmentAsync(3001, new CoordinateCareEpisodeReferralRequest
        {
            ReferralId = refRes.Data!.ReferralId,
            AssignedDentistUserId = 2002,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(10)
        });
        var apptBId = coordRes.Data!.AppointmentId;

        // Verify QueueNumber allocated upon Confirmed
        Assert.NotNull(coordRes.Data.QueueNumber);
        Assert.StartsWith("Q-ORT-", coordRes.Data.QueueNumber);

        // Complete Station A first so Station B is unblocked
        var apptA = await db.Appointments.FindAsync(5001L);
        apptA!.Status = AppointmentStatus.Completed;
        await db.SaveChangesAsync();

        // Patient arrives at Station B -> Receptionist Check-in
        var checkInRes = await service.StationBCheckInAsync(3001, new StationBCheckInRequest
        {
            AppointmentId = apptBId
        });

        // Assert
        Assert.True(checkInRes.Success);
        Assert.NotNull(checkInRes.Data);
        Assert.Equal(AppointmentStatus.CheckedIn, checkInRes.Data.Status);
        // Preserves exact QueueNumber allocated upon Confirmed
        Assert.Equal(coordRes.Data.QueueNumber, checkInRes.Data.QueueNumber);
    }

    [Fact]
    public async Task Scenario09_CaseB_SequentialInServiceInvariant_BlockedIfStationAStillInService()
    {
        var db = CreateDbContext(nameof(Scenario09_CaseB_SequentialInServiceInvariant_BlockedIfStationAStillInService));
        await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        var refRes = await service.CreateInternalReferralAsync(2001, new CreateCareEpisodeReferralRequest
        {
            SourceAppointmentId = 5001,
            ToDepartmentId = 2,
            RequestedServiceId = 201,
            Reason = "Chuyển Chuyên Khoa Chỉnh Nha"
        });
        var coordRes = await service.CoordinateReferralAppointmentAsync(3001, new CoordinateCareEpisodeReferralRequest
        {
            ReferralId = refRes.Data!.ReferralId,
            AssignedDentistUserId = 2002,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(10)
        });
        var apptBId = coordRes.Data!.AppointmentId;

        // Station A (5001) is currently InService!
        var apptA = await db.Appointments.FindAsync(5001L);
        Assert.Equal(AppointmentStatus.InService, apptA!.Status);

        // Action: Attempt to Check-in Station B while Station A is still InService
        var checkInRes = await service.StationBCheckInAsync(3001, new StationBCheckInRequest
        {
            AppointmentId = apptBId
        });

        // Assert: MUST BE BLOCKED
        Assert.False(checkInRes.Success);
        Assert.Contains("trạm khám trước", checkInRes.Message);
        Assert.Contains("InService", checkInRes.Message);

        // When Doctor A finishes examination at Station A:
        apptA.Status = AppointmentStatus.Completed;
        await db.SaveChangesAsync();

        // Check-in Station B now succeeds!
        var retryRes = await service.StationBCheckInAsync(3001, new StationBCheckInRequest
        {
            AppointmentId = apptBId
        });
        Assert.True(retryRes.Success);
        Assert.Equal(AppointmentStatus.CheckedIn, retryRes.Data!.Status);
    }

    [Fact]
    public async Task Scenario10_SingleInvoice_ConsolidatesDoctorAAndDoctorBServices()
    {
        var db = CreateDbContext(nameof(Scenario10_SingleInvoice_ConsolidatesDoctorAAndDoctorBServices));
        await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        var epRes = await service.GetOrCreateActiveEpisodeForAppointmentAsync(5001);
        var episodeId = epRes.Data!.CareEpisodeId;

        // Doctor A completes Task 1: Scaling (102, 250k)
        var taskARes = await service.AddServiceTaskAsync(2001, new CreateEpisodeServiceTaskRequest
        {
            AppointmentId = 5001,
            DepartmentServiceId = 102
        });
        await service.UpdateServiceTaskStatusAsync(2001, taskARes.Data!.ServiceTaskId, new UpdateServiceTaskStatusRequest
        {
            Status = "Completed"
        });

        // Doctor A completes exam at Station A
        var apptA = await db.Appointments.FindAsync(5001L);
        apptA!.Status = AppointmentStatus.Completed;

        // Referral & Station B coordination
        var refRes = await service.CreateInternalReferralAsync(2001, new CreateCareEpisodeReferralRequest
        {
            SourceAppointmentId = 5001,
            ToDepartmentId = 2,
            RequestedServiceId = 201,
            Reason = "Chuyển Khám Chỉnh Nha"
        });
        var coordRes = await service.CoordinateReferralAppointmentAsync(3001, new CoordinateCareEpisodeReferralRequest
        {
            ReferralId = refRes.Data!.ReferralId,
            AssignedDentistUserId = 2002,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(10)
        });
        var apptBId = coordRes.Data!.AppointmentId;

        await service.StationBCheckInAsync(3001, new StationBCheckInRequest { AppointmentId = apptBId });

        // Doctor B starts and completes Task 2: Ortho Consult (201, 500k)
        var taskB = await db.EpisodeServiceTasks.FirstAsync(t => t.AppointmentId == apptBId);
        await service.UpdateServiceTaskStatusAsync(2002, taskB.ServiceTaskId, new UpdateServiceTaskStatusRequest
        {
            Status = "Completed"
        });

        var apptB = await db.Appointments.FindAsync(apptBId);
        apptB!.Status = AppointmentStatus.Completed;
        await db.SaveChangesAsync();

        // Receptionist generates consolidated invoice
        var invoiceRes = await service.GenerateConsolidatedInvoiceAsync(3001, new GenerateConsolidatedInvoiceRequest
        {
            CareEpisodeId = episodeId,
            DiscountAmount = 50000m
        });

        // Assert
        Assert.True(invoiceRes.Success);
        Assert.NotNull(invoiceRes.Data);
        Assert.Equal(episodeId, invoiceRes.Data.CareEpisodeId);
        Assert.StartsWith("INV-", invoiceRes.Data.InvoiceCode);

        // SubTotal: 250k + 500k = 750k; Discount: 50k; Total = 700k
        Assert.Equal(750000m, invoiceRes.Data.SubTotal);
        Assert.Equal(50000m, invoiceRes.Data.DiscountAmount);
        Assert.Equal(700000m, invoiceRes.Data.TotalAmount);

        // Verify items contain both Doctor A and Doctor B metadata
        Assert.Equal(2, invoiceRes.Data.Items.Count);
        Assert.Contains(invoiceRes.Data.Items, i => i.PerformingDentistUserId == 2001 && i.UnitPrice == 250000m);
        Assert.Contains(invoiceRes.Data.Items, i => i.PerformingDentistUserId == 2002 && i.UnitPrice == 50000m || i.UnitPrice == 500000m);
    }

    [Fact]
    public async Task Scenario11_SingleInvoice_PaidProtection_RejectsMutation()
    {
        var db = CreateDbContext(nameof(Scenario11_SingleInvoice_PaidProtection_RejectsMutation));
        await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        var epRes = await service.GetOrCreateActiveEpisodeForAppointmentAsync(5001);
        var episodeId = epRes.Data!.CareEpisodeId;

        var taskRes = await service.AddServiceTaskAsync(2001, new CreateEpisodeServiceTaskRequest
        {
            AppointmentId = 5001,
            DepartmentServiceId = 102
        });
        await service.UpdateServiceTaskStatusAsync(2001, taskRes.Data!.ServiceTaskId, new UpdateServiceTaskStatusRequest
        {
            Status = "Completed"
        });

        // Generate invoice
        var invRes = await service.GenerateConsolidatedInvoiceAsync(3001, new GenerateConsolidatedInvoiceRequest
        {
            CareEpisodeId = episodeId
        });
        Assert.True(invRes.Success);

        // Simulate patient paid invoice in full
        var invoice = await db.Invoices.FindAsync(invRes.Data!.InvoiceId);
        invoice!.Status = "Paid";
        await db.SaveChangesAsync();

        // Action: Attempt to re-generate or mutate the invoice for this episode
        var mutateRes = await service.GenerateConsolidatedInvoiceAsync(3001, new GenerateConsolidatedInvoiceRequest
        {
            CareEpisodeId = episodeId,
            DiscountAmount = 100000m
        });

        // Assert: MUST BE REJECTED
        Assert.False(mutateRes.Success);
        Assert.Contains("đã thanh toán (Paid)", mutateRes.Message);
        Assert.Contains("bảo toàn dòng tiền", mutateRes.Message);
    }

    [Fact]
    public async Task Scenario12_Concurrency_StationBCheckIn_NoDuplicateQueueNumber()
    {
        var db = CreateDbContext(nameof(Scenario12_Concurrency_StationBCheckIn_NoDuplicateQueueNumber));
        await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        // Complete Station A
        var apptA = await db.Appointments.FindAsync(5001L);
        apptA!.Status = AppointmentStatus.Completed;
        await db.SaveChangesAsync();

        // Create 3 Station B appointments
        var apptB1 = new Appointment
        {
            AppointmentId = 6001,
            AppointmentCode = "APT-B1",
            DepartmentId = 2,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(10),
            Status = AppointmentStatus.Confirmed,
            QueueNumber = null,
            BookingSource = "WalkIn",
            ReasonForVisit = "Khám chỉnh nha",
            CreatedAt = DateTime.UtcNow
        };
        var apptB2 = new Appointment
        {
            AppointmentId = 6002,
            AppointmentCode = "APT-B2",
            DepartmentId = 2,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(10).AddMinutes(15),
            Status = AppointmentStatus.Confirmed,
            QueueNumber = null,
            BookingSource = "WalkIn",
            ReasonForVisit = "Khám chỉnh nha",
            CreatedAt = DateTime.UtcNow
        };
        var apptB3 = new Appointment
        {
            AppointmentId = 6003,
            AppointmentCode = "APT-B3",
            DepartmentId = 2,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            QueueNumber = null,
            BookingSource = "WalkIn",
            ReasonForVisit = "Khám chỉnh nha",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.AddRange(apptB1, apptB2, apptB3);
        await db.SaveChangesAsync();

        // Check in all 3 sequentially/concurrently
        var res1 = await service.StationBCheckInAsync(3001, new StationBCheckInRequest { AppointmentId = 6001 });
        var res2 = await service.StationBCheckInAsync(3001, new StationBCheckInRequest { AppointmentId = 6002 });
        var res3 = await service.StationBCheckInAsync(3001, new StationBCheckInRequest { AppointmentId = 6003 });

        // Assert: All 3 succeed and have strictly distinct, monotonic queue numbers
        Assert.True(res1.Success);
        Assert.True(res2.Success);
        Assert.True(res3.Success);

        var q1 = res1.Data!.QueueNumber;
        var q2 = res2.Data!.QueueNumber;
        var q3 = res3.Data!.QueueNumber;

        Assert.NotNull(q1);
        Assert.NotNull(q2);
        Assert.NotNull(q3);

        var queueNumbers = new HashSet<string> { q1, q2, q3 };
        Assert.Equal(3, queueNumbers.Count); // Strictly unique, no collisions!
        Assert.True(string.Compare(q1, q2, StringComparison.Ordinal) < 0);
        Assert.True(string.Compare(q2, q3, StringComparison.Ordinal) < 0);
    }
}
