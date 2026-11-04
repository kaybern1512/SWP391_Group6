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
using DentalClinic.Web.ViewModels.PostCare;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class PostCareWebControllerTests
{
    private readonly Mock<IPostCareApiService> _postCareApiMock = new();
    private readonly Mock<IAppointmentApiService> _apptApiMock = new();
    private readonly Mock<ITreatmentApiService> _treatmentApiMock = new();
    private readonly Mock<IAdminApiService> _adminApiMock = new();

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

    private static TempDataDictionary CreateTempData()
    {
        var httpContext = new DefaultHttpContext();
        return new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task DentistReferralController_Index_ReturnsViewWithData()
    {
        var referrals = new List<DepartmentReferralDto> { new() { ReferralId = 1, PatientName = "Bệnh nhân A" } };
        var followUps = new List<FollowUpScheduleDto> { new() { FollowUpId = 2, PatientName = "Bệnh nhân A" } };
        var depts = new List<DepartmentDto> { new() { DepartmentId = 1, Name = "Khoa Răng" } };
        var sessions = new List<TreatmentSessionDto> { new() { TreatmentSessionId = 10, PatientName = "Bệnh nhân A" } };

        _postCareApiMock.Setup(x => x.GetDentistReferralsAsync()).ReturnsAsync(ApiResult<List<DepartmentReferralDto>>.Success(referrals));
        _postCareApiMock.Setup(x => x.GetDentistFollowUpsAsync()).ReturnsAsync(ApiResult<List<FollowUpScheduleDto>>.Success(followUps));
        _apptApiMock.Setup(x => x.GetDepartmentsAsync()).ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(depts));
        _treatmentApiMock.Setup(x => x.GetDentistTreatmentSessionsAsync(null)).ReturnsAsync(ApiResult<List<TreatmentSessionDto>>.Success(sessions));

        var controller = new DentistReferralController(_postCareApiMock.Object, _apptApiMock.Object, _treatmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(20, "Dentist", "BS Lê Hoàng"),
            TempData = CreateTempData()
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DentistReferralIndexViewModel>().Subject;
        model.Referrals.Should().HaveCount(1);
        model.FollowUps.Should().HaveCount(1);
        model.Departments.Should().HaveCount(1);
    }

    [Fact]
    public async Task DentistReferralController_CreateReferral_Success_Redirects()
    {
        var req = new CreateReferralRequest { TreatmentSessionId = 10, ToDepartmentId = 2, Reason = "Cần chỉnh nha" };
        var dto = new DepartmentReferralDto { ReferralId = 1, Reason = "Cần chỉnh nha" };

        _postCareApiMock.Setup(x => x.CreateReferralAsync(req)).ReturnsAsync(ApiResult<DepartmentReferralDto>.Success(dto));

        var controller = new DentistReferralController(_postCareApiMock.Object, _apptApiMock.Object, _treatmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(20, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.Create(req);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task DentistReferralController_CreateFollowUp_Success_Redirects()
    {
        var req = new CreateFollowUpRequest { TreatmentSessionId = 10, RecommendedDate = DateTime.Today.AddDays(7), Reason = "Tái khám kiểm tra vết thương" };
        var dto = new FollowUpScheduleDto { FollowUpId = 1, RecommendedDate = DateTime.Today.AddDays(7) };

        _postCareApiMock.Setup(x => x.CreateFollowUpAsync(req)).ReturnsAsync(ApiResult<FollowUpScheduleDto>.Success(dto));

        var controller = new DentistReferralController(_postCareApiMock.Object, _apptApiMock.Object, _treatmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(20, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.CreateFollowUp(req);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task DepartmentReferralController_Index_ReturnsViewWithData()
    {
        var referrals = new List<DepartmentReferralDto> { new() { ReferralId = 1, Status = "Pending" } };
        var depts = new List<AdminDepartmentDto> { new() { DepartmentId = 3, Name = "Khoa Chỉnh Nha", ManagerUserId = 102 } };
        var dentists = new List<TreatmentDentistOptionDto> { new() { DentistUserId = 30, FullName = "BS C" } };

        _postCareApiMock.Setup(x => x.GetDepartmentReferralsAsync(null)).ReturnsAsync(ApiResult<List<DepartmentReferralDto>>.Success(referrals));
        _adminApiMock.Setup(x => x.GetDepartmentsAsync()).ReturnsAsync(ApiResult<List<AdminDepartmentDto>>.Success(depts));
        _treatmentApiMock.Setup(x => x.GetDentistOptionsAsync(3)).ReturnsAsync(ApiResult<List<TreatmentDentistOptionDto>>.Success(dentists));

        var controller = new DepartmentReferralController(_postCareApiMock.Object, _adminApiMock.Object, _treatmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(102, "DepartmentManager", "Trưởng Khoa"),
            TempData = CreateTempData()
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DepartmentReferralIndexViewModel>().Subject;
        model.Referrals.Should().HaveCount(1);
        model.Dentists.Should().HaveCount(1);
    }

    [Fact]
    public async Task DepartmentReferralController_Review_Success_Redirects()
    {
        var req = new ReviewReferralRequest { Status = "Accepted", AssignedDentistUserId = 30 };
        var dto = new DepartmentReferralDto { ReferralId = 1, Status = "Assigned" };

        _postCareApiMock.Setup(x => x.ReviewReferralAsync(1, req)).ReturnsAsync(ApiResult<DepartmentReferralDto>.Success(dto));

        var controller = new DepartmentReferralController(_postCareApiMock.Object, _adminApiMock.Object, _treatmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(102, "DepartmentManager"),
            TempData = CreateTempData()
        };

        var result = await controller.Review(1, req);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task PatientFollowUpController_Index_ReturnsViewWithFollowUps()
    {
        var followUps = new List<FollowUpScheduleDto> { new() { FollowUpId = 5, Reason = "Kiểm tra sau trám" } };

        _postCareApiMock.Setup(x => x.GetPatientFollowUpsAsync()).ReturnsAsync(ApiResult<List<FollowUpScheduleDto>>.Success(followUps));
        _postCareApiMock.Setup(x => x.GetPatientProposalsAsync()).ReturnsAsync(ApiResult<List<FollowUpProposalDto>>.Success(new List<FollowUpProposalDto>()));

        var controller = new PatientFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(10, "Patient", "Nguyễn Văn A"),
            TempData = CreateTempData()
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<PatientFollowUpIndexViewModel>().Subject;
        model.FollowUps.Should().HaveCount(1);
    }

    [Fact]
    public async Task PatientFollowUpController_SubmitProposal_Success_ReturnsOk()
    {
        var req = new PatientFollowUpChangeRequest
        {
            AppointmentId = 55,
            ProposedStart = DateTime.Today.AddDays(2).AddHours(9),
            ProposedEnd = DateTime.Today.AddDays(2).AddHours(9).AddMinutes(30),
            Reason = "Đổi lịch sang sáng"
        };
        var proposalDto = new FollowUpProposalDto { ProposalId = 88, Status = "Pending" };

        _postCareApiMock.Setup(x => x.SubmitPatientProposalAsync(req))
            .ReturnsAsync(ApiResult<FollowUpProposalDto>.Success(proposalDto, "Gửi yêu cầu thành công"));

        var controller = new PatientFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(10, "Patient", "Nguyễn Văn A"),
            TempData = CreateTempData()
        };

        var result = await controller.SubmitProposal(req);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PatientFollowUpController_WithdrawProposal_Success_ReturnsOk()
    {
        _postCareApiMock.Setup(x => x.WithdrawPatientProposalAsync(88))
            .ReturnsAsync(ApiResult<bool>.Success(true, "Rút thành công"));

        var controller = new PatientFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(10, "Patient", "Nguyễn Văn A"),
            TempData = CreateTempData()
        };

        var result = await controller.WithdrawProposal(88);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DentistReferralController_ResolveProposal_Approve_Success()
    {
        var req = new ResolveProposalRequest { ProposalId = 101, IsApproved = true };
        _postCareApiMock.Setup(x => x.ResolveProposalAsync(req))
            .ReturnsAsync(ApiResult<FollowUpProposalDto>.Success(new FollowUpProposalDto { ProposalId = 101, Status = "Approved" }, "Đã duyệt"));

        var controller = new DentistReferralController(_postCareApiMock.Object, _apptApiMock.Object, _treatmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(20, "Dentist", "BS Lê Hoàng"),
            TempData = CreateTempData()
        };

        var result = await controller.ResolveProposal(req);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DentistReferralController_ResolveProposal_RejectWithoutReason_BadRequest()
    {
        var req = new ResolveProposalRequest { ProposalId = 101, IsApproved = false, RejectionReason = "" };

        var controller = new DentistReferralController(_postCareApiMock.Object, _apptApiMock.Object, _treatmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(20, "Dentist", "BS Lê Hoàng"),
            TempData = CreateTempData()
        };

        var result = await controller.ResolveProposal(req);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ReceptionistFollowUpController_Index_ReturnsViewWithStatsAndReminders()
    {
        var today = DateTime.Today;
        var reminders = new List<FollowUpReminderDto>
        {
            new() { FollowUpId = 1, PatientName = "Bệnh nhân A", FollowUpDate = today, CallStatus = "NotCalled" },
            new() { FollowUpId = 2, PatientName = "Bệnh nhân B", FollowUpDate = today.AddDays(1), CallStatus = "Called" }
        };

        _postCareApiMock.Setup(x => x.GetReceptionistRemindersAsync(null, null))
            .ReturnsAsync(ApiResult<List<FollowUpReminderDto>>.Success(reminders));
        _postCareApiMock.Setup(x => x.GetReceptionistRemindersAsync("today", "NotCalled"))
            .ReturnsAsync(ApiResult<List<FollowUpReminderDto>>.Success(new List<FollowUpReminderDto> { reminders[0] }));

        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Receptionist", "Lễ Tân Hoa"),
            TempData = CreateTempData()
        };

        var result = await controller.Index("today", "NotCalled");

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ReceptionistFollowUpIndexViewModel>().Subject;
        model.Reminders.Should().HaveCount(1);
        model.TodayCount.Should().Be(1);
        model.TomorrowCount.Should().Be(1);
        model.NotCalledCount.Should().Be(1);
        model.CalledCount.Should().Be(1);
    }

    [Fact]
    public async Task ReceptionistFollowUpController_UpdateCallStatus_Success_ReturnsOk()
    {
        var req = new UpdateReminderCallStatusRequest
        {
            FollowUpId = 1,
            CallStatus = "Called",
            Note = "Đã gọi xác nhận"
        };

        _postCareApiMock.Setup(x => x.UpdateReminderCallStatusAsync(req))
            .ReturnsAsync(ApiResult<FollowUpReminderDto>.Success(new FollowUpReminderDto { FollowUpId = 1, CallStatus = "Called" }, "Cập nhật thành công"));

        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Receptionist", "Lễ Tân Hoa"),
            TempData = CreateTempData()
        };

        var result = await controller.UpdateCallStatus(req);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ReceptionistFollowUpController_UpdateCallStatus_Invalid_ReturnsBadRequest()
    {
        var req = new UpdateReminderCallStatusRequest
        {
            FollowUpId = 0,
            CallStatus = ""
        };

        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Receptionist", "Lễ Tân Hoa"),
            TempData = CreateTempData()
        };

        var result = await controller.UpdateCallStatus(req);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
    [Fact]
    public async Task ReceptionistFollowUpController_Confirm_WhenValid_CallsApiAndRedirectsWithSuccess()
    {
        _postCareApiMock.Setup(x => x.UpdateFollowUpStatusAsync(10, It.Is<UpdateFollowUpStatusRequest>(r => r.Status == "Confirmed")))
            .ReturnsAsync(ApiResult<FollowUpScheduleDto>.Success(new FollowUpScheduleDto { FollowUpId = 10, Status = "Confirmed" }));

        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Receptionist", "Lễ Tân Hoa"),
            TempData = CreateTempData()
        };

        var result = await controller.Confirm(10, "due", "today", "NotCalled");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ReceptionistFollowUpController.Index));
        controller.TempData["SuccessMessage"].Should().NotBeNull();
        _postCareApiMock.Verify(x => x.UpdateFollowUpStatusAsync(10, It.IsAny<UpdateFollowUpStatusRequest>()), Times.Once);
    }

    [Fact]
    public async Task ReceptionistFollowUpController_Confirm_WhenApiFails_SetsErrorMessageAndRedirects()
    {
        _postCareApiMock.Setup(x => x.UpdateFollowUpStatusAsync(10, It.IsAny<UpdateFollowUpStatusRequest>()))
            .ReturnsAsync(ApiResult<FollowUpScheduleDto>.Failure("Lỗi cập nhật"));

        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Receptionist", "Lễ Tân Hoa"),
            TempData = CreateTempData()
        };

        var result = await controller.Confirm(10);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ReceptionistFollowUpController.Index));
        controller.TempData["ErrorMessage"].Should().Be("Lỗi cập nhật");
    }

    [Fact]
    public async Task ReceptionistFollowUpController_Confirm_WhenInvalidId_SetsErrorMessageWithoutApiCall()
    {
        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Receptionist", "Lễ Tân Hoa"),
            TempData = CreateTempData()
        };

        var result = await controller.Confirm(0);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ReceptionistFollowUpController.Index));
        controller.TempData["ErrorMessage"].Should().Be("Mã lịch tái khám không hợp lệ.");
        _postCareApiMock.Verify(x => x.UpdateFollowUpStatusAsync(It.IsAny<long>(), It.IsAny<UpdateFollowUpStatusRequest>()), Times.Never);
    }

    [Fact]
    public async Task ReceptionistFollowUpController_Complete_WhenValid_CallsApiAndRedirectsWithSuccess()
    {
        _postCareApiMock.Setup(x => x.UpdateFollowUpStatusAsync(20, It.Is<UpdateFollowUpStatusRequest>(r => r.Status == "Completed")))
            .ReturnsAsync(ApiResult<FollowUpScheduleDto>.Success(new FollowUpScheduleDto { FollowUpId = 20, Status = "Completed" }));

        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Receptionist", "Lễ Tân Hoa"),
            TempData = CreateTempData()
        };

        var result = await controller.Complete(20);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ReceptionistFollowUpController.Index));
        controller.TempData["SuccessMessage"].Should().NotBeNull();
        _postCareApiMock.Verify(x => x.UpdateFollowUpStatusAsync(20, It.IsAny<UpdateFollowUpStatusRequest>()), Times.Once);
    }

    [Fact]
    public async Task ReceptionistFollowUpController_Complete_WhenInvalidId_SetsErrorMessageWithoutApiCall()
    {
        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Receptionist", "Lễ Tân Hoa"),
            TempData = CreateTempData()
        };

        var result = await controller.Complete(-1);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ReceptionistFollowUpController.Index));
        controller.TempData["ErrorMessage"].Should().Be("Mã lịch tái khám không hợp lệ.");
        _postCareApiMock.Verify(x => x.UpdateFollowUpStatusAsync(It.IsAny<long>(), It.IsAny<UpdateFollowUpStatusRequest>()), Times.Never);
    }

    [Fact]
    public async Task ReceptionistFollowUpController_Reschedule_WhenValid_CallsApiAndRedirectsWithSuccess()
    {
        var futureDate = DateTime.Today.AddDays(5);
        _postCareApiMock.Setup(x => x.UpdateFollowUpStatusAsync(30, It.Is<UpdateFollowUpStatusRequest>(r => r.Status == "Pending" && r.NewRecommendedDate == futureDate)))
            .ReturnsAsync(ApiResult<FollowUpScheduleDto>.Success(new FollowUpScheduleDto { FollowUpId = 30, RecommendedDate = futureDate }));

        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Receptionist", "Lễ Tân Hoa"),
            TempData = CreateTempData()
        };

        var result = await controller.Reschedule(30, futureDate, "Bệnh nhân bận công tác");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ReceptionistFollowUpController.Index));
        controller.TempData["SuccessMessage"].Should().NotBeNull();
        _postCareApiMock.Verify(x => x.UpdateFollowUpStatusAsync(30, It.IsAny<UpdateFollowUpStatusRequest>()), Times.Once);
    }

    [Fact]
    public async Task ReceptionistFollowUpController_Reschedule_WhenDateNotFuture_SetsErrorMessageWithoutApiCall()
    {
        var pastDate = DateTime.Today;

        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Receptionist", "Lễ Tân Hoa"),
            TempData = CreateTempData()
        };

        var result = await controller.Reschedule(30, pastDate, "Dời gấp hôm nay");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ReceptionistFollowUpController.Index));
        controller.TempData["ErrorMessage"].Should().Be("Ngày dời hẹn phải từ ngày mai trở đi.");
        _postCareApiMock.Verify(x => x.UpdateFollowUpStatusAsync(It.IsAny<long>(), It.IsAny<UpdateFollowUpStatusRequest>()), Times.Never);
    }

    [Fact]
    public async Task ReceptionistFollowUpController_Reschedule_WhenInvalidId_SetsErrorMessageWithoutApiCall()
    {
        var futureDate = DateTime.Today.AddDays(5);

        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Receptionist", "Lễ Tân Hoa"),
            TempData = CreateTempData()
        };

        var result = await controller.Reschedule(0, futureDate, "Lý do");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ReceptionistFollowUpController.Index));
        controller.TempData["ErrorMessage"].Should().Be("Mã lịch tái khám không hợp lệ.");
        _postCareApiMock.Verify(x => x.UpdateFollowUpStatusAsync(It.IsAny<long>(), It.IsAny<UpdateFollowUpStatusRequest>()), Times.Never);
    }

    [Fact]
    public void Views_Receptionist_FollowUps_Index_ContainsRestoredActionsAndSafeDataAttributes()
    {
        var viewPath = Path.Combine(AppContext.BaseDirectory, "../../../../DentalClinic.Web/Views/Receptionist/FollowUps/Index.cshtml");
        File.Exists(viewPath).Should().BeTrue();

        var content = File.ReadAllText(viewPath);

        content.Should().Contain("asp-action=\"Confirm\"");
        content.Should().Contain("asp-action=\"Complete\"");
        content.Should().Contain("modalRescheduleFollowUp");

        content.Should().Contain("btn-open-call-modal");
        content.Should().Contain("data-followup-id=");
        content.Should().Contain("data-patient-name=");
        content.Should().NotContain("onclick=\"openCallModal(");
        content.Should().NotContain("onclick=\"openRescheduleModal(");
    }

    [Fact]
    public void Views_Patient_FollowUps_Index_ContainsSafeDataAttributesAndNoUnsafeInlineScript()
    {
        var viewPath = Path.Combine(AppContext.BaseDirectory, "../../../../DentalClinic.Web/Views/Patient/FollowUps/Index.cshtml");
        File.Exists(viewPath).Should().BeTrue();

        var content = File.ReadAllText(viewPath);

        content.Should().Contain("btn-open-proposal-modal");
        content.Should().Contain("data-appointment-id=");
        content.Should().Contain("data-dentist-name=");
        content.Should().NotContain("onclick=\"openProposalModal(");
    }

    [Fact]
    public void Views_Dentist_Referrals_Index_ContainsSafeDataAttributesAndNoUnsafeInlineScript()
    {
        var viewPath = Path.Combine(AppContext.BaseDirectory, "../../../../DentalClinic.Web/Views/Dentist/Referrals/Index.cshtml");
        File.Exists(viewPath).Should().BeTrue();

        var content = File.ReadAllText(viewPath);

        content.Should().Contain("btn-open-approve-proposal");
        content.Should().Contain("btn-open-reject-proposal");
        content.Should().Contain("data-proposal-id=");
        content.Should().Contain("data-patient-name=");
        content.Should().NotContain("onclick=\"openApproveModal(");
        content.Should().NotContain("onclick=\"openRejectModal(");
    }
}