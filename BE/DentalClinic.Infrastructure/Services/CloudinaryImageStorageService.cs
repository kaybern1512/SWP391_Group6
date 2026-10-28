using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class CloudinaryImageStorageService : IImageStorageService, IMedicalImagingStorageService
{
    public string StorageProviderName => "Cloudinary";

    public bool IsLocalReference(string? fileUrl) => false;

    private readonly Cloudinary? _cloudinary;
    private readonly ILogger<CloudinaryImageStorageService> _logger;
    private readonly bool _isConfigured;

    public CloudinaryImageStorageService(IConfiguration configuration, ILogger<CloudinaryImageStorageService> logger)
    {
        _logger = logger;

        var cloudName = configuration["Cloudinary:CloudName"] ?? configuration["CLOUDINARY_CLOUD_NAME"];
        var apiKey = configuration["Cloudinary:ApiKey"] ?? configuration["CLOUDINARY_API_KEY"];
        var apiSecret = configuration["Cloudinary:ApiSecret"] ?? configuration["CLOUDINARY_API_SECRET"];

        if (!string.IsNullOrWhiteSpace(cloudName) &&
            !string.IsNullOrWhiteSpace(apiKey) &&
            !string.IsNullOrWhiteSpace(apiSecret) &&
            !cloudName.Contains("YOUR_CLOUDINARY", StringComparison.OrdinalIgnoreCase) &&
            !apiKey.Contains("YOUR_CLOUDINARY", StringComparison.OrdinalIgnoreCase))
        {
            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
            _isConfigured = true;
        }
        else
        {
            _logger.LogWarning("Cloudinary credentials are not configured or contain placeholder values. Uploads will require configuration via user-secrets.");
            _cloudinary = null;
            _isConfigured = false;
        }
    }

    public async Task<string> UploadAvatarAsync(long userId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default)
    {
        if (fileStream == null || fileStream.Length == 0)
        {
            throw new ArgumentException("Tập tin hình ảnh không hợp lệ hoặc rỗng.");
        }

        if (fileStream.Length > 5 * 1024 * 1024)
        {
            throw new ArgumentException("Kích thước ảnh không được vượt quá 5 MB.");
        }

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExts.Contains(ext))
        {
            throw new ArgumentException("Định dạng file không hợp lệ. Chỉ chấp nhận .jpg, .jpeg, .png, .webp.");
        }

        if (!_isConfigured || _cloudinary == null)
        {
            throw new InvalidOperationException("Dịch vụ lưu trữ Cloudinary chưa được cấu hình. Vui lòng thiết lập CloudName, ApiKey và ApiSecret qua dotnet user-secrets.");
        }

        var publicId = $"dental-clinic/avatars/user_{userId}";

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(originalFileName, fileStream),
            PublicId = publicId,
            Overwrite = true,
            Invalidate = true,
            Transformation = new Transformation()
                .Width(500)
                .Height(500)
                .Crop("fill")
                .Gravity("auto")
                .Quality("auto")
                .FetchFormat("auto")
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams, ct);

        if (uploadResult.Error != null)
        {
            _logger.LogError("Cloudinary upload failed: {Error}", uploadResult.Error.Message);
            throw new InvalidOperationException($"Lỗi tải ảnh lên Cloudinary: {uploadResult.Error.Message}");
        }

        var url = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString();
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Không nhận được URL ảnh từ Cloudinary.");
        }

        return url;
    }

    public async Task<string> UploadBlogImageAsync(Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default)
    {
        if (fileStream == null || fileStream.Length == 0)
        {
            throw new ArgumentException("Tập tin hình ảnh không hợp lệ hoặc rỗng.");
        }

        if (fileStream.Length > 5 * 1024 * 1024)
        {
            throw new ArgumentException("Kích thước ảnh không được vượt quá 5 MB.");
        }

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExts.Contains(ext))
        {
            throw new ArgumentException("Định dạng file không hợp lệ. Chỉ chấp nhận .jpg, .jpeg, .png, .webp.");
        }

        if (!_isConfigured || _cloudinary == null)
        {
            throw new InvalidOperationException("Dịch vụ lưu trữ Cloudinary chưa được cấu hình. Vui lòng thiết lập CloudName, ApiKey và ApiSecret qua dotnet user-secrets.");
        }

        var publicId = $"dental-clinic/blogs/blog_{Guid.NewGuid():N}";

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(originalFileName, fileStream),
            PublicId = publicId,
            Overwrite = true,
            Invalidate = true,
            Transformation = new Transformation()
                .Width(1200)
                .Crop("limit")
                .Quality("auto")
                .FetchFormat("auto")
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams, ct);

        if (uploadResult.Error != null)
        {
            _logger.LogError("Cloudinary blog upload failed: {Error}", uploadResult.Error.Message);
            throw new InvalidOperationException($"Lỗi tải ảnh bài viết lên Cloudinary: {uploadResult.Error.Message}");
        }

        var url = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString();
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Không nhận được URL ảnh từ Cloudinary.");
        }

        return url;
    }

    /// <summary>
    /// Uploads a dental medical imaging file using the DentalImageFileInput abstraction.
    /// </summary>
    public Task<DentalImageUploadResult> UploadDentalImageAsync(DentalImageFileInput file, string? subFolder = null, CancellationToken ct = default)
    {
        if (file == null)
        {
            throw new ArgumentNullException(nameof(file), "Thông tin tập tin hình ảnh không được để trống.");
        }

        return UploadDentalImageAsync(file.Content, file.FileName, file.ContentType, subFolder, ct);
    }

    /// <summary>
    /// Streams a dental image or DICOM file directly to Cloudinary storage.
    /// NOTE & RISK DISCLOSURE:
    /// Default Cloudinary uploads generate publicly accessible HTTPS URLs once the URL is shared.
    /// For strict HIPAA or confidential medical records, production systems should configure Cloudinary
    /// authenticated mode or private object storage (e.g. AWS S3 private bucket with presigned URLs).
    /// DICOM LIMITATION:
    /// Cloudinary stores .dcm files as Raw assets. It does not render in-browser DICOM previews without
    /// specialized medical imaging extensions. Full parsing and previewing should be handled on client/viewer.
    /// </summary>
    public async Task<DentalImageUploadResult> UploadDentalImageAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        string? subFolder = null,
        CancellationToken ct = default)
    {
        if (fileStream == null || (fileStream.CanSeek && fileStream.Length == 0))
        {
            throw new ArgumentException("Tập tin hình ảnh nha khoa không hợp lệ hoặc rỗng.", nameof(fileStream));
        }

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException("Tên tập tin không được để trống.", nameof(originalFileName));
        }

        if (!_isConfigured || _cloudinary == null)
        {
            throw new InvalidOperationException("Dịch vụ lưu trữ Cloudinary chưa được cấu hình. Vui lòng thiết lập CloudName, ApiKey và ApiSecret qua dotnet user-secrets.");
        }

        if (fileStream.CanSeek && fileStream.Position != 0)
        {
            fileStream.Seek(0, SeekOrigin.Begin);
        }

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var folder = string.IsNullOrWhiteSpace(subFolder)
            ? "dental-clinic/medical-imaging"
            : $"dental-clinic/medical-imaging/{subFolder.Trim().Trim('/')}";

        var publicId = $"{folder}/img_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";

        string url;
        long bytesUploaded;
        string format;

        if (ext is ".dcm" or ".dicom")
        {
            // DICOM files (PS 3.10) must be stored as raw assets in Cloudinary
            var rawParams = new RawUploadParams
            {
                File = new FileDescription(originalFileName, fileStream),
                PublicId = publicId,
                UseFilename = false,
                UniqueFilename = false,
                Overwrite = false
            };

            var rawResult = await _cloudinary.UploadAsync(rawParams, type: "auto", cancellationToken: ct);

            if (rawResult.Error != null)
            {
                _logger.LogError("Cloudinary DICOM upload failed: {Error}", rawResult.Error.Message);
                throw new InvalidOperationException($"Lỗi tải tập tin DICOM lên Cloudinary: {rawResult.Error.Message}");
            }

            url = rawResult.SecureUrl?.ToString() ?? rawResult.Url?.ToString() ?? string.Empty;
            bytesUploaded = rawResult.Bytes;
            format = "dcm";
        }
        else
        {
            // Standard dental X-Ray / clinical photography (.jpg, .png, etc.)
            // Full resolution preserved without destructive cropping
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(originalFileName, fileStream),
                PublicId = publicId,
                UseFilename = false,
                UniqueFilename = false,
                Overwrite = false
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams, ct);

            if (uploadResult.Error != null)
            {
                _logger.LogError("Cloudinary dental image upload failed: {Error}", uploadResult.Error.Message);
                throw new InvalidOperationException($"Lỗi tải hình ảnh X-quang lên Cloudinary: {uploadResult.Error.Message}");
            }

            url = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString() ?? string.Empty;
            bytesUploaded = uploadResult.Bytes;
            format = uploadResult.Format ?? ext.TrimStart('.');
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Không nhận được URL ảnh từ Cloudinary.");
        }

        return new DentalImageUploadResult
        {
            Url = url,
            PublicId = publicId,
            FileSizeBytes = bytesUploaded > 0 ? bytesUploaded : (fileStream.CanSeek ? fileStream.Length : 0),
            Format = format,
            StorageProvider = "Cloudinary"
        };
    }

    /// <summary>
    /// Safely deletes an uploaded asset from Cloudinary to prevent orphaned files when downstream operations fail.
    /// </summary>
    public async Task<bool> DeleteDentalImageAsync(string publicId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(publicId) || !_isConfigured || _cloudinary == null)
        {
            return false;
        }

        try
        {
            var delParams = new DeletionParams(publicId) { Invalidate = true };
            var result = await _cloudinary.DestroyAsync(delParams);
            if (string.Equals(result.Result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            delParams.ResourceType = ResourceType.Raw;
            var rawResult = await _cloudinary.DestroyAsync(delParams);
            return string.Equals(rawResult.Result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up Cloudinary asset with public ID {PublicId}", publicId);
            return false;
        }
    }

    /// <summary>
    /// Retrieves a secure read stream of an image asset from storage for authorized server-side delivery.
    /// Incorporates SSRF guard: only HTTPS requests targeting legitimate Cloudinary domains are permitted.
    /// </summary>
    public async Task<Stream?> GetImageStreamAsync(string fileUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl) || !Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!IsAllowedCloudinaryStorageUri(uri))
        {
            _logger.LogWarning("Potential SSRF attempt or disallowed storage URL blocked: {FileUrl}", fileUrl);
            return null;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            using var handler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false };
            using var httpClient = new System.Net.Http.HttpClient(handler);
            var response = await httpClient.GetAsync(uri, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            const long maxAllowedBytes = 20 * 1024 * 1024; // 20 MB limit to prevent OOM
            if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > maxAllowedBytes)
            {
                _logger.LogWarning("Storage response exceeds maximum permitted size of 20 MB: {FileUrl}", fileUrl);
                return null;
            }

            var ms = new MemoryStream();
            await response.Content.CopyToAsync(ms, cts.Token);
            ms.Position = 0;
            return ms;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve image stream from URL {FileUrl}", fileUrl);
            return null;
        }
    }

    /// <summary>
    /// Validates that a storage URI is strictly HTTPS and points exclusively to approved Cloudinary domains,
    /// protecting against Server-Side Request Forgery (SSRF) targeting internal networks or cloud metadata.
    /// </summary>
    public static bool IsAllowedCloudinaryStorageUri(Uri uri)
    {
        if (uri == null || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        // Reject credentials in URL
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        // Reject non-standard HTTPS ports
        if (!uri.IsDefaultPort && uri.Port != 443)
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();

        // Reject raw IP addresses (IPv4 or IPv6)
        if (System.Net.IPAddress.TryParse(host, out _))
        {
            return false;
        }

        // Reject localhost and internal network identifiers
        if (host is "localhost" || host.EndsWith(".local") || host.EndsWith(".internal"))
        {
            return false;
        }

        // Only allow official Cloudinary domains
        return host == "res.cloudinary.com" ||
               host == "api.cloudinary.com" ||
               host.EndsWith(".cloudinary.com");
    }
}
