using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DentalClinic.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class CloudinaryMediaStorageService : IMediaStorageService
{
    private readonly Cloudinary? _cloudinary;
    private readonly ILogger<CloudinaryMediaStorageService> _logger;
    private readonly bool _isConfigured;

    public const string DefaultImagesFolder = "dental-clinic/blog/images";
    public const string DefaultVideosFolder = "dental-clinic/blog/videos";

    public CloudinaryMediaStorageService(IConfiguration configuration, ILogger<CloudinaryMediaStorageService> logger)
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
            _logger.LogWarning("Cloudinary credentials are not configured or contain placeholder values. Media uploads will require user-secrets.");
            _cloudinary = null;
            _isConfigured = false;
        }
    }

    public async Task<MediaUploadResult> UploadImageAsync(Stream fileStream, string originalFileName, string contentType, string? folder = null, CancellationToken ct = default)
    {
        if (fileStream == null || fileStream.Length == 0)
        {
            throw new ArgumentException("Tập tin hình ảnh không hợp lệ hoặc rỗng.");
        }

        if (!_isConfigured || _cloudinary == null)
        {
            throw new InvalidOperationException("Dịch vụ lưu trữ Cloudinary chưa được cấu hình.");
        }

        var publicId = $"img_{Guid.NewGuid():N}";
        var targetFolder = folder ?? DefaultImagesFolder;

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(originalFileName, fileStream),
            Folder = targetFolder,
            PublicId = publicId,
            Overwrite = true,
            Transformation = new Transformation()
                .Width(1600)
                .Crop("limit")
                .Quality("auto")
                .FetchFormat("auto")
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams, ct);
        if (uploadResult.Error != null)
        {
            _logger.LogError("Cloudinary image upload failed: {Error}", uploadResult.Error.Message);
            throw new InvalidOperationException($"Lỗi tải ảnh lên Cloudinary: {uploadResult.Error.Message}");
        }

        var url = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString() ?? string.Empty;

        return new MediaUploadResult
        {
            Url = url,
            PublicId = uploadResult.PublicId,
            ThumbnailUrl = url,
            MediaType = "Image",
            ContentType = contentType,
            SizeBytes = uploadResult.Bytes > 0 ? uploadResult.Bytes : fileStream.Length
        };
    }

    public async Task<MediaUploadResult> UploadVideoAsync(Stream fileStream, string originalFileName, string contentType, string? folder = null, CancellationToken ct = default)
    {
        if (fileStream == null || fileStream.Length == 0)
        {
            throw new ArgumentException("Tập tin video không hợp lệ hoặc rỗng.");
        }

        if (!_isConfigured || _cloudinary == null)
        {
            throw new InvalidOperationException("Dịch vụ lưu trữ Cloudinary chưa được cấu hình.");
        }

        var publicId = $"vid_{Guid.NewGuid():N}";
        var targetFolder = folder ?? DefaultVideosFolder;

        var uploadParams = new VideoUploadParams
        {
            File = new FileDescription(originalFileName, fileStream),
            Folder = targetFolder,
            PublicId = publicId,
            Overwrite = true
        };

        var uploadResult = await _cloudinary.UploadLargeAsync(uploadParams, bufferSize: 20 * 1024 * 1024, cancellationToken: ct);
        if (uploadResult.Error != null)
        {
            _logger.LogError("Cloudinary video upload failed: {Error}", uploadResult.Error.Message);
            throw new InvalidOperationException($"Lỗi tải video lên Cloudinary: {uploadResult.Error.Message}");
        }

        var url = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString() ?? string.Empty;

        // Poster thumbnail: replace video extension with .jpg or fetch first frame
        string? posterUrl = null;
        if (!string.IsNullOrWhiteSpace(url))
        {
            var lastDot = url.LastIndexOf('.');
            if (lastDot > 0)
            {
                posterUrl = url.Substring(0, lastDot) + ".jpg";
            }
        }

        return new MediaUploadResult
        {
            Url = url,
            PublicId = uploadResult.PublicId,
            ThumbnailUrl = posterUrl,
            MediaType = "Video",
            ContentType = contentType,
            SizeBytes = uploadResult.Bytes > 0 ? uploadResult.Bytes : fileStream.Length,
            DurationSeconds = uploadResult.Duration > 0 ? (int)Math.Round(uploadResult.Duration) : null
        };
    }

    public async Task<bool> DeleteMediaAsync(string publicId, string mediaType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(publicId)) return false;

        if (!_isConfigured || _cloudinary == null)
        {
            _logger.LogWarning("Cloudinary not configured. Skipping remote deletion of {PublicId}", publicId);
            return true;
        }

        try
        {
            var isVideo = string.Equals(mediaType, "Video", StringComparison.OrdinalIgnoreCase);
            var deletionParams = new DeletionParams(publicId)
            {
                ResourceType = isVideo ? ResourceType.Video : ResourceType.Image
            };

            var result = await _cloudinary.DestroyAsync(deletionParams);
            return result.Result == "ok" || result.Result == "not found";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa media khỏi Cloudinary: {PublicId}", publicId);
            return false;
        }
    }
}
