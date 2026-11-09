using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers;
using DentalClinic.Web.Controllers.Admin;
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

public class WorkforceAndAttendanceWebControllerTests
{
    private readonly Mock<IAdminApiService> _adminApiMock;
    private readonly Mock<IAttendanceApiService> _attendanceApiMock;

    public WorkforceAndAttendanceWebControllerTests()
    {
        _adminApiMock = new Mock<IAdminApiService>();
        _attendanceApiMock = new Mock<IAttendanceApiService>();
    }

    private static ControllerContext CreateUserContext(long userId, string name, string role)
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Email, $"{role.ToLower()}@clinic.com")
        };
        var identity = new ClaimsIdentity(claims, "CookieAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        return new ControllerContext { HttpContext = httpContext };
    }

    private static TempDataDictionary CreateTempData()
    {
        return new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
    }

    #region AdminWorkforceController Tests

    [Fact]
    public async Task AdminWorkforceController_Report_ReturnsViewWithSummary()
    {
        var summary = new WorkforceMonthlySummaryDto
        {
            Year = 2026,
            Month = 11,
            TotalActiveStaff = 5,
            TotalClinicWorkedHours = 120.5m
        };

        _adminApiMock.Setup(x => x.GetMonthlyWorkforceReportAsync(2026, 11, null))
            .ReturnsAsync(ApiResult<WorkforceMonthlySummaryDto>.Success(summary));

        _attendanceApiMock.Setup(x => x.GetPendingCorrectionsAsync())
            .ReturnsAsync(ApiResult<List<AttendanceCorrectionRequestDto>>.Success(new List<AttendanceCorrectionRequestDto>()));

        var controller = new AdminWorkforceController(_adminApiMock.Object, _attendanceApiMock.Object)
        {
            ControllerContext = CreateUserContext(1, "Admin", "SystemAdministrator"),
            TempData = CreateTempData()
        };

        var result = await controller.Report(2026, 11);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<WorkforceMonthlySummaryDto>().Subject;
        model.TotalActiveStaff.Should().Be(5);
        model.TotalClinicWorkedHours.Should().Be(120.5m);
    }

    [Fact]
    public async Task AdminWorkforceController_FinalizePeriod_RedirectsWithSuccessMessage()
    {
        _adminApiMock.Setup(x => x.FinalizeWorkforcePeriodAsync(2026, 11))
            .ReturnsAsync(ApiResult<AttendancePeriodClosureDto>.Success(new AttendancePeriodClosureDto(), "Đã chốt sổ kỳ công"));

        var controller = new AdminWorkforceController(_adminApiMock.Object, _attendanceApiMock.Object)
        {
            ControllerContext = CreateUserContext(1, "Admin", "SystemAdministrator"),
            TempData = CreateTempData()
        };

        var result = await controller.FinalizePeriod(2026, 11);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminWorkforceController.Report));
        controller.TempData["SuccessMessage"].Should().Be("Đã chốt sổ kỳ công");
    }

    [Fact]
    public async Task AdminWorkforceController_ReopenPeriod_RequiresReason_Redirects()
    {
        _adminApiMock.Setup(x => x.ReopenWorkforcePeriodAsync(2026, 11, "Lý do bổ sung"))
            .ReturnsAsync(ApiResult<AttendancePeriodClosureDto>.Success(new AttendancePeriodClosureDto(), "Đã mở lại kỳ công"));

        var controller = new AdminWorkforceController(_adminApiMock.Object, _attendanceApiMock.Object)
        {
            ControllerContext = CreateUserContext(1, "Admin", "SystemAdministrator"),
            TempData = CreateTempData()
        };

        var result = await controller.ReopenPeriod(2026, 11, "Lý do bổ sung");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminWorkforceController.Report));
        controller.TempData["SuccessMessage"].Should().Be("Đã mở lại kỳ công");
    }

    [Fact]
    public async Task AdminWorkforceController_ReviewCorrection_ApprovesAndRedirects()
    {
        _attendanceApiMock.Setup(x => x.ReviewCorrectionAsync(55, true, "Đã chấp thuận"))
            .ReturnsAsync(ApiResult<AttendanceCorrectionRequestDto>.Success(new AttendanceCorrectionRequestDto(), "Đã duyệt điều chỉnh."));

        var controller = new AdminWorkforceController(_adminApiMock.Object, _attendanceApiMock.Object)
        {
            ControllerContext = CreateUserContext(1, "Admin", "SystemAdministrator"),
            TempData = CreateTempData()
        };

        var result = await controller.ReviewCorrection(55, true, "Đã chấp thuận", 2026, 11);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminWorkforceController.Report));
        controller.TempData["SuccessMessage"].Should().Be("Đã duyệt điều chỉnh.");
    }

    #endregion

    #region AttendanceWebController Tests

    [Fact]
    public async Task AttendanceWebController_Index_ReturnsViewWithTodayShifts()
    {
        var shifts = new List<TodayShiftAttendanceDto>
        {
            new() { Shift = new StaffShiftDto { ShiftIdentifier = "SHIFT_1", ShiftName = "Ca Sáng" }, CanClockIn = true }
        };

        _attendanceApiMock.Setup(x => x.GetTodayAttendanceAsync())
            .ReturnsAsync(ApiResult<List<TodayShiftAttendanceDto>>.Success(shifts));
        _attendanceApiMock.Setup(x => x.GetAttendanceHistoryAsync(null, null))
            .ReturnsAsync(ApiResult<List<StaffAttendanceRecordDto>>.Success(new List<StaffAttendanceRecordDto>()));
        _attendanceApiMock.Setup(x => x.GetMyCorrectionsAsync())
            .ReturnsAsync(ApiResult<List<AttendanceCorrectionRequestDto>>.Success(new List<AttendanceCorrectionRequestDto>()));

        var controller = new AttendanceWebController(_attendanceApiMock.Object)
        {
            ControllerContext = CreateUserContext(10, "Bác sĩ A", "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.Index();

        result.Should().BeOfType<ViewResult>();
        var resultShifts = (List<TodayShiftAttendanceDto>)controller.ViewBag.TodayShifts;
        resultShifts.Should().NotBeNull();
    }

    [Fact]
    public async Task AttendanceWebController_ClockIn_Valid_RedirectsWithSuccess()
    {
        _attendanceApiMock.Setup(x => x.ClockInAsync(It.IsAny<ClockInRequest>()))
            .ReturnsAsync(ApiResult<StaffAttendanceRecordDto>.Success(new StaffAttendanceRecordDto(), "Chấm công vào thành công!"));

        var controller = new AttendanceWebController(_attendanceApiMock.Object)
        {
            ControllerContext = CreateUserContext(10, "Bác sĩ A", "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.ClockIn("SHIFT_10", "Đúng giờ");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AttendanceWebController.Index));
        controller.TempData["SuccessMessage"].Should().Be("Chấm công vào thành công!");
    }

    [Fact]
    public async Task AttendanceWebController_ClockOut_Valid_RedirectsWithSuccess()
    {
        _attendanceApiMock.Setup(x => x.ClockOutAsync(It.IsAny<ClockOutRequest>()))
            .ReturnsAsync(ApiResult<StaffAttendanceRecordDto>.Success(new StaffAttendanceRecordDto(), "Chấm công ra thành công!"));

        var controller = new AttendanceWebController(_attendanceApiMock.Object)
        {
            ControllerContext = CreateUserContext(20, "Tiếp tân B", "Receptionist"),
            TempData = CreateTempData()
        };

        var result = await controller.ClockOut(123, "SHIFT_20", "Kết thúc ca");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AttendanceWebController.Index));
        controller.TempData["SuccessMessage"].Should().Be("Chấm công ra thành công!");
    }

    [Fact]
    public async Task AttendanceWebController_SubmitCorrection_Valid_RedirectsWithSuccess()
    {
        _attendanceApiMock.Setup(x => x.SubmitCorrectionRequestAsync(It.IsAny<SubmitAttendanceCorrectionRequest>()))
            .ReturnsAsync(ApiResult<AttendanceCorrectionRequestDto>.Success(new AttendanceCorrectionRequestDto(), "Đã gửi yêu cầu điều chỉnh"));

        var controller = new AttendanceWebController(_attendanceApiMock.Object)
        {
            ControllerContext = CreateUserContext(20, "Tiếp tân B", "Receptionist"),
            TempData = CreateTempData()
        };

        var req = new SubmitAttendanceCorrectionRequest
        {
            AttendanceId = 123,
            Reason = "Quên chấm công ra",
            Explanation = "Bận việc cuối giờ"
        };

        var result = await controller.SubmitCorrection(req);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AttendanceWebController.Index));
        controller.TempData["SuccessMessage"].Should().Be("Đã gửi yêu cầu điều chỉnh");
    }

    #endregion
}
