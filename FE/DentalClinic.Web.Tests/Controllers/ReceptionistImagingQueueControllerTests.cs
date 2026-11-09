using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Receptionist;
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

public class ReceptionistImagingQueueControllerTests
{
    private readonly Mock<IImagingApiService> _imagingApiMock;

    public ReceptionistImagingQueueControllerTests()
    {
        _imagingApiMock = new Mock<IImagingApiService>();
    }

    private static ControllerContext CreateContext(long userId, string role, string name = "Receptionist")
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
    public async Task Index_ReturnsViewWithQueueAndEquipments()
    {
        var equipments = new List<ImagingEquipmentDto>
        {
            new() { EquipmentId = 1, EquipmentCode = "XRAY-01", RoomId = 100 }
        };
        var queue = new ImagingQueueSummaryDto
        {
            RoomId = 100,
            RoomCode = "XRAY-01",
            CurrentWaitingCount = 1,
            EstimatedNextWaitMinutes = 15
        };

        _imagingApiMock.Setup(s => s.GetAllEquipmentsAsync())
            .ReturnsAsync(ApiResult<List<ImagingEquipmentDto>>.Success(equipments));
        _imagingApiMock.Setup(s => s.GetQueueStatusAsync(100))
            .ReturnsAsync(ApiResult<ImagingQueueSummaryDto>.Success(queue));
        _imagingApiMock.Setup(s => s.GetAllConfigsAsync())
            .ReturnsAsync(ApiResult<List<ImagingServiceConfigDto>>.Success(new List<ImagingServiceConfigDto>()));

        var controller = new ReceptionistImagingQueueController(_imagingApiMock.Object)
        {
            ControllerContext = CreateContext(5, "Receptionist"),
            TempData = CreateTempData()
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.Model.Should().BeEquivalentTo(queue);
    }

    [Fact]
    public async Task CheckIn_WhenSuccess_SetsTempDataAndRedirects()
    {
        var session = new ImagingSessionDto
        {
            ImagingSessionId = 123,
            QueueNumber = "XR-001",
            EstimatedWaitMinutes = 10
        };

        _imagingApiMock.Setup(s => s.CheckInSessionAsync(123))
            .ReturnsAsync(ApiResult<ImagingSessionDto>.Success(session));

        var controller = new ReceptionistImagingQueueController(_imagingApiMock.Object)
        {
            ControllerContext = CreateContext(5, "Receptionist"),
            TempData = CreateTempData()
        };

        var result = await controller.CheckIn(123);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task Start_WhenSuccess_SetsTempDataAndRedirects()
    {
        _imagingApiMock.Setup(s => s.StartSessionAsync(123))
            .ReturnsAsync(ApiResult<ImagingSessionDto>.Success(new ImagingSessionDto { ImagingSessionId = 123, Status = "InProgress" }));

        var controller = new ReceptionistImagingQueueController(_imagingApiMock.Object)
        {
            ControllerContext = CreateContext(5, "Receptionist"),
            TempData = CreateTempData()
        };

        var result = await controller.Start(123);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("bắt đầu");
    }

    [Fact]
    public async Task Complete_WhenSuccess_SetsTempDataAndRedirects()
    {
        _imagingApiMock.Setup(s => s.CompleteSessionAsync(123))
            .ReturnsAsync(ApiResult<ImagingSessionDto>.Success(new ImagingSessionDto { ImagingSessionId = 123, Status = "Completed" }));

        var controller = new ReceptionistImagingQueueController(_imagingApiMock.Object)
        {
            ControllerContext = CreateContext(5, "Receptionist"),
            TempData = CreateTempData()
        };

        var result = await controller.Complete(123);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("hoàn tất");
    }

    [Fact]
    public async Task Cancel_WhenSuccess_SetsTempDataAndRedirects()
    {
        _imagingApiMock.Setup(s => s.CancelSessionAsync(123, "Bệnh nhân yêu cầu hủy"))
            .ReturnsAsync(ApiResult<ImagingSessionDto>.Success(new ImagingSessionDto { ImagingSessionId = 123, Status = "Cancelled" }));

        var controller = new ReceptionistImagingQueueController(_imagingApiMock.Object)
        {
            ControllerContext = CreateContext(5, "Receptionist"),
            TempData = CreateTempData()
        };

        var result = await controller.Cancel(123, "Bệnh nhân yêu cầu hủy");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"]?.ToString().Should().Contain("hủy");
    }
}
