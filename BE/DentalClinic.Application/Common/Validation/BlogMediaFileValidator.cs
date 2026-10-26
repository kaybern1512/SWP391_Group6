using System;
using System.IO;
using System.Linq;
using System.Text;
using DentalClinic.Application.Common.Interfaces;

namespace DentalClinic.Application.Common.Validation;

public class BlogMediaFileValidator : IBlogMediaFileValidator
{
    public const long MaxImageSizeBytes = 5 * 1024 * 1024;    // 5 MB
    public const long MaxVideoSizeBytes = 50 * 1024 * 1024;   // 50 MB

    public static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    public static readonly string[] AllowedVideoExtensions = { ".mp4", ".webm" };

    // Magic bytes
    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] WebmHeader = { 0x1A, 0x45, 0xDF, 0xA3 };
    private static readonly byte[] ExeHeader = { 0x4D, 0x5A }; // 'MZ' DOS/PE

    public BlogMediaValidationResult Validate(Stream? fileStream, string? fileName, string? declaredContentType, long? declaredLength = null)
    {
        if (fileStream == null)
        {
            return BlogMediaValidationResult.Failure("Tập tin phương tiện không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BlogMediaValidationResult.Failure("Tên tập tin không được để trống.");
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var isImageExt = AllowedImageExtensions.Contains(ext);
        var isVideoExt = AllowedVideoExtensions.Contains(ext);

        if (!isImageExt && !isVideoExt)
        {
            return BlogMediaValidationResult.Failure(
                $"Định dạng tệp '{ext}' không được hỗ trợ. Chỉ chấp nhận ảnh ({string.Join(", ", AllowedImageExtensions)}) hoặc video ({string.Join(", ", AllowedVideoExtensions)}).");
        }

        long actualLength = Math.Max(declaredLength ?? 0, fileStream.CanSeek ? fileStream.Length : 0);
        if (actualLength <= 0)
        {
            return BlogMediaValidationResult.Failure("Tập tin rỗng hoặc không có dữ liệu nội dung.");
        }

        if (isImageExt && actualLength > MaxImageSizeBytes)
        {
            return BlogMediaValidationResult.Failure(
                $"Kích thước ảnh vượt quá giới hạn cho phép (tối đa {MaxImageSizeBytes / (1024 * 1024)} MB).");
        }

        if (isVideoExt && actualLength > MaxVideoSizeBytes)
        {
            return BlogMediaValidationResult.Failure(
                $"Kích thước video vượt quá giới hạn cho phép (tối đa {MaxVideoSizeBytes / (1024 * 1024)} MB).");
        }

        // Read magic header bytes
        byte[] headerBytes;
        try
        {
            headerBytes = ReadHeaderBytes(fileStream, 64);
        }
        catch (Exception ex)
        {
            return BlogMediaValidationResult.Failure($"Không thể đọc chữ ký nhị phân của tập tin: {ex.Message}");
        }

        if (headerBytes.Length < 4)
        {
            return BlogMediaValidationResult.Failure("Tệp quá ngắn để xác thực định dạng.");
        }

        // Block executable signatures (e.g., .exe renamed to .mp4 or .jpg)
        if (IsExecutable(headerBytes))
        {
            return BlogMediaValidationResult.Failure("Phát hiện tệp thực thi độc hại giả mạo định dạng phương tiện.");
        }

        if (isImageExt)
        {
            if (ext is ".jpg" or ".jpeg")
            {
                if (!IsJpeg(headerBytes))
                {
                    return BlogMediaValidationResult.Failure("Nội dung tệp không khớp với chữ ký nhị phân của ảnh JPEG (thiếu SOI marker).");
                }
                return BlogMediaValidationResult.Success("Image", "JPEG", "image/jpeg");
            }

            if (ext == ".png")
            {
                if (!IsPng(headerBytes))
                {
                    return BlogMediaValidationResult.Failure("Nội dung tệp không khớp với chữ ký nhị phân của ảnh PNG.");
                }
                return BlogMediaValidationResult.Success("Image", "PNG", "image/png");
            }

            if (ext == ".webp")
            {
                if (!IsWebp(headerBytes))
                {
                    return BlogMediaValidationResult.Failure("Nội dung tệp không khớp với chữ ký nhị phân của ảnh WEBP (thiếu RIFF/WEBP).");
                }
                return BlogMediaValidationResult.Success("Image", "WEBP", "image/webp");
            }
        }

        if (isVideoExt)
        {
            if (ext == ".mp4")
            {
                if (!IsMp4(headerBytes))
                {
                    return BlogMediaValidationResult.Failure("Nội dung tệp không khớp với chữ ký nhị phân của video MP4 (thiếu ftyp atom).");
                }
                return BlogMediaValidationResult.Success("Video", "MP4", "video/mp4");
            }

            if (ext == ".webm")
            {
                if (!IsWebm(headerBytes))
                {
                    return BlogMediaValidationResult.Failure("Nội dung tệp không khớp với chữ ký nhị phân của video WebM (thiếu EBML header).");
                }
                return BlogMediaValidationResult.Success("Video", "WEBM", "video/webm");
            }
        }

        return BlogMediaValidationResult.Failure("Không thể xác minh định dạng phương tiện hợp lệ.");
    }

    private static byte[] ReadHeaderBytes(Stream stream, int count)
    {
        var buffer = new byte[count];
        long originalPos = stream.CanSeek ? stream.Position : 0;

        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        int bytesRead = stream.Read(buffer, 0, count);

        if (stream.CanSeek)
        {
            stream.Seek(originalPos, SeekOrigin.Begin);
        }

        if (bytesRead < count)
        {
            Array.Resize(ref buffer, bytesRead);
        }

        return buffer;
    }

    private static bool IsExecutable(byte[] header)
    {
        return header.Length >= 2 && header[0] == ExeHeader[0] && header[1] == ExeHeader[1];
    }

    private static bool IsJpeg(byte[] header)
    {
        return header.Length >= 3 &&
               header[0] == JpegHeader[0] &&
               header[1] == JpegHeader[1] &&
               header[2] == JpegHeader[2];
    }

    private static bool IsPng(byte[] header)
    {
        if (header.Length < PngHeader.Length) return false;
        return PngHeader.SequenceEqual(header.Take(PngHeader.Length));
    }

    private static bool IsWebp(byte[] header)
    {
        // Offset 0..3 is "RIFF" and 8..11 is "WEBP"
        if (header.Length < 12) return false;
        var riff = Encoding.ASCII.GetString(header, 0, 4);
        var webp = Encoding.ASCII.GetString(header, 8, 4);
        return riff == "RIFF" && webp == "WEBP";
    }

    private static bool IsMp4(byte[] header)
    {
        // ISO Base Media File Format: bytes 4..7 contain "ftyp"
        if (header.Length < 8) return false;
        var ftyp = Encoding.ASCII.GetString(header, 4, 4);
        return ftyp == "ftyp";
    }

    private static bool IsWebm(byte[] header)
    {
        if (header.Length < WebmHeader.Length) return false;
        return WebmHeader.SequenceEqual(header.Take(WebmHeader.Length));
    }
}
