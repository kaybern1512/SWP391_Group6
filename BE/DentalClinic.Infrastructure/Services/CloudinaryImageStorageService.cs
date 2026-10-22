using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DentalClinic.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class CloudinaryImageStorageService : IImageStorageService
{
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
}
