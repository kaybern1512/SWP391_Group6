using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Operations.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IAdminImpactAnalysisService
{
    Task<ApiResponse<UserLockImpactDto>> PreviewUserLockImpactAsync(long userId, CancellationToken ct = default);
    Task<ApiResponse<ServiceDeactivationImpactDto>> PreviewServiceDeactivationImpactAsync(long serviceId, CancellationToken ct = default);
    Task<ApiResponse<RoomChairDeactivationImpactDto>> PreviewRoomDeactivationImpactAsync(long roomId, CancellationToken ct = default);
    Task<ApiResponse<RoomChairDeactivationImpactDto>> PreviewChairDeactivationImpactAsync(long chairId, CancellationToken ct = default);
    Task<ApiResponse<PriceChangeImpactDto>> PreviewPriceChangeImpactAsync(long serviceId, decimal newPrice, CancellationToken ct = default);
    Task<ApiResponse<bool>> ExecuteSafeUserStatusChangeAsync(long userId, string newStatus, string? reason, long adminUserId, CancellationToken ct = default);
}
