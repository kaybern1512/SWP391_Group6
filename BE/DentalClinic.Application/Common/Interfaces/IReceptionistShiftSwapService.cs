using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IReceptionistShiftSwapService
{
    Task<ApiResponse<ReceptionistShiftSwapRequestDto>> CreateSwapRequestAsync(long requestingUserId, CreateReceptionistShiftSwapRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> RespondToSwapRequestAsync(long swapRequestId, long counterpartyUserId, RespondShiftSwapRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> WithdrawSwapRequestAsync(long swapRequestId, long requestingUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> AdminDecideSwapRequestAsync(long swapRequestId, long adminUserId, AdminDecideShiftSwapRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<ReceptionistShiftSwapRequestDto>>> GetSwapRequestsAsync(ReceptionistSwapFilterRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<ReceptionistShiftSwapRequestDto>>> GetMySwapRequestsAsync(long receptionistUserId, CancellationToken ct = default);
}
