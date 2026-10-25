using System;
using System.IO;
using System.Linq;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.MedicalRecords.DTOs;

namespace DentalClinic.Application.Common.Validation;

/// <summary>
/// Secure input validator for dental imaging files.
/// Validates file size, extension, MIME type, and deep binary magic-byte signatures
/// for JPEG, PNG, and DICOM (PS 3.10) dental files.
/// </summary>
public class DentalImageFileValidator : IDentalImageFileValidator
{
    public const long DefaultMaxFileSizeBytes = 15 * 1024 * 1024; // 15 MB

    public static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".dcm", ".dicom" };

    // Standard magic byte definitions
    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] DicomMagic = { 0x44, 0x49, 0x43, 0x4D }; // ASCII 'DICM' at byte offset 128

    private readonly long _maxFileSizeBytes;

    public DentalImageFileValidator(long maxFileSizeBytes = DefaultMaxFileSizeBytes)
    {
        _maxFileSizeBytes = maxFileSizeBytes > 0 ? maxFileSizeBytes : DefaultMaxFileSizeBytes;
    }

    public ImageFileValidationResult Validate(DentalImageFileInput? file)
    {
        if (file == null)
        {
            return ImageFileValidationResult.Failure("Tập tin hình ảnh không được để trống.");
        }

        return Validate(file.Content, file.FileName, file.ContentType, file.Length);
    }

    public ImageFileValidationResult Validate(Stream? fileStream, string? fileName, string? declaredContentType, long? declaredLength = null)
    {
        // 1. Basic null and empty checks
        if (fileStream == null)
        {
            return ImageFileValidationResult.Failure("Tập tin hình ảnh không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return ImageFileValidationResult.Failure("Tên tập tin không được để trống.");
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            return ImageFileValidationResult.Failure(
                $"Định dạng tệp '{ext}' không được hỗ trợ. Chỉ chấp nhận các tệp hình ảnh nha khoa: {string.Join(", ", AllowedExtensions)}.");
        }

        // 2. File size validation
        long actualLength = Math.Max(declaredLength ?? 0, fileStream.CanSeek ? fileStream.Length : 0);

        if (actualLength <= 0)
        {
            return ImageFileValidationResult.Failure("Tập tin rỗng hoặc không có dữ liệu nội dung.");
        }

        if (actualLength > _maxFileSizeBytes)
        {
            var maxMb = _maxFileSizeBytes / (1024 * 1024);
            return ImageFileValidationResult.Failure(
                $"Kích thước tập tin ({actualLength / 1024 / 1024} MB) vượt quá giới hạn cho phép ({maxMb} MB).");
        }

        // 3. MIME type consistency check
        var mime = (declaredContentType ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(mime) && !IsMimeTypeAcceptable(ext, mime))
        {
            return ImageFileValidationResult.Failure(
                $"Loại MIME '{declaredContentType}' không phù hợp với phần mở rộng '{ext}' của tập tin hình ảnh nha khoa.");
        }

        // 4. Binary signature (magic bytes) validation
        byte[] headerBytes;
        try
        {
            headerBytes = ReadHeaderBytes(fileStream, 132);
        }
        catch (Exception ex)
        {
            return ImageFileValidationResult.Failure($"Không thể đọc dữ liệu nhị phân của tập tin: {ex.Message}");
        }

        if (headerBytes.Length < 3)
        {
            return ImageFileValidationResult.Failure("Nội dung tệp quá ngắn để xác thực chữ ký nhị phân.");
        }

        // Check JPEG
        if (ext is ".jpg" or ".jpeg")
        {
            if (!IsJpeg(headerBytes))
            {
                return ImageFileValidationResult.Failure(
                    "Nội dung tệp không khớp với chữ ký nhị phân của ảnh JPEG (thiếu SOI marker 0xFF 0xD8 0xFF).");
            }

            return ImageFileValidationResult.Success("JPEG", "image/jpeg");
        }

        // Check PNG
        if (ext == ".png")
        {
            if (!IsPng(headerBytes))
            {
                return ImageFileValidationResult.Failure(
                    "Nội dung tệp không khớp với chữ ký nhị phân của ảnh PNG.");
            }

            return ImageFileValidationResult.Success("PNG", "image/png");
        }

        // Check DICOM (.dcm, .dicom)
        if (ext is ".dcm" or ".dicom")
        {
            // First check if it's actually a disguised JPEG or PNG
            if (IsJpeg(headerBytes))
            {
                return ImageFileValidationResult.Failure(
                    "Tệp mang phần mở rộng DICOM nhưng nội dung thực tế là ảnh JPEG thông thường. Vui lòng sử dụng tệp chuẩn DICOM PS 3.10 hoặc tải lên dưới dạng .jpg/.jpeg.");
            }

            if (IsPng(headerBytes))
            {
                return ImageFileValidationResult.Failure(
                    "Tệp mang phần mở rộng DICOM nhưng nội dung thực tế là ảnh PNG thông thường. Vui lòng sử dụng tệp chuẩn DICOM PS 3.10 hoặc tải lên dưới dạng .png.");
            }

            // Standard DICOM Part 10 (PS 3.10) requires 128-byte preamble + 4-byte 'DICM' prefix
            if (!IsDicomPart10(headerBytes))
            {
                return ImageFileValidationResult.Failure(
                    "Tệp DICOM không hợp lệ hoặc thiếu tiêu chuẩn PS 3.10 (phải chứa 128-byte preamble và tiền tố 'DICM' tại vị trí byte 128).");
            }

            return ImageFileValidationResult.Success("DICOM", "application/dicom");
        }

        return ImageFileValidationResult.Failure("Không thể xác định định dạng tập tin hình ảnh nha khoa.");
    }

    private static bool IsMimeTypeAcceptable(string ext, string mime)
    {
        // Allow general application/octet-stream as many client browsers/OSs do not recognize .dcm
        if (mime is "application/octet-stream" or "application/x-binary")
        {
            return true;
        }

        return ext switch
        {
            ".jpg" or ".jpeg" => mime is "image/jpeg" or "image/pjpeg" or "image/jpg",
            ".png" => mime is "image/png" or "image/x-png",
            ".dcm" or ".dicom" => mime is "application/dicom" or "image/dicom" or "image/x-dicom",
            _ => false
        };
    }

    private static byte[] ReadHeaderBytes(Stream stream, int count)
    {
        var buffer = new byte[count];
        int bytesRead = 0;

        if (stream.CanSeek)
        {
            var originalPos = stream.Position;
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                while (bytesRead < count)
                {
                    int n = stream.Read(buffer, bytesRead, count - bytesRead);
                    if (n <= 0) break;
                    bytesRead += n;
                }
            }
            finally
            {
                stream.Seek(originalPos, SeekOrigin.Begin);
            }
        }
        else
        {
            // If stream is not seekable, read up to count
            while (bytesRead < count)
            {
                int n = stream.Read(buffer, bytesRead, count - bytesRead);
                if (n <= 0) break;
                bytesRead += n;
            }
        }

        if (bytesRead == count)
        {
            return buffer;
        }

        var result = new byte[bytesRead];
        Array.Copy(buffer, result, bytesRead);
        return result;
    }

    private static bool IsJpeg(byte[] bytes)
    {
        if (bytes.Length < 3) return false;
        return bytes[0] == JpegHeader[0] &&
               bytes[1] == JpegHeader[1] &&
               bytes[2] == JpegHeader[2];
    }

    private static bool IsPng(byte[] bytes)
    {
        if (bytes.Length < PngHeader.Length) return false;
        for (int i = 0; i < PngHeader.Length; i++)
        {
            if (bytes[i] != PngHeader[i]) return false;
        }
        return true;
    }

    private static bool IsDicomPart10(byte[] bytes)
    {
        // Must contain at least 128 preamble bytes + 4 'DICM' bytes = 132 bytes
        if (bytes.Length < 132) return false;

        return bytes[128] == DicomMagic[0] &&
               bytes[129] == DicomMagic[1] &&
               bytes[130] == DicomMagic[2] &&
               bytes[131] == DicomMagic[3];
    }
}
