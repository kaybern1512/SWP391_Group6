using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IClinicalCoreApiClient
{
    Task<ApiResult<DentalMedicalRecordDto>> GetMedicalRecordAsync(long patientUserId);
    Task<ApiResult<ClinicalEntryDto>> CreateClinicalEntryAsync(long patientUserId, CreateClinicalEntryRequest request);
    Task<ApiResult<TreatmentPlanDto>> CreateTreatmentPlanAsync(CreateTreatmentPlanRequest request);
    Task<ApiResult<TreatmentSessionDto>> CreateTreatmentSessionAsync(CreateTreatmentSessionRequest request);
    Task<ApiResult<PrescriptionDto>> CreatePrescriptionAsync(CreatePrescriptionRequest request);
    Task<ApiResult<DepartmentReferralDto>> CreateDepartmentReferralAsync(CreateDepartmentReferralRequest request);
    Task<ApiResult<FollowUpScheduleDto>> CreateFollowUpAsync(CreateFollowUpRequest request);
}
