using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Dentist;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class DentistClinicalWorkflowWebTests
{
    private static ControllerContext CreateContextWithUser(long userId, string role, string name = "Dentist")
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
        var httpContext = new DefaultHttpContext();
        return new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task CompleteExam_WithFollowUpInfo_CallsApiService_AndRedirectsToToday()
    {
        var medRecordApiMock = new Mock<IMedicalRecordApiService>();
        var apptApiMock = new Mock<IAppointmentApiService>();

        apptApiMock.Setup(a => a.DentistCompleteAppointmentAsync(
            123,
            "Lấy sạch vôi răng",
            "Tái khám kiểm tra sau 1 tuần",
            It.IsAny<DateTime>(),
            "Chưa chốt ngày"))
            .ReturnsAsync(ApiResult.Success("Hoàn tất ca khám"));

        var controller = new DentistMedicalRecordController(
            medRecordApiMock.Object,
            apptApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(3, "Dentist"),
            TempData = CreateTempData()
        };

        var targetDate = DateTime.UtcNow.AddDays(7);
        var result = await controller.CompleteExam(
            appointmentId: 123,
            patientUserId: 10,
            notes: "Lấy sạch vôi răng",
            followUpReason: "Tái khám kiểm tra sau 1 tuần",
            recommendedFollowUpDate: targetDate,
            followUpReasonCategory: "Chưa chốt ngày");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Today");
        redirect.ControllerName.Should().Be("DentistAppointment");
        controller.TempData["SuccessMessage"].Should().Be("Hoàn tất ca khám");
    }

    [Fact]
    public async Task CompleteExam_WhenBackendFails_SetsErrorMessage_AndRedirectsBack()
    {
        var medRecordApiMock = new Mock<IMedicalRecordApiService>();
        var apptApiMock = new Mock<IAppointmentApiService>();

        apptApiMock.Setup(a => a.DentistCompleteAppointmentAsync(
            123,
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<string?>()))
            .ReturnsAsync(ApiResult.Failure("Dịch vụ này yêu cầu phải có phác đồ điều trị."));

        var controller = new DentistMedicalRecordController(
            medRecordApiMock.Object,
            apptApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(3, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.CompleteExam(
            appointmentId: 123,
            patientUserId: 10,
            notes: "Cố tình hoàn tất");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["ErrorMessage"].Should().Be("Dịch vụ này yêu cầu phải có phác đồ điều trị.");
    }

    [Fact]
    public async Task DentistAppointmentController_Complete_WithFollowUpParams_CallsApi_AndRedirects()
    {
        var apptApiMock = new Mock<IAppointmentApiService>();
        apptApiMock.Setup(a => a.DentistCompleteAppointmentAsync(
            456,
            "Hoàn tất trám răng",
            "Theo dõi tủy 2 tuần",
            It.IsAny<DateTime>(),
            "Theo dõi tủy răng"))
            .ReturnsAsync(ApiResult.Success("Thành công"));

        var controller = new DentistAppointmentController(apptApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(3, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.Complete(
            id: 456,
            notes: "Hoàn tất trám răng",
            date: "2026-11-12",
            returnUrl: null,
            followUpReason: "Theo dõi tủy 2 tuần",
            recommendedFollowUpDate: DateTime.UtcNow.AddDays(14),
            followUpReasonCategory: "Theo dõi tủy răng");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Today");
        controller.TempData["SuccessMessage"].Should().Be("Thành công");
    }
}
