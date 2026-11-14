using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.CareEpisodes.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

namespace DentalClinic.Infrastructure.Services;

public class CareEpisodeService : ICareEpisodeService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly ILogger<CareEpisodeService> _logger;
    private readonly INotificationService? _notificationService;

    public CareEpisodeService(
        DentalClinicDbContext dbContext,
        ILogger<CareEpisodeService> logger,
        INotificationService? notificationService = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _notificationService = notificationService;
    }

    #region 1. Episode Lifecycle Management

    public async Task<ApiResponse<CareEpisodeDto>> GetOrCreateActiveEpisodeForAppointmentAsync(long appointmentId, CancellationToken ct = default)
    {
        return await GetEpisodeByIdAsync(appointmentId, ct);
    }

    public async Task<ApiResponse<CareEpisodeDto>> GetEpisodeByIdAsync(long careEpisodeId, CancellationToken ct = default)
    {
        var appointment = await LoadAppointmentEpisodeGraphAsync(careEpisodeId, ct);
        if (appointment == null)
            return ApiResponse<CareEpisodeDto>.Fail("Không tìm thấy đợt khám chỉ định.");

        var referrals = await LoadReferralsForAppointmentAsync(appointment.AppointmentId, ct);
        var dto = MapAppointmentToCareEpisodeDto(appointment, referrals);
        return ApiResponse<CareEpisodeDto>.Ok(dto);
    }

    public async Task<ApiResponse<CareEpisodeDto>> GetEpisodeByAppointmentIdAsync(long appointmentId, CancellationToken ct = default)
    {
        return await GetEpisodeByIdAsync(appointmentId, ct);
    }

    public async Task<ApiResponse<CareEpisodeDto>> GetPatientActiveEpisodeAsync(long patientUserId, CancellationToken ct = default)
    {
        var activeStatuses = new[]
        {
            AppointmentStatus.PendingReceptionReview,
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.InProgress,
            AppointmentStatus.InService,
            "WaitingForFollowUp"
        };

        var appointmentId = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.PatientUserId == patientUserId && activeStatuses.Contains(a.Status))
            .OrderByDescending(a => a.AppointmentId)
            .Select(a => (long?)a.AppointmentId)
            .FirstOrDefaultAsync(ct);

        if (!appointmentId.HasValue)
            return ApiResponse<CareEpisodeDto>.Fail("Hiện tại bệnh nhân không có đợt khám nào đang diễn ra.");

        return await GetEpisodeByIdAsync(appointmentId.Value, ct);
    }

    #endregion

    #region 2. Case A: Additional Services within Same Appointment (Doctor A)

    public async Task<ApiResponse<List<EpisodeServiceTaskDto>>> GetEligibleServicesForDentistAsync(long appointmentId, long dentistUserId, CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
            return ApiResponse<List<EpisodeServiceTaskDto>>.Fail("Không tìm thấy ca khám.");

        var dentist = await _dbContext.DentistProfiles
            .Include(d => d.StaffProfile)
            .Include(d => d.Department)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null || dentist.Department == null || dentist.Department.Status != "Active")
            return ApiResponse<List<EpisodeServiceTaskDto>>.Ok(new List<EpisodeServiceTaskDto>(), "Bác sĩ chưa được gán chuyên khoa hoạt động.");

        var services = await _dbContext.DepartmentServices
            .Include(s => s.Department)
            .AsNoTracking()
            .Where(s => s.DepartmentId == dentist.DepartmentId && s.Status == "Active")
            .OrderBy(s => s.ServiceName)
            .ToListAsync(ct);

        var apptCode = FormatAppointmentCode(appointment.AppointmentId);
        var dentistName = dentist.StaffProfile?.FullName ?? string.Empty;

        var result = services.Select(s => new EpisodeServiceTaskDto
        {
            ServiceTaskId = 0,
            TaskCode = string.Empty,
            CareEpisodeId = appointment.AppointmentId,
            AppointmentId = appointment.AppointmentId,
            AppointmentCode = apptCode,
            DepartmentServiceId = s.DepartmentServiceId,
            ServiceName = s.ServiceName,
            DepartmentId = s.DepartmentId,
            DepartmentName = s.Department.Name,
            PerformingDentistUserId = dentistUserId,
            PerformingDentistName = dentistName,
            Status = "Planned",
            Quantity = 1,
            UnitPriceSnapshot = s.Price,
            EstimatedDurationMinutes = s.DurationMinutes,
            PlannedAtUtc = DateTime.UtcNow
        }).ToList();

        return ApiResponse<List<EpisodeServiceTaskDto>>.Ok(result);
    }

    public async Task<ApiResponse<EpisodeServiceTaskDto>> AddServiceTaskAsync(long dentistUserId, CreateEpisodeServiceTaskRequest request, CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Dữ liệu chỉ định dịch vụ không hợp lệ.");

        if (request.AppointmentId <= 0)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Mã ca khám không hợp lệ.");

        if (request.DepartmentServiceId <= 0)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Vui lòng chọn dịch vụ cần thực hiện.");

        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

        if (appointment == null)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Không tìm thấy ca khám chỉ định.");

        var service = await _dbContext.DepartmentServices
            .Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == (int)request.DepartmentServiceId && s.Status == "Active", ct);

        if (service == null)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Dịch vụ được chọn không tồn tại hoặc đã ngừng hoạt động.");

        var dentist = await _dbContext.DentistProfiles
            .Include(d => d.StaffProfile)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null || dentist.DepartmentId != service.DepartmentId)
        {
            return ApiResponse<EpisodeServiceTaskDto>.Fail(
                $"Bác sĩ không thuộc chuyên khoa '{service.Department.Name}' cung cấp dịch vụ này. Vui lòng chuyển tiếp bệnh nhân sang bác sĩ chuyên khoa phù hợp (Internal Referral).");
        }

        var referenceItem = appointment.AppointmentServices
            .OrderByDescending(s => s.ScheduledEnd)
            .FirstOrDefault();

        var scheduledStart = referenceItem?.ScheduledEnd ?? DateTime.UtcNow;
        var durationMinutes = service.DurationMinutes > 0 ? service.DurationMinutes : 30;
        var scheduledEnd = scheduledStart.AddMinutes(durationMinutes);
        var serviceDate = DateOnly.FromDateTime(scheduledStart);

        var nextQueueNumber = await AllocateDepartmentQueueNumberAsync(service.DepartmentId, serviceDate, ct);

        var apptService = new AppointmentServiceEntity
        {
            AppointmentId = appointment.AppointmentId,
            DepartmentServiceId = service.DepartmentServiceId,
            DepartmentId = service.DepartmentId,
            RequestedDentistUserId = dentistUserId,
            AssignedDentistUserId = dentistUserId,
            RoomId = referenceItem?.RoomId,
            DentalChairId = referenceItem?.DentalChairId,
            ServiceDate = serviceDate,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledEnd,
            QueueNumber = nextQueueNumber,
            UnitPrice = service.Price,
            Status = "Confirmed",
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.AppointmentServices.Add(apptService);
        appointment.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        apptService.DepartmentService = service;

        _logger.LogInformation("Đã thêm chỉ định dịch vụ #{AppointmentServiceId} ({ServiceName}) cho ca khám #{AppointmentId} của bác sĩ {DentistUserId}",
            apptService.AppointmentServiceId, service.ServiceName, appointment.AppointmentId, dentistUserId);

        var dto = MapAppointmentServiceToTaskDto(apptService, dentist.StaffProfile?.FullName);
        dto.Quantity = request.Quantity > 0 ? request.Quantity : 1;

        return ApiResponse<EpisodeServiceTaskDto>.Ok(dto, "Chỉ định dịch vụ bổ sung thành công.");
    }

    public async Task<ApiResponse<EpisodeServiceTaskDto>> StartServiceTaskAsync(long dentistUserId, long serviceTaskId, CancellationToken ct = default)
    {
        var apptService = await _dbContext.AppointmentServices
            .Include(s => s.Appointment)
            .Include(s => s.DepartmentService)
                .ThenInclude(ds => ds.Department)
            .Include(s => s.AssignedDentist)
                .ThenInclude(d => d!.StaffProfile)
            .Include(s => s.TreatmentSessions)
                .ThenInclude(ts => ts.PerformedServices)
            .FirstOrDefaultAsync(s => s.AppointmentServiceId == serviceTaskId, ct);

        if (apptService == null)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Không tìm thấy tác vụ điều trị chỉ định.");

        var isAuthorized = apptService.AssignedDentistUserId == dentistUserId ||
            await _dbContext.DentistProfiles.AnyAsync(d => d.UserId == dentistUserId && d.DepartmentId == apptService.DepartmentId, ct);

        if (!isAuthorized)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Bạn không có quyền thực hiện tác vụ của chuyên khoa/bác sĩ khác.");

        if (apptService.Status == "Completed")
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Tác vụ này đã hoàn thành, không thể bắt đầu lại.");

        if (apptService.Status == "Cancelled")
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Tác vụ này đã bị hủy.");

        apptService.AssignedDentistUserId ??= dentistUserId;
        apptService.Status = "InProgress";
        apptService.UpdatedAt = DateTime.UtcNow;

        if (apptService.Appointment.Status == AppointmentStatus.Confirmed || apptService.Appointment.Status == AppointmentStatus.CheckedIn)
        {
            apptService.Appointment.Status = AppointmentStatus.InProgress;
            apptService.Appointment.UpdatedAt = DateTime.UtcNow;
        }

        var session = apptService.TreatmentSessions.OrderByDescending(ts => ts.SessionId).FirstOrDefault();
        if (session == null)
        {
            session = new TreatmentSession
            {
                AppointmentId = apptService.AppointmentId,
                AppointmentServiceId = apptService.AppointmentServiceId,
                PatientUserId = apptService.Appointment.PatientUserId,
                DentistUserId = dentistUserId,
                SessionDate = apptService.ServiceDate,
                Notes = apptService.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.TreatmentSessions.Add(session);
        }

        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<EpisodeServiceTaskDto>.Ok(MapAppointmentServiceToTaskDto(apptService), "Bắt đầu thực hiện dịch vụ thành công.");
    }

    public async Task<ApiResponse<EpisodeTaskStepProgressDto>> CompleteTaskStepAsync(
        long dentistUserId,
        long serviceTaskId,
        long stepId,
        CompleteTaskStepRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<EpisodeTaskStepProgressDto>.Fail("Dữ liệu xác nhận bước không hợp lệ.");

        var apptService = await _dbContext.AppointmentServices
            .Include(s => s.Appointment)
            .Include(s => s.DepartmentService)
            .FirstOrDefaultAsync(s => s.AppointmentServiceId == serviceTaskId, ct);

        if (apptService == null)
            return ApiResponse<EpisodeTaskStepProgressDto>.Fail("Không tìm thấy tác vụ điều trị chỉ định.");

        var isAuthorized = apptService.AssignedDentistUserId == dentistUserId ||
            await _dbContext.DentistProfiles.AnyAsync(d => d.UserId == dentistUserId && d.DepartmentId == apptService.DepartmentId, ct);

        if (!isAuthorized)
            return ApiResponse<EpisodeTaskStepProgressDto>.Fail("Bạn không có quyền cập nhật tiến độ tác vụ của bác sĩ khác.");

        if (apptService.Status == "Completed")
            return ApiResponse<EpisodeTaskStepProgressDto>.Fail("Tác vụ này đã hoàn thành.");

        if (apptService.Status == "Cancelled")
            return ApiResponse<EpisodeTaskStepProgressDto>.Fail("Tác vụ này đã bị hủy.");

        if (apptService.Status != "InProgress")
        {
            apptService.Status = "InProgress";
            apptService.UpdatedAt = DateTime.UtcNow;
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            apptService.Notes = string.IsNullOrWhiteSpace(apptService.Notes)
                ? request.Notes.Trim()
                : $"{apptService.Notes} | {request.Notes.Trim()}";
            apptService.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);

        var dentistName = await _dbContext.StaffProfiles
            .AsNoTracking()
            .Where(s => s.UserId == dentistUserId)
            .Select(s => s.FullName)
            .FirstOrDefaultAsync(ct);

        var dto = new EpisodeTaskStepProgressDto
        {
            ProgressId = stepId,
            ServiceTaskId = serviceTaskId,
            StepId = stepId,
            StepName = apptService.DepartmentService.ServiceName,
            StepOrder = 1,
            IsRequired = true,
            ExpectedDataType = "None",
            Status = "Completed",
            ResultValue = request.ResultValue?.Trim(),
            Notes = request.Notes?.Trim(),
            EvidenceUrl = request.EvidenceUrl?.Trim(),
            CompletedByDentistUserId = dentistUserId,
            CompletedByDentistName = dentistName ?? string.Empty,
            StartedAtUtc = apptService.CreatedAt,
            CompletedAtUtc = DateTime.UtcNow
        };

        return ApiResponse<EpisodeTaskStepProgressDto>.Ok(dto, "Đã xác nhận hoàn thành bước điều trị.");
    }

    public async Task<ApiResponse<EpisodeServiceTaskDto>> CompleteServiceTaskAsync(
        long dentistUserId,
        long serviceTaskId,
        CompleteServiceTaskRequest request,
        CancellationToken ct = default)
    {
        var apptService = await _dbContext.AppointmentServices
            .Include(s => s.Appointment)
            .Include(s => s.DepartmentService)
                .ThenInclude(ds => ds.Department)
            .Include(s => s.AssignedDentist)
                .ThenInclude(d => d!.StaffProfile)
            .Include(s => s.TreatmentSessions)
                .ThenInclude(ts => ts.PerformedServices)
            .FirstOrDefaultAsync(s => s.AppointmentServiceId == serviceTaskId, ct);

        if (apptService == null)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Không tìm thấy tác vụ điều trị chỉ định.");

        var isAuthorized = apptService.AssignedDentistUserId == dentistUserId ||
            await _dbContext.DentistProfiles.AnyAsync(d => d.UserId == dentistUserId && d.DepartmentId == apptService.DepartmentId, ct);

        if (!isAuthorized)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Bạn không có quyền hoàn thành tác vụ của bác sĩ khác.");

        if (apptService.Status == "Completed")
            return ApiResponse<EpisodeServiceTaskDto>.Ok(MapAppointmentServiceToTaskDto(apptService), "Tác vụ đã được hoàn thành trước đó.");

        if (apptService.Status == "Cancelled")
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Tác vụ này đã bị hủy.");

        apptService.AssignedDentistUserId ??= dentistUserId;
        apptService.Status = "Completed";
        if (!string.IsNullOrWhiteSpace(request?.Notes))
        {
            apptService.Notes = request.Notes.Trim();
        }
        apptService.UpdatedAt = DateTime.UtcNow;

        await EnsurePerformedServiceRecordedAsync(apptService, dentistUserId, request?.ClinicalFindings, request?.Notes, ct);
        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<EpisodeServiceTaskDto>.Ok(
            MapAppointmentServiceToTaskDto(apptService),
            $"Đã hoàn thành dịch vụ '{apptService.DepartmentService.ServiceName}'.");
    }

    public async Task<ApiResponse<bool>> ValidateAppointmentTasksCompletedAsync(long appointmentId, CancellationToken ct = default)
    {
        var services = await _dbContext.AppointmentServices
            .Include(s => s.DepartmentService)
            .AsNoTracking()
            .Where(s => s.AppointmentId == appointmentId && s.Status != "Cancelled" && s.Status != "NoShow")
            .ToListAsync(ct);

        if (!services.Any())
            return ApiResponse<bool>.Ok(true, "Ca khám không có tác vụ nào cần xử lý.");

        var incomplete = services.Where(s => s.Status != "Completed").ToList();
        if (incomplete.Any())
        {
            var names = string.Join(", ", incomplete.Select(s => s.DepartmentService?.ServiceName ?? $"#{s.AppointmentServiceId}"));
            return ApiResponse<bool>.Fail($"Ca khám còn các dịch vụ chưa hoàn thành: {names}. Vui lòng hoàn thành các dịch vụ trước khi kết thúc ca khám.");
        }

        return ApiResponse<bool>.Ok(true, "Tất cả các dịch vụ thuộc ca khám đã hoàn thành.");
    }

    public async Task<ApiResponse<EpisodeServiceTaskDto>> UpdateServiceTaskStatusAsync(
        long dentistUserId,
        long serviceTaskId,
        UpdateServiceTaskStatusRequest request,
        CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Status))
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Dữ liệu cập nhật trạng thái không hợp lệ.");

        var apptService = await _dbContext.AppointmentServices
            .Include(s => s.Appointment)
            .Include(s => s.DepartmentService)
                .ThenInclude(ds => ds.Department)
            .Include(s => s.AssignedDentist)
                .ThenInclude(d => d!.StaffProfile)
            .Include(s => s.TreatmentSessions)
                .ThenInclude(ts => ts.PerformedServices)
            .FirstOrDefaultAsync(s => s.AppointmentServiceId == serviceTaskId, ct);

        if (apptService == null)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Không tìm thấy tác vụ điều trị chỉ định.");

        var targetStatus = request.Status.Trim();
        if (string.Equals(targetStatus, "Planned", StringComparison.OrdinalIgnoreCase))
        {
            apptService.Status = "Confirmed";
        }
        else if (string.Equals(targetStatus, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            return await StartServiceTaskAsync(dentistUserId, serviceTaskId, ct);
        }
        else if (string.Equals(targetStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return await CompleteServiceTaskAsync(dentistUserId, serviceTaskId, new CompleteServiceTaskRequest
            {
                ClinicalRecordId = request.ClinicalRecordId,
                Notes = request.Notes
            }, ct);
        }
        else if (string.Equals(targetStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            apptService.Status = "Cancelled";
            var cancelNote = request.CancellationReason?.Trim() ?? request.Notes?.Trim() ?? "Hủy theo yêu cầu lâm sàng";
            apptService.Notes = cancelNote;
        }
        else
        {
            return ApiResponse<EpisodeServiceTaskDto>.Fail($"Trạng thái '{request.Status}' không hợp lệ.");
        }

        apptService.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<EpisodeServiceTaskDto>.Ok(MapAppointmentServiceToTaskDto(apptService), "Cập nhật trạng thái dịch vụ thành công.");
    }

    #endregion

    #region 3. Case B: Cross-Dentist Referral & Handoff (Doctor A -> Receptionist -> Doctor B)

    public async Task<ApiResponse<CareEpisodeReferralSummaryDto>> CreateInternalReferralAsync(long dentistUserId, CreateCareEpisodeReferralRequest request, CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Dữ liệu chuyển tiếp chuyên khoa không hợp lệ.");

        if (request.SourceAppointmentId <= 0)
            return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Vui lòng chỉ định ca khám nguồn.");

        if (request.ToDepartmentId <= 0 && request.RequestedServiceId <= 0)
            return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Vui lòng chọn chuyên khoa hoặc dịch vụ chuyển đến.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Vui lòng nhập lý do chuyển tiếp và chẩn đoán lâm sàng.");

        var sourceApptService = await _dbContext.AppointmentServices
            .Include(s => s.Appointment)
            .Include(s => s.DepartmentService)
                .ThenInclude(ds => ds.Department)
            .Include(s => s.TreatmentSessions)
            .FirstOrDefaultAsync(s => s.AppointmentServiceId == request.SourceAppointmentId, ct);

        Appointment? sourceAppt = sourceApptService?.Appointment;
        if (sourceAppt == null)
        {
            sourceAppt = await _dbContext.Appointments
                .Include(a => a.AppointmentServices)
                    .ThenInclude(s => s.DepartmentService)
                        .ThenInclude(ds => ds.Department)
                .Include(a => a.AppointmentServices)
                    .ThenInclude(s => s.TreatmentSessions)
                .FirstOrDefaultAsync(a => a.AppointmentId == request.SourceAppointmentId, ct);

            sourceApptService = sourceAppt?.AppointmentServices
                .OrderByDescending(s => s.AssignedDentistUserId == dentistUserId)
                .ThenBy(s => s.AppointmentServiceId)
                .FirstOrDefault();
        }

        if (sourceAppt == null)
            return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Không tìm thấy ca khám nguồn.");

        DepartmentService? requestedService = null;
        int toDepartmentId = (int)request.ToDepartmentId;
        if (request.RequestedServiceId > 0)
        {
            requestedService = await _dbContext.DepartmentServices
                .FirstOrDefaultAsync(s => s.DepartmentServiceId == (int)request.RequestedServiceId && s.Status == "Active", ct);

            if (requestedService == null)
                return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Dịch vụ yêu cầu không tồn tại hoặc đã ngừng hoạt động.");

            toDepartmentId = requestedService.DepartmentId;
        }

        var targetDept = await _dbContext.Departments.FirstOrDefaultAsync(d => d.DepartmentId == toDepartmentId && d.Status == "Active", ct);
        if (targetDept == null)
            return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Chuyên khoa chuyển đến không tồn tại hoặc đã ngừng hoạt động.");

        int fromDepartmentId = sourceApptService?.DepartmentId
            ?? await _dbContext.DentistProfiles
                .Where(d => d.UserId == dentistUserId)
                .Select(d => (int?)d.DepartmentId)
                .FirstOrDefaultAsync(ct)
            ?? targetDept.DepartmentId;

        var fromDept = await _dbContext.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.DepartmentId == fromDepartmentId, ct);
        var session = sourceApptService?.TreatmentSessions.OrderByDescending(ts => ts.SessionId).FirstOrDefault();

        var urgency = NormalizeUrgency(request.Urgency);

        var referral = new DepartmentReferral
        {
            TreatmentSessionId = session?.SessionId,
            FromDepartmentId = fromDepartmentId,
            ToDepartmentId = targetDept.DepartmentId,
            RequestedServiceId = requestedService?.DepartmentServiceId,
            AssignedDentistUserId = request.PreferredDentistUserId,
            Reason = request.Reason.Trim(),
            ClinicalInstructions = request.ClinicalInstructions?.Trim(),
            Urgency = urgency,
            Status = "Pending",
            SourceAppointmentServiceId = sourceApptService?.AppointmentServiceId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DepartmentReferrals.Add(referral);
        await _dbContext.SaveChangesAsync(ct);

        var dto = new CareEpisodeReferralSummaryDto
        {
            ReferralId = referral.ReferralId,
            SourceAppointmentId = sourceAppt.AppointmentId,
            SourceAppointmentCode = FormatAppointmentCode(sourceAppt.AppointmentId),
            FromDepartmentId = referral.FromDepartmentId,
            FromDepartmentName = fromDept?.Name ?? string.Empty,
            ToDepartmentId = referral.ToDepartmentId,
            ToDepartmentName = targetDept.Name,
            RequestedServiceId = referral.RequestedServiceId,
            RequestedServiceName = requestedService?.ServiceName,
            AssignedDentistUserId = referral.AssignedDentistUserId,
            Reason = referral.Reason,
            Urgency = referral.Urgency,
            Status = referral.Status,
            ClinicalInstructions = referral.ClinicalInstructions,
            CreatedAt = referral.CreatedAt
        };

        return ApiResponse<CareEpisodeReferralSummaryDto>.Ok(dto, "Tạo yêu cầu chuyển chuyên khoa thành công. Lễ tân sẽ điều phối lịch hẹn tiếp theo.");
    }

    public async Task<ApiResponse<List<CareEpisodeReferralSummaryDto>>> GetPendingCareEpisodeReferralsAsync(CancellationToken ct = default)
    {
        var referrals = await _dbContext.DepartmentReferrals
            .Include(r => r.FromDepartment)
            .Include(r => r.ToDepartment)
            .Include(r => r.RequestedService)
            .Include(r => r.AssignedDentist)
                .ThenInclude(d => d!.StaffProfile)
            .Include(r => r.SourceAppointmentService)
            .Include(r => r.DestinationAppointmentService)
            .Include(r => r.TreatmentSession)
            .AsNoTracking()
            .Where(r => r.Status == "Pending")
            .OrderByDescending(r => r.Urgency == "Emergency")
            .ThenByDescending(r => r.Urgency == "Urgent")
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(ct);

        var list = referrals.Select(MapReferralToDto).ToList();
        return ApiResponse<List<CareEpisodeReferralSummaryDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<AvailableDentistForReferralDto>>> GetEligibleDentistsForReferralAsync(long referralId, DateTime targetDate, CancellationToken ct = default)
    {
        var referral = await _dbContext.DepartmentReferrals
            .Include(r => r.ToDepartment)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReferralId == referralId, ct);

        if (referral == null)
            return ApiResponse<List<AvailableDentistForReferralDto>>.Fail("Không tìm thấy yêu cầu chuyển tiếp chỉ định.");

        var targetDeptId = referral.ToDepartmentId;
        var dateOnly = DateOnly.FromDateTime(targetDate);

        var activeDentists = await _dbContext.DentistProfiles
            .Include(d => d.StaffProfile)
                .ThenInclude(s => s.User)
            .AsNoTracking()
            .Where(d => d.DepartmentId == targetDeptId && d.StaffProfile.User.Status == "Active")
            .ToListAsync(ct);

        var result = new List<AvailableDentistForReferralDto>();

        foreach (var dentist in activeDentists)
        {
            var hasLeave = await _dbContext.StaffLeaveRequests
                .AnyAsync(l => l.StaffUserId == dentist.UserId &&
                               l.Status == "Approved" &&
                               l.StartDate <= dateOnly &&
                               l.EndDate >= dateOnly, ct);

            bool isAvailable = !hasLeave;
            string? unavailReason = hasLeave
                ? "Bác sĩ có lịch nghỉ phép đã được phê duyệt trong ngày này."
                : null;

            result.Add(new AvailableDentistForReferralDto
            {
                DentistUserId = dentist.UserId,
                FullName = dentist.StaffProfile?.FullName ?? "Bác sĩ",
                DentistCode = dentist.LicenseNumber,
                Qualifications = dentist.Qualification,
                DepartmentId = targetDeptId,
                DepartmentName = referral.ToDepartment.Name,
                IsAvailable = isAvailable,
                UnavailabilityReason = unavailReason,
                AvailableTimeSlots = isAvailable ? new List<string> { "08:00 - 11:30", "13:30 - 17:00" } : new List<string>()
            });
        }

        return ApiResponse<List<AvailableDentistForReferralDto>>.Ok(result);
    }

    public async Task<ApiResponse<CareEpisodeAppointmentSummaryDto>> CoordinateReferralAppointmentAsync(long receptionistUserId, CoordinateCareEpisodeReferralRequest request, CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Dữ liệu điều phối không hợp lệ.");

        if (request.ReferralId <= 0)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Mã yêu cầu chuyển chuyên khoa không hợp lệ.");

        if (request.AssignedDentistUserId <= 0)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Vui lòng phân công bác sĩ tiếp nhận.");

        var lockKey = SqlAppLockHelper.CareEpisodeReferralKey(request.ReferralId);
        using var appLock = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, new[] { lockKey }, 5000, ct);

        var referral = await _dbContext.DepartmentReferrals
            .Include(r => r.SourceAppointmentService)
                .ThenInclude(s => s!.Appointment)
            .Include(r => r.TreatmentSession)
                .ThenInclude(ts => ts!.Appointment)
            .Include(r => r.ToDepartment)
            .FirstOrDefaultAsync(r => r.ReferralId == request.ReferralId, ct);

        if (referral == null)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Không tìm thấy yêu cầu chuyển tiếp.");

        if (referral.Status != "Pending")
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail($"Yêu cầu này đã được xử lý trước đó (Trạng thái: {referral.Status}).");

        var assignedDentist = await _dbContext.DentistProfiles
            .Include(d => d.StaffProfile)
            .FirstOrDefaultAsync(d => d.UserId == request.AssignedDentistUserId && d.DepartmentId == referral.ToDepartmentId, ct);

        if (assignedDentist == null)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Bác sĩ được chọn không thuộc chuyên khoa tiếp nhận.");

        var scheduledStart = request.ScheduledStart;
        var scheduledEnd = request.ScheduledEnd ?? scheduledStart.AddMinutes(30);

        if (scheduledStart >= scheduledEnd)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Thời gian bắt đầu ca khám phải trước thời gian kết thúc.");

        var dateOnly = DateOnly.FromDateTime(scheduledStart);

        var hasLeave = await _dbContext.StaffLeaveRequests
            .AnyAsync(l => l.StaffUserId == request.AssignedDentistUserId &&
                           l.Status == "Approved" &&
                           l.StartDate <= dateOnly &&
                           l.EndDate >= dateOnly, ct);

        if (hasLeave)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Bác sĩ tiếp nhận có lịch nghỉ phép đã phê duyệt trong khung giờ này.");

        var inactiveServiceStatuses = new[] { "Cancelled", "NoShow" };

        var hasDentistOverlap = await _dbContext.AppointmentServices
            .AnyAsync(s => s.AssignedDentistUserId == request.AssignedDentistUserId &&
                           s.ScheduledStart < scheduledEnd &&
                           s.ScheduledEnd > scheduledStart &&
                           !inactiveServiceStatuses.Contains(s.Status), ct);

        if (hasDentistOverlap)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Bác sĩ đã có lịch hẹn khác trùng khung giờ này.");

        int? chairId = request.DentalChairId.HasValue && request.DentalChairId.Value > 0
            ? (int)request.DentalChairId.Value
            : null;
        int? roomId = request.RoomId.HasValue && request.RoomId.Value > 0
            ? (int)request.RoomId.Value
            : null;

        if (chairId.HasValue)
        {
            var chair = await _dbContext.DentalChairs
                .FirstOrDefaultAsync(c => c.DentalChairId == chairId.Value && c.Status == "Active", ct);
            if (chair == null)
                return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Ghế điều trị không tồn tại hoặc đã ngừng hoạt động.");

            roomId ??= chair.RoomId;

            var hasChairOverlap = await _dbContext.AppointmentServices
                .AnyAsync(s => s.DentalChairId == chairId.Value &&
                               s.ScheduledStart < scheduledEnd &&
                               s.ScheduledEnd > scheduledStart &&
                               !inactiveServiceStatuses.Contains(s.Status), ct);

            if (hasChairOverlap)
                return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Ghế điều trị đã có ca hẹn khác sử dụng trong khung giờ này.");
        }

        var parentAppointment = referral.SourceAppointmentService?.Appointment ?? referral.TreatmentSession?.Appointment;
        if (parentAppointment == null)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Không xác định được hồ sơ cuộc hẹn gốc của yêu cầu chuyển khoa.");

        DepartmentService? targetService = null;
        if (referral.RequestedServiceId.HasValue)
        {
            targetService = await _dbContext.DepartmentServices
                .FirstOrDefaultAsync(s => s.DepartmentServiceId == referral.RequestedServiceId.Value && s.Status == "Active", ct);
        }
        targetService ??= await _dbContext.DepartmentServices
            .Where(s => s.DepartmentId == referral.ToDepartmentId && s.Status == "Active")
            .OrderBy(s => s.DepartmentServiceId)
            .FirstOrDefaultAsync(ct);

        if (targetService == null)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Chuyên khoa tiếp nhận chưa có dịch vụ hoạt động để tạo lịch hẹn.");

        var queueNumberInt = await AllocateDepartmentQueueNumberAsync(referral.ToDepartmentId, dateOnly, ct);
        var formattedQueue = FormatQueueNumber(referral.ToDepartment?.Name, referral.ToDepartmentId, queueNumberInt);

        var destApptService = new AppointmentServiceEntity
        {
            AppointmentId = parentAppointment.AppointmentId,
            DepartmentServiceId = targetService.DepartmentServiceId,
            DepartmentId = referral.ToDepartmentId,
            RequestedDentistUserId = request.AssignedDentistUserId,
            AssignedDentistUserId = request.AssignedDentistUserId,
            RoomId = roomId,
            DentalChairId = chairId,
            ServiceDate = dateOnly,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledEnd,
            QueueNumber = queueNumberInt,
            UnitPrice = targetService.Price,
            Status = "Confirmed",
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? $"Chuyển chuyên khoa từ #{referral.ReferralId}: {referral.Reason}"
                : request.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.AppointmentServices.Add(destApptService);
        await _dbContext.SaveChangesAsync(ct);

        referral.DestinationAppointmentServiceId = destApptService.AppointmentServiceId;
        referral.AssignedDentistUserId = request.AssignedDentistUserId;
        referral.ReviewedByUserId = receptionistUserId;
        referral.Status = "Accepted";
        referral.ReviewedAt = DateTime.UtcNow;

        parentAppointment.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        if (_notificationService != null && parentAppointment.PatientUserId > 0)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    parentAppointment.PatientUserId,
                    parentAppointment.AppointmentId,
                    "AppointmentConfirmed",
                    $"Xác nhận lịch hẹn chuyển chuyên khoa #{FormatAppointmentCode(parentAppointment.AppointmentId)}",
                    $"Lịch khám chuyển chuyên khoa của bạn tại {referral.ToDepartment?.Name ?? "phòng khám"} đã được xác nhận. Số thứ tự: {formattedQueue}.",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi gửi thông báo lịch hẹn cho bệnh nhân {PatientUserId}", parentAppointment.PatientUserId);
            }
        }

        if (_notificationService != null && request.AssignedDentistUserId > 0)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    request.AssignedDentistUserId,
                    parentAppointment.AppointmentId,
                    "AppointmentAssigned",
                    $"Phân công ca khám chuyển khoa #{FormatAppointmentCode(parentAppointment.AppointmentId)}",
                    $"Bạn được phân công tiếp nhận dịch vụ chuyển khoa ({referral.ToDepartment?.Name}). STT: {formattedQueue}.",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi gửi thông báo phân công cho bác sĩ {DentistUserId}", request.AssignedDentistUserId);
            }
        }

        var dto = new CareEpisodeAppointmentSummaryDto
        {
            AppointmentId = destApptService.AppointmentServiceId,
            AppointmentCode = FormatAppointmentCode(parentAppointment.AppointmentId),
            DepartmentId = destApptService.DepartmentId,
            DepartmentName = referral.ToDepartment?.Name,
            RequestedServiceId = destApptService.DepartmentServiceId,
            RequestedServiceName = targetService.ServiceName,
            AssignedDentistUserId = destApptService.AssignedDentistUserId,
            AssignedDentistName = assignedDentist.StaffProfile?.FullName,
            RoomId = destApptService.RoomId,
            DentalChairId = destApptService.DentalChairId,
            ScheduledStart = destApptService.ScheduledStart,
            ScheduledEnd = destApptService.ScheduledEnd,
            Status = destApptService.Status,
            QueueNumber = formattedQueue,
            IsRoot = false,
            ReasonForVisit = destApptService.Notes
        };

        return ApiResponse<CareEpisodeAppointmentSummaryDto>.Ok(dto, $"Điều phối ca khám chuyển khoa thành công. Số thứ tự: {formattedQueue}.");
    }

    public async Task<ApiResponse<CareEpisodeAppointmentSummaryDto>> StationBCheckInAsync(long receptionistUserId, StationBCheckInRequest request, CancellationToken ct = default)
    {
        if (request == null || request.AppointmentId <= 0)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Mã ca khám không hợp lệ.");

        var apptService = await _dbContext.AppointmentServices
            .Include(s => s.Appointment)
            .Include(s => s.DepartmentService)
                .ThenInclude(ds => ds.Department)
            .Include(s => s.AssignedDentist)
                .ThenInclude(d => d!.StaffProfile)
            .FirstOrDefaultAsync(s => s.AppointmentServiceId == request.AppointmentId, ct);

        if (apptService == null)
        {
            apptService = await _dbContext.AppointmentServices
                .Include(s => s.Appointment)
                .Include(s => s.DepartmentService)
                    .ThenInclude(ds => ds.Department)
                .Include(s => s.AssignedDentist)
                    .ThenInclude(d => d!.StaffProfile)
                .Where(s => s.AppointmentId == request.AppointmentId)
                .OrderByDescending(s => s.AppointmentServiceId)
                .FirstOrDefaultAsync(ct);
        }

        if (apptService == null)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Không tìm thấy ca khám chỉ định.");

        if (apptService.Status == "CheckedIn" || apptService.Status == "InProgress")
        {
            var existingQ = FormatQueueNumber(apptService.DepartmentService?.Department?.Name, apptService.DepartmentId, apptService.QueueNumber ?? 1);
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail($"Ca khám đã được Check-in trước đó (Trạng thái: {apptService.Status}, STT: {existingQ}).");
        }

        if (apptService.Status != "Confirmed" && apptService.Status != "PendingReceptionReview")
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail($"Ca khám ở trạng thái '{apptService.Status}', không thể Check-in.");

        var isAnotherInService = await _dbContext.AppointmentServices
            .AnyAsync(s => s.AppointmentId == apptService.AppointmentId &&
                           s.AppointmentServiceId != apptService.AppointmentServiceId &&
                           s.Status == "InProgress", ct);

        if (isAnotherInService)
        {
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail(
                "Bệnh nhân hiện đang trong quá trình thực hiện dịch vụ tại trạm khám trước (InProgress). Vui lòng đợi bác sĩ hoàn tất trước khi tiến hành Check-in và gọi vào Trạm B.");
        }

        if (!apptService.QueueNumber.HasValue || apptService.QueueNumber.Value <= 0)
        {
            apptService.QueueNumber = await AllocateDepartmentQueueNumberAsync(apptService.DepartmentId, apptService.ServiceDate, ct);
        }

        apptService.Status = "CheckedIn";
        apptService.UpdatedAt = DateTime.UtcNow;

        if (apptService.Appointment.Status == AppointmentStatus.Confirmed || apptService.Appointment.Status == AppointmentStatus.PendingReceptionReview)
        {
            apptService.Appointment.Status = AppointmentStatus.CheckedIn;
            apptService.Appointment.CheckedInAt ??= DateTime.UtcNow;
            apptService.Appointment.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);

        var formattedQueue = FormatQueueNumber(apptService.DepartmentService?.Department?.Name, apptService.DepartmentId, apptService.QueueNumber.Value);

        var dto = new CareEpisodeAppointmentSummaryDto
        {
            AppointmentId = apptService.AppointmentServiceId,
            AppointmentCode = FormatAppointmentCode(apptService.AppointmentId),
            DepartmentId = apptService.DepartmentId,
            DepartmentName = apptService.DepartmentService?.Department?.Name,
            RequestedServiceId = apptService.DepartmentServiceId,
            RequestedServiceName = apptService.DepartmentService?.ServiceName,
            AssignedDentistUserId = apptService.AssignedDentistUserId,
            AssignedDentistName = apptService.AssignedDentist?.StaffProfile?.FullName,
            RoomId = apptService.RoomId,
            DentalChairId = apptService.DentalChairId,
            ScheduledStart = apptService.ScheduledStart,
            ScheduledEnd = apptService.ScheduledEnd,
            Status = apptService.Status,
            QueueNumber = formattedQueue,
            IsRoot = false,
            ReasonForVisit = apptService.Notes
        };

        return ApiResponse<CareEpisodeAppointmentSummaryDto>.Ok(dto, $"Check-in Trạm B thành công. Giữ nguyên số thứ tự: {formattedQueue}.");
    }

    #endregion

    #region 4. Single Consolidated Invoice per Episode

    public async Task<ApiResponse<ConsolidatedEpisodeBillingSummaryDto>> GetConsolidatedInvoiceSummaryAsync(long careEpisodeId, CancellationToken ct = default)
    {
        var appointment = await LoadAppointmentEpisodeGraphAsync(careEpisodeId, ct);
        if (appointment == null)
            return ApiResponse<ConsolidatedEpisodeBillingSummaryDto>.Fail("Không tìm thấy đợt khám chỉ định.");

        var apptCode = FormatAppointmentCode(appointment.AppointmentId);
        var lineItems = new List<ConsolidatedBillingLineItemDto>();

        var performedServices = appointment.TreatmentSessions
            .SelectMany(ts => ts.PerformedServices.Select(ps => new { Session = ts, Performed = ps }))
            .ToList();

        foreach (var entry in performedServices)
        {
            lineItems.Add(new ConsolidatedBillingLineItemDto
            {
                ServiceTaskId = entry.Performed.AppointmentServiceId,
                AppointmentId = appointment.AppointmentId,
                AppointmentCode = apptCode,
                Description = entry.Performed.DepartmentService?.ServiceName ?? $"Dịch vụ #{entry.Performed.DepartmentServiceId}",
                PerformingDentistUserId = entry.Session.DentistUserId,
                PerformingDentistName = entry.Session.Dentist?.StaffProfile?.FullName,
                Quantity = entry.Performed.Quantity,
                UnitPrice = entry.Performed.UnitPrice,
                SourceType = "PerformedService",
                SourceId = entry.Performed.PerformedServiceId,
                IsIncludedInCombo = appointment.ComboId.HasValue
            });
        }

        var recordedApptServiceIds = performedServices.Select(p => p.Performed.AppointmentServiceId).ToHashSet();
        foreach (var svc in appointment.AppointmentServices.Where(s => s.Status == "Completed" && !recordedApptServiceIds.Contains(s.AppointmentServiceId)))
        {
            lineItems.Add(new ConsolidatedBillingLineItemDto
            {
                ServiceTaskId = svc.AppointmentServiceId,
                AppointmentId = appointment.AppointmentId,
                AppointmentCode = apptCode,
                Description = svc.DepartmentService?.ServiceName ?? $"Dịch vụ #{svc.DepartmentServiceId}",
                PerformingDentistUserId = svc.AssignedDentistUserId,
                PerformingDentistName = svc.AssignedDentist?.StaffProfile?.FullName,
                Quantity = 1,
                UnitPrice = svc.UnitPrice,
                SourceType = "ServiceTask",
                SourceId = svc.AppointmentServiceId,
                IsIncludedInCombo = svc.ComboId.HasValue
            });
        }

        var dispensedItems = await _dbContext.PrescriptionItems
            .Include(pi => pi.Prescription)
            .Include(pi => pi.Medicine)
            .AsNoTracking()
            .Where(pi => pi.Prescription.AppointmentId == appointment.AppointmentId && pi.QuantityDispensed > 0)
            .ToListAsync(ct);

        foreach (var medItem in dispensedItems)
        {
            lineItems.Add(new ConsolidatedBillingLineItemDto
            {
                AppointmentId = appointment.AppointmentId,
                AppointmentCode = apptCode,
                Description = $"Thuốc: {medItem.Medicine.MedicineName}",
                PerformingDentistUserId = medItem.DispensedByUserId ?? medItem.Prescription.DentistUserId,
                Quantity = medItem.QuantityDispensed,
                UnitPrice = medItem.UnitPriceAtDispense ?? medItem.Medicine.UnitPrice,
                SourceType = "Prescription",
                SourceId = medItem.PrescriptionItemId
            });
        }

        var subTotal = lineItems.Sum(i => i.TotalAmount);
        var existingInvoice = appointment.Invoice;
        var discountAmount = existingInvoice?.DiscountAmount ?? 0m;
        var totalAmount = existingInvoice?.FinalAmount ?? Math.Max(0m, subTotal - discountAmount);
        decimal paidAmount = 0m;
        if (existingInvoice != null)
        {
            var incoming = existingInvoice.Payments.Where(p => p.Status == "Successful" && p.PaymentType != "Refund").Sum(p => p.Amount);
            var refunded = existingInvoice.Payments.Where(p => p.Status == "Successful" && p.PaymentType == "Refund").Sum(p => p.Amount);
            paidAmount = Math.Max(0m, incoming - refunded);
        }

        var summary = new ConsolidatedEpisodeBillingSummaryDto
        {
            CareEpisodeId = appointment.AppointmentId,
            EpisodeCode = FormatEpisodeCode(appointment.AppointmentId, appointment.CreatedAt),
            PatientUserId = appointment.PatientUserId,
            PatientName = appointment.Patient?.FullName ?? string.Empty,
            EpisodeStatus = MapEpisodeStatus(appointment),
            HasExistingInvoice = existingInvoice != null,
            ExistingInvoiceId = existingInvoice?.InvoiceId,
            ExistingInvoiceCode = existingInvoice?.InvoiceCode,
            ExistingInvoiceStatus = existingInvoice?.Status,
            SubTotal = subTotal,
            DiscountAmount = discountAmount,
            TaxPercent = 0m,
            TaxAmount = 0m,
            TotalAmount = totalAmount,
            PaidAmount = paidAmount,
            BalanceRemaining = Math.Max(0m, totalAmount - paidAmount),
            LineItems = lineItems
        };

        return ApiResponse<ConsolidatedEpisodeBillingSummaryDto>.Ok(summary);
    }

    public async Task<ApiResponse<InvoiceDto>> GenerateConsolidatedInvoiceAsync(long receptionistUserId, GenerateConsolidatedInvoiceRequest request, CancellationToken ct = default)
    {
        if (request == null || request.CareEpisodeId <= 0)
            return ApiResponse<InvoiceDto>.Fail("Mã đợt khám không hợp lệ.");

        var lockKey = SqlAppLockHelper.CareEpisodeInvoiceKey(request.CareEpisodeId);
        using var appLock = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, new[] { lockKey }, 10000, ct);

        using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var appointment = await _dbContext.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Include(a => a.AppointmentServices)
                    .ThenInclude(s => s.DepartmentService)
                .Include(a => a.AppointmentServices)
                    .ThenInclude(s => s.TreatmentSessions)
                        .ThenInclude(ts => ts.PerformedServices)
                .Include(a => a.TreatmentSessions)
                    .ThenInclude(ts => ts.PerformedServices)
                        .ThenInclude(ps => ps.DepartmentService)
                .Include(a => a.Invoice)
                    .ThenInclude(i => i!.InvoiceItems)
                .Include(a => a.Invoice)
                    .ThenInclude(i => i!.Payments)
                .FirstOrDefaultAsync(a => a.AppointmentId == request.CareEpisodeId, ct);

            if (appointment == null)
            {
                await tx.RollbackAsync(ct);
                return ApiResponse<InvoiceDto>.Fail("Không tìm thấy đợt khám chỉ định.");
            }

            var existingInvoice = appointment.Invoice;
            if (existingInvoice != null && existingInvoice.Status != "Unpaid")
            {
                await tx.RollbackAsync(ct);
                return ApiResponse<InvoiceDto>.Fail(
                    $"Đợt khám này đã có hóa đơn #{existingInvoice.InvoiceCode} với trạng thái {existingInvoice.Status}. Không được chỉnh sửa hoặc tạo đè.");
            }

            foreach (var completedSvc in appointment.AppointmentServices.Where(s => s.Status == "Completed"))
            {
                var dentistId = completedSvc.AssignedDentistUserId ?? completedSvc.RequestedDentistUserId
                    ?? await _dbContext.DentistProfiles.Select(d => d.UserId).FirstOrDefaultAsync(ct);
                await EnsurePerformedServiceRecordedAsync(completedSvc, dentistId, null, completedSvc.Notes, ct);
            }
            await _dbContext.SaveChangesAsync(ct);

            var performedServices = await _dbContext.PerformedServices
                .Include(ps => ps.DepartmentService)
                .Include(ps => ps.TreatmentSession)
                .Where(ps => ps.AppointmentId == appointment.AppointmentId)
                .ToListAsync(ct);

            var dispensedItems = await _dbContext.PrescriptionItems
                .Include(pi => pi.Prescription)
                .Include(pi => pi.Medicine)
                .Where(pi => pi.Prescription.AppointmentId == appointment.AppointmentId && pi.QuantityDispensed > 0)
                .ToListAsync(ct);

            if (!performedServices.Any() && !dispensedItems.Any())
            {
                await tx.RollbackAsync(ct);
                return ApiResponse<InvoiceDto>.Fail("Đợt khám chưa có dịch vụ hoặc đơn thuốc đã cấp phát nào để xuất hóa đơn.");
            }

            var newItems = new List<InvoiceItem>();
            foreach (var ps in performedServices)
            {
                var lineTotal = ps.Quantity * ps.UnitPrice;
                newItems.Add(new InvoiceItem
                {
                    AppointmentId = appointment.AppointmentId,
                    PerformedServiceId = ps.PerformedServiceId,
                    Description = ps.DepartmentService?.ServiceName ?? $"Dịch vụ #{ps.DepartmentServiceId}",
                    Quantity = ps.Quantity,
                    UnitPrice = ps.UnitPrice,
                    TotalAmount = lineTotal
                });
            }

            foreach (var pi in dispensedItems)
            {
                var unitPrice = pi.UnitPriceAtDispense ?? pi.Medicine.UnitPrice;
                var lineTotal = pi.QuantityDispensed * unitPrice;
                newItems.Add(new InvoiceItem
                {
                    AppointmentId = appointment.AppointmentId,
                    PrescriptionItemId = pi.PrescriptionItemId,
                    Description = $"Thuốc: {pi.Medicine.MedicineName}",
                    Quantity = pi.QuantityDispensed,
                    UnitPrice = unitPrice,
                    TotalAmount = lineTotal
                });
            }

            var subTotal = newItems.Sum(i => i.TotalAmount);
            var discount = Math.Max(0m, request.DiscountAmount);
            if (discount > subTotal) discount = subTotal;
            var finalAmount = subTotal - discount;

            Invoice invoice;
            if (existingInvoice != null)
            {
                invoice = existingInvoice;
                _dbContext.InvoiceItems.RemoveRange(invoice.InvoiceItems);
                invoice.InvoiceItems.Clear();
                invoice.TotalAmount = subTotal;
                invoice.DiscountAmount = discount;
                invoice.FinalAmount = finalAmount;
                invoice.GeneratedByUserId = receptionistUserId;
                invoice.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var invoiceCode = await GenerateInvoiceCodeAsync(ct);
                invoice = new Invoice
                {
                    InvoiceCode = invoiceCode,
                    AppointmentId = appointment.AppointmentId,
                    GeneratedByUserId = receptionistUserId,
                    TotalAmount = subTotal,
                    DiscountAmount = discount,
                    FinalAmount = finalAmount,
                    Status = finalAmount == 0m ? "Paid" : "Unpaid",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Invoices.Add(invoice);
            }

            foreach (var item in newItems)
            {
                invoice.InvoiceItems.Add(item);
            }

            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var staffName = await _dbContext.StaffProfiles
                .AsNoTracking()
                .Where(s => s.UserId == receptionistUserId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync(ct);

            var invoiceDto = MapInvoiceToDto(invoice, appointment, staffName);
            return ApiResponse<InvoiceDto>.Ok(invoiceDto, "Xuất hóa đơn tổng hợp cho đợt khám thành công.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Lỗi khi tạo hóa đơn tổng hợp cho đợt khám {CareEpisodeId}", request.CareEpisodeId);
            return ApiResponse<InvoiceDto>.Fail($"Lỗi hệ thống khi lập hóa đơn: {ex.Message}");
        }
    }

    #endregion

    #region Helper Methods

    private async Task<Appointment?> LoadAppointmentEpisodeGraphAsync(long appointmentId, CancellationToken ct)
    {
        return await _dbContext.Appointments
            .Include(a => a.Patient)
                .ThenInclude(p => p.User)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.DepartmentService)
                    .ThenInclude(ds => ds.Department)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.AssignedDentist)
                    .ThenInclude(d => d!.StaffProfile)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.Room)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.DentalChair)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.TreatmentSessions)
                    .ThenInclude(ts => ts.PerformedServices)
            .Include(a => a.TreatmentSessions)
                .ThenInclude(ts => ts.Dentist)
                    .ThenInclude(d => d.StaffProfile)
            .Include(a => a.TreatmentSessions)
                .ThenInclude(ts => ts.PerformedServices)
                    .ThenInclude(ps => ps.DepartmentService)
            .Include(a => a.Invoice)
                .ThenInclude(i => i!.InvoiceItems)
            .Include(a => a.Invoice)
                .ThenInclude(i => i!.Payments)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);
    }

    private async Task<List<DepartmentReferral>> LoadReferralsForAppointmentAsync(long appointmentId, CancellationToken ct)
    {
        return await _dbContext.DepartmentReferrals
            .Include(r => r.FromDepartment)
            .Include(r => r.ToDepartment)
            .Include(r => r.RequestedService)
            .Include(r => r.AssignedDentist)
                .ThenInclude(d => d!.StaffProfile)
            .Include(r => r.SourceAppointmentService)
            .Include(r => r.DestinationAppointmentService)
            .Include(r => r.TreatmentSession)
            .AsNoTracking()
            .Where(r =>
                (r.SourceAppointmentService != null && r.SourceAppointmentService.AppointmentId == appointmentId) ||
                (r.DestinationAppointmentService != null && r.DestinationAppointmentService.AppointmentId == appointmentId) ||
                (r.TreatmentSession != null && r.TreatmentSession.AppointmentId == appointmentId))
            .OrderBy(r => r.ReferralId)
            .ToListAsync(ct);
    }

    private async Task EnsurePerformedServiceRecordedAsync(
        AppointmentServiceEntity apptService,
        long dentistUserId,
        string? clinicalFindings,
        string? notes,
        CancellationToken ct)
    {
        var session = apptService.TreatmentSessions.OrderByDescending(ts => ts.SessionId).FirstOrDefault();
        if (session == null)
        {
            session = new TreatmentSession
            {
                AppointmentId = apptService.AppointmentId,
                AppointmentServiceId = apptService.AppointmentServiceId,
                PatientUserId = apptService.Appointment.PatientUserId,
                DentistUserId = dentistUserId,
                SessionDate = apptService.ServiceDate,
                ClinicalObservations = clinicalFindings?.Trim(),
                Notes = notes?.Trim() ?? apptService.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.TreatmentSessions.Add(session);
            await _dbContext.SaveChangesAsync(ct);
        }
        else if (!string.IsNullOrWhiteSpace(clinicalFindings) || !string.IsNullOrWhiteSpace(notes))
        {
            if (!string.IsNullOrWhiteSpace(clinicalFindings))
                session.ClinicalObservations = clinicalFindings.Trim();
            if (!string.IsNullOrWhiteSpace(notes))
                session.Notes = notes.Trim();
            session.UpdatedAt = DateTime.UtcNow;
        }

        var hasPerformed = await _dbContext.PerformedServices
            .AnyAsync(ps => ps.AppointmentServiceId == apptService.AppointmentServiceId, ct);

        if (!hasPerformed)
        {
            var performed = new PerformedService
            {
                TreatmentSessionId = session.SessionId,
                AppointmentId = apptService.AppointmentId,
                AppointmentServiceId = apptService.AppointmentServiceId,
                DepartmentServiceId = apptService.DepartmentServiceId,
                Quantity = 1,
                UnitPrice = apptService.UnitPrice,
                Notes = notes?.Trim() ?? apptService.Notes,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.PerformedServices.Add(performed);
        }
    }

    private async Task<int> AllocateDepartmentQueueNumberAsync(int departmentId, DateOnly serviceDate, CancellationToken ct)
    {
        var maxQueue = await _dbContext.AppointmentServices
            .Where(s => s.DepartmentId == departmentId && s.ServiceDate == serviceDate && s.QueueNumber.HasValue)
            .Select(s => s.QueueNumber!.Value)
            .DefaultIfEmpty(0)
            .MaxAsync(ct);

        return maxQueue + 1;
    }

    private async Task<string> GenerateInvoiceCodeAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow;
        var prefix = $"INV-{today:yyyyMMdd}-";
        var count = await _dbContext.Invoices
            .CountAsync(i => i.CreatedAt >= today.Date && i.CreatedAt < today.Date.AddDays(1), ct);
        return $"{prefix}{(count + 1):D4}";
    }

    private static string FormatAppointmentCode(long appointmentId) => $"APT-{appointmentId:D6}";

    private static string FormatEpisodeCode(long appointmentId, DateTime createdAt) => $"EP-{createdAt:yyyyMMdd}-{appointmentId:D4}";

    private static string FormatQueueNumber(string? deptName, int deptId, int queueNumber)
    {
        var deptCode = GetDepartmentCode(deptName, deptId);
        return $"Q-{deptCode}-{queueNumber:D3}";
    }

    private static string GetDepartmentCode(string? deptName, int? deptId)
    {
        if (string.IsNullOrWhiteSpace(deptName))
            return $"D{deptId ?? 1}";

        var name = deptName.Trim().ToUpperInvariant();
        if (name.Contains("TỔNG QUÁT") || name.Contains("GENERAL")) return "GEN";
        if (name.Contains("CHỈNH NHA") || name.Contains("ORTHO")) return "ORT";
        if (name.Contains("PHỤC HÌNH") || name.Contains("PROS") || name.Contains("IMPLANT")) return "PRO";
        if (name.Contains("TRẺ EM") || name.Contains("PEDI")) return "PED";
        if (name.Contains("NHA CHU") || name.Contains("PERIO")) return "PER";
        if (name.Contains("TIỂU PHẪU") || name.Contains("SURG")) return "SUR";

        var words = deptName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
            return $"{words[0][0]}{words[1][0]}".ToUpperInvariant();

        return deptName.Length >= 3 ? deptName.Substring(0, 3).ToUpperInvariant() : $"D{deptId ?? 1}";
    }

    private static string NormalizeUrgency(string? urgency)
    {
        if (string.Equals(urgency?.Trim(), "Emergency", StringComparison.OrdinalIgnoreCase))
            return "Emergency";
        if (string.Equals(urgency?.Trim(), "Urgent", StringComparison.OrdinalIgnoreCase))
            return "Urgent";
        return "Normal";
    }

    private static string MapEpisodeStatus(Appointment appointment)
    {
        if (appointment.Status == AppointmentStatus.Cancelled || appointment.Status == AppointmentStatus.Rejected)
            return "Cancelled";
        if (appointment.Invoice != null && appointment.Invoice.Status == "Paid")
            return "Closed";
        if (appointment.Status == AppointmentStatus.Completed)
            return "ReadyForBilling";
        return "Open";
    }

    private static CareEpisodeDto MapAppointmentToCareEpisodeDto(Appointment appointment, List<DepartmentReferral> referrals)
    {
        var orderedServices = appointment.AppointmentServices
            .OrderBy(s => s.ScheduledStart)
            .ThenBy(s => s.AppointmentServiceId)
            .ToList();

        var firstServiceId = orderedServices.FirstOrDefault()?.AppointmentServiceId;

        var appointmentsList = orderedServices.Select(s => new CareEpisodeAppointmentSummaryDto
        {
            AppointmentId = s.AppointmentServiceId,
            AppointmentCode = FormatAppointmentCode(appointment.AppointmentId),
            DepartmentId = s.DepartmentId,
            DepartmentName = s.DepartmentService?.Department?.Name,
            RequestedServiceId = s.DepartmentServiceId,
            RequestedServiceName = s.DepartmentService?.ServiceName,
            AssignedDentistUserId = s.AssignedDentistUserId,
            AssignedDentistName = s.AssignedDentist?.StaffProfile?.FullName,
            RoomId = s.RoomId,
            RoomName = s.Room?.RoomCode,
            DentalChairId = s.DentalChairId,
            ChairName = s.DentalChair?.ChairCode,
            ScheduledStart = s.ScheduledStart,
            ScheduledEnd = s.ScheduledEnd,
            Status = s.Status,
            QueueNumber = s.QueueNumber.HasValue
                ? FormatQueueNumber(s.DepartmentService?.Department?.Name, s.DepartmentId, s.QueueNumber.Value)
                : null,
            IsRoot = s.AppointmentServiceId == firstServiceId,
            ReasonForVisit = s.Notes ?? appointment.ReasonForVisit
        }).ToList();

        var serviceTasks = orderedServices
            .Select(s => MapAppointmentServiceToTaskDto(s))
            .ToList();

        var referralDtos = referrals.Select(MapReferralToDto).ToList();

        var invoiceDto = appointment.Invoice != null
            ? MapInvoiceToDto(appointment.Invoice, appointment, null)
            : null;

        var totalEstimatedCost = serviceTasks.Sum(t => t.TotalAmount);
        var totalActualCost = serviceTasks.Where(t => t.Status == "Completed").Sum(t => t.TotalAmount);
        var totalPaid = invoiceDto?.PaidAmount ?? 0m;

        return new CareEpisodeDto
        {
            CareEpisodeId = appointment.AppointmentId,
            EpisodeCode = FormatEpisodeCode(appointment.AppointmentId, appointment.CreatedAt),
            PatientUserId = appointment.PatientUserId,
            PatientName = appointment.Patient?.FullName ?? string.Empty,
            PatientPhone = appointment.Patient?.User?.PhoneNumber,
            RootAppointmentId = appointment.AppointmentId,
            RootAppointmentCode = FormatAppointmentCode(appointment.AppointmentId),
            Status = MapEpisodeStatus(appointment),
            OpenedAtUtc = appointment.CreatedAt,
            ClosedAtUtc = appointment.CompletedAt,
            Notes = appointment.ReasonForVisit,
            CreatedAtUtc = appointment.CreatedAt,
            Appointments = appointmentsList,
            ServiceTasks = serviceTasks,
            Referrals = referralDtos,
            ConsolidatedInvoice = invoiceDto,
            TotalEstimatedCost = totalEstimatedCost,
            TotalActualCost = totalActualCost,
            TotalPaidAmount = totalPaid,
            RemainingBalance = Math.Max(0m, totalActualCost - totalPaid)
        };
    }

    private static EpisodeServiceTaskDto MapAppointmentServiceToTaskDto(AppointmentServiceEntity s, string? performingDentistName = null)
    {
        var taskStatus = s.Status switch
        {
            "Completed" => "Completed",
            "InProgress" => "InProgress",
            "Cancelled" or "NoShow" => "Cancelled",
            _ => "Planned"
        };

        var session = s.TreatmentSessions?.OrderByDescending(ts => ts.SessionId).FirstOrDefault();

        return new EpisodeServiceTaskDto
        {
            ServiceTaskId = s.AppointmentServiceId,
            TaskCode = $"TSK-{s.AppointmentServiceId:D6}",
            CareEpisodeId = s.AppointmentId,
            AppointmentId = s.AppointmentId,
            AppointmentCode = FormatAppointmentCode(s.AppointmentId),
            DepartmentServiceId = s.DepartmentServiceId,
            ServiceName = s.DepartmentService?.ServiceName ?? string.Empty,
            DepartmentId = s.DepartmentId,
            DepartmentName = s.DepartmentService?.Department?.Name ?? string.Empty,
            PerformingDentistUserId = s.AssignedDentistUserId ?? s.RequestedDentistUserId ?? 0,
            PerformingDentistName = performingDentistName ?? s.AssignedDentist?.StaffProfile?.FullName ?? string.Empty,
            Status = taskStatus,
            Quantity = 1,
            UnitPriceSnapshot = s.UnitPrice,
            EstimatedDurationMinutes = s.DepartmentService?.DurationMinutes ?? (int)Math.Max(15, (s.ScheduledEnd - s.ScheduledStart).TotalMinutes),
            PlannedAtUtc = s.CreatedAt,
            StartedAtUtc = session?.CreatedAt,
            CompletedAtUtc = taskStatus == "Completed" ? (s.UpdatedAt ?? session?.UpdatedAt ?? s.CreatedAt) : null,
            CancelledAtUtc = taskStatus == "Cancelled" ? s.UpdatedAt : null,
            CancellationReason = taskStatus == "Cancelled" ? s.Notes : null,
            ClinicalRecordId = session?.SessionId,
            Notes = s.Notes
        };
    }

    private static CareEpisodeReferralSummaryDto MapReferralToDto(DepartmentReferral r)
    {
        var sourceApptId = r.SourceAppointmentService?.AppointmentId ?? r.TreatmentSession?.AppointmentId;
        var destApptId = r.DestinationAppointmentService?.AppointmentId;

        return new CareEpisodeReferralSummaryDto
        {
            ReferralId = r.ReferralId,
            SourceAppointmentId = r.SourceAppointmentServiceId ?? sourceApptId,
            SourceAppointmentCode = sourceApptId.HasValue ? FormatAppointmentCode(sourceApptId.Value) : null,
            DestinationAppointmentId = r.DestinationAppointmentServiceId ?? destApptId,
            DestinationAppointmentCode = destApptId.HasValue ? FormatAppointmentCode(destApptId.Value) : null,
            FromDepartmentId = r.FromDepartmentId,
            FromDepartmentName = r.FromDepartment?.Name ?? string.Empty,
            ToDepartmentId = r.ToDepartmentId,
            ToDepartmentName = r.ToDepartment?.Name ?? string.Empty,
            RequestedServiceId = r.RequestedServiceId,
            RequestedServiceName = r.RequestedService?.ServiceName,
            AssignedDentistUserId = r.AssignedDentistUserId,
            AssignedDentistName = r.AssignedDentist?.StaffProfile?.FullName,
            TargetAppointmentQueueNumber = r.DestinationAppointmentService?.QueueNumber,
            StationBCheckInStatus = r.DestinationAppointmentService?.Status,
            Reason = r.Reason,
            Urgency = r.Urgency,
            Status = r.Status,
            ClinicalInstructions = r.ClinicalInstructions,
            CreatedAt = r.CreatedAt,
            ReviewedAt = r.ReviewedAt
        };
    }

    private static InvoiceDto MapInvoiceToDto(Invoice invoice, Appointment appointment, string? generatedByName)
    {
        var incoming = invoice.Payments.Where(p => p.Status == "Successful" && p.PaymentType != "Refund").Sum(p => p.Amount);
        var refunded = invoice.Payments.Where(p => p.Status == "Successful" && p.PaymentType == "Refund").Sum(p => p.Amount);
        var paidAmount = Math.Max(0m, incoming - refunded);

        return new InvoiceDto
        {
            InvoiceId = invoice.InvoiceId,
            InvoiceCode = invoice.InvoiceCode,
            CareEpisodeId = appointment.AppointmentId,
            CareEpisodeCode = FormatEpisodeCode(appointment.AppointmentId, appointment.CreatedAt),
            AppointmentId = invoice.AppointmentId,
            AppointmentCode = FormatAppointmentCode(appointment.AppointmentId),
            PatientUserId = appointment.PatientUserId,
            PatientName = appointment.Patient?.FullName ?? string.Empty,
            PatientPhoneNumber = appointment.Patient?.User?.PhoneNumber,
            PatientEmail = appointment.Patient?.User?.Email,
            GeneratedByUserId = invoice.GeneratedByUserId,
            GeneratedByName = generatedByName,
            SubTotal = invoice.TotalAmount,
            DiscountAmount = invoice.DiscountAmount,
            TaxPercent = 0m,
            TaxAmount = 0m,
            TotalAmount = invoice.FinalAmount,
            PaidAmount = paidAmount,
            RemainingAmount = Math.Max(0m, invoice.FinalAmount - paidAmount),
            Status = invoice.Status,
            CreatedAt = invoice.CreatedAt,
            Items = invoice.InvoiceItems.Select(ii => new InvoiceItemDto
            {
                InvoiceItemId = ii.InvoiceItemId,
                InvoiceId = ii.InvoiceId,
                AppointmentId = ii.AppointmentId,
                PerformedServiceId = ii.PerformedServiceId,
                SourceType = ii.PerformedServiceId.HasValue ? "PerformedService" : "Prescription",
                SourceId = ii.PerformedServiceId ?? ii.PrescriptionItemId,
                Description = ii.Description,
                Quantity = ii.Quantity,
                UnitPrice = ii.UnitPrice,
                Amount = ii.TotalAmount
            }).ToList()
        };
    }

    #endregion
}
