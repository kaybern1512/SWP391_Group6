using System.IO;
using System.Threading;
using System.Threading.Tasks;

using DentalClinic.Application.Common.Models;

namespace DentalClinic.Application.Common.Interfaces;

public interface IImageStorageService
{
    Task<string> UploadAvatarAsync(long userId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default);
    Task<string> UploadImagingRecordAsync(long patientUserId, long clinicalEntryId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default);
    Task<bool> DeleteImageAsync(string fileUrl, CancellationToken ct = default);
    Task<StorageFileStreamResult?> GetImageStreamAsync(string fileUrl, CancellationToken ct = default);
}
