using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Dentist;
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
    private readonly Mock<IFeedbackApiService> _feedbackApiMock;

    public AppointmentWebControllerTests()
    {
        _apiMock = new Mock<IAppointmentApiService>();
        _feedbackApiMock = new Mock<IFeedbackApiService>();
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

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object)
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

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object)
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
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("Q001");
    }

    [Fact]
    public async Task Patient_Book_Get_WhenBlocked_ReturnsEligibilityInModel()
    {
        _apiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>()));

        _apiMock.Setup(x => x.GetBookingEligibilityAsync())
            .ReturnsAsync(ApiResult<BookingEligibilityDto>.Success(new BookingEligibilityDto
            {
                IsEligible = false,
                StrikeCount = 3,
                BlockReason = "Tài khoản đã tích lũy 3 lần vi phạm."
            }));

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.Book();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<PatientBookViewModel>().Subject;
        model.Eligibility.Should().NotBeNull();
        model.Eligibility!.IsEligible.Should().BeFalse();
        model.Eligibility.StrikeCount.Should().Be(3);
    }

    [Fact]
    public async Task Patient_Book_Post_WhenBlocked_RejectsAndReturnsViewWithErrorMessage()
    {
        _apiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>()));

        _apiMock.Setup(x => x.GetBookingEligibilityAsync())
            .ReturnsAsync(ApiResult<BookingEligibilityDto>.Success(new BookingEligibilityDto
            {
                IsEligible = false,
                StrikeCount = 3,
                BlockReason = "Tài khoản bị tạm khóa 30 ngày."
            }));

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var model = new PatientBookViewModel
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(1).AddHours(9),
            ReasonForVisit = "Đau răng"
        };

        var result = await controller.Book(model);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.TempData["ErrorMessage"]?.ToString().Should().Contain("tạm khóa");
    }

    [Fact]
    public async Task Receptionist_NoShow_Success_RedirectsToTodayCheckInWithSuccessMessage()
    {
        _apiMock.Setup(x => x.ReceptionistMarkNoShowAsync(10, "Bệnh nhân không đến"))
            .ReturnsAsync(ApiResult.Success("OK"));

        var controller = new ReceptionistAppointmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Receptionist"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.NoShow(10, "Bệnh nhân không đến");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("TodayCheckIn");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("vắng mặt");
    }

    [Fact]
    public async Task Patient_GetServices_ReturnsJsonResult()
    {
        _apiMock.Setup(x => x.GetServicesAsync(1))
            .ReturnsAsync(ApiResult<List<DepartmentServiceDto>>.Success(new List<DepartmentServiceDto>
            {
                new() { DepartmentServiceId = 10, ServiceName = "Khám Răng" }
            }));

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.GetServices(1);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        var list = json.Value.Should().BeAssignableTo<IEnumerable<DepartmentServiceDto>>().Subject;
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task Patient_GetDentists_ReturnsJsonResult()
    {
        _apiMock.Setup(x => x.GetDentistsAsync(1))
            .ReturnsAsync(ApiResult<List<DentistOptionDto>>.Success(new List<DentistOptionDto>
            {
                new() { DentistUserId = 401, FullName = "Bác Sĩ An" }
            }));

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.GetDentists(1);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        var list = json.Value.Should().BeAssignableTo<IEnumerable<DentistOptionDto>>().Subject;
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task Patient_GetSlots_ValidDate_ReturnsJsonResult()
    {
        _apiMock.Setup(x => x.GetAvailableSlotsAsync(1, 10, new DateOnly(2026, 10, 15), null))
            .ReturnsAsync(ApiResult<List<AvailableSlotDto>>.Success(new List<AvailableSlotDto>
            {
                new() { StartTime = new DateTime(2026, 10, 15, 9, 0, 0), EndTime = new DateTime(2026, 10, 15, 9, 30, 0) }
            }));

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.GetSlots(1, 10, "2026-10-15", null);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        var list = json.Value.Should().BeAssignableTo<IEnumerable<AvailableSlotDto>>().Subject;
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task Patient_GetSlots_InvalidDate_ReturnsBadRequest()
    {
        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.GetSlots(1, 10, "invalid-date", null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Patient_Detail_Success_ReturnsViewWithDetail()
    {
        _apiMock.Setup(x => x.GetAppointmentDetailAsync(100))
            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto
            {
                AppointmentId = 100,
                AppointmentCode = "APP-001",
                Status = "Confirmed"
            }));

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.Detail(100);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<AppointmentDetailViewModel>().Subject;
        vm.Appointment.AppointmentId.Should().Be(100);
        vm.CurrentUserRole.Should().Be("Patient");
    }

    [Fact]
    public async Task Patient_Cancel_Success_RedirectsToDetailWithTempData()
    {
        _apiMock.Setup(x => x.PatientCancelAsync(100, "Bận việc đột xuất"))
            .ReturnsAsync(ApiResult.Success("Hủy thành công"));

        var controller = new PatientAppointmentController(_apiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Cancel(100, "Bận việc đột xuất");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Detail");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task Receptionist_TodayCheckIn_ReturnsViewWithList()
    {
        _apiMock.Setup(x => x.GetReceptionistTodayConfirmedAsync("test"))
            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Success(new List<AppointmentDto>
            {
                new() { AppointmentId = 1, AppointmentCode = "APP-TODAY-01", Status = "Confirmed" }
            }));

        var controller = new ReceptionistAppointmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Receptionist")
        };

        var result = await controller.TodayCheckIn("test");

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<ReceptionistTodayCheckInViewModel>().Subject;
        vm.ConfirmedAppointments.Should().HaveCount(1);
        vm.SearchQuery.Should().Be("test");
    }

    [Fact]
    public async Task Receptionist_CheckIn_Success_RedirectsToTodayCheckInWithSuccessMessage()
    {
        _apiMock.Setup(x => x.ReceptionistCheckInAsync(10, "Bệnh nhân đã đến"))
            .ReturnsAsync(ApiResult.Success("Check-in thành công"));

        var controller = new ReceptionistAppointmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Receptionist"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.CheckIn(10, "Bệnh nhân đã đến");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("TodayCheckIn");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task Receptionist_NoShow_Failure_RedirectsToTodayCheckInWithErrorMessage()
    {
        _apiMock.Setup(x => x.ReceptionistMarkNoShowAsync(10, "Không đến khám"))
            .ReturnsAsync(ApiResult.Failure("Lỗi ghi nhận vắng mặt"));

        var controller = new ReceptionistAppointmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Receptionist"),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.NoShow(10, "Không đến khám");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("TodayCheckIn");
        controller.TempData["ErrorMessage"].Should().Be("Lỗi ghi nhận vắng mặt");
    }

    [Fact]
    public async Task DentistAppointment_Today_ReturnsViewWithAppointments()
    {
        _apiMock.Setup(x => x.GetDentistTodayAppointmentsAsync())
            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Success(new List<AppointmentDto>
            {
                new()
                {
                    AppointmentId = 99,
                    AppointmentCode = "APT-99",
                    PatientName = "Nguyễn Văn A",
                    ServiceName = "Khám răng định kỳ",
                    Status = "CheckedIn"
                }
            }));

        var controller = new DentistAppointmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(301, "Dentist")
        };

        var result = await controller.Today();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<DentistTodayAppointmentsViewModel>().Subject;
        vm.Appointments.Should().HaveCount(1);
        vm.Appointments[0].AppointmentCode.Should().Be("APT-99");
    }
}
