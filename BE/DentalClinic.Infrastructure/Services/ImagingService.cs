using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
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

    public ImagingService(DentalClinicDbContext dbContext, ILogger<ImagingService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    #region Diagnostic Orders

    public async Task<DiagnosticOrderDto> CreateDiagnosticOrderAsync(CreateDiagnosticOrderRequest request, long dentistUserId, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var appt = await _dbContext.Appointments
            .Include(a => a.PatientUser)
            .Include(a => a.RequestedDentistUser)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

        if (appt == null)
            throw new InvalidOperationException("Không tìm thấy cuộc hẹn lâm sàng.");

        var validStatuses = new[] { AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn, AppointmentStatus.InService };
        if (!validStatuses.Contains(appt.Status))
            throw new InvalidOperationException($"Không thể tạo chỉ định chẩn đoán cho cuộc hẹn ở trạng thái '{appt.Status}'.");

        var normalizedType = request.DiagnosticType?.Trim();
        if (!ImagingDiagnosticType.IsValid(normalizedType))
            throw new InvalidOperationException($"Loại chẩn đoán '{request.DiagnosticType}' không hợp lệ. Hỗ trợ: Intraoral, Panorama, CBCT.");

        if (string.IsNullOrWhiteSpace(request.ClinicalIndication))
            throw new InvalidOperationException("Chỉ định lâm sàng (ClinicalIndication) không được để trống.");

        if (request.IsExternal && string.IsNullOrWhiteSpace(request.ExternalFacilityName))
            throw new InvalidOperationException("Khi chỉ định chụp ngoại viện, bắt buộc phải nhập tên cơ sở thực hiện (ExternalFacilityName).");

        var order = new DiagnosticOrder
        {
            AppointmentId = appt.AppointmentId,
            PatientUserId = appt.PatientUserId,
            DentistUserId = dentistUserId > 0 ? dentistUserId : (appt.RequestedDentistUserId ?? dentistUserId),
            DiagnosticType = normalizedType!,
            ClinicalIndication = request.ClinicalIndication.Trim(),
            IsExternal = request.IsExternal,
            ExternalFacilityName = request.IsExternal ? request.ExternalFacilityName?.Trim() : null,
            Status = DiagnosticOrderStatus.Ordered,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.DiagnosticOrders.Add(order);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created DiagnosticOrder #{OrderId} for Appointment #{ApptId}, Type={Type}, External={IsExt}",
            order.DiagnosticOrderId, appt.AppointmentId, order.DiagnosticType, order.IsExternal);

        return await MapToOrderDtoAsync(order, ct);
    }

    public async Task<List<DiagnosticOrderDto>> GetDiagnosticOrdersByAppointmentAsync(long appointmentId, CancellationToken ct = default)
    {
        var orders = await _dbContext.DiagnosticOrders
            .AsNoTracking()
            .Include(o => o.Appointment)
            .Include(o => o.PatientUser)
            .Include(o => o.DentistUser)
                .ThenInclude(d => d.User)
            .Include(o => o.ImagingSessions)
                .ThenInclude(s => s.ImagingRoom)
            .Include(o => o.ImagingSessions)
                .ThenInclude(s => s.Equipment)
            .Include(o => o.DiagnosticResults)
                .ThenInclude(r => r.ReviewedByDentistUser)
                    .ThenInclude(d => d!.User)
            .Where(o => o.AppointmentId == appointmentId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);

        var dtos = new List<DiagnosticOrderDto>();
        foreach (var order in orders)
        {
            dtos.Add(MapOrderToDto(order));
        }
        return dtos;
    }

    public async Task<DiagnosticOrderDto?> GetDiagnosticOrderByIdAsync(long diagnosticOrderId, CancellationToken ct = default)
    {
        var order = await _dbContext.DiagnosticOrders
            .AsNoTracking()
            .Include(o => o.Appointment)
            .Include(o => o.PatientUser)
            .Include(o => o.DentistUser)
                .ThenInclude(d => d.User)
            .Include(o => o.ImagingSessions)
                .ThenInclude(s => s.ImagingRoom)
            .Include(o => o.ImagingSessions)
                .ThenInclude(s => s.Equipment)
            .Include(o => o.DiagnosticResults)
                .ThenInclude(r => r.ReviewedByDentistUser)
                    .ThenInclude(d => d!.User)
            .FirstOrDefaultAsync(o => o.DiagnosticOrderId == diagnosticOrderId, ct);

        return order == null ? null : MapOrderToDto(order);
    }

    #endregion

    #region Slots & Scheduling

    public async Task<List<AvailableSlotDto>> GetAvailableSlotsAsync(long equipmentId, DateTime dateUtc, string diagnosticType, CancellationToken ct = default)
    {
        var equipment = await _dbContext.ImagingEquipments
            .AsNoTracking()
            .Include(e => e.Room)
            .FirstOrDefaultAsync(e => e.EquipmentId == equipmentId, ct);

        if (equipment == null)
            throw new InvalidOperationException("Không tìm thấy thiết bị chẩn đoán hình ảnh.");

        if (!equipment.IsActive || equipment.OperationalStatus != EquipmentOperationalStatus.Operational)
        {
            return new List<AvailableSlotDto>();
        }

        var config = await _dbContext.ImagingServiceConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.DiagnosticType == diagnosticType && c.IsActive, ct);

        var durationMinutes = config?.SlotDurationMinutes ?? 15;
        var bufferMinutes = config?.BufferMinutes ?? 0;
        var totalSlotMinutes = durationMinutes + bufferMinutes;

        var startOfDay = dateUtc.Date.Add(equipment.AvailableFrom ?? new TimeSpan(7, 0, 0));
        var endOfDay = dateUtc.Date.Add(equipment.AvailableTo ?? new TimeSpan(21, 0, 0));

        // Get non-terminal sessions for this equipment on date
        var blockingStatuses = new[]
        {
            ImagingSessionStatus.Scheduled,
            ImagingSessionStatus.CheckedIn,
            ImagingSessionStatus.Waiting,
            ImagingSessionStatus.InProgress,
            ImagingSessionStatus.Acquired,
            ImagingSessionStatus.ResultAvailable,
            ImagingSessionStatus.Completed
        };

        var existingSessions = await _dbContext.ImagingSessions
            .AsNoTracking()
            .Where(s => s.EquipmentId == equipmentId &&
                        s.ScheduledStartUtc < endOfDay &&
                        s.ScheduledEndUtc > startOfDay &&
                        blockingStatuses.Contains(s.Status))
            .ToListAsync(ct);

        var slots = new List<AvailableSlotDto>();
        var currentSlotStart = startOfDay;

        while (currentSlotStart.AddMinutes(totalSlotMinutes) <= endOfDay)
        {
            var currentSlotEnd = currentSlotStart.AddMinutes(durationMinutes);
            var slotIntervalEnd = currentSlotStart.AddMinutes(totalSlotMinutes);

            var conflict = existingSessions.FirstOrDefault(s =>
                s.ScheduledStartUtc < slotIntervalEnd && s.ScheduledEndUtc > currentSlotStart);

            slots.Add(new AvailableSlotDto
            {
                StartUtc = currentSlotStart,
                EndUtc = currentSlotEnd,
                IsAvailable = conflict == null,
                ConflictReason = conflict != null ? $"Máy đang bận cho ca #{conflict.ImagingSessionId}" : null
            });

            currentSlotStart = currentSlotStart.AddMinutes(totalSlotMinutes);
        }

        return slots;
    }

    public async Task<ImagingSessionDto> BookImagingSessionAsync(BookImagingSessionRequest request, long createdByUserId, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var order = await _dbContext.DiagnosticOrders
            .Include(o => o.Appointment)
            .FirstOrDefaultAsync(o => o.DiagnosticOrderId == request.DiagnosticOrderId, ct);

        if (order == null)
            throw new InvalidOperationException("Không tìm thấy chỉ định chẩn đoán hình ảnh.");

        if (order.IsExternal)
            throw new InvalidOperationException("Chỉ định chụp ngoại viện không thể đặt lịch phiên chụp nội bộ.");

        if (order.Status == DiagnosticOrderStatus.Cancelled || order.Status == DiagnosticOrderStatus.Completed)
            throw new InvalidOperationException($"Không thể đặt lịch cho chỉ định ở trạng thái '{order.Status}'.");

        var equipment = await _dbContext.ImagingEquipments
            .Include(e => e.Room)
            .FirstOrDefaultAsync(e => e.EquipmentId == request.EquipmentId, ct);

        if (equipment == null)
            throw new InvalidOperationException("Không tìm thấy thiết bị chụp X-quang.");

        if (!equipment.IsActive || equipment.OperationalStatus != EquipmentOperationalStatus.Operational)
            throw new InvalidOperationException($"Thiết bị '{equipment.EquipmentName}' hiện không hoạt động (Trạng thái: {equipment.OperationalStatus}).");

        // Verify equipment supports the diagnostic type
        var supported = (equipment.SupportedDiagnosticTypes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!supported.Contains(order.DiagnosticType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Thiết bị '{equipment.EquipmentName}' không hỗ trợ loại chẩn đoán '{order.DiagnosticType}'.");

        // Get config for duration
        var config = await _dbContext.ImagingServiceConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.DiagnosticType == order.DiagnosticType && c.IsActive, ct);

        var durationMinutes = config?.SlotDurationMinutes ?? 15;
        var bufferMinutes = config?.BufferMinutes ?? 0;
        var scheduledStart = request.ScheduledStartUtc;
        var scheduledEnd = scheduledStart.AddMinutes(durationMinutes);

        // Distributed Lock: Machine + Room + Order
        var lockKeys = new[]
        {
            SqlAppLockHelper.ImagingEquipmentKey(equipment.EquipmentId),
            SqlAppLockHelper.ImagingRoomKey(equipment.RoomId),
            SqlAppLockHelper.DiagnosticOrderKey(order.DiagnosticOrderId)
        };

        var isRelational = _dbContext.Database.IsRelational();
        await using var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        try
        {
            using (await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct))
            {
                // Idempotency: check if order already has an active session
                var existingOrderSession = await _dbContext.ImagingSessions
                    .FirstOrDefaultAsync(s => s.DiagnosticOrderId == order.DiagnosticOrderId &&
                                             s.Status != ImagingSessionStatus.Completed && s.Status != ImagingSessionStatus.Cancelled, ct);

                if (existingOrderSession != null)
                    throw new InvalidOperationException($"Chỉ định #{order.DiagnosticOrderId} đã có phiên chụp hoạt động (#{existingOrderSession.ImagingSessionId}).");

                // Overlap check on equipment
                var blockingStatuses = new[]
                {
                    ImagingSessionStatus.Scheduled,
                    ImagingSessionStatus.CheckedIn,
                    ImagingSessionStatus.Waiting,
                    ImagingSessionStatus.InProgress,
                    ImagingSessionStatus.Acquired,
                    ImagingSessionStatus.ResultAvailable,
                    ImagingSessionStatus.Completed
                };

                var hasConflict = await _dbContext.ImagingSessions
                    .AnyAsync(s => s.EquipmentId == equipment.EquipmentId &&
                                   s.ScheduledStartUtc < scheduledEnd &&
                                   s.ScheduledEndUtc > scheduledStart &&
                                   blockingStatuses.Contains(s.Status), ct);

                if (hasConflict)
                    throw new InvalidOperationException("Khung giờ đã chọn bị trùng lặp với phiên chụp khác trên cùng thiết bị.");

                var session = new ImagingSession
                {
                    DiagnosticOrderId = order.DiagnosticOrderId,
                    SourceAppointmentId = order.AppointmentId,
                    PatientUserId = order.PatientUserId,
                    ImagingRoomId = equipment.RoomId,
                    EquipmentId = equipment.EquipmentId,
                    ScheduledStartUtc = scheduledStart,
                    ScheduledEndUtc = scheduledEnd,
                    Priority = string.Equals(request.Priority, ImagingSessionPriority.Urgent, StringComparison.OrdinalIgnoreCase) ? ImagingSessionPriority.Urgent : ImagingSessionPriority.Normal,
                    Status = ImagingSessionStatus.Scheduled,
                    Notes = request.Notes?.Trim(),
                    CreatedByUserId = createdByUserId,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _dbContext.ImagingSessions.Add(session);
                order.Status = DiagnosticOrderStatus.Scheduled;
                order.UpdatedAtUtc = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);

                _logger.LogInformation("Booked ImagingSession #{SessionId} for Order #{OrderId} on Equipment #{EqId} at {Start}",
                    session.ImagingSessionId, order.DiagnosticOrderId, equipment.EquipmentId, scheduledStart);

                return await MapToSessionDtoAsync(session, ct);
            }
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }
    }

    #endregion

    #region Queue & Procedure Workflow

    public async Task<ImagingSessionDto> CheckInImagingSessionAsync(long imagingSessionId, CancellationToken ct = default)
    {
        var session = await _dbContext.ImagingSessions
            .Include(s => s.ImagingRoom)
            .Include(s => s.DiagnosticOrder)
            .FirstOrDefaultAsync(s => s.ImagingSessionId == imagingSessionId, ct);

        if (session == null)
            throw new InvalidOperationException("Không tìm thấy phiên chụp X-quang.");

        if (session.Status != ImagingSessionStatus.Scheduled)
            throw new InvalidOperationException($"Chỉ có thể Check-in cho phiên chụp ở trạng thái 'Scheduled' (Hiện tại: '{session.Status}').");

        var room = session.ImagingRoom;
        var today = DateTime.UtcNow.Date;

        var lockKeys = new[]
        {
            SqlAppLockHelper.ImagingRoomKey(room.RoomId),
            SqlAppLockHelper.ImagingQueueDateKey(today)
        };

        var isRelational = _dbContext.Database.IsRelational();
        await using var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        try
        {
            using (await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct))
            {
                // Refresh room capacity
                var freshRoom = await _dbContext.Rooms.FindAsync(new object[] { room.RoomId }, ct);
                var maxWaiting = freshRoom?.MaxWaitingPatients ?? room.MaxWaitingPatients;

                // Verify room waiting capacity
                var currentWaitingCount = await _dbContext.ImagingSessions
                    .CountAsync(s => s.ImagingRoomId == room.RoomId &&
                                     s.Status == ImagingSessionStatus.Waiting, ct);

                if (currentWaitingCount >= maxWaiting)
                {
                    throw new InvalidOperationException(
                        $"Hàng đợi phòng chụp X-quang đã đầy (Tối đa {maxWaiting} bệnh nhân chờ). Vui lòng đợi hoặc điều phối sau.");
                }

                // Generate QueueNumber: XR-001, XR-002... for today
                var countToday = await _dbContext.ImagingSessions
                    .CountAsync(s => s.ImagingRoomId == room.RoomId &&
                                     s.CheckInTimeUtc.HasValue &&
                                     s.CheckInTimeUtc.Value >= today &&
                                     s.QueueNumber != null, ct);

                var queueNumber = $"XR-{(countToday + 1):D3}";

                session.QueueNumber = queueNumber;
                session.Status = ImagingSessionStatus.Waiting;
                session.CheckInTimeUtc = DateTime.UtcNow;
                session.UpdatedAtUtc = DateTime.UtcNow;

                // Calculate estimated wait minutes based on actual service configs
                var estimatedWait = await CalculateEstimatedWaitMinutesAsync(room.RoomId, session.DiagnosticOrder.DiagnosticType, ct);
                session.EstimatedWaitMinutes = estimatedWait;

                if (estimatedWait >= room.TargetMaxWaitMinutes)
                {
                    session.DelayNotificationSent = true;
                    _logger.LogWarning("ImagingSession #{SessionId} ({QueueNum}) estimated wait {Wait}m exceeds TargetMaxWait {Target}m",
                        session.ImagingSessionId, queueNumber, estimatedWait, room.TargetMaxWaitMinutes);
                }

                if (estimatedWait >= room.EscalationWaitMinutes)
                {
                    session.EscalationAlertSent = true;
                    _logger.LogWarning("ESCALATION ALERT: ImagingSession #{SessionId} ({QueueNum}) wait {Wait}m exceeds EscalationWait {Esc}m",
                        session.ImagingSessionId, queueNumber, estimatedWait, room.EscalationWaitMinutes);
                }

                await _dbContext.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);

                _logger.LogInformation("Checked-in ImagingSession #{SessionId} as {QueueNum}, EstWait={Wait}m",
                    session.ImagingSessionId, queueNumber, estimatedWait);

                return await MapToSessionDtoAsync(session, ct);
            }
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ImagingSessionDto> StartImagingSessionAsync(long imagingSessionId, long operatorUserId, CancellationToken ct = default)
    {
        var session = await _dbContext.ImagingSessions
            .Include(s => s.DiagnosticOrder)
            .Include(s => s.Equipment)
            .FirstOrDefaultAsync(s => s.ImagingSessionId == imagingSessionId, ct);

        if (session == null)
            throw new InvalidOperationException("Không tìm thấy phiên chụp X-quang.");

        if (session.Status != ImagingSessionStatus.Waiting && session.Status != ImagingSessionStatus.CheckedIn)
            throw new InvalidOperationException($"Chỉ có thể bắt đầu phiên chụp khi bệnh nhân đang chờ (Trạng thái: '{session.Status}').");

        // Dedicated X-ray Duty Dentist Enforcement
        var vnZone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnZone);
        var today = DateOnly.FromDateTime(localNow);
        var nowTime = TimeOnly.FromDateTime(localNow);

        var activeRoster = await _dbContext.XrayDentistRosters
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomId == session.ImagingRoomId &&
                                      r.WorkDate == today &&
                                      r.Status == "Active" &&
                                      r.StartTime <= nowTime &&
                                      r.EndTime >= nowTime, ct);

        if (activeRoster != null)
        {
            var user = await _dbContext.UserAccounts.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == operatorUserId, ct);
            var isAdmin = user != null && user.Role == UserRole.SystemAdministrator;

            if (operatorUserId != activeRoster.DentistUserId &&
                operatorUserId != activeRoster.BackupDentistUserId &&
                !isAdmin)
            {
                var dutyDentist = await _dbContext.StaffProfiles.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == activeRoster.DentistUserId, ct);
                var dutyName = dutyDentist?.FullName ?? $"Bác sĩ #{activeRoster.DentistUserId}";
                throw new InvalidOperationException($"Chỉ bác sĩ trực phòng X-quang trong ca này ({dutyName}) mới có quyền bắt đầu phiên chụp.");
            }
        }

        var lockKeys = new[]
        {
            SqlAppLockHelper.ImagingEquipmentKey(session.EquipmentId),
            SqlAppLockHelper.ImagingRoomKey(session.ImagingRoomId)
        };

        using (await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct))
        {
            // Verify no other session is InProgress on this machine
            var activeOnMachine = await _dbContext.ImagingSessions
                .AnyAsync(s => s.EquipmentId == session.EquipmentId &&
                               s.Status == ImagingSessionStatus.InProgress &&
                               s.ImagingSessionId != session.ImagingSessionId, ct);

            if (activeOnMachine)
                throw new InvalidOperationException($"Thiết bị '{session.Equipment.EquipmentName}' đang được sử dụng cho một ca chụp khác.");

            session.Status = ImagingSessionStatus.InProgress;
            session.ActualStartUtc = DateTime.UtcNow;
            session.AssignedOperatorUserId = operatorUserId;
            session.UpdatedAtUtc = DateTime.UtcNow;

            session.DiagnosticOrder.Status = DiagnosticOrderStatus.InProgress;
            session.DiagnosticOrder.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Started ImagingSession #{SessionId} on Machine #{EqId}",
                session.ImagingSessionId, session.EquipmentId);

            return await MapToSessionDtoAsync(session, ct);
        }
    }

    public async Task<ImagingSessionDto> AcquireImageAsync(long imagingSessionId, CancellationToken ct = default)
    {
        var session = await _dbContext.ImagingSessions
            .Include(s => s.DiagnosticOrder)
            .FirstOrDefaultAsync(s => s.ImagingSessionId == imagingSessionId, ct);

        if (session == null)
            throw new InvalidOperationException("Không tìm thấy phiên chụp X-quang.");

        if (session.Status != ImagingSessionStatus.InProgress)
            throw new InvalidOperationException($"Chỉ có thể ghi nhận chụp ảnh khi phiên đang thực hiện 'InProgress' (Trạng thái: '{session.Status}').");

        session.Status = ImagingSessionStatus.Acquired;
        session.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return await MapToSessionDtoAsync(session, ct);
    }

    public async Task<DiagnosticResultDto> UploadDiagnosticResultAsync(UploadDiagnosticResultRequest request, long uploadedByUserId, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var order = await _dbContext.DiagnosticOrders
            .Include(o => o.Appointment)
            .FirstOrDefaultAsync(o => o.DiagnosticOrderId == request.DiagnosticOrderId, ct);

        if (order == null)
            throw new InvalidOperationException("Không tìm thấy chỉ định chẩn đoán hình ảnh.");

        if (string.IsNullOrWhiteSpace(request.FileUrl))
            throw new InvalidOperationException("Đường dẫn tệp hình ảnh (FileUrl) không được để trống.");

        ImagingSession? session = null;
        if (request.ImagingSessionId.HasValue)
        {
            session = await _dbContext.ImagingSessions
                .FirstOrDefaultAsync(s => s.ImagingSessionId == request.ImagingSessionId.Value, ct);

            if (session != null && session.Status != ImagingSessionStatus.Completed && session.Status != ImagingSessionStatus.Cancelled)
            {
                session.Status = ImagingSessionStatus.ResultAvailable;
                session.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        var result = new DiagnosticResult
        {
            DiagnosticOrderId = order.DiagnosticOrderId,
            ImagingSessionId = request.ImagingSessionId,
            SourceType = string.Equals(request.SourceType, DiagnosticResultSourceType.External, StringComparison.OrdinalIgnoreCase)
                ? DiagnosticResultSourceType.External
                : DiagnosticResultSourceType.Internal,
            EquipmentId = request.EquipmentId ?? session?.EquipmentId,
            ExternalFacilityName = request.ExternalFacilityName ?? order.ExternalFacilityName,
            FileUrl = request.FileUrl.Trim(),
            ThumbnailUrl = request.ThumbnailUrl?.Trim(),
            ImageFormat = string.IsNullOrWhiteSpace(request.ImageFormat) ? "JPEG" : request.ImageFormat.Trim().ToUpperInvariant(),
            Findings = request.Findings?.Trim(),
            UploadedByUserId = uploadedByUserId,
            PerformedAtUtc = request.PerformedAtUtc ?? DateTime.UtcNow,
            UploadedAtUtc = DateTime.UtcNow
        };

        _dbContext.DiagnosticResults.Add(result);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Uploaded DiagnosticResult #{ResultId} for Order #{OrderId}, Source={Source}",
            result.DiagnosticResultId, order.DiagnosticOrderId, result.SourceType);

        // Synchronize with EMR (MedicalRecord -> ClinicalEntry -> ImagingRecord)
        await SynchronizeEmrImagingRecordAsync(order, result, ct);

        return await MapToResultDtoAsync(result, ct);
    }

    public async Task<ImagingSessionDto> CompleteImagingSessionAsync(long imagingSessionId, CancellationToken ct = default)
    {
        var session = await _dbContext.ImagingSessions
            .Include(s => s.DiagnosticOrder)
            .FirstOrDefaultAsync(s => s.ImagingSessionId == imagingSessionId, ct);

        if (session == null)
            throw new InvalidOperationException("Không tìm thấy phiên chụp X-quang.");

        var validPrevStatuses = new[] { ImagingSessionStatus.Acquired, ImagingSessionStatus.ResultAvailable, ImagingSessionStatus.InProgress };
        if (!validPrevStatuses.Contains(session.Status))
            throw new InvalidOperationException($"Không thể hoàn tất phiên chụp ở trạng thái '{session.Status}'.");

        session.Status = ImagingSessionStatus.Completed;
        session.ActualEndUtc = DateTime.UtcNow;
        session.UpdatedAtUtc = DateTime.UtcNow;

        session.DiagnosticOrder.Status = DiagnosticOrderStatus.Completed;
        session.DiagnosticOrder.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Completed ImagingSession #{SessionId} for Order #{OrderId}. Eligible for billing.",
            session.ImagingSessionId, session.DiagnosticOrderId);

        return await MapToSessionDtoAsync(session, ct);
    }

    public async Task<DiagnosticResultDto> ReviewDiagnosticResultAsync(long diagnosticResultId, ReviewDiagnosticResultRequest request, long dentistUserId, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var result = await _dbContext.DiagnosticResults
            .Include(r => r.DiagnosticOrder)
            .FirstOrDefaultAsync(r => r.DiagnosticResultId == diagnosticResultId, ct);

        if (result == null)
            throw new InvalidOperationException("Không tìm thấy kết quả chẩn đoán hình ảnh.");

        if (string.IsNullOrWhiteSpace(request.ReviewNotes))
            throw new InvalidOperationException("Bác sĩ phải nhập nhận xét lâm sàng (ReviewNotes) khi đánh giá kết quả.");

        result.IsReviewed = true;
        result.ReviewedByDentistUserId = dentistUserId;
        result.ReviewedAtUtc = DateTime.UtcNow;
        result.ReviewNotes = request.ReviewNotes.Trim();
        if (!string.IsNullOrWhiteSpace(request.Findings))
        {
            result.Findings = request.Findings.Trim();
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Reviewed DiagnosticResult #{ResultId} by Dentist #{DentistId}",
            result.DiagnosticResultId, dentistUserId);

        // Update EMR ImagingRecord with signed findings and impression
        if (result.DiagnosticOrder != null)
        {
            await SynchronizeEmrImagingRecordAsync(result.DiagnosticOrder, result, ct);
        }

        return await MapToResultDtoAsync(result, ct);
    }

    public async Task<ImagingSessionDto> CancelImagingSessionAsync(long imagingSessionId, string reason, long cancelledByUserId, CancellationToken ct = default)
    {
        var session = await _dbContext.ImagingSessions
            .Include(s => s.DiagnosticOrder)
            .FirstOrDefaultAsync(s => s.ImagingSessionId == imagingSessionId, ct);

        if (session == null)
            throw new InvalidOperationException("Không tìm thấy phiên chụp X-quang.");

        if (session.Status == ImagingSessionStatus.Completed)
            throw new InvalidOperationException("Không thể hủy phiên chụp đã hoàn tất.");

        session.Status = ImagingSessionStatus.Cancelled;
        session.Notes = string.IsNullOrWhiteSpace(session.Notes)
            ? $"Lý do hủy: {reason}"
            : $"{session.Notes} | Lý do hủy: {reason}";
        session.UpdatedAtUtc = DateTime.UtcNow;

        // Check if order has any other non-cancelled sessions
        var otherActive = await _dbContext.ImagingSessions
            .AnyAsync(s => s.DiagnosticOrderId == session.DiagnosticOrderId &&
                           s.ImagingSessionId != session.ImagingSessionId &&
                           s.Status != ImagingSessionStatus.Completed && s.Status != ImagingSessionStatus.Cancelled, ct);

        if (!otherActive)
        {
            session.DiagnosticOrder.Status = DiagnosticOrderStatus.Ordered;
            session.DiagnosticOrder.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Cancelled ImagingSession #{SessionId} by User #{UserId}",
            session.ImagingSessionId, cancelledByUserId);

        return await MapToSessionDtoAsync(session, ct);
    }

    #endregion

    #region Queue Status & Admin Management

    public async Task<ImagingQueueSummaryDto> GetImagingQueueStatusAsync(long roomId, CancellationToken ct = default)
    {
        var room = await _dbContext.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomId == roomId, ct);

        if (room == null)
            throw new InvalidOperationException("Không tìm thấy phòng chụp.");

        var activeSessions = await _dbContext.ImagingSessions
            .AsNoTracking()
            .Include(s => s.PatientUser)
                .ThenInclude(p => p.User)
            .Include(s => s.Equipment)
            .Include(s => s.DiagnosticOrder)
            .Where(s => s.ImagingRoomId == roomId && s.Status == ImagingSessionStatus.InProgress)
            .ToListAsync(ct);

        var waitingQueue = await _dbContext.ImagingSessions
            .AsNoTracking()
            .Include(s => s.PatientUser)
                .ThenInclude(p => p.User)
            .Include(s => s.Equipment)
            .Include(s => s.DiagnosticOrder)
            .Where(s => s.ImagingRoomId == roomId && s.Status == ImagingSessionStatus.Waiting)
            .OrderBy(s => s.CheckInTimeUtc)
            .ToListAsync(ct);

        // Calculate dynamic wait time for next incoming patient
        var estimatedNextWait = 0;
        var now = DateTime.UtcNow;

        foreach (var inProg in activeSessions)
        {
            var remaining = (int)Math.Max(0, (inProg.ScheduledEndUtc - now).TotalMinutes);
            estimatedNextWait += remaining > 0 ? remaining : 5;
        }

        // Add duration of each waiting session based on its specific DiagnosticType
        var configs = await _dbContext.ImagingServiceConfigs.AsNoTracking().ToListAsync(ct);
        var configMap = configs.ToDictionary(c => c.DiagnosticType, c => c.SlotDurationMinutes + c.BufferMinutes, StringComparer.OrdinalIgnoreCase);

        foreach (var wait in waitingQueue)
        {
            var dur = configMap.TryGetValue(wait.DiagnosticOrder.DiagnosticType, out var d) ? d : 15;
            estimatedNextWait += dur;
        }

        var activeDtos = activeSessions.Select(MapSessionToDto).ToList();
        var waitingDtos = waitingQueue.Select(MapSessionToDto).ToList();

        var hasDelayWarning = estimatedNextWait >= room.TargetMaxWaitMinutes;
        var hasEscalationAlert = estimatedNextWait >= room.EscalationWaitMinutes;

        // Query active dedicated X-ray roster for this room
        var vnZone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, vnZone);
        var today = DateOnly.FromDateTime(localNow);
        var nowTime = TimeOnly.FromDateTime(localNow);

        var activeRoster = await _dbContext.XrayDentistRosters
            .AsNoTracking()
            .Include(r => r.DentistUser)
                .ThenInclude(u => u.StaffProfile)
                    .ThenInclude(s => s!.DentistProfile)
            .Include(r => r.BackupDentistUser)
                .ThenInclude(u => u!.StaffProfile)
            .FirstOrDefaultAsync(r => r.RoomId == roomId &&
                                      r.WorkDate == today &&
                                      r.Status == "Active" &&
                                      r.StartTime <= nowTime &&
                                      r.EndTime >= nowTime, ct);

        long? dutyDentistId = activeRoster?.DentistUserId;
        string? dutyDentistName = activeRoster?.DentistUser?.StaffProfile?.FullName ?? activeRoster?.DentistUser?.Email;
        string? dutyDentistShift = activeRoster?.ShiftType;
        string? dutyDentistCert = activeRoster?.DentistUser?.StaffProfile?.DentistProfile?.RadiationCertNumber;
        long? backupDentistId = activeRoster?.BackupDentistUserId;
        string? backupDentistName = activeRoster?.BackupDentistUser?.StaffProfile?.FullName;
        bool hasDutyDentist = activeRoster != null;
        bool isDutyOnLeave = false;

        if (activeRoster != null)
        {
            isDutyOnLeave = await _dbContext.DentistLeaveRequests
                .AsNoTracking()
                .AnyAsync(l => l.DentistUserId == activeRoster.DentistUserId &&
                               l.Status == "Approved" &&
                               l.StartDate <= today &&
                               l.EndDate >= today, ct);
        }

        return new ImagingQueueSummaryDto
        {
            RoomId = room.RoomId,
            RoomCode = room.RoomCode,
            RoomName = room.RoomName ?? room.RoomCode,
            CurrentInProgressCount = activeSessions.Count,
            CurrentWaitingCount = waitingQueue.Count,
            MaxWaitingPatients = room.MaxWaitingPatients,
            TargetMaxWaitMinutes = room.TargetMaxWaitMinutes,
            EscalationWaitMinutes = room.EscalationWaitMinutes,
            EstimatedNextWaitMinutes = estimatedNextWait,
            HasDelayWarning = hasDelayWarning,
            HasEscalationAlert = hasEscalationAlert,
            ActiveSessions = activeDtos,
            WaitingQueue = waitingDtos,
            OnDutyDentistUserId = dutyDentistId,
            OnDutyDentistName = dutyDentistName,
            OnDutyDentistShift = dutyDentistShift,
            OnDutyDentistRadiationCert = dutyDentistCert,
            BackupDentistUserId = backupDentistId,
            BackupDentistName = backupDentistName,
            HasOnDutyDentist = hasDutyDentist,
            IsDentistOnLeave = isDutyOnLeave
        };
    }

    public async Task<List<ImagingEquipmentDto>> GetAllEquipmentsAsync(CancellationToken ct = default)
    {
        var list = await _dbContext.ImagingEquipments
            .AsNoTracking()
            .Include(e => e.Room)
            .OrderBy(e => e.EquipmentCode)
            .ToListAsync(ct);

        return list.Select(e => new ImagingEquipmentDto
        {
            EquipmentId = e.EquipmentId,
            RoomId = e.RoomId,
            RoomCode = e.Room.RoomCode,
            RoomName = e.Room.RoomName ?? e.Room.RoomCode,
            EquipmentCode = e.EquipmentCode,
            EquipmentName = e.EquipmentName,
            EquipmentType = e.EquipmentType,
            SupportedDiagnosticTypes = e.SupportedDiagnosticTypes,
            IsActive = e.IsActive,
            OperationalStatus = e.OperationalStatus,
            AvailableFrom = e.AvailableFrom,
            AvailableTo = e.AvailableTo
        }).ToList();
    }

    public async Task<ImagingEquipmentDto> UpdateEquipmentStatusAsync(long equipmentId, string operationalStatus, CancellationToken ct = default)
    {
        var eq = await _dbContext.ImagingEquipments
            .Include(e => e.Room)
            .FirstOrDefaultAsync(e => e.EquipmentId == equipmentId, ct);

        if (eq == null)
            throw new InvalidOperationException("Không tìm thấy thiết bị chụp X-quang.");

        var normalized = operationalStatus?.Trim();
        if (!EquipmentOperationalStatus.IsValid(normalized))
            throw new InvalidOperationException($"Trạng thái vận hành '{operationalStatus}' không hợp lệ.");

        eq.OperationalStatus = normalized!;
        eq.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Updated Equipment #{EqId} OperationalStatus to '{Status}'", eq.EquipmentId, eq.OperationalStatus);

        return new ImagingEquipmentDto
        {
            EquipmentId = eq.EquipmentId,
            RoomId = eq.RoomId,
            RoomCode = eq.Room.RoomCode,
            RoomName = eq.Room.RoomName ?? eq.Room.RoomCode,
            EquipmentCode = eq.EquipmentCode,
            EquipmentName = eq.EquipmentName,
            EquipmentType = eq.EquipmentType,
            SupportedDiagnosticTypes = eq.SupportedDiagnosticTypes,
            IsActive = eq.IsActive,
            OperationalStatus = eq.OperationalStatus,
            AvailableFrom = eq.AvailableFrom,
            AvailableTo = eq.AvailableTo
        };
    }

    public async Task<List<ImagingServiceConfigDto>> GetAllServiceConfigsAsync(CancellationToken ct = default)
    {
        var list = await _dbContext.ImagingServiceConfigs
            .AsNoTracking()
            .OrderBy(c => c.ConfigId)
            .ToListAsync(ct);

        return list.Select(c => new ImagingServiceConfigDto
        {
            ConfigId = c.ConfigId,
            DiagnosticType = c.DiagnosticType,
            ServiceName = c.ServiceName,
            SlotDurationMinutes = c.SlotDurationMinutes,
            BufferMinutes = c.BufferMinutes,
            Price = c.Price,
            IsActive = c.IsActive
        }).ToList();
    }

    public async Task<ImagingServiceConfigDto> UpdateServiceConfigAsync(long configId, UpdateImagingConfigRequest request, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var cfg = await _dbContext.ImagingServiceConfigs
            .FirstOrDefaultAsync(c => c.ConfigId == configId, ct);

        if (cfg == null)
            throw new InvalidOperationException("Không tìm thấy cấu hình dịch vụ chẩn đoán hình ảnh.");

        if (request.SlotDurationMinutes <= 0)
            throw new InvalidOperationException("Thời lượng slot phải lớn hơn 0 phút.");

        cfg.SlotDurationMinutes = request.SlotDurationMinutes;
        cfg.BufferMinutes = Math.Max(0, request.BufferMinutes);
        cfg.Price = Math.Max(0, request.Price);
        cfg.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Updated ImagingServiceConfig #{ConfigId} for {Type}: Duration={Dur}m, Price={Price}",
            cfg.ConfigId, cfg.DiagnosticType, cfg.SlotDurationMinutes, cfg.Price);

        return new ImagingServiceConfigDto
        {
            ConfigId = cfg.ConfigId,
            DiagnosticType = cfg.DiagnosticType,
            ServiceName = cfg.ServiceName,
            SlotDurationMinutes = cfg.SlotDurationMinutes,
            BufferMinutes = cfg.BufferMinutes,
            Price = cfg.Price,
            IsActive = cfg.IsActive
        };
    }

    #endregion

    #region Helper Calculation & Mapping

    private async Task<int> CalculateEstimatedWaitMinutesAsync(long roomId, string currentDiagnosticType, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var inProgress = await _dbContext.ImagingSessions
            .AsNoTracking()
            .Where(s => s.ImagingRoomId == roomId && s.Status == ImagingSessionStatus.InProgress)
            .ToListAsync(ct);

        var totalWait = 0;
        foreach (var session in inProgress)
        {
            var remaining = (int)Math.Max(0, (session.ScheduledEndUtc - now).TotalMinutes);
            totalWait += remaining > 0 ? remaining : 5;
        }

        var waitingList = await _dbContext.ImagingSessions
            .AsNoTracking()
            .Include(s => s.DiagnosticOrder)
            .Where(s => s.ImagingRoomId == roomId && s.Status == ImagingSessionStatus.Waiting)
            .ToListAsync(ct);

        var configs = await _dbContext.ImagingServiceConfigs.AsNoTracking().ToListAsync(ct);
        var configMap = configs.ToDictionary(c => c.DiagnosticType, c => c.SlotDurationMinutes + c.BufferMinutes, StringComparer.OrdinalIgnoreCase);

        foreach (var wait in waitingList)
        {
            var dur = configMap.TryGetValue(wait.DiagnosticOrder.DiagnosticType, out var d) ? d : 15;
            totalWait += dur;
        }

        return totalWait;
    }

    private static DiagnosticOrderDto MapOrderToDto(DiagnosticOrder order)
    {
        return new DiagnosticOrderDto
        {
            DiagnosticOrderId = order.DiagnosticOrderId,
            AppointmentId = order.AppointmentId,
            PatientUserId = order.PatientUserId,
            PatientName = order.PatientUser?.FullName ?? $"Bệnh nhân #{order.PatientUserId}",
            PatientCode = order.PatientUser?.PatientCode,
            DentistUserId = order.DentistUserId,
            DentistName = order.DentistUser?.User?.FullName ?? $"Bác sĩ #{order.DentistUserId}",
            DiagnosticType = order.DiagnosticType,
            DiagnosticTypeName = ImagingDiagnosticType.GetDisplayName(order.DiagnosticType),
            ClinicalIndication = order.ClinicalIndication,
            IsExternal = order.IsExternal,
            ExternalFacilityName = order.ExternalFacilityName,
            Status = order.Status,
            BilledAtUtc = order.BilledAtUtc,
            InvoiceItemId = order.InvoiceItemId,
            CreatedAtUtc = order.CreatedAtUtc,
            Sessions = order.ImagingSessions.Select(MapSessionToDto).ToList(),
            Results = order.DiagnosticResults.Select(MapResultToDto).ToList()
        };
    }

    private static ImagingSessionDto MapSessionToDto(ImagingSession s)
    {
        return new ImagingSessionDto
        {
            ImagingSessionId = s.ImagingSessionId,
            DiagnosticOrderId = s.DiagnosticOrderId,
            SourceAppointmentId = s.SourceAppointmentId,
            PatientUserId = s.PatientUserId,
            PatientName = s.PatientUser?.FullName ?? $"Bệnh nhân #{s.PatientUserId}",
            PatientCode = s.PatientUser?.PatientCode,
            ImagingRoomId = s.ImagingRoomId,
            RoomCode = s.ImagingRoom?.RoomCode ?? "XRAY",
            RoomName = s.ImagingRoom?.RoomName ?? s.ImagingRoom?.RoomCode ?? "Phòng X-quang",
            EquipmentId = s.EquipmentId,
            EquipmentName = s.Equipment?.EquipmentName ?? $"Máy #{s.EquipmentId}",
            DiagnosticType = s.DiagnosticOrder?.DiagnosticType ?? "Intraoral",
            SlotDurationMinutes = (int)(s.ScheduledEndUtc - s.ScheduledStartUtc).TotalMinutes,
            ScheduledStartUtc = s.ScheduledStartUtc,
            ScheduledEndUtc = s.ScheduledEndUtc,
            ActualStartUtc = s.ActualStartUtc,
            ActualEndUtc = s.ActualEndUtc,
            QueueNumber = s.QueueNumber,
            Priority = s.Priority,
            Status = s.Status,
            CheckInTimeUtc = s.CheckInTimeUtc,
            EstimatedWaitMinutes = s.EstimatedWaitMinutes,
            DelayNotificationSent = s.DelayNotificationSent,
            EscalationAlertSent = s.EscalationAlertSent,
            Notes = s.Notes,
            CreatedAtUtc = s.CreatedAtUtc
        };
    }

    private static DiagnosticResultDto MapResultToDto(DiagnosticResult r)
    {
        return new DiagnosticResultDto
        {
            DiagnosticResultId = r.DiagnosticResultId,
            DiagnosticOrderId = r.DiagnosticOrderId,
            ImagingSessionId = r.ImagingSessionId,
            SourceType = r.SourceType,
            EquipmentId = r.EquipmentId,
            EquipmentName = r.Equipment?.EquipmentName,
            ExternalFacilityName = r.ExternalFacilityName,
            FileUrl = r.FileUrl,
            ThumbnailUrl = r.ThumbnailUrl,
            ImageFormat = r.ImageFormat,
            Findings = r.Findings,
            ReviewedByDentistUserId = r.ReviewedByDentistUserId,
            ReviewedByDentistName = r.ReviewedByDentistUser?.User?.FullName,
            ReviewedAtUtc = r.ReviewedAtUtc,
            ReviewNotes = r.ReviewNotes,
            IsReviewed = r.IsReviewed,
            UploadedAtUtc = r.UploadedAtUtc
        };
    }

    private async Task<DiagnosticOrderDto> MapToOrderDtoAsync(DiagnosticOrder order, CancellationToken ct)
    {
        var refreshed = await _dbContext.DiagnosticOrders
            .AsNoTracking()
            .Include(o => o.Appointment)
            .Include(o => o.PatientUser)
            .Include(o => o.DentistUser)
                .ThenInclude(d => d.User)
            .Include(o => o.ImagingSessions)
                .ThenInclude(s => s.ImagingRoom)
            .Include(o => o.ImagingSessions)
                .ThenInclude(s => s.Equipment)
            .Include(o => o.DiagnosticResults)
                .ThenInclude(r => r.ReviewedByDentistUser)
                    .ThenInclude(d => d!.User)
            .FirstAsync(o => o.DiagnosticOrderId == order.DiagnosticOrderId, ct);

        return MapOrderToDto(refreshed);
    }

    private async Task<ImagingSessionDto> MapToSessionDtoAsync(ImagingSession session, CancellationToken ct)
    {
        var refreshed = await _dbContext.ImagingSessions
            .AsNoTracking()
            .Include(s => s.PatientUser)
            .Include(s => s.ImagingRoom)
            .Include(s => s.Equipment)
            .Include(s => s.DiagnosticOrder)
            .FirstAsync(s => s.ImagingSessionId == session.ImagingSessionId, ct);

        return MapSessionToDto(refreshed);
    }

    private async Task<DiagnosticResultDto> MapToResultDtoAsync(DiagnosticResult result, CancellationToken ct)
    {
        var refreshed = await _dbContext.DiagnosticResults
            .AsNoTracking()
            .Include(r => r.Equipment)
            .Include(r => r.ReviewedByDentistUser)
                .ThenInclude(d => d!.User)
            .FirstAsync(r => r.DiagnosticResultId == result.DiagnosticResultId, ct);

        return MapResultToDto(refreshed);
    }

    private async Task SynchronizeEmrImagingRecordAsync(DiagnosticOrder order, DiagnosticResult result, CancellationToken ct)
    {
        try
        {
            var patientId = order.PatientUserId;
            if (patientId <= 0 && order.Appointment != null)
            {
                patientId = order.Appointment.PatientUserId;
            }

            if (patientId <= 0) return;

            var medicalRecord = await _dbContext.DentalMedicalRecords
                .FirstOrDefaultAsync(m => m.PatientUserId == patientId, ct);

            if (medicalRecord == null)
            {
                medicalRecord = new DentalMedicalRecord
                {
                    PatientUserId = patientId,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.DentalMedicalRecords.Add(medicalRecord);
                await _dbContext.SaveChangesAsync(ct);
            }

            ClinicalEntry? entry = null;
            if (order.AppointmentId > 0)
            {
                entry = await _dbContext.ClinicalEntries
                    .FirstOrDefaultAsync(c => c.AppointmentId == order.AppointmentId, ct);
            }

            if (entry == null)
            {
                entry = await _dbContext.ClinicalEntries
                    .Where(c => c.MedicalRecordId == medicalRecord.MedicalRecordId)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync(ct);
            }

            if (entry == null)
            {
                var dentistId = order.DentistUserId > 0 ? order.DentistUserId : (result.UploadedByUserId > 0 ? result.UploadedByUserId : 14);
                entry = new ClinicalEntry
                {
                    MedicalRecordId = medicalRecord.MedicalRecordId,
                    AppointmentId = order.AppointmentId > 0 ? order.AppointmentId : null,
                    DentistUserId = dentistId,
                    Diagnosis = $"Chẩn đoán hình ảnh: {order.DiagnosticType ?? "X-quang"}",
                    ClinicalFindings = order.ClinicalIndication ?? "Khảo sát chẩn đoán hình ảnh theo chỉ định",
                    Symptoms = "Chỉ định cận lâm sàng",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.ClinicalEntries.Add(entry);
                await _dbContext.SaveChangesAsync(ct);
            }

            var summary = !string.IsNullOrWhiteSpace(result.ReviewNotes)
                ? $"{result.Findings} | Kết luận: {result.ReviewNotes}"
                : (!string.IsNullOrWhiteSpace(result.Findings) ? result.Findings : "Kết quả chụp X-quang kỹ thuật số");

            var existingImg = await _dbContext.ImagingRecords
                .FirstOrDefaultAsync(i => i.ClinicalEntryId == entry.ClinicalEntryId && i.FileUrl == result.FileUrl, ct);

            if (existingImg != null)
            {
                existingImg.ResultSummary = summary;
            }
            else
            {
                var imagingRecord = new ImagingRecord
                {
                    ClinicalEntryId = entry.ClinicalEntryId,
                    ImagingType = order.DiagnosticType ?? "Panorama",
                    FileUrl = result.FileUrl,
                    ResultSummary = summary,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.ImagingRecords.Add(imagingRecord);
            }

            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully synchronized EMR ImagingRecord for Order #{OrderId}, Patient #{PatientId}, Entry #{EntryId}",
                order.DiagnosticOrderId, patientId, entry.ClinicalEntryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synchronize EMR ImagingRecord for Order #{OrderId}", order.DiagnosticOrderId);
        }
    }

    #endregion

    #region Clinical Consultations

    public async Task<ClinicalConsultationDto> CreateConsultationAsync(CreateConsultationRequest request, long dentistUserId, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.SourceAppointmentId <= 0)
            throw new ArgumentException("M� ca kh�m (SourceAppointmentId) kh�ng h?p l?.");
        if (request.ConsultedDentistUserId <= 0)
            throw new ArgumentException("Vui l�ng ch?n b�c si c?n h?i ch?n.");
        if (string.IsNullOrWhiteSpace(request.ConsultationReason))
            throw new ArgumentException("L� do h?i ch?n kh�ng du?c d? tr?ng.");
        if (string.IsNullOrWhiteSpace(request.ClinicalQuestion))
            throw new ArgumentException("C�u h?i l�m s�ng kh�ng du?c d? tr?ng.");

        var appt = await _dbContext.Appointments
            .Include(a => a.PatientUser)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.SourceAppointmentId, ct);
        if (appt == null)
            throw new InvalidOperationException("Kh�ng t�m th?y ca kh�m ngu?n.");

        var consultedDentist = await _dbContext.DentistProfiles
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.UserId == request.ConsultedDentistUserId, ct);
        if (consultedDentist == null || consultedDentist.User.EmploymentStatus != "Active")
            throw new InvalidOperationException("B�c si du?c m?i h?i ch?n kh�ng t?n t?i ho?c d� ng?ng ho?t d?ng.");

        if (request.ConsultedDentistUserId == dentistUserId)
            throw new InvalidOperationException("Kh�ng th? g?i y�u c?u h?i ch?n cho ch�nh m�nh.");

        var requestingDentist = await _dbContext.DentistProfiles
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        var urgency = string.IsNullOrWhiteSpace(request.Urgency) ? ConsultationUrgency.Normal : request.Urgency.Trim();

        var consultation = new ClinicalConsultation
        {
            SourceAppointmentId = request.SourceAppointmentId,
            PatientUserId = appt.PatientUserId,
            RequestingDentistUserId = dentistUserId,
            ConsultedDentistUserId = request.ConsultedDentistUserId,
            ConsultationReason = request.ConsultationReason.Trim(),
            ClinicalQuestion = request.ClinicalQuestion.Trim(),
            Urgency = urgency,
            Status = ConsultationStatus.Requested,
            RequestedAtUtc = DateTime.UtcNow
        };

        _dbContext.ClinicalConsultations.Add(consultation);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("B�c si {RequestingId} d� g?i y�u c?u h?i ch?n #{ConsultationId} t?i b�c si {ConsultedId}",
            dentistUserId, consultation.ConsultationId, request.ConsultedDentistUserId);

        return new ClinicalConsultationDto
        {
            ConsultationId = consultation.ConsultationId,
            SourceAppointmentId = consultation.SourceAppointmentId,
            PatientUserId = consultation.PatientUserId,
            PatientName = appt.PatientUser.FullName,
            PatientCode = appt.PatientUser.PatientCode,
            RequestingDentistUserId = consultation.RequestingDentistUserId,
            RequestingDentistName = requestingDentist?.User.FullName ?? $"B�c si #{dentistUserId}",
            ConsultedDentistUserId = consultation.ConsultedDentistUserId,
            ConsultedDentistName = consultedDentist.User.FullName,
            ConsultationReason = consultation.ConsultationReason,
            ClinicalQuestion = consultation.ClinicalQuestion,
            Urgency = consultation.Urgency,
            Status = consultation.Status,
            DentistOpinion = consultation.DentistOpinion,
            Recommendations = consultation.Recommendations,
            RequestedAtUtc = consultation.RequestedAtUtc,
            AnsweredAtUtc = consultation.AnsweredAtUtc
        };
    }

    public async Task<ClinicalConsultationDto> RespondConsultationAsync(long consultationId, RespondConsultationRequest request, long dentistUserId, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.DentistOpinion))
            throw new ArgumentException("� ki?n chuy�n m�n kh�ng du?c d? tr?ng.");

        var consultation = await _dbContext.ClinicalConsultations
            .Include(c => c.PatientUser)
            .Include(c => c.RequestingDentistUser).ThenInclude(d => d.User)
            .Include(c => c.ConsultedDentistUser).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(c => c.ConsultationId == consultationId, ct);

        if (consultation == null)
            throw new InvalidOperationException("Kh�ng t�m th?y y�u c?u h?i ch?n.");

        if (consultation.ConsultedDentistUserId != dentistUserId)
            throw new UnauthorizedAccessException("Ch? b�c si du?c m?i h?i ch?n m?i c� quy?n ph?n h?i y�u c?u n�y.");

        if (consultation.Status != ConsultationStatus.Requested)
            throw new InvalidOperationException($"Y�u c?u h?i ch?n d� ? tr?ng th�i '{consultation.Status}', kh�ng th? ph?n h?i l?i.");

        consultation.DentistOpinion = request.DentistOpinion.Trim();
        consultation.Recommendations = request.Recommendations?.Trim();
        consultation.Status = ConsultationStatus.Answered;
        consultation.AnsweredAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("B�c si {ConsultedId} d� ho�n t?t ph?n h?i h?i ch?n #{ConsultationId}",
            dentistUserId, consultationId);

        return new ClinicalConsultationDto
        {
            ConsultationId = consultation.ConsultationId,
            SourceAppointmentId = consultation.SourceAppointmentId,
            PatientUserId = consultation.PatientUserId,
            PatientName = consultation.PatientUser.FullName,
            PatientCode = consultation.PatientUser.PatientCode,
            RequestingDentistUserId = consultation.RequestingDentistUserId,
            RequestingDentistName = consultation.RequestingDentistUser.User.FullName,
            ConsultedDentistUserId = consultation.ConsultedDentistUserId,
            ConsultedDentistName = consultation.ConsultedDentistUser.User.FullName,
            ConsultationReason = consultation.ConsultationReason,
            ClinicalQuestion = consultation.ClinicalQuestion,
            Urgency = consultation.Urgency,
            Status = consultation.Status,
            DentistOpinion = consultation.DentistOpinion,
            Recommendations = consultation.Recommendations,
            RequestedAtUtc = consultation.RequestedAtUtc,
            AnsweredAtUtc = consultation.AnsweredAtUtc
        };
    }

    public async Task<List<ClinicalConsultationDto>> GetConsultationsByDentistAsync(long dentistUserId, string type, CancellationToken ct = default)
    {
        var query = _dbContext.ClinicalConsultations
            .AsNoTracking()
            .Include(c => c.PatientUser)
            .Include(c => c.RequestingDentistUser).ThenInclude(d => d.User)
            .Include(c => c.ConsultedDentistUser).ThenInclude(d => d.User)
            .AsQueryable();

        if (string.Equals(type, "requested", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.RequestingDentistUserId == dentistUserId);
        }
        else if (string.Equals(type, "received", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.ConsultedDentistUserId == dentistUserId);
        }
        else
        {
            query = query.Where(c => c.RequestingDentistUserId == dentistUserId || c.ConsultedDentistUserId == dentistUserId);
        }

        var list = await query.OrderByDescending(c => c.RequestedAtUtc).ToListAsync(ct);

        return list.Select(c => new ClinicalConsultationDto
        {
            ConsultationId = c.ConsultationId,
            SourceAppointmentId = c.SourceAppointmentId,
            PatientUserId = c.PatientUserId,
            PatientName = c.PatientUser.FullName,
            PatientCode = c.PatientUser.PatientCode,
            RequestingDentistUserId = c.RequestingDentistUserId,
            RequestingDentistName = c.RequestingDentistUser.User.FullName,
            ConsultedDentistUserId = c.ConsultedDentistUserId,
            ConsultedDentistName = c.ConsultedDentistUser.User.FullName,
            ConsultationReason = c.ConsultationReason,
            ClinicalQuestion = c.ClinicalQuestion,
            Urgency = c.Urgency,
            Status = c.Status,
            DentistOpinion = c.DentistOpinion,
            Recommendations = c.Recommendations,
            RequestedAtUtc = c.RequestedAtUtc,
            AnsweredAtUtc = c.AnsweredAtUtc
        }).ToList();
    }

    public async Task<List<ClinicalConsultationDto>> GetConsultationsByAppointmentAsync(long appointmentId, CancellationToken ct = default)
    {
        var list = await _dbContext.ClinicalConsultations
            .AsNoTracking()
            .Include(c => c.PatientUser)
            .Include(c => c.RequestingDentistUser).ThenInclude(d => d.User)
            .Include(c => c.ConsultedDentistUser).ThenInclude(d => d.User)
            .Where(c => c.SourceAppointmentId == appointmentId)
            .OrderByDescending(c => c.RequestedAtUtc)
            .ToListAsync(ct);

        return list.Select(c => new ClinicalConsultationDto
        {
            ConsultationId = c.ConsultationId,
            SourceAppointmentId = c.SourceAppointmentId,
            PatientUserId = c.PatientUserId,
            PatientName = c.PatientUser.FullName,
            PatientCode = c.PatientUser.PatientCode,
            RequestingDentistUserId = c.RequestingDentistUserId,
            RequestingDentistName = c.RequestingDentistUser.User.FullName,
            ConsultedDentistUserId = c.ConsultedDentistUserId,
            ConsultedDentistName = c.ConsultedDentistUser.User.FullName,
            ConsultationReason = c.ConsultationReason,
            ClinicalQuestion = c.ClinicalQuestion,
            Urgency = c.Urgency,
            Status = c.Status,
            DentistOpinion = c.DentistOpinion,
            Recommendations = c.Recommendations,
            RequestedAtUtc = c.RequestedAtUtc,
            AnsweredAtUtc = c.AnsweredAtUtc
        }).ToList();
    }

    public async Task<ClinicalConsultationDto?> GetConsultationByIdAsync(long consultationId, CancellationToken ct = default)
    {
        var c = await _dbContext.ClinicalConsultations
            .AsNoTracking()
            .Include(c => c.PatientUser)
            .Include(c => c.RequestingDentistUser).ThenInclude(d => d.User)
            .Include(c => c.ConsultedDentistUser).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(c => c.ConsultationId == consultationId, ct);

        if (c == null) return null;

        return new ClinicalConsultationDto
        {
            ConsultationId = c.ConsultationId,
            SourceAppointmentId = c.SourceAppointmentId,
            PatientUserId = c.PatientUserId,
            PatientName = c.PatientUser.FullName,
            PatientCode = c.PatientUser.PatientCode,
            RequestingDentistUserId = c.RequestingDentistUserId,
            RequestingDentistName = c.RequestingDentistUser.User.FullName,
            ConsultedDentistUserId = c.ConsultedDentistUserId,
            ConsultedDentistName = c.ConsultedDentistUser.User.FullName,
            ConsultationReason = c.ConsultationReason,
            ClinicalQuestion = c.ClinicalQuestion,
            Urgency = c.Urgency,
            Status = c.Status,
            DentistOpinion = c.DentistOpinion,
            Recommendations = c.Recommendations,
            RequestedAtUtc = c.RequestedAtUtc,
            AnsweredAtUtc = c.AnsweredAtUtc
        };
    }

    #endregion
}