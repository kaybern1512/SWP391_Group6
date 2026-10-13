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
    private readonly Mock<IAppointmentApiService> _appointmentApiMock = new();
    private readonly Mock<ITreatmentApiService> _treatmentApiMock = new();

    private static ControllerContext CreateContextWithUser(long userId, string role, string name = "Test User")
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
    public async Task DepartmentReferralController_Index_ReturnsViewWithReferralsAndDentists()
    {
        _postCareApiMock.Setup(x => x.GetDepartmentReferralsAsync(null, null))
            .ReturnsAsync(ApiResult<List<DepartmentReferralDto>>.Success(new List<DepartmentReferralDto>
            {
                new() { ReferralId = 1, PatientName = "Bệnh nhân 1", Status = "Pending" }
            }));

        _postCareApiMock.Setup(x => x.GetDepartmentDentistsAsync(null))
            .ReturnsAsync(ApiResult<List<DepartmentDentistLookupDto>>.Success(new List<DepartmentDentistLookupDto>
            {
                new() { DentistUserId = 20, DentistName = "BS. Nam" }
            }));

        var controller = new DepartmentReferralController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(30, "DepartmentManager"),
            TempData = CreateTempData()
        };

        var result = await controller.Index(null);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<DepartmentReferralsViewModel>().Subject;
        vm.Referrals.Should().HaveCount(1);
        vm.Dentists.Should().HaveCount(1);
    }

    [Fact]
    public async Task DepartmentReferralController_Review_WhenSuccess_SetsSuccessMessageAndRedirects()
    {
        _postCareApiMock.Setup(x => x.ReviewReferralAsync(1, It.IsAny<ReviewDepartmentReferralRequest>()))
            .ReturnsAsync(ApiResult<DepartmentReferralDto>.Success(new DepartmentReferralDto { ReferralId = 1, Status = "Assigned" }, "Tiếp nhận thành công"));

        var controller = new DepartmentReferralController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(30, "DepartmentManager"),
            TempData = CreateTempData()
        };

        var result = await controller.Review(1, new ReviewDepartmentReferralRequest { Action = "Assign", AssignedDentistUserId = 20 });

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task DentistPostCareController_Index_ReturnsViewWithReferralsAndSessions()
    {
        _postCareApiMock.Setup(x => x.GetDentistReferralsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentReferralDto>>.Success(new List<DepartmentReferralDto>
            {
                new() { ReferralId = 1, Status = "Pending" }
            }));

        _treatmentApiMock.Setup(x => x.GetDentistTreatmentSessionsAsync(null))
            .ReturnsAsync(ApiResult<List<TreatmentSessionDto>>.Success(new List<TreatmentSessionDto>
            {
                new() { TreatmentSessionId = 50, AppointmentCode = "APT-001" }
            }));

        _appointmentApiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>
            {
                new() { DepartmentId = 2, Name = "Khoa Phẫu Thuật" }
            }));

        var controller = new DentistPostCareController(_postCareApiMock.Object, _appointmentApiMock.Object, _treatmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(20, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<DentistPostCareViewModel>().Subject;
        vm.Referrals.Should().HaveCount(1);
        vm.RecentSessions.Should().HaveCount(1);
        vm.AvailableDepartments.Should().HaveCount(1);
    }

    [Fact]
    public async Task DentistPostCareController_CreateReferral_WhenSuccess_RedirectsWithSuccessMessage()
    {
        _postCareApiMock.Setup(x => x.CreateReferralAsync(It.IsAny<CreateDepartmentReferralRequest>()))
            .ReturnsAsync(ApiResult<DepartmentReferralDto>.Success(new DepartmentReferralDto { ReferralId = 1 }, "Thành công"));

        var controller = new DentistPostCareController(_postCareApiMock.Object, _appointmentApiMock.Object, _treatmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(20, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.CreateReferral(new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 50,
            ToDepartmentId = 2,
            Reason = "Cần nhổ răng"
        });

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task DentistPostCareController_CreateFollowUp_WhenSuccess_RedirectsWithSuccessMessage()
    {
        _postCareApiMock.Setup(x => x.CreateFollowUpScheduleAsync(It.IsAny<CreateFollowUpScheduleRequest>()))
            .ReturnsAsync(ApiResult<FollowUpScheduleDto>.Success(new FollowUpScheduleDto { FollowUpId = 1 }, "Thành công"));

        var controller = new DentistPostCareController(_postCareApiMock.Object, _appointmentApiMock.Object, _treatmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(20, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.CreateFollowUp(new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = 50,
            RecommendedDate = DateTime.UtcNow.AddDays(7)
        });

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task PatientFollowUpController_Index_ReturnsViewWithPatientFollowUps()
    {
        _postCareApiMock.Setup(x => x.GetMyFollowUpsAsync())
            .ReturnsAsync(ApiResult<List<FollowUpScheduleDto>>.Success(new List<FollowUpScheduleDto>
            {
                new() { FollowUpId = 1, DentistName = "BS. Nam", Status = "Pending" }
            }));

        _appointmentApiMock.Setup(x => x.GetPatientAppointmentsAsync(null))
            .ReturnsAsync(ApiResult<List<AppointmentDto>>.Success(new List<AppointmentDto>()));

        var controller = new PatientFollowUpController(_postCareApiMock.Object, _appointmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(10, "Patient"),
            TempData = CreateTempData()
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<PatientFollowUpsViewModel>().Subject;
        vm.FollowUps.Should().HaveCount(1);
    }

    [Fact]
    public async Task PatientFollowUpController_Cancel_WhenSuccess_RedirectsWithSuccessMessage()
    {
        _postCareApiMock.Setup(x => x.UpdateFollowUpStatusAsync(1, It.IsAny<UpdateFollowUpStatusRequest>()))
            .ReturnsAsync(ApiResult<FollowUpScheduleDto>.Success(new FollowUpScheduleDto { FollowUpId = 1, Status = "Cancelled" }));

        var controller = new PatientFollowUpController(_postCareApiMock.Object, _appointmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(10, "Patient"),
            TempData = CreateTempData()
        };

        var result = await controller.Cancel(1);

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task PatientFollowUpController_LinkAppointment_WhenSuccess_RedirectsWithSuccessMessage()
    {
        _postCareApiMock.Setup(x => x.UpdateFollowUpStatusAsync(1, It.Is<UpdateFollowUpStatusRequest>(r => r.Status == "Scheduled" && r.AppointmentId == 501)))
            .ReturnsAsync(ApiResult<FollowUpScheduleDto>.Success(new FollowUpScheduleDto { FollowUpId = 1, Status = "Scheduled", AppointmentId = 501 }));

        var controller = new PatientFollowUpController(_postCareApiMock.Object, _appointmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(10, "Patient"),
            TempData = CreateTempData()
        };

        var result = await controller.LinkAppointment(1, 501);

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task DentistPostCareController_CompleteReferral_WhenSuccess_RedirectsWithSuccessMessage()
    {
        _postCareApiMock.Setup(x => x.ReviewReferralAsync(1, It.Is<ReviewDepartmentReferralRequest>(r => r.Action == "Complete")))
            .ReturnsAsync(ApiResult<DepartmentReferralDto>.Success(new DepartmentReferralDto { ReferralId = 1, Status = "Completed" }));

        var controller = new DentistPostCareController(_postCareApiMock.Object, _appointmentApiMock.Object, _treatmentApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(20, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.CompleteReferral(1, "Hoàn tất điều trị");

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task ReceptionistFollowUpController_Index_ReturnsViewWithFilteredFollowUps()
    {
        _postCareApiMock.Setup(x => x.GetReceptionistFollowUpsAsync("Pending", null, null))
            .ReturnsAsync(ApiResult<List<FollowUpScheduleDto>>.Success(new List<FollowUpScheduleDto>
            {
                new() { FollowUpId = 1, PatientName = "BN A", Status = "Pending" }
            }));

        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(40, "Receptionist"),
            TempData = CreateTempData()
        };

        var result = await controller.Index("Pending", null, null);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<ReceptionistFollowUpsViewModel>().Subject;
        vm.FollowUps.Should().HaveCount(1);
        vm.PendingCount.Should().Be(1);
    }

    [Fact]
    public async Task ReceptionistFollowUpController_UpdateStatus_WhenReceptionistAttemptsCompleted_BlocksWithErrorMessage()
    {
        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(40, "Receptionist"),
            TempData = CreateTempData()
        };

        var result = await controller.UpdateStatus(1, new UpdateFollowUpStatusRequest { Status = "Completed" });

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");
        controller.TempData["ErrorMessage"].Should().NotBeNull();
        controller.TempData["ErrorMessage"]!.ToString().Should().Contain("Lễ tân không có quyền đánh dấu hoàn thành");
    }

    [Fact]
    public async Task ReceptionistFollowUpController_UpdateStatus_WhenLinkingAppointment_Success()
    {
        _postCareApiMock.Setup(x => x.UpdateFollowUpStatusAsync(1, It.Is<UpdateFollowUpStatusRequest>(r => r.Status == "Scheduled" && r.AppointmentId == 501)))
            .ReturnsAsync(ApiResult<FollowUpScheduleDto>.Success(new FollowUpScheduleDto { FollowUpId = 1, Status = "Scheduled", AppointmentId = 501 }));

        var controller = new ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(40, "Receptionist"),
            TempData = CreateTempData()
        };

        var result = await controller.UpdateStatus(1, new UpdateFollowUpStatusRequest { Status = "Scheduled", AppointmentId = 501 });

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }
}
