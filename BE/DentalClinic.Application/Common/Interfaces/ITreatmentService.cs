using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Treatments.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface ITreatmentService
{
    Task<ApiResponse<List<TreatmentPlanDto>>> GetDentistTreatmentPlansAsync(long dentistId, string? status, string? search, CancellationToken ct = default);
    Task<ApiResponse<TreatmentPlanDto>> GetTreatmentPlanByIdAsync(long planId, CancellationToken ct = default);
    Task<ApiResponse<TreatmentPlanDto>> CreateTreatmentPlanAsync(long dentistUserId, CreateTreatmentPlanRequest request, CancellationToken ct = default);
    Task<ApiResponse<TreatmentPlanItemDto>> AddTreatmentPlanItemAsync(long planId, CreateTreatmentPlanItemInput input, CancellationToken ct = default);
    Task<ApiResponse<bool>> UpdateTreatmentPlanStatusAsync(long planId, string status, CancellationToken ct = default);
    Task<ApiResponse<bool>> UpdateTreatmentPlanItemStatusAsync(long itemId, string status, CancellationToken ct = default);

    Task<ApiResponse<List<TreatmentSessionDto>>> GetDentistTreatmentSessionsAsync(long dentistId, string? search, CancellationToken ct = default);
    Task<ApiResponse<TreatmentSessionDto>> CreateTreatmentSessionAsync(long dentistUserId, CreateTreatmentSessionRequest request, CancellationToken ct = default);

    Task<ApiResponse<TreatmentProgressSummaryDto>> GetDepartmentTreatmentProgressAsync(long managerUserId, long? departmentId, string? search, CancellationToken ct = default);
    Task<ApiResponse<bool>> AssignPhaseDentistAsync(long itemId, long managerUserId, long dentistUserId, CancellationToken ct = default);

    Task<ApiResponse<List<TreatmentPlanDto>>> GetPatientTreatmentPlansAsync(long patientUserId, CancellationToken ct = default);

    Task<ApiResponse<List<TreatmentPatientOptionDto>>> GetTreatmentPatientOptionsAsync(CancellationToken ct = default);
    Task<ApiResponse<List<TreatmentServiceOptionDto>>> GetTreatmentServiceOptionsAsync(long? departmentId, CancellationToken ct = default);
    Task<ApiResponse<List<TreatmentDentistOptionDto>>> GetTreatmentDentistOptionsAsync(long? departmentId, CancellationToken ct = default);
    Task<ApiResponse<List<TreatmentEligiblePhaseDto>>> GetEligiblePhasesForSessionAsync(long dentistUserId, CancellationToken ct = default);
}
