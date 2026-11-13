using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.CareEpisodes.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface ICareEpisodeService
{
    // Episode Lifecycle Management
    Task<ApiResponse<CareEpisodeDto>> GetOrCreateActiveEpisodeForAppointmentAsync(long appointmentId, CancellationToken ct = default);
    Task<ApiResponse<CareEpisodeDto>> GetEpisodeByIdAsync(long careEpisodeId, CancellationToken ct = default);
    Task<ApiResponse<CareEpisodeDto>> GetEpisodeByAppointmentIdAsync(long appointmentId, CancellationToken ct = default);
    Task<ApiResponse<CareEpisodeDto>> GetPatientActiveEpisodeAsync(long patientUserId, CancellationToken ct = default);

    // Case A: Additional Services within Same Appointment (Doctor A)
    Task<ApiResponse<List<EpisodeServiceTaskDto>>> GetEligibleServicesForDentistAsync(long appointmentId, long dentistUserId, CancellationToken ct = default);
    Task<ApiResponse<EpisodeServiceTaskDto>> AddServiceTaskAsync(long dentistUserId, CreateEpisodeServiceTaskRequest request, CancellationToken ct = default);
    Task<ApiResponse<EpisodeServiceTaskDto>> UpdateServiceTaskStatusAsync(long dentistUserId, long serviceTaskId, UpdateServiceTaskStatusRequest request, CancellationToken ct = default);
    Task<ApiResponse<EpisodeServiceTaskDto>> StartServiceTaskAsync(long dentistUserId, long serviceTaskId, CancellationToken ct = default);
    Task<ApiResponse<EpisodeTaskStepProgressDto>> CompleteTaskStepAsync(long dentistUserId, long serviceTaskId, long stepId, CompleteTaskStepRequest request, CancellationToken ct = default);
    Task<ApiResponse<EpisodeServiceTaskDto>> CompleteServiceTaskAsync(long dentistUserId, long serviceTaskId, CompleteServiceTaskRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> ValidateAppointmentTasksCompletedAsync(long appointmentId, CancellationToken ct = default);

    // Case B: Cross-Dentist Referral & Handoff (Doctor A -> Receptionist -> Doctor B)
    Task<ApiResponse<CareEpisodeReferralSummaryDto>> CreateInternalReferralAsync(long dentistUserId, CreateCareEpisodeReferralRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<CareEpisodeReferralSummaryDto>>> GetPendingCareEpisodeReferralsAsync(CancellationToken ct = default);
    Task<ApiResponse<List<AvailableDentistForReferralDto>>> GetEligibleDentistsForReferralAsync(long referralId, DateTime targetDate, CancellationToken ct = default);
    Task<ApiResponse<CareEpisodeAppointmentSummaryDto>> CoordinateReferralAppointmentAsync(long receptionistUserId, CoordinateCareEpisodeReferralRequest request, CancellationToken ct = default);
    Task<ApiResponse<CareEpisodeAppointmentSummaryDto>> StationBCheckInAsync(long receptionistUserId, StationBCheckInRequest request, CancellationToken ct = default);

    // Single Consolidated Invoice per Episode
    Task<ApiResponse<ConsolidatedEpisodeBillingSummaryDto>> GetConsolidatedInvoiceSummaryAsync(long careEpisodeId, CancellationToken ct = default);
    Task<ApiResponse<InvoiceDto>> GenerateConsolidatedInvoiceAsync(long receptionistUserId, GenerateConsolidatedInvoiceRequest request, CancellationToken ct = default);
}
