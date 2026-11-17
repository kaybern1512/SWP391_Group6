using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Dentist;
using DentalClinic.Web.Controllers.Patient;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Treatment;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class TreatmentWebControllerTests
{
    private readonly Mock<ITreatmentApiService> _apiMock;
    private readonly Mock<IAdminApiService> _adminApiMock;
    private readonly Mock<IAppointmentApiService> _apptApiMock;

    public TreatmentWebControllerTests()
    {
        _apiMock = new Mock<ITreatmentApiService>();
        _adminApiMock = new Mock<IAdminApiService>();
        _apptApiMock = new Mock<IAppointmentApiService>();
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

    private static TempDataDictionary CreateTempData()
    {
        var httpContext = new DefaultHttpContext();
        return new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task DentistTreatmentPlanController_Index_ReturnsViewWithPlans()
    {
        _apiMock.Setup(x => x.GetDentistTreatmentPlansAsync(null, null))
            .ReturnsAsync(ApiResult<List<TreatmentPlanDto>>.Success(new List<TreatmentPlanDto>
            {
                new()
                {
                    TreatmentPlanId = 1,
                    PatientName = "Nguyễn Văn B",
                    Status = "Active",
                    TotalItemsCount = 2,
                    CompletedItemsCount = 1,
                    ProgressPercentage = 50
                }
            }));

        var controller = new DentistTreatmentPlanController(_apiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(20, "Dentist")
        };

        var result = await controller.Index(null, null);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<DentistTreatmentPlanListViewModel>().Subject;
        vm.Plans.Should().HaveCount(1);
        vm.TotalPlans.Should().Be(1);
        vm.ActivePlans.Should().Be(1);
    }

    [Fact]
    public async Task DentistTreatmentPlanController_Create_Post_RedirectsToDetail()
    {
        var request = new CreateTreatmentPlanRequest
        {
            PatientUserId = 10,
            DepartmentId = 1,
            Status = "Active"
        };

        _apiMock.Setup(x => x.CreateTreatmentPlanAsync(request))
            .ReturnsAsync(ApiResult<TreatmentPlanDto>.Success(new TreatmentPlanDto
            {
                TreatmentPlanId = 99,
                PatientUserId = 10,
                Status = "Active"
            }));

        var controller = new DentistTreatmentPlanController(_apiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(20, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.Create(request);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Detail");
        redirect.RouteValues!["id"].Should().Be(99L);
    }

    [Fact]
    public async Task DentistTreatmentPlanController_Create_Post_WithMismatchingPatient_RejectsAndRedirects()
    {
        var request = new CreateTreatmentPlanRequest
        {
            PatientUserId = 999,
            DepartmentId = 1,
            Status = "Active"
        };

        _apptApiMock.Setup(x => x.GetAppointmentDetailAsync(56))
            .ReturnsAsync(ApiResult<AppointmentDto>.Success(new AppointmentDto
            {
                AppointmentId = 56,
                PatientUserId = 10,
                PatientName = "Bệnh nhân A"
            }));

        var controller = new DentistTreatmentPlanController(_apiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateContextWithUser(20, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.Create(request, returnAppointmentId: 56);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Create");
        controller.TempData["ErrorMessage"].Should().NotBeNull();
        _apiMock.Verify(x => x.CreateTreatmentPlanAsync(It.IsAny<CreateTreatmentPlanRequest>()), Times.Never);
    }

    [Fact]
    public async Task PatientTreatmentPlanController_Index_ReturnsViewWithMyPlans()
    {
        _apiMock.Setup(x => x.GetPatientMyPlansAsync())
            .ReturnsAsync(ApiResult<List<TreatmentPlanDto>>.Success(new List<TreatmentPlanDto>
            {
                new()
                {
                    TreatmentPlanId = 5,
                    DepartmentName = "Khoa Chỉnh Nha",
                    Status = "Active",
                    ProgressPercentage = 30
                }
            }));

        var controller = new PatientTreatmentPlanController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(10, "Patient")
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<PatientTreatmentPlanListViewModel>().Subject;
        vm.Plans.Should().HaveCount(1);
        vm.Plans[0].TreatmentPlanId.Should().Be(5);
    }
}
