using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IReceptionistHandoverService
{
    Task<ApiResponse<HandoverSummaryItemsDto>> GetCurrentHandoverSummaryAsync(CancellationToken ct = default);
    Task<ApiResponse<ReceptionistWorkHandoverDto>> CreateHandoverAsync(CreateWorkHandoverRequest request, long fromUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> AcknowledgeHandoverAsync(long handoverId, long toUserId, CancellationToken ct = default);
    Task<ApiResponse<List<ReceptionistWorkHandoverDto>>> GetHandoversAsync(DateOnly? date = null, long? userId = null, CancellationToken ct = default);
}
