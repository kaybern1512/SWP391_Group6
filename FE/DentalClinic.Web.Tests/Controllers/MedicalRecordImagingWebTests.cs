using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Dentist;
using DentalClinic.Web.Controllers.Patient;
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

public class MedicalRecordImagingWebTests
{
    private readonly Mock<IMedicalRecordApiService> _apiMock;

    public MedicalRecordImagingWebTests()
    {
        _apiMock = new Mock<IMedicalRecordApiService>();
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

    private static IFormFile CreateMockFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.Length).Returns(content.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        return fileMock.Object;
    }

    [Fact]
    public async Task DentistMedicalRecordController_UploadImaging_ValidFile_CallsApiAndRedirects()
    {
        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Dentist", "BS. Long"),
            TempData = CreateTempData()
        };

        var file = CreateMockFormFile("xray.jpg", "image/jpeg", new byte[] { 1, 2, 3 });

        _apiMock.Setup(x => x.UploadImagingRecordAsync(
                701,
                It.IsAny<Stream>(),
                "xray.jpg",
                "image/jpeg",
                "Panorama",
                "Khảo sát toàn hàm"))
            .ReturnsAsync(ApiResult<ImagingRecordDto>.Success(new ImagingRecordDto
            {
                ImagingRecordId = 10,
                ClinicalEntryId = 701,
                ImagingType = "Panorama"
            }));

        var result = await controller.UploadImaging(
            patientUserId: 101,
            clinicalEntryId: 701,
            imageFile: file,
            imagingType: "Panorama",
            resultSummary: "Khảo sát toàn hàm");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        redirect.RouteValues!["patientId"].Should().Be(101L);
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task DentistMedicalRecordController_UploadImaging_NullFile_SetsErrorAndRedirects()
    {
        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Dentist"),
            TempData = CreateTempData()
        };

        var result = await controller.UploadImaging(
            patientUserId: 101,
            clinicalEntryId: 701,
            imageFile: null,
            imagingType: "Panorama",
            resultSummary: null);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["ErrorMessage"].Should().Be("Vui lòng chọn một tệp hình ảnh để tải lên.");
    }

    [Fact]
    public async Task DentistMedicalRecordController_DeleteImaging_CallsApiAndRedirects()
    {
        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Dentist"),
            TempData = CreateTempData()
        };

        _apiMock.Setup(x => x.DeleteImagingRecordAsync(10))
            .ReturnsAsync(ApiResult<bool>.Success(true));

        var result = await controller.DeleteImaging(patientUserId: 101, imagingRecordId: 10);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task DentistMedicalRecordController_ImageContent_Found_ReturnsFileResultWithSecurityHeaders()
    {
        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Dentist")
        };

        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("fake-image-bytes"));
        _apiMock.Setup(x => x.GetImageContentAsync(10))
            .ReturnsAsync((memoryStream, "image/jpeg"));

        var result = await controller.ImageContent(10);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("image/jpeg");
        controller.Response.Headers["Cache-Control"].ToString().Should().Contain("no-store");
        controller.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task DentistMedicalRecordController_ImageContent_InvalidContentType_ReturnsBadRequest()
    {
        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Dentist")
        };

        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("<html>error</html>"));
        _apiMock.Setup(x => x.GetImageContentAsync(10))
            .ReturnsAsync((memoryStream, "text/html"));

        var result = await controller.ImageContent(10);

        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task DentistMedicalRecordController_ImageContent_NotFound_ReturnsNotFound()
    {
        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(201, "Dentist")
        };

        _apiMock.Setup(x => x.GetImageContentAsync(999))
            .ReturnsAsync(((Stream, string)?)null);

        var result = await controller.ImageContent(999);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PatientMedicalRecordController_ImageContent_Found_ReturnsFileResultWithSecurityHeaders()
    {
        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("fake-patient-xray"));
        _apiMock.Setup(x => x.GetImageContentAsync(10))
            .ReturnsAsync((memoryStream, "image/png"));

        var result = await controller.ImageContent(10);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("image/png");
        controller.Response.Headers["Cache-Control"].ToString().Should().Contain("no-store");
        controller.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task PatientMedicalRecordController_ImageContent_InvalidContentType_ReturnsBadRequest()
    {
        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("<html>error</html>"));
        _apiMock.Setup(x => x.GetImageContentAsync(10))
            .ReturnsAsync((memoryStream, "text/html"));

        var result = await controller.ImageContent(10);

        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task PatientMedicalRecordController_ImageContent_NotFound_ReturnsNotFound()
    {
        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        _apiMock.Setup(x => x.GetImageContentAsync(999))
            .ReturnsAsync(((Stream, string)?)null);

        var result = await controller.ImageContent(999);

        result.Should().BeOfType<NotFoundResult>();
    }
}
