using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IReceptionistLeaveService
{
    Task<ApiResponse<ReceptionistLeaveRequestDto>> CreateLeaveRequestAsync(long receptionistUserId, CreateReceptionistLeaveRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> WithdrawLeaveRequestAsync(long leaveRequestId, long receptionistUserId, CancellationToken ct = default);
    Task<ApiResponse<List<ReceptionistLeaveRequestDto>>> GetLeaveRequestsAsync(ReceptionistLeaveFilterRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<ReceptionistLeaveRequestDto>>> GetMyLeaveRequestsAsync(long receptionistUserId, CancellationToken ct = default);
    Task<ApiResponse<LeaveImpactPreviewDto>> PreviewLeaveImpactAsync(long leaveRequestId, CancellationToken ct = default);
    Task<ApiResponse<bool>> ApproveLeaveRequestAsync(long leaveRequestId, long adminUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> RejectLeaveRequestAsync(long leaveRequestId, string rejectionReason, long adminUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> CancelApprovedLeaveAsync(long leaveRequestId, string reason, long adminUserId, CancellationToken ct = default);
}
