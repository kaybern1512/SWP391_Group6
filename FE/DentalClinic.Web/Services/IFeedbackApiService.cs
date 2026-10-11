using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IFeedbackApiService
{
    Task<ApiResult<VisitFeedbackDto>> CreateFeedbackAsync(CreateFeedbackRequest request);
    Task<ApiResult<VisitFeedbackDto?>> GetFeedbackByAppointmentIdAsync(long appointmentId);
    Task<ApiResult<DentistFeedbackSummaryDto>> GetDentistFeedbackSummaryAsync(long dentistUserId);
    Task<ApiResult<List<VisitFeedbackDto>>> GetMyFeedbacksAsync();
    Task<ApiResult<FeedbackPagedResultDto>> GetFeedbacksAsync(FeedbackFilterRequest request);
}
