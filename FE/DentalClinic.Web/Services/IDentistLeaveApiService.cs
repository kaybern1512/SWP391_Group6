using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IDentistLeaveApiService
{
    Task<ApiResult<DentistLeaveRequestDto>> SubmitLeaveRequestAsync(CreateDentistLeaveRequest request);
    Task<ApiResult<List<DentistLeaveRequestDto>>> GetMyLeaveRequestsAsync();
    Task<ApiResult<DentistLeaveRequestDto>> WithdrawLeaveRequestAsync(long leaveRequestId);
    Task<ApiResult<List<DentistLeaveRequestDto>>> GetAllLeaveRequestsAsync(DentistLeaveFilterRequest filter);
    Task<ApiResult<DentistLeaveRequestDto>> GetLeaveRequestDetailAsync(long leaveRequestId);
    Task<ApiResult<DentistLeaveImpactAnalysisDto>> GetLeaveImpactAnalysisAsync(long leaveRequestId);
    Task<ApiResult<DentistLeaveRequestDto>> ApproveLeaveRequestAsync(long leaveRequestId, ApproveDentistLeaveRequest? request);
    Task<ApiResult<DentistLeaveRequestDto>> RejectLeaveRequestAsync(long leaveRequestId, RejectDentistLeaveRequest request);
}
