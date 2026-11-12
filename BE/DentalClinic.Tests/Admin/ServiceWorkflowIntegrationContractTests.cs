using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Admin.DTOs;
using DentalClinic.Application.Features.Appointments.DTOs;
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

namespace DentalClinic.Tests.Admin;

public class ServiceWorkflowIntegrationContractTests
{
    private readonly Mock<ILogger<AdminService>> _mockAdminLogger = new();
    private readonly Mock<ILogger<CareEpisodeService>> _mockCareLogger = new();
    private readonly Mock<ILogger<AppointmentService>> _mockApptLogger = new();
    private readonly Mock<IAuditLogService> _mockAudit = new();
    private readonly Mock<INotificationService> _mockNotification = new();

    private DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedContractDataAsync(DentalClinicDbContext db)
    {
        // 1. Department 1: Răng Tổng Quát & Nội Nha
        var dept1 = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Tổng Quát & Nội Nha",
            Status = "Active"
        };
        db.Departments.Add(dept1);

        // 2. Services: General Consultation (1), Scaling (2), Filling (3)
        var srvConsultation = new DepartmentService
        {
            DepartmentServiceId = 1,
            DepartmentId = 1,
            ServiceCode = "SRV-GEN-001",
            ServiceName = "Khám tổng quát & Tư vấn",
            DurationMinutes = 20,
            Price = 100000,
            Status = "Active"
        };
        var srvScaling = new DepartmentService
        {
            DepartmentServiceId = 2,
            DepartmentId = 1,
            ServiceCode = "SRV-GEN-002",
            ServiceName = "Lấy cao răng & Đánh bóng",
            DurationMinutes = 30,
            Price = 250000,
            Status = "Active"
        };
        var srvFilling = new DepartmentService
        {
            DepartmentServiceId = 3,
            DepartmentId = 1,
            ServiceCode = "SRV-GEN-003",
            ServiceName = "Trám răng Composite thẩm mỹ",
            DurationMinutes = 40,
            Price = 350000,
            Status = "Active"
        };
        db.DepartmentServices.AddRange(srvConsultation, srvScaling, srvFilling);

        // 3. Separate workflows for each service
        var wfConsultation = new ServiceWorkflow
        {
            WorkflowId = 101,
            DepartmentServiceId = 1,
            WorkflowName = "Quy trình: Khám tổng quát & Tư vấn",
            Version = 1,
            IsActive = true,
            ApprovalStatus = "Approved",
            Steps = new List<ServiceWorkflowStep>
            {
                new() { StepId = 1001, StepName = "Khám tổng quan cung răng", StepOrder = 1, IsRequired = true, CompletionRequirement = "Đánh giá 32 răng", ExpectedDataType = "Text" },
                new() { StepId = 1002, StepName = "Tư vấn phương án điều trị", StepOrder = 2, IsRequired = true, CompletionRequirement = "Thống nhất phác đồ", ExpectedDataType = "Text" }
            }
        };

        var wfScaling = new ServiceWorkflow
        {
            WorkflowId = 201,
            DepartmentServiceId = 2,
            WorkflowName = "Quy trình: Lấy cao răng & Đánh bóng",
            Version = 1,
            IsActive = true,
            ApprovalStatus = "Approved",
            Steps = new List<ServiceWorkflowStep>
            {
                new() { StepId = 2001, StepName = "Súc miệng sát khuẩn", StepOrder = 1, IsRequired = true, CompletionRequirement = "Súc miệng Chlorhexidine", ExpectedDataType = "Boolean" },
                new() { StepId = 2002, StepName = "Lấy cao răng siêu âm", StepOrder = 2, IsRequired = true, CompletionRequirement = "Làm sạch trên và dưới nướu", ExpectedDataType = "Text" },
                new() { StepId = 2003, StepName = "Đánh bóng bề mặt men", StepOrder = 3, IsRequired = true, CompletionRequirement = "Đánh bóng bằng chổi và bột sò", ExpectedDataType = "Boolean" }
            }
        };

        var wfFilling = new ServiceWorkflow
        {
            WorkflowId = 301,
            DepartmentServiceId = 3,
            WorkflowName = "Quy trình: Trám răng Composite",
            Version = 1,
            IsActive = true,
            ApprovalStatus = "Approved",
            Steps = new List<ServiceWorkflowStep>
            {
                new() { StepId = 3001, StepName = "Cách ly và làm sạch xoang sâu", StepOrder = 1, IsRequired = true, CompletionRequirement = "Loại bỏ ngà mủn", ExpectedDataType = "Text" },
                new() { StepId = 3002, StepName = "Xoi mòn & Dán dính Etching/Bonding", StepOrder = 2, IsRequired = true, CompletionRequirement = "Chiếu đèn trùng hợp keo", ExpectedDataType = "Boolean" },
                new() { StepId = 3003, StepName = "Trám từng lớp Composite", StepOrder = 3, IsRequired = true, CompletionRequirement = "Tái tạo múi răng giải phẫu", ExpectedDataType = "Number" },
                new() { StepId = 3004, StepName = "Chỉnh cộm và đánh bóng", StepOrder = 4, IsRequired = true, CompletionRequirement = "Kiểm tra giấy cắn không vướng", ExpectedDataType = "Boolean" }
            }
        };

        db.ServiceWorkflows.AddRange(wfConsultation, wfScaling, wfFilling);

        // 4. Users (Patient, Dentist, Receptionist)
        var patientUser = new UserAccount
        {
            UserId = 1001,
            Email = "patient.contract@dentalcare.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = 1001,
            PatientCode = "PAT-CONTRACT-01",
            FullName = "Bệnh Nhân Test Contract",
            CreatedAt = DateTime.UtcNow,
            User = patientUser
        };

        var dentistUser = new UserAccount
        {
            UserId = 2001,
            Email = "dentist.contract@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var dentistStaff = new StaffProfile
        {
            UserId = 2001,
            EmployeeCode = "DEN-CONTRACT-01",
            FullName = "Bác Sĩ Test Contract",
            EmploymentStatus = "Active",
            User = dentistUser
        };
        var dentistProfile = new DentistProfile
        {
            UserId = 2001,
            LicenseNumber = "BS-CONTRACT-01",
            Qualification = "BS CKI RHM",
            User = dentistStaff
        };

        var recUser = new UserAccount
        {
            UserId = 3001,
            Email = "rec.contract@dentalcare.com",
            Role = UserRole.Receptionist,
            Status = AccountStatus.Active
        };
        var recStaff = new StaffProfile
        {
            UserId = 3001,
            EmployeeCode = "REC-CONTRACT-01",
            FullName = "Lễ Tân Test Contract",
            EmploymentStatus = "Active",
            User = recUser
        };

        db.UserAccounts.AddRange(patientUser, dentistUser, recUser);
        db.PatientProfiles.Add(patientProfile);
        db.StaffProfiles.AddRange(dentistStaff, recStaff);
        db.DentistProfiles.Add(dentistProfile);

        // 5. Dentist Department Allocation
        var dentistDept = new DentistDepartment
        {
            DentistUserId = 2001,
            DepartmentId = 1,
            Status = "Active"
        };
        db.DentistDepartments.Add(dentistDept);

        // 6. Appointment
        var appt = new Appointment
        {
            AppointmentId = 5001,
            AppointmentCode = "APT-CONTRACT-01",
            PatientUserId = 1001,
            DepartmentId = 1,
            RequestedServiceId = 1,
            AssignedDentistUserId = 2001,
            Status = AppointmentStatus.Confirmed,
            ScheduledStart = DateTime.UtcNow,
            ScheduledEnd = DateTime.UtcNow.AddMinutes(30),
            ReasonForVisit = "Khám răng tổng quát",
            BookingSource = "Online"
        };
        db.Appointments.Add(appt);

        // 7. CareEpisode
        var episode = new CareEpisode
        {
            CareEpisodeId = 6001,
            EpisodeCode = "EP-CONTRACT-01",
            PatientUserId = 1001,
            RootAppointmentId = 5001,
            Status = "Open",
            OpenedAtUtc = DateTime.UtcNow
        };
        db.CareEpisodes.Add(episode);
        appt.CareEpisodeId = 6001;

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Criterion1_2_3_Scaling_Filling_And_GeneralConsultation_HaveSeparateDistinctWorkflows()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedContractDataAsync(db);

        var adminService = new AdminService(db, _mockAudit.Object, _mockAdminLogger.Object);

        // 1. General Consultation workflow
        var consultWf = await adminService.GetWorkflowAsync(serviceId: 1);
        Assert.True(consultWf.Success);
        Assert.Equal(101, consultWf.Data!.WorkflowId);
        Assert.Equal(2, consultWf.Data.Steps.Count);
        Assert.Equal("Khám tổng quan cung răng", consultWf.Data.Steps[0].StepName);

        // 2. Scaling workflow
        var scalingWf = await adminService.GetWorkflowAsync(serviceId: 2);
        Assert.True(scalingWf.Success);
        Assert.Equal(201, scalingWf.Data!.WorkflowId);
        Assert.Equal(3, scalingWf.Data.Steps.Count);
        Assert.Equal("Súc miệng sát khuẩn", scalingWf.Data.Steps[0].StepName);
        Assert.Equal("Đánh bóng bề mặt men", scalingWf.Data.Steps[2].StepName);

        // 3. Filling workflow
        var fillingWf = await adminService.GetWorkflowAsync(serviceId: 3);
        Assert.True(fillingWf.Success);
        Assert.Equal(301, fillingWf.Data!.WorkflowId);
        Assert.Equal(4, fillingWf.Data.Steps.Count);
        Assert.Equal("Trám từng lớp Composite", fillingWf.Data.Steps[2].StepName);

        // Strict distinctness: None of the workflows share WorkflowId or step definitions
        Assert.NotEqual(consultWf.Data.WorkflowId, scalingWf.Data.WorkflowId);
        Assert.NotEqual(scalingWf.Data.WorkflowId, fillingWf.Data.WorkflowId);
        Assert.NotEqual(consultWf.Data.WorkflowId, fillingWf.Data.WorkflowId);
    }

    [Fact]
    public async Task Criterion4_DraftOrUnconfiguredService_DoesNotFallbackToGeneralConsultation()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedContractDataAsync(db);

        // Add a new service with NO active workflow (e.g. Draft or no workflow)
        var unconfiguredService = new DepartmentService
        {
            DepartmentServiceId = 99,
            DepartmentId = 1,
            ServiceCode = "SRV-GEN-099",
            ServiceName = "Thủ thuật thử nghiệm mới",
            DurationMinutes = 30,
            Price = 500000,
            Status = "Active"
        };
        db.DepartmentServices.Add(unconfiguredService);
        await db.SaveChangesAsync();

        var careEpisodeService = new CareEpisodeService(db, _mockCareLogger.Object, _mockNotification.Object);

        // Dentist assigns this unconfigured service to the patient's CareEpisode
        var request = new CreateEpisodeServiceTaskRequest
        {
            AppointmentId = 5001,
            DepartmentServiceId = 99,
            Quantity = 1,
            Notes = "Chỉ định dịch vụ chưa có workflow active"
        };

        var result = await careEpisodeService.AddServiceTaskAsync(2001, request);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);

        // Strict invariant: Must NOT fallback to General Consultation (Workflow 101)
        Assert.Null(result.Data.AppliedWorkflowId);
        Assert.Null(result.Data.AppliedWorkflowVersion);
        Assert.NotEqual(101, result.Data.AppliedWorkflowId); // NOT General Consultation!
        Assert.Equal("Workflow Not Configured / Pending Clinical Review", result.Data.WorkflowStatusText);

        // Verify task stored in DB has null workflow
        var taskInDb = await db.EpisodeServiceTasks.FindAsync(result.Data.ServiceTaskId);
        Assert.NotNull(taskInDb);
        Assert.Null(taskInDb.AppliedWorkflowId);
        Assert.Null(taskInDb.AppliedWorkflowVersion);
    }

    [Fact]
    public async Task Criterion5_UpdatingWorkflow_PreservesVersionOfExistingRunningTasks_ImmutableContract()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedContractDataAsync(db);

        var careEpisodeService = new CareEpisodeService(db, _mockCareLogger.Object, _mockNotification.Object);
        var adminService = new AdminService(db, _mockAudit.Object, _mockAdminLogger.Object);

        // 1. Task 1 is created under Scaling Workflow v1 (WorkflowId = 201, Version = 1)
        var task1Res = await careEpisodeService.AddServiceTaskAsync(2001, new CreateEpisodeServiceTaskRequest
        {
            AppointmentId = 5001,
            DepartmentServiceId = 2, // Scaling
            Quantity = 1,
            Notes = "Thực hiện cạo vôi đợt 1"
        });
        Assert.True(task1Res.Success, task1Res.Message);
        Assert.Equal(201, task1Res.Data!.AppliedWorkflowId);
        Assert.Equal(1, task1Res.Data.AppliedWorkflowVersion);

        // 2. Admin updates the Scaling workflow to Version 2 with 5 steps
        var updateReq = new ConfigureWorkflowRequest
        {
            WorkflowName = "Quy trình: Lấy cao răng & Đánh bóng (Chuẩn Quốc tế v2)",
            ActivateImmediately = true,
            ApprovalStatus = "Approved",
            Steps = new List<ServiceWorkflowStepInputDto>
            {
                new() { StepName = "Khám và chấm điểm mảng bám OHI-S", StepOrder = 1, IsRequired = true, ExpectedDataType = "Number", CompletionRequirement = "Ghi chỉ số OHI-S" },
                new() { StepName = "Súc miệng sát khuẩn Chlorhexidine", StepOrder = 2, IsRequired = true, ExpectedDataType = "Boolean", CompletionRequirement = "Súc 30 giây" },
                new() { StepName = "Lấy cao răng siêu âm đa tần", StepOrder = 3, IsRequired = true, ExpectedDataType = "Text", CompletionRequirement = "Làm sạch túi lợi" },
                new() { StepName = "Đánh bóng men răng", StepOrder = 4, IsRequired = true, ExpectedDataType = "Boolean", CompletionRequirement = "Đánh bóng mịn" },
                new() { StepName = "Bôi gel Fluoride tái khoáng", StepOrder = 5, IsRequired = true, ExpectedDataType = "Boolean", CompletionRequirement = "Phủ đều 4 góc hàm" }
            }
        };

        var wf2Res = await adminService.ConfigureWorkflowAsync(serviceId: 2, updateReq, adminUserId: 9999);
        Assert.True(wf2Res.Success);
        Assert.Equal(2, wf2Res.Data!.Version);
        Assert.Equal(5, wf2Res.Data.Steps.Count);
        long wf2Id = wf2Res.Data.WorkflowId;
        Assert.NotEqual(201, wf2Id); // New workflow ID generated

        // 3. Verify Task 1 STILL references Version 1 (Immutable history!)
        var task1InDb = await db.EpisodeServiceTasks.FindAsync(task1Res.Data.ServiceTaskId);
        Assert.NotNull(task1InDb);
        Assert.Equal(201, task1InDb.AppliedWorkflowId); // Still v1!
        Assert.Equal(1, task1InDb.AppliedWorkflowVersion);

        // 4. A newly created Task 2 for Scaling now gets Version 2
        var task2Res = await careEpisodeService.AddServiceTaskAsync(2001, new CreateEpisodeServiceTaskRequest
        {
            AppointmentId = 5001,
            DepartmentServiceId = 2,
            Quantity = 1,
            Notes = "Thực hiện cạo vôi theo quy trình v2 mới"
        });
        Assert.True(task2Res.Success);
        Assert.Equal(wf2Id, task2Res.Data!.AppliedWorkflowId);
        Assert.Equal(2, task2Res.Data.AppliedWorkflowVersion);
    }

    [Fact]
    public void Criterion6_StrictFourRoles_NoDepartmentManagerRoleExists()
    {
        // Enforce the 4-role domain model
        var validRoles = UserRole.All;
        Assert.Contains("Patient", validRoles);
        Assert.Contains("Receptionist", validRoles);
        Assert.Contains("Dentist", validRoles);
        Assert.Contains("SystemAdministrator", validRoles);
        Assert.Equal(4, validRoles.Length);

        // Strict rejection: DepartmentManager is NOT a role
        Assert.DoesNotContain("DepartmentManager", validRoles);
        Assert.DoesNotContain("Manager", validRoles);
    }

    [Fact]
    public async Task Criterion7_AdminCanConfigure5StepWorkflow_CompatibleWithDentistTaskEngine()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedContractDataAsync(db);

        var adminService = new AdminService(db, _mockAudit.Object, _mockAdminLogger.Object);

        // Admin configures a brand-new 5-step workflow
        var req = new ConfigureWorkflowRequest
        {
            WorkflowName = "Quy trình 5 bước phẫu thuật nha chu",
            Description = "Tiêu chuẩn kỹ thuật 5 bước cho Dentist Task Engine",
            ActivateImmediately = true,
            ApprovalStatus = "Approved",
            Steps = new List<ServiceWorkflowStepInputDto>
            {
                new() { StepName = "Bước 1: Sát khuẩn và gây tê", StepOrder = 1, IsRequired = true, ExpectedDataType = "Text", CompletionRequirement = "Đạt tê hoàn toàn" },
                new() { StepName = "Bước 2: Rạch vạt màng xương", StepOrder = 2, IsRequired = true, ExpectedDataType = "Text", CompletionRequirement = "Bộc lộ vùng phẫu thuật" },
                new() { StepName = "Bước 3: Nạo túi lợi và làm sạch gốc răng", StepOrder = 3, IsRequired = true, ExpectedDataType = "Number", CompletionRequirement = "Đo độ sâu túi lợi sau nạo" },
                new() { StepName = "Bước 4: Bơm rửa dung dịch kháng sinh", StepOrder = 4, IsRequired = true, ExpectedDataType = "Boolean", CompletionRequirement = "Bơm rửa Metronidazole" },
                new() { StepName = "Bước 5: Khâu đóng vạt", StepOrder = 5, IsRequired = true, ExpectedDataType = "Selection", CompletionRequirement = "Khâu mũi rời chỉ 4-0" }
            }
        };

        var configRes = await adminService.ConfigureWorkflowAsync(serviceId: 3, req, adminUserId: 9999);
        Assert.True(configRes.Success);
        Assert.NotNull(configRes.Data);
        Assert.Equal(5, configRes.Data.Steps.Count);

        // Read workflow back as Dentist Task Engine would
        var getWfRes = await adminService.GetWorkflowAsync(serviceId: 3);
        Assert.True(getWfRes.Success);
        var steps = getWfRes.Data!.Steps;
        Assert.Equal(5, steps.Count);

        // Verify StepOrder, ExpectedDataType, CompletionRequirement are fully preserved
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(i + 1, steps[i].StepOrder);
            Assert.Equal(req.Steps[i].StepName, steps[i].StepName);
            Assert.Equal(req.Steps[i].ExpectedDataType, steps[i].ExpectedDataType);
            Assert.Equal(req.Steps[i].CompletionRequirement, steps[i].CompletionRequirement);
        }
    }

    [Fact]
    public async Task Criterion8_QueueNumberFormat_StrictlyAdheresToDepartmentCodeAndSequence()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedContractDataAsync(db);

        // Add Room and Chair to Dept 1
        var room = new Room { RoomId = 1, RoomCode = "R-101", DepartmentId = 1, Status = "Active" };
        var chair = new DentalChair { DentalChairId = 11, RoomId = 1, ChairCode = "C-101-A", Status = "Active" };
        db.Rooms.Add(room);
        db.DentalChairs.Add(chair);

        // Pending appointment to confirm
        var pendingAppt = new Appointment
        {
            AppointmentId = 5002,
            AppointmentCode = "APT-CONTRACT-02",
            PatientUserId = 1001,
            DepartmentId = 1,
            RequestedServiceId = 1,
            Status = AppointmentStatus.PendingReceptionReview,
            ScheduledStart = DateTime.Today.AddHours(9),
            ScheduledEnd = DateTime.Today.AddHours(9).AddMinutes(30),
            ReasonForVisit = "Đau răng hàm dưới",
            BookingSource = "WalkIn"
        };
        db.Appointments.Add(pendingAppt);

        // Work schedule for Dentist 2001
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 2001,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(DateTime.Today),
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(23, 59),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var apptService = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);

        // Confirm appointment
        var confirmReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = 2001,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = DateTime.Today.AddHours(9),
            ScheduledEnd = DateTime.Today.AddHours(9).AddMinutes(30)
        };

        var confirmRes = await apptService.ManagerConfirmAsync(5002, 3001, confirmReq);
        Assert.True(confirmRes.Success, confirmRes.Message);
        Assert.NotNull(confirmRes.Data);
        Assert.NotNull(confirmRes.Data.QueueNumber);

        // Strict pattern check: Q-{deptCode}-{seq:D3} (e.g. Q-TQ-001 for Dept 1 Tổng Quát)
        Assert.Matches(@"^Q-[A-Z0-9]+-\d{3}$", confirmRes.Data.QueueNumber);
        Assert.StartsWith("Q-TQ-", confirmRes.Data.QueueNumber);
        Assert.Equal("Q-TQ-001", confirmRes.Data.QueueNumber);
    }
}
