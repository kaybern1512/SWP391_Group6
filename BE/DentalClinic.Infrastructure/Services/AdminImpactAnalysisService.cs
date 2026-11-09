using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Operations.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class AdminImpactAnalysisService : IAdminImpactAnalysisService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly ILogger<AdminImpactAnalysisService> _logger;

    private static readonly string[] ActiveAppointmentStatuses = new[]
    {
        AppointmentStatus.PendingReceptionReview,
        AppointmentStatus.PendingDepartmentReview,
        AppointmentStatus.Confirmed,
        AppointmentStatus.CheckedIn,
        AppointmentStatus.InService,
        AppointmentStatus.Waiting,
        AppointmentStatus.Called
    };

    public AdminImpactAnalysisService(
        DentalClinicDbContext dbContext,
        ILogger<AdminImpactAnalysisService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApiResponse<UserLockImpactDto>> PreviewUserLockImpactAsync(long userId, CancellationToken ct = default)
    {
        var user = await _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);

        if (user == null)
        {
            return ApiResponse<UserLockImpactDto>.Fail("Không tìm thấy thông tin tài khoản người dùng.");
        }

        var nowUtc = DateTime.UtcNow;
        var today = ClinicTimeHelper.TodayDateOnly;
        var warnings = new List<string>();
        int futureShiftsCount = 0;
        int futureAppointmentsCount = 0;
        bool triggersShortage = false;

        if (user.Role == UserRole.Dentist)
        {
            futureShiftsCount = await _dbContext.DentistWorkSchedules
                .AsNoTracking()
                .CountAsync(ws => ws.DentistUserId == userId && ws.WorkDate >= today && ws.Status == "Scheduled", ct);

            futureAppointmentsCount = await _dbContext.Appointments
                .AsNoTracking()
                .CountAsync(a => (a.AssignedDentistUserId == userId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == userId))
                                 && a.ScheduledStart != null && a.ScheduledStart.Value >= nowUtc
                                 && ActiveAppointmentStatuses.Contains(a.Status), ct);

            if (futureAppointmentsCount > 0)
            {
                warnings.Add($"Bác sĩ hiện có {futureAppointmentsCount} lịch hẹn trong tương lai đang gán hoặc chờ khám. Việc khóa tài khoản sẽ làm gián đoạn lịch hẹn của bệnh nhân.");
            }

            if (futureShiftsCount > 0)
            {
                warnings.Add($"Bác sĩ hiện có {futureShiftsCount} ca trực đã phân công từ ngày hôm nay trở đi. Cần hủy lịch trực hoặc phân công lại trước khi khóa.");
            }
        }
        else if (user.Role == UserRole.Receptionist)
        {
            var remainingActiveCount = await _dbContext.UserAccounts
                .AsNoTracking()
                .CountAsync(u => u.Role == UserRole.Receptionist && u.Status == "Active" && u.UserId != userId, ct);

            if (remainingActiveCount < 2)
            {
                triggersShortage = true;
                warnings.Add($"CẢNH BÁO NGUY HIỂM: Khóa tài khoản này sẽ làm số lượng tiếp tân hoạt động chỉ còn {remainingActiveCount} người (dưới 2 người tối thiểu), gây gián đoạn quy trình tiếp đón và trực quầy!");
            }
        }

        var dto = new UserLockImpactDto
        {
            UserId = user.UserId,
            FullName = user.StaffProfile?.FullName ?? $"Tài khoản #{user.UserId}",
            Role = user.Role,
            FutureShiftsCount = futureShiftsCount,
            FutureAppointmentsCount = futureAppointmentsCount,
            TriggersReceptionistShortage = triggersShortage,
            Warnings = warnings
        };

        return ApiResponse<UserLockImpactDto>.Ok(dto);
    }

    public async Task<ApiResponse<ServiceDeactivationImpactDto>> PreviewServiceDeactivationImpactAsync(long serviceId, CancellationToken ct = default)
    {
        var service = await _dbContext.DepartmentServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == serviceId, ct);

        if (service == null)
        {
            return ApiResponse<ServiceDeactivationImpactDto>.Fail("Không tìm thấy dịch vụ.");
        }

        var nowUtc = DateTime.UtcNow;
        var futureAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.RequestedServiceId == serviceId && a.ScheduledStart != null && a.ScheduledStart.Value >= nowUtc && ActiveAppointmentStatuses.Contains(a.Status))
            .OrderBy(a => a.ScheduledStart)
            .ToListAsync(ct);

        var warnings = new List<string>();
        if (futureAppointments.Any())
        {
            warnings.Add($"Dịch vụ '{service.ServiceName}' đang có {futureAppointments.Count} lịch hẹn sắp tới chưa hoàn thành. Ngưng kích hoạt dịch vụ có thể ảnh hưởng đến điều trị của bệnh nhân.");
        }

        var affectedDates = futureAppointments
            .Where(a => a.ScheduledStart.HasValue)
            .Select(a => ClinicTimeHelper.ToClinicTime(a.ScheduledStart!.Value).ToString("dd/MM/yyyy HH:mm"))
            .Distinct()
            .Take(10)
            .ToList();

        var dto = new ServiceDeactivationImpactDto
        {
            DepartmentServiceId = service.DepartmentServiceId,
            ServiceName = service.ServiceName,
            FutureAppointmentsCount = futureAppointments.Count,
            AffectedDates = affectedDates,
            Warnings = warnings
        };

        return ApiResponse<ServiceDeactivationImpactDto>.Ok(dto);
    }

    public async Task<ApiResponse<RoomChairDeactivationImpactDto>> PreviewRoomDeactivationImpactAsync(long roomId, CancellationToken ct = default)
    {
        var room = await _dbContext.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomId == roomId, ct);

        if (room == null)
        {
            return ApiResponse<RoomChairDeactivationImpactDto>.Fail("Không tìm thấy phòng điều trị.");
        }

        var nowUtc = DateTime.UtcNow;
        var futureApptCount = await _dbContext.Appointments
            .AsNoTracking()
            .CountAsync(a => a.RoomId == roomId && a.ScheduledStart != null && a.ScheduledStart.Value >= nowUtc && ActiveAppointmentStatuses.Contains(a.Status), ct);

        var warnings = new List<string>();
        if (futureApptCount > 0)
        {
            warnings.Add($"Phòng '{room.RoomCode} - {room.RoomName}' đang có {futureApptCount} lịch hẹn được gán trong tương lai.");
        }

        var dto = new RoomChairDeactivationImpactDto
        {
            TargetId = room.RoomId,
            TargetType = "Room",
            Name = $"{room.RoomCode} - {room.RoomName}",
            FutureAppointmentsCount = futureApptCount,
            Warnings = warnings
        };

        return ApiResponse<RoomChairDeactivationImpactDto>.Ok(dto);
    }

    public async Task<ApiResponse<RoomChairDeactivationImpactDto>> PreviewChairDeactivationImpactAsync(long chairId, CancellationToken ct = default)
    {
        var chair = await _dbContext.DentalChairs
            .AsNoTracking()
            .Include(c => c.Room)
            .FirstOrDefaultAsync(c => c.DentalChairId == chairId, ct);

        if (chair == null)
        {
            return ApiResponse<RoomChairDeactivationImpactDto>.Fail("Không tìm thấy ghế nha khoa.");
        }

        var nowUtc = DateTime.UtcNow;
        var futureApptCount = await _dbContext.Appointments
            .AsNoTracking()
            .CountAsync(a => a.DentalChairId == chairId && a.ScheduledStart != null && a.ScheduledStart.Value >= nowUtc && ActiveAppointmentStatuses.Contains(a.Status), ct);

        var warnings = new List<string>();
        if (futureApptCount > 0)
        {
            warnings.Add($"Ghế '{chair.ChairCode}' (Phòng {chair.Room?.RoomCode}) đang có {futureApptCount} lịch hẹn được gán trong tương lai.");
        }

        var dto = new RoomChairDeactivationImpactDto
        {
            TargetId = chair.DentalChairId,
            TargetType = "Chair",
            Name = $"{chair.ChairCode} ({chair.Room?.RoomCode})",
            FutureAppointmentsCount = futureApptCount,
            Warnings = warnings
        };

        return ApiResponse<RoomChairDeactivationImpactDto>.Ok(dto);
    }

    public async Task<ApiResponse<PriceChangeImpactDto>> PreviewPriceChangeImpactAsync(long serviceId, decimal newPrice, CancellationToken ct = default)
    {
        var service = await _dbContext.DepartmentServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == serviceId, ct);

        if (service == null)
        {
            return ApiResponse<PriceChangeImpactDto>.Fail("Không tìm thấy dịch vụ.");
        }

        var pastInvoicesCount = await _dbContext.Invoices
            .AsNoTracking()
            .CountAsync(i => i.Appointment.RequestedServiceId == serviceId, ct);

        var dto = new PriceChangeImpactDto
        {
            DepartmentServiceId = service.DepartmentServiceId,
            ServiceName = service.ServiceName,
            CurrentPrice = service.Price,
            NewPrice = newPrice,
            PastInvoicesCount = pastInvoicesCount,
            Message = $"Giá hiện tại là {service.Price:N0} đ. Giá mới sẽ là {newPrice:N0} đ. Toàn bộ {pastInvoicesCount} hóa đơn đã phát hành trước đây được bảo toàn theo giá snapshot gốc."
        };

        return ApiResponse<PriceChangeImpactDto>.Ok(dto);
    }

    public async Task<ApiResponse<bool>> ExecuteSafeUserStatusChangeAsync(long userId, string newStatus, string? reason, long adminUserId, CancellationToken ct = default)
    {
        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;

        try
        {
            var user = await _dbContext.UserAccounts
                .Include(u => u.StaffProfile)
                .FirstOrDefaultAsync(u => u.UserId == userId, ct);

            if (user == null)
            {
                return ApiResponse<bool>.Fail("Không tìm thấy người dùng.");
            }

            var oldStatus = user.Status;
            user.Status = newStatus;
            user.UpdatedAt = DateTime.UtcNow;

            _dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = adminUserId,
                Action = "AdminChangeUserStatus",
                EntityName = "UserAccounts",
                EntityId = userId.ToString(),
                OldValues = $"{{\"Status\":\"{oldStatus}\"}}",
                NewValues = $"{{\"Status\":\"{newStatus}\",\"Reason\":\"{reason?.Replace("\"", "\\\"")}\"}}",
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            _logger.LogInformation("Admin {AdminId} changed status of User {UserId} from {Old} to {New}. Reason: {Reason}", adminUserId, userId, oldStatus, newStatus, reason);
            return ApiResponse<bool>.Ok(true, $"Đã cập nhật trạng thái người dùng thành công sang '{newStatus}'.");
        }
        catch (Exception ex)
        {
            if (tx != null) await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Error changing status for user {UserId}", userId);
            throw;
        }
    }
}
