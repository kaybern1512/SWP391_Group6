using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Dentist;
using DentalClinic.Web.Controllers.Patient;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Prescription;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class PrescriptionWebControllerTests
{
    private readonly Mock<IPrescriptionApiService> _apiMock;

    public PrescriptionWebControllerTests()
    {
        _apiMock = new Mock<IPrescriptionApiService>();
    }

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
    public async Task DentistPrescriptionController_Index_ReturnsViewWithPrescriptionsAndEligibleAppointments()
    {
        var prescriptions = new List<PrescriptionDto>
        {
            new() { PrescriptionId = 1, PatientName = "Nguyễn Văn A" }
        };
        var eligibleAppts = new List<PrescriptionEligibleAppointmentDto>
        {
            new() { AppointmentId = 10, PatientName = "Nguyễn Văn A" }
        };

        _apiMock.Setup(x => x.GetDentistPrescriptionsAsync(null))
            .ReturnsAsync(ApiResult<List<PrescriptionDto>>.Success(prescriptions));
        _apiMock.Setup(x => x.GetEligibleAppointmentsAsync())
            .ReturnsAsync(ApiResult<List<PrescriptionEligibleAppointmentDto>>.Success(eligibleAppts));

        var controller = new DentistPrescriptionController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist")
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<DentistPrescriptionIndexViewModel>().Subject;
        vm.Prescriptions.Should().HaveCount(1);
        vm.EligibleAppointments.Should().HaveCount(1);
    }

    [Fact]
    public async Task DentistPrescriptionController_Create_WhenValid_RedirectsToIndex()
    {
        _apiMock.Setup(x => x.CreatePrescriptionAsync(It.IsAny<CreatePrescriptionRequest>()))
            .ReturnsAsync(ApiResult<PrescriptionDto>.Success(new PrescriptionDto { PrescriptionId = 5 }));

        var controller = new DentistPrescriptionController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist"),
            TempData = CreateTempData()
        };

        var form = new CreatePrescriptionFormModel
        {
            AppointmentId = 10,
            GeneralInstructions = "Uống sau ăn",
            ItemsJson = "[{\"MedicationName\":\"Paracetamol\",\"Dosage\":\"500mg\"}]"
        };

        var result = await controller.Create(form);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task PatientPrescriptionController_Index_ReturnsViewWithPatientPrescriptions()
    {
        var prescriptions = new List<PrescriptionDto>
        {
            new() { PrescriptionId = 2, PatientName = "Trần Thị B" }
        };

        _apiMock.Setup(x => x.GetMyPrescriptionsAsync())
            .ReturnsAsync(ApiResult<List<PrescriptionDto>>.Success(prescriptions));

        var controller = new PatientPrescriptionController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(5, "Patient", "Trần Thị B")
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<PatientPrescriptionIndexViewModel>().Subject;
        vm.Prescriptions.Should().HaveCount(1);
    }
}
