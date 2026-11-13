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
        var appointment = await _dbContext.Appointments
            .Include(a => a.PatientUser)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
            return ApiResponse<CareEpisodeDto>.Fail("Không tìm thấy ca khám chỉ định.");

        if (appointment.CareEpisodeId.HasValue)
        {
            await EnsurePrimaryTaskForAppointmentAsync(appointment, appointment.CareEpisodeId.Value, ct);
            return await GetEpisodeByIdAsync(appointment.CareEpisodeId.Value, ct);
        }

        // Look for an existing open episode for this patient today
        var today = DateTime.UtcNow.Date;
        var existingEpisode = await _dbContext.CareEpisodes
            .Where(e => e.PatientUserId == appointment.PatientUserId &&
                        (e.Status == "Open" || e.Status == "PendingAdditionalService" || e.Status == "ReadyForBilling") &&
                        e.OpenedAtUtc >= today && e.OpenedAtUtc < today.AddDays(1))
            .OrderByDescending(e => e.CareEpisodeId)
            .FirstOrDefaultAsync(ct);

        if (existingEpisode != null)
        {
            appointment.CareEpisodeId = existingEpisode.CareEpisodeId;
            appointment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            await EnsurePrimaryTaskForAppointmentAsync(appointment, existingEpisode.CareEpisodeId, ct);
            return await GetEpisodeByIdAsync(existingEpisode.CareEpisodeId, ct);
        }

        // Create a new care episode with appointment as RootAppointment
        var episodeCode = await GenerateEpisodeCodeAsync(ct);
        var newEpisode = new CareEpisode
        {
            EpisodeCode = episodeCode,
            PatientUserId = appointment.PatientUserId,
            RootAppointmentId = appointment.AppointmentId,
            Status = "Open",
            OpenedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            Notes = $"Đợt khám khởi tạo từ ca khám #{appointment.AppointmentCode}"
        };

        _dbContext.CareEpisodes.Add(newEpisode);
        await _dbContext.SaveChangesAsync(ct);

        appointment.CareEpisodeId = newEpisode.CareEpisodeId;
        appointment.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await EnsurePrimaryTaskForAppointmentAsync(appointment, newEpisode.CareEpisodeId, ct);

        _logger.LogInformation("Khởi tạo đợt khám mới {EpisodeCode} cho bệnh nhân {PatientUserId} từ ca khám #{AppointmentId}",
            newEpisode.EpisodeCode, appointment.PatientUserId, appointment.AppointmentId);

        return await GetEpisodeByIdAsync(newEpisode.CareEpisodeId, ct);
    }

    public async Task<ApiResponse<CareEpisodeDto>> GetEpisodeByIdAsync(long careEpisodeId, CancellationToken ct = default)
    {
        var episode = await _dbContext.CareEpisodes
            .Include(e => e.PatientUser)
                .ThenInclude(u => u.PatientProfile)
            .Include(e => e.Appointments)
                .ThenInclude(a => a.Department)
            .Include(e => e.Appointments)
                .ThenInclude(a => a.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(e => e.Appointments)
                .ThenInclude(a => a.Room)
            .Include(e => e.Appointments)
                .ThenInclude(a => a.DentalChair)
            .Include(e => e.Appointments)
                .ThenInclude(a => a.RequestedService)
            .Include(e => e.ServiceTasks)
                .ThenInclude(t => t.DepartmentService)
                    .ThenInclude(s => s.Department)
            .Include(e => e.ServiceTasks)
                .ThenInclude(t => t.PerformingDentistUser)
                    .ThenInclude(u => u.StaffProfile)
            .Include(e => e.ServiceTasks)
                .ThenInclude(t => t.Appointment)
            .Include(e => e.ServiceTasks)
                .ThenInclude(t => t.StepProgresses)
                    .ThenInclude(p => p.Step)
            .Include(e => e.ServiceTasks)
                .ThenInclude(t => t.StepProgresses)
                    .ThenInclude(p => p.CompletedByDentistUser)
                        .ThenInclude(u => u!.StaffProfile)
            .Include(e => e.DepartmentReferrals)
                .ThenInclude(r => r.FromDepartment)
            .Include(e => e.DepartmentReferrals)
                .ThenInclude(r => r.ToDepartment)
            .Include(e => e.DepartmentReferrals)
                .ThenInclude(r => r.RequestedService)
            .Include(e => e.DepartmentReferrals)
                .ThenInclude(r => r.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(e => e.DepartmentReferrals)
                .ThenInclude(r => r.SourceAppointment)
            .Include(e => e.DepartmentReferrals)
                .ThenInclude(r => r.DestinationAppointment)
            .Include(e => e.Invoices)
                .ThenInclude(i => i.InvoiceItems)
            .Include(e => e.Invoices)
                .ThenInclude(i => i.Payments)
            .FirstOrDefaultAsync(e => e.CareEpisodeId == careEpisodeId, ct);

        if (episode == null)
            return ApiResponse<CareEpisodeDto>.Fail("Không tìm thấy đợt khám chỉ định.");

        var dto = MapCareEpisodeToDto(episode);
        return ApiResponse<CareEpisodeDto>.Ok(dto);
    }

    public async Task<ApiResponse<CareEpisodeDto>> GetEpisodeByAppointmentIdAsync(long appointmentId, CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
            return ApiResponse<CareEpisodeDto>.Fail("Không tìm thấy ca khám chỉ định.");

        if (appointment.CareEpisodeId.HasValue)
        {
            await EnsurePrimaryTaskForAppointmentAsync(appointment, appointment.CareEpisodeId.Value, ct);
            return await GetEpisodeByIdAsync(appointment.CareEpisodeId.Value, ct);
        }

        return await GetOrCreateActiveEpisodeForAppointmentAsync(appointmentId, ct);
    }

    public async Task<ApiResponse<CareEpisodeDto>> GetPatientActiveEpisodeAsync(long patientUserId, CancellationToken ct = default)
    {
        var episode = await _dbContext.CareEpisodes
            .Where(e => e.PatientUserId == patientUserId &&
                        (e.Status == "Open" || e.Status == "PendingAdditionalService" || e.Status == "ReadyForBilling"))
            .OrderByDescending(e => e.CareEpisodeId)
            .FirstOrDefaultAsync(ct);

        if (episode == null)
            return ApiResponse<CareEpisodeDto>.Fail("Hiện tại bệnh nhân không có đợt khám nào đang diễn ra.");

        return await GetEpisodeByIdAsync(episode.CareEpisodeId, ct);
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

        // Check departments where dentist is actively assigned
        var activeDepartmentIds = await _dbContext.DentistDepartments
            .AsNoTracking()
            .Where(dd => dd.DentistUserId == dentistUserId && dd.Status == "Active")
            .Select(dd => dd.DepartmentId)
            .ToListAsync(ct);

        if (!activeDepartmentIds.Any())
            return ApiResponse<List<EpisodeServiceTaskDto>>.Ok(new List<EpisodeServiceTaskDto>(), "Bác sĩ chưa được gán chuyên khoa hoạt động.");

        var services = await _dbContext.DepartmentServices
            .Include(s => s.Department)
            .AsNoTracking()
            .Where(s => activeDepartmentIds.Contains(s.DepartmentId) && s.Status == "Active")
            .OrderBy(s => s.Department.Name)
            .ThenBy(s => s.ServiceName)
            .ToListAsync(ct);

        var result = services.Select(s => new EpisodeServiceTaskDto
        {
            ServiceTaskId = 0,
            TaskCode = string.Empty,
            CareEpisodeId = appointment.CareEpisodeId ?? 0,
            AppointmentId = appointmentId,
            AppointmentCode = appointment.AppointmentCode,
            DepartmentServiceId = s.DepartmentServiceId,
            ServiceName = s.ServiceName,
            DepartmentId = s.DepartmentId,
            DepartmentName = s.Department.Name,
            PerformingDentistUserId = dentistUserId,
            PerformingDentistName = string.Empty,
            Status = "Planned",
            Quantity = 1,
            UnitPriceSnapshot = s.Price,
            EstimatedDurationMinutes = s.DurationMinutes ?? 30,
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
            .Include(a => a.PatientUser)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

        if (appointment == null)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Không tìm thấy ca khám chỉ định.");

        var service = await _dbContext.DepartmentServices
            .Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == request.DepartmentServiceId && s.Status == "Active", ct);

        if (service == null)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Dịch vụ được chọn không tồn tại hoặc đã ngừng hoạt động.");

        // Qualification check: Dentist must be active in the service's department
        var isQualified = await _dbContext.DentistDepartments
            .AnyAsync(dd => dd.DentistUserId == dentistUserId && dd.DepartmentId == service.DepartmentId && dd.Status == "Active", ct);

        if (!isQualified)
        {
            return ApiResponse<EpisodeServiceTaskDto>.Fail(
                $"Bác sĩ không thuộc chuyên khoa '{service.Department.Name}' cung cấp dịch vụ này. Vui lòng chuyển tiếp bệnh nhân sang bác sĩ chuyên khoa phù hợp (Internal Referral).");
        }

        // Ensure episode exists
        if (!appointment.CareEpisodeId.HasValue)
        {
            var epRes = await GetOrCreateActiveEpisodeForAppointmentAsync(appointment.AppointmentId, ct);
            if (!epRes.Success || epRes.Data == null)
                return ApiResponse<EpisodeServiceTaskDto>.Fail(epRes.Message ?? "Không thể tạo đợt khám cho ca điều trị.");
            appointment.CareEpisodeId = epRes.Data.CareEpisodeId;
        }

        var activeWorkflow = await _dbContext.ServiceWorkflows
            .Include(w => w.Steps)
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.DepartmentServiceId == service.DepartmentServiceId && w.IsActive && w.ApprovalStatus == "Approved", ct);

        var taskCode = await GenerateTaskCodeAsync(ct);
        var task = new EpisodeServiceTask
        {
            TaskCode = taskCode,
            CareEpisodeId = appointment.CareEpisodeId.Value,
            AppointmentId = appointment.AppointmentId,
            DepartmentServiceId = service.DepartmentServiceId,
            PerformingDentistUserId = dentistUserId,
            Status = "Planned",
            Quantity = request.Quantity > 0 ? request.Quantity : 1,
            UnitPriceSnapshot = service.Price,
            EstimatedDurationMinutes = service.DurationMinutes ?? 30,
            AppliedWorkflowId = activeWorkflow?.WorkflowId,
            AppliedWorkflowVersion = activeWorkflow?.Version,
            PlannedAtUtc = DateTime.UtcNow,
            Notes = request.Notes?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        if (activeWorkflow != null && activeWorkflow.Steps != null)
        {
            foreach (var step in activeWorkflow.Steps.OrderBy(s => s.StepOrder))
            {
                task.StepProgresses.Add(new EpisodeServiceTaskStepProgress
                {
                    StepId = step.StepId,
                    Status = "Pending",
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        _dbContext.EpisodeServiceTasks.Add(task);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Đã thêm chỉ định dịch vụ {TaskCode} ({ServiceName}) cho ca khám #{AppointmentCode} của bác sĩ {DentistUserId}",
            task.TaskCode, service.ServiceName, appointment.AppointmentCode, dentistUserId);

        var performingUser = await _dbContext.UserAccounts
            .Include(u => u.StaffProfile)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == dentistUserId, ct);

        return ApiResponse<EpisodeServiceTaskDto>.Ok(MapToTaskDto(task, GetUserDisplayName(performingUser)), "Chỉ định dịch vụ bổ sung thành công.");
    }

    public async Task<ApiResponse<EpisodeServiceTaskDto>> StartServiceTaskAsync(long dentistUserId, long serviceTaskId, CancellationToken ct = default)
    {
        var task = await _dbContext.EpisodeServiceTasks
            .Include(t => t.DepartmentService)
                .ThenInclude(s => s.Department)
            .Include(t => t.Appointment)
            .Include(t => t.PerformingDentistUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(t => t.StepProgresses)
                .ThenInclude(p => p.Step)
            .Include(t => t.CareEpisode)
            .FirstOrDefaultAsync(t => t.ServiceTaskId == serviceTaskId, ct);

        if (task == null)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Không tìm thấy tác vụ điều trị chỉ định.");

        var isAuthorized = task.PerformingDentistUserId == dentistUserId ||
            await _dbContext.DentistDepartments.AnyAsync(dd => dd.DentistUserId == dentistUserId && dd.DepartmentId == task.DepartmentService.DepartmentId && dd.Status == "Active", ct);

        if (!isAuthorized)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Bạn không có quyền thực hiện tác vụ của chuyên khoa/bác sĩ khác.");

        if (task.Status == "Completed")
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Tác vụ này đã hoàn thành, không thể bắt đầu lại.");

        if (task.Status == "Cancelled")
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Tác vụ này đã bị hủy.");

        // Lazy initialize steps if workflow exists but step progresses weren't seeded
        if (task.AppliedWorkflowId.HasValue && !task.StepProgresses.Any())
        {
            var wfSteps = await _dbContext.ServiceWorkflowSteps
                .Where(s => s.WorkflowId == task.AppliedWorkflowId.Value)
                .OrderBy(s => s.StepOrder)
                .ToListAsync(ct);

            foreach (var step in wfSteps)
            {
                var prog = new EpisodeServiceTaskStepProgress
                {
                    ServiceTaskId = task.ServiceTaskId,
                    StepId = step.StepId,
                    Status = "Pending",
                    CreatedAtUtc = DateTime.UtcNow
                };
                _dbContext.EpisodeServiceTaskStepProgresses.Add(prog);
                task.StepProgresses.Add(prog);
            }
        }

        task.Status = "InProgress";
        task.StartedAtUtc ??= DateTime.UtcNow;
        task.UpdatedAtUtc = DateTime.UtcNow;

        var firstStep = task.StepProgresses.OrderBy(p => p.Step?.StepOrder ?? 0).FirstOrDefault();
        if (firstStep != null && firstStep.Status == "Pending")
        {
            firstStep.Status = "InProgress";
            firstStep.StartedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Bác sĩ {DentistUserId} đã bắt đầu tác vụ {TaskCode} ({ServiceName})", dentistUserId, task.TaskCode, task.DepartmentService.ServiceName);

        return ApiResponse<EpisodeServiceTaskDto>.Ok(MapToTaskDto(task), "Bắt đầu thực hiện dịch vụ thành công.");
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

        var task = await _dbContext.EpisodeServiceTasks
            .Include(t => t.DepartmentService)
                .ThenInclude(s => s.Department)
            .Include(t => t.Appointment)
            .Include(t => t.StepProgresses)
                .ThenInclude(p => p.Step)
            .FirstOrDefaultAsync(t => t.ServiceTaskId == serviceTaskId, ct);

        if (task == null)
            return ApiResponse<EpisodeTaskStepProgressDto>.Fail("Không tìm thấy tác vụ điều trị chỉ định.");

        var isAuthorized = task.PerformingDentistUserId == dentistUserId ||
            await _dbContext.DentistDepartments.AnyAsync(dd => dd.DentistUserId == dentistUserId && dd.DepartmentId == task.DepartmentService.DepartmentId && dd.Status == "Active", ct);

        if (!isAuthorized)
            return ApiResponse<EpisodeTaskStepProgressDto>.Fail("Bạn không có quyền cập nhật tiến độ tác vụ của bác sĩ khác.");

        if (task.Status == "Completed")
            return ApiResponse<EpisodeTaskStepProgressDto>.Fail("Tác vụ này đã hoàn thành.");

        if (task.Status == "Cancelled")
            return ApiResponse<EpisodeTaskStepProgressDto>.Fail("Tác vụ này đã bị hủy.");

        if (task.Status == "Planned")
        {
            task.Status = "InProgress";
            task.StartedAtUtc = DateTime.UtcNow;
            task.UpdatedAtUtc = DateTime.UtcNow;
        }

        var progress = task.StepProgresses.FirstOrDefault(p => p.StepId == stepId);
        var stepDef = progress?.Step ?? await _dbContext.ServiceWorkflowSteps.FirstOrDefaultAsync(s => s.StepId == stepId, ct);

        if (stepDef == null)
            return ApiResponse<EpisodeTaskStepProgressDto>.Fail("Không tìm thấy định nghĩa bước quy trình.");

        if (progress == null)
        {
            progress = new EpisodeServiceTaskStepProgress
            {
                ServiceTaskId = serviceTaskId,
                StepId = stepId,
                Status = "Pending",
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.EpisodeServiceTaskStepProgresses.Add(progress);
            task.StepProgresses.Add(progress);
        }

        var inputVal = request.ResultValue?.Trim();
        if (stepDef.IsRequired && stepDef.ExpectedDataType != "None" && string.IsNullOrWhiteSpace(inputVal))
        {
            return ApiResponse<EpisodeTaskStepProgressDto>.Fail($"Bước '{stepDef.StepName}' yêu cầu nhập dữ liệu kết quả ({stepDef.ExpectedDataType}).");
        }

        if (!string.IsNullOrWhiteSpace(inputVal))
        {
            switch (stepDef.ExpectedDataType?.ToUpperInvariant())
            {
                case "NUMBER":
                    if (!decimal.TryParse(inputVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _) &&
                        !decimal.TryParse(inputVal, out _))
                    {
                        return ApiResponse<EpisodeTaskStepProgressDto>.Fail($"Dữ liệu bước '{stepDef.StepName}' phải là định dạng số hợp lệ.");
                    }
                    break;
                case "BOOLEAN":
                    var lower = inputVal.ToLowerInvariant();
                    if (lower != "true" && lower != "false" && lower != "1" && lower != "0" && lower != "đạt" && lower != "yes" && lower != "no")
                    {
                        return ApiResponse<EpisodeTaskStepProgressDto>.Fail($"Dữ liệu bước '{stepDef.StepName}' phải là xác nhận Có/Không (Boolean).");
                    }
                    break;
            }
        }

        var reqText = (stepDef.CompletionRequirement ?? string.Empty).ToLowerInvariant();
        var stepNameLower = stepDef.StepName.ToLowerInvariant();
        if (reqText.Contains("x-quang") || reqText.Contains("xray") || reqText.Contains("chụp phim") ||
            stepNameLower.Contains("x-quang") || stepNameLower.Contains("xray") || stepNameLower.Contains("chụp phim"))
        {
            var hasXrayResult = await _dbContext.DiagnosticResults
                .Include(r => r.DiagnosticOrder)
                .AnyAsync(r => (r.DiagnosticOrder.AppointmentId == task.AppointmentId || r.DiagnosticOrder.PatientUserId == task.Appointment.PatientUserId) &&
                               (r.DiagnosticOrder.Status == "Completed" || r.IsReviewed || !string.IsNullOrWhiteSpace(r.FileUrl)), ct)
                || !string.IsNullOrWhiteSpace(request.EvidenceUrl);

            if (!hasXrayResult)
            {
                return ApiResponse<EpisodeTaskStepProgressDto>.Fail(
                    $"Bước '{stepDef.StepName}' yêu cầu có kết quả chụp phim/X-quang hoàn tất trước khi xác nhận hoàn thành.");
            }
        }

        progress.Status = "Completed";
        progress.ResultValue = inputVal;
        progress.Notes = request.Notes?.Trim();
        progress.EvidenceUrl = request.EvidenceUrl?.Trim();
        progress.CompletedByDentistUserId = dentistUserId;
        progress.CompletedAtUtc = DateTime.UtcNow;
        progress.UpdatedAtUtc = DateTime.UtcNow;

        task.UpdatedAtUtc = DateTime.UtcNow;

        var nextStep = task.StepProgresses
            .Where(p => (p.Step?.StepOrder ?? 0) > stepDef.StepOrder && p.Status == "Pending")
            .OrderBy(p => p.Step?.StepOrder ?? 0)
            .FirstOrDefault();

        if (nextStep != null)
        {
            nextStep.Status = "InProgress";
            nextStep.StartedAtUtc ??= DateTime.UtcNow;
            nextStep.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Bác sĩ {DentistUserId} đã hoàn thành bước #{StepOrder} ({StepName}) cho tác vụ {TaskCode}",
            dentistUserId, stepDef.StepOrder, stepDef.StepName, task.TaskCode);

        var performingUser = await _dbContext.UserAccounts
            .Include(u => u.StaffProfile)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == dentistUserId, ct);

        var dto = new EpisodeTaskStepProgressDto
        {
            ProgressId = progress.ProgressId,
            ServiceTaskId = progress.ServiceTaskId,
            StepId = progress.StepId,
            StepName = stepDef.StepName,
            Description = stepDef.Description,
            StepOrder = stepDef.StepOrder,
            IsRequired = stepDef.IsRequired,
            CompletionRequirement = stepDef.CompletionRequirement,
            ExpectedDataType = stepDef.ExpectedDataType,
            Status = progress.Status,
            ResultValue = progress.ResultValue,
            Notes = progress.Notes,
            EvidenceUrl = progress.EvidenceUrl,
            CompletedByDentistUserId = dentistUserId,
            CompletedByDentistName = GetUserDisplayName(performingUser),
            StartedAtUtc = progress.StartedAtUtc,
            CompletedAtUtc = progress.CompletedAtUtc
        };

        return ApiResponse<EpisodeTaskStepProgressDto>.Ok(dto, $"Đã xác nhận hoàn thành bước '{stepDef.StepName}'.");
    }

    public async Task<ApiResponse<EpisodeServiceTaskDto>> CompleteServiceTaskAsync(
        long dentistUserId,
        long serviceTaskId,
        CompleteServiceTaskRequest request,
        CancellationToken ct = default)
    {
        var task = await _dbContext.EpisodeServiceTasks
            .Include(t => t.DepartmentService)
                .ThenInclude(s => s.Department)
            .Include(t => t.Appointment)
            .Include(t => t.PerformingDentistUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(t => t.StepProgresses)
                .ThenInclude(p => p.Step)
            .Include(t => t.CareEpisode)
            .FirstOrDefaultAsync(t => t.ServiceTaskId == serviceTaskId, ct);

        if (task == null)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Không tìm thấy tác vụ điều trị chỉ định.");

        var isAuthorized = task.PerformingDentistUserId == dentistUserId ||
            await _dbContext.DentistDepartments.AnyAsync(dd => dd.DentistUserId == dentistUserId && dd.DepartmentId == task.DepartmentService.DepartmentId && dd.Status == "Active", ct);

        if (!isAuthorized)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Bạn không có quyền hoàn thành tác vụ của bác sĩ khác.");

        if (task.Status == "Completed")
            return ApiResponse<EpisodeServiceTaskDto>.Ok(MapToTaskDto(task), "Tác vụ đã được hoàn thành trước đó.");

        if (task.Status == "Cancelled")
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Tác vụ này đã bị hủy.");

        var requiredIncompleteSteps = task.StepProgresses
            .Where(p => (p.Step?.IsRequired ?? true) && p.Status != "Completed")
            .OrderBy(p => p.Step?.StepOrder ?? 0)
            .ToList();

        if (requiredIncompleteSteps.Any())
        {
            var firstIncomplete = requiredIncompleteSteps.First();
            return ApiResponse<EpisodeServiceTaskDto>.Fail(
                $"Chưa thể hoàn thành dịch vụ. Bước bắt buộc số {firstIncomplete.Step?.StepOrder}: '{firstIncomplete.Step?.StepName}' chưa được hoàn thành.");
        }

        task.Status = "Completed";
        task.CompletedAtUtc = DateTime.UtcNow;
        task.ClinicalRecordId = request?.ClinicalRecordId ?? task.ClinicalRecordId;
        if (!string.IsNullOrWhiteSpace(request?.Notes))
        {
            task.Notes = request.Notes.Trim();
        }
        task.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Bác sĩ {DentistUserId} đã hoàn thành toàn bộ dịch vụ cho tác vụ {TaskCode} ({ServiceName})",
            dentistUserId, task.TaskCode, task.DepartmentService.ServiceName);

        return ApiResponse<EpisodeServiceTaskDto>.Ok(MapToTaskDto(task), $"Đã hoàn thành dịch vụ '{task.DepartmentService.ServiceName}'.");
    }

    public async Task<ApiResponse<bool>> ValidateAppointmentTasksCompletedAsync(long appointmentId, CancellationToken ct = default)
    {
        var tasks = await _dbContext.EpisodeServiceTasks
            .Include(t => t.DepartmentService)
            .Include(t => t.StepProgresses)
                .ThenInclude(p => p.Step)
            .Where(t => t.AppointmentId == appointmentId && t.Status != "Cancelled")
            .ToListAsync(ct);

        if (!tasks.Any())
            return ApiResponse<bool>.Ok(true, "Ca khám không có tác vụ nào cần xử lý.");

        var incompleteTasks = tasks.Where(t => t.Status != "Completed").ToList();
        if (incompleteTasks.Any())
        {
            var names = string.Join(", ", incompleteTasks.Select(t => t.DepartmentService?.ServiceName ?? t.TaskCode));
            return ApiResponse<bool>.Fail($"Ca khám còn các dịch vụ chưa hoàn thành: {names}. Vui lòng hoàn thành các dịch vụ trước khi kết thúc ca khám.");
        }

        return ApiResponse<bool>.Ok(true, "Tất cả các dịch vụ thuộc ca khám đã hoàn thành.");
    }

    public async Task<ApiResponse<EpisodeServiceTaskDto>> UpdateServiceTaskStatusAsync(long dentistUserId, long serviceTaskId, UpdateServiceTaskStatusRequest request, CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Dữ liệu cập nhật trạng thái không hợp lệ.");

        var task = await _dbContext.EpisodeServiceTasks
            .Include(t => t.DepartmentService)
                .ThenInclude(s => s.Department)
            .Include(t => t.Appointment)
            .Include(t => t.PerformingDentistUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(t => t.StepProgresses)
                .ThenInclude(p => p.Step)
            .Include(t => t.CareEpisode)
            .FirstOrDefaultAsync(t => t.ServiceTaskId == serviceTaskId, ct);

        if (task == null)
            return ApiResponse<EpisodeServiceTaskDto>.Fail("Không tìm thấy tác vụ điều trị chỉ định.");

        var validStatuses = new[] { "Planned", "InProgress", "Completed", "Cancelled" };
        if (!validStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
            return ApiResponse<EpisodeServiceTaskDto>.Fail($"Trạng thái '{request.Status}' không hợp lệ.");

        var targetStatus = request.Status;

        if (string.Equals(targetStatus, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            task.Status = "InProgress";
            task.StartedAtUtc = DateTime.UtcNow;
            task.UpdatedAtUtc = DateTime.UtcNow;
        }
        else if (string.Equals(targetStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            var requiredIncompleteSteps = task.StepProgresses
                .Where(p => (p.Step?.IsRequired ?? true) && p.Status != "Completed")
                .OrderBy(p => p.Step?.StepOrder ?? 0)
                .ToList();

            if (requiredIncompleteSteps.Any())
            {
                var firstIncomplete = requiredIncompleteSteps.First();
                return ApiResponse<EpisodeServiceTaskDto>.Fail(
                    $"Chưa thể hoàn thành dịch vụ. Bước bắt buộc số {firstIncomplete.Step?.StepOrder}: '{firstIncomplete.Step?.StepName}' chưa được hoàn thành.");
            }

            task.Status = "Completed";
            task.CompletedAtUtc = DateTime.UtcNow;
            task.UpdatedAtUtc = DateTime.UtcNow;
            if (request.ClinicalRecordId.HasValue)
                task.ClinicalRecordId = request.ClinicalRecordId;
            if (!string.IsNullOrWhiteSpace(request.Notes))
                task.Notes = request.Notes.Trim();
        }
        else if (string.Equals(targetStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            task.Status = "Cancelled";
            task.CancelledAtUtc = DateTime.UtcNow;
            task.CancellationReason = request.CancellationReason?.Trim() ?? "Hủy theo yêu cầu lâm sàng";
            task.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Tác vụ {TaskCode} đã cập nhật sang trạng thái {Status} bởi bác sĩ {DentistUserId}",
            task.TaskCode, task.Status, dentistUserId);

        return ApiResponse<EpisodeServiceTaskDto>.Ok(MapToTaskDto(task), "Cập nhật trạng thái dịch vụ thành công.");
    }

    #endregion

    #region 3. Case B: Cross-Dentist Referral & Handoff (Doctor A -> Receptionist -> Doctor B)

    public async Task<ApiResponse<CareEpisodeReferralSummaryDto>> CreateInternalReferralAsync(long dentistUserId, CreateCareEpisodeReferralRequest request, CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Dữ liệu chuyển tiếp chuyên khoa không hợp lệ.");

        if (request.SourceAppointmentId <= 0)
            return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Vui lòng chỉ định ca khám nguồn.");

        if (request.ToDepartmentId <= 0)
            return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Vui lòng chọn chuyên khoa chuyển đến.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Vui lòng nhập lý do chuyển tiếp và chẩn đoán lâm sàng.");

        var sourceAppt = await _dbContext.Appointments
            .Include(a => a.PatientUser)
            .Include(a => a.Department)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.SourceAppointmentId, ct);

        if (sourceAppt == null)
            return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Không tìm thấy ca khám nguồn.");

        DepartmentService? requestedService = null;
        if (request.RequestedServiceId > 0)
        {
            requestedService = await _dbContext.DepartmentServices
                .FirstOrDefaultAsync(s => s.DepartmentServiceId == request.RequestedServiceId && s.Status == "Active", ct);

            if (requestedService == null)
                return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Dịch vụ yêu cầu không tồn tại hoặc đã ngừng hoạt động.");

            request.ToDepartmentId = requestedService.DepartmentId;
        }

        var targetDept = await _dbContext.Departments.FirstOrDefaultAsync(d => d.DepartmentId == request.ToDepartmentId && d.Status == "Active", ct);
        if (targetDept == null)
            return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Chuyên khoa chuyển đến không tồn tại hoặc đã ngừng hoạt động.");

        // Ensure episode exists
        if (!sourceAppt.CareEpisodeId.HasValue)
        {
            var epRes = await GetOrCreateActiveEpisodeForAppointmentAsync(sourceAppt.AppointmentId, ct);
            if (!epRes.Success || epRes.Data == null)
                return ApiResponse<CareEpisodeReferralSummaryDto>.Fail("Không thể khởi tạo đợt khám cho ca khám này.");
            sourceAppt.CareEpisodeId = epRes.Data.CareEpisodeId;
        }

        var episode = await _dbContext.CareEpisodes.FirstOrDefaultAsync(e => e.CareEpisodeId == sourceAppt.CareEpisodeId.Value, ct);
        if (episode != null)
        {
            episode.Status = "PendingAdditionalService";
            episode.UpdatedAtUtc = DateTime.UtcNow;
        }

        var referral = new DepartmentReferral
        {
            SourceAppointmentId = sourceAppt.AppointmentId,
            CareEpisodeId = sourceAppt.CareEpisodeId,
            FromDepartmentId = sourceAppt.DepartmentId ?? targetDept.DepartmentId,
            ToDepartmentId = request.ToDepartmentId,
            RequestedServiceId = request.RequestedServiceId > 0 ? request.RequestedServiceId : null,
            AssignedDentistUserId = request.PreferredDentistUserId,
            Reason = request.Reason.Trim(),
            Urgency = string.Equals(request.Urgency?.Trim(), "Urgent", StringComparison.OrdinalIgnoreCase) ? "Urgent" : "Normal",
            Status = "Pending",
            ClinicalInstructions = request.ClinicalInstructions?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DepartmentReferrals.Add(referral);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Bác sĩ {DentistUserId} tạo chuyển tiếp khoa #{ReferralId} từ ca #{SourceCode} sang khoa #{ToDeptId} cho bệnh nhân {PatientUserId}",
            dentistUserId, referral.ReferralId, sourceAppt.AppointmentCode, request.ToDepartmentId, sourceAppt.PatientUserId);

        var dto = new CareEpisodeReferralSummaryDto
        {
            ReferralId = referral.ReferralId,
            SourceAppointmentId = referral.SourceAppointmentId,
            SourceAppointmentCode = sourceAppt.AppointmentCode,
            FromDepartmentId = referral.FromDepartmentId,
            FromDepartmentName = sourceAppt.Department?.Name ?? string.Empty,
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
            .Include(r => r.AssignedDentistUser)
                .ThenInclude(d => d!.User)
            .Include(r => r.SourceAppointment)
            .Where(r => r.Status == "Pending")
            .OrderByDescending(r => r.Urgency == "Emergency")
            .ThenByDescending(r => r.Urgency == "Urgent")
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(ct);

        var list = referrals.Select(r => new CareEpisodeReferralSummaryDto
        {
            ReferralId = r.ReferralId,
            SourceAppointmentId = r.SourceAppointmentId,
            SourceAppointmentCode = r.SourceAppointment?.AppointmentCode,
            DestinationAppointmentId = r.DestinationAppointmentId,
            FromDepartmentId = r.FromDepartmentId,
            FromDepartmentName = r.FromDepartment.Name,
            ToDepartmentId = r.ToDepartmentId,
            ToDepartmentName = r.ToDepartment.Name,
            RequestedServiceId = r.RequestedServiceId,
            RequestedServiceName = r.RequestedService?.ServiceName,
            AssignedDentistUserId = r.AssignedDentistUserId,
            AssignedDentistName = r.AssignedDentistUser?.User?.FullName,
            Reason = r.Reason,
            Urgency = r.Urgency,
            Status = r.Status,
            ClinicalInstructions = r.ClinicalInstructions,
            CreatedAt = r.CreatedAt,
            ReviewedAt = r.ReviewedAt
        }).ToList();

        return ApiResponse<List<CareEpisodeReferralSummaryDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<AvailableDentistForReferralDto>>> GetEligibleDentistsForReferralAsync(long referralId, DateTime targetDate, CancellationToken ct = default)
    {
        var referral = await _dbContext.DepartmentReferrals
            .Include(r => r.ToDepartment)
            .FirstOrDefaultAsync(r => r.ReferralId == referralId, ct);

        if (referral == null)
            return ApiResponse<List<AvailableDentistForReferralDto>>.Fail("Không tìm thấy yêu cầu chuyển tiếp chỉ định.");

        var targetDeptId = referral.ToDepartmentId;
        var dateOnly = DateOnly.FromDateTime(targetDate);

        // Get dentists active in this department
        var activeDentists = await _dbContext.DentistDepartments
            .Include(dd => dd.DentistUser)
                .ThenInclude(d => d.User)
            .Where(dd => dd.DepartmentId == targetDeptId && dd.Status == "Active")
            .Select(dd => dd.DentistUser)
            .Distinct()
            .ToListAsync(ct);

        var result = new List<AvailableDentistForReferralDto>();

        foreach (var dentist in activeDentists)
        {
            // Check approved leave
            var hasLeave = await _dbContext.DentistLeaveRequests
                .AnyAsync(l => l.DentistUserId == dentist.UserId &&
                               l.Status == "Approved" &&
                               l.StartDate <= dateOnly &&
                               l.EndDate >= dateOnly, ct);

            bool isAvailable = !hasLeave;
            string? unavailReason = null;
            if (hasLeave)
            {
                unavailReason = "Bác sĩ có lịch nghỉ phép đã được phê duyệt trong ngày này.";
            }

            result.Add(new AvailableDentistForReferralDto
            {
                DentistUserId = dentist.UserId,
                FullName = dentist.User?.FullName ?? "Bác sĩ",
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
            .Include(r => r.CareEpisode)
            .Include(r => r.SourceAppointment)
            .Include(r => r.ToDepartment)
            .FirstOrDefaultAsync(r => r.ReferralId == request.ReferralId, ct);

        if (referral == null)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Không tìm thấy yêu cầu chuyển tiếp.");

        if (referral.Status != "Pending")
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail($"Yêu cầu này đã được xử lý trước đó (Trạng thái: {referral.Status}).");

        // Validate dentist belongs to ToDepartment
        var isDentistInDept = await _dbContext.DentistDepartments
            .AnyAsync(dd => dd.DentistUserId == request.AssignedDentistUserId && dd.DepartmentId == referral.ToDepartmentId && dd.Status == "Active", ct);

        if (!isDentistInDept)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Bác sĩ được chọn không thuộc chuyên khoa tiếp nhận.");

        var scheduledStart = request.ScheduledStart;
        var scheduledEnd = request.ScheduledEnd ?? scheduledStart.AddMinutes(30);

        if (scheduledStart >= scheduledEnd)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Thời gian bắt đầu ca khám phải trước thời gian kết thúc.");

        var dateOnly = DateOnly.FromDateTime(scheduledStart);

        // Check dentist approved leave
        var hasLeave = await _dbContext.DentistLeaveRequests
            .AnyAsync(l => l.DentistUserId == request.AssignedDentistUserId &&
                           l.Status == "Approved" &&
                           l.StartDate <= dateOnly &&
                           l.EndDate >= dateOnly, ct);

        if (hasLeave)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Bác sĩ tiếp nhận có lịch nghỉ phép đã phê duyệt trong khung giờ này.");

        // Check dentist overlapping appointments
        var hasDentistOverlap = await _dbContext.Appointments
            .AnyAsync(a => a.AssignedDentistUserId == request.AssignedDentistUserId &&
                           a.ScheduledStart.HasValue &&
                           a.ScheduledEnd.HasValue &&
                           a.ScheduledStart < scheduledEnd &&
                           a.ScheduledEnd > scheduledStart &&
                           a.Status != AppointmentStatus.Cancelled &&
                           a.Status != AppointmentStatus.Rejected, ct);

        if (hasDentistOverlap)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Bác sĩ đã có lịch hẹn khác trùng khung giờ này.");

        // Check chair overlapping if chair specified
        if (request.DentalChairId.HasValue && request.DentalChairId.Value > 0)
        {
            var chair = await _dbContext.DentalChairs.FirstOrDefaultAsync(c => c.DentalChairId == request.DentalChairId.Value && c.Status == "Active", ct);
            if (chair == null)
                return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Ghế điều trị không tồn tại hoặc đã ngừng hoạt động.");

            var hasChairOverlap = await _dbContext.Appointments
                .AnyAsync(a => a.DentalChairId == request.DentalChairId.Value &&
                               a.ScheduledStart.HasValue &&
                               a.ScheduledEnd.HasValue &&
                               a.ScheduledStart < scheduledEnd &&
                               a.ScheduledEnd > scheduledStart &&
                               a.Status != AppointmentStatus.Cancelled &&
                               a.Status != AppointmentStatus.Rejected, ct);

            if (hasChairOverlap)
                return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Ghế điều trị đã có ca hẹn khác sử dụng trong khung giờ này.");
        }

        var episodeId = referral.CareEpisodeId ?? referral.SourceAppointment?.CareEpisodeId;
        if (!episodeId.HasValue)
        {
            var epRes = await GetOrCreateActiveEpisodeForAppointmentAsync(referral.SourceAppointmentId ?? 0, ct);
            episodeId = epRes.Data?.CareEpisodeId;
        }

        var apptCode = await GenerateUniqueAppointmentCodeAsync(ct);

        // Allocate Monotonic Queue Number for Department B on scheduled date (Policy: Allocated on Confirmed)
        var targetDate = scheduledStart.Date;
        var existingQueueNumbers = await _dbContext.Appointments
            .Where(a => a.DepartmentId == referral.ToDepartmentId &&
                        a.ScheduledStart.HasValue &&
                        a.ScheduledStart.Value.Date == targetDate &&
                        !string.IsNullOrEmpty(a.QueueNumber))
            .Select(a => a.QueueNumber!)
            .ToListAsync(ct);

        int maxSeq = 0;
        foreach (var qn in existingQueueNumbers)
        {
            var match = System.Text.RegularExpressions.Regex.Match(qn, @"\d+$");
            if (match.Success && int.TryParse(match.Value, out int num))
            {
                if (num > maxSeq) maxSeq = num;
            }
        }

        var deptCode = GetDepartmentCode(referral.ToDepartment?.Name, referral.ToDepartmentId);
        var queueNumber = $"Q-{deptCode}-{(maxSeq + 1):D3}";

        // CREATE APPOINTMENT B: Status = Confirmed, QueueNumber allocated immediately upon Confirmed
        var apptB = new Appointment
        {
            AppointmentCode = apptCode,
            PatientUserId = referral.SourceAppointment?.PatientUserId ?? referral.CareEpisode?.PatientUserId ?? 0,
            DepartmentId = referral.ToDepartmentId,
            RequestedServiceId = referral.RequestedServiceId,
            AssignedDentistUserId = request.AssignedDentistUserId,
            RoomId = request.RoomId,
            DentalChairId = request.DentalChairId,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledEnd,
            Status = AppointmentStatus.Confirmed,
            QueueNumber = queueNumber,
            CareEpisodeId = episodeId,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = $"Chuyển chuyên khoa từ ca khám #{referral.SourceAppointment?.AppointmentCode}: {referral.Reason}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Appointments.Add(apptB);
        await _dbContext.SaveChangesAsync(ct);

        // Record status history for Appointment B Confirmed
        _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
        {
            AppointmentId = apptB.AppointmentId,
            OldStatus = null,
            NewStatus = AppointmentStatus.Confirmed,
            ChangedByUserId = receptionistUserId,
            Note = $"Tạo và xác nhận ca khám chuyển tiếp #{apptB.AppointmentCode}. Cấp số thứ tự: {queueNumber}.",
            ChangedAt = DateTime.UtcNow
        });

        // Update referral
        referral.DestinationAppointmentId = apptB.AppointmentId;
        referral.AssignedDentistUserId = request.AssignedDentistUserId;
        referral.Status = "Accepted";
        referral.ReviewedAt = DateTime.UtcNow;

        // Auto-create initial planned task for Appointment B if requestedService exists
        if (referral.RequestedServiceId.HasValue && episodeId.HasValue)
        {
            var srv = await _dbContext.DepartmentServices.FirstOrDefaultAsync(s => s.DepartmentServiceId == referral.RequestedServiceId.Value, ct);
            if (srv != null)
            {
                var activeWf = await _dbContext.ServiceWorkflows
                    .Include(w => w.Steps)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(w => w.DepartmentServiceId == srv.DepartmentServiceId && w.IsActive && w.ApprovalStatus == "Approved", ct);

                var taskCode = await GenerateTaskCodeAsync(ct);
                var bTask = new EpisodeServiceTask
                {
                    TaskCode = taskCode,
                    CareEpisodeId = episodeId.Value,
                    AppointmentId = apptB.AppointmentId,
                    DepartmentServiceId = srv.DepartmentServiceId,
                    PerformingDentistUserId = request.AssignedDentistUserId,
                    Status = "Planned",
                    Quantity = 1,
                    UnitPriceSnapshot = srv.Price,
                    EstimatedDurationMinutes = srv.DurationMinutes ?? 30,
                    AppliedWorkflowId = activeWf?.WorkflowId,
                    AppliedWorkflowVersion = activeWf?.Version,
                    PlannedAtUtc = DateTime.UtcNow,
                    Notes = $"Dịch vụ chỉ định theo ca chuyển tiếp #{referral.ReferralId}",
                    CreatedAtUtc = DateTime.UtcNow
                };

                if (activeWf != null && activeWf.Steps != null)
                {
                    foreach (var step in activeWf.Steps.OrderBy(s => s.StepOrder))
                    {
                        bTask.StepProgresses.Add(new EpisodeServiceTaskStepProgress
                        {
                            StepId = step.StepId,
                            Status = "Pending",
                            CreatedAtUtc = DateTime.UtcNow
                        });
                    }
                }

                _dbContext.EpisodeServiceTasks.Add(bTask);
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        // Post-commit notifications for Patient & Dentist (Idempotent notification contract)
        if (_notificationService != null && apptB.PatientUserId > 0)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    apptB.PatientUserId,
                    apptB.AppointmentId,
                    "AppointmentConfirmed",
                    $"Xác nhận lịch hẹn chuyển chuyên khoa #{apptB.AppointmentCode}",
                    $"Lịch khám chuyển chuyên khoa của bạn tại {referral.ToDepartment?.Name ?? "phòng khám"} đã được xác nhận. Số thứ tự: {queueNumber}. Vui lòng đến đúng giờ.",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi gửi thông báo lịch hẹn cho bệnh nhân {PatientUserId}", apptB.PatientUserId);
            }
        }

        if (_notificationService != null && request.AssignedDentistUserId > 0)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    request.AssignedDentistUserId,
                    apptB.AppointmentId,
                    "AppointmentAssigned",
                    $"Phân công ca khám chuyển khoa #{apptB.AppointmentCode}",
                    $"Bạn được phân công tiếp nhận ca khám chuyển khoa #{apptB.AppointmentCode} ({referral.ToDepartment?.Name}). Bệnh nhân có STT: {queueNumber}.",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi gửi thông báo phân công cho bác sĩ {DentistUserId}", request.AssignedDentistUserId);
            }
        }

        _logger.LogInformation("Điều phối ca khám đích #{ApptCode} (ID: {ApptId}) cho ca chuyển khoa #{ReferralId} với STT {QueueNumber} bởi lễ tân {ReceptionistUserId}",
            apptB.AppointmentCode, apptB.AppointmentId, referral.ReferralId, queueNumber, receptionistUserId);

        var assignedDentist = await _dbContext.DentistProfiles
            .Include(d => d.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == request.AssignedDentistUserId, ct);

        var srvInfo = referral.RequestedServiceId.HasValue
            ? await _dbContext.DepartmentServices.AsNoTracking().FirstOrDefaultAsync(s => s.DepartmentServiceId == referral.RequestedServiceId.Value, ct)
            : null;

        var dto = new CareEpisodeAppointmentSummaryDto
        {
            AppointmentId = apptB.AppointmentId,
            AppointmentCode = apptB.AppointmentCode,
            DepartmentId = apptB.DepartmentId,
            DepartmentName = referral.ToDepartment.Name,
            RequestedServiceId = apptB.RequestedServiceId,
            RequestedServiceName = srvInfo?.ServiceName,
            AssignedDentistUserId = apptB.AssignedDentistUserId,
            AssignedDentistName = assignedDentist?.User?.FullName,
            RoomId = apptB.RoomId,
            DentalChairId = apptB.DentalChairId,
            ScheduledStart = apptB.ScheduledStart,
            ScheduledEnd = apptB.ScheduledEnd,
            Status = apptB.Status,
            QueueNumber = apptB.QueueNumber,
            IsRoot = false,
            ReasonForVisit = apptB.ReasonForVisit
        };

        return ApiResponse<CareEpisodeAppointmentSummaryDto>.Ok(dto, $"Điều phối ca khám chuyển khoa thành công. Ca khám đang ở trạng thái Confirmed với số thứ tự: {queueNumber}.");
    }

    public async Task<ApiResponse<CareEpisodeAppointmentSummaryDto>> StationBCheckInAsync(long receptionistUserId, StationBCheckInRequest request, CancellationToken ct = default)
    {
        if (request == null || request.AppointmentId <= 0)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Mã ca khám không hợp lệ.");

        var appt = await _dbContext.Appointments
            .Include(a => a.Department)
            .Include(a => a.AssignedDentistUser)
                .ThenInclude(d => d!.User)
            .Include(a => a.CareEpisode)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

        if (appt == null)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail("Không tìm thấy ca khám chỉ định.");

        if (appt.Status == AppointmentStatus.CheckedIn || appt.Status == AppointmentStatus.InService)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail($"Ca khám đã được Check-in trước đó (Trạng thái: {appt.Status}, STT: {appt.QueueNumber}).");

        if (appt.Status != AppointmentStatus.Confirmed && appt.Status != AppointmentStatus.PendingReceptionReview)
            return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail($"Ca khám ở trạng thái '{appt.Status}', không thể Check-in.");

        // CRITICAL INVARIANT: Sequential InService check across the entire CareEpisode
        if (appt.CareEpisodeId.HasValue)
        {
            var isAnotherInService = await _dbContext.Appointments
                .AnyAsync(a => a.CareEpisodeId == appt.CareEpisodeId.Value &&
                               a.AppointmentId != appt.AppointmentId &&
                               a.Status == AppointmentStatus.InService, ct);

            if (isAnotherInService)
            {
                return ApiResponse<CareEpisodeAppointmentSummaryDto>.Fail(
                    "Bệnh nhân hiện đang trong quá trình thực hiện dịch vụ tại trạm khám trước (InService). Vui lòng đợi bác sĩ hoàn tất trước khi tiến hành Check-in và gọi vào Trạm B.");
            }
        }

        // PRESERVE QueueNumber if already allocated upon Confirmed (Policy Alignment)
        var queueNumber = appt.QueueNumber;
        if (string.IsNullOrEmpty(queueNumber))
        {
            // Safe fallback for legacy records missing QueueNumber
            var targetDate = (appt.ScheduledStart ?? DateTime.UtcNow).Date;
            var existingQueueNumbers = await _dbContext.Appointments
                .Where(a => a.DepartmentId == appt.DepartmentId &&
                            a.ScheduledStart.HasValue &&
                            a.ScheduledStart.Value.Date == targetDate &&
                            !string.IsNullOrEmpty(a.QueueNumber))
                .Select(a => a.QueueNumber!)
                .ToListAsync(ct);

            int maxSeq = 0;
            foreach (var qn in existingQueueNumbers)
            {
                var match = System.Text.RegularExpressions.Regex.Match(qn, @"\d+$");
                if (match.Success && int.TryParse(match.Value, out int num))
                {
                    if (num > maxSeq) maxSeq = num;
                }
            }

            var deptCode = GetDepartmentCode(appt.Department?.Name, appt.DepartmentId);
            queueNumber = $"Q-{deptCode}-{(maxSeq + 1):D3}";
            appt.QueueNumber = queueNumber;
            _logger.LogWarning("Ca khám #{ApptCode} thiếu QueueNumber lúc Confirmed. Đã cấp fallback: {QueueNumber}", appt.AppointmentCode, queueNumber);
        }

        var oldStatus = appt.Status;
        appt.Status = AppointmentStatus.CheckedIn;
        appt.CheckedInAt = DateTime.UtcNow;
        appt.UpdatedAt = DateTime.UtcNow;

        _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
        {
            AppointmentId = appt.AppointmentId,
            OldStatus = oldStatus,
            NewStatus = AppointmentStatus.CheckedIn,
            ChangedByUserId = receptionistUserId,
            Note = $"Bệnh nhân đã đến Trạm B và hoàn tất Check-in. Giữ nguyên số thứ tự: {queueNumber}.",
            ChangedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Lễ tân {UserId} hoàn tất Check-in Trạm B cho ca #{ApptCode}, giữ nguyên STT: {QueueNumber}",
            receptionistUserId, appt.AppointmentCode, queueNumber);

        var dto = new CareEpisodeAppointmentSummaryDto
        {
            AppointmentId = appt.AppointmentId,
            AppointmentCode = appt.AppointmentCode,
            DepartmentId = appt.DepartmentId,
            DepartmentName = appt.Department?.Name,
            RequestedServiceId = appt.RequestedServiceId,
            AssignedDentistUserId = appt.AssignedDentistUserId,
            AssignedDentistName = appt.AssignedDentistUser?.User?.FullName,
            RoomId = appt.RoomId,
            DentalChairId = appt.DentalChairId,
            ScheduledStart = appt.ScheduledStart,
            ScheduledEnd = appt.ScheduledEnd,
            Status = appt.Status,
            QueueNumber = appt.QueueNumber,
            IsRoot = false,
            ReasonForVisit = appt.ReasonForVisit
        };

        return ApiResponse<CareEpisodeAppointmentSummaryDto>.Ok(dto, $"Check-in Trạm B thành công. Giữ nguyên số thứ tự: {queueNumber}.");
    }

    #endregion

    #region 4. Single Consolidated Invoice per Episode

    public async Task<ApiResponse<ConsolidatedEpisodeBillingSummaryDto>> GetConsolidatedInvoiceSummaryAsync(long careEpisodeId, CancellationToken ct = default)
    {
        var episode = await _dbContext.CareEpisodes
            .Include(e => e.PatientUser)
                .ThenInclude(u => u.PatientProfile)
            .Include(e => e.Appointments)
                .ThenInclude(a => a.Department)
            .Include(e => e.Appointments)
                .ThenInclude(a => a.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(e => e.Appointments)
                .ThenInclude(a => a.RequestedService)
            .Include(e => e.ServiceTasks)
                .ThenInclude(t => t.DepartmentService)
            .Include(e => e.ServiceTasks)
                .ThenInclude(t => t.PerformingDentistUser)
                    .ThenInclude(u => u.StaffProfile)
            .Include(e => e.ServiceTasks)
                .ThenInclude(t => t.Appointment)
            .Include(e => e.Invoices)
                .ThenInclude(i => i.InvoiceItems)
            .Include(e => e.Invoices)
                .ThenInclude(i => i.Payments)
            .FirstOrDefaultAsync(e => e.CareEpisodeId == careEpisodeId, ct);

        if (episode == null)
            return ApiResponse<ConsolidatedEpisodeBillingSummaryDto>.Fail("Không tìm thấy đợt khám chỉ định.");

        var apptIds = episode.Appointments.Select(a => a.AppointmentId).ToList();

        // 1. Collect all completed service tasks under this episode
        var completedTasks = episode.ServiceTasks
            .Where(t => t.Status == "Completed")
            .ToList();

        var lineItems = new List<ConsolidatedBillingLineItemDto>();

        foreach (var task in completedTasks)
        {
            lineItems.Add(new ConsolidatedBillingLineItemDto
            {
                ServiceTaskId = task.ServiceTaskId,
                AppointmentId = task.AppointmentId,
                AppointmentCode = task.Appointment?.AppointmentCode ?? string.Empty,
                Description = task.DepartmentService.ServiceName,
                PerformingDentistUserId = task.PerformingDentistUserId,
                PerformingDentistName = GetUserDisplayName(task.PerformingDentistUser),
                Quantity = task.Quantity,
                UnitPrice = task.UnitPriceSnapshot,
                SourceType = "ServiceTask",
                SourceId = task.ServiceTaskId
            });
        }

        // 2. Collect completed diagnostic imaging orders under this episode
        var diagnosticOrders = await _dbContext.DiagnosticOrders
            .Include(d => d.Appointment)
            .Where(d => apptIds.Contains(d.AppointmentId) && d.Status == "Completed")
            .ToListAsync(ct);

        foreach (var diag in diagnosticOrders)
        {
            lineItems.Add(new ConsolidatedBillingLineItemDto
            {
                AppointmentId = diag.AppointmentId,
                AppointmentCode = diag.Appointment.AppointmentCode,
                Description = $"Chẩn đoán hình ảnh: {diag.DiagnosticType}",
                Quantity = 1,
                UnitPrice = 150000m,
                SourceType = "DiagnosticOrder",
                SourceId = diag.DiagnosticOrderId
            });
        }

        // 3. Fallback: If no completed service tasks yet, check if any appointment has requested service completed
        if (!lineItems.Any())
        {
            foreach (var appt in episode.Appointments.Where(a => a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.InService))
            {
                if (appt.RequestedService != null)
                {
                    lineItems.Add(new ConsolidatedBillingLineItemDto
                    {
                        AppointmentId = appt.AppointmentId,
                        AppointmentCode = appt.AppointmentCode,
                        Description = appt.RequestedService.ServiceName,
                        PerformingDentistUserId = appt.AssignedDentistUserId,
                        PerformingDentistName = appt.AssignedDentistUser?.User?.FullName,
                        Quantity = 1,
                        UnitPrice = appt.RequestedService.Price,
                        SourceType = "AppointmentService",
                        SourceId = appt.RequestedServiceId
                    });
                }
            }
        }

        var subTotal = lineItems.Sum(i => i.TotalAmount);
        var existingInvoice = episode.Invoices.OrderByDescending(i => i.InvoiceId).FirstOrDefault();
        var taxItem = existingInvoice?.InvoiceItems.FirstOrDefault(ii => ii.Description.Contains("VAT"));
        var taxAmount = taxItem?.Amount ?? 0m;
        var paidAmount = existingInvoice != null && existingInvoice.Status == "Paid"
            ? existingInvoice.TotalAmount
            : (existingInvoice?.Payments.Where(p => p.Status == "Completed" || p.Status == "Success").Sum(p => p.Amount) ?? 0m);

        var summary = new ConsolidatedEpisodeBillingSummaryDto
        {
            CareEpisodeId = episode.CareEpisodeId,
            EpisodeCode = episode.EpisodeCode,
            PatientUserId = episode.PatientUserId,
            PatientName = GetUserDisplayName(episode.PatientUser),
            EpisodeStatus = episode.Status,
            HasExistingInvoice = existingInvoice != null,
            ExistingInvoiceId = existingInvoice?.InvoiceId,
            ExistingInvoiceCode = existingInvoice?.InvoiceCode,
            ExistingInvoiceStatus = existingInvoice?.Status,
            SubTotal = subTotal,
            DiscountAmount = 0m,
            TaxPercent = 0m,
            TaxAmount = taxAmount,
            TotalAmount = existingInvoice?.TotalAmount ?? subTotal,
            PaidAmount = paidAmount,
            BalanceRemaining = Math.Max(0m, (existingInvoice?.TotalAmount ?? subTotal) - paidAmount),
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
            var episode = await _dbContext.CareEpisodes
                .Include(e => e.PatientUser)
                    .ThenInclude(u => u.PatientProfile)
                .Include(e => e.Appointments)
                    .ThenInclude(a => a.Department)
                .Include(e => e.Appointments)
                    .ThenInclude(a => a.AssignedDentistUser)
                        .ThenInclude(d => d!.User)
                .Include(e => e.Appointments)
                    .ThenInclude(a => a.RequestedService)
                .Include(e => e.ServiceTasks)
                    .ThenInclude(t => t.DepartmentService)
                .Include(e => e.ServiceTasks)
                    .ThenInclude(t => t.PerformingDentistUser)
                        .ThenInclude(u => u.StaffProfile)
                .Include(e => e.Invoices)
                    .ThenInclude(i => i.InvoiceItems)
                .FirstOrDefaultAsync(e => e.CareEpisodeId == request.CareEpisodeId, ct);

            if (episode == null)
            {
                await tx.RollbackAsync(ct);
                return ApiResponse<InvoiceDto>.Fail("Không tìm thấy đợt khám chỉ định.");
            }

            // CRITICAL INVARIANT: Protect existing non-Unpaid invoices from mutation or duplicate invoice creation
            var existingInvoice = episode.Invoices.FirstOrDefault();
            if (existingInvoice != null && existingInvoice.Status != "Unpaid")
            {
                await tx.RollbackAsync(ct);
                if (existingInvoice.Status == "Paid")
                {
                    return ApiResponse<InvoiceDto>.Fail(
                        $"Đợt khám này đã có hóa đơn #{existingInvoice.InvoiceCode} đã thanh toán (Paid). Để bảo toàn dòng tiền, hóa đơn đã thanh toán không được chỉnh sửa hoặc tạo đè.");
                }
                return ApiResponse<InvoiceDto>.Fail(
                    $"Đợt khám này đã có hóa đơn #{existingInvoice.InvoiceCode} với trạng thái {existingInvoice.Status}. Để bảo toàn dữ liệu tài chính, không được chỉnh sửa hoặc tạo thêm hóa đơn.");
            }

            // Get consolidated billing summary items
            var summaryRes = await GetConsolidatedInvoiceSummaryAsync(episode.CareEpisodeId, ct);
            if (!summaryRes.Success || summaryRes.Data == null || !summaryRes.Data.LineItems.Any())
            {
                await tx.RollbackAsync(ct);
                return ApiResponse<InvoiceDto>.Fail("Đợt khám chưa có dịch vụ nào hoàn tất để xuất hóa đơn.");
            }

            var lineItems = summaryRes.Data.LineItems;
            var subTotal = lineItems.Sum(i => i.TotalAmount);
            var discount = Math.Max(0m, request.DiscountAmount);
            if (discount > subTotal) discount = subTotal;

            var discountedSubTotal = subTotal - discount;
            var taxPercent = Math.Max(0m, request.TaxPercent);
            if (taxPercent > 100m) taxPercent = 100m;

            var taxAmount = Math.Round(discountedSubTotal * (taxPercent / 100m), 2);
            var totalAmount = Math.Round(discountedSubTotal + taxAmount, 2);

            // Deduplicate line items by ServiceTaskId within this batch
            var distinctLineItems = new List<ConsolidatedBillingLineItemDto>();
            var seenTaskIds = new HashSet<long>();
            foreach (var item in lineItems)
            {
                if (item.ServiceTaskId.HasValue)
                {
                    if (!seenTaskIds.Add(item.ServiceTaskId.Value))
                    {
                        continue;
                    }
                }
                distinctLineItems.Add(item);
            }

            // Check if there is an existing Unpaid invoice to update, or create a new one
            Invoice invoice;

            if (existingInvoice != null && existingInvoice.Status == "Unpaid")
            {
                invoice = existingInvoice;
                _dbContext.InvoiceItems.RemoveRange(invoice.InvoiceItems);
                invoice.InvoiceItems.Clear();
                invoice.TotalAmount = totalAmount;
                invoice.GeneratedByUserId = receptionistUserId;
            }
            else
            {
                var invoiceCode = await GenerateInvoiceCodeAsync(ct);
                invoice = new Invoice
                {
                    InvoiceCode = invoiceCode,
                    CareEpisodeId = episode.CareEpisodeId,
                    AppointmentId = episode.RootAppointmentId,
                    GeneratedByUserId = receptionistUserId,
                    TotalAmount = totalAmount,
                    Status = totalAmount == 0 ? "Paid" : "Unpaid",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Invoices.Add(invoice);
            }

            // Anti-double-billing: Verify that none of these ServiceTasks are billed in any OTHER invoice
            var taskIdsToCheck = distinctLineItems.Where(i => i.ServiceTaskId.HasValue).Select(i => i.ServiceTaskId!.Value).ToList();
            if (taskIdsToCheck.Any())
            {
                var alreadyBilledTask = await _dbContext.InvoiceItems
                    .Where(ii => ii.ServiceTaskId.HasValue && taskIdsToCheck.Contains(ii.ServiceTaskId.Value) && (invoice.InvoiceId == 0 || ii.InvoiceId != invoice.InvoiceId))
                    .Select(ii => new { ii.ServiceTaskId, ii.Invoice.InvoiceCode })
                    .FirstOrDefaultAsync(ct);

                if (alreadyBilledTask != null)
                {
                    await tx.RollbackAsync(ct);
                    return ApiResponse<InvoiceDto>.Fail(
                        $"Dịch vụ chỉ định ID {alreadyBilledTask.ServiceTaskId} đã được lập hóa đơn tại #{alreadyBilledTask.InvoiceCode}. Không thể lập hóa đơn trùng lặp.");
                }
            }

            // Add consolidated line items
            foreach (var item in distinctLineItems)
            {
                invoice.InvoiceItems.Add(new InvoiceItem
                {
                    AppointmentId = item.AppointmentId,
                    PerformingDentistUserId = item.PerformingDentistUserId,
                    ServiceTaskId = item.ServiceTaskId,
                    SourceType = item.SourceType,
                    SourceId = item.SourceId,
                    Description = item.Description.Trim(),
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Amount = item.TotalAmount
                });
            }

            // VAT item if tax applied
            if (taxPercent > 0 && taxAmount > 0)
            {
                invoice.InvoiceItems.Add(new InvoiceItem
                {
                    AppointmentId = episode.RootAppointmentId,
                    Description = $"Thuế giá trị gia tăng (VAT {taxPercent:G29}%)",
                    Quantity = 1,
                    UnitPrice = taxAmount,
                    Amount = taxAmount,
                    SourceType = "Tax"
                });
            }

            // Update Care Episode status
            episode.Status = totalAmount == 0 ? "Closed" : "ReadyForBilling";
            if (totalAmount == 0)
            {
                episode.ClosedAtUtc = DateTime.UtcNow;
            }
            episode.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("Đã lập hóa đơn tổng hợp #{InvoiceCode} ({TotalAmount:N0} đ) cho đợt khám #{EpisodeCode}",
                invoice.InvoiceCode, invoice.TotalAmount, episode.EpisodeCode);

            // Map and return
            var staffUser = await _dbContext.UserAccounts
                .Include(u => u.StaffProfile)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == receptionistUserId, ct);
            var rootAppt = episode.Appointments.FirstOrDefault(a => a.AppointmentId == episode.RootAppointmentId);

            var invoiceDto = new InvoiceDto
            {
                InvoiceId = invoice.InvoiceId,
                InvoiceCode = invoice.InvoiceCode,
                CareEpisodeId = episode.CareEpisodeId,
                CareEpisodeCode = episode.EpisodeCode,
                AppointmentId = invoice.AppointmentId,
                AppointmentCode = rootAppt?.AppointmentCode,
                PatientUserId = episode.PatientUserId,
                PatientName = GetUserDisplayName(episode.PatientUser),
                PatientPhoneNumber = episode.PatientUser.PhoneNumber,
                PatientEmail = episode.PatientUser.Email,
                GeneratedByUserId = receptionistUserId,
                GeneratedByName = GetUserDisplayName(staffUser),
                SubTotal = subTotal,
                DiscountAmount = discount,
                TaxPercent = taxPercent,
                TaxAmount = taxAmount,
                TotalAmount = totalAmount,
                PaidAmount = invoice.Status == "Paid" ? totalAmount : 0,
                RemainingAmount = invoice.Status == "Paid" ? 0 : totalAmount,
                Status = invoice.Status,
                CreatedAt = invoice.CreatedAt,
                Items = invoice.InvoiceItems.Select(ii => new InvoiceItemDto
                {
                    InvoiceItemId = ii.InvoiceItemId,
                    InvoiceId = ii.InvoiceId,
                    AppointmentId = ii.AppointmentId,
                    PerformingDentistUserId = ii.PerformingDentistUserId,
                    ServiceTaskId = ii.ServiceTaskId,
                    SourceType = ii.SourceType,
                    SourceId = ii.SourceId,
                    Description = ii.Description,
                    Quantity = ii.Quantity,
                    UnitPrice = ii.UnitPrice,
                    Amount = ii.Quantity * ii.UnitPrice
                }).ToList()
            };

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

    private CareEpisodeDto MapCareEpisodeToDto(CareEpisode ep)
    {
        var appointments = ep.Appointments.Select(a => new CareEpisodeAppointmentSummaryDto
        {
            AppointmentId = a.AppointmentId,
            AppointmentCode = a.AppointmentCode,
            DepartmentId = a.DepartmentId,
            DepartmentName = a.Department?.Name,
            RequestedServiceId = a.RequestedServiceId,
            RequestedServiceName = a.RequestedService?.ServiceName,
            AssignedDentistUserId = a.AssignedDentistUserId,
            AssignedDentistName = a.AssignedDentistUser?.User?.FullName,
            RoomId = a.RoomId,
            RoomName = a.Room?.RoomCode,
            DentalChairId = a.DentalChairId,
            ChairName = a.DentalChair?.ChairCode,
            ScheduledStart = a.ScheduledStart,
            ScheduledEnd = a.ScheduledEnd,
            Status = a.Status,
            QueueNumber = a.QueueNumber,
            IsRoot = a.AppointmentId == ep.RootAppointmentId,
            ReasonForVisit = a.ReasonForVisit
        }).OrderBy(a => a.AppointmentId).ToList();

        var serviceTasks = ep.ServiceTasks
            .Select(t => MapToTaskDto(t))
            .OrderBy(t => t.ServiceTaskId)
            .ToList();

        var referrals = ep.DepartmentReferrals.Select(r => new CareEpisodeReferralSummaryDto
        {
            ReferralId = r.ReferralId,
            SourceAppointmentId = r.SourceAppointmentId,
            SourceAppointmentCode = r.SourceAppointment?.AppointmentCode,
            DestinationAppointmentId = r.DestinationAppointmentId,
            DestinationAppointmentCode = r.DestinationAppointment?.AppointmentCode,
            FromDepartmentId = r.FromDepartmentId,
            FromDepartmentName = r.FromDepartment?.Name ?? string.Empty,
            ToDepartmentId = r.ToDepartmentId,
            ToDepartmentName = r.ToDepartment?.Name ?? string.Empty,
            RequestedServiceId = r.RequestedServiceId,
            RequestedServiceName = r.RequestedService?.ServiceName,
            AssignedDentistUserId = r.AssignedDentistUserId,
            AssignedDentistName = r.AssignedDentistUser?.User?.FullName,
            Reason = r.Reason,
            Urgency = r.Urgency,
            Status = r.Status,
            ClinicalInstructions = r.ClinicalInstructions,
            CreatedAt = r.CreatedAt,
            ReviewedAt = r.ReviewedAt
        }).OrderBy(r => r.ReferralId).ToList();

        var invoice = ep.Invoices.OrderByDescending(i => i.InvoiceId).FirstOrDefault();
        InvoiceDto? invoiceDto = null;
        if (invoice != null)
        {
            invoiceDto = new InvoiceDto
            {
                InvoiceId = invoice.InvoiceId,
                InvoiceCode = invoice.InvoiceCode,
                CareEpisodeId = ep.CareEpisodeId,
                CareEpisodeCode = ep.EpisodeCode,
                AppointmentId = invoice.AppointmentId,
                PatientUserId = ep.PatientUserId,
                PatientName = GetUserDisplayName(ep.PatientUser),
                PatientPhoneNumber = ep.PatientUser.PhoneNumber,
                PatientEmail = ep.PatientUser.Email,
                GeneratedByUserId = invoice.GeneratedByUserId,
                TotalAmount = invoice.TotalAmount,
                Status = invoice.Status,
                CreatedAt = invoice.CreatedAt,
                Items = invoice.InvoiceItems.Select(ii => new InvoiceItemDto
                {
                    InvoiceItemId = ii.InvoiceItemId,
                    InvoiceId = ii.InvoiceId,
                    AppointmentId = ii.AppointmentId,
                    PerformingDentistUserId = ii.PerformingDentistUserId,
                    ServiceTaskId = ii.ServiceTaskId,
                    SourceType = ii.SourceType,
                    SourceId = ii.SourceId,
                    Description = ii.Description,
                    Quantity = ii.Quantity,
                    UnitPrice = ii.UnitPrice,
                    Amount = ii.Quantity * ii.UnitPrice
                }).ToList()
            };
        }

        var totalEstimatedCost = serviceTasks.Sum(t => t.TotalAmount);
        var totalActualCost = serviceTasks.Where(t => t.Status == "Completed").Sum(t => t.TotalAmount);
        var totalPaid = invoice != null && invoice.Status == "Paid" ? invoice.TotalAmount : 0;

        return new CareEpisodeDto
        {
            CareEpisodeId = ep.CareEpisodeId,
            EpisodeCode = ep.EpisodeCode,
            PatientUserId = ep.PatientUserId,
            PatientName = GetUserDisplayName(ep.PatientUser),
            PatientPhone = ep.PatientUser.PhoneNumber,
            RootAppointmentId = ep.RootAppointmentId,
            RootAppointmentCode = ep.RootAppointment?.AppointmentCode ?? string.Empty,
            Status = ep.Status,
            OpenedAtUtc = ep.OpenedAtUtc,
            ClosedAtUtc = ep.ClosedAtUtc,
            Notes = ep.Notes,
            CreatedAtUtc = ep.CreatedAtUtc,
            Appointments = appointments,
            ServiceTasks = serviceTasks,
            Referrals = referrals,
            ConsolidatedInvoice = invoiceDto,
            TotalEstimatedCost = totalEstimatedCost,
            TotalActualCost = totalActualCost,
            TotalPaidAmount = totalPaid,
            RemainingBalance = Math.Max(0, totalActualCost - totalPaid)
        };
    }

    private static string GetUserDisplayName(UserAccount? user)
    {
        if (user == null) return string.Empty;
        return user.StaffProfile?.FullName ?? user.PatientProfile?.FullName ?? user.Email ?? "N/A";
    }

    private async Task<string> GenerateEpisodeCodeAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow;
        var prefix = $"EP-{today:yyyyMMdd}-";
        var count = await _dbContext.CareEpisodes
            .CountAsync(e => e.OpenedAtUtc.Date == today.Date, ct);
        return $"{prefix}{(count + 1):D4}";
    }

    private async Task<string> GenerateTaskCodeAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow;
        var prefix = $"TSK-{today:yyyyMMdd}-";
        var count = await _dbContext.EpisodeServiceTasks
            .CountAsync(t => t.CreatedAtUtc.Date == today.Date, ct);
        return $"{prefix}{(count + 1):D4}";
    }

    private async Task<string> GenerateInvoiceCodeAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow;
        var prefix = $"INV-{today:yyyyMMdd}-";
        var count = await _dbContext.Invoices
            .CountAsync(i => i.CreatedAt.Date == today.Date, ct);
        return $"{prefix}{(count + 1):D4}";
    }

    private async Task<string> GenerateUniqueAppointmentCodeAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow;
        var prefix = $"APT-{today:yyyyMMdd}-";
        var count = await _dbContext.Appointments
            .CountAsync(a => a.CreatedAt.Date == today.Date, ct);
        return $"{prefix}{(count + 1):D4}";
    }

    private static string GetDepartmentCode(string? deptName, long? deptId)
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

    private async Task EnsurePrimaryTaskForAppointmentAsync(Appointment appointment, long episodeId, CancellationToken ct)
    {
        if (!appointment.RequestedServiceId.HasValue) return;

        var hasExistingTask = await _dbContext.EpisodeServiceTasks.AnyAsync(t => t.AppointmentId == appointment.AppointmentId, ct);
        if (hasExistingTask) return;

        var srv = await _dbContext.DepartmentServices
            .Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == appointment.RequestedServiceId.Value && s.Status == "Active", ct);

        if (srv == null) return;

        var activeWf = await _dbContext.ServiceWorkflows
            .Include(w => w.Steps)
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.DepartmentServiceId == srv.DepartmentServiceId && w.IsActive && w.ApprovalStatus == "Approved", ct);

        var taskCode = await GenerateTaskCodeAsync(ct);
        var dentistId = appointment.AssignedDentistUserId ?? appointment.RequestedDentistUserId ?? 0;
        var initTask = new EpisodeServiceTask
        {
            TaskCode = taskCode,
            CareEpisodeId = episodeId,
            AppointmentId = appointment.AppointmentId,
            DepartmentServiceId = srv.DepartmentServiceId,
            PerformingDentistUserId = dentistId,
            Status = "Planned",
            Quantity = 1,
            UnitPriceSnapshot = srv.Price,
            EstimatedDurationMinutes = srv.DurationMinutes ?? 30,
            AppliedWorkflowId = activeWf?.WorkflowId,
            AppliedWorkflowVersion = activeWf?.Version,
            PlannedAtUtc = DateTime.UtcNow,
            Notes = $"Dịch vụ ban đầu của ca khám #{appointment.AppointmentCode}",
            CreatedAtUtc = DateTime.UtcNow
        };

        if (activeWf != null && activeWf.Steps != null)
        {
            foreach (var step in activeWf.Steps.OrderBy(s => s.StepOrder))
            {
                initTask.StepProgresses.Add(new EpisodeServiceTaskStepProgress
                {
                    StepId = step.StepId,
                    Status = "Pending",
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        _dbContext.EpisodeServiceTasks.Add(initTask);
        await _dbContext.SaveChangesAsync(ct);
    }

    private static EpisodeServiceTaskDto MapToTaskDto(EpisodeServiceTask t, string? performingDentistName = null)
    {
        var steps = t.StepProgresses?
            .OrderBy(p => p.Step?.StepOrder ?? 0)
            .Select(p => new EpisodeTaskStepProgressDto
            {
                ProgressId = p.ProgressId,
                ServiceTaskId = p.ServiceTaskId,
                StepId = p.StepId,
                StepName = p.Step?.StepName ?? string.Empty,
                Description = p.Step?.Description,
                StepOrder = p.Step?.StepOrder ?? 0,
                IsRequired = p.Step?.IsRequired ?? true,
                CompletionRequirement = p.Step?.CompletionRequirement,
                ExpectedDataType = p.Step?.ExpectedDataType ?? "None",
                Status = p.Status,
                ResultValue = p.ResultValue,
                Notes = p.Notes,
                EvidenceUrl = p.EvidenceUrl,
                CompletedByDentistUserId = p.CompletedByDentistUserId,
                CompletedByDentistName = p.CompletedByDentistUser?.StaffProfile?.FullName ?? p.CompletedByDentistUser?.Email,
                StartedAtUtc = p.StartedAtUtc,
                CompletedAtUtc = p.CompletedAtUtc
            }).ToList() ?? new List<EpisodeTaskStepProgressDto>();

        return new EpisodeServiceTaskDto
        {
            ServiceTaskId = t.ServiceTaskId,
            TaskCode = t.TaskCode,
            CareEpisodeId = t.CareEpisodeId,
            AppointmentId = t.AppointmentId,
            AppointmentCode = t.Appointment?.AppointmentCode ?? string.Empty,
            DepartmentServiceId = t.DepartmentServiceId,
            ServiceName = t.DepartmentService?.ServiceName ?? string.Empty,
            DepartmentId = t.DepartmentService?.DepartmentId ?? 0,
            DepartmentName = t.DepartmentService?.Department?.Name ?? string.Empty,
            PerformingDentistUserId = t.PerformingDentistUserId,
            PerformingDentistName = performingDentistName ?? GetUserDisplayName(t.PerformingDentistUser),
            Status = t.Status,
            Quantity = t.Quantity,
            UnitPriceSnapshot = t.UnitPriceSnapshot,
            EstimatedDurationMinutes = t.EstimatedDurationMinutes,
            PlannedAtUtc = t.PlannedAtUtc,
            StartedAtUtc = t.StartedAtUtc,
            CompletedAtUtc = t.CompletedAtUtc,
            CancelledAtUtc = t.CancelledAtUtc,
            CancellationReason = t.CancellationReason,
            ClinicalRecordId = t.ClinicalRecordId,
            AppliedWorkflowId = t.AppliedWorkflowId,
            AppliedWorkflowVersion = t.AppliedWorkflowVersion,
            StepProgresses = steps,
            Notes = t.Notes
        };
    }

    #endregion
}
