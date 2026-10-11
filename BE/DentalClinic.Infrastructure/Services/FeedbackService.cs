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
            .Include(a => a.PatientUser)
            .Include(a => a.AssignedDentistUser)
                .ThenInclude(d => d!.User)
            .Include(a => a.Department)
            .Include(a => a.RequestedService)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Không tìm thấy cuộc hẹn để đánh giá.");
        }

        if (appointment.PatientUserId != patientUserId)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Bạn chỉ có quyền đánh giá các ca khám của chính mình.");
        }

        if (appointment.Status != AppointmentStatus.Completed)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Chỉ có thể gửi đánh giá sau khi ca khám đã hoàn thành.");
        }

        var existingFeedback = await _dbContext.VisitFeedbacks
            .AnyAsync(f => f.AppointmentId == request.AppointmentId, ct);

        if (existingFeedback)
        {
            return ApiResponse<VisitFeedbackDto>.Fail("Ca khám này đã được gửi đánh giá trước đó.");
        }

        var feedback = new VisitFeedback
        {
            AppointmentId = request.AppointmentId,
            DentistRating = request.DentistRating,
            ClinicRating = request.ClinicRating,
            Comment = request.Comment?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.VisitFeedbacks.Add(feedback);
        await _dbContext.SaveChangesAsync(ct);

        var dto = new VisitFeedbackDto
        {
            FeedbackId = feedback.FeedbackId,
            AppointmentId = appointment.AppointmentId,
            AppointmentCode = appointment.AppointmentCode,
            PatientUserId = appointment.PatientUserId,
            PatientName = appointment.PatientUser.FullName,
            DentistUserId = appointment.AssignedDentistUserId,
            DentistName = appointment.AssignedDentistUser?.User.FullName,
            DepartmentId = appointment.DepartmentId,
            DepartmentName = appointment.Department?.Name,
            DentistRating = feedback.DentistRating,
            ClinicRating = feedback.ClinicRating,
            Comment = feedback.Comment,
            CreatedAt = feedback.CreatedAt,
            AppointmentDate = appointment.ScheduledStart,
            ServiceName = appointment.RequestedService?.ServiceName
        };

        return ApiResponse<VisitFeedbackDto>.Ok(dto, "Cảm ơn bạn đã gửi đánh giá và đóng góp ý kiến cho phòng khám!");
    }

    public async Task<ApiResponse<VisitFeedbackDto?>> GetFeedbackByAppointmentIdAsync(long appointmentId, CancellationToken ct = default)
    {
        var feedback = await _dbContext.VisitFeedbacks
            .AsNoTracking()
            .Include(f => f.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(f => f.Appointment)
                .ThenInclude(a => a.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(f => f.Appointment)
                .ThenInclude(a => a.Department)
            .Include(f => f.Appointment)
                .ThenInclude(a => a.RequestedService)
            .FirstOrDefaultAsync(f => f.AppointmentId == appointmentId, ct);

        if (feedback == null)
        {
            return ApiResponse<VisitFeedbackDto?>.Ok(null);
        }

        var appt = feedback.Appointment;
        var dto = new VisitFeedbackDto
        {
            FeedbackId = feedback.FeedbackId,
            AppointmentId = appt.AppointmentId,
            AppointmentCode = appt.AppointmentCode,
            PatientUserId = appt.PatientUserId,
            PatientName = appt.PatientUser.FullName,
            DentistUserId = appt.AssignedDentistUserId,
            DentistName = appt.AssignedDentistUser?.User.FullName,
            DepartmentId = appt.DepartmentId,
            DepartmentName = appt.Department?.Name,
            DentistRating = feedback.DentistRating,
            ClinicRating = feedback.ClinicRating,
            Comment = feedback.Comment,
            CreatedAt = feedback.CreatedAt,
            AppointmentDate = appt.ScheduledStart,
            ServiceName = appt.RequestedService?.ServiceName
        };

        return ApiResponse<VisitFeedbackDto?>.Ok(dto);
    }

    public async Task<ApiResponse<DentistFeedbackSummaryDto>> GetDentistFeedbackSummaryAsync(long dentistUserId, CancellationToken ct = default)
    {
        var dentist = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null)
        {
            return ApiResponse<DentistFeedbackSummaryDto>.Fail("Không tìm thấy thông tin bác sĩ.");
        }

        var feedbacks = await _dbContext.VisitFeedbacks
            .AsNoTracking()
            .Include(f => f.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(f => f.Appointment)
                .ThenInclude(a => a.Department)
            .Include(f => f.Appointment)
                .ThenInclude(a => a.RequestedService)
            .Where(f => f.Appointment.AssignedDentistUserId == dentistUserId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        var total = feedbacks.Count;
        double avgDentist = total > 0 ? feedbacks.Average(f => f.DentistRating) : 0;
        double avgClinic = total > 0 ? feedbacks.Average(f => f.ClinicRating) : 0;

        var items = feedbacks.Select(f => new VisitFeedbackDto
        {
            FeedbackId = f.FeedbackId,
            AppointmentId = f.Appointment.AppointmentId,
            AppointmentCode = f.Appointment.AppointmentCode,
            PatientUserId = f.Appointment.PatientUserId,
            PatientName = f.Appointment.PatientUser.FullName,
            DentistUserId = dentistUserId,
            DentistName = dentist.User.FullName,
            DepartmentId = f.Appointment.DepartmentId,
            DepartmentName = f.Appointment.Department?.Name,
            DentistRating = f.DentistRating,
            ClinicRating = f.ClinicRating,
            Comment = f.Comment,
            CreatedAt = f.CreatedAt,
            AppointmentDate = f.Appointment.ScheduledStart,
            ServiceName = f.Appointment.RequestedService?.ServiceName
        }).ToList();

        var summary = new DentistFeedbackSummaryDto
        {
            DentistUserId = dentistUserId,
            DentistName = dentist.User.FullName,
            TotalFeedbacks = total,
            AverageDentistRating = Math.Round(avgDentist, 1),
            AverageClinicRating = Math.Round(avgClinic, 1),
            RecentFeedbacks = items
        };

        return ApiResponse<DentistFeedbackSummaryDto>.Ok(summary);
    }

    public async Task<ApiResponse<List<VisitFeedbackDto>>> GetMyFeedbacksAsync(long patientUserId, CancellationToken ct = default)
    {
        var feedbacks = await _dbContext.VisitFeedbacks
            .AsNoTracking()
            .Include(f => f.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(f => f.Appointment)
                .ThenInclude(a => a.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(f => f.Appointment)
                .ThenInclude(a => a.Department)
            .Include(f => f.Appointment)
                .ThenInclude(a => a.RequestedService)
            .Where(f => f.Appointment.PatientUserId == patientUserId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new VisitFeedbackDto
            {
                FeedbackId = f.FeedbackId,
                AppointmentId = f.Appointment.AppointmentId,
                AppointmentCode = f.Appointment.AppointmentCode,
                PatientUserId = patientUserId,
                PatientName = f.Appointment.PatientUser.FullName,
                DentistUserId = f.Appointment.AssignedDentistUserId,
                DentistName = f.Appointment.AssignedDentistUser != null ? f.Appointment.AssignedDentistUser.User.FullName : null,
                DepartmentId = f.Appointment.DepartmentId,
                DepartmentName = f.Appointment.Department != null ? f.Appointment.Department.Name : null,
                DentistRating = f.DentistRating,
                ClinicRating = f.ClinicRating,
                Comment = f.Comment,
                CreatedAt = f.CreatedAt,
                AppointmentDate = f.Appointment.ScheduledStart,
                ServiceName = f.Appointment.RequestedService != null ? f.Appointment.RequestedService.ServiceName : null
            })
            .ToListAsync(ct);

        return ApiResponse<List<VisitFeedbackDto>>.Ok(feedbacks);
    }

    public async Task<ApiResponse<FeedbackPagedResultDto>> GetFeedbacksAsync(FeedbackFilterRequest request, CancellationToken ct = default)
    {
        var query = _dbContext.VisitFeedbacks
            .AsNoTracking()
            .Include(f => f.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(f => f.Appointment)
                .ThenInclude(a => a.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(f => f.Appointment)
                .ThenInclude(a => a.Department)
            .Include(f => f.Appointment)
                .ThenInclude(a => a.RequestedService)
            .AsQueryable();

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(f => f.Appointment.DepartmentId == request.DepartmentId.Value);
        }

        if (request.DentistUserId.HasValue)
        {
            query = query.Where(f => f.Appointment.AssignedDentistUserId == request.DentistUserId.Value);
        }

        if (request.MinRating.HasValue)
        {
            query = query.Where(f => f.DentistRating >= request.MinRating.Value);
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

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new VisitFeedbackDto
            {
                FeedbackId = f.FeedbackId,
                AppointmentId = f.Appointment.AppointmentId,
                AppointmentCode = f.Appointment.AppointmentCode,
                PatientUserId = f.Appointment.PatientUserId,
                PatientName = f.Appointment.PatientUser.FullName,
                DentistUserId = f.Appointment.AssignedDentistUserId,
                DentistName = f.Appointment.AssignedDentistUser != null ? f.Appointment.AssignedDentistUser.User.FullName : null,
                DepartmentId = f.Appointment.DepartmentId,
                DepartmentName = f.Appointment.Department != null ? f.Appointment.Department.Name : null,
                DentistRating = f.DentistRating,
                ClinicRating = f.ClinicRating,
                Comment = f.Comment,
                CreatedAt = f.CreatedAt,
                AppointmentDate = f.Appointment.ScheduledStart,
                ServiceName = f.Appointment.RequestedService != null ? f.Appointment.RequestedService.ServiceName : null
            })
            .ToListAsync(ct);

        var result = new FeedbackPagedResultDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            OverallDentistRating = Math.Round(overallDentist, 1),
            OverallClinicRating = Math.Round(overallClinic, 1)
        };

        return ApiResponse<FeedbackPagedResultDto>.Ok(result);
    }
}
