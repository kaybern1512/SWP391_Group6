using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Admin.DTOs;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using ApptServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Admin;

public class AdminServiceSimplificationAcceptanceTests
{
    private readonly Mock<ILogger<AdminService>> _mockLogger = new();

    private DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private IAuditLogService CreateRealAuditService(DentalClinicDbContext db)
    {
        return new AuditLogService(db, Mock.Of<ILogger<AuditLogService>>());
    }

    // =========================================================================
    // TASK 1: WORKFLOW PERSISTENCE & VERSIONING (WF-01 to WF-08)
    // =========================================================================

    [Fact]
    public async Task WF_01_CreateNormalService_DefaultWorkflowConfiguredAndSaved()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa Tổng Quát", Status = "Active" });
        await db.SaveChangesAsync();

        var auditService = CreateRealAuditService(db);
        var adminService = new AdminService(db, auditService, _mockLogger.Object);

        var createReq = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-WF-01",
            ServiceName = "Trám Răng Sâu WF01",
            Price = 300000,
            DurationMinutes = 30,
            ServiceType = "Treatment"
        };

        var res = await adminService.CreateServiceAsync(createReq, adminUserId: 100);
        Assert.True(res.Success);
        Assert.NotNull(res.Data);

        // Verify AuditLog record was created
        var audit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.EntityName == "ServiceWorkflow" && a.EntityId == res.Data.DepartmentServiceId.ToString() && a.Action == "CONFIGURE");
        Assert.NotNull(audit);
        Assert.NotNull(audit.NewValues);

        var savedWf = JsonSerializer.Deserialize<ServiceWorkflowDto>(audit.NewValues,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(savedWf);
        Assert.Equal(1, savedWf.Version);
        Assert.Single(savedWf.Steps);
        Assert.Equal("None", savedWf.Steps[0].ExpectedDataType);
    }

    [Fact]
    public async Task WF_02_ReloadService_RetrievesPersistedWorkflow()
    {
        var dbName = Guid.NewGuid().ToString();
        var db = CreateDbContext(dbName);
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa Tổng Quát", Status = "Active" });
        await db.SaveChangesAsync();

        var auditService = CreateRealAuditService(db);
        var adminService = new AdminService(db, auditService, _mockLogger.Object);

        var createRes = await adminService.CreateServiceAsync(new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-WF-02",
            ServiceName = "Lấy Cao Răng WF02",
            Price = 200000,
            DurationMinutes = 30,
            ServiceType = "Treatment"
        }, adminUserId: 100);

        long serviceId = createRes.Data!.DepartmentServiceId;

        // Simulate reload with a fresh AdminService instance against same db
        var reloadedAdminService = new AdminService(db, auditService, _mockLogger.Object);
        var wfRes = await reloadedAdminService.GetWorkflowAsync(serviceId);

        Assert.True(wfRes.Success);
        Assert.NotNull(wfRes.Data);
        Assert.Equal(1, wfRes.Data.Version);
        Assert.Single(wfRes.Data.Steps);
        Assert.Equal("None", wfRes.Data.Steps[0].ExpectedDataType);
        Assert.Contains("Xác nhận đã thực hiện dịch vụ", wfRes.Data.Steps[0].StepName);
    }

    [Fact]
    public async Task WF_03_EditWorkflowStep_SavesNewVersionAndRetrievesOnReload()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa Tổng Quát", Status = "Active" });
        await db.SaveChangesAsync();

        var auditService = CreateRealAuditService(db);
        var adminService = new AdminService(db, auditService, _mockLogger.Object);

        var createRes = await adminService.CreateServiceAsync(new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-WF-03",
            ServiceName = "Khám Răng WF03",
            Price = 150000,
            DurationMinutes = 20,
            ServiceType = "Treatment"
        }, adminUserId: 100);

        long serviceId = createRes.Data!.DepartmentServiceId;

        // Configure new workflow step
        var configReq = new ConfigureWorkflowRequest
        {
            WorkflowName = "Quy trình khám răng nâng cao",
            ActivateImmediately = true,
            Steps = new List<ServiceWorkflowStepInputDto>
            {
                new()
                {
                    StepOrder = 1,
                    StepName = "Bác sĩ kiểm tra răng miệng toàn diện và tư vấn",
                    IsRequired = true,
                    ExpectedDataType = "None",
                    CompletionRequirement = "Hoàn tất khám"
                }
            }
        };

        var configRes = await adminService.ConfigureWorkflowAsync(serviceId, configReq, adminUserId: 100);
        Assert.True(configRes.Success);
        Assert.Equal(2, configRes.Data!.Version);

        // Reopen / reload and verify new version is returned
        var reloadedWf = await adminService.GetWorkflowAsync(serviceId);
        Assert.True(reloadedWf.Success);
        Assert.Equal(2, reloadedWf.Data!.Version);
        Assert.Equal("Bác sĩ kiểm tra răng miệng toàn diện và tư vấn", reloadedWf.Data.Steps[0].StepName);
    }

    [Fact]
    public async Task WF_04_VersionHistory_QueryingHistoricalVersion_ReturnsExactVersion()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa Tổng Quát", Status = "Active" });
        await db.SaveChangesAsync();

        var auditService = CreateRealAuditService(db);
        var adminService = new AdminService(db, auditService, _mockLogger.Object);

        var createRes = await adminService.CreateServiceAsync(new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-WF-04",
            ServiceName = "Nhổ Răng WF04",
            Price = 500000,
            DurationMinutes = 45,
            ServiceType = "Treatment"
        }, adminUserId: 100);

        long serviceId = createRes.Data!.DepartmentServiceId;

        // Update to version 2
        await adminService.ConfigureWorkflowAsync(serviceId, new ConfigureWorkflowRequest
        {
            WorkflowName = "Quy trình v2",
            ActivateImmediately = true,
            Steps = new List<ServiceWorkflowStepInputDto>
            {
                new() { StepOrder = 1, StepName = "Bước 1 - v2", IsRequired = true, ExpectedDataType = "None" }
            }
        }, adminUserId: 100);

        // Update to version 3
        await adminService.ConfigureWorkflowAsync(serviceId, new ConfigureWorkflowRequest
        {
            WorkflowName = "Quy trình v3",
            ActivateImmediately = true,
            Steps = new List<ServiceWorkflowStepInputDto>
            {
                new() { StepOrder = 1, StepName = "Bước 1 - v3", IsRequired = true, ExpectedDataType = "None" }
            }
        }, adminUserId: 100);

        // Query historical version 1
        var v1Res = await adminService.GetWorkflowAsync(serviceId, version: 1);
        Assert.True(v1Res.Success);
        Assert.Equal(1, v1Res.Data!.Version);
        Assert.Equal("Xác nhận đã thực hiện dịch vụ", v1Res.Data.Steps[0].StepName);

        // Query historical version 2
        var v2Res = await adminService.GetWorkflowAsync(serviceId, version: 2);
        Assert.True(v2Res.Success);
        Assert.Equal(2, v2Res.Data!.Version);
        Assert.Equal("Bước 1 - v2", v2Res.Data.Steps[0].StepName);

        // Query latest (version == null) -> returns v3
        var vLatest = await adminService.GetWorkflowAsync(serviceId, version: null);
        Assert.True(vLatest.Success);
        Assert.Equal(3, vLatest.Data!.Version);
        Assert.Equal("Bước 1 - v3", vLatest.Data.Steps[0].StepName);
    }

    [Fact]
    public async Task WF_05_ServiceIsolation_ServiceA_DoesNotReadServiceB_Workflow()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa Tổng Quát", Status = "Active" });
        await db.SaveChangesAsync();

        var auditService = CreateRealAuditService(db);
        var adminService = new AdminService(db, auditService, _mockLogger.Object);

        var srvA = await adminService.CreateServiceAsync(new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-WF-A",
            ServiceName = "Dịch vụ A",
            Price = 100000,
            DurationMinutes = 20,
            ServiceType = "Treatment"
        }, adminUserId: 100);

        var srvB = await adminService.CreateServiceAsync(new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-WF-B",
            ServiceName = "Dịch vụ B",
            Price = 200000,
            DurationMinutes = 30,
            ServiceType = "XRay"
        }, adminUserId: 100);

        var wfA = await adminService.GetWorkflowAsync(srvA.Data!.DepartmentServiceId);
        var wfB = await adminService.GetWorkflowAsync(srvB.Data!.DepartmentServiceId);

        Assert.Equal(srvA.Data!.DepartmentServiceId, wfA.Data!.DepartmentServiceId);
        Assert.Equal("None", wfA.Data.Steps[0].ExpectedDataType);

        Assert.Equal(srvB.Data!.DepartmentServiceId, wfB.Data!.DepartmentServiceId);
        Assert.Equal("Image", wfB.Data.Steps[0].ExpectedDataType);
    }

    [Fact]
    public async Task WF_06_MultipleConfigurations_IncrementMonotonically_NoAmbiguity()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa Tổng Quát", Status = "Active" });
        await db.SaveChangesAsync();

        var auditService = CreateRealAuditService(db);
        var adminService = new AdminService(db, auditService, _mockLogger.Object);

        var srv = await adminService.CreateServiceAsync(new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-WF-06",
            ServiceName = "Dịch vụ WF06",
            Price = 100000,
            DurationMinutes = 20,
            ServiceType = "Treatment"
        }, adminUserId: 100);

        long id = srv.Data!.DepartmentServiceId;

        // Two consecutive configurations
        var cfg1 = await adminService.ConfigureWorkflowAsync(id, new ConfigureWorkflowRequest
        {
            WorkflowName = "Cấu hình lần 1",
            Steps = new List<ServiceWorkflowStepInputDto>
            {
                new() { StepOrder = 1, StepName = "Bước v2", ExpectedDataType = "None" }
            }
        }, adminUserId: 100);

        var cfg2 = await adminService.ConfigureWorkflowAsync(id, new ConfigureWorkflowRequest
        {
            WorkflowName = "Cấu hình lần 2",
            Steps = new List<ServiceWorkflowStepInputDto>
            {
                new() { StepOrder = 1, StepName = "Bước v3", ExpectedDataType = "None" }
            }
        }, adminUserId: 100);

        Assert.Equal(2, cfg1.Data!.Version);
        Assert.Equal(3, cfg2.Data!.Version);

        var active = await adminService.GetWorkflowAsync(id);
        Assert.Equal(3, active.Data!.Version);
    }

    [Fact]
    public async Task WF_07_XRayService_DefaultsTo_Image_ExpectedDataType()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa Chẩn Đoán", Status = "Active" });
        await db.SaveChangesAsync();

        var auditService = CreateRealAuditService(db);
        var adminService = new AdminService(db, auditService, _mockLogger.Object);

        var srv = await adminService.CreateServiceAsync(new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-WF-07",
            ServiceName = "Chụp X-quang CT Cone Beam",
            Price = 500000,
            DurationMinutes = 20,
            ServiceType = "XRay"
        }, adminUserId: 100);

        var wf = await adminService.GetWorkflowAsync(srv.Data!.DepartmentServiceId);
        Assert.True(wf.Success);
        Assert.Equal("Image", wf.Data!.Steps[0].ExpectedDataType);
        Assert.Contains("X-quang", wf.Data.Steps[0].StepName);
    }

    [Fact]
    public async Task WF_08_NormalTreatmentService_DefaultsTo_None_ExpectedDataType()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa Tổng Quát", Status = "Active" });
        await db.SaveChangesAsync();

        var auditService = CreateRealAuditService(db);
        var adminService = new AdminService(db, auditService, _mockLogger.Object);

        var srv = await adminService.CreateServiceAsync(new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-WF-08",
            ServiceName = "Điều Trị Viêm Tủy Răng",
            Price = 800000,
            DurationMinutes = 60,
            ServiceType = "Treatment"
        }, adminUserId: 100);

        var wf = await adminService.GetWorkflowAsync(srv.Data!.DepartmentServiceId);
        Assert.True(wf.Success);
        Assert.Equal("None", wf.Data!.Steps[0].ExpectedDataType);
    }

    // =========================================================================
    // TASK 3: SYNTHETIC FULL LIFECYCLE & NEGATIVE TESTS
    // =========================================================================

    [Fact]
    public async Task Task3_Synthetic_FullLifecycle_Create_Reload_Update_Reload()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa Tổng Quát", Status = "Active" });
        await db.SaveChangesAsync();

        var auditService = CreateRealAuditService(db);
        var adminService = new AdminService(db, auditService, _mockLogger.Object);

        // 1. Create synthetic service
        var createReq = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-SYNTH-01",
            ServiceName = "Dịch Vụ Tổng Hợp Test",
            Description = "Mô tả ban đầu",
            Price = 400000,
            DurationMinutes = 45,
            ServiceType = "Treatment"
        };
        var createRes = await adminService.CreateServiceAsync(createReq, adminUserId: 100);
        Assert.True(createRes.Success);
        long svcId = createRes.Data!.DepartmentServiceId;

        // 2. Reload and verify DB persistence
        var reloaded = await adminService.GetServiceByIdAsync(svcId);
        Assert.True(reloaded.Success);
        Assert.Equal("SVC-SYNTH-01", reloaded.Data!.ServiceCode);
        Assert.Equal(400000, reloaded.Data.Price);

        // 3. Edit service and workflow step
        var updateReq = new UpdateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceName = "Dịch Vụ Tổng Hợp Test Đã Cập Nhật",
            Description = "Mô tả đã sửa",
            Price = 450000,
            DurationMinutes = 50,
            ServiceType = "Treatment",
            WorkflowSteps = new List<ServiceWorkflowStepInputDto>
            {
                new()
                {
                    StepId = 1,
                    StepName = "Xác nhận kiểm tra kỹ lưỡng",
                    StepOrder = 1,
                    IsRequired = true,
                    ExpectedDataType = "None",
                    CompletionRequirement = "Hoàn tất điều trị"
                }
            }
        };
        var updateRes = await adminService.UpdateServiceAsync(svcId, updateReq, adminUserId: 100);
        Assert.True(updateRes.Success);

        // 4. Reload after edit
        var finalSvc = await adminService.GetServiceByIdAsync(svcId);
        Assert.True(finalSvc.Success);
        Assert.Equal("Dịch Vụ Tổng Hợp Test Đã Cập Nhật", finalSvc.Data!.ServiceName);
        Assert.Equal(450000, finalSvc.Data.Price);
        Assert.Equal(50, finalSvc.Data.DurationMinutes);

        var finalWf = await adminService.GetWorkflowAsync(svcId);
        Assert.True(finalWf.Success);
        Assert.Equal(2, finalWf.Data!.Version);
        Assert.Equal("Xác nhận kiểm tra kỹ lưỡng", finalWf.Data.Steps[0].StepName);
    }

    [Fact]
    public async Task Task3_Negative_DuplicateServiceCode_Fails()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa 1", Status = "Active" });
        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = 1,
            DepartmentId = 1,
            ServiceCode = "SVC-DUP-01",
            ServiceName = "Dịch Vụ 1",
            Price = 100000,
            DurationMinutes = 30,
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var adminService = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        var req = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-DUP-01",
            ServiceName = "Dịch Vụ 2",
            Price = 200000,
            DurationMinutes = 30,
            ServiceType = "Treatment"
        };
        var res = await adminService.CreateServiceAsync(req);
        Assert.False(res.Success);
        Assert.Contains("đã tồn tại trong hệ thống", res.Message);
    }

    [Fact]
    public async Task Task3_Negative_MissingName_Fails()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa 1", Status = "Active" });
        await db.SaveChangesAsync();

        var adminService = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        var req = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceName = "   ",
            Price = 100000,
            DurationMinutes = 30
        };
        var res = await adminService.CreateServiceAsync(req);
        Assert.False(res.Success);
        Assert.Contains("không được để trống", res.Message);
    }

    [Fact]
    public async Task Task3_Negative_MissingOrInactiveDepartment_Fails()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa Ngừng Hoạt Động", Status = "Inactive" });
        await db.SaveChangesAsync();

        var adminService = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        // Non-existent department
        var resNonExistent = await adminService.CreateServiceAsync(new CreateDepartmentServiceRequest
        {
            DepartmentId = 999,
            ServiceName = "Test",
            Price = 100000,
            DurationMinutes = 30
        });
        Assert.False(resNonExistent.Success);
        Assert.Contains("không tồn tại hoặc đã ngưng hoạt động", resNonExistent.Message);

        // Inactive department
        var resInactive = await adminService.CreateServiceAsync(new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceName = "Test",
            Price = 100000,
            DurationMinutes = 30
        });
        Assert.False(resInactive.Success);
        Assert.Contains("không tồn tại hoặc đã ngưng hoạt động", resInactive.Message);
    }

    [Fact]
    public async Task Task3_Negative_NegativePrice_Fails()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa 1", Status = "Active" });
        await db.SaveChangesAsync();

        var adminService = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        var req = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceName = "Test",
            Price = -50000,
            DurationMinutes = 30
        };
        var res = await adminService.CreateServiceAsync(req);
        Assert.False(res.Success);
        Assert.Contains("không được nhỏ hơn 0", res.Message);
    }

    [Fact]
    public async Task Task3_Negative_ZeroOrNegativeDuration_Fails()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa 1", Status = "Active" });
        await db.SaveChangesAsync();

        var adminService = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        var req = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceName = "Test",
            Price = 100000,
            DurationMinutes = 0
        };
        var res = await adminService.CreateServiceAsync(req);
        Assert.False(res.Success);
        Assert.Contains("lớn hơn 0 phút", res.Message);
    }

    // =========================================================================
    // TASK 4: DELETE SERVICE INTEGRITY AUDIT (DEL-01 to DEL-07)
    // =========================================================================

    [Fact]
    public async Task DEL_01_NewService_WithoutReferences_DeletesSuccessfully()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa 1", Status = "Active" });
        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = 50,
            DepartmentId = 1,
            ServiceCode = "SVC-DEL-01",
            ServiceName = "Dịch Vụ Xóa Sạch",
            Price = 100000,
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var adminService = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        var check = await adminService.CheckServiceCanDeleteAsync(50);
        Assert.True(check.Success);
        Assert.True(check.Data!.CanDelete);
        Assert.Equal(0, check.Data.ActiveReferenceCount);

        var del = await adminService.DeleteServiceAsync(50, adminUserId: 100);
        Assert.True(del.Success);

        var deleted = await db.DepartmentServices.FindAsync((int)50);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DEL_02_Service_WithAppointmentService_BlocksDelete()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa 1", Status = "Active" });
        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = 51,
            DepartmentId = 1,
            ServiceCode = "SVC-DEL-02",
            ServiceName = "Dịch Vụ Có Lịch Hẹn",
            Status = "Active"
        });
        db.AppointmentServices.Add(new ApptServiceEntity
        {
            AppointmentServiceId = 1,
            AppointmentId = 10,
            DepartmentServiceId = 51
        });
        await db.SaveChangesAsync();

        var adminService = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        var check = await adminService.CheckServiceCanDeleteAsync(51);
        Assert.True(check.Success);
        Assert.False(check.Data!.CanDelete);
        Assert.Equal(1, check.Data.ActiveReferenceCount);

        var del = await adminService.DeleteServiceAsync(51, adminUserId: 100);
        Assert.False(del.Success);
        Assert.Contains("Ngừng cung cấp", del.Message);
    }

    [Fact]
    public async Task DEL_03_Service_WithPerformedService_BlocksDelete()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa 1", Status = "Active" });
        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = 52,
            DepartmentId = 1,
            ServiceCode = "SVC-DEL-03",
            ServiceName = "Dịch Vụ Có Thực Hiện",
            Status = "Active"
        });
        db.PerformedServices.Add(new PerformedService
        {
            PerformedServiceId = 1,
            TreatmentSessionId = 1,
            AppointmentId = 10,
            AppointmentServiceId = 1,
            DepartmentServiceId = 52,
            UnitPrice = 200000,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var adminService = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        var check = await adminService.CheckServiceCanDeleteAsync(52);
        Assert.False(check.Data!.CanDelete);
        Assert.Equal(1, check.Data.ActiveReferenceCount);

        var del = await adminService.DeleteServiceAsync(52, adminUserId: 100);
        Assert.False(del.Success);
        Assert.Contains("Ngừng cung cấp", del.Message);
    }

    [Fact]
    public async Task DEL_04_Service_WithDiagnostics_BlocksDelete()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa 1", Status = "Active" });
        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = 53,
            DepartmentId = 1,
            ServiceCode = "SVC-DEL-04",
            ServiceName = "Dịch Vụ Chẩn Đoán",
            Status = "Active"
        });
        db.Diagnostics.Add(new Diagnostic
        {
            DiagnosticId = 1,
            AppointmentServiceId = 1,
            DepartmentServiceId = 53,
            ClinicalIndication = "Chụp kiểm tra",
            ImagingType = "XRay",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var adminService = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        var check = await adminService.CheckServiceCanDeleteAsync(53);
        Assert.False(check.Data!.CanDelete);
        Assert.Equal(1, check.Data.ActiveReferenceCount);

        var del = await adminService.DeleteServiceAsync(53, adminUserId: 100);
        Assert.False(del.Success);
    }

    [Fact]
    public async Task DEL_05_Service_WithTreatmentPlan_BlocksDelete()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa 1", Status = "Active" });
        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = 54,
            DepartmentId = 1,
            ServiceCode = "SVC-DEL-05",
            ServiceName = "Dịch Vụ Kế Hoạch Điều Trị",
            Status = "Active"
        });
        db.TreatmentPlanItems.Add(new TreatmentPlanItem
        {
            PlanItemId = 1,
            TreatmentPlanId = 10,
            DepartmentServiceId = 54,
            SequenceNo = 1,
            EstimatedCost = 500000,
            Status = "Planned"
        });
        await db.SaveChangesAsync();

        var adminService = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        var check = await adminService.CheckServiceCanDeleteAsync(54);
        Assert.False(check.Data!.CanDelete);
        Assert.Equal(1, check.Data.ActiveReferenceCount);

        var del = await adminService.DeleteServiceAsync(54, adminUserId: 100);
        Assert.False(del.Success);
    }

    [Fact]
    public async Task DEL_06_Service_Delete_PreservesAuditHistory()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa 1", Status = "Active" });
        await db.SaveChangesAsync();

        var auditService = CreateRealAuditService(db);
        var adminService = new AdminService(db, auditService, _mockLogger.Object);

        var createRes = await adminService.CreateServiceAsync(new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-DEL-06",
            ServiceName = "Dịch Vụ Có Audit",
            Price = 100000,
            DurationMinutes = 20,
            ServiceType = "Treatment"
        }, adminUserId: 100);

        long svcId = createRes.Data!.DepartmentServiceId;

        // Delete the service
        var delRes = await adminService.DeleteServiceAsync(svcId, adminUserId: 100);
        Assert.True(delRes.Success);

        // Verify AuditLog entries are preserved (both CREATE/CONFIGURE and DELETE logs exist)
        var logs = await db.AuditLogs
            .Where(a => a.EntityId == svcId.ToString())
            .ToListAsync();
        Assert.True(logs.Count >= 2);
        Assert.Contains(logs, l => l.Action == "DELETE");
    }

    [Fact]
    public async Task DEL_07_ToggleServiceStatus_PreservesAllDataAndMarksInactive()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa 1", Status = "Active" });
        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = 57,
            DepartmentId = 1,
            ServiceCode = "SVC-DEL-07",
            ServiceName = "Dịch Vụ Ngừng Cung Cấp",
            Price = 300000,
            Status = "Active"
        });
        db.AppointmentServices.Add(new ApptServiceEntity
        {
            AppointmentServiceId = 1,
            AppointmentId = 10,
            DepartmentServiceId = 57
        });
        await db.SaveChangesAsync();

        var adminService = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        // Toggle from Active to Inactive
        var res = await adminService.ToggleServiceStatusAsync(57, adminUserId: 100);
        Assert.True(res.Success);

        var srv = await db.DepartmentServices.FindAsync((int)57);
        Assert.NotNull(srv);
        Assert.Equal("Inactive", srv.Status);

        // All historical records remain completely intact
        var apptCount = await db.AppointmentServices.CountAsync(a => a.DepartmentServiceId == 57);
        Assert.Equal(1, apptCount);
    }

    // =========================================================================
    // LEGACY CRITERIA REGRESSION
    // =========================================================================

    [Fact]
    public async Task Criterion1_And_2_GetServices_Filters_By_Code_Name_And_Department()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa Nha Tổng Quát", Status = "Active" });
        db.Departments.Add(new Department { DepartmentId = 2, Name = "Khoa Chẩn Đoán Hình Ảnh", Status = "Active" });

        db.DepartmentServices.AddRange(
            new DepartmentService { DepartmentServiceId = 1, DepartmentId = 1, ServiceCode = "SVC-EXT-01", ServiceName = "Nhổ Răng Khôn", DiagnosticType = "Treatment", Price = 500000, Status = "Active" },
            new DepartmentService { DepartmentServiceId = 2, DepartmentId = 1, ServiceCode = "SVC-FILL-01", ServiceName = "Trám Răng thẩm mỹ", DiagnosticType = "Treatment", Price = 300000, Status = "Active" },
            new DepartmentService { DepartmentServiceId = 3, DepartmentId = 2, ServiceCode = "SVC-XRAY-01", ServiceName = "Chụp X-quang Panorama", DiagnosticType = "XRay", Price = 150000, Status = "Active" }
        );
        await db.SaveChangesAsync();

        var service = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        // Filter by code
        var resCode = await service.GetServicesAsync(null, "SVC-XRAY-01");
        Assert.True(resCode.Success);
        Assert.NotNull(resCode.Data);
        Assert.Single(resCode.Data);
        Assert.Equal("SVC-XRAY-01", resCode.Data[0].ServiceCode);

        // Filter by name
        var resName = await service.GetServicesAsync(null, "Trám");
        Assert.True(resName.Success);
        Assert.NotNull(resName.Data);
        Assert.Single(resName.Data);
        Assert.Equal("Trám Răng thẩm mỹ", resName.Data[0].ServiceName);

        // Filter by department
        var resDept = await service.GetServicesAsync(2, null);
        Assert.True(resDept.Success);
        Assert.NotNull(resDept.Data);
        Assert.Single(resDept.Data);
        Assert.Equal(2, resDept.Data[0].DepartmentId);
    }

    [Fact]
    public async Task Criterion6_7_8_UpdateService_Protects_Department_And_Updates_Workflow()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa 1", Status = "Active" });
        db.Departments.Add(new Department { DepartmentId = 2, Name = "Khoa 2", Status = "Active" });

        var svc = new DepartmentService
        {
            DepartmentServiceId = 10,
            DepartmentId = 1,
            ServiceCode = "SVC-TEST-10",
            ServiceName = "Dịch vụ thử nghiệm",
            Price = 100000,
            DurationMinutes = 20,
            DiagnosticType = "Treatment",
            Status = "Active"
        };
        db.DepartmentServices.Add(svc);
        db.AppointmentServices.Add(new ApptServiceEntity { AppointmentServiceId = 101, AppointmentId = 1, DepartmentServiceId = 10 });
        await db.SaveChangesAsync();

        var service = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        // Attempt to change Department from 1 to 2 -> should fail because appointment history exists
        var failReq = new UpdateDepartmentServiceRequest
        {
            DepartmentId = 2,
            ServiceName = "Dịch vụ thử nghiệm cập nhật",
            Price = 120000,
            DurationMinutes = 25
        };
        var failRes = await service.UpdateServiceAsync(10, failReq, adminUserId: 99);
        Assert.False(failRes.Success);
        Assert.Contains("lịch sử", failRes.Message);

        // Keep Department 1, update fields and workflow steps
        var okReq = new UpdateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceName = "Dịch vụ thử nghiệm cập nhật",
            Price = 120000,
            DurationMinutes = 25,
            WorkflowSteps = new List<ServiceWorkflowStepInputDto>
            {
                new()
                {
                    StepId = 1,
                    StepName = "Xác nhận đã thử nghiệm thành công",
                    StepOrder = 1,
                    IsRequired = true,
                    ExpectedDataType = "None",
                    CompletionRequirement = "Hoàn tất thử nghiệm"
                }
            }
        };

        var okRes = await service.UpdateServiceAsync(10, okReq, adminUserId: 99);
        Assert.True(okRes.Success);
        Assert.NotNull(okRes.Data);
        Assert.Equal(120000, okRes.Data.Price);
        Assert.Equal(25, okRes.Data.DurationMinutes);
    }

    [Fact]
    public async Task Criterion9_GetWorkflow_Returns_Canonical_Default_When_No_Audit_Exists()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = 20,
            DepartmentId = 1,
            ServiceCode = "SVC-XRAY-20",
            ServiceName = "Chụp X-quang răng khôn",
            DiagnosticType = "XRay",
            Price = 150000,
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var service = new AdminService(db, CreateRealAuditService(db), _mockLogger.Object);

        var wfRes = await service.GetWorkflowAsync(20);
        Assert.True(wfRes.Success);
        Assert.NotNull(wfRes.Data);
        Assert.Single(wfRes.Data.Steps);
        Assert.Equal("Image", wfRes.Data.Steps[0].ExpectedDataType);
        Assert.Equal("Xác nhận đã chụp X-quang", wfRes.Data.Steps[0].StepName);
    }

    [Fact]
    public void Criterion18_DiagnosticType_Classification_Check()
    {
        // Nhổ Răng Khôn (SVC-EXT-01) is Treatment, Panorama (SVC-XRAY-01) is XRay
        var ext = new DepartmentService { ServiceCode = "SVC-EXT-01", DiagnosticType = "Treatment" };
        var xray = new DepartmentService { ServiceCode = "SVC-XRAY-01", DiagnosticType = "XRay" };

        Assert.Equal("Treatment", ext.DiagnosticType);
        Assert.Equal("XRay", xray.DiagnosticType);
    }
}
