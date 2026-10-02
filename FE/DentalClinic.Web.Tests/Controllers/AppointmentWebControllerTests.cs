using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Department;
using DentalClinic.Web.Controllers.Patient;
using DentalClinic.Web.Controllers.Receptionist;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Appointments;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class AppointmentWebControllerTests
{
    private readonly Mock<IAppointmentApiService> _apiMock;

    public AppointmentWebControllerTests()
    {
        _apiMock = new Mock<IAppointmentApiService>();
    }

    private static ControllerContext CreateContextWithUser(long userId, string role, string name = "User")
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

    [Fact]
    public async Task Patient_Book_Get_ReturnsViewWithDepartments()
    {
        _apiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>
            {
                new() { DepartmentId = 1, Name = "Khoa Răng Tổng Quát" }
            }));

        var controller = new PatientAppointmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.Book();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<PatientBookViewModel>().Subject;
        model.Departments.Should().HaveCount(1);
    }

    [Fact]
    public async Task Patient_Book_Post_Valid_RedirectsToSuccess()
    {
        _apiMock.Setup(x => x.BookAppointmentAsync(It.IsAny<CreateAppointmentRequest>()))
            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto
            {
                AppointmentId = 555,
                AppointmentCode = "APP-20261003-ABCDEF"
            }));

        var controller = new PatientAppointmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var model = new PatientBookViewModel
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(1).AddHours(9),
            ReasonForVisit = "Đau răng cần khám gấp"
        };

        var result = await controller.Book(model);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Success");
        redirect.RouteValues!["id"].Should().Be(555L);
    }

    [Fact]
    public async Task Receptionist_Requests_ReturnsViewWithList()
    {
        _apiMock.Setup(x => x.GetReceptionistRequestsAsync())
            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Success(new List<AppointmentDto>
            {
                new() { AppointmentId = 1, AppointmentCode = "APP-001" },
                new() { AppointmentId = 2, AppointmentCode = "APP-002" }
            }));

        var controller = new ReceptionistAppointmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Receptionist")
        };

        var result = await controller.Requests();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<ReceptionistRequestsViewModel>().Subject;
        vm.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Receptionist_Forward_Success_RedirectsToRequestsWithTempData()
    {
        _apiMock.Setup(x => x.ReceptionistForwardAsync(10, "OK"))
            .ReturnsAsync(ApiResult.Success("OK"));

        var controller = new ReceptionistAppointmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Receptionist"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Forward(10, "OK");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Requests");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task Department_Confirm_Valid_RedirectsToRequestsWithSuccessMessage()
    {
        _apiMock.Setup(x => x.ManagerConfirmAsync(10, It.IsAny<ManagerConfirmRequest>()))
            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto
            {
                AppointmentId = 10,
                QueueNumber = "Q001"
            }));

        var controller = new DepartmentAppointmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(301, "DepartmentManager"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var model = new DepartmentReviewViewModel
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = DateTime.Today.AddDays(1).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(1).AddHours(9).AddMinutes(30)
        };

        var result = await controller.Confirm(10, model);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Requests");
        controller.TempData["SuccessMessage"].ToString().Should().Contain("Q001");
    }
}
