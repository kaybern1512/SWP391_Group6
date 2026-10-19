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
    public async Task ReceptionistFollowUpController_Index_ReturnsViewWithFilteredFollowUps()
    {
        var followUps = new List<ReceptionistFollowUpDto>
        {
            new()
            {
                FollowUpId = 1,
                PatientName = "Nguyễn Văn A",
                PatientCode = "PAT-001",
                PhoneNumber = "0987654321",
                RecommendedDate = DateTime.UtcNow.Date,
                Status = "Pending"
            },
            new()
            {
                FollowUpId = 2,
                PatientName = "Trần Thị B",
                PatientCode = "PAT-002",
                PhoneNumber = "0912345678",
                RecommendedDate = DateTime.UtcNow.Date.AddDays(-2),
                Status = "Pending"
            }
        };

        _postCareApiMock.Setup(x => x.GetReceptionistFollowUpsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()))
            .ReturnsAsync(ApiResult<List<ReceptionistFollowUpDto>>.Success(followUps));

        var controller = new DentalClinic.Web.Controllers.Receptionist.ReceptionistFollowUpController(_postCareApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(5, "Receptionist", "Lễ Tân"),
            TempData = CreateTempData()
        };

        // Test search by patient name
        var result = await controller.Index(null, null, null, "all", "Nguyễn");

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ReceptionistFollowUpIndexViewModel>().Subject;
        model.FollowUps.Should().HaveCount(1);
        model.FollowUps[0].PatientName.Should().Be("Nguyễn Văn A");
    }
}
