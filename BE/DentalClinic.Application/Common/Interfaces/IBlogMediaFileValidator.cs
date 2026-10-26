using System.IO;

namespace DentalClinic.Application.Common.Interfaces;

public class BlogMediaValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string MediaType { get; set; } = string.Empty; // "Image" or "Video"
    public string DetectedFormat { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    public static BlogMediaValidationResult Success(string mediaType, string format, string contentType) => new()
    {
        IsValid = true,
        MediaType = mediaType,
        DetectedFormat = format,
        ContentType = contentType
    };

    public static BlogMediaValidationResult Failure(string errorMessage) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage
    };
}

public interface IBlogMediaFileValidator
{
    BlogMediaValidationResult Validate(Stream? fileStream, string? fileName, string? declaredContentType, long? declaredLength = null);
}
