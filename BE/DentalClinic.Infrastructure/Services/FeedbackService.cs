using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Feedback.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services;

public class FeedbackService : IFeedbackService
{
    private readonly DentalClinicDbContext _dbContext;

    public FeedbackService(DentalClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<VisitFeedbackDto>> CreateFeedbackAsync(CreateFeedbackRequest request, long patientUserId, CancellationToken ct = default)
    {
        if (request.DentistRating < 1 || request.DentistRating > 5)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Đánh giá bác sĩ phải từ 1 đến 5 sao.");
        }

        if (request.ClinicRating < 1 || request.ClinicRating > 5)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Đánh giá phòng khám phải từ 1 đến 5 sao.");
        }

        var appointment = await _dbContext.Appointments
            .Include(a => a.Patient)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.AssignedDentist)
                    .ThenInclude(d => d!.StaffProfile)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.DepartmentService)
                    .ThenInclude(ds => ds.Department)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Không tìm thấy cuộc hẹn để đánh giá.");
        }

        if (appointment.PatientUserId != patientUserId)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Bạn chỉ có quyền đánh giá các ca khám của chính mình.");
        }

        var targetService = appointment.AppointmentServices
            .Where(s => s.Status == AppointmentStatus.Completed && s.AssignedDentistUserId.HasValue &&
                        (!request.DentistUserId.HasValue || s.AssignedDentistUserId == request.DentistUserId.Value))
            .OrderByDescending(s => s.ScheduledEnd)
            .FirstOrDefault();

        if (targetService == null || !targetService.AssignedDentistUserId.HasValue)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Chỉ có thể gửi đánh giá sau khi dịch vụ ca khám đã hoàn thành và có bác sĩ phụ trách.");
        }

        var assignedDentistId = targetService.AssignedDentistUserId.Value;

        if (request.DentistUserId.HasValue && request.DentistUserId.Value != assignedDentistId)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Bác sĩ được đánh giá không khớp với bác sĩ phụ trách dịch vụ.");
        }

        var existingFeedback = await _dbContext.VisitFeedbacks
            .AnyAsync(f => f.AppointmentId == appointment.AppointmentId && f.DentistUserId == assignedDentistId, ct);

        if (existingFeedback)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Ca khám này đã được gửi đánh giá cho bác sĩ phụ trách trước đó.");
        }

        var feedback = new VisitFeedback
        {
            AppointmentId = appointment.AppointmentId,
            AppointmentServiceId = targetService.AppointmentServiceId,
            PatientUserId = appointment.PatientUserId,
            DentistUserId = assignedDentistId,
            DentistRating = request.DentistRating,
            ClinicRating = request.ClinicRating,
            Comments = request.Comment?.Trim(),
            Status = "Published",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.VisitFeedbacks.Add(feedback);
        await _dbContext.SaveChangesAsync(ct);

        feedback.Appointment = appointment;
        feedback.AppointmentService = targetService;
        feedback.Patient = appointment.Patient;
        feedback.Dentist = targetService.AssignedDentist!;

        return ApiResponse<VisitFeedbackDto>.Ok(MapToDto(feedback), "Cảm ơn bạn đã gửi đánh giá và đóng góp ý kiến cho phòng khám!");
    }

    public async Task<ApiResponse<VisitFeedbackDto?>> GetFeedbackByAppointmentIdAsync(long appointmentId, CancellationToken ct = default)
    {
        var feedback = await BuildQuery()
            .FirstOrDefaultAsync(f => f.AppointmentId == appointmentId || f.AppointmentServiceId == appointmentId, ct);

        if (feedback == null)
        {
            return ApiResponse<VisitFeedbackDto?>.Ok(null);
        }

        return ApiResponse<VisitFeedbackDto?>.Ok(MapToDto(feedback));
    }

    public async Task<ApiResponse<DentistFeedbackSummaryDto>> GetDentistFeedbackSummaryAsync(long dentistUserId, CancellationToken ct = default)
    {
        var dentist = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.StaffProfile)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null)
        {
            return ApiResponse<DentistFeedbackSummaryDto>.Fail("Không tìm thấy thông tin bác sĩ.");
        }

        var feedbacks = await BuildQuery()
            .Where(f => f.DentistUserId == dentistUserId && f.Status != "Hidden")
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        var total = feedbacks.Count;
        double avgDentist = total > 0 ? feedbacks.Average(f => f.DentistRating) : 0;
        double avgClinic = total > 0 ? feedbacks.Average(f => f.ClinicRating) : 0;

        var summary = new DentistFeedbackSummaryDto
        {
            DentistUserId = dentistUserId,
            DentistName = dentist.StaffProfile?.FullName ?? "Bác sĩ",
            TotalFeedbacks = total,
            AverageDentistRating = Math.Round(avgDentist, 1),
            AverageClinicRating = Math.Round(avgClinic, 1),
            RecentFeedbacks = feedbacks.Select(MapToDto).ToList()
        };

        return ApiResponse<DentistFeedbackSummaryDto>.Ok(summary);
    }

    public async Task<ApiResponse<List<VisitFeedbackDto>>> GetMyFeedbacksAsync(long patientUserId, CancellationToken ct = default)
    {
        var feedbacks = await BuildQuery()
            .Where(f => f.PatientUserId == patientUserId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        return ApiResponse<List<VisitFeedbackDto>>.Ok(feedbacks.Select(MapToDto).ToList());
    }

    public async Task<ApiResponse<FeedbackPagedResultDto>> GetFeedbacksAsync(FeedbackFilterRequest request, CancellationToken ct = default)
    {
        var query = BuildQuery();

        if (request.DepartmentId.HasValue)
        {
            var deptIdInt = (int)request.DepartmentId.Value;
            query = query.Where(f => f.AppointmentService.DepartmentId == deptIdInt);
        }

        if (request.DentistUserId.HasValue)
        {
            query = query.Where(f => f.DentistUserId == request.DentistUserId.Value);
        }

        if (request.MinRating.HasValue)
        {
            query = query.Where(f => f.DentistRating >= request.MinRating.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            if (string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(f => f.Status == "Published" || f.Status == "Approved");
            }
            else
            {
                query = query.Where(f => f.Status == status);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var q = request.Search.Trim().ToLower();
            query = query.Where(f =>
                (f.Comments != null && f.Comments.ToLower().Contains(q)) ||
                f.Patient.FullName.ToLower().Contains(q) ||
                f.Dentist.StaffProfile.FullName.ToLower().Contains(q));
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(f => f.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(f => f.CreatedAt <= request.ToDate.Value);
        }

        var total = await query.CountAsync(ct);
        double overallDentist = total > 0 ? await query.AverageAsync(f => (double)f.DentistRating, ct) : 0;
        double overallClinic = total > 0 ? await query.AverageAsync(f => (double)f.ClinicRating, ct) : 0;

        var approvedCount = await _dbContext.VisitFeedbacks.CountAsync(f => f.Status == "Published", ct);
        var hiddenCount = await _dbContext.VisitFeedbacks.CountAsync(f => f.Status == "Hidden", ct);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var list = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var result = new FeedbackPagedResultDto
        {
            Items = list.Select(MapToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            OverallDentistRating = Math.Round(overallDentist, 1),
            OverallClinicRating = Math.Round(overallClinic, 1),
            ApprovedCount = approvedCount,
            HiddenCount = hiddenCount,
            PendingCount = 0
        };

        return ApiResponse<FeedbackPagedResultDto>.Ok(result);
    }

    public async Task<ApiResponse<VisitFeedbackDto>> ModerateFeedbackAsync(long feedbackId, ModerateFeedbackRequest request, long adminUserId, CancellationToken ct = default)
    {
        var feedback = await _dbContext.VisitFeedbacks.FirstOrDefaultAsync(f => f.FeedbackId == feedbackId, ct);
        if (feedback == null)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Không tìm thấy đánh giá.");
        }

        feedback.Status = string.Equals(request.Status, "Hidden", StringComparison.OrdinalIgnoreCase)
            ? "Hidden"
            : "Published";

        await _dbContext.SaveChangesAsync(ct);
        var dtoRes = await GetFeedbackDtoByIdAsync(feedback.FeedbackId, ct);
        if (dtoRes.Success && dtoRes.Data != null)
        {
            dtoRes.Data.AdminNotes = request.AdminNotes?.Trim();
        }
        return dtoRes;
    }

    public async Task<ApiResponse<VisitFeedbackDto>> RespondFeedbackAsync(long feedbackId, RespondFeedbackRequest request, long adminUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.AdminResponse))
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Nội dung phản hồi không được để trống.");
        }

        var feedback = await _dbContext.VisitFeedbacks.FirstOrDefaultAsync(f => f.FeedbackId == feedbackId, ct);
        if (feedback == null)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Không tìm thấy đánh giá.");
        }

        await _dbContext.SaveChangesAsync(ct);
        var dtoRes = await GetFeedbackDtoByIdAsync(feedback.FeedbackId, ct);
        if (dtoRes.Success && dtoRes.Data != null)
        {
            dtoRes.Data.AdminResponse = request.AdminResponse.Trim();
            dtoRes.Data.AdminRespondedByUserId = adminUserId;
        }
        return dtoRes;
    }

    private IQueryable<VisitFeedback> BuildQuery()
    {
        return _dbContext.VisitFeedbacks
            .AsNoTracking()
            .Include(f => f.Appointment)
            .Include(f => f.Patient)
            .Include(f => f.Dentist)
                .ThenInclude(d => d.StaffProfile)
            .Include(f => f.AppointmentService)
                .ThenInclude(s => s.DepartmentService)
                    .ThenInclude(ds => ds.Department);
    }

    private async Task<ApiResponse<VisitFeedbackDto>> GetFeedbackDtoByIdAsync(long feedbackId, CancellationToken ct)
    {
        var f = await BuildQuery().FirstOrDefaultAsync(x => x.FeedbackId == feedbackId, ct);
        if (f == null)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Không tìm thấy đánh giá.");
        }

        return ApiResponse<VisitFeedbackDto>.Ok(MapToDto(f));
    }

    private static VisitFeedbackDto MapToDto(VisitFeedback f)
    {
        return new VisitFeedbackDto
        {
            FeedbackId = f.FeedbackId,
            AppointmentId = f.AppointmentId,
            AppointmentCode = $"APT-{f.AppointmentId:D6}",
            PatientUserId = f.PatientUserId,
            PatientName = f.Patient?.FullName ?? "Bệnh nhân",
            DentistUserId = f.DentistUserId,
            DentistName = f.Dentist?.StaffProfile?.FullName,
            DepartmentId = f.AppointmentService?.DepartmentId,
            DepartmentName = f.AppointmentService?.DepartmentService?.Department?.Name,
            DentistRating = (byte)f.DentistRating,
            ClinicRating = (byte)f.ClinicRating,
            Comment = f.Comments,
            Status = f.Status == "Published" ? "Approved" : f.Status,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.CreatedAt,
            AppointmentDate = f.AppointmentService?.ScheduledStart ?? f.Appointment?.CreatedAt,
            ServiceName = f.AppointmentService?.DepartmentService?.ServiceName
        };
    }
}
