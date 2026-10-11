using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Department;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.DentistSchedule;
using DentalClinic.Web.ViewModels.Department;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class DepartmentManagementWebControllerTests
{
    private readonly Mock<IDentistScheduleApiService> _scheduleApiMock;
    private readonly Mock<IAdminApiService> _adminApiMock;

    public DepartmentManagementWebControllerTests()
    {
        _scheduleApiMock = new Mock<IDentistScheduleApiService>();
        _adminApiMock = new Mock<IAdminApiService>();
    }

    private static ControllerContext CreateContext(long userId = 2, string role = "DepartmentManager", string name = "Trưởng Khoa")
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "CookieAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        return new ControllerContext { HttpContext = httpContext };
    }

    private static TempDataDictionary CreateTempData()
    {
        return new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
    }

    // ==========================================
    // DEPARTMENT SCHEDULE TESTS
    // ==========================================

    [Fact]
    public async Task DepartmentSchedule_Index_ReturnsCalendarGrid_WithComputedDays()
    {
        _adminApiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<AdminDepartmentDto>>.Success(new List<AdminDepartmentDto>
            {
                new() { DepartmentId = 1, Name = "Nha Khoa Tổng Quát" }
            }));

        _adminApiMock.Setup(x => x.GetUsersAsync("Dentist", null))
            .ReturnsAsync(ApiResult<List<AdminUserDto>>.Success(new List<AdminUserDto>
            {
                new() { UserId = 10, FullName = "BS Nguyễn Văn A", Code = "BS01" }
            }));

        _scheduleApiMock.Setup(x => x.GetSchedulesAsync(It.IsAny<DentistScheduleFilterRequest>()))
            .ReturnsAsync(ApiResult<List<DentistWorkScheduleDto>>.Success(new List<DentistWorkScheduleDto>
            {
                new()
                {
                    DentistScheduleId = 1,
                    DentistUserId = 10,
                    DentistName = "BS Nguyễn Văn A",
                    DepartmentId = 1,
                    DepartmentName = "Nha Khoa Tổng Quát",
                    WorkDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(12, 0),
                    Status = "Scheduled"
                }
            }));

        var controller = new DepartmentScheduleController(_scheduleApiMock.Object, _adminApiMock.Object)
        {
            ControllerContext = CreateContext(),
            TempData = CreateTempData()
        };

        var result = await controller.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<DepartmentScheduleManageViewModel>().Subject;
        vm.CalendarDays.Should().NotBeEmpty();
        vm.CalendarDays.Count.Should().BeOneOf(35, 42); // 5 or 6 weeks grid
        vm.Schedules.Should().HaveCount(1);
    }

    [Fact]
    public async Task DepartmentSchedule_BulkCreate_ValidRange_CreatesSchedulesAndRedirects()
    {
        _scheduleApiMock.Setup(x => x.CreateScheduleAsync(It.IsAny<CreateDentistScheduleRequest>()))
            .ReturnsAsync(ApiResult<DentistWorkScheduleDto>.Success(new DentistWorkScheduleDto()));

        var controller = new DepartmentScheduleController(_scheduleApiMock.Object, _adminApiMock.Object)
        {
            ControllerContext = CreateContext(),
            TempData = CreateTempData()
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var bulkForm = new BulkScheduleFormModel
        {
            DentistUserId = 10,
            DepartmentId = 1,
            StartDate = today,
            EndDate = today.AddDays(4),
            SelectedDaysOfWeek = new List<int> { 1, 2, 3, 4, 5, 6, 7 },
            ShiftPreset = "Morning",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0)
        };

        var result = await controller.BulkCreate(bulkForm);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("thành công");
    }

    [Fact]
    public async Task DepartmentSchedule_BulkCreate_EndDateBeforeStartDate_ReturnsErrorRedirect()
    {
        var controller = new DepartmentScheduleController(_scheduleApiMock.Object, _adminApiMock.Object)
        {
            ControllerContext = CreateContext(),
            TempData = CreateTempData()
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var bulkForm = new BulkScheduleFormModel
        {
            DentistUserId = 10,
            DepartmentId = 1,
            StartDate = today.AddDays(5),
            EndDate = today,
            SelectedDaysOfWeek = new List<int> { 1, 2, 3 }
        };

        var result = await controller.BulkCreate(bulkForm);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["ErrorMessage"]?.ToString().Should().Contain("Ngày kết thúc phải lớn hơn");
    }

    // ==========================================
    // DEPARTMENT SERVICE TESTS
    // ==========================================

    [Fact]
    public async Task DepartmentService_Index_ReturnsServicesView()
    {
        _adminApiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<AdminDepartmentDto>>.Success(new List<AdminDepartmentDto>
            {
                new() { DepartmentId = 1, Name = "Nha Khoa Tổng Quát" }
            }));

        _adminApiMock.Setup(x => x.GetServicesAsync(It.IsAny<long?>(), It.IsAny<string?>()))
            .ReturnsAsync(ApiResult<List<AdminDepartmentServiceDto>>.Success(new List<AdminDepartmentServiceDto>
            {
                new()
                {
                    DepartmentServiceId = 101,
                    DepartmentId = 1,
                    DepartmentName = "Nha Khoa Tổng Quát",
                    ServiceName = "Trám răng Composite",
                    Price = 500000,
                    Status = "Active"
                }
            }));

        var controller = new DepartmentServiceController(_adminApiMock.Object)
        {
            ControllerContext = CreateContext(),
            TempData = CreateTempData()
        };

        var result = await controller.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<DepartmentServicesViewModel>().Subject;
        vm.Services.Should().HaveCount(1);
        vm.Services[0].ServiceName.Should().Be("Trám răng Composite");
    }

    [Fact]
    public async Task DepartmentService_Create_ValidRequest_CallsApiAndRedirects()
    {
        _adminApiMock.Setup(x => x.CreateServiceAsync(It.IsAny<CreateDepartmentServiceRequest>()))
            .ReturnsAsync(ApiResult<AdminDepartmentServiceDto>.Success(new AdminDepartmentServiceDto()));

        var controller = new DepartmentServiceController(_adminApiMock.Object)
        {
            ControllerContext = CreateContext(),
            TempData = CreateTempData()
        };

        var req = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceName = "Nhổ răng khôn",
            Price = 1200000,
            DurationMinutes = 45,
            Status = "Active"
        };

        var result = await controller.Create(req);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("thành công");
    }

    [Fact]
    public async Task DepartmentService_ToggleStatus_CallsApiAndRedirects()
    {
        _adminApiMock.Setup(x => x.ToggleServiceStatusAsync(101))
            .ReturnsAsync(ApiResult.Success());

        var controller = new DepartmentServiceController(_adminApiMock.Object)
        {
            ControllerContext = CreateContext(),
            TempData = CreateTempData()
        };

        var result = await controller.ToggleStatus(101, 1);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("thành công");
    }

    // ==========================================
    // DEPARTMENT DENTIST ALLOCATION TESTS
    // ==========================================

    [Fact]
    public async Task DepartmentDentist_Index_ReturnsAllocationsView()
    {
        _adminApiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<AdminDepartmentDto>>.Success(new List<AdminDepartmentDto>
            {
                new() { DepartmentId = 1, Name = "Nha Khoa Tổng Quát" }
            }));

        _adminApiMock.Setup(x => x.GetUsersAsync("Dentist", null))
            .ReturnsAsync(ApiResult<List<AdminUserDto>>.Success(new List<AdminUserDto>
            {
                new() { UserId = 10, FullName = "BS Nguyễn Văn A" }
            }));

        _adminApiMock.Setup(x => x.GetDentistAllocationsAsync(It.IsAny<long?>()))
            .ReturnsAsync(ApiResult<List<DentistDepartmentAllocationDto>>.Success(new List<DentistDepartmentAllocationDto>
            {
                new()
                {
                    DentistDepartmentId = 1,
                    DentistUserId = 10,
                    DentistName = "BS Nguyễn Văn A",
                    DepartmentId = 1,
                    DepartmentName = "Nha Khoa Tổng Quát",
                    Status = "Active",
                    AssignedAt = DateTime.UtcNow
                }
            }));

        var controller = new DepartmentDentistController(_adminApiMock.Object)
        {
            ControllerContext = CreateContext(),
            TempData = CreateTempData()
        };

        var result = await controller.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<DepartmentDentistsViewModel>().Subject;
        vm.Allocations.Should().HaveCount(1);
        vm.Allocations[0].DentistName.Should().Be("BS Nguyễn Văn A");
    }

    [Fact]
    public async Task DepartmentDentist_Assign_ValidRequest_CallsApiAndRedirects()
    {
        _adminApiMock.Setup(x => x.AssignDentistToDepartmentAsync(It.IsAny<AssignDentistDepartmentRequest>()))
            .ReturnsAsync(ApiResult<DentistDepartmentAllocationDto>.Success(new DentistDepartmentAllocationDto()));

        var controller = new DepartmentDentistController(_adminApiMock.Object)
        {
            ControllerContext = CreateContext(),
            TempData = CreateTempData()
        };

        var req = new AssignDentistDepartmentRequest
        {
            DentistUserId = 10,
            DepartmentId = 1,
            Status = "Active"
        };

        var result = await controller.Assign(req);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("thành công");
    }

    [Fact]
    public async Task DepartmentDentist_ToggleStatus_CallsApiAndRedirects()
    {
        _adminApiMock.Setup(x => x.ToggleDentistAllocationStatusAsync(5))
            .ReturnsAsync(ApiResult.Success());

        var controller = new DepartmentDentistController(_adminApiMock.Object)
        {
            ControllerContext = CreateContext(),
            TempData = CreateTempData()
        };

        var result = await controller.ToggleStatus(5, 1);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("thành công");
    }
}
