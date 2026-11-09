using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.DentistLeave.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IDentistLeaveService
{
    Task<ApiResponse<DentistLeaveRequestDto>> SubmitLeaveRequestAsync(CreateDentistLeaveRequest request, long dentistUserId, CancellationToken ct = default);
    Task<ApiResponse<DentistLeaveRequestDto>> WithdrawLeaveRequestAsync(long leaveRequestId, long dentistUserId, CancellationToken ct = default);
    Task<ApiResponse<List<DentistLeaveRequestDto>>> GetMyLeaveRequestsAsync(long dentistUserId, CancellationToken ct = default);
    Task<ApiResponse<List<DentistLeaveRequestDto>>> GetAllLeaveRequestsAsync(DentistLeaveFilterRequest filter, CancellationToken ct = default);
    Task<ApiResponse<DentistLeaveRequestDto>> GetLeaveRequestDetailAsync(long leaveRequestId, long? currentUserId = null, string? currentRole = null, CancellationToken ct = default);
    Task<ApiResponse<DentistLeaveImpactAnalysisDto>> GetLeaveImpactAnalysisAsync(long leaveRequestId, CancellationToken ct = default);
    Task<ApiResponse<DentistLeaveRequestDto>> ApproveLeaveRequestAsync(long leaveRequestId, long adminUserId, ApproveDentistLeaveRequest? request = null, CancellationToken ct = default);
    Task<ApiResponse<DentistLeaveRequestDto>> RejectLeaveRequestAsync(long leaveRequestId, long adminUserId, RejectDentistLeaveRequest request, CancellationToken ct = default);
}
