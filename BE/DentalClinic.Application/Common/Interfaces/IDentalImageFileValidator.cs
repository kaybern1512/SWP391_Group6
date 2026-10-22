using System.IO;
using DentalClinic.Application.Features.MedicalRecords.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

/// <summary>
/// Service interface for validating dental imaging files (X-Rays, CT, Intraoral photos)
/// against size limits, allowed extensions, MIME types, and binary file signatures.
/// </summary>
public interface IDentalImageFileValidator
{
    /// <summary>
    /// Validates a DentalImageFileInput object.
    /// </summary>
    ImageFileValidationResult Validate(DentalImageFileInput? file);

    /// <summary>
    /// Validates a raw stream and its associated file metadata.
    /// </summary>
    ImageFileValidationResult Validate(Stream? fileStream, string? fileName, string? declaredContentType, long? declaredLength = null);
}

/// <summary>
/// Encapsulates the result of a dental image file validation check.
/// </summary>
public class ImageFileValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DetectedFormat { get; set; }
    public string? NormalizedContentType { get; set; }

    public static ImageFileValidationResult Success(string detectedFormat, string normalizedContentType) =>
        new()
        {
            IsValid = true,
            DetectedFormat = detectedFormat,
            NormalizedContentType = normalizedContentType
        };

    public static ImageFileValidationResult Failure(string errorMessage) =>
        new()
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };
}
