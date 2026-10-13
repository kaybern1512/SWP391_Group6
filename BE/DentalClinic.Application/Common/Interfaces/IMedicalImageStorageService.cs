using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DentalClinic.Application.Common.Interfaces;

public class MedicalImageUploadResult
{
    public bool Success { get; set; }
    public string? StoredIdentifier { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
}

public class MedicalImageStreamResult
{
    public Stream Stream { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/octet-stream";
    public string? FileName { get; set; }
}

public interface IMedicalImageStorageService
{
    Task<MedicalImageUploadResult> SaveImageAsync(Stream stream, string originalFileName, string contentType, CancellationToken ct = default);
    Task<MedicalImageStreamResult?> GetImageStreamAsync(string storedIdentifier, CancellationToken ct = default);
    Task<bool> DeleteImageAsync(string storedIdentifier, CancellationToken ct = default);
}
