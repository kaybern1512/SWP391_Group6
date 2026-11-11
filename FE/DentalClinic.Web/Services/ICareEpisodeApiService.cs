using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface ICareEpisodeApiService
{
    Task<ApiResult<CareEpisodeDto>> GetEpisodeByIdAsync(long episodeId);
    Task<ApiResult<CareEpisodeDto>> GetEpisodeByAppointmentIdAsync(long appointmentId);
    Task<ApiResult<CareEpisodeDto>> GetPatientActiveEpisodeAsync(long patientUserId);
    Task<ApiResult<List<EpisodeServiceTaskDto>>> GetEligibleServicesForDentistAsync(long appointmentId);
    Task<ApiResult<EpisodeServiceTaskDto>> AddServiceTaskAsync(CreateEpisodeServiceTaskRequest request);
    Task<ApiResult<EpisodeServiceTaskDto>> UpdateServiceTaskStatusAsync(long serviceTaskId, UpdateServiceTaskStatusRequest request);
    Task<ApiResult<CareEpisodeReferralSummaryDto>> CreateInternalReferralAsync(CreateCareEpisodeReferralRequest request);
    Task<ApiResult<List<CareEpisodeReferralSummaryDto>>> GetPendingReferralsAsync();
    Task<ApiResult<List<AvailableDentistForReferralDto>>> GetEligibleDentistsForReferralAsync(long referralId, DateTime? date = null);
    Task<ApiResult<CareEpisodeAppointmentSummaryDto>> CoordinateReferralAsync(CoordinateCareEpisodeReferralRequest request);
    Task<ApiResult<CareEpisodeAppointmentSummaryDto>> StationBCheckInAsync(StationBCheckInRequest request);
    Task<ApiResult<ConsolidatedEpisodeBillingSummaryDto>> GetConsolidatedBillingSummaryAsync(long episodeId);
    Task<ApiResult<InvoiceDto>> GenerateConsolidatedInvoiceAsync(GenerateConsolidatedInvoiceRequest request);
}
