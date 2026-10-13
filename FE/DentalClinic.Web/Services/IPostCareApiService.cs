using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IPostCareApiService
{
    // Referrals
    Task<ApiResult<DepartmentReferralDto>> CreateReferralAsync(CreateDepartmentReferralRequest request);
    Task<ApiResult<List<DepartmentReferralDto>>> GetDentistReferralsAsync();
    Task<ApiResult<List<DepartmentReferralDto>>> GetDepartmentReferralsAsync(long? departmentId = null, string? status = null);
    Task<ApiResult<DepartmentReferralDto>> GetReferralByIdAsync(long referralId);
    Task<ApiResult<DepartmentReferralDto>> ReviewReferralAsync(long referralId, ReviewDepartmentReferralRequest request);
    Task<ApiResult<List<DepartmentDentistLookupDto>>> GetDepartmentDentistsAsync(long? departmentId = null);

    // Follow-ups
    Task<ApiResult<FollowUpScheduleDto>> CreateFollowUpScheduleAsync(CreateFollowUpScheduleRequest request);
    Task<ApiResult<List<FollowUpScheduleDto>>> GetMyFollowUpsAsync();
    Task<ApiResult<List<FollowUpScheduleDto>>> GetPatientFollowUpsAsync(long patientUserId);
    Task<ApiResult<List<FollowUpScheduleDto>>> GetReceptionistFollowUpsAsync(string? status = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<ApiResult<FollowUpScheduleDto>> UpdateFollowUpStatusAsync(long followUpId, UpdateFollowUpStatusRequest request);
}
