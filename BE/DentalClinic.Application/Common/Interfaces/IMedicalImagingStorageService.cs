using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Features.MedicalRecords.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

/// <summary>
/// Abstraction for Dental Medical Imaging file storage (Local File System or Cloudinary).
/// Provides dedicated isolation for clinical diagnostic imagery without coupling to avatar or blog media.
/// </summary>
public interface IMedicalImagingStorageService
{
    /// <summary>
    /// Gets the name of the active storage provider (e.g. "Local" or "Cloudinary").
    /// </summary>
    string StorageProviderName { get; }

    /// <summary>
    /// Checks whether a stored file URL reference is hosted locally (e.g. local://medical-imaging/...).
    /// </summary>
    bool IsLocalReference(string? fileUrl);

    /// <summary>
    /// Uploads a dental medical imaging file (X-Ray, CT, Intraoral, DICOM) using a Stream.
    /// </summary>
    Task<DentalImageUploadResult> UploadDentalImageAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        string? subFolder = null,
        CancellationToken ct = default);

    /// <summary>
    /// Uploads a dental medical imaging file using the DentalImageFileInput abstraction.
    /// </summary>
    Task<DentalImageUploadResult> UploadDentalImageAsync(
        DentalImageFileInput file,
        string? subFolder = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes or cleans up an uploaded asset from storage.
    /// </summary>
    Task<bool> DeleteDentalImageAsync(string fileReferenceOrPublicId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a secure read stream of an image asset from storage for authorized server-side delivery.
    /// </summary>
    Task<Stream?> GetImageStreamAsync(string fileUrl, CancellationToken ct = default);
}
