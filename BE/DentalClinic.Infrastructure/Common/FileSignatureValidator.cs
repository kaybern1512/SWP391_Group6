using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DentalClinic.Infrastructure.Common;

public class FileValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string DetectedFormat { get; set; } = string.Empty;
    public bool IsDicom { get; set; }

    public static FileValidationResult Success(string format, bool isDicom = false) =>
        new() { IsValid = true, DetectedFormat = format, IsDicom = isDicom };

    public static FileValidationResult Fail(string message) =>
        new() { IsValid = false, ErrorMessage = message };
}

public static class FileSignatureValidator
{
    public const long MaxImagingFileSize = 15 * 1024 * 1024; // 15 MB

    private static readonly byte[] JpegHeader = new byte[] { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] RiffHeader = new byte[] { 0x52, 0x49, 0x46, 0x46 }; // "RIFF"
    private static readonly byte[] WebpHeader = new byte[] { 0x57, 0x45, 0x42, 0x50 }; // "WEBP"
    private static readonly byte[] DicomMagic = Encoding.ASCII.GetBytes("DICM"); // 0x44, 0x49, 0x43, 0x4D at offset 128

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".dcm" };

    public static FileValidationResult ValidateImagingFile(Stream stream, string originalFileName, string? contentType)
    {
        if (stream == null || stream.Length == 0)
        {
            return FileValidationResult.Fail("Tập tin hình ảnh phim chụp rỗng hoặc không hợp lệ.");
        }

        if (stream.Length > MaxImagingFileSize)
        {
            return FileValidationResult.Fail($"Kích thước tập tin ({stream.Length / (1024 * 1024):F1} MB) vượt quá giới hạn tối đa cho phép là 15 MB.");
        }

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            return FileValidationResult.Fail($"Phần mở rộng '{ext}' không được hỗ trợ. Hệ thống chỉ chấp nhận .jpg, .jpeg, .png, .webp và file y tế .dcm.");
        }

        var originalPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            // Đọc 132 bytes để kiểm tra chữ ký ảnh thông thường hoặc DICOM (preamble 128 + 4 bytes DICM)
            var buffer = new byte[132];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);

            if (bytesRead < 4)
            {
                return FileValidationResult.Fail("Tập tin bị lỗi hoặc không có nội dung hợp lệ.");
            }

            // 1. Kiểm tra JPEG
            if (bytesRead >= 3 && buffer[0] == JpegHeader[0] && buffer[1] == JpegHeader[1] && buffer[2] == JpegHeader[2])
            {
                if (ext != ".jpg" && ext != ".jpeg")
                {
                    return FileValidationResult.Fail("Nội dung tập tin là JPEG nhưng phần mở rộng không khớp.");
                }
                return FileValidationResult.Success("JPEG");
            }

            // 2. Kiểm tra PNG
            if (bytesRead >= 8 && buffer.Take(8).SequenceEqual(PngHeader))
            {
                if (ext != ".png")
                {
                    return FileValidationResult.Fail("Nội dung tập tin là PNG nhưng phần mở rộng không khớp.");
                }
                return FileValidationResult.Success("PNG");
            }

            // 3. Kiểm tra WEBP (RIFF .... WEBP)
            if (bytesRead >= 12 &&
                buffer.Take(4).SequenceEqual(RiffHeader) &&
                buffer.Skip(8).Take(4).SequenceEqual(WebpHeader))
            {
                if (ext != ".webp")
                {
                    return FileValidationResult.Fail("Nội dung tập tin là WebP nhưng phần mở rộng không khớp.");
                }
                return FileValidationResult.Success("WEBP");
            }

            // 4. Kiểm tra DICOM (Part 10 format: 128 bytes preamble + "DICM")
            if (bytesRead >= 132 && buffer.Skip(128).Take(4).SequenceEqual(DicomMagic))
            {
                if (ext != ".dcm")
                {
                    return FileValidationResult.Fail("Nội dung tập tin là DICOM y tế nhưng phần mở rộng không phải .dcm.");
                }
                return FileValidationResult.Success("DICOM", isDicom: true);
            }

            // Trường hợp file .dcm nhưng là raw stream không có chuẩn preamble Part 10
            if (ext == ".dcm")
            {
                var ct = contentType?.ToLowerInvariant() ?? string.Empty;
                if (ct.Contains("dicom") || ct.Contains("octet-stream"))
                {
                    return FileValidationResult.Success("DICOM", isDicom: true);
                }
            }

            return FileValidationResult.Fail("Chữ ký nội dung tập tin (magic bytes) không khớp với các định dạng ảnh nha khoa được hỗ trợ (.jpg, .png, .webp, .dcm).");
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }
}
