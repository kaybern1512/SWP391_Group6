using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Features.MedicalRecords.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

/// <summary>
/// Result metadata returned after uploading a dental imaging asset to cloud storage.
/// </summary>
public class DentalImageUploadResult
{
    public string Url { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Format { get; set; }
    public string StorageProvider { get; set; } = "Cloudinary";
}

public interface IImageStorageService
{
    Task<string> UploadAvatarAsync(long userId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Uploads a dental medical imaging file (X-Ray, CT, Intraoral, DICOM) to cloud storage using a stream.
    /// Preserves full resolution without lossy cropping for clinical diagnostics.
    /// </summary>
    Task<DentalImageUploadResult> UploadDentalImageAsync(Stream fileStream, string originalFileName, string contentType, string? subFolder = null, CancellationToken ct = default);

    /// <summary>
    /// Uploads a dental medical imaging file using the DentalImageFileInput abstraction.
    /// </summary>
    Task<DentalImageUploadResult> UploadDentalImageAsync(DentalImageFileInput file, string? subFolder = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes or cleans up an uploaded asset from cloud storage if a downstream database operation fails.
    /// </summary>
    Task<bool> DeleteDentalImageAsync(string publicId, CancellationToken ct = default);
}
