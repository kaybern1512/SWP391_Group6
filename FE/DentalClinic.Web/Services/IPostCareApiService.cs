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
    Task<ApiResult<List<ReceptionistFollowUpDto>>> GetReceptionistFollowUpsAsync(System.DateTime? fromDate = null, System.DateTime? toDate = null, string? status = null);
}
