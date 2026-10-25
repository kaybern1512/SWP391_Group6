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
    public async Task DentistMedicalRecordController_UploadImaging_WhenValid_RedirectsToIndexWithSuccessMessage()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("xray.jpg");
        fileMock.Setup(f => f.Length).Returns(1024);

        _apiMock.Setup(x => x.UploadImagingRecordAsync(15, fileMock.Object, "Panorama", "Bình thường"))
            .ReturnsAsync(ApiResult<ImagingRecordMetadataDto>.Success(new ImagingRecordMetadataDto { ImagingRecordId = 1 }));

        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist"),
            TempData = CreateTempData()
        };

        var model = new UploadDentalImagingFormModel
        {
            ClinicalEntryId = 15,
            PatientUserId = 10,
            File = fileMock.Object,
            ImagingType = "Panorama",
            ResultSummary = "Bình thường"
        };

        var result = await controller.UploadImaging(model);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        redirect.RouteValues!["patientId"].Should().Be(10L);
        controller.TempData["SuccessMessage"].Should().Be("Tải lên phim X-quang / ảnh răng thành công!");
    }

    [Fact]
    public async Task DentistMedicalRecordController_UploadImaging_WhenFileMissing_RedirectsToIndexWithErrorMessage()
    {
        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist"),
            TempData = CreateTempData()
        };

        var model = new UploadDentalImagingFormModel
        {
            ClinicalEntryId = 15,
            PatientUserId = 10,
            File = null,
            ImagingType = "Panorama"
        };

        var result = await controller.UploadImaging(model);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["ErrorMessage"]!.ToString().Should().Contain("chọn tập tin");
    }

    [Fact]
    public async Task DentistMedicalRecordController_UploadImaging_WhenFileExtensionInvalid_RedirectsToIndexWithErrorMessage()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("virus.exe");
        fileMock.Setup(f => f.Length).Returns(1024);

        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist"),
            TempData = CreateTempData()
        };

        var model = new UploadDentalImagingFormModel
        {
            ClinicalEntryId = 15,
            PatientUserId = 10,
            File = fileMock.Object,
            ImagingType = "Panorama"
        };

        var result = await controller.UploadImaging(model);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["ErrorMessage"]!.ToString().Should().Contain("không được hỗ trợ");
    }

    [Fact]
    public async Task DentistMedicalRecordController_UploadImaging_WhenApiFails_RedirectsToIndexWithErrorMessage()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("xray.png");
        fileMock.Setup(f => f.Length).Returns(1024);

        _apiMock.Setup(x => x.UploadImagingRecordAsync(15, fileMock.Object, "Panorama", null))
            .ReturnsAsync(ApiResult<ImagingRecordMetadataDto>.Failure("Cloudinary quota exceeded", statusCode: 500));

        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist"),
            TempData = CreateTempData()
        };

        var model = new UploadDentalImagingFormModel
        {
            ClinicalEntryId = 15,
            PatientUserId = 10,
            File = fileMock.Object,
            ImagingType = "Panorama"
        };

        var result = await controller.UploadImaging(model);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["ErrorMessage"].Should().Be("Cloudinary quota exceeded");
    }

    [Fact]
    public async Task DentistMedicalRecordController_ImagingContent_WhenFound_ReturnsFileResult()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("fake-image-data");
        var ms = new System.IO.MemoryStream(bytes);
        _apiMock.Setup(x => x.GetImagingStreamAsync(88))
            .ReturnsAsync((ms, "image/png", 200));

        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist")
        };

        var result = await controller.ImagingContent(88);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("image/png");
        fileResult.FileStream.Should().NotBeNull();
    }

    [Fact]
    public async Task DentistMedicalRecordController_ImagingContent_WhenNotFound_ReturnsNotFound()
    {
        _apiMock.Setup(x => x.GetImagingStreamAsync(99))
            .ReturnsAsync((null, string.Empty, 404));

        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist")
        };

        var result = await controller.ImagingContent(99);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DentistMedicalRecordController_ImagingContent_WhenForbidden_ReturnsForbid()
    {
        _apiMock.Setup(x => x.GetImagingStreamAsync(100))
            .ReturnsAsync((null, string.Empty, 403));

        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist")
        };

        var result = await controller.ImagingContent(100);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task DentistMedicalRecordController_Index_WithClinicalEntriesAndImaging_PopulatesViewModelForGallery()
    {
        var record = new MedicalRecordDto
        {
            PatientUserId = 12,
            PatientName = "Trần Văn C",
            ClinicalEntries = new List<ClinicalEntryDto>
            {
                new()
                {
                    ClinicalEntryId = 5,
                    Diagnosis = "Viêm tủy cấp R46",
                    ImagingRecords = new List<ImagingRecordDto>
                    {
                        new()
                        {
                            ImagingRecordId = 88,
                            ClinicalEntryId = 5,
                            ImagingType = "Periapical",
                            FileUrl = "https://cloudinary.com/sample.jpg",
                            ResultSummary = "Nang quanh cuống chân răng 46"
                        }
                    }
                }
            }
        };

        _apiMock.Setup(x => x.SearchPatientsAsync(null))
            .ReturnsAsync(ApiResult<List<PatientLookupDto>>.Success(new List<PatientLookupDto> { new() { PatientUserId = 12, FullName = "Trần Văn C" } }));
        _apiMock.Setup(x => x.GetByPatientIdAsync(12))
            .ReturnsAsync(ApiResult<MedicalRecordDto>.Success(record));

        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist")
        };

        var result = await controller.Index(12);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<DentistMedicalRecordIndexViewModel>().Subject;
        vm.SelectedRecord.Should().NotBeNull();
        vm.SelectedRecord!.ClinicalEntries.Should().HaveCount(1);
        vm.SelectedRecord.ClinicalEntries[0].ImagingRecords.Should().HaveCount(1);
        vm.SelectedRecord.ClinicalEntries[0].ImagingRecords[0].ImagingType.Should().Be("Periapical");
    }

    [Fact]
    public void PatientMedicalRecordController_HasAuthorizeAttribute_WithRolePatient()
    {
        var authAttr = System.Attribute.GetCustomAttribute(
            typeof(PatientMedicalRecordController),
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute)) as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

        authAttr.Should().NotBeNull();
        authAttr!.Roles.Should().Be("Patient");
    }

    [Fact]
    public async Task PatientMedicalRecordController_Index_WithImagingRecords_PopulatesPatientViewModel()
    {
        var record = new MedicalRecordDto
        {
            PatientUserId = 101,
            PatientName = "Lê Thị B",
            ClinicalEntries = new List<ClinicalEntryDto>
            {
                new()
                {
                    ClinicalEntryId = 12,
                    Diagnosis = "Chỉnh nha toàn hàm",
                    CreatedAt = System.DateTime.UtcNow,
                    ImagingRecords = new List<ImagingRecordDto>
                    {
                        new()
                        {
                            ImagingRecordId = 55,
                            ClinicalEntryId = 12,
                            ImagingType = "Cephalometric",
                            FileUrl = "https://res.cloudinary.com/demo/image/upload/ceph.jpg",
                            ResultSummary = "Góc ANB tăng nhẹ, hạng II xương",
                            CreatedAt = System.DateTime.UtcNow
                        }
                    }
                }
            }
        };

        _apiMock.Setup(x => x.GetMyMedicalRecordAsync())
            .ReturnsAsync(ApiResult<MedicalRecordDto>.Success(record));

        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient", "Lê Thị B")
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewName.Should().Be("~/Views/Patient/MedicalRecords/Index.cshtml");
        var vm = viewResult.Model.Should().BeOfType<PatientMedicalRecordIndexViewModel>().Subject;
        vm.Record.Should().NotBeNull();
        vm.Record!.PatientUserId.Should().Be(101);
        vm.Record.ClinicalEntries.Should().HaveCount(1);
        vm.Record.ClinicalEntries[0].ImagingRecords.Should().HaveCount(1);
        vm.Record.ClinicalEntries[0].ImagingRecords[0].ImagingType.Should().Be("Cephalometric");
        vm.Record.ClinicalEntries[0].ImagingRecords[0].ResultSummary.Should().Be("Góc ANB tăng nhẹ, hạng II xương");
    }

    [Fact]
    public async Task PatientMedicalRecordController_Index_WhenNoRecord_ReturnsNullRecordInViewModel()
    {
        _apiMock.Setup(x => x.GetMyMedicalRecordAsync())
            .ReturnsAsync(ApiResult<MedicalRecordDto>.Failure("NotFound", statusCode: 404));

        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient", "Lê Thị B")
        };

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<PatientMedicalRecordIndexViewModel>().Subject;
        vm.Record.Should().BeNull();
    }

    [Fact]
    public async Task PatientMedicalRecordController_ImagingContent_WhenFound_ReturnsFileResult()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("patient-image-content");
        var ms = new System.IO.MemoryStream(bytes);
        _apiMock.Setup(x => x.GetImagingStreamAsync(55))
            .ReturnsAsync((ms, "image/jpeg", 200));

        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.ImagingContent(55);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("image/jpeg");
        fileResult.FileStream.Should().NotBeNull();
    }

    [Fact]
    public async Task PatientMedicalRecordController_ImagingContent_WhenForbidden_ReturnsForbid()
    {
        _apiMock.Setup(x => x.GetImagingStreamAsync(99))
            .ReturnsAsync((null, string.Empty, 403));

        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.ImagingContent(99);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task PatientMedicalRecordController_ImagingContent_WhenNotFound_ReturnsNotFound()
    {
        _apiMock.Setup(x => x.GetImagingStreamAsync(404))
            .ReturnsAsync((null, string.Empty, 404));

        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.ImagingContent(404);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PatientMedicalRecordController_ImagingContent_WhenUnauthorized_ReturnsUnauthorized()
    {
        _apiMock.Setup(x => x.GetImagingStreamAsync(401))
            .ReturnsAsync((null, string.Empty, 401));

        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.ImagingContent(401);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task PatientMedicalRecordController_ImagingContent_WhenInvalidId_ReturnsNotFound(long invalidId)
    {
        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(101, "Patient")
        };

        var result = await controller.ImagingContent(invalidId);

        result.Should().BeOfType<NotFoundResult>();
        _apiMock.Verify(x => x.GetImagingStreamAsync(It.IsAny<long>()), Times.Never);
    }
}
