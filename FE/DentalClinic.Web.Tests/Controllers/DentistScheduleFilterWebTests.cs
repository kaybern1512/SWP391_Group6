using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Dentist;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Appointments;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class DentistScheduleFilterWebTests
{
    private readonly Mock<IAppointmentApiService> _mockApi;

    public DentistScheduleFilterWebTests()
    {
        _mockApi = new Mock<IAppointmentApiService>();
    }

    private static ControllerContext CreateContextWithUser(long userId, string role, string name = "Dentist User")
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
    public async Task Today_DefaultInvocation_LoadsTodayAppointmentsAndAvailableRooms()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var appointments = new List<AppointmentDto>
        {
            new() { AppointmentId = 1, AppointmentCode = "APT-001", Status = "Confirmed", ScheduledStart = DateTime.Today.AddHours(9) },
            new() { AppointmentId = 2, AppointmentCode = "APT-002", Status = "CheckedIn", ScheduledStart = DateTime.Today.AddHours(10) }
        };
        var rooms = new List<RoomOptionDto>
        {
            new() { RoomId = 1, RoomCode = "R101", RoomName = "Phòng Khám 101" }
        };

        _mockApi.Setup(x => x.GetDentistTodayAppointmentsAsync())
            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Success(appointments));
        _mockApi.Setup(x => x.GetDentistRoomsAsync())
            .ReturnsAsync(ApiResult<List<RoomOptionDto>>.Success(rooms));

        var controller = new DentistAppointmentController(_mockApi.Object)
        {
            ControllerContext = CreateContextWithUser(501, "Dentist")
        };

        // Act
        var result = await controller.Today();

        // Assert
        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<DentistTodayAppointmentsViewModel>().Subject;
        vm.FromDate.Should().Be(today);
        vm.ToDate.Should().Be(today);
        vm.DatePreset.Should().Be("today");
        vm.Appointments.Should().HaveCount(2);
        vm.AvailableRooms.Should().HaveCount(1);
        vm.TotalCount.Should().Be(2);
        vm.ConfirmedCount.Should().Be(1);
        vm.CheckedInCount.Should().Be(1);
        vm.ValidationError.Should().BeNull();
    }

    [Fact]
    public async Task Today_WithDatePresetTomorrow_ComputesTomorrowRange()
    {
        // Arrange
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        _mockApi.Setup(x => x.GetDentistAppointmentsAsync(
                tomorrow,
                tomorrow,
                null,
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Success(new List<AppointmentDto>
            {
                new() { AppointmentId = 10, AppointmentCode = "APT-TOMORROW", Status = "Confirmed", ScheduledStart = DateTime.Today.AddDays(1).AddHours(8) }
            }));
        _mockApi.Setup(x => x.GetDentistRoomsAsync())
            .ReturnsAsync(ApiResult<List<RoomOptionDto>>.Success(new List<RoomOptionDto>()));

        var controller = new DentistAppointmentController(_mockApi.Object)
        {
            ControllerContext = CreateContextWithUser(501, "Dentist")
        };

        // Act
        var result = await controller.Today(datePreset: "tomorrow");

        // Assert
        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<DentistTodayAppointmentsViewModel>().Subject;
        vm.FromDate.Should().Be(tomorrow);
        vm.ToDate.Should().Be(tomorrow);
        vm.DatePreset.Should().Be("tomorrow");
        vm.Appointments.Should().HaveCount(1);
        vm.Appointments[0].AppointmentCode.Should().Be("APT-TOMORROW");
    }

    [Fact]
    public async Task Today_WithDatePresetThisWeek_ComputesMondayToSundayRange()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var monday = today.AddDays(-diff);
        var sunday = monday.AddDays(6);

        _mockApi.Setup(x => x.GetDentistAppointmentsAsync(
                monday,
                sunday,
                null,
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Success(new List<AppointmentDto>()));
        _mockApi.Setup(x => x.GetDentistRoomsAsync())
            .ReturnsAsync(ApiResult<List<RoomOptionDto>>.Success(new List<RoomOptionDto>()));

        var controller = new DentistAppointmentController(_mockApi.Object)
        {
            ControllerContext = CreateContextWithUser(501, "Dentist")
        };

        // Act
        var result = await controller.Today(datePreset: "this_week");

        // Assert
        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<DentistTodayAppointmentsViewModel>().Subject;
        vm.FromDate.Should().Be(monday);
        vm.ToDate.Should().Be(sunday);
        vm.DatePreset.Should().Be("this_week");
        vm.IsMultiDay.Should().BeTrue();
    }

    [Fact]
    public async Task Today_WithInvalidDateRange_FromDateAfterToDate_SetsValidationError()
    {
        // Arrange: fromDate is after toDate
        var fromDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var toDate = DateOnly.FromDateTime(DateTime.Today);

        _mockApi.Setup(x => x.GetDentistAppointmentsAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<TimeOnly?>(),
                It.IsAny<TimeOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<long?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Success(new List<AppointmentDto>()));
        _mockApi.Setup(x => x.GetDentistRoomsAsync())
            .ReturnsAsync(ApiResult<List<RoomOptionDto>>.Success(new List<RoomOptionDto>()));

        var controller = new DentistAppointmentController(_mockApi.Object)
        {
            ControllerContext = CreateContextWithUser(501, "Dentist")
        };

        // Act
        var result = await controller.Today(datePreset: "custom_range", fromDate: fromDate.ToString("yyyy-MM-dd"), toDate: toDate.ToString("yyyy-MM-dd"));

        // Assert
        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<DentistTodayAppointmentsViewModel>().Subject;
        vm.ValidationError.Should().NotBeNullOrEmpty();
        vm.ValidationError.Should().Contain("không được lớn hơn");
        // Preserves user's chosen dates so they can see and fix the error
        vm.FromDate.Should().Be(fromDate);
        vm.ToDate.Should().Be(toDate);
        vm.Appointments.Should().BeEmpty();
    }

    [Fact]
    public async Task Today_WithInvalidTimeRange_FromTimeAfterToTime_SetsValidationError()
    {
        // Arrange: FromTime (14:00) >= ToTime (09:00)
        var today = DateOnly.FromDateTime(DateTime.Today);
        var fromTime = new TimeOnly(14, 0);
        var toTime = new TimeOnly(9, 0);

        _mockApi.Setup(x => x.GetDentistRoomsAsync())
            .ReturnsAsync(ApiResult<List<RoomOptionDto>>.Success(new List<RoomOptionDto>()));

        var controller = new DentistAppointmentController(_mockApi.Object)
        {
            ControllerContext = CreateContextWithUser(501, "Dentist")
        };

        // Act
        var result = await controller.Today(fromTime: fromTime.ToString("HH:mm"), toTime: toTime.ToString("HH:mm"));

        // Assert
        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<DentistTodayAppointmentsViewModel>().Subject;
        vm.ValidationError.Should().NotBeNullOrEmpty();
        vm.ValidationError.Should().Contain("phải nhỏ hơn giờ kết thúc");
        // Preserves user's chosen times so they can see and fix the error
        vm.FromTime.Should().Be(fromTime);
        vm.ToTime.Should().Be(toTime);
        vm.Appointments.Should().BeEmpty();
    }

    [Fact]
    public async Task Today_WithAdvancedFilters_PassesAllParametersToApi()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var fromTime = new TimeOnly(8, 30);
        var toTime = new TimeOnly(12, 0);

        _mockApi.Setup(x => x.GetDentistAppointmentsAsync(
                today,
                today,
                fromTime,
                toTime,
                "InService",
                1L,
                10L,
                "PAT-001"))
            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Success(new List<AppointmentDto>
            {
                new() { AppointmentId = 5, AppointmentCode = "APT-005", Status = "InService", ScheduledStart = DateTime.Today.AddHours(9) }
            }));
        _mockApi.Setup(x => x.GetDentistRoomsAsync())
            .ReturnsAsync(ApiResult<List<RoomOptionDto>>.Success(new List<RoomOptionDto>()));

        var controller = new DentistAppointmentController(_mockApi.Object)
        {
            ControllerContext = CreateContextWithUser(501, "Dentist")
        };

        // Act
        var result = await controller.Today(
            datePreset: "today",
            fromTime: fromTime.ToString("HH:mm"),
            toTime: toTime.ToString("HH:mm"),
            status: "InService",
            roomId: 1L,
            dentalChairId: 10L,
            search: "PAT-001");

        // Assert
        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<DentistTodayAppointmentsViewModel>().Subject;
        vm.Appointments.Should().HaveCount(1);
        vm.Appointments[0].Status.Should().Be("InService");
        vm.InServiceCount.Should().Be(1);
        vm.HasActiveFilters.Should().BeTrue();
    }
}
