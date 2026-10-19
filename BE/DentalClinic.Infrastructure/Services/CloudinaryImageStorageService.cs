using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class CloudinaryImageStorageService : IImageStorageService
{
    private static readonly HttpClient _httpClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false, // CHỐNG SSRF: Ngăn chặn tự động chuyển hướng request đến các địa chỉ nội bộ
        ConnectTimeout = TimeSpan.FromSeconds(10)
    }) { Timeout = TimeSpan.FromSeconds(30) };

    private readonly Cloudinary? _cloudinary;
    private readonly ILogger<CloudinaryImageStorageService> _logger;
    private readonly bool _isConfigured;
    private readonly string? _cloudName;

    public CloudinaryImageStorageService(IConfiguration configuration, ILogger<CloudinaryImageStorageService> logger)
    {
        _logger = logger;

        var cloudName = configuration["Cloudinary:CloudName"] ?? configuration["CLOUDINARY_CLOUD_NAME"];
        var apiKey = configuration["Cloudinary:ApiKey"] ?? configuration["CLOUDINARY_API_KEY"];
        var apiSecret = configuration["Cloudinary:ApiSecret"] ?? configuration["CLOUDINARY_API_SECRET"];
        _cloudName = cloudName;

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

    public async Task<string> UploadImagingRecordAsync(long patientUserId, long clinicalEntryId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default)
    {
        // 1. Kiểm tra kích thước, extension, MIME type và chữ ký nội dung file (Magic bytes)
        var validation = Common.FileSignatureValidator.ValidateImagingFile(fileStream, originalFileName, contentType);
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.ErrorMessage ?? "Tập tin hình ảnh phim chụp không hợp lệ.");
        }

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var publicId = $"dental-clinic/imaging/patient_{patientUserId}/entry_{clinicalEntryId}_{Guid.NewGuid():N}";

        // 2. Nếu Cloudinary đã được cấu hình, upload lên Cloudinary với delivery type authenticated
        if (_isConfigured && _cloudinary != null)
        {
            if (validation.IsDicom)
            {
                // DICOM lưu dạng Raw resource, Authenticated delivery để bảo toàn 100% metadata y tế và bảo vệ truy cập
                var rawParams = new RawUploadParams
                {
                    File = new FileDescription(originalFileName, fileStream),
                    PublicId = publicId,
                    Type = "authenticated", // BẢO MẬT: Phim y tế DICOM chỉ truy cập qua signed URL/API
                    Overwrite = true,
                    Tags = $"imaging-record,patient-{patientUserId},entry-{clinicalEntryId},dicom"
                };

                var rawResult = await Task.Run(() => _cloudinary.Upload(rawParams), ct);
                if (rawResult.Error != null)
                {
                    _logger.LogError("Cloudinary DICOM upload failed: {Error}", rawResult.Error.Message);
                    throw new InvalidOperationException($"Lỗi tải file DICOM lên Cloudinary: {rawResult.Error.Message}");
                }

                var dicomUrl = rawResult.SecureUrl?.ToString() ?? rawResult.Url?.ToString();
                if (string.IsNullOrWhiteSpace(dicomUrl))
                {
                    throw new InvalidOperationException("Không nhận được URL file DICOM từ Cloudinary.");
                }

                return dicomUrl;
            }
            else
            {
                // Ảnh chẩn đoán nha khoa (JPEG, PNG, WebP) - Authenticated delivery
                var imageParams = new ImageUploadParams
                {
                    File = new FileDescription(originalFileName, fileStream),
                    PublicId = publicId,
                    Type = "authenticated", // BẢO MẬT: Delivery type authenticated, không thể truy cập công khai
                    Overwrite = true,
                    Invalidate = true,
                    Tags = $"imaging-record,patient-{patientUserId},entry-{clinicalEntryId}",
                    UseFilename = true,
                    UniqueFilename = true
                };

                var uploadResult = await _cloudinary.UploadAsync(imageParams, ct);
                if (uploadResult.Error != null)
                {
                    _logger.LogError("Cloudinary imaging upload failed: {Error}", uploadResult.Error.Message);
                    throw new InvalidOperationException($"Lỗi tải phim chụp lên Cloudinary: {uploadResult.Error.Message}");
                }

                var url = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString();
                if (string.IsNullOrWhiteSpace(url))
                {
                    throw new InvalidOperationException("Không nhận được URL ảnh từ Cloudinary.");
                }

                return url;
            }
        }

        // 3. Fallback lưu file cục bộ NGOÀI wwwroot (App_Data/uploads/imaging) nếu chưa cấu hình Cloudinary (cho môi trường dev & unit test)
        _logger.LogInformation("Cloudinary chưa được cấu hình. Thực hiện lưu trữ phim chụp vào thư mục bảo mật cục bộ ngoài wwwroot (App_Data/uploads/imaging).");

        var baseAllowed = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads", "imaging"));
        var localFolder = Path.GetFullPath(Path.Combine(baseAllowed, $"patient_{patientUserId}"));
        Directory.CreateDirectory(localFolder);

        var safeFileName = $"entry_{clinicalEntryId}_{Guid.NewGuid():N}{ext}";
        var localFilePath = Path.GetFullPath(Path.Combine(localFolder, safeFileName));

        // Kiểm tra chống Path Traversal với ranh giới thư mục an toàn
        if (!IsPathSafelyInsideBase(localFilePath, baseAllowed))
        {
            throw new InvalidOperationException("Đường dẫn lưu trữ tập tin không hợp lệ.");
        }

        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        using (var destStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(destStream, ct);
        }

        return $"App_Data/uploads/imaging/patient_{patientUserId}/{safeFileName}";
    }

    public async Task<bool> DeleteImageAsync(string fileUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return true;
        }

        try
        {
            // Trường hợp 1: Asset trên Cloudinary
            if (TryValidateCloudinaryUrl(fileUrl, _cloudName, out _, out _))
            {
                if (!_isConfigured || _cloudinary == null)
                {
                    _logger.LogWarning("Không thể xóa asset Cloudinary vì dịch vụ chưa được cấu hình: {Url}", fileUrl);
                    return false;
                }

                var publicId = ExtractCloudinaryPublicId(fileUrl);
                if (string.IsNullOrWhiteSpace(publicId))
                {
                    _logger.LogWarning("Không thể trích xuất PublicId từ Cloudinary URL: {Url}", fileUrl);
                    return false;
                }

                var isRaw = fileUrl.Contains("/raw/", StringComparison.OrdinalIgnoreCase) || fileUrl.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase);
                var isAuth = fileUrl.Contains("/authenticated/", StringComparison.OrdinalIgnoreCase);

                var delParams = new DeletionParams(publicId)
                {
                    ResourceType = isRaw ? ResourceType.Raw : ResourceType.Image,
                    Type = isAuth ? "authenticated" : "upload"
                };

                var delResult = await _cloudinary.DestroyAsync(delParams);
                if (delResult.Result != "ok" && delResult.Result != "not found")
                {
                    _logger.LogWarning("Cloudinary DestroyAsync returned: {Result} for {PublicId}", delResult.Result, publicId);
                }
                return true;
            }

            // Trường hợp 2: File cục bộ bảo mật (App_Data ngoài wwwroot)
            if (fileUrl.Contains("App_Data", StringComparison.OrdinalIgnoreCase) || fileUrl.StartsWith("local://", StringComparison.OrdinalIgnoreCase))
            {
                var cleanPath = fileUrl.Replace("local://", "").TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var baseAllowed = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads", "imaging"));
                var localFilePath = Path.GetFullPath(cleanPath.StartsWith("App_Data", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(Directory.GetCurrentDirectory(), cleanPath)
                    : Path.Combine(baseAllowed, cleanPath));

                if (IsPathSafelyInsideBase(localFilePath, baseAllowed) && File.Exists(localFilePath))
                {
                    File.Delete(localFilePath);
                    _logger.LogInformation("Đã xóa file cục bộ App_Data thành công: {Path}", localFilePath);
                }
                return true;
            }

            // Trường hợp 3: File cục bộ cũ (legacy wwwroot)
            if (fileUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) || fileUrl.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = fileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var baseAllowed = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "imaging"));
                var localFilePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath));

                if (IsPathSafelyInsideBase(localFilePath, baseAllowed) && File.Exists(localFilePath))
                {
                    File.Delete(localFilePath);
                    _logger.LogInformation("Đã xóa file cục bộ legacy wwwroot thành công: {Path}", localFilePath);
                }
                return true;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa asset hình ảnh: {Url}", fileUrl);
            return false;
        }
    }

    public async Task<StorageFileStreamResult?> GetImageStreamAsync(string fileUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return null;
        }

        try
        {
            // Case 1: Secure Local Storage (App_Data ngoài wwwroot)
            if (fileUrl.Contains("App_Data", StringComparison.OrdinalIgnoreCase) || fileUrl.StartsWith("local://", StringComparison.OrdinalIgnoreCase))
            {
                var cleanPath = fileUrl.Replace("local://", "").TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var baseAllowed = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads", "imaging"));
                var localFilePath = Path.GetFullPath(cleanPath.StartsWith("App_Data", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(Directory.GetCurrentDirectory(), cleanPath)
                    : Path.Combine(baseAllowed, cleanPath));

                if (!IsPathSafelyInsideBase(localFilePath, baseAllowed))
                {
                    _logger.LogWarning("Phát hiện nguy cơ Path Traversal khi truy cập file cục bộ: {Url}", fileUrl);
                    return null;
                }

                if (!File.Exists(localFilePath))
                {
                    _logger.LogWarning("Không tìm thấy file cục bộ tại đường dẫn: {Path}", localFilePath);
                    return null;
                }

                var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var ext = Path.GetExtension(localFilePath).ToLowerInvariant();
                return new StorageFileStreamResult
                {
                    Stream = fileStream,
                    ContentType = GetContentTypeFromExtension(ext),
                    FileName = Path.GetFileName(localFilePath),
                    FileLength = fileStream.Length
                };
            }

            // Case 2: Legacy Local Storage (wwwroot)
            if (fileUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) || fileUrl.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = fileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var baseAllowed = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "imaging"));
                var localFilePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath));

                if (!IsPathSafelyInsideBase(localFilePath, baseAllowed))
                {
                    _logger.LogWarning("Phát hiện nguy cơ Path Traversal với legacy file: {Url}", fileUrl);
                    return null;
                }

                if (!File.Exists(localFilePath))
                {
                    _logger.LogWarning("Không tìm thấy file cục bộ legacy tại: {Path}", localFilePath);
                    return null;
                }

                var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var ext = Path.GetExtension(localFilePath).ToLowerInvariant();
                return new StorageFileStreamResult
                {
                    Stream = fileStream,
                    ContentType = GetContentTypeFromExtension(ext),
                    FileName = Path.GetFileName(localFilePath),
                    FileLength = fileStream.Length
                };
            }

            // Case 3: Cloudinary Storage (Xác thực nguồn tin cậy & phòng chống SSRF nghiêm ngặt)
            if (TryValidateCloudinaryUrl(fileUrl, _cloudName, out var validUri, out var urlValidationError))
            {
                if (!_isConfigured || _cloudinary == null)
                {
                    _logger.LogWarning("Cloudinary chưa được cấu hình credentials để tải asset: {Url}", fileUrl);
                    return null;
                }

                var publicId = ExtractCloudinaryPublicId(fileUrl);
                if (string.IsNullOrWhiteSpace(publicId))
                {
                    _logger.LogWarning("Không thể trích xuất PublicId từ Cloudinary URL: {Url}", fileUrl);
                    return null;
                }

                var isRaw = fileUrl.Contains("/raw/", StringComparison.OrdinalIgnoreCase) || fileUrl.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase);
                var isAuth = fileUrl.Contains("/authenticated/", StringComparison.OrdinalIgnoreCase);

                // Tái tạo downloadUrl từ SDK an toàn thay vì gọi trực tiếp chuỗi URL người dùng cung cấp
                string downloadUrl;
                if (isAuth)
                {
                    downloadUrl = _cloudinary.Api.Url.ResourceType(isRaw ? "raw" : "image").Action("authenticated").Signed(true).BuildUrl(publicId);
                }
                else
                {
                    downloadUrl = _cloudinary.Api.Url.ResourceType(isRaw ? "raw" : "image").Action("upload").BuildUrl(publicId);
                }

                var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);

                // Chặn theo chuyển hướng (SSRF Redirect Protection)
                if (response.StatusCode == HttpStatusCode.MovedPermanently ||
                    response.StatusCode == HttpStatusCode.Found ||
                    response.StatusCode == HttpStatusCode.SeeOther ||
                    response.StatusCode == HttpStatusCode.TemporaryRedirect ||
                    response.StatusCode == (HttpStatusCode)308)
                {
                    _logger.LogWarning("Phát hiện chuyển hướng HTTP {StatusCode} từ Cloudinary. Từ chối redirect để ngăn ngừa SSRF.", response.StatusCode);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Không thể tải file từ Cloudinary. Status: {StatusCode}, Url: {Url}", response.StatusCode, downloadUrl);
                    return null;
                }

                var stream = await response.Content.ReadAsStreamAsync(ct);
                var ext = isRaw ? ".dcm" : (Path.GetExtension(new Uri(downloadUrl).AbsolutePath) ?? ".jpg");
                var contentType = response.Content.Headers.ContentType?.MediaType ?? GetContentTypeFromExtension(ext);
                var fileName = $"{publicId.Split('/').LastOrDefault() ?? "imaging"}{ext}";

                return new StorageFileStreamResult
                {
                    Stream = stream,
                    ContentType = contentType,
                    FileName = fileName,
                    FileLength = response.Content.Headers.ContentLength
                };
            }
            else if (fileUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase) || fileUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Từ chối truy xuất URL không an toàn hoặc có nguy cơ SSRF: {Error}. Url: {Url}", urlValidationError, fileUrl);
                return null;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi truy xuất stream hình ảnh từ {Url}", fileUrl);
            return null;
        }
    }

    public bool ValidateCloudinaryUrl(string url)
    {
        return TryValidateCloudinaryUrl(url, _cloudName, out _, out _);
    }

    public static bool TryValidateCloudinaryUrl(string fileUrl, string? expectedCloudName, out Uri? validUri, out string? errorMessage)
    {
        validUri = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            errorMessage = "URL rỗng hoặc không hợp lệ.";
            return false;
        }

        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            errorMessage = "URL không đúng định dạng URI tuyệt đối.";
            return false;
        }

        // 1. Bắt buộc giao thức HTTPS
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            errorMessage = "Chỉ chấp nhận giao thức HTTPS an toàn.";
            return false;
        }

        // 2. Cổng chuẩn HTTPS (443)
        if (!uri.IsDefaultPort)
        {
            errorMessage = "Chỉ chấp nhận cổng HTTPS mặc định (443).";
            return false;
        }

        // 3. Chặn các địa chỉ Loopback, Private network, Link-Local metadata
        if (IsPrivateOrRestrictedAddress(uri.Host))
        {
            errorMessage = "Từ chối truy cập loopback, cloud metadata hoặc dải mạng riêng tư.";
            return false;
        }

        // 4. Hostname bắt buộc là res.cloudinary.com chính xác
        if (!string.Equals(uri.Host, "res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Hostname không thuộc máy chủ phân phối Cloudinary (res.cloudinary.com).";
            return false;
        }

        // 5. Kiểm tra CloudName khớp cấu hình hệ thống
        if (!string.IsNullOrWhiteSpace(expectedCloudName))
        {
            var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || !string.Equals(segments[0], expectedCloudName, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"CloudName trong URL không khớp với cấu hình hệ thống (kỳ vọng: {expectedCloudName}).";
                return false;
            }
        }

        validUri = uri;
        return true;
    }

    public static bool IsPrivateOrRestrictedAddress(string hostOrIp)
    {
        if (string.IsNullOrWhiteSpace(hostOrIp)) return true;

        if (string.Equals(hostOrIp, "localhost", StringComparison.OrdinalIgnoreCase)) return true;

        if (IPAddress.TryParse(hostOrIp, out var ip))
        {
            if (IPAddress.IsLoopback(ip)) return true;
            if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return true;

            var bytes = ip.GetAddressBytes();
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                // 10.0.0.0/8
                if (bytes[0] == 10) return true;
                // 172.16.0.0/12
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168) return true;
                // 169.254.0.0/16 (Link-local & Cloud Metadata 169.254.169.254)
                if (bytes[0] == 169 && bytes[1] == 254) return true;
                // 127.0.0.0/8
                if (bytes[0] == 127) return true;
                // 0.0.0.0/8
                if (bytes[0] == 0) return true;
                // 100.64.0.0/10 (CGNAT)
                if (bytes[0] == 100 && (bytes[1] & 0xC0) == 64) return true;
            }
            else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                if (ip.IsIPv6LinkLocal || (bytes[0] & 0xFE) == 0xFC) return true;
            }

            return true;
        }

        return false;
    }

    public static bool IsPathSafelyInsideBase(string candidatePath, string baseDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(baseDirectory))
            {
                return false;
            }

            var fullBase = EnsureTrailingSeparator(baseDirectory);
            var fullCandidate = Path.GetFullPath(candidatePath);

            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!fullCandidate.StartsWith(fullBase, comparison))
            {
                return false;
            }

            // Kiểm tra Symlink / Reparse Point
            if (File.Exists(fullCandidate))
            {
                var fileInfo = new FileInfo(fullCandidate);
                if (fileInfo.LinkTarget != null)
                {
                    var target = fileInfo.ResolveLinkTarget(true);
                    if (target != null)
                    {
                        var targetFull = Path.GetFullPath(target.FullName);
                        if (!targetFull.StartsWith(fullBase, comparison))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        var full = Path.GetFullPath(path);
        if (!full.EndsWith(Path.DirectorySeparatorChar.ToString()) && !full.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
        {
            full += Path.DirectorySeparatorChar;
        }
        return full;
    }

    private static string GetContentTypeFromExtension(string ext) => ext.ToLowerInvariant() switch
    {
        ".dcm" => "application/dicom",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };

    private static string ExtractCloudinaryPublicId(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var uploadIndex = path.IndexOf("/upload/", StringComparison.OrdinalIgnoreCase);
            var authIndex = path.IndexOf("/authenticated/", StringComparison.OrdinalIgnoreCase);
            var markerIndex = uploadIndex >= 0 ? uploadIndex : authIndex;
            var markerLength = uploadIndex >= 0 ? "/upload/".Length : "/authenticated/".Length;
            if (markerIndex < 0) return string.Empty;

            var afterMarker = path.Substring(markerIndex + markerLength);

            // Bỏ signature nếu có: "s--xyz--/..."
            if (afterMarker.StartsWith("s--", StringComparison.OrdinalIgnoreCase))
            {
                var nextSlash = afterMarker.IndexOf('/', 3);
                if (nextSlash > 0)
                {
                    afterMarker = afterMarker.Substring(nextSlash + 1);
                }
            }

            // Bỏ version nếu có: "v1234567890/..."
            if (afterMarker.StartsWith("v") && afterMarker.IndexOf('/') > 0)
            {
                var slashIndex = afterMarker.IndexOf('/');
                if (long.TryParse(afterMarker.Substring(1, slashIndex - 1), out _))
                {
                    afterMarker = afterMarker.Substring(slashIndex + 1);
                }
            }

            // Bỏ phần mở rộng cuối cùng nếu là image
            var lastDot = afterMarker.LastIndexOf('.');
            if (lastDot > 0 && !afterMarker.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase))
            {
                afterMarker = afterMarker.Substring(0, lastDot);
            }

            return Uri.UnescapeDataString(afterMarker);
        }
        catch
        {
            return string.Empty;
        }
    }
}
