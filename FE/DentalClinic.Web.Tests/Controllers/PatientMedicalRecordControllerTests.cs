using System.Security.Claims;
using DentalClinic.Web.Controllers.Patient;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.ClinicalCore;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class PatientMedicalRecordControllerTests
{
    [Fact]
    public async Task Index_RequestsRecordForAuthenticatedPatientOnly()
    {
        var api = new Mock<IClinicalCoreApiClient>();
        api.Setup(client => client.GetMedicalRecordAsync(42))
            .ReturnsAsync(ApiResult<DentalMedicalRecordDto>.Success(new DentalMedicalRecordDto
            {
                PatientUserId = 42,
                PatientName = "Patient"
            }));
        var controller = CreateController(api.Object, new Claim(ClaimTypes.NameIdentifier, "42"));

        var result = await controller.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<PatientMedicalRecordViewModel>().Subject;
        model.MedicalRecord!.PatientUserId.Should().Be(42);
        api.Verify(client => client.GetMedicalRecordAsync(42), Times.Once);
        api.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Index_WithoutUsableUserId_DoesNotCallApi()
    {
        var api = new Mock<IClinicalCoreApiClient>();
        var controller = CreateController(api.Object, new Claim(ClaimTypes.NameIdentifier, "not-an-id"));

        var result = await controller.Index();

        result.Should().BeOfType<ForbidResult>();
        api.Verify(client => client.GetMedicalRecordAsync(It.IsAny<long>()), Times.Never);
    }

    private static PatientMedicalRecordController CreateController(
        IClinicalCoreApiClient api,
        Claim claim)
    {
        var controller = new PatientMedicalRecordController(api);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { claim }, "test"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }
}
