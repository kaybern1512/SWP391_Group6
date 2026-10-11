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

    public TreatmentWebControllerTests()
    {
        _apiMock = new Mock<ITreatmentApiService>();
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

        var controller = new DentistTreatmentPlanController(_apiMock.Object)
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

        var controller = new DentistTreatmentPlanController(_apiMock.Object)
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
    public async Task DepartmentTreatmentController_Index_ReturnsViewWithSummary()
    {
        _apiMock.Setup(x => x.GetDepartmentTreatmentProgressAsync(null, null))
            .ReturnsAsync(ApiResult<TreatmentProgressSummaryDto>.Success(new TreatmentProgressSummaryDto
            {
                TotalPlans = 3,
                ActivePlans = 2,
                CompletedPlans = 1,
                PendingPhases = 4
            }));
        _apiMock.Setup(x => x.GetDentistOptionsAsync(null))
            .ReturnsAsync(ApiResult<List<TreatmentDentistOptionDto>>.Success(new List<TreatmentDentistOptionDto>()));

        var controller = new DepartmentTreatmentController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(30, "DepartmentManager")
        };

        var result = await controller.Index(null, null);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<DepartmentTreatmentProgressViewModel>().Subject;
        vm.Summary.TotalPlans.Should().Be(3);
        vm.Summary.ActivePlans.Should().Be(2);
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
