using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IPostCareApiService
{
    Task<ApiResult<DepartmentReferralDto>> CreateReferralAsync(CreateReferralRequest request);
    Task<ApiResult<List<DepartmentReferralDto>>> GetDentistReferralsAsync();
    Task<ApiResult<List<DepartmentReferralDto>>> GetDepartmentReferralsAsync(string? status = null);
    Task<ApiResult<DepartmentReferralDto>> ReviewReferralAsync(long referralId, ReviewReferralRequest request);
    Task<ApiResult<FollowUpScheduleDto>> CreateFollowUpAsync(CreateFollowUpRequest request);
    Task<ApiResult<List<FollowUpScheduleDto>>> GetPatientFollowUpsAsync();
    Task<ApiResult<List<FollowUpScheduleDto>>> GetDentistFollowUpsAsync();
    Task<ApiResult<List<FollowUpScheduleDto>>> GetReceptionistFollowUpsAsync();
    Task<ApiResult<FollowUpAppointmentDto>> DoctorBookFollowUpAsync(CreateFollowUpAppointmentRequest request);
    Task<ApiResult<List<FollowUpSlotDto>>> GetFollowUpAvailableSlotsAsync(long dentistUserId, System.DateTime date, int durationMinutes = 30);
    Task<ApiResult<FollowUpProposalDto>> SubmitPatientProposalAsync(PatientFollowUpChangeRequest request);
    Task<ApiResult<bool>> WithdrawPatientProposalAsync(long proposalId);
    Task<ApiResult<List<FollowUpProposalDto>>> GetPatientProposalsAsync();
    Task<ApiResult<List<FollowUpProposalDto>>> GetDoctorPendingProposalsAsync();
    Task<ApiResult<FollowUpProposalDto>> ResolveProposalAsync(ResolveProposalRequest request);
    Task<ApiResult<List<FollowUpReminderDto>>> GetReceptionistRemindersAsync(string? dateFilter = null, string? callStatus = null);
    Task<ApiResult<FollowUpReminderDto>> UpdateReminderCallStatusAsync(UpdateReminderCallStatusRequest request);
}
