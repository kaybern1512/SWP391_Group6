using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Dentist;
using DentalClinic.Web.Controllers.Patient;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.MedicalRecord;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class MedicalRecordWebControllerTests
{
    private readonly Mock<IMedicalRecordApiService> _apiMock;

    public MedicalRecordWebControllerTests()
    {
        _apiMock = new Mock<IMedicalRecordApiService>();
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
    public async Task DentistMedicalRecordController_Index_ReturnsViewWithPatientRecord()
    {
        var patients = new List<PatientLookupDto>
        {
            new() { PatientUserId = 10, FullName = "Nguyễn Văn A", PatientCode = "BN10" }
        };
        var record = new MedicalRecordDto
        {
            PatientUserId = 10,
            PatientName = "Nguyễn Văn A",
            PatientCode = "BN10",
            CurrentOdontogram = new List<OdontogramEntryDto>
            {
                new() { ToothNumber = "16", Condition = "Caries" }
            }
        };

        _apiMock.Setup(x => x.SearchPatientsAsync(null))
            .ReturnsAsync(ApiResult<List<PatientLookupDto>>.Success(patients));
        _apiMock.Setup(x => x.GetByPatientIdAsync(10))
            .ReturnsAsync(ApiResult<MedicalRecordDto>.Success(record));

        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist")
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<DentistMedicalRecordIndexViewModel>().Subject;
        vm.SelectedPatientUserId.Should().Be(10);
        vm.SelectedRecord.Should().NotBeNull();
        vm.SelectedRecord!.PatientName.Should().Be("Nguyễn Văn A");
    }

    [Fact]
    public async Task DentistMedicalRecordController_CreateClinicalEntry_WhenValid_RedirectsToIndex()
    {
        _apiMock.Setup(x => x.CreateClinicalEntryAsync(It.IsAny<CreateClinicalEntryRequest>()))
            .ReturnsAsync(ApiResult<ClinicalEntryDto>.Success(new ClinicalEntryDto { ClinicalEntryId = 1 }));

        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist"),
            TempData = CreateTempData()
        };

        var form = new CreateClinicalEntryFormModel
        {
            PatientUserId = 10,
            Diagnosis = "Sâu răng 16",
            ToothConditionsJson = "[{\"ToothNumber\":\"16\",\"Condition\":\"Caries\"}]"
        };

        var result = await controller.CreateEntry(form);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task PatientMedicalRecordController_Index_ReturnsViewWithPatientOwnRecord()
    {
        var record = new MedicalRecordDto
        {
            PatientUserId = 50,
            PatientName = "Lê Thị B",
            PatientCode = "BN50"
        };

        _apiMock.Setup(x => x.GetMyMedicalRecordAsync())
            .ReturnsAsync(ApiResult<MedicalRecordDto>.Success(record));

        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Patient", "Lê Thị B")
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<PatientMedicalRecordIndexViewModel>().Subject;
        vm.Record.Should().NotBeNull();
        vm.Record!.PatientName.Should().Be("Lê Thị B");
    }

    [Fact]
    public async Task DentistMedicalRecordController_GetImagingFile_WhenApiSucceeds_ReturnsFileResultWithSecurityHeaders()
    {
        var fakeStream = new System.IO.MemoryStream(new byte[] { 0x1, 0x2, 0x3 });
        _apiMock.Setup(x => x.GetImagingFileStreamAsync(42, false))
            .ReturnsAsync(new ApiFileStreamResult
            {
                Stream = fakeStream,
                ContentType = "image/jpeg",
                FileName = "xray_42.jpg",
                StatusCode = 200
            });

        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist")
        };

        var result = await controller.GetImagingFile(42, false);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("image/jpeg");
        controller.Response.Headers["Cache-Control"].ToString().Should().Contain("no-store");
        controller.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task DentistMedicalRecordController_GetImagingFile_WhenApiReturns403_ReturnsForbidResult()
    {
        _apiMock.Setup(x => x.GetImagingFileStreamAsync(42, false))
            .ReturnsAsync(new ApiFileStreamResult
            {
                StatusCode = 403
            });

        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist")
        };

        var result = await controller.GetImagingFile(42, false);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task DentistMedicalRecordController_GetImagingFile_WhenApiReturns401_ReturnsUnauthorizedResult()
    {
        _apiMock.Setup(x => x.GetImagingFileStreamAsync(42, false))
            .ReturnsAsync(new ApiFileStreamResult
            {
                StatusCode = 401
            });

        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist")
        };

        var result = await controller.GetImagingFile(42, false);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task PatientMedicalRecordController_GetImagingFile_WhenApiSucceeds_ReturnsFileResult()
    {
        var fakeStream = new System.IO.MemoryStream(new byte[] { 0x4, 0x5 });
        _apiMock.Setup(x => x.GetImagingFileStreamAsync(99, true))
            .ReturnsAsync(new ApiFileStreamResult
            {
                Stream = fakeStream,
                ContentType = "application/dicom",
                FileName = "scan_99.dcm",
                StatusCode = 200
            });

        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Patient")
        };

        var result = await controller.GetImagingFile(99, true);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("application/dicom");
        fileResult.FileDownloadName.Should().Be("scan_99.dcm");
        controller.Response.Headers["Cache-Control"].ToString().Should().Contain("no-store");
    }

    [Fact]
    public async Task PatientMedicalRecordController_GetImagingFile_WhenApiReturns403_ReturnsForbidResult()
    {
        _apiMock.Setup(x => x.GetImagingFileStreamAsync(99, false))
            .ReturnsAsync(new ApiFileStreamResult
            {
                StatusCode = 403
            });

        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(50, "Patient")
        };

        var result = await controller.GetImagingFile(99, false);

        result.Should().BeOfType<ForbidResult>();
    }
}
