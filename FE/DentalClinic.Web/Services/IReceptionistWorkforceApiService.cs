using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IReceptionistWorkforceApiService
{
    // ==========================================
    // ADMIN ENDPOINTS
    // ==========================================
    Task<ApiResult<MonthlyReceptionistScheduleDto>> GetMonthlyScheduleAsync(int year, int month, long? receptionistUserId = null);
    Task<ApiResult<List<ReceptionistShiftTemplateDto>>> GetShiftTemplatesAsync();
    Task<ApiResult<ReceptionistWorkScheduleDto>> CreateScheduleAsync(CreateReceptionistScheduleRequest request);
    Task<ApiResult<ReceptionistWorkScheduleDto>> UpdateScheduleAsync(long scheduleId, UpdateReceptionistScheduleRequest request);
    Task<ApiResult> DeleteScheduleAsync(long scheduleId);
    Task<ApiResult<ReceptionistBulkSchedulePreviewDto>> PreviewBulkScheduleAsync(ReceptionistBulkScheduleAssignmentRequest request);
    Task<ApiResult> ApplyBulkScheduleAsync(ReceptionistBulkScheduleAssignmentRequest request);
    Task<ApiResult> CopyMonthScheduleAsync(ReceptionistCopyMonthScheduleRequest request);
    Task<ApiResult> ApplyDailyOverrideAsync(ReceptionistDailyOverrideRequest request);

    Task<ApiResult<List<ReceptionistLeaveRequestDto>>> GetLeaveRequestsAsync(ReceptionistLeaveFilterRequest request);
    Task<ApiResult<LeaveImpactPreviewDto>> PreviewLeaveImpactAsync(long leaveId);
    Task<ApiResult> ApproveLeaveRequestAsync(long leaveId);
    Task<ApiResult> RejectLeaveRequestAsync(long leaveId, string reason);
    Task<ApiResult> CancelApprovedLeaveAsync(long leaveId, string reason);

    Task<ApiResult<List<ReceptionistShiftSwapRequestDto>>> GetSwapRequestsAsync(ReceptionistSwapFilterRequest request);
    Task<ApiResult> AdminDecideSwapRequestAsync(long swapId, AdminDecideShiftSwapRequest request);

    Task<ApiResult<List<ReceptionistCoverageIncidentDto>>> GetCoverageIncidentsAsync(CoverageIncidentFilterRequest request);
    Task<ApiResult> ResolveCoverageIncidentAsync(long incidentId, ResolveCoverageIncidentRequest request);

    // ==========================================
    // RECEPTIONIST PORTAL ENDPOINTS
    // ==========================================
    Task<ApiResult<List<ReceptionistWorkScheduleDto>>> GetMySchedulesAsync(DateOnly? fromDate = null, DateOnly? toDate = null);
    Task<ApiResult<List<ReceptionistLeaveRequestDto>>> GetMyLeavesAsync();
    Task<ApiResult<ReceptionistLeaveRequestDto>> CreateLeaveRequestAsync(CreateReceptionistLeaveRequest request);
    Task<ApiResult> WithdrawLeaveRequestAsync(long leaveId);

    Task<ApiResult<List<ReceptionistShiftSwapRequestDto>>> GetMySwapsAsync();
    Task<ApiResult<ReceptionistShiftSwapRequestDto>> CreateSwapRequestAsync(CreateReceptionistShiftSwapRequest request);
    Task<ApiResult> RespondToSwapAsync(long swapId, RespondShiftSwapRequest request);
    Task<ApiResult> WithdrawSwapAsync(long swapId);

    Task<ApiResult<ReceptionistCoverageIncidentDto>> ReportEmergencyAbsenceAsync(ReportEmergencyAbsenceRequest request);

    Task<ApiResult<HandoverSummaryItemsDto>> GetHandoverSummaryAsync();
    Task<ApiResult<List<ReceptionistWorkHandoverDto>>> GetHandoversAsync(DateOnly? date = null);
    Task<ApiResult<ReceptionistWorkHandoverDto>> CreateHandoverAsync(CreateWorkHandoverRequest request);
    Task<ApiResult> AcknowledgeHandoverAsync(long handoverId);
}
