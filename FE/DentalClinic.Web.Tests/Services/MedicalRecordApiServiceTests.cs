using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace DentalClinic.Web.Tests.Services;

public class MedicalRecordApiServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<MedicalRecordApiService>> _loggerMock;
    private readonly MedicalRecordApiService _apiService;

    public MedicalRecordApiServiceTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new System.Uri("https://localhost:7350")
        };
        _loggerMock = new Mock<ILogger<MedicalRecordApiService>>();
        _apiService = new MedicalRecordApiService(_httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task UploadImagingRecordAsync_WithValidStream_SendsMultipartPost_AndReturnsSuccess()
    {
        // Arrange
        var clinicalEntryId = 15L;
        var bytes = Encoding.UTF8.GetBytes("fake-image-bytes");
        using var stream = new MemoryStream(bytes);

        var responseDto = new ImagingRecordMetadataDto
        {
            ImagingRecordId = 101,
            ClinicalEntryId = clinicalEntryId,
            ImagingType = "Panorama",
            FileUrl = "https://res.cloudinary.com/demo/image/upload/sample.jpg",
            ViewUrl = "/api/medical-records/imaging/101/content",
            ResultSummary = "Ảnh toàn cảnh bình thường"
        };

        var responseJson = JsonSerializer.Serialize(new ApiResponse<ImagingRecordMetadataDto>
        {
            Success = true,
            Message = "Tải phim X-quang thành công.",
            Data = responseDto
        });

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.AbsolutePath == $"/api/medical-records/clinical-entries/{clinicalEntryId}/imaging" &&
                    req.Content is MultipartFormDataContent),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _apiService.UploadImagingRecordAsync(
            clinicalEntryId,
            stream,
            "pano.jpg",
            "image/jpeg",
            "Panorama",
            "Ảnh toàn cảnh bình thường");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ImagingRecordId.Should().Be(101);
        result.Data.ImagingType.Should().Be("Panorama");
    }

    [Fact]
    public async Task UploadImagingRecordAsync_WithValidFormFile_Succeeds()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        var content = "fake-file-content";
        var fileName = "xray.png";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));

        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(ms.Length);
        fileMock.Setup(f => f.ContentType).Returns("image/png");

        var responseDto = new ImagingRecordMetadataDto
        {
            ImagingRecordId = 102,
            ClinicalEntryId = 20,
            ImagingType = "Periapical"
        };

        var responseJson = JsonSerializer.Serialize(new ApiResponse<ImagingRecordMetadataDto>
        {
            Success = true,
            Data = responseDto
        });

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.AbsolutePath == "/api/medical-records/clinical-entries/20/imaging"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _apiService.UploadImagingRecordAsync(20, fileMock.Object, "Periapical", "Răng số 6");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.ImagingRecordId.Should().Be(102);
    }

    [Fact]
    public async Task UploadImagingRecordAsync_WhenFileEmpty_ReturnsBadRequestWithoutApiCall()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(0);

        // Act
        var result = await _apiService.UploadImagingRecordAsync(10, fileMock.Object, "Panorama");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("chọn tập tin");
    }

    [Fact]
    public async Task UploadImagingRecordAsync_WhenExtensionInvalid_ReturnsBadRequest()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("malicious.exe");
        fileMock.Setup(f => f.Length).Returns(1024);

        // Act
        var result = await _apiService.UploadImagingRecordAsync(10, fileMock.Object, "Panorama");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("không được hỗ trợ");
    }

    [Fact]
    public async Task UploadImagingRecordAsync_WhenApiReturns400_ReturnsFailure()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        var ms = new MemoryStream(new byte[100]);
        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.FileName).Returns("test.jpg");
        fileMock.Setup(f => f.Length).Returns(100);

        var errorJson = JsonSerializer.Serialize(new
        {
            message = "Loại phim X-quang không hợp lệ.",
            errors = new[] { "Loại phim X-quang không hợp lệ." }
        });

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent(errorJson, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _apiService.UploadImagingRecordAsync(10, fileMock.Object, "InvalidType");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("Loại phim X-quang không hợp lệ.");
    }

    [Fact]
    public async Task UploadImagingRecordAsync_WhenNetworkFails_ReturnsServiceUnavailable()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        var ms = new MemoryStream(new byte[100]);
        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.FileName).Returns("test.jpg");
        fileMock.Setup(f => f.Length).Returns(100);

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var result = await _apiService.UploadImagingRecordAsync(10, fileMock.Object, "Panorama");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be((int)HttpStatusCode.ServiceUnavailable);
        result.Message.Should().Contain("Không thể kết nối đến máy chủ API");
    }

    [Fact]
    public async Task GetClinicalEntryImagingAsync_SendsGet_ReturnsList()
    {
        // Arrange
        var clinicalEntryId = 33L;
        var list = new List<ImagingRecordDto>
        {
            new() { ImagingRecordId = 1, ClinicalEntryId = clinicalEntryId, ImagingType = "Panorama" },
            new() { ImagingRecordId = 2, ClinicalEntryId = clinicalEntryId, ImagingType = "Periapical" }
        };

        var responseJson = JsonSerializer.Serialize(new ApiResponse<List<ImagingRecordDto>>
        {
            Success = true,
            Data = list
        });

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.AbsolutePath == $"/api/medical-records/clinical-entries/{clinicalEntryId}/imaging"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _apiService.GetClinicalEntryImagingAsync(clinicalEntryId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_SendsDelete_ReturnsSuccess()
    {
        // Arrange
        var imagingRecordId = 77L;
        var responseJson = JsonSerializer.Serialize(new ApiResponse<bool>
        {
            Success = true,
            Data = true,
            Message = "Xóa phim X-quang thành công."
        });

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Delete &&
                    req.RequestUri!.AbsolutePath == $"/api/medical-records/imaging/{imagingRecordId}"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _apiService.DeleteImagingRecordAsync(imagingRecordId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }
}
