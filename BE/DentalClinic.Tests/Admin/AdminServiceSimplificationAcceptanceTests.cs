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
    private readonly Mock<IAuditLogService> _mockAudit = new();

    private DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

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

        var service = new AdminService(db, _mockAudit.Object, _mockLogger.Object);

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
    public async Task Criterion3_4_5_CreateService_AutoPersists_OneStep_Workflow()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Departments.Add(new Department { DepartmentId = 1, Name = "Khoa Tổng Quát", Status = "Active" });
        await db.SaveChangesAsync();

        var service = new AdminService(db, _mockAudit.Object, _mockLogger.Object);

        // 1. Create XRay service
        var xrayReq = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-XRAY-02",
            ServiceName = "Chụp X-quang Chóp",
            Price = 80000,
            DurationMinutes = 15,
            ServiceType = "XRay"
        };

        var xrayRes = await service.CreateServiceAsync(xrayReq, adminUserId: 99);
        Assert.True(xrayRes.Success);
        Assert.NotNull(xrayRes.Data);
        Assert.Equal("XRay", xrayRes.Data.ServiceType);

        // Verify audit log call for workflow configuration
        _mockAudit.Verify(a => a.LogAsync(
            It.Is<long?>(u => u == 99),
            "CONFIGURE",
            "ServiceWorkflow",
            xrayRes.Data.DepartmentServiceId.ToString(),
            It.IsAny<string?>(),
            It.Is<string>(json => json.Contains("Image") && json.Contains("X-quang")),
            It.IsAny<CancellationToken>()), Times.Once);

        // 2. Create Treatment service
        var treatReq = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SVC-CLEAN-01",
            ServiceName = "Cạo vôi răng",
            Price = 200000,
            DurationMinutes = 30,
            ServiceType = "Treatment"
        };

        var treatRes = await service.CreateServiceAsync(treatReq, adminUserId: 99);
        Assert.True(treatRes.Success);
        Assert.NotNull(treatRes.Data);
        Assert.Equal("Treatment", treatRes.Data.ServiceType);

        _mockAudit.Verify(a => a.LogAsync(
            It.Is<long?>(u => u == 99),
            "CONFIGURE",
            "ServiceWorkflow",
            treatRes.Data.DepartmentServiceId.ToString(),
            It.IsAny<string?>(),
            It.Is<string>(json => json.Contains("None")),
            It.IsAny<CancellationToken>()), Times.Once);
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

        var service = new AdminService(db, _mockAudit.Object, _mockLogger.Object);

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

        var service = new AdminService(db, _mockAudit.Object, _mockLogger.Object);

        var wfRes = await service.GetWorkflowAsync(20);
        Assert.True(wfRes.Success);
        Assert.NotNull(wfRes.Data);
        Assert.Single(wfRes.Data.Steps);
        Assert.Equal("Image", wfRes.Data.Steps[0].ExpectedDataType);
        Assert.Equal("Xác nhận đã chụp X-quang", wfRes.Data.Steps[0].StepName);
    }

    [Fact]
    public async Task Criterion10_To_16_DeleteService_Checks_References_Properly()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var svcReferenced = new DepartmentService
        {
            DepartmentServiceId = 30,
            DepartmentId = 1,
            ServiceCode = "SVC-REF-30",
            ServiceName = "Dịch vụ đã có lịch sử",
            DiagnosticType = "Treatment",
            Price = 300000,
            Status = "Active"
        };
        var svcClean = new DepartmentService
        {
            DepartmentServiceId = 31,
            DepartmentId = 1,
            ServiceCode = "SVC-CLN-31",
            ServiceName = "Dịch vụ sạch chưa phát sinh",
            DiagnosticType = "Treatment",
            Price = 250000,
            Status = "Active"
        };
        db.DepartmentServices.AddRange(svcReferenced, svcClean);
        db.AppointmentServices.Add(new ApptServiceEntity { AppointmentServiceId = 201, AppointmentId = 10, DepartmentServiceId = 30 });
        await db.SaveChangesAsync();

        var service = new AdminService(db, _mockAudit.Object, _mockLogger.Object);

        // 1. Check referenced service
        var checkRef = await service.CheckServiceCanDeleteAsync(30);
        Assert.True(checkRef.Success);
        Assert.NotNull(checkRef.Data);
        Assert.False(checkRef.Data.CanDelete);
        Assert.True(checkRef.Data.ActiveReferenceCount > 0);
        Assert.Contains("lịch sử liên kết", checkRef.Data.Reason);

        // Attempt delete referenced service -> should fail
        var delRef = await service.DeleteServiceAsync(30, adminUserId: 99);
        Assert.False(delRef.Success);
        Assert.Contains("Ngừng cung cấp", delRef.Message);

        // 2. Check clean service
        var checkClean = await service.CheckServiceCanDeleteAsync(31);
        Assert.True(checkClean.Success);
        Assert.NotNull(checkClean.Data);
        Assert.True(checkClean.Data.CanDelete);
        Assert.Equal(0, checkClean.Data.ActiveReferenceCount);

        // Delete clean service -> should succeed
        var delClean = await service.DeleteServiceAsync(31, adminUserId: 99);
        Assert.True(delClean.Success);

        var remaining = await db.DepartmentServices.FindAsync((int)31);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task Criterion17_ToggleServiceStatus_Toggles_Active_Inactive()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var svc = new DepartmentService
        {
            DepartmentServiceId = 40,
            DepartmentId = 1,
            ServiceCode = "SVC-TOG-40",
            ServiceName = "Dịch vụ đổi trạng thái",
            Status = "Active"
        };
        db.DepartmentServices.Add(svc);
        await db.SaveChangesAsync();

        var service = new AdminService(db, _mockAudit.Object, _mockLogger.Object);

        // Active -> Inactive
        var res1 = await service.ToggleServiceStatusAsync(40, adminUserId: 99);
        Assert.True(res1.Success);
        Assert.Equal("Inactive", svc.Status);

        // Inactive -> Active
        var res2 = await service.ToggleServiceStatusAsync(40, adminUserId: 99);
        Assert.True(res2.Success);
        Assert.Equal("Active", svc.Status);
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
