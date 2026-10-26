using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DentalClinic.Application.Common.Interfaces;

public class MediaUploadResult
{
    public string Url { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string MediaType { get; set; } = "Image"; // "Image" or "Video"
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int? DurationSeconds { get; set; }
}

public interface IMediaStorageService
{
    Task<MediaUploadResult> UploadImageAsync(Stream fileStream, string originalFileName, string contentType, string? folder = null, CancellationToken ct = default);
    Task<MediaUploadResult> UploadVideoAsync(Stream fileStream, string originalFileName, string contentType, string? folder = null, CancellationToken ct = default);
    Task<bool> DeleteMediaAsync(string publicId, string mediaType, CancellationToken ct = default);
}
