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

public class DentistXrayQueueControllerTests
{
    private readonly Mock<IImagingApiService> _imagingApiMock;

    public DentistXrayQueueControllerTests()
    {
        _imagingApiMock = new Mock<IImagingApiService>();
    }

    private static ControllerContext CreateContext(long userId, string role, string name = "Dentist")
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

    [Fact]
    public async Task Index_ReturnsViewWithActiveDutyAndQueueStatus()
    {
        var activeDuty = new XrayDentistRosterDto
        {
            RosterId = 1,
            RoomId = 9,
            RoomCode = "XRAY-01",
            RoomName = "Phong X-quang KTS",
            DentistUserId = 10,
            DentistFullName = "BS. Tran Van Hung",
            HasRadiationSafetyCert = true,
            RadiationCertNumber = "ATBX-001",
            ShiftType = "Morning",
            StartTime = new TimeOnly(7, 30),
            EndTime = new TimeOnly(11, 30),
            Status = "Active"
        };

        var queueSummary = new ImagingQueueSummaryDto
        {
            RoomId = 9,
            RoomCode = "XRAY-01",
            RoomName = "Phong X-quang KTS",
            CurrentWaitingCount = 2,
            CurrentInProgressCount = 1,
            EstimatedNextWaitMinutes = 15,
            HasOnDutyDentist = true,
            OnDutyDentistUserId = 10,
            OnDutyDentistName = "BS. Tran Van Hung"
        };

        _imagingApiMock.Setup(x => x.GetMyXrayDutyAsync(It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>()))
            .ReturnsAsync(ApiResult<List<XrayDentistRosterDto>>.Success(new List<XrayDentistRosterDto> { activeDuty }));

        _imagingApiMock.Setup(x => x.GetQueueStatusAsync(9))
            .ReturnsAsync(ApiResult<ImagingQueueSummaryDto>.Success(queueSummary));

        var controller = new DentistXrayQueueController(_imagingApiMock.Object)
        {
            ControllerContext = CreateContext(10, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.Index(9);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ImagingQueueSummaryDto>().Subject;
        model.RoomCode.Should().Be("XRAY-01");
        model.CurrentWaitingCount.Should().Be(2);

        var duty = viewResult.ViewData["ActiveDuty"] as XrayDentistRosterDto;
        duty.Should().NotBeNull();
        duty!.DentistFullName.Should().Be("BS. Tran Van Hung");
    }

    [Fact]
    public async Task StartSession_Success_RedirectsToIndexWithSuccessMessage()
    {
        var sessionDto = new ImagingSessionDto
        {
            ImagingSessionId = 301,
            Status = "InProgress"
        };

        _imagingApiMock.Setup(x => x.StartSessionAsync(301))
            .ReturnsAsync(ApiResult<ImagingSessionDto>.Success(sessionDto, "Thanh cong"));

        var controller = new DentistXrayQueueController(_imagingApiMock.Object)
        {
            ControllerContext = CreateContext(10, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.StartSession(301, roomId: 9);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(DentistXrayQueueController.Index));
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task AcquireImage_Success_RedirectsToIndex()
    {
        var sessionDto = new ImagingSessionDto
        {
            ImagingSessionId = 301,
            Status = "ImageAcquired"
        };

        _imagingApiMock.Setup(x => x.AcquireImageAsync(301))
            .ReturnsAsync(ApiResult<ImagingSessionDto>.Success(sessionDto));

        var controller = new DentistXrayQueueController(_imagingApiMock.Object)
        {
            ControllerContext = CreateContext(10, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.AcquireImage(301, roomId: 9);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(DentistXrayQueueController.Index));
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task UploadResult_Success_CallsUploadAndComplete_RedirectsToIndex()
    {
        var resultDto = new DiagnosticResultDto
        {
            DiagnosticResultId = 55,
            FileUrl = "/uploads/xray/test.jpg"
        };

        var completeSessionDto = new ImagingSessionDto
        {
            ImagingSessionId = 301,
            Status = "Completed"
        };

        _imagingApiMock.Setup(x => x.UploadResultAsync(It.IsAny<UploadDiagnosticResultRequest>()))
            .ReturnsAsync(ApiResult<DiagnosticResultDto>.Success(resultDto));

        _imagingApiMock.Setup(x => x.CompleteSessionAsync(301))
            .ReturnsAsync(ApiResult<ImagingSessionDto>.Success(completeSessionDto));

        var controller = new DentistXrayQueueController(_imagingApiMock.Object)
        {
            ControllerContext = CreateContext(10, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.UploadResult(
            diagnosticOrderId: 201,
            sessionId: 301,
            equipmentId: 1,
            findings: "Tien trien tot",
            fileUrl: "/uploads/xray/test.jpg",
            roomId: 9);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(DentistXrayQueueController.Index));
        controller.TempData["SuccessMessage"].Should().NotBeNull();
        _imagingApiMock.Verify(x => x.CompleteSessionAsync(301), Times.Once);
    }

    [Fact]
    public async Task CompleteSession_Success_RedirectsToIndex()
    {
        var sessionDto = new ImagingSessionDto
        {
            ImagingSessionId = 301,
            Status = "Completed"
        };

        _imagingApiMock.Setup(x => x.CompleteSessionAsync(301))
            .ReturnsAsync(ApiResult<ImagingSessionDto>.Success(sessionDto));

        var controller = new DentistXrayQueueController(_imagingApiMock.Object)
        {
            ControllerContext = CreateContext(10, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.CompleteSession(301, roomId: 9);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(DentistXrayQueueController.Index));
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }
}
