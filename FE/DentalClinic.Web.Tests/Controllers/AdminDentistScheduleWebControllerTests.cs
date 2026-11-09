using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Admin;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Admin;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class AdminDentistScheduleWebControllerTests
{
    private readonly Mock<IAdminApiService> _mockAdminApi = new();
    private readonly Mock<IDentistScheduleApiService> _mockScheduleApi = new();

    private AdminDentistScheduleController CreateController(string role = "SystemAdministrator", long userId = 1)
    {
        var controller = new AdminDentistScheduleController(_mockAdminApi.Object, _mockScheduleApi.Object);

        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "Admin User"),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "CookieAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        return controller;
    }

    [Fact]
    public async Task Index_MonthlyTab_LoadsMonthlyScheduleAndReturnsView()
    {
        var controller = CreateController();

        _mockAdminApi.Setup(x => x.GetDentistAllocationsAsync(It.IsAny<long?>()))
            .ReturnsAsync(ApiResult<List<DentistDepartmentAllocationDto>>.Success(new List<DentistDepartmentAllocationDto>()));

        _mockAdminApi.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<AdminDepartmentDto>>.Success(new List<AdminDepartmentDto>()));

        _mockAdminApi.Setup(x => x.GetUsersAsync("Dentist", It.IsAny<string?>()))
            .ReturnsAsync(ApiResult<List<AdminUserDto>>.Success(new List<AdminUserDto>()));

        _mockAdminApi.Setup(x => x.GetRoomsAsync())
            .ReturnsAsync(ApiResult<List<AdminRoomDto>>.Success(new List<AdminRoomDto>()));

        _mockScheduleApi.Setup(x => x.GetSchedulesAsync(It.IsAny<DentistScheduleFilterRequest>()))
            .ReturnsAsync(ApiResult<List<DentistWorkScheduleDto>>.Success(new List<DentistWorkScheduleDto>()));

        _mockScheduleApi.Setup(x => x.GetAvailabilitiesAsync(It.IsAny<DentistAvailabilityFilterRequest>()))
            .ReturnsAsync(ApiResult<List<DentistAvailabilityDto>>.Success(new List<DentistAvailabilityDto>()));

        _mockScheduleApi.Setup(x => x.GetMonthlyScheduleAsync(It.IsAny<MonthlyDentistScheduleFilterRequest>()))
            .ReturnsAsync(ApiResult<MonthlyDentistScheduleDto>.Success(new MonthlyDentistScheduleDto
            {
                Year = 2026,
                Month = 11,
                TotalWorkingDays = 15,
                TotalWorkingHours = 60.0
            }));

        var result = await controller.Index(tab: "monthly", year: 2026, month: 11);

        result.Should().BeOfType<ViewResult>();
        var viewResult = (ViewResult)result;
        viewResult.Model.Should().BeOfType<AdminDentistScheduleManageViewModel>();
        var vm = (AdminDentistScheduleManageViewModel)viewResult.Model!;
        vm.ActiveTab.Should().Be("monthly");
        vm.SelectedYear.Should().Be(2026);
        vm.SelectedMonth.Should().Be(11);
        vm.MonthlySchedule.Should().NotBeNull();
        vm.MonthlySchedule!.TotalWorkingDays.Should().Be(15);
    }

    [Fact]
    public async Task BulkPreview_ReturnsJsonResultWithPreviewData()
    {
        var controller = CreateController();

        var previewResult = new BulkSchedulePreviewResultDto
        {
            TotalProposedShifts = 10,
            ValidShiftsCount = 8,
            DuplicateShiftsCount = 2
        };

        _mockScheduleApi.Setup(x => x.BulkPreviewScheduleAsync(It.IsAny<BulkScheduleAssignmentRequest>()))
            .ReturnsAsync(ApiResult<BulkSchedulePreviewResultDto>.Success(previewResult));

        var req = new BulkScheduleAssignmentRequest { DepartmentId = 1 };
        var result = await controller.BulkPreview(req);

        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        var apiRes = jsonResult.Value as ApiResult<BulkSchedulePreviewResultDto>;
        apiRes.Should().NotBeNull();
        apiRes!.IsSuccess.Should().BeTrue();
        apiRes.Data!.TotalProposedShifts.Should().Be(10);
    }

    [Fact]
    public async Task BulkApply_ReturnsJsonResultWithApplyData()
    {
        var controller = CreateController();

        var applyResult = new BulkScheduleApplyResultDto
        {
            CreatedCount = 8,
            SkippedCount = 2
        };

        _mockScheduleApi.Setup(x => x.BulkApplyScheduleAsync(It.IsAny<BulkScheduleApplyRequest>()))
            .ReturnsAsync(ApiResult<BulkScheduleApplyResultDto>.Success(applyResult));

        var req = new BulkScheduleApplyRequest { DepartmentId = 1, SkipConflicts = true };
        var result = await controller.BulkApply(req);

        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        var apiRes = jsonResult.Value as ApiResult<BulkScheduleApplyResultDto>;
        apiRes.Should().NotBeNull();
        apiRes!.IsSuccess.Should().BeTrue();
        apiRes.Data!.CreatedCount.Should().Be(8);
    }

    [Fact]
    public async Task CopyMonthPreview_ReturnsJsonResult()
    {
        var controller = CreateController();

        var copyPreview = new CopyMonthSchedulePreviewDto
        {
            SourceShiftsCount = 12,
            ProposedShiftsCount = 12,
            ValidCount = 10
        };

        _mockScheduleApi.Setup(x => x.CopyMonthPreviewScheduleAsync(It.IsAny<CopyMonthScheduleRequest>()))
            .ReturnsAsync(ApiResult<CopyMonthSchedulePreviewDto>.Success(copyPreview));

        var req = new CopyMonthScheduleRequest { SourceYear = 2026, SourceMonth = 10, TargetYear = 2026, TargetMonth = 11 };
        var result = await controller.CopyMonthPreview(req);

        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        var apiRes = jsonResult.Value as ApiResult<CopyMonthSchedulePreviewDto>;
        apiRes.Should().NotBeNull();
        apiRes!.Data!.ValidCount.Should().Be(10);
    }

    [Fact]
    public async Task DailyOverride_ReturnsJsonResult()
    {
        var controller = CreateController();

        _mockScheduleApi.Setup(x => x.DailyOverrideScheduleAsync(It.IsAny<DailyScheduleOverrideRequest>()))
            .ReturnsAsync(ApiResult.Success("Cập nhật ca trực thành công."));

        var req = new DailyScheduleOverrideRequest
        {
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 5)
        };
        var result = await controller.DailyOverride(req);

        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        var apiRes = jsonResult.Value as ApiResult;
        apiRes.Should().NotBeNull();
        apiRes!.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AffectedAppointments_ValidParams_ReturnsJsonResult()
    {
        var controller = CreateController();

        _mockScheduleApi.Setup(x => x.GetAffectedAppointmentsAsync(10, new DateOnly(2026, 11, 5), new TimeOnly(8, 0), new TimeOnly(12, 0)))
            .ReturnsAsync(ApiResult<List<AffectedAppointmentDto>>.Success(new List<AffectedAppointmentDto>
            {
                new() { AppointmentId = 100, AppointmentCode = "APT-01", PatientName = "Nguyen Van C" }
            }));

        var result = await controller.AffectedAppointments(10, "2026-11-05", "08:00", "12:00");

        result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)result;
        var apiRes = jsonResult.Value as ApiResult<List<AffectedAppointmentDto>>;
        apiRes.Should().NotBeNull();
        apiRes!.Data.Should().HaveCount(1);
        apiRes.Data![0].AppointmentCode.Should().Be("APT-01");
    }
}
