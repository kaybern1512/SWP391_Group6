using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class SecureMedicalImageStorageService : IMedicalImageStorageService
{
    private readonly string _storageDirectory;
    private readonly ILogger<SecureMedicalImageStorageService> _logger;

    private const long MaxFileSizeBytes = 15 * 1024 * 1024; // 15 MB
    private const int MaxDimensionPixels = 8000;
    private const int MinDimensionPixels = 10;

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] AllowedMimeTypes = { "image/jpeg", "image/jpg", "image/png", "image/webp" };

    public SecureMedicalImageStorageService(
        IConfiguration configuration,
        ILogger<SecureMedicalImageStorageService> logger)
    {
        _logger = logger;

        var configuredPath = configuration["Storage:MedicalImagesPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            _storageDirectory = Path.GetFullPath(configuredPath);
        }
        else
        {
            _storageDirectory = Path.Combine(AppContext.BaseDirectory, "App_Data", "MedicalImages");
        }

        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public async Task<MedicalImageUploadResult> SaveImageAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default)
    {
        if (stream == null || stream.Length == 0)
        {
            return new MedicalImageUploadResult
            {
                Success = false,
                ErrorMessage = "Tập tin hình ảnh rỗng hoặc không tồn tại."
            };
        }

        if (stream.Length > MaxFileSizeBytes)
        {
            return new MedicalImageUploadResult
            {
                Success = false,
                ErrorMessage = $"Kích thước tệp vượt quá giới hạn cho phép (tối đa {MaxFileSizeBytes / (1024 * 1024)} MB)."
            };
        }

        var ext = Path.GetExtension(originalFileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            return new MedicalImageUploadResult
            {
                Success = false,
                ErrorMessage = "Định dạng tệp không được hỗ trợ. Chỉ chấp nhận các định dạng ảnh JPEG, PNG hoặc WEBP."
            };
        }

        var normalizedMime = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(normalizedMime) || !AllowedMimeTypes.Contains(normalizedMime))
        {
            return new MedicalImageUploadResult
            {
                Success = false,
                ErrorMessage = "MIME type của tệp không hợp lệ cho hình ảnh y tế."
            };
        }

        // Đọc toàn bộ nội dung stream vào memory để kiểm tra magic bytes và dimensions
        byte[] fileBytes;
        if (stream is MemoryStream ms && stream.Position == 0)
        {
            fileBytes = ms.ToArray();
        }
        else
        {
            using var memory = new MemoryStream();
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
            await stream.CopyToAsync(memory, ct);
            fileBytes = memory.ToArray();
        }

        // Kiểm tra xem có phải định dạng DICOM không (DICOM có prefix 'DICM' tại offset 128)
        if (IsDicomFile(fileBytes))
        {
            return new MedicalImageUploadResult
            {
                Success = false,
                ErrorMessage = "Định dạng DICOM chưa được hỗ trợ trực tiếp. Vui lòng xuất sang định dạng JPEG hoặc PNG trước khi tải lên."
            };
        }

        // Kiểm tra magic bytes
        if (!ValidateMagicBytes(fileBytes, ext))
        {
            return new MedicalImageUploadResult
            {
                Success = false,
                ErrorMessage = "Nội dung tệp không khớp với phần mở rộng ảnh (Phát hiện tệp giả mạo hoặc bị hỏng)."
            };
        }

        // Kiểm tra kích thước điểm ảnh (Width, Height) chống decompression bomb
        var (isValidDim, width, height, dimError) = TryValidateImageDimensions(fileBytes, ext);
        if (!isValidDim)
        {
            return new MedicalImageUploadResult
            {
                Success = false,
                ErrorMessage = dimError ?? "Không thể xác thực thông số kích thước hình ảnh an toàn."
            };
        }

        // Chuẩn hóa tên tệp lưu trữ ngẫu nhiên (chống path traversal & overwrite)
        var safeFileName = $"xray_{Guid.NewGuid():N}{ext}";
        var destinationPath = Path.Combine(_storageDirectory, safeFileName);

        try
        {
            await File.WriteAllBytesAsync(destinationPath, fileBytes, ct);

            var storedIdentifier = $"secure://med-img/{safeFileName}";
            return new MedicalImageUploadResult
            {
                Success = true,
                StoredIdentifier = storedIdentifier,
                ContentType = GetCanonicalContentType(ext),
                FileSizeBytes = fileBytes.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi ghi tệp hình ảnh vào hệ thống lưu trữ tại {Path}", destinationPath);
            return new MedicalImageUploadResult
            {
                Success = false,
                ErrorMessage = "Lỗi hệ thống lưu trữ tệp khi ghi hình ảnh."
            };
        }
    }

    public Task<MedicalImageStreamResult?> GetImageStreamAsync(string storedIdentifier, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storedIdentifier))
        {
            return Task.FromResult<MedicalImageStreamResult?>(null);
        }

        var fileName = storedIdentifier.Replace("secure://med-img/", "").Trim();
        fileName = Path.GetFileName(fileName); // Sanitize path traversal

        var fullPath = Path.Combine(_storageDirectory, fileName);
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Không tìm thấy tệp ảnh lưu trữ tại đường dẫn {Path}", fullPath);
            return Task.FromResult<MedicalImageStreamResult?>(null);
        }

        var ext = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";
        var contentType = GetCanonicalContentType(ext);

        var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var result = new MedicalImageStreamResult
        {
            Stream = fileStream,
            ContentType = contentType,
            FileName = fileName
        };

        return Task.FromResult<MedicalImageStreamResult?>(result);
    }

    public Task<bool> DeleteImageAsync(string storedIdentifier, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storedIdentifier))
        {
            return Task.FromResult(false);
        }

        var fileName = storedIdentifier.Replace("secure://med-img/", "").Trim();
        fileName = Path.GetFileName(fileName);

        var fullPath = Path.Combine(_storageDirectory, fileName);
        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa tệp ảnh tại {Path}", fullPath);
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    private static bool IsDicomFile(byte[] bytes)
    {
        if (bytes.Length >= 132)
        {
            // DICOM standard preamble: 128 bytes followed by 'D', 'I', 'C', 'M'
            if (bytes[128] == 'D' && bytes[129] == 'I' && bytes[130] == 'C' && bytes[131] == 'M')
            {
                return true;
            }
        }
        return false;
    }

    private static bool ValidateMagicBytes(byte[] bytes, string ext)
    {
        if (bytes.Length < 12) return false;

        return ext switch
        {
            ".jpg" or ".jpeg" => bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            ".png" => bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                      bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
            ".webp" => bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && // 'RIFF'
                       bytes[8] == 0x57 && bytes[9] == 0x41 && bytes[10] == 0x42 && bytes[11] == 0x50, // 'WEBP'
            _ => false
        };
    }

    private static (bool IsValid, int Width, int Height, string? Error) TryValidateImageDimensions(byte[] bytes, string ext)
    {
        try
        {
            int width = 0;
            int height = 0;

            if (ext == ".png")
            {
                // PNG: IHDR chunk bắt đầu tại byte 12 (4 bytes length = 13, 4 bytes type = 'IHDR', 4 bytes width, 4 bytes height)
                if (bytes.Length < 24)
                {
                    return (false, 0, 0, "Dữ liệu tệp PNG quá ngắn.");
                }
                width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
                height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
            }
            else if (ext == ".jpg" || ext == ".jpeg")
            {
                // Parse JPEG markers để tìm SOF (Start of Frame)
                int offset = 2;
                while (offset < bytes.Length - 8)
                {
                    if (bytes[offset] != 0xFF)
                    {
                        offset++;
                        continue;
                    }

                    byte marker = bytes[offset + 1];
                    // Các marker SOF: C0 (Baseline), C1 (Extended), C2 (Progressive), C3 (Lossless)
                    if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3)
                    {
                        height = (bytes[offset + 5] << 8) | bytes[offset + 6];
                        width = (bytes[offset + 7] << 8) | bytes[offset + 8];
                        break;
                    }

                    // Nhảy qua marker segment
                    int length = (bytes[offset + 2] << 8) | bytes[offset + 3];
                    offset += 2 + length;
                }
            }
            else if (ext == ".webp")
            {
                if (bytes.Length >= 30)
                {
                    string format = Encoding.ASCII.GetString(bytes, 12, 4);
                    if (format == "VP8 ")
                    {
                        // Lossy VP8
                        width = ((bytes[27] & 0x3F) << 8) | bytes[26];
                        height = ((bytes[29] & 0x3F) << 8) | bytes[28];
                    }
                    else if (format == "VP8L")
                    {
                        // Lossless VP8L
                        uint bits = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(21, 4));
                        width = (int)(bits & 0x3FFF) + 1;
                        height = (int)((bits >> 14) & 0x3FFF) + 1;
                    }
                    else if (format == "VP8X")
                    {
                        // Extended VP8X
                        width = 1 + (bytes[24] | (bytes[25] << 8) | (bytes[26] << 16));
                        height = 1 + (bytes[27] | (bytes[28] << 8) | (bytes[29] << 16));
                    }
                }
            }

            if (width < MinDimensionPixels || height < MinDimensionPixels)
            {
                return (false, width, height, "Kích thước hình ảnh quá nhỏ (Tối thiểu 10x10 px).");
            }

            if (width > MaxDimensionPixels || height > MaxDimensionPixels)
            {
                return (false, width, height, $"Kích thước hình ảnh vượt quá giới hạn an toàn tối đa ({MaxDimensionPixels}x{MaxDimensionPixels} px).");
            }

            return (true, width, height, null);
        }
        catch (Exception)
        {
            return (false, 0, 0, "Không thể đọc dữ liệu kích thước hình ảnh từ header của tệp.");
        }
    }

    private static string GetCanonicalContentType(string ext)
    {
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
