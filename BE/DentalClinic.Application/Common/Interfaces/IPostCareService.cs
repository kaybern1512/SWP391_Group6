using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.PostCare.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IPostCareService
{
    // Referrals (Chuyển chuyên khoa / Hội chẩn)
    Task<ApiResponse<DepartmentReferralDto>> CreateReferralAsync(long dentistUserId, CreateReferralRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<DepartmentReferralDto>>> GetDentistReferralsAsync(long dentistUserId, CancellationToken ct = default);
    Task<ApiResponse<List<DepartmentReferralDto>>> GetDepartmentReferralsAsync(long managerUserId, string? status = null, CancellationToken ct = default);
    Task<ApiResponse<DepartmentReferralDto>> ReviewReferralAsync(long managerUserId, long referralId, ReviewReferralRequest request, CancellationToken ct = default);

    // Follow-ups (Hẹn tái khám định kỳ)
    Task<ApiResponse<FollowUpScheduleDto>> CreateFollowUpAsync(long dentistUserId, CreateFollowUpRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<FollowUpScheduleDto>>> GetPatientFollowUpsAsync(long patientUserId, CancellationToken ct = default);
    Task<ApiResponse<List<FollowUpScheduleDto>>> GetDentistFollowUpsAsync(long dentistUserId, CancellationToken ct = default);
}
