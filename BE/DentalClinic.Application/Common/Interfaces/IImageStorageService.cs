using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DentalClinic.Application.Common.Interfaces;

public interface IImageStorageService
{
    Task<string> UploadAvatarAsync(long userId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default);
}
