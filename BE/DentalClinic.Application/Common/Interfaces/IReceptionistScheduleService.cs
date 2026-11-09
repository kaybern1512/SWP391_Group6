using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IReceptionistScheduleService
{
    Task<ApiResponse<List<ReceptionistShiftTemplateDto>>> GetShiftTemplatesAsync(CancellationToken ct = default);
    Task<ApiResponse<MonthlyReceptionistScheduleDto>> GetMonthlyScheduleAsync(int year, int month, long? receptionistUserId = null, CancellationToken ct = default);
    Task<ApiResponse<List<ReceptionistWorkScheduleDto>>> GetMySchedulesAsync(long receptionistUserId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default);
    Task<ApiResponse<ReceptionistWorkScheduleDto>> CreateScheduleAsync(CreateReceptionistScheduleRequest request, long currentUserId, CancellationToken ct = default);
    Task<ApiResponse<ReceptionistWorkScheduleDto>> UpdateScheduleAsync(long scheduleId, UpdateReceptionistScheduleRequest request, long currentUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteScheduleAsync(long scheduleId, long currentUserId, CancellationToken ct = default);

    Task<ApiResponse<ReceptionistBulkSchedulePreviewDto>> PreviewBulkScheduleAsync(ReceptionistBulkScheduleAssignmentRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> ApplyBulkScheduleAsync(ReceptionistBulkScheduleAssignmentRequest request, long currentUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> CopyMonthScheduleAsync(ReceptionistCopyMonthScheduleRequest request, long currentUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> ApplyDailyOverrideAsync(ReceptionistDailyOverrideRequest request, long currentUserId, CancellationToken ct = default);
}
