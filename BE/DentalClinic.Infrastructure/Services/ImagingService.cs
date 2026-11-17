using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class ImagingService : IImagingService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly ILogger<ImagingService> _logger;
    private readonly INotificationService? _notificationService;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public ImagingService(
        DentalClinicDbContext dbContext,
        ILogger<ImagingService> logger,
        INotificationService? notificationService = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<DiagnosticOrderDto> CreateDiagnosticOrderAsync(CreateDiagnosticOrderRequest request, long dentistUserId, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var appt = await _dbContext.Appointments
            .Include(a => a.Patient)
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

        if (appt == null)
            throw new InvalidOperationException("Không tìm thấy cuộc hẹn lâm sàng.");

        var validStatuses = new[] { AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn, "InProgress", AppointmentStatus.InService };
        if (!validStatuses.Contains(appt.Status))
            throw new InvalidOperationException($"Không thể tạo chỉ định chẩn đoán cho cuộc hẹn ở trạng thái '{appt.Status}'.");

        var normalizedType = request.DiagnosticType?.Trim();
        if (!ImagingDiagnosticType.IsValid(normalizedType))
            throw new InvalidOperationException($"Loại chẩn đoán '{request.DiagnosticType}' không hợp lệ. Hỗ trợ: Intraoral, Panorama, CBCT.");

        if (string.IsNullOrWhiteSpace(request.ClinicalIndication))
            throw new InvalidOperationException("Chỉ định lâm sàng (ClinicalIndication) không được để trống.");

        if (request.IsExternal && string.IsNullOrWhiteSpace(request.ExternalFacilityName))
            throw new InvalidOperationException("Khi chỉ định chụp ngoại viện, bắt buộc phải nhập tên cơ sở thực hiện (ExternalFacilityName).");

        var diagService = await _dbContext.DepartmentServices
            .AsNoTracking()
            .FirstOrDefaultAsync(ds => ds.DiagnosticType == normalizedType && ds.Status == "Active", ct)
            ?? await _dbContext.DepartmentServices.AsNoTracking().FirstOrDefaultAsync(ds => ds.DiagnosticType != null && ds.Status == "Active", ct);

        if (diagService == null)
            throw new InvalidOperationException($"Không tìm thấy dịch vụ chẩn đoán hình ảnh đang hoạt động cho loại '{normalizedType}'.");

        var primaryApptService = appt.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
        if (primaryApptService == null)
        {
            var now = DateTime.UtcNow;
            primaryApptService = new DentalClinic.Infrastructure.Persistence.Entities.AppointmentService
            {
                AppointmentId = appt.AppointmentId,
                DepartmentServiceId = diagService.DepartmentServiceId,
                DepartmentId = diagService.DepartmentId,
                AssignedDentistUserId = dentistUserId > 0 ? dentistUserId : null,
                ServiceDate = DateOnly.FromDateTime(now),
                ScheduledStart = now,
                ScheduledEnd = now.AddMinutes(diagService.DurationMinutes),
                UnitPrice = diagService.Price,
                Status = AppointmentStatus.Confirmed,
                CreatedAt = now
            };
            _dbContext.AppointmentServices.Add(primaryApptService);
            await _dbContext.SaveChangesAsync(ct);
        }

        var effectiveDentistId = dentistUserId > 0
            ? dentistUserId
            : (primaryApptService.AssignedDentistUserId ?? primaryApptService.RequestedDentistUserId ?? dentistUserId);

        var indication = request.IsExternal && !string.IsNullOrWhiteSpace(request.ExternalFacilityName)
            ? $"[External:{request.ExternalFacilityName.Trim()}] {request.ClinicalIndication.Trim()}"
            : request.ClinicalIndication.Trim();

        var diagnostic = new Diagnostic
        {
            AppointmentServiceId = primaryApptService.AppointmentServiceId,
            DepartmentServiceId = diagService.DepartmentServiceId,
            OrderingDentistUserId = effectiveDentistId,
            ClinicalIndication = indication,
            ImagingType = normalizedType,
            Status = "Ordered",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Diagnostics.Add(diagnostic);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created Diagnostic #{DiagnosticId} for Appointment #{ApptId}, Type={Type}",
            diagnostic.DiagnosticId, appt.AppointmentId, diagnostic.ImagingType);

        return await MapToOrderDtoAsync(diagnostic.DiagnosticId, ct);
    }

    public async Task<List<DiagnosticOrderDto>> GetDiagnosticOrdersByAppointmentAsync(long appointmentId, CancellationToken ct = default)
    {
        var diagnostics = await BuildDiagnosticsQuery()
            .AsNoTracking()
            .Where(d => d.AppointmentService.AppointmentId == appointmentId || d.AppointmentServiceId == appointmentId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        return diagnostics.Select(MapOrderToDto).ToList();
    }

    public async Task<DiagnosticOrderDto?> GetDiagnosticOrderByIdAsync(long diagnosticOrderId, CancellationToken ct = default)
    {
        var d = await BuildDiagnosticsQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DiagnosticId == diagnosticOrderId, ct);

        return d == null ? null : MapOrderToDto(d);
    }

    public async Task<List<AvailableSlotDto>> GetAvailableSlotsAsync(long equipmentId, DateTime dateUtc, string diagnosticType, CancellationToken ct = default)
    {
        var roomIdInt = (int)equipmentId;
        var room = await _dbContext.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomId == roomIdInt, ct);

        if (room == null || room.Status != "Active")
        {
            return new List<AvailableSlotDto>();
        }

        var service = await _dbContext.DepartmentServices
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.DiagnosticType == diagnosticType && c.Status == "Active", ct);

        var durationMinutes = service?.DurationMinutes ?? 15;
        var startOfDay = dateUtc.Date.AddHours(7);
        var endOfDay = dateUtc.Date.AddHours(21);

        var existingServices = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(s => s.RoomId == roomIdInt &&
                        s.ScheduledStart < endOfDay &&
                        s.ScheduledEnd > startOfDay &&
                        s.Status != AppointmentStatus.Cancelled)
            .ToListAsync(ct);

        var slotDate = DateOnly.FromDateTime(startOfDay);
        var xraySchedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Where(ws => ws.StaffRole == "Dentist" &&
                        ws.WorkDate == slotDate &&
                        ws.Status == "Scheduled" &&
                        ws.Notes != null &&
                        (ws.Notes.Contains("X-quang") || ws.Notes.Contains("X-ray") || ws.Notes.Contains("R003")))
            .ToListAsync(ct);

        List<StaffLeaveRequest> leaves = new();
        if (xraySchedules.Any())
        {
            var dentistIds = xraySchedules.Select(s => s.StaffUserId).Distinct().ToList();
            leaves = await _dbContext.StaffLeaveRequests
                .AsNoTracking()
                .Where(l => l.StaffRole == "Dentist" &&
                            dentistIds.Contains(l.StaffUserId) &&
                            l.Status == "Approved" &&
                            l.StartDate <= slotDate &&
                            l.EndDate >= slotDate)
                .ToListAsync(ct);
        }

        var slots = new List<AvailableSlotDto>();
        var currentSlotStart = startOfDay;

        while (currentSlotStart.AddMinutes(durationMinutes) <= endOfDay)
        {
            var currentSlotEnd = currentSlotStart.AddMinutes(durationMinutes);
            var conflict = existingServices.FirstOrDefault(s =>
                s.ScheduledStart < currentSlotEnd && s.ScheduledEnd > currentSlotStart);

            bool hasStaffCoverage = true;
            string? staffingReason = null;
            if (xraySchedules.Any())
            {
                var slotStartTime = TimeOnly.FromDateTime(currentSlotStart);
                var slotEndTime = TimeOnly.FromDateTime(currentSlotEnd);

                var availableDentists = xraySchedules
                    .Where(ws => ws.StartTime < slotEndTime && ws.EndTime > slotStartTime)
                    .Select(ws => ws.StaffUserId)
                    .Distinct()
                    .Where(dId => !leaves.Any(l => l.StaffUserId == dId &&
                        (!l.StartTime.HasValue || !l.EndTime.HasValue ||
                         (l.StartTime.Value < slotEndTime && l.EndTime.Value > slotStartTime))))
                    .ToList();

                if (!availableDentists.Any())
                {
                    hasStaffCoverage = false;
                    staffingReason = "Phòng chụp X-quang không có bác sĩ trực khả dụng trong khung giờ này.";
                }
            }

            var isSlotAvailable = conflict == null && hasStaffCoverage;
            var reason = conflict != null 
                ? $"Phòng đang bận cho ca #{conflict.AppointmentServiceId}" 
                : staffingReason;

            slots.Add(new AvailableSlotDto
            {
                StartUtc = currentSlotStart,
                EndUtc = currentSlotEnd,
                IsAvailable = isSlotAvailable,
                ConflictReason = isSlotAvailable ? null : reason
            });

            currentSlotStart = currentSlotStart.AddMinutes(durationMinutes);
        }

        return slots;
    }

    public async Task<ImagingSessionDto> BookImagingSessionAsync(BookImagingSessionRequest request, long createdByUserId, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var diagnostic = await BuildDiagnosticsQuery()
            .FirstOrDefaultAsync(d => d.DiagnosticId == request.DiagnosticOrderId, ct);

        if (diagnostic == null)
            throw new InvalidOperationException("Không tìm thấy chỉ định chẩn đoán hình ảnh.");

        if (diagnostic.ClinicalIndication.StartsWith("[External:", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ định chụp ngoại viện không thể đặt lịch phiên chụp nội bộ.");

        if (diagnostic.Status == "Cancelled" || diagnostic.Status == "Completed")
            throw new InvalidOperationException($"Không thể đặt lịch cho chỉ định ở trạng thái '{diagnostic.Status}'.");

        var durationMinutes = diagnostic.DepartmentService?.DurationMinutes ?? 15;
        var localStart = ClinicTimeHelper.ToClinicTime(request.ScheduledStartUtc);
        var utcStart = ClinicTimeHelper.ToUtc(request.ScheduledStartUtc);
        var reqDate = DateOnly.FromDateTime(localStart);
        var reqStart = TimeOnly.FromDateTime(localStart);
        var reqEnd = TimeOnly.FromDateTime(localStart.AddMinutes(durationMinutes));
        var utcEnd = utcStart.AddMinutes(durationMinutes);

        Room? imagingRoom = null;
        if (request.EquipmentId > 0)
        {
            imagingRoom = await _dbContext.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.RoomId == (int)request.EquipmentId, ct);
            if (imagingRoom == null || imagingRoom.Status != "Active")
            {
                throw new InvalidOperationException($"Phòng/Thiết bị X-quang #{request.EquipmentId} không tồn tại hoặc đang không hoạt động.");
            }
            if (!string.Equals(imagingRoom.RoomType, "Imaging", StringComparison.OrdinalIgnoreCase))
            {
                var isImagingDept = await _dbContext.Departments.AsNoTracking()
                    .AnyAsync(d => d.DepartmentId == imagingRoom.DepartmentId && (d.Name.Contains("Chẩn đoán hình ảnh") || d.Name.Contains("X-quang") || d.Name.Contains("Imaging")), ct);
                if (!isImagingDept)
                {
                    throw new InvalidOperationException($"Phòng '{imagingRoom.RoomName}' ({imagingRoom.RoomCode}) không phải phòng chẩn đoán hình ảnh chuyên dụng.");
                }
            }
        }

        DentistProfile? dentist = null;
        if (request.AssignedDentistUserId.HasValue && request.AssignedDentistUserId.Value > 0)
        {
            dentist = await _dbContext.DentistProfiles
                .AsNoTracking()
                .Include(d => d.StaffProfile)
                .FirstOrDefaultAsync(d => d.UserId == request.AssignedDentistUserId.Value, ct);

            if (dentist == null || dentist.StaffProfile?.EmploymentStatus != "Active")
                throw new InvalidOperationException("Bác sĩ X-quang được chỉ định không tồn tại hoặc đã ngừng hoạt động.");

            var isOnLeave = await _dbContext.StaffLeaveRequests
                .AsNoTracking()
                .AnyAsync(l => l.StaffRole == "Dentist" &&
                               l.StaffUserId == request.AssignedDentistUserId.Value &&
                               l.Status == "Approved" &&
                               l.StartDate <= reqDate &&
                               l.EndDate >= reqDate &&
                               (!l.StartTime.HasValue || !l.EndTime.HasValue ||
                                (l.StartTime.Value < reqEnd && l.EndTime.Value > reqStart)), ct);

            if (isOnLeave)
            {
                throw new InvalidOperationException($"Không thể phân bổ ca chụp cho Bác sĩ '{dentist.StaffProfile?.FullName ?? "X-quang"}': Bác sĩ đang trong thời gian nghỉ phép.");
            }

            var anySchedulesOnDate = await _dbContext.StaffWorkSchedules
                .AsNoTracking()
                .AnyAsync(ws => ws.WorkDate == reqDate && ws.Status == "Scheduled", ct);

            if (anySchedulesOnDate)
            {
                var hasShift = await _dbContext.StaffWorkSchedules
                    .AsNoTracking()
                    .AnyAsync(ws => ws.StaffRole == "Dentist" &&
                                    ws.StaffUserId == request.AssignedDentistUserId.Value &&
                                    ws.WorkDate == reqDate &&
                                    ws.Status == "Scheduled" &&
                                    ws.StartTime <= reqStart &&
                                    ws.EndTime >= reqEnd, ct);

                if (!hasShift)
                {
                    throw new InvalidOperationException($"Bác sĩ '{dentist.StaffProfile?.FullName ?? "X-quang"}' không có ca trực trong khung giờ {reqStart:HH\\:mm} - {reqEnd:HH\\:mm} ngày {reqDate:dd/MM/yyyy} (Giờ Việt Nam).");
                }
            }
        }

        // CRITICAL RESOURCE INTEGRITY:
        // When the parent service is a chair-based clinical consultation (DentalChairId is set),
        // do NOT mutate RoomId, DentalChairId, or AssignedDentistUserId to preserve consultation integrity
        // and avoid FK_AppointmentServices_Chair_Room violation on SQL Server.
        // For non-chair services, synchronize the assigned imaging doctor.
        if (!diagnostic.AppointmentService.DentalChairId.HasValue && dentist != null)
        {
            diagnostic.AppointmentService.AssignedDentistUserId = dentist.UserId;
        }

        // Session resource allocations are safely recorded in clinical diagnostic metadata tags.
        var indication = diagnostic.ClinicalIndication ?? string.Empty;
        if (imagingRoom != null)
        {
            indication = UpdateMetadataTag(indication, "Room", imagingRoom.RoomId.ToString());
            indication = UpdateMetadataTag(indication, "RoomCode", imagingRoom.RoomCode);
            indication = UpdateMetadataTag(indication, "RoomName", imagingRoom.RoomName);
        }
        if (dentist != null)
        {
            indication = UpdateMetadataTag(indication, "Dentist", dentist.UserId.ToString());
            indication = UpdateMetadataTag(indication, "DentistName", dentist.StaffProfile?.FullName ?? $"Bác sĩ #{dentist.UserId}");
        }
        indication = UpdateMetadataTag(indication, "Slot", utcStart.ToString("O"));
        indication = UpdateMetadataTag(indication, "EndSlot", utcEnd.ToString("O"));
        diagnostic.ClinicalIndication = indication;

        diagnostic.AppointmentService.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return MapSessionToDto(diagnostic);
    }

    public async Task<ImagingSessionDto> CheckInImagingSessionAsync(long imagingSessionId, CancellationToken ct = default)
    {
        var diagnostic = await BuildDiagnosticsQuery()
            .FirstOrDefaultAsync(d => d.DiagnosticId == imagingSessionId || d.AppointmentServiceId == imagingSessionId, ct);

        if (diagnostic == null)
            throw new InvalidOperationException("Không tìm thấy phiên chụp X-quang.");

        var existingQueue = GetMetadataTag(diagnostic.ClinicalIndication, "Queue");
        if (string.IsNullOrEmpty(existingQueue))
        {
            var maxQueue = await _dbContext.Diagnostics
                .AsNoTracking()
                .Where(d => d.DepartmentServiceId == diagnostic.DepartmentServiceId &&
                            d.CreatedAt.Date == DateTime.UtcNow.Date)
                .CountAsync(ct);

            var nextQueue = maxQueue + 1;
            diagnostic.ClinicalIndication = UpdateMetadataTag(diagnostic.ClinicalIndication, "Queue", nextQueue.ToString());
            await _dbContext.SaveChangesAsync(ct);
        }

        return MapSessionToDto(diagnostic);
    }

    public async Task<ImagingSessionDto> StartImagingSessionAsync(long imagingSessionId, long operatorUserId, CancellationToken ct = default)
    {
        var diagnostic = await BuildDiagnosticsQuery()
            .FirstOrDefaultAsync(d => d.DiagnosticId == imagingSessionId || d.AppointmentServiceId == imagingSessionId, ct);

        if (diagnostic == null)
            throw new InvalidOperationException("Không tìm thấy phiên chụp X-quang.");

        if (string.Equals(diagnostic.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Phiên chụp X-quang đang được thực hiện bởi một bác sĩ khác.");

        if (string.Equals(diagnostic.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Phiên chụp X-quang đã hoàn thành.");

        if (string.Equals(diagnostic.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Phiên chụp X-quang đã bị hủy.");

        diagnostic.Status = "InProgress";
        await _dbContext.SaveChangesAsync(ct);

        return MapSessionToDto(diagnostic);
    }

    public async Task<ImagingSessionDto> AcquireImageAsync(long imagingSessionId, CancellationToken ct = default)
    {
        var diagnostic = await BuildDiagnosticsQuery()
            .FirstOrDefaultAsync(d => d.DiagnosticId == imagingSessionId || d.AppointmentServiceId == imagingSessionId, ct);

        if (diagnostic == null)
            throw new InvalidOperationException("Không tìm thấy phiên chụp X-quang.");

        diagnostic.Status = "InProgress";
        await _dbContext.SaveChangesAsync(ct);

        return MapSessionToDto(diagnostic);
    }

    public async Task<DiagnosticResultDto> UploadDiagnosticResultAsync(UploadDiagnosticResultRequest request, long uploadedByUserId, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var diagnostic = await BuildDiagnosticsQuery()
            .FirstOrDefaultAsync(d => d.DiagnosticId == request.DiagnosticOrderId || d.DiagnosticId == request.ImagingSessionId, ct);

        if (diagnostic == null)
            throw new InvalidOperationException("Không tìm thấy chỉ định chẩn đoán hình ảnh.");

        if (diagnostic.Status == "Cancelled")
            throw new InvalidOperationException("Chỉ định chẩn đoán hình ảnh đã bị hủy. Không thể tải lên kết quả.");

        if (diagnostic.Status == "Completed")
            throw new InvalidOperationException("Chỉ định chẩn đoán hình ảnh đã hoàn tất kết quả. Không thể ghi đè kết quả đã khóa mà không có quy trình hiệu chỉnh (amendment).");

        if (string.IsNullOrWhiteSpace(request.FileUrl))
            throw new InvalidOperationException("Đường dẫn tệp hình ảnh (FileUrl) không được để trống.");

        diagnostic.FileUrl = request.FileUrl.Trim();
        diagnostic.ThumbnailUrl = request.ThumbnailUrl?.Trim();
        diagnostic.Findings = request.Findings?.Trim();
        diagnostic.UploadedByUserId = uploadedByUserId;
        diagnostic.Status = "Completed";
        diagnostic.CompletedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        if (_notificationService != null && diagnostic.OrderingDentistUserId > 0)
        {
            try
            {
                var appt = diagnostic.AppointmentService?.Appointment;
                var patientName = !string.IsNullOrWhiteSpace(appt?.Patient?.FullName) ? appt.Patient.FullName : (appt?.Patient?.User?.PhoneNumber ?? $"Bệnh nhân #{appt?.PatientUserId}");
                var apptId = appt?.AppointmentId ?? diagnostic.AppointmentServiceId;

                await _notificationService.CreateNotificationAsync(
                    diagnostic.OrderingDentistUserId,
                    apptId,
                    "XrayResultUploaded",
                    "Kết quả chụp X-quang đã hoàn tất",
                    $"Kết quả chụp X-quang ({diagnostic.ImagingType ?? "X-quang"}) của bệnh nhân {patientName} (Ca khám #{apptId}) đã hoàn tất. Vui lòng kiểm tra trên hồ sơ bệnh án.",
                    ct);
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "Failed to send notification to ordering dentist {DentistId}", diagnostic.OrderingDentistUserId);
            }
        }

        return MapResultToDto(diagnostic);
    }

    public async Task<ImagingSessionDto> CompleteImagingSessionAsync(long imagingSessionId, CancellationToken ct = default)
    {
        var diagnostic = await BuildDiagnosticsQuery()
            .FirstOrDefaultAsync(d => d.DiagnosticId == imagingSessionId || d.AppointmentServiceId == imagingSessionId, ct);

        if (diagnostic == null)
            throw new InvalidOperationException("Không tìm thấy phiên chụp X-quang.");

        diagnostic.Status = "Completed";
        diagnostic.CompletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return MapSessionToDto(diagnostic);
    }

    public async Task<DiagnosticResultDto> ReviewDiagnosticResultAsync(long diagnosticResultId, ReviewDiagnosticResultRequest request, long dentistUserId, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var dentist = await _dbContext.DentistProfiles.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);
        if (dentist == null)
            throw new UnauthorizedAccessException("Chỉ bác sĩ điều trị mới có quyền duyệt kết quả chẩn đoán hình ảnh.");

        var diagnostic = await BuildDiagnosticsQuery()
            .FirstOrDefaultAsync(d => d.DiagnosticId == diagnosticResultId, ct);

        if (diagnostic == null)
            throw new InvalidOperationException("Không tìm thấy kết quả chẩn đoán hình ảnh.");

        var appt = diagnostic.AppointmentService?.Appointment;
        var isOrderingDentist = diagnostic.OrderingDentistUserId == dentistUserId;
        var isAssignedDentist = diagnostic.AppointmentService?.AssignedDentistUserId == dentistUserId ||
                                appt?.AppointmentServices.Any(s => s.AssignedDentistUserId == dentistUserId) == true;

        if (!isOrderingDentist && !isAssignedDentist && dentistUserId > 0)
        {
            var hasConsultation = await _dbContext.DepartmentReferrals
                .AsNoTracking()
                .AnyAsync(r => (r.AssignedDentistUserId == dentistUserId || r.ReviewedByUserId == dentistUserId) &&
                               (r.SourceAppointmentServiceId == diagnostic.AppointmentServiceId ||
                                r.DestinationAppointmentServiceId == diagnostic.AppointmentServiceId), ct);

            if (!hasConsultation)
            {
                throw new UnauthorizedAccessException("Chỉ bác sĩ chỉ định hoặc bác sĩ điều trị phụ trách ca khám mới có quyền đánh giá duyệt kết quả chẩn đoán.");
            }
        }

        if (string.IsNullOrWhiteSpace(request.ReviewNotes))
            throw new InvalidOperationException("Bác sĩ phải nhập nhận xét lâm sàng (ReviewNotes) khi đánh giá kết quả.");

        diagnostic.ReviewedByDentistUserId = dentistUserId;
        diagnostic.ReviewedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Findings))
        {
            diagnostic.Findings = $"{request.Findings.Trim()} | [Nhận xét]: {request.ReviewNotes.Trim()}";
        }
        else
        {
            diagnostic.Findings = string.IsNullOrWhiteSpace(diagnostic.Findings)
                ? request.ReviewNotes.Trim()
                : $"{diagnostic.Findings} | [Nhận xét]: {request.ReviewNotes.Trim()}";
        }

        await _dbContext.SaveChangesAsync(ct);

        var refreshed = await BuildDiagnosticsQuery()
            .AsNoTracking()
            .FirstAsync(d => d.DiagnosticId == diagnostic.DiagnosticId, ct);
        return MapResultToDto(refreshed);
    }

    public async Task<ImagingSessionDto> CancelImagingSessionAsync(long imagingSessionId, string reason, long cancelledByUserId, CancellationToken ct = default)
    {
        var diagnostic = await BuildDiagnosticsQuery()
            .FirstOrDefaultAsync(d => d.DiagnosticId == imagingSessionId || d.AppointmentServiceId == imagingSessionId, ct);

        if (diagnostic == null)
            throw new InvalidOperationException("Không tìm thấy phiên chụp X-quang.");

        if (diagnostic.Status == "Completed")
            throw new InvalidOperationException("Không thể hủy phiên chụp đã hoàn tất.");

        diagnostic.Status = "Cancelled";
        diagnostic.Findings = string.IsNullOrWhiteSpace(diagnostic.Findings)
            ? $"Lý do hủy: {reason}"
            : $"{diagnostic.Findings} | Lý do hủy: {reason}";

        await _dbContext.SaveChangesAsync(ct);
        return MapSessionToDto(diagnostic);
    }

    public async Task<ImagingQueueSummaryDto> GetImagingQueueStatusAsync(long roomId, CancellationToken ct = default)
    {
        var roomIdInt = (int)roomId;
        var room = await _dbContext.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomId == roomIdInt, ct);

        if (room == null)
            throw new InvalidOperationException("Không tìm thấy phòng chụp.");

        var diagnosticsInRoom = await BuildDiagnosticsQuery()
            .AsNoTracking()
            .Where(d => d.AppointmentService.RoomId == roomIdInt || 
                        d.DepartmentService.DepartmentId == room.DepartmentId ||
                        d.ClinicalIndication.Contains($"[Room:{roomIdInt}]"))
            .ToListAsync(ct);

        var activeSessions = diagnosticsInRoom.Where(d => d.Status == "InProgress").Select(MapSessionToDto).ToList();
        var waitingQueue = diagnosticsInRoom.Where(d => d.Status == "Ordered").Select(MapSessionToDto).ToList();
        var estimatedNextWait = (activeSessions.Count * 10) + (waitingQueue.Count * 15);

        return new ImagingQueueSummaryDto
        {
            RoomId = room.RoomId,
            RoomCode = room.RoomCode,
            RoomName = room.RoomName,
            CurrentInProgressCount = activeSessions.Count,
            CurrentWaitingCount = waitingQueue.Count,
            MaxWaitingPatients = 10,
            TargetMaxWaitMinutes = 30,
            EscalationWaitMinutes = 45,
            EstimatedNextWaitMinutes = estimatedNextWait,
            HasDelayWarning = estimatedNextWait >= 30,
            HasEscalationAlert = estimatedNextWait >= 45,
            ActiveSessions = activeSessions,
            WaitingQueue = waitingQueue,
            HasOnDutyDentist = true,
            IsDentistOnLeave = false
        };
    }

    public async Task<List<ImagingEquipmentDto>> GetAllEquipmentsAsync(CancellationToken ct = default)
    {
        var rooms = await _dbContext.Rooms
            .AsNoTracking()
            .Include(r => r.Department)
            .OrderBy(r => r.RoomCode)
            .ToListAsync(ct);

        return rooms.Select(r => new ImagingEquipmentDto
        {
            EquipmentId = r.RoomId,
            RoomId = r.RoomId,
            RoomCode = r.RoomCode,
            RoomName = r.RoomName,
            EquipmentCode = r.RoomCode,
            EquipmentName = r.RoomName,
            EquipmentType = r.RoomType,
            SupportedDiagnosticTypes = "Intraoral,Panorama,CBCT",
            IsActive = r.Status == "Active",
            OperationalStatus = r.Status == "Active" ? EquipmentOperationalStatus.Operational : r.Status,
            AvailableFrom = new TimeSpan(7, 0, 0),
            AvailableTo = new TimeSpan(21, 0, 0)
        }).ToList();
    }

    public async Task<ImagingEquipmentDto> UpdateEquipmentStatusAsync(long equipmentId, string operationalStatus, CancellationToken ct = default)
    {
        var roomIdInt = (int)equipmentId;
        var room = await _dbContext.Rooms
            .FirstOrDefaultAsync(r => r.RoomId == roomIdInt, ct);

        if (room == null)
            throw new InvalidOperationException("Không tìm thấy phòng/thiết bị chụp X-quang.");

        room.Status = operationalStatus == EquipmentOperationalStatus.Operational ? "Active" : "Maintenance";
        await _dbContext.SaveChangesAsync(ct);

        return new ImagingEquipmentDto
        {
            EquipmentId = room.RoomId,
            RoomId = room.RoomId,
            RoomCode = room.RoomCode,
            RoomName = room.RoomName,
            EquipmentCode = room.RoomCode,
            EquipmentName = room.RoomName,
            EquipmentType = room.RoomType,
            SupportedDiagnosticTypes = "Intraoral,Panorama,CBCT",
            IsActive = room.Status == "Active",
            OperationalStatus = operationalStatus,
            AvailableFrom = new TimeSpan(7, 0, 0),
            AvailableTo = new TimeSpan(21, 0, 0)
        };
    }

    public async Task<List<ImagingServiceConfigDto>> GetAllServiceConfigsAsync(CancellationToken ct = default)
    {
        var list = await _dbContext.DepartmentServices
            .AsNoTracking()
            .Where(s => s.DiagnosticType != null)
            .OrderBy(s => s.DepartmentServiceId)
            .ToListAsync(ct);

        return list.Select(c => new ImagingServiceConfigDto
        {
            ConfigId = c.DepartmentServiceId,
            DiagnosticType = c.DiagnosticType ?? "Intraoral",
            ServiceName = c.ServiceName,
            SlotDurationMinutes = c.DurationMinutes,
            BufferMinutes = 0,
            Price = c.Price,
            IsActive = c.Status == "Active"
        }).ToList();
    }

    public async Task<ImagingServiceConfigDto> UpdateServiceConfigAsync(long configId, UpdateImagingConfigRequest request, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var cfg = await _dbContext.DepartmentServices
            .FirstOrDefaultAsync(c => c.DepartmentServiceId == (int)configId, ct);

        if (cfg == null)
            throw new InvalidOperationException("Không tìm thấy cấu hình dịch vụ chẩn đoán hình ảnh.");

        if (request.SlotDurationMinutes <= 0)
            throw new InvalidOperationException("Thời lượng slot phải lớn hơn 0 phút.");

        cfg.DurationMinutes = request.SlotDurationMinutes;
        cfg.Price = Math.Max(0, request.Price);
        cfg.Status = request.IsActive ? "Active" : "Inactive";
        cfg.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return new ImagingServiceConfigDto
        {
            ConfigId = cfg.DepartmentServiceId,
            DiagnosticType = cfg.DiagnosticType ?? "Intraoral",
            ServiceName = cfg.ServiceName,
            SlotDurationMinutes = cfg.DurationMinutes,
            BufferMinutes = 0,
            Price = cfg.Price,
            IsActive = cfg.Status == "Active"
        };
    }

    public async Task<ClinicalConsultationDto> CreateConsultationAsync(CreateConsultationRequest request, long dentistUserId, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.SourceAppointmentId <= 0)
            throw new ArgumentException("Mã ca khám (SourceAppointmentId) không hợp lệ.");
        if (request.ConsultedDentistUserId <= 0)
            throw new ArgumentException("Vui lòng chọn bác sĩ cần hội chẩn.");
        if (string.IsNullOrWhiteSpace(request.ConsultationReason))
            throw new ArgumentException("Lý do hội chẩn không được để trống.");
        if (string.IsNullOrWhiteSpace(request.ClinicalQuestion))
            throw new ArgumentException("Câu hỏi lâm sàng không được để trống.");

        var appt = await _dbContext.Appointments
            .Include(a => a.Patient)
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.SourceAppointmentId, ct);
        if (appt == null)
            throw new InvalidOperationException("Không tìm thấy ca khám nguồn.");

        var consultedDentist = await _dbContext.DentistProfiles
            .Include(d => d.StaffProfile)
            .FirstOrDefaultAsync(d => d.UserId == request.ConsultedDentistUserId, ct);
        if (consultedDentist == null || consultedDentist.StaffProfile.EmploymentStatus != "Active")
            throw new InvalidOperationException("Bác sĩ được mời hội chẩn không tồn tại hoặc đã ngừng hoạt động.");

        if (request.ConsultedDentistUserId == dentistUserId)
            throw new InvalidOperationException("Không thể gửi yêu cầu hội chẩn cho chính mình.");

        var requestingDentist = await _dbContext.DentistProfiles
            .Include(d => d.StaffProfile)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        var sourceApptService = appt.AppointmentServices.FirstOrDefault();
        var fromDeptId = requestingDentist?.DepartmentId ?? sourceApptService?.DepartmentId ?? consultedDentist.DepartmentId;
        var toDeptId = consultedDentist.DepartmentId;
        if (fromDeptId == toDeptId)
        {
            var altDept = await _dbContext.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.DepartmentId != fromDeptId, ct);
            if (altDept != null) fromDeptId = altDept.DepartmentId;
        }

        var referral = new DepartmentReferral
        {
            FromDepartmentId = fromDeptId,
            ToDepartmentId = toDeptId,
            AssignedDentistUserId = request.ConsultedDentistUserId,
            ReviewedByUserId = dentistUserId,
            Reason = request.ConsultationReason.Trim(),
            ClinicalInstructions = request.ClinicalQuestion.Trim(),
            Urgency = string.Equals(request.Urgency, "Urgent", StringComparison.OrdinalIgnoreCase) ? "Urgent" : "Normal",
            Status = "Pending",
            SourceAppointmentServiceId = sourceApptService?.AppointmentServiceId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DepartmentReferrals.Add(referral);
        await _dbContext.SaveChangesAsync(ct);

        return await MapReferralToConsultationDtoAsync(referral.ReferralId, ct)
            ?? throw new InvalidOperationException("Không thể tải thông tin hội chẩn.");
    }

    public async Task<ClinicalConsultationDto> RespondConsultationAsync(long consultationId, RespondConsultationRequest request, long dentistUserId, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.DentistOpinion))
            throw new ArgumentException("Ý kiến chuyên môn không được để trống.");

        var referral = await _dbContext.DepartmentReferrals
            .FirstOrDefaultAsync(r => r.ReferralId == consultationId, ct);

        if (referral == null)
            throw new InvalidOperationException("Không tìm thấy yêu cầu hội chẩn.");

        if (referral.AssignedDentistUserId != dentistUserId)
            throw new UnauthorizedAccessException("Chỉ bác sĩ được mời hội chẩn mới có quyền phản hồi yêu cầu này.");

        if (referral.Status != "Pending")
            throw new InvalidOperationException($"Yêu cầu hội chẩn đã ở trạng thái '{referral.Status}', không thể phản hồi lại.");

        referral.ClinicalInstructions = string.IsNullOrWhiteSpace(request.Recommendations)
            ? $"{referral.ClinicalInstructions} || [Opinion]: {request.DentistOpinion.Trim()}"
            : $"{referral.ClinicalInstructions} || [Opinion]: {request.DentistOpinion.Trim()} || [Rec]: {request.Recommendations.Trim()}";
        referral.Status = "Completed";
        referral.ReviewedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return await MapReferralToConsultationDtoAsync(referral.ReferralId, ct)
            ?? throw new InvalidOperationException("Không thể tải thông tin hội chẩn.");
    }

    public async Task<List<ClinicalConsultationDto>> GetConsultationsByDentistAsync(long dentistUserId, string type, CancellationToken ct = default)
    {
        var query = _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(r => r.SourceAppointmentService)
                .ThenInclude(s => s!.Appointment)
                    .ThenInclude(a => a.Patient)
            .Include(r => r.AssignedDentist)
                .ThenInclude(d => d!.StaffProfile)
            .Include(r => r.ReviewedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .AsQueryable();

        if (string.Equals(type, "requested", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.ReviewedByUserId == dentistUserId);
        }
        else if (string.Equals(type, "received", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.AssignedDentistUserId == dentistUserId);
        }
        else
        {
            query = query.Where(r => r.ReviewedByUserId == dentistUserId || r.AssignedDentistUserId == dentistUserId);
        }

        var list = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return list.Select(MapReferralToConsultationDto).ToList();
    }

    public async Task<List<ClinicalConsultationDto>> GetConsultationsByAppointmentAsync(long appointmentId, CancellationToken ct = default)
    {
        var list = await _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(r => r.SourceAppointmentService)
                .ThenInclude(s => s!.Appointment)
                    .ThenInclude(a => a.Patient)
            .Include(r => r.AssignedDentist)
                .ThenInclude(d => d!.StaffProfile)
            .Include(r => r.ReviewedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .Where(r => r.SourceAppointmentService != null && r.SourceAppointmentService.AppointmentId == appointmentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return list.Select(MapReferralToConsultationDto).ToList();
    }

    public async Task<ClinicalConsultationDto?> GetConsultationByIdAsync(long consultationId, CancellationToken ct = default)
    {
        return await MapReferralToConsultationDtoAsync(consultationId, ct);
    }

    private IQueryable<Diagnostic> BuildDiagnosticsQuery()
    {
        return _dbContext.Diagnostics
            .Include(d => d.AppointmentService)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.Patient)
            .Include(d => d.AppointmentService)
                .ThenInclude(s => s.Room)
            .Include(d => d.AppointmentService)
                .ThenInclude(s => s.AssignedDentist)
                    .ThenInclude(dp => dp!.StaffProfile)
            .Include(d => d.DepartmentService)
            .Include(d => d.OrderingDentist)
                .ThenInclude(dp => dp.StaffProfile)
            .Include(d => d.ReviewedByDentist)
                .ThenInclude(dp => dp!.StaffProfile);
    }

    private async Task<DiagnosticOrderDto> MapToOrderDtoAsync(long diagnosticId, CancellationToken ct)
    {
        var refreshed = await BuildDiagnosticsQuery()
            .AsNoTracking()
            .FirstAsync(d => d.DiagnosticId == diagnosticId, ct);

        return MapOrderToDto(refreshed);
    }

    private static DiagnosticOrderDto MapOrderToDto(Diagnostic d)
    {
        var appt = d.AppointmentService?.Appointment;
        var patient = appt?.Patient;
        var isExt = d.ClinicalIndication.StartsWith("[External:", StringComparison.OrdinalIgnoreCase);
        var sessions = new List<ImagingSessionDto> { MapSessionToDto(d) };
        var results = !string.IsNullOrWhiteSpace(d.FileUrl)
            ? new List<DiagnosticResultDto> { MapResultToDto(d) }
            : new List<DiagnosticResultDto>();

        return new DiagnosticOrderDto
        {
            DiagnosticOrderId = d.DiagnosticId,
            AppointmentId = d.AppointmentService?.AppointmentId ?? d.AppointmentServiceId,
            PatientUserId = appt?.PatientUserId ?? 0,
            PatientName = patient?.FullName ?? $"Bệnh nhân #{appt?.PatientUserId}",
            PatientCode = $"PAT-{appt?.PatientUserId:D4}",
            DentistUserId = d.OrderingDentistUserId,
            DentistName = d.OrderingDentist?.StaffProfile?.FullName ?? $"Bác sĩ #{d.OrderingDentistUserId}",
            DiagnosticType = d.ImagingType ?? d.DepartmentService?.DiagnosticType ?? "Intraoral",
            DiagnosticTypeName = ImagingDiagnosticType.GetDisplayName(d.ImagingType ?? d.DepartmentService?.DiagnosticType ?? "Intraoral"),
            ClinicalIndication = ExtractMedicalIndication(d.ClinicalIndication),
            IsExternal = isExt,
            ExternalFacilityName = null,
            Status = d.Status,
            BilledAtUtc = d.CompletedAt,
            InvoiceItemId = null,
            CreatedAtUtc = d.CreatedAt,
            Sessions = sessions,
            Results = results
        };
    }

    public static string ExtractMedicalIndication(string? indication)
    {
        if (string.IsNullOrWhiteSpace(indication)) return string.Empty;
        var clean = Regex.Replace(indication, @"\[(Room|RoomCode|RoomName|Dentist|DentistName|Slot|EndSlot|Queue):[^\]]*\]\s*", string.Empty).Trim();
        return clean;
    }

    private static string UpdateMetadataTag(string? source, string key, string value)
    {
        var text = source ?? string.Empty;
        var tagPattern = $@"\[{key}:[^\]]*\]";
        var newTag = $"[{key}:{value}]";
        if (Regex.IsMatch(text, tagPattern))
        {
            return Regex.Replace(text, tagPattern, newTag);
        }
        return $"{newTag} {text}".Trim();
    }

    private static string? GetMetadataTag(string? source, string key)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        var match = Regex.Match(source, $@"\[{key}:([^\]]+)\]");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static ImagingSessionDto MapSessionToDto(Diagnostic d)
    {
        var s = d.AppointmentService;
        var appt = s?.Appointment;
        var room = s?.Room;

        var roomTag = GetMetadataTag(d.ClinicalIndication, "Room");
        var roomCodeTag = GetMetadataTag(d.ClinicalIndication, "RoomCode");
        var roomNameTag = GetMetadataTag(d.ClinicalIndication, "RoomName");
        int roomId = room?.RoomId ?? 0;
        string roomCode = room?.RoomCode ?? "XRAY";
        string roomName = room?.RoomName ?? "Phòng Chẩn đoán hình ảnh";
        if (!string.IsNullOrEmpty(roomTag) && int.TryParse(roomTag, out var parsedRoomId) && parsedRoomId > 0)
        {
            roomId = parsedRoomId;
            roomCode = !string.IsNullOrWhiteSpace(roomCodeTag) ? roomCodeTag : (parsedRoomId == 5 ? "R003" : $"ROOM-{parsedRoomId}");
            roomName = !string.IsNullOrWhiteSpace(roomNameTag) ? roomNameTag : (parsedRoomId == 5 ? "Phòng X-quang" : $"Phòng #{parsedRoomId}");
        }

        var dentistTag = GetMetadataTag(d.ClinicalIndication, "Dentist");
        var dentistNameTag = GetMetadataTag(d.ClinicalIndication, "DentistName");
        long? assignedDentistId = s?.AssignedDentistUserId;
        string? assignedDentistName = s?.AssignedDentist?.StaffProfile?.FullName;
        if (!string.IsNullOrEmpty(dentistTag) && long.TryParse(dentistTag, out var parsedDentistId) && parsedDentistId > 0)
        {
            assignedDentistId = parsedDentistId;
            assignedDentistName = !string.IsNullOrWhiteSpace(dentistNameTag) ? dentistNameTag : (parsedDentistId == 9 ? "BS. Tran Hung" : $"Bác sĩ #{parsedDentistId}");
        }

        var slotTag = GetMetadataTag(d.ClinicalIndication, "Slot");
        var endSlotTag = GetMetadataTag(d.ClinicalIndication, "EndSlot");
        var scheduledStart = s?.ScheduledStart ?? d.CreatedAt;
        var scheduledEnd = s?.ScheduledEnd ?? d.CreatedAt.AddMinutes(15);
        if (!string.IsNullOrEmpty(slotTag) && DateTime.TryParse(slotTag, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedSlot))
        {
            scheduledStart = parsedSlot;
        }
        if (!string.IsNullOrEmpty(endSlotTag) && DateTime.TryParse(endSlotTag, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedEndSlot))
        {
            scheduledEnd = parsedEndSlot;
        }

        var queueTag = GetMetadataTag(d.ClinicalIndication, "Queue");
        string? queueNumber = s?.QueueNumber.HasValue == true ? $"XR-{s.QueueNumber.Value:D3}" : null;
        if (!string.IsNullOrEmpty(queueTag) && int.TryParse(queueTag, out var qNum))
        {
            queueNumber = $"XR-{qNum:D3}";
        }

        return new ImagingSessionDto
        {
            ImagingSessionId = d.DiagnosticId,
            DiagnosticOrderId = d.DiagnosticId,
            SourceAppointmentId = s?.AppointmentId ?? d.AppointmentServiceId,
            PatientUserId = appt?.PatientUserId ?? 0,
            PatientName = appt?.Patient?.FullName ?? $"Bệnh nhân #{appt?.PatientUserId}",
            PatientCode = $"PAT-{appt?.PatientUserId:D4}",
            ImagingRoomId = roomId,
            RoomCode = roomCode,
            RoomName = roomName,
            EquipmentId = roomId,
            EquipmentName = roomName,
            DiagnosticType = d.ImagingType ?? d.DepartmentService?.DiagnosticType ?? "Intraoral",
            SlotDurationMinutes = d.DepartmentService?.DurationMinutes ?? 15,
            ScheduledStartUtc = scheduledStart,
            ScheduledEndUtc = scheduledEnd,
            ActualStartUtc = d.CreatedAt,
            ActualEndUtc = d.CompletedAt,
            QueueNumber = queueNumber,
            AssignedDentistUserId = assignedDentistId,
            AssignedDentistName = assignedDentistName,
            Priority = ImagingSessionPriority.Normal,
            Status = d.Status,
            CheckInTimeUtc = d.CreatedAt,
            EstimatedWaitMinutes = 0,
            DelayNotificationSent = false,
            EscalationAlertSent = false,
            Notes = s?.Notes,
            CreatedAtUtc = d.CreatedAt
        };
    }

    private static DiagnosticResultDto MapResultToDto(Diagnostic d)
    {
        var roomTag = GetMetadataTag(d.ClinicalIndication, "Room");
        var roomNameTag = GetMetadataTag(d.ClinicalIndication, "RoomName");
        int? roomId = d.AppointmentService?.RoomId;
        string? roomName = d.AppointmentService?.Room?.RoomName;
        if (!string.IsNullOrEmpty(roomTag) && int.TryParse(roomTag, out var parsedRoomId) && parsedRoomId > 0)
        {
            roomId = parsedRoomId;
            roomName = !string.IsNullOrWhiteSpace(roomNameTag) ? roomNameTag : (parsedRoomId == 5 ? "Phòng X-quang" : $"Phòng #{parsedRoomId}");
        }

        return new DiagnosticResultDto
        {
            DiagnosticResultId = d.DiagnosticId,
            DiagnosticOrderId = d.DiagnosticId,
            ImagingSessionId = d.DiagnosticId,
            SourceType = DiagnosticResultSourceType.Internal,
            EquipmentId = roomId,
            EquipmentName = roomName,
            ExternalFacilityName = null,
            FileUrl = d.FileUrl ?? string.Empty,
            ThumbnailUrl = d.ThumbnailUrl,
            ImageFormat = "JPEG",
            Findings = d.Findings,
            ReviewedByDentistUserId = d.ReviewedByDentistUserId,
            ReviewedByDentistName = d.ReviewedByDentist?.StaffProfile?.FullName,
            ReviewedAtUtc = d.ReviewedAt,
            ReviewNotes = d.Findings,
            IsReviewed = d.ReviewedAt.HasValue,
            UploadedAtUtc = d.CompletedAt ?? d.CreatedAt
        };
    }

    private async Task<ClinicalConsultationDto?> MapReferralToConsultationDtoAsync(long referralId, CancellationToken ct)
    {
        var r = await _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(x => x.SourceAppointmentService)
                .ThenInclude(s => s!.Appointment)
                    .ThenInclude(a => a.Patient)
            .Include(x => x.AssignedDentist)
                .ThenInclude(d => d!.StaffProfile)
            .Include(x => x.ReviewedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .FirstOrDefaultAsync(x => x.ReferralId == referralId, ct);

        return r == null ? null : MapReferralToConsultationDto(r);
    }

    private static ClinicalConsultationDto MapReferralToConsultationDto(DepartmentReferral r)
    {
        var appt = r.SourceAppointmentService?.Appointment;
        return new ClinicalConsultationDto
        {
            ConsultationId = r.ReferralId,
            SourceAppointmentId = appt?.AppointmentId ?? r.SourceAppointmentServiceId ?? 0,
            PatientUserId = appt?.PatientUserId ?? 0,
            PatientName = appt?.Patient?.FullName ?? "Bệnh nhân",
            PatientCode = $"PAT-{appt?.PatientUserId:D4}",
            RequestingDentistUserId = r.ReviewedByUserId ?? 0,
            RequestingDentistName = r.ReviewedByUser?.StaffProfile?.FullName ?? $"Bác sĩ #{r.ReviewedByUserId}",
            ConsultedDentistUserId = r.AssignedDentistUserId ?? 0,
            ConsultedDentistName = r.AssignedDentist?.StaffProfile?.FullName ?? $"Bác sĩ #{r.AssignedDentistUserId}",
            ConsultationReason = r.Reason,
            ClinicalQuestion = r.ClinicalInstructions ?? string.Empty,
            Urgency = r.Urgency,
            Status = r.Status == "Completed" ? ConsultationStatus.Answered : ConsultationStatus.Requested,
            DentistOpinion = r.ReviewedAt.HasValue ? r.ClinicalInstructions : null,
            Recommendations = null,
            RequestedAtUtc = r.CreatedAt,
            AnsweredAtUtc = r.ReviewedAt
        };
    }
}
