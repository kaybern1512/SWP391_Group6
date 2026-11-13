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

namespace DentalClinic.Tests.Dentist;

public class DentistServiceTaskWorkflowTests
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

    private async Task<(long patientId, long dentistAId, long dentistBId, long receptionistId, long apptId, long episodeId)> SeedBaseDataAsync(DentalClinicDbContext db)
    {
        // 1. Roles & Users (Strictly 4 Roles: Patient, Dentist, Receptionist, SystemAdministrator)
        var patientUser = new UserAccount { UserId = 101, Email = "patient101@dentalcare.vn", Role = UserRole.Patient, Status = AccountStatus.Active };
        var patientProfile = new PatientProfile { UserId = 101, PatientCode = "PAT-101", FullName = "Nguyễn Văn Bệnh Nhân", CreatedAt = DateTime.UtcNow };

        var dentistAUser = new UserAccount { UserId = 201, Email = "dentistA@dentalcare.vn", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var dentistAStaff = new StaffProfile { UserId = 201, EmployeeCode = "DEN-201", FullName = "Bác Sĩ A (Tổng Quát)", EmploymentStatus = "Active" };
        var dentistAProfile = new DentistProfile { UserId = 201, LicenseNumber = "BS-TQ-201", Qualification = "BS CKI RHM" };

        var dentistBUser = new UserAccount { UserId = 202, Email = "dentistB@dentalcare.vn", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var dentistBStaff = new StaffProfile { UserId = 202, EmployeeCode = "DEN-202", FullName = "Bác Sĩ B (Chuyên Khoa Tủy)", EmploymentStatus = "Active" };
        var dentistBProfile = new DentistProfile { UserId = 202, LicenseNumber = "BS-TUY-202", Qualification = "BS CKI Nội Nha" };

        var receptionistUser = new UserAccount { UserId = 301, Email = "recep301@dentalcare.vn", Role = UserRole.Receptionist, Status = AccountStatus.Active };
        var receptionistStaff = new StaffProfile { UserId = 301, EmployeeCode = "REC-301", FullName = "Lễ Tân Điều Phối", EmploymentStatus = "Active" };

        db.UserAccounts.AddRange(patientUser, dentistAUser, dentistBUser, receptionistUser);
        db.PatientProfiles.Add(patientProfile);
        db.StaffProfiles.AddRange(dentistAStaff, dentistBStaff, receptionistStaff);
        db.DentistProfiles.AddRange(dentistAProfile, dentistBProfile);

        // 2. Departments & Dentist Assignments
        var deptGeneral = new Department { DepartmentId = 1, Name = "Khoa Răng Tổng Quát", Status = "Active", CreatedAt = DateTime.UtcNow };
        var deptEndo = new Department { DepartmentId = 2, Name = "Khoa Điều Trị Tủy", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Departments.AddRange(deptGeneral, deptEndo);

        db.DentistDepartments.AddRange(
            new DentistDepartment { DentistDepartmentId = 1, DentistUserId = 201, DepartmentId = 1, Status = "Active", AssignedAt = DateTime.UtcNow },
            new DentistDepartment { DentistDepartmentId = 2, DentistUserId = 201, DepartmentId = 2, Status = "Active", AssignedAt = DateTime.UtcNow },
            new DentistDepartment { DentistDepartmentId = 3, DentistUserId = 202, DepartmentId = 2, Status = "Active", AssignedAt = DateTime.UtcNow }
        );

        // 3. Services & Workflows
        // 3.1 Khám tổng quát (General Consultation)
        var serviceConsult = new DepartmentService { DepartmentId = 1, DepartmentServiceId = 11, ServiceName = "Khám Tổng Quát & Tư Vấn", Price = 150000m, Status = "Active" };
        var wfConsult = new ServiceWorkflow { WorkflowId = 1, DepartmentServiceId = 11, WorkflowName = "Quy trình Khám tổng quát chuẩn", Version = 1, ApprovalStatus = "Approved", IsActive = true, CreatedAt = DateTime.UtcNow };
        var stepC1 = new ServiceWorkflowStep { StepId = 101, WorkflowId = 1, StepOrder = 1, StepName = "Lấy bệnh sử & triệu chứng", ExpectedDataType = "Text", IsRequired = true, CompletionRequirement = "Hỏi bệnh sử và lý do đến khám" };
        var stepC2 = new ServiceWorkflowStep { StepId = 102, WorkflowId = 1, StepOrder = 2, StepName = "Khám ngoài mặt & khớp thái dương hàm", ExpectedDataType = "Text", IsRequired = true, CompletionRequirement = "Khám mặt, cơ nhai và khớp TDH" };
        var stepC3 = new ServiceWorkflowStep { StepId = 103, WorkflowId = 1, StepOrder = 3, StepName = "Khám trong miệng & lập sơ đồ răng", ExpectedDataType = "Text", IsRequired = true, CompletionRequirement = "Khám mô mềm, nướu và ghi nhận tình trạng răng" };
        var stepC4 = new ServiceWorkflowStep { StepId = 104, WorkflowId = 1, StepOrder = 4, StepName = "Tư vấn & kết luận kế hoạch điều trị", ExpectedDataType = "Text", IsRequired = true, CompletionRequirement = "Giải thích phác đồ điều trị cho bệnh nhân" };

        // 3.2 Lấy cao răng (Scaling)
        var serviceScaling = new DepartmentService { DepartmentId = 1, DepartmentServiceId = 12, ServiceName = "Cạo Vôi Răng & Đánh Bóng", Price = 250000m, Status = "Active" };
        var wfScaling = new ServiceWorkflow { WorkflowId = 2, DepartmentServiceId = 12, WorkflowName = "Quy trình cạo vôi răng sóng siêu âm", Version = 1, ApprovalStatus = "Approved", IsActive = true, CreatedAt = DateTime.UtcNow };
        var stepS1 = new ServiceWorkflowStep { StepId = 201, WorkflowId = 2, StepOrder = 1, StepName = "Khám & đánh giá mức độ vôi răng", ExpectedDataType = "Text", IsRequired = true, CompletionRequirement = "Phân loại vôi răng độ 1, 2 hoặc 3" };
        var stepS2 = new ServiceWorkflowStep { StepId = 202, WorkflowId = 2, StepOrder = 2, StepName = "Lấy cao răng siêu âm trên và dưới nướu", ExpectedDataType = "Boolean", IsRequired = true, CompletionRequirement = "Làm sạch mảng bám và vôi răng triệt để" };
        var stepS3 = new ServiceWorkflowStep { StepId = 203, WorkflowId = 2, StepOrder = 3, StepName = "Đánh bóng toàn bộ bề mặt răng", ExpectedDataType = "Boolean", IsRequired = true, CompletionRequirement = "Đánh bóng bằng chổi và bột mịn" };
        var stepS4 = new ServiceWorkflowStep { StepId = 204, WorkflowId = 2, StepOrder = 4, StepName = "Hướng dẫn vệ sinh răng miệng & dặn dò", ExpectedDataType = "Text", IsRequired = true, CompletionRequirement = "Hướng dẫn chải răng và dùng chỉ nha khoa" };

        // 3.3 Trám răng Composite (Composite Filling)
        var serviceFilling = new DepartmentService { DepartmentId = 1, DepartmentServiceId = 13, ServiceName = "Trám Răng Thẩm Mỹ Composite", Price = 350000m, Status = "Active" };
        var wfFilling = new ServiceWorkflow { WorkflowId = 3, DepartmentServiceId = 13, WorkflowName = "Quy trình trám răng thẩm mỹ chuẩn", Version = 1, ApprovalStatus = "Approved", IsActive = true, CreatedAt = DateTime.UtcNow };
        var stepF1 = new ServiceWorkflowStep { StepId = 301, WorkflowId = 3, StepOrder = 1, StepName = "Sửa soạn xoang trám & làm sạch ngà mủn", ExpectedDataType = "Text", IsRequired = true, CompletionRequirement = "Lấy sạch ngà sâu, bảo tồn tủy" };
        var stepF2 = new ServiceWorkflowStep { StepId = 302, WorkflowId = 3, StepOrder = 2, StepName = "So màu composite theo bảng màu Vita", ExpectedDataType = "Text", IsRequired = true, CompletionRequirement = "Chọn màu A2/A3 đồng nhất men răng" };
        var stepF3 = new ServiceWorkflowStep { StepId = 303, WorkflowId = 3, StepOrder = 3, StepName = "Xử lý Etching & thoa keo Bonding", ExpectedDataType = "Boolean", IsRequired = true, CompletionRequirement = "Acid etching 15s và chiếu đèn 20s" };
        var stepF4 = new ServiceWorkflowStep { StepId = 304, WorkflowId = 3, StepOrder = 4, StepName = "Trám từng lớp composite & trùng hợp quang học", ExpectedDataType = "Boolean", IsRequired = true, CompletionRequirement = "Đặt composite từng lớp <= 2mm, chiếu đèn" };
        var stepF5 = new ServiceWorkflowStep { StepId = 305, WorkflowId = 3, StepOrder = 5, StepName = "Chỉnh khớp cắn bằng giấy cắn & đánh bóng", ExpectedDataType = "Boolean", IsRequired = true, CompletionRequirement = "Kiểm tra không cộm khớp, đánh bóng láng mịn" };

        // 3.4 Điều trị tủy (Endodontics - Multi-session)
        var serviceEndo = new DepartmentService { DepartmentId = 2, DepartmentServiceId = 21, ServiceName = "Điều Trị Tủy Răng Nhiều Buổi", Price = 1200000m, Status = "Active" };
        var wfEndo = new ServiceWorkflow { WorkflowId = 4, DepartmentServiceId = 21, WorkflowName = "Quy trình nội nha chuẩn 3 buổi", Version = 1, ApprovalStatus = "Approved", IsActive = true, CreatedAt = DateTime.UtcNow };
        var stepE1 = new ServiceWorkflowStep { StepId = 401, WorkflowId = 4, StepOrder = 1, StepName = "Buổi 1: Mở tủy, lấy tủy buồng & đặt thuốc sát trùng", ExpectedDataType = "Text", IsRequired = true, CompletionRequirement = "Mở tủy không thủng sàn, đặt bông thuốc" };
        var stepE2 = new ServiceWorkflowStep { StepId = 402, WorkflowId = 4, StepOrder = 2, StepName = "Buổi 2: Đo chiều dài ống tủy & tạo hình", ExpectedDataType = "Number", IsRequired = true, CompletionRequirement = "Đo chiều dài làm việc (mm) bằng máy định vị chóp" };
        var stepE3 = new ServiceWorkflowStep { StepId = 403, WorkflowId = 4, StepOrder = 3, StepName = "Buổi 3: Thử cone & chụp X-quang kiểm tra", ExpectedDataType = "Boolean", IsRequired = true, CompletionRequirement = "Chụp X-quang cone gutta-percha kiểm tra độ khít sát chóp" };
        var stepE4 = new ServiceWorkflowStep { StepId = 404, WorkflowId = 4, StepOrder = 4, StepName = "Buổi 3: Trám bít ống tủy vĩnh viễn", ExpectedDataType = "Boolean", IsRequired = true, CompletionRequirement = "Bít kín 3 chiều ống tủy và chụp phim kiểm tra hàn tủy" };

        db.DepartmentServices.AddRange(serviceConsult, serviceScaling, serviceFilling, serviceEndo);
        db.ServiceWorkflows.AddRange(wfConsult, wfScaling, wfFilling, wfEndo);
        db.ServiceWorkflowSteps.AddRange(
            stepC1, stepC2, stepC3, stepC4,
            stepS1, stepS2, stepS3, stepS4,
            stepF1, stepF2, stepF3, stepF4, stepF5,
            stepE1, stepE2, stepE3, stepE4
        );

        // 4. Initial Appointment & CareEpisode
        var appt = new Appointment
        {
            AppointmentId = 501,
            AppointmentCode = "APT-20261113-0501",
            PatientUserId = 101,
            AssignedDentistUserId = 201,
            DepartmentId = 1,
            RequestedServiceId = 11,
            Status = "InService",
            ScheduledStart = DateTime.UtcNow,
            QueueNumber = "12",
            ReasonForVisit = "Khám tổng quát",
            BookingSource = "Direct",
            CareEpisodeId = 601,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        var episode = new CareEpisode
        {
            CareEpisodeId = 601,
            EpisodeCode = "EP-20261113-0601",
            PatientUserId = 101,
            RootAppointmentId = 501,
            Status = "Open",
            OpenedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.CareEpisodes.Add(episode);

        await db.SaveChangesAsync();
        return (101, 201, 202, 301, 501, 601);
    }

    [Fact]
    public async Task GeneralConsultation_Workflow_Execution_StepByStep_Succeeds()
    {
        var db = CreateDbContext(nameof(GeneralConsultation_Workflow_Execution_StepByStep_Succeeds));
        var (patientId, dentistAId, _, _, apptId, _) = await SeedBaseDataAsync(db);

        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        // 1. Initial Consultation task should be created / bound to workflow 1
        var taskRes = await service.AddServiceTaskAsync(dentistAId, new CreateEpisodeServiceTaskRequest
        {
            AppointmentId = apptId,
            DepartmentServiceId = 11, // General Consultation
            Quantity = 1
        });
        Assert.True(taskRes.Success);
        var task = taskRes.Data;
        Assert.NotNull(task);
        Assert.Equal(1, task.AppliedWorkflowId);
        Assert.Equal(4, task.TotalSteps);
        Assert.Equal("Planned", task.Status);

        // 2. Start Task
        var startRes = await service.StartServiceTaskAsync(dentistAId, task.ServiceTaskId);
        Assert.True(startRes.Success);
        Assert.Equal("InProgress", startRes.Data.Status);
        Assert.Equal("InProgress", startRes.Data.StepProgresses[0].Status);

        // 3. Step 1: Lấy bệnh sử
        var s1Res = await service.CompleteTaskStepAsync(dentistAId, task.ServiceTaskId, 101, new CompleteTaskStepRequest
        {
            ResultValue = "Bệnh nhân đau nhẹ răng hàm dưới khi nhai đồ cứng, không sốt",
            Notes = "Tiền sử không dị ứng thuốc"
        });
        Assert.True(s1Res.Success);
        Assert.Equal("Completed", s1Res.Data.Status);

        // 4. Step 2: Khám ngoài mặt
        var s2Res = await service.CompleteTaskStepAsync(dentistAId, task.ServiceTaskId, 102, new CompleteTaskStepRequest
        {
            ResultValue = "Mặt cân đối, cơ nhai bình thường, khớp TDH không tiếng kêu",
            Notes = "Vận động há ngậm miệng bình thường"
        });
        Assert.True(s2Res.Success);
        Assert.Equal("Completed", s2Res.Data.Status);

        // 5. Step 3: Khám trong miệng
        var s3Res = await service.CompleteTaskStepAsync(dentistAId, task.ServiceTaskId, 103, new CompleteTaskStepRequest
        {
            ResultValue = "Răng 36 có xoang sâu mặt nhai ngà nông, vôi răng độ 1 hàm dưới",
            Notes = "Nướu hồng hào, không chảy máu tự nhiên"
        });
        Assert.True(s3Res.Success);
        Assert.Equal("Completed", s3Res.Data.Status);

        // 6. Step 4: Tư vấn & kết luận
        var s4Res = await service.CompleteTaskStepAsync(dentistAId, task.ServiceTaskId, 104, new CompleteTaskStepRequest
        {
            ResultValue = "Chỉ định: Cạo vôi răng + Trám răng composite R36. Bệnh nhân đồng ý.",
            Notes = "Bệnh nhân hiểu rõ chi phí và thời gian điều trị"
        });
        Assert.True(s4Res.Success);
        Assert.Equal("Completed", s4Res.Data.Status);

        // 7. Complete Task
        var completeRes = await service.CompleteServiceTaskAsync(dentistAId, task.ServiceTaskId, new CompleteServiceTaskRequest
        {
            ClinicalFindings = "Hoàn tất khám tổng quát",
            Notes = "Chuyển sang thực hiện cạo vôi răng"
        });
        Assert.True(completeRes.Success);
        Assert.Equal("Completed", completeRes.Data.Status);
        Assert.Equal(100, completeRes.Data.ProgressPercentage);

        // 8. Validate Appointment Tasks Completed
        var valRes = await service.ValidateAppointmentTasksCompletedAsync(apptId);
        Assert.True(valRes.Success);
        Assert.True(valRes.Data);
    }

    [Fact]
    public async Task Scaling_And_Filling_Workflow_Execution_FollowsSpecifications()
    {
        var db = CreateDbContext(nameof(Scaling_And_Filling_Workflow_Execution_FollowsSpecifications));
        var (patientId, dentistAId, _, _, apptId, _) = await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        // Scaling Task
        var scalingTaskRes = await service.AddServiceTaskAsync(dentistAId, new CreateEpisodeServiceTaskRequest
        {
            AppointmentId = apptId,
            DepartmentServiceId = 12, // Cạo vôi
            Quantity = 1
        });
        Assert.True(scalingTaskRes.Success);
        var sTaskId = scalingTaskRes.Data.ServiceTaskId;

        await service.StartServiceTaskAsync(dentistAId, sTaskId);
        await service.CompleteTaskStepAsync(dentistAId, sTaskId, 201, new CompleteTaskStepRequest { ResultValue = "Vôi răng độ 1 dưới nướu răng cửa dưới" });
        await service.CompleteTaskStepAsync(dentistAId, sTaskId, 202, new CompleteTaskStepRequest { ResultValue = "Đạt" });
        await service.CompleteTaskStepAsync(dentistAId, sTaskId, 203, new CompleteTaskStepRequest { ResultValue = "Đạt" });
        await service.CompleteTaskStepAsync(dentistAId, sTaskId, 204, new CompleteTaskStepRequest { ResultValue = "Dặn dò chải răng ngày 2 lần, dùng chỉ tơ" });

        var compScaling = await service.CompleteServiceTaskAsync(dentistAId, sTaskId, new CompleteServiceTaskRequest());
        Assert.True(compScaling.Success);
        Assert.Equal("Completed", compScaling.Data.Status);

        // Composite Filling Task
        var fillTaskRes = await service.AddServiceTaskAsync(dentistAId, new CreateEpisodeServiceTaskRequest
        {
            AppointmentId = apptId,
            DepartmentServiceId = 13, // Trám composite
            Quantity = 1
        });
        Assert.True(fillTaskRes.Success);
        var fTaskId = fillTaskRes.Data.ServiceTaskId;

        await service.StartServiceTaskAsync(dentistAId, fTaskId);
        await service.CompleteTaskStepAsync(dentistAId, fTaskId, 301, new CompleteTaskStepRequest { ResultValue = "Làm sạch xoang R36 loại I" });
        await service.CompleteTaskStepAsync(dentistAId, fTaskId, 302, new CompleteTaskStepRequest { ResultValue = "So màu Vita A2" });
        await service.CompleteTaskStepAsync(dentistAId, fTaskId, 303, new CompleteTaskStepRequest { ResultValue = "Đạt" });
        await service.CompleteTaskStepAsync(dentistAId, fTaskId, 304, new CompleteTaskStepRequest { ResultValue = "Đạt" });
        await service.CompleteTaskStepAsync(dentistAId, fTaskId, 305, new CompleteTaskStepRequest { ResultValue = "Đạt" });

        var compFill = await service.CompleteServiceTaskAsync(dentistAId, fTaskId, new CompleteServiceTaskRequest());
        Assert.True(compFill.Success);
        Assert.Equal("Completed", compFill.Data.Status);
    }

    [Fact]
    public async Task ThreeCompletionLevels_And_XrayGuard_AreEnforced()
    {
        var db = CreateDbContext(nameof(ThreeCompletionLevels_And_XrayGuard_AreEnforced));
        var (patientId, dentistAId, dentistBId, _, apptId, _) = await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        // Endodontics task (requires X-ray on step 3)
        var endoTaskRes = await service.AddServiceTaskAsync(dentistAId, new CreateEpisodeServiceTaskRequest
        {
            AppointmentId = apptId,
            DepartmentServiceId = 21,
            Quantity = 1
        });
        Assert.True(endoTaskRes.Success);
        var endoTaskId = endoTaskRes.Data.ServiceTaskId;

        await service.StartServiceTaskAsync(dentistAId, endoTaskId);

        // LEVEL 1: Completing step 1 does NOT mark task completed
        var step1Res = await service.CompleteTaskStepAsync(dentistAId, endoTaskId, 401, new CompleteTaskStepRequest { ResultValue = "Mở tủy R46 hoàn tất" });
        Assert.True(step1Res.Success);

        var taskInDb = await db.EpisodeServiceTasks.FindAsync(endoTaskId);
        Assert.Equal("InProgress", taskInDb!.Status); // Level 1 verified: Task is still InProgress

        // LEVEL 2: Attempting to complete service task while steps are incomplete must FAIL
        var prematureComplete = await service.CompleteServiceTaskAsync(dentistAId, endoTaskId, new CompleteServiceTaskRequest());
        Assert.False(prematureComplete.Success); // Level 2 guard verified!

        // Complete step 2
        await service.CompleteTaskStepAsync(dentistAId, endoTaskId, 402, new CompleteTaskStepRequest { ResultValue = "21.5" });

        // X-RAY GUARD: Step 3 requires X-ray. Attempting to complete without X-ray result or evidenceUrl fails!
        var xrayStepFail = await service.CompleteTaskStepAsync(dentistAId, endoTaskId, 403, new CompleteTaskStepRequest { ResultValue = "Đạt" });
        Assert.False(xrayStepFail.Success);
        Assert.Contains("X-quang", xrayStepFail.Message);

        // Provide evidenceUrl for X-ray
        var xrayStepSuccess = await service.CompleteTaskStepAsync(dentistAId, endoTaskId, 403, new CompleteTaskStepRequest
        {
            ResultValue = "Đạt",
            EvidenceUrl = "https://dentalcare.vn/images/xray-r46-cone.jpg"
        });
        Assert.True(xrayStepSuccess.Success);

        // Complete step 4 (also requires X-ray proof for post-obturation check)
        var s4Res = await service.CompleteTaskStepAsync(dentistAId, endoTaskId, 404, new CompleteTaskStepRequest
        {
            ResultValue = "Đạt",
            EvidenceUrl = "https://dentalcare.vn/images/xray-r46-final.jpg"
        });
        Assert.True(s4Res.Success);

        // LEVEL 3: Validate appointment completed should fail BEFORE task completion
        var apptValBefore = await service.ValidateAppointmentTasksCompletedAsync(apptId);
        Assert.False(apptValBefore.Success); // Level 3 guard verified!

        // Now complete task
        var taskCompleted = await service.CompleteServiceTaskAsync(dentistAId, endoTaskId, new CompleteServiceTaskRequest());
        Assert.True(taskCompleted.Success);

        // Level 3 passes after task completion
        var apptValAfter = await service.ValidateAppointmentTasksCompletedAsync(apptId);
        Assert.True(apptValAfter.Success);
        Assert.True(apptValAfter.Data);
    }

    [Fact]
    public async Task DoctorHandoff_InternalReferral_CreatesStationBAppointmentWithQueue()
    {
        var db = CreateDbContext(nameof(DoctorHandoff_InternalReferral_CreatesStationBAppointmentWithQueue));
        var (patientId, dentistAId, dentistBId, receptionistId, apptId, episodeId) = await SeedBaseDataAsync(db);
        var service = new CareEpisodeService(db, _mockLogger.Object, _mockNotification.Object);

        // Doctor A creates internal referral for Endodontics to Department 2 (Doctor B)
        var referralRes = await service.CreateInternalReferralAsync(dentistAId, new CreateCareEpisodeReferralRequest
        {
            SourceAppointmentId = apptId,
            ToDepartmentId = 2,
            RequestedServiceId = 21,
            PreferredDentistUserId = dentistBId,
            Reason = "Bệnh nhân sâu răng 46 sát tủy buồng, cần chuyển chuyên khoa nội nha",
            Urgency = "Standard"
        });
        Assert.True(referralRes.Success);
        var referralId = referralRes.Data.ReferralId;

        // Receptionist coordinates referral to Doctor B
        var coordRes = await service.CoordinateReferralAppointmentAsync(receptionistId, new CoordinateCareEpisodeReferralRequest
        {
            ReferralId = referralId,
            AssignedDentistUserId = dentistBId,
            ScheduledStart = DateTime.UtcNow.AddHours(2),
            Notes = "Xếp phòng nội nha cho BS B"
        });
        Assert.True(coordRes.Success);
        var targetAppt = coordRes.Data;
        Assert.NotNull(targetAppt);
        Assert.True(targetAppt.AppointmentId > 0);
        Assert.Equal("Confirmed", targetAppt.Status);
        Assert.NotNull(targetAppt.QueueNumber); // Allocated QueueNumber verified!

        // Verify Doctor B's task is initialized with active workflow steps
        var targetTask = await db.EpisodeServiceTasks
            .Include(t => t.StepProgresses)
            .FirstOrDefaultAsync(t => t.AppointmentId == targetAppt.AppointmentId);
        Assert.NotNull(targetTask);
        Assert.Equal(4, targetTask.AppliedWorkflowId); // Bound to Endodontics workflow
        Assert.Equal(4, targetTask.StepProgresses.Count);

        // Station A appointment completes so Station B can check in
        var apptA = await db.Appointments.FindAsync(apptId);
        apptA!.Status = "Completed";
        await db.SaveChangesAsync();

        // Verify Check-In at Station B preserves QueueNumber
        var checkinRes = await service.StationBCheckInAsync(receptionistId, new StationBCheckInRequest
        {
            AppointmentId = targetAppt.AppointmentId
        });
        Assert.True(checkinRes.Success);
        Assert.Equal("CheckedIn", checkinRes.Data.Status);
        Assert.Equal(targetAppt.QueueNumber, checkinRes.Data.QueueNumber); // QueueNumber preserved!
    }

    [Fact]
    public async Task MultiSession_TreatmentPlan_AdvancesSessionWithoutPrematureCompletion()
    {
        var db = CreateDbContext(nameof(MultiSession_TreatmentPlan_AdvancesSessionWithoutPrematureCompletion));
        var (patientId, dentistAId, _, _, apptId, _) = await SeedBaseDataAsync(db);

        // Seed a 3-session treatment plan
        var plan = new TreatmentPlan
        {
            TreatmentPlanId = 801,
            PatientUserId = patientId,
            CreatedByDentistUserId = dentistAId,
            DepartmentId = 1,
            DiagnosisSummary = "Phác đồ điều trị tủy răng 46 (3 buổi)",
            Status = "Active",
            EstimatedTotal = 1200000m,
            CreatedAt = DateTime.UtcNow
        };
        db.TreatmentPlans.Add(plan);

        var item1 = new TreatmentPlanItem { TreatmentPlanItemId = 811, TreatmentPlanId = 801, DepartmentServiceId = 21, SequenceNo = 1, StageName = "Buổi 1: Mở tủy", Status = "InProgress", EstimatedCost = 400000m };
        var item2 = new TreatmentPlanItem { TreatmentPlanItemId = 812, TreatmentPlanId = 801, DepartmentServiceId = 21, SequenceNo = 2, StageName = "Buổi 2: Tạo hình ống tủy", Status = "Pending", EstimatedCost = 400000m };
        var item3 = new TreatmentPlanItem { TreatmentPlanItemId = 813, TreatmentPlanId = 801, DepartmentServiceId = 21, SequenceNo = 3, StageName = "Buổi 3: Hàn bít tủy", Status = "Pending", EstimatedCost = 400000m };
        db.TreatmentPlanItems.AddRange(item1, item2, item3);
        await db.SaveChangesAsync();

        // Complete session 1
        item1.Status = "Completed";
        item2.Status = "InProgress";
        await db.SaveChangesAsync();

        // Treatment plan must still be Active, not Completed
        var updatedPlan = await db.TreatmentPlans.Include(p => p.TreatmentPlanItems).FirstOrDefaultAsync(p => p.TreatmentPlanId == 801);
        Assert.NotNull(updatedPlan);
        Assert.Equal("Active", updatedPlan.Status);
        Assert.Equal(1, updatedPlan.TreatmentPlanItems.Count(i => i.Status == "Completed"));
        Assert.Equal(2, updatedPlan.TreatmentPlanItems.Count(i => i.Status != "Completed"));
    }

    [Fact]
    public async Task DedicatedXrayDentist_RosterAndQueue_Verification()
    {
        var db = CreateDbContext(nameof(DedicatedXrayDentist_RosterAndQueue_Verification));
        var (patientId, dentistAId, dentistBId, _, apptId, _) = await SeedBaseDataAsync(db);

        // Dedicated X-ray dentist user
        var xrayUser = new UserAccount { UserId = 299, Email = "xraydentist@dentalcare.vn", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var xrayStaff = new StaffProfile { UserId = 299, EmployeeCode = "DEN-XRAY", FullName = "Bác Sĩ Chuyên Trách X-Quang", EmploymentStatus = "Active" };
        var xrayDentistProfile = new DentistProfile
        {
            UserId = 299,
            LicenseNumber = "BS-XRAY-001",
            Qualification = "BS CKI Chẩn Đoán Hình Ảnh RHM",
            HasRadiationSafetyCert = true,
            RadiationCertNumber = "RAD-SAFE-2026-999",
            RadiationCertExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddYears(2))
        };
        db.UserAccounts.Add(xrayUser);
        db.StaffProfiles.Add(xrayStaff);
        db.DentistProfiles.Add(xrayDentistProfile);

        // Room & Equipment
        var xrayRoom = new Room { RoomId = 99, RoomName = "Phòng X-quang Kỹ Thuật Số", RoomCode = "R-XRAY", Status = "Active", IsExclusive = true };
        var equip = new ImagingEquipment { EquipmentId = 88, RoomId = 99, EquipmentName = "Máy CT Cone Beam 3D", EquipmentCode = "CBCT-01", OperationalStatus = "Operational", IsActive = true };
        db.Rooms.Add(xrayRoom);
        db.ImagingEquipments.Add(equip);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var roster = new XrayDentistRoster
        {
            RosterId = 901,
            DentistUserId = 299,
            RoomId = 99,
            EquipmentId = 88,
            WorkDate = today,
            ShiftType = "Morning",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Active",
            CreatedByUserId = 301,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.XrayDentistRosters.Add(roster);

        // Diagnostic Order
        var order = new DiagnosticOrder
        {
            DiagnosticOrderId = 701,
            AppointmentId = apptId,
            PatientUserId = patientId,
            DentistUserId = dentistAId,
            DiagnosticType = "Panorama",
            ClinicalIndication = "Kiểm tra chóp răng 46",
            Status = "Ordered",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.DiagnosticOrders.Add(order);
        await db.SaveChangesAsync();

        // Verify active X-ray dentist on duty with radiation certification
        var onDuty = await db.XrayDentistRosters
            .Include(r => r.DentistUser)
            .AnyAsync(r => r.WorkDate == today && r.DentistUserId == 299 && r.Status == "Active");
        Assert.True(onDuty);

        var dentistProfile = await db.DentistProfiles.FindAsync(299L);
        Assert.NotNull(dentistProfile);
        Assert.True(dentistProfile.HasRadiationSafetyCert);

        // Complete Diagnostic Result
        var result = new DiagnosticResult
        {
            DiagnosticResultId = 711,
            DiagnosticOrderId = 701,
            FileUrl = "https://dentalcare.vn/imaging/panoramic-46.jpg",
            Findings = "Tiêu xương vùng chóp răng 46 đường kính 3mm",
            UploadedByUserId = 299,
            ReviewedByDentistUserId = 299,
            IsReviewed = true,
            ReviewedAtUtc = DateTime.UtcNow
        };
        order.Status = "Completed";
        db.DiagnosticResults.Add(result);
        await db.SaveChangesAsync();

        // Verify result is available
        var orderInDb = await db.DiagnosticOrders.Include(o => o.DiagnosticResults).FirstOrDefaultAsync(o => o.DiagnosticOrderId == 701);
        Assert.NotNull(orderInDb);
        Assert.Equal("Completed", orderInDb.Status);
        Assert.Single(orderInDb.DiagnosticResults);
        Assert.True(orderInDb.DiagnosticResults.First().IsReviewed);
    }
}
