using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Profile.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IProfileService
{
    Task<ApiResponse<UserProfileResponse>> GetProfileAsync(long userId, CancellationToken ct = default);
    Task<ApiResponse> UpdateProfileAsync(long userId, UpdateProfileRequest request, CancellationToken ct = default);
    Task<ApiResponse<AvatarUploadResponse>> UploadAvatarAsync(long userId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default);
}
