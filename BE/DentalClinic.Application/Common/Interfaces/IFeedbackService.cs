using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Feedback.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IFeedbackService
{
    Task<ApiResponse<VisitFeedbackDto>> CreateFeedbackAsync(CreateFeedbackRequest request, long patientUserId, CancellationToken ct = default);
    Task<ApiResponse<VisitFeedbackDto?>> GetFeedbackByAppointmentIdAsync(long appointmentId, CancellationToken ct = default);
    Task<ApiResponse<DentistFeedbackSummaryDto>> GetDentistFeedbackSummaryAsync(long dentistUserId, CancellationToken ct = default);
    Task<ApiResponse<List<VisitFeedbackDto>>> GetMyFeedbacksAsync(long patientUserId, CancellationToken ct = default);
    Task<ApiResponse<FeedbackPagedResultDto>> GetFeedbacksAsync(FeedbackFilterRequest request, CancellationToken ct = default);
    Task<ApiResponse<VisitFeedbackDto>> ModerateFeedbackAsync(long feedbackId, ModerateFeedbackRequest request, long adminUserId, CancellationToken ct = default);
    Task<ApiResponse<VisitFeedbackDto>> RespondFeedbackAsync(long feedbackId, RespondFeedbackRequest request, long adminUserId, CancellationToken ct = default);
}
