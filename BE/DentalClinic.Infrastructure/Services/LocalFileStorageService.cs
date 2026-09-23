using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DentalClinic.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IConfiguration _configuration;
    private readonly string _uploadDirectory;
    private readonly string _baseUrl;

    public LocalFileStorageService(IConfiguration configuration, Microsoft.Extensions.Hosting.IHostEnvironment env)
    {
        _configuration = configuration;
        _baseUrl = _configuration["FileStorage:BaseUrl"] ?? "https://localhost:7350";
        var customPath = _configuration["FileStorage:LocalPath"];

        if (!string.IsNullOrWhiteSpace(customPath))
        {
            _uploadDirectory = customPath;
        }
        else
        {
            _uploadDirectory = Path.Combine(env.ContentRootPath, "wwwroot", "uploads", "avatars");
        }

        if (!Directory.Exists(_uploadDirectory))
        {
            Directory.CreateDirectory(_uploadDirectory);
        }
    }

    public async Task<string> SaveAvatarAsync(Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExts.Contains(ext))
        {
            throw new ArgumentException("Định dạng file không hợp lệ. Chỉ chấp nhận .jpg, .jpeg, .png, .webp.");
        }

        var newFileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(_uploadDirectory, newFileName);

        using (var outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(outputStream, ct);
        }

        return $"{_baseUrl.TrimEnd('/')}/uploads/avatars/{newFileName}";
    }
}
