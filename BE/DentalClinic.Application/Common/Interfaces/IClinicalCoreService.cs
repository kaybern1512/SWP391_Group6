using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ClinicalCore.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IClinicalCoreService
{
    Task<ApiResponse<DentalMedicalRecordDto>> GetMedicalRecordAsync(long patientUserId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<DentalMedicalRecordDto>> GetOrCreateMedicalRecordAsync(long patientUserId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<ClinicalEntryDto>> CreateClinicalEntryAsync(CreateClinicalEntryRequest request, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<List<TreatmentPlanDto>>> GetTreatmentPlansForPatientAsync(long patientUserId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<TreatmentPlanDto>> GetTreatmentPlanByIdAsync(long treatmentPlanId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<TreatmentPlanDto>> CreateTreatmentPlanAsync(CreateTreatmentPlanRequest request, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<TreatmentPlanDto>> UpdateTreatmentPlanItemStatusAsync(long treatmentPlanItemId, UpdateTreatmentPlanItemStatusRequest request, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<TreatmentPlanDto>> UpdateTreatmentPlanStatusAsync(long treatmentPlanId, UpdateTreatmentPlanStatusRequest request, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<List<TreatmentSessionDto>>> GetTreatmentSessionsForPatientAsync(long patientUserId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<TreatmentSessionDto>> CreateTreatmentSessionAsync(CreateTreatmentSessionRequest request, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<List<PrescriptionDto>>> GetPrescriptionsForPatientAsync(long patientUserId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<PrescriptionDto>> GetPrescriptionByIdAsync(long prescriptionId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<PrescriptionDto>> GetPrescriptionForTreatmentSessionAsync(long treatmentSessionId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<PrescriptionDto>> CreatePrescriptionAsync(CreatePrescriptionRequest request, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<List<DepartmentReferralDto>>> GetDepartmentReferralsForPatientAsync(long patientUserId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<List<DepartmentReferralDto>>> GetDepartmentReferralsForManagerAsync(long currentUserId, string currentRole, string? status, CancellationToken ct = default);
    Task<ApiResponse<DepartmentReferralDto>> CreateDepartmentReferralAsync(CreateDepartmentReferralRequest request, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<DepartmentReferralDto>> ReviewDepartmentReferralAsync(long referralId, ReviewDepartmentReferralRequest request, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<DepartmentReferralDto>> AssignDepartmentReferralAsync(long referralId, AssignDepartmentReferralRequest request, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<DepartmentReferralDto>> CompleteDepartmentReferralAsync(long referralId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<List<FollowUpScheduleDto>>> GetFollowUpsForPatientAsync(long patientUserId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<FollowUpScheduleDto>> CreateFollowUpAsync(CreateFollowUpRequest request, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<FollowUpScheduleDto>> LinkFollowUpAppointmentAsync(long followUpId, LinkFollowUpAppointmentRequest request, long currentUserId, string currentRole, CancellationToken ct = default);
}
