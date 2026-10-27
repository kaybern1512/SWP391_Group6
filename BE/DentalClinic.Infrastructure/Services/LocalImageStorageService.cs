using System;
using System.IO;
using System.Net.Http;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Validation;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

/// <summary>
/// High-performance, secure Local File Storage Service for Dental Medical Imaging.
/// Stores clinical diagnostic assets (JPG, PNG, DICOM) on the local disk (outside source code and wwwroot)
/// without requiring any external cloud credentials (Cloudinary).
/// </summary>
public class LocalImageStorageService : IMedicalImagingStorageService, IImageStorageService
{
    public const string SchemePrefix = "local://medical-imaging/";
    public const long MaxFileSizeBytes = 15 * 1024 * 1024; // 15 MB limit

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".dcm", ".dicom" };
    private static readonly Regex SafeSubFolderRegex = new(@"^[a-zA-Z0-9_\-]+$", RegexOptions.Compiled);

    private readonly IConfiguration _configuration;
    private readonly IDentalImageFileValidator _fileValidator;
    private readonly ILogger<LocalImageStorageService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _storageRootPath;

    public string StorageProviderName => "Local";

    public LocalImageStorageService(
        IConfiguration configuration,
        ILogger<LocalImageStorageService> logger,
        IServiceProvider serviceProvider,
        IDentalImageFileValidator? fileValidator = null)
    {
        _configuration = configuration;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _fileValidator = fileValidator ?? new DentalImageFileValidator();

        _storageRootPath = ResolveStorageRootPath();
        EnsureDirectoryExists(_storageRootPath);
    }

    /// <summary>
    /// Explicit constructor for unit testing with custom root directory.
    /// </summary>
    public LocalImageStorageService(
        string customRootPath,
        ILogger<LocalImageStorageService> logger,
        IDentalImageFileValidator? fileValidator = null)
    {
        _configuration = new ConfigurationBuilder().Build();
        _logger = logger;
        _serviceProvider = null!;
        _fileValidator = fileValidator ?? new DentalImageFileValidator();

        _storageRootPath = Path.GetFullPath(customRootPath);
        EnsureDirectoryExists(_storageRootPath);
    }

    public bool IsLocalReference(string? fileUrl)
    {
        return !string.IsNullOrWhiteSpace(fileUrl) &&
               fileUrl.StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase);
    }

    public Task<DentalImageUploadResult> UploadDentalImageAsync(
        DentalImageFileInput file,
        string? subFolder = null,
        CancellationToken ct = default)
    {
        if (file == null)
        {
            throw new ArgumentNullException(nameof(file), "Tập tin hình ảnh không được để trống.");
        }

        return UploadDentalImageAsync(file.Content, file.FileName, file.ContentType, subFolder, ct);
    }

    public async Task<DentalImageUploadResult> UploadDentalImageAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        string? subFolder = null,
        CancellationToken ct = default)
    {
        if (fileStream == null || (fileStream.CanSeek && fileStream.Length == 0))
        {
            throw new ArgumentException("Tập tin hình ảnh y tế không hợp lệ hoặc rỗng.", nameof(fileStream));
        }

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException("Tên tập tin không được để trống.", nameof(originalFileName));
        }

        if (fileStream.CanSeek && fileStream.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException($"Dung lượng tệp ({(fileStream.Length / (1024.0 * 1024.0)):F2} MB) vượt quá giới hạn tối đa 15 MB.");
        }

        // Extension check
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (Array.IndexOf(AllowedExtensions, ext) < 0)
        {
            throw new ArgumentException($"Định dạng tệp '{ext}' không được hỗ trợ. Chỉ chấp nhận JPG, JPEG, PNG hoặc DICOM (.dcm, .dicom).");
        }

        // Deep content validation (magic bytes / file signature)
        var validationInput = new DentalImageFileInput
        {
            Content = fileStream,
            FileName = originalFileName,
            ContentType = contentType,
            Length = fileStream.CanSeek ? fileStream.Length : 0
        };

        var validationResult = _fileValidator.Validate(validationInput);
        if (!validationResult.IsValid)
        {
            throw new ArgumentException(validationResult.ErrorMessage ?? "Tập tin hình ảnh không vượt qua xác thực định dạng.");
        }

        // Sanitize subfolder to prevent path traversal
        string sanitizedSubFolder = string.Empty;
        if (!string.IsNullOrWhiteSpace(subFolder))
        {
            var trimmed = subFolder.Trim().Trim('/', '\\');
            if (!SafeSubFolderRegex.IsMatch(trimmed))
            {
                throw new SecurityException($"Thư mục con '{subFolder}' chứa ký tự không hợp lệ.");
            }
            sanitizedSubFolder = trimmed;
        }

        // Generate collision-safe unique file name using GUID
        var safeFileName = $"img_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
        var relativePath = string.IsNullOrEmpty(sanitizedSubFolder)
            ? safeFileName
            : $"{sanitizedSubFolder}/{safeFileName}";

        var physicalPath = ResolveAndValidatePhysicalPath(relativePath);
        var parentDir = Path.GetDirectoryName(physicalPath)!;
        EnsureDirectoryExists(parentDir);

        if (fileStream.CanSeek && fileStream.Position != 0)
        {
            fileStream.Seek(0, SeekOrigin.Begin);
        }

        await using (var outputStream = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await fileStream.CopyToAsync(outputStream, ct);
        }

        var fileInfo = new FileInfo(physicalPath);
        var localUrl = $"{SchemePrefix}{relativePath.Replace('\\', '/')}";

        _logger.LogInformation("Saved dental imaging file to local disk: {PhysicalPath} (Size: {Size} bytes, URI: {Uri})",
            physicalPath, fileInfo.Length, localUrl);

        return new DentalImageUploadResult
        {
            Url = localUrl,
            PublicId = localUrl,
            FileSizeBytes = fileInfo.Length,
            Format = ext.TrimStart('.'),
            StorageProvider = StorageProviderName
        };
    }

    public async Task<Stream?> GetImageStreamAsync(string fileUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return null;
        }

        // 1. Handle Local Reference
        if (IsLocalReference(fileUrl))
        {
            var relativePath = fileUrl.Substring(SchemePrefix.Length);
            try
            {
                var physicalPath = ResolveAndValidatePhysicalPath(relativePath);
                if (File.Exists(physicalPath))
                {
                    return new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                }

                _logger.LogWarning("Local imaging asset not found on disk at {PhysicalPath} for URL {Url}", physicalPath, fileUrl);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading local imaging file from disk for URL {Url}", fileUrl);
                return null;
            }
        }

        // 2. Backward Compatibility Fallback for Legacy Cloudinary URLs
        if (Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return await GetRemoteLegacyStreamAsync(fileUrl, ct);
        }

        _logger.LogWarning("Unrecognized image URL scheme or format: {Url}", fileUrl);
        return null;
    }

    public async Task<bool> DeleteDentalImageAsync(string fileReferenceOrPublicId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileReferenceOrPublicId))
        {
            return true; // Idempotent
        }

        // 1. Handle Local Reference
        if (IsLocalReference(fileReferenceOrPublicId) || fileReferenceOrPublicId.StartsWith("entry_", StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = IsLocalReference(fileReferenceOrPublicId)
                ? fileReferenceOrPublicId.Substring(SchemePrefix.Length)
                : fileReferenceOrPublicId;

            try
            {
                var physicalPath = ResolveAndValidatePhysicalPath(relativePath);
                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                    _logger.LogInformation("Deleted local dental imaging asset from disk: {PhysicalPath}", physicalPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete local imaging file {Path}", relativePath);
                return false;
            }
        }

        // 2. Legacy Cloudinary deletion if service available
        try
        {
            var cloudinaryService = _serviceProvider?.GetService<CloudinaryImageStorageService>();
            if (cloudinaryService != null)
            {
                return await cloudinaryService.DeleteDentalImageAsync(fileReferenceOrPublicId, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete legacy Cloudinary asset {PublicId}. Proceeding gracefully.", fileReferenceOrPublicId);
        }

        return true;
    }

    public async Task<string> UploadAvatarAsync(
        long userId,
        Stream fileStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default)
    {
        if (fileStream == null || fileStream.Length == 0)
        {
            throw new ArgumentException("Tập tin hình ảnh không hợp lệ hoặc rỗng.");
        }

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var safeFileName = $"avatar_user_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        var avatarDir = Path.Combine(_storageRootPath, "avatars");
        EnsureDirectoryExists(avatarDir);

        var physicalPath = Path.Combine(avatarDir, safeFileName);
        if (fileStream.CanSeek && fileStream.Position != 0)
        {
            fileStream.Seek(0, SeekOrigin.Begin);
        }

        await using (var outputStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await fileStream.CopyToAsync(outputStream, ct);
        }

        return $"/uploads/avatars/{safeFileName}";
    }

    private string ResolveStorageRootPath()
    {
        var configuredPath = _configuration["MedicalImagingStorage:LocalPath"]
            ?? Environment.GetEnvironmentVariable("MEDICAL_IMAGING_LOCAL_PATH");

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        return Path.GetFullPath(Path.Combine(localAppData, "DentalCare", "MedicalImaging"));
    }

    private string ResolveAndValidatePhysicalPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Đường dẫn tệp không được để trống.", nameof(relativePath));
        }

        if (relativePath.Contains("..") || relativePath.Contains("//") || relativePath.Contains("\\\\"))
        {
            throw new SecurityException("Phát hiện đường dẫn không an toàn hoặc chứa ký tự chuyển hướng (path traversal).");
        }

        var rootWithSep = _storageRootPath.EndsWith(Path.DirectorySeparatorChar.ToString())
            ? _storageRootPath
            : _storageRootPath + Path.DirectorySeparatorChar;

        var normalized = relativePath.TrimStart('/', '\\')
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var combined = Path.GetFullPath(Path.Combine(_storageRootPath, normalized));

        if (!combined.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase) &&
            !combined.Equals(_storageRootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException("Đường dẫn tệp vượt ra ngoài thư mục lưu trữ được cho phép.");
        }

        return combined;
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private async Task<Stream?> GetRemoteLegacyStreamAsync(string fileUrl, CancellationToken ct)
    {
        try
        {
            // Delegate to Cloudinary service if registered
            var cloudinaryService = _serviceProvider?.GetService<CloudinaryImageStorageService>();
            if (cloudinaryService != null)
            {
                var stream = await cloudinaryService.GetImageStreamAsync(fileUrl, ct);
                if (stream != null) return stream;
            }

            // Direct HTTP stream fallback if Cloudinary credentials are not configured locally
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(15);
            var response = await http.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode)
            {
                var ms = new MemoryStream();
                await response.Content.CopyToAsync(ms, ct);
                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stream legacy remote asset from {Url}", fileUrl);
        }

        return null;
    }
}
