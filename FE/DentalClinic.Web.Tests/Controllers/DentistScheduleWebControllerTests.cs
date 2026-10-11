using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Dentist;
using DentalClinic.Web.Controllers.Department;
using DentalClinic.Web.Controllers.Patient;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.DentistSchedule;
using DentalClinic.Web.ViewModels.Feedback;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class DentistScheduleWebControllerTests
{
    private readonly Mock<IDentistScheduleApiService> _scheduleApiMock;
    private readonly Mock<IFeedbackApiService> _feedbackApiMock;
    private readonly Mock<IAdminApiService> _adminApiMock;
    private readonly Mock<IAppointmentApiService> _appointmentApiMock;

    public DentistScheduleWebControllerTests()
    {
        _scheduleApiMock = new Mock<IDentistScheduleApiService>();
        _feedbackApiMock = new Mock<IFeedbackApiService>();
        _adminApiMock = new Mock<IAdminApiService>();
        _appointmentApiMock = new Mock<IAppointmentApiService>();
    }

    private static ControllerContext CreateContext(long userId, string role, string name = "Test User")
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
    // DEPARTMENT SCHEDULE CONTROLLER TESTS
    // ==========================================

    [Fact]
    public async Task DepartmentSchedule_Index_ReturnsViewWithData()
    {
        _adminApiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<AdminDepartmentDto>>.Success(new List<AdminDepartmentDto>
            {
                new() { DepartmentId = 1, Name = "Khoa Răng Tổng Quát" }
            }));

        _adminApiMock.Setup(x => x.GetUsersAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(ApiResult<List<AdminUserDto>>.Success(new List<AdminUserDto>
            {
                new() { UserId = 10, FullName = "Dr. John" }
            }));

        _scheduleApiMock.Setup(x => x.GetSchedulesAsync(It.IsAny<DentistScheduleFilterRequest>()))
            .ReturnsAsync(ApiResult<List<DentistWorkScheduleDto>>.Success(new List<DentistWorkScheduleDto>
            {
                new() { DentistScheduleId = 1, DentistUserId = 10, DentistName = "Dr. John", Status = "Scheduled" }
            }));

        var controller = new DepartmentScheduleController(_scheduleApiMock.Object, _adminApiMock.Object)
        {
            ControllerContext = CreateContext(2, "DepartmentManager")
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DepartmentScheduleManageViewModel>().Subject;
        model.Departments.Should().HaveCount(1);
        model.Dentists.Should().HaveCount(1);
        model.Schedules.Should().HaveCount(1);
    }

    [Fact]
    public async Task DepartmentSchedule_Create_ValidForm_RedirectsWithSuccess()
    {
        _scheduleApiMock.Setup(x => x.CreateScheduleAsync(It.IsAny<CreateDentistScheduleRequest>()))
            .ReturnsAsync(ApiResult<DentistWorkScheduleDto>.Success(new DentistWorkScheduleDto { DentistScheduleId = 99 }));

        var controller = new DepartmentScheduleController(_scheduleApiMock.Object, _adminApiMock.Object)
        {
            ControllerContext = CreateContext(2, "DepartmentManager"),
            TempData = CreateTempData()
        };

        var form = new CreateScheduleFormModel
        {
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0)
        };

        var result = await controller.Create(form);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("thành công");
    }

    [Fact]
    public async Task DepartmentSchedule_Create_InvalidModel_RedirectsWithError()
    {
        var controller = new DepartmentScheduleController(_scheduleApiMock.Object, _adminApiMock.Object)
        {
            ControllerContext = CreateContext(2, "DepartmentManager"),
            TempData = CreateTempData()
        };
        controller.ModelState.AddModelError("DentistUserId", "Required");

        var form = new CreateScheduleFormModel
        {
            WorkDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
        };

        var result = await controller.Create(form);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["ErrorMessage"]?.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DepartmentSchedule_Cancel_CallsApiAndRedirects()
    {
        _scheduleApiMock.Setup(x => x.CancelScheduleAsync(5))
            .ReturnsAsync(ApiResult.Success());

        var controller = new DepartmentScheduleController(_scheduleApiMock.Object, _adminApiMock.Object)
        {
            ControllerContext = CreateContext(2, "DepartmentManager"),
            TempData = CreateTempData()
        };

        var result = await controller.Cancel(5, 1, "2026-10-15");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("hủy ca trực");
    }

    // ==========================================
    // DENTIST SCHEDULE & AVAILABILITY TESTS
    // ==========================================

    [Fact]
    public async Task DentistSchedule_Schedule_ReturnsViewWithPersonalSchedules()
    {
        _scheduleApiMock.Setup(x => x.GetMySchedulesAsync(It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>()))
            .ReturnsAsync(ApiResult<List<DentistWorkScheduleDto>>.Success(new List<DentistWorkScheduleDto>
            {
                new() { DentistScheduleId = 1, Status = "Scheduled" }
            }));

        var controller = new DentistScheduleController(_scheduleApiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContext(10, "Dentist")
        };

        var result = await controller.Schedule();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DentistPersonalScheduleViewModel>().Subject;
        model.Schedules.Should().HaveCount(1);
    }

    [Fact]
    public async Task DentistSchedule_Availability_ReturnsViewWithItems()
    {
        _scheduleApiMock.Setup(x => x.GetMyAvailabilitiesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(ApiResult<List<DentistAvailabilityDto>>.Success(new List<DentistAvailabilityDto>
            {
                new() { AvailabilityId = 1, AvailabilityStatus = "OnLeave", Reason = "Annual leave" }
            }));

        var controller = new DentistScheduleController(_scheduleApiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContext(10, "Dentist")
        };

        var result = await controller.Availability();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DentistAvailabilityViewModel>().Subject;
        model.Availabilities.Should().HaveCount(1);
    }

    [Fact]
    public async Task DentistSchedule_CreateAvailability_Valid_RedirectsWithSuccess()
    {
        _scheduleApiMock.Setup(x => x.CreateAvailabilityAsync(It.IsAny<CreateDentistAvailabilityRequest>()))
            .ReturnsAsync(ApiResult<DentistAvailabilityDto>.Success(new DentistAvailabilityDto { AvailabilityId = 2 }));

        var controller = new DentistScheduleController(_scheduleApiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContext(10, "Dentist"),
            TempData = CreateTempData()
        };

        var form = new CreateAvailabilityFormModel
        {
            StartDateTime = DateTime.UtcNow.AddDays(2),
            EndDateTime = DateTime.UtcNow.AddDays(3),
            AvailabilityStatus = "OnLeave",
            Reason = "Nghỉ phép"
        };

        var result = await controller.CreateAvailability(form);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Availability");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("thành công");
    }

    [Fact]
    public async Task DentistSchedule_DeleteAvailability_CallsApi_Redirects()
    {
        _scheduleApiMock.Setup(x => x.DeleteAvailabilityAsync(2))
            .ReturnsAsync(ApiResult.Success());

        var controller = new DentistScheduleController(_scheduleApiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContext(10, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.DeleteAvailability(2);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Availability");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("xóa");
    }

    [Fact]
    public async Task DentistSchedule_Feedback_ReturnsViewWithSummary()
    {
        _feedbackApiMock.Setup(x => x.GetDentistFeedbackSummaryAsync(10))
            .ReturnsAsync(ApiResult<DentistFeedbackSummaryDto>.Success(new DentistFeedbackSummaryDto
            {
                DentistUserId = 10,
                DentistName = "Dr. John",
                TotalFeedbacks = 10,
                AverageDentistRating = 4.8
            }));

        var controller = new DentistScheduleController(_scheduleApiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContext(10, "Dentist")
        };

        var result = await controller.Feedback();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DentistFeedbackViewViewModel>().Subject;
        model.Summary.AverageDentistRating.Should().Be(4.8);
        model.Summary.TotalFeedbacks.Should().Be(10);
    }

    // ==========================================
    // PATIENT FEEDBACK ACTIONS TESTS
    // ==========================================

    [Fact]
    public async Task Patient_Feedback_Get_CompletedAppointment_ReturnsView()
    {
        _appointmentApiMock.Setup(x => x.GetAppointmentDetailAsync(100))
            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto
            {
                AppointmentId = 100,
                AppointmentCode = "APP-100",
                Status = "Completed",
                DepartmentName = "Khoa Răng Tổng Quát",
                ServiceName = "Trám răng",
                AssignedDentistName = "Dr. John"
            }));

        _feedbackApiMock.Setup(x => x.GetFeedbackByAppointmentIdAsync(100))
            .ReturnsAsync(ApiResult<VisitFeedbackDto?>.Failure("Not found"));

        var controller = new PatientAppointmentController(_appointmentApiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContext(50, "Patient")
        };

        var result = await controller.Feedback(100);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<PatientFeedbackFormViewModel>().Subject;
        model.AppointmentId.Should().Be(100);
        model.IsAlreadySubmitted.Should().BeFalse();
    }

    [Fact]
    public async Task Patient_Feedback_Get_NotCompletedAppointment_RedirectsToDetail()
    {
        _appointmentApiMock.Setup(x => x.GetAppointmentDetailAsync(100))
            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto
            {
                AppointmentId = 100,
                Status = "Confirmed"
            }));

        var controller = new PatientAppointmentController(_appointmentApiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContext(50, "Patient"),
            TempData = CreateTempData()
        };

        var result = await controller.Feedback(100);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Detail");
        controller.TempData["ErrorMessage"]?.ToString().Should().Contain("hoàn thành");
    }

    [Fact]
    public async Task Patient_SubmitFeedback_Valid_RedirectsToDetailWithSuccess()
    {
        _feedbackApiMock.Setup(x => x.CreateFeedbackAsync(It.IsAny<CreateFeedbackRequest>()))
            .ReturnsAsync(ApiResult<VisitFeedbackDto>.Success(new VisitFeedbackDto { FeedbackId = 1 }));

        var controller = new PatientAppointmentController(_appointmentApiMock.Object, _feedbackApiMock.Object)
        {
            ControllerContext = CreateContext(50, "Patient"),
            TempData = CreateTempData()
        };

        var form = new PatientFeedbackFormViewModel
        {
            AppointmentId = 100,
            DentistRating = 5,
            ClinicRating = 5,
            Comment = "Rất hài lòng!"
        };

        var result = await controller.SubmitFeedback(form);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Detail");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("Cảm ơn");
    }
}
