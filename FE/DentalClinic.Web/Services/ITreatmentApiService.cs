using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface ITreatmentApiService
{
    Task<ApiResult<List<TreatmentPlanDto>>> GetDentistTreatmentPlansAsync(string? status = null, string? search = null);
    Task<ApiResult<TreatmentPlanDto>> GetTreatmentPlanDetailAsync(long planId);
    Task<ApiResult<TreatmentPlanDto>> CreateTreatmentPlanAsync(CreateTreatmentPlanRequest request);
    Task<ApiResult<TreatmentPlanItemDto>> AddTreatmentPlanItemAsync(long planId, CreateTreatmentPlanItemInput input);
    Task<ApiResult<bool>> UpdatePlanStatusAsync(long planId, string status);
    Task<ApiResult<bool>> UpdateItemStatusAsync(long itemId, string status);

    Task<ApiResult<List<TreatmentSessionDto>>> GetDentistTreatmentSessionsAsync(string? search = null);
    Task<ApiResult<TreatmentSessionDto>> CreateTreatmentSessionAsync(CreateTreatmentSessionRequest request);
    Task<ApiResult<List<TreatmentEligiblePhaseDto>>> GetEligiblePhasesAsync();

    Task<ApiResult<TreatmentProgressSummaryDto>> GetDepartmentTreatmentProgressAsync(long? departmentId = null, string? search = null);
    Task<ApiResult<bool>> AssignPhaseDentistAsync(long itemId, long dentistUserId);

    Task<ApiResult<List<TreatmentPlanDto>>> GetPatientMyPlansAsync();

    Task<ApiResult<List<TreatmentPatientOptionDto>>> GetPatientOptionsAsync();
    Task<ApiResult<List<TreatmentServiceOptionDto>>> GetServiceOptionsAsync(long? departmentId = null);
    Task<ApiResult<List<TreatmentDentistOptionDto>>> GetDentistOptionsAsync(long? departmentId = null);
}
