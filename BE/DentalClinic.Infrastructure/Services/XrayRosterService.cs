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

public class XrayRosterService : IXrayRosterService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly ILogger<XrayRosterService> _logger;

    public XrayRosterService(DentalClinicDbContext dbContext, ILogger<XrayRosterService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<XrayDentistRosterDto>> GetRostersAsync(XrayRosterFilterRequest request, CancellationToken ct = default)
    {
        var query = _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.User)
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.DentistProfile!)
                    .ThenInclude(dp => dp.Department)
                        .ThenInclude(d => d.Rooms)
            .Where(ws => ws.StaffRole == "Dentist");

        if (request.WorkDate.HasValue)
        {
            query = query.Where(ws => ws.WorkDate == request.WorkDate.Value);
        }

        if (request.DentistUserId.HasValue)
        {
            query = query.Where(ws => ws.StaffUserId == request.DentistUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var mappedStatus = request.Status == "Active" ? "Scheduled" : request.Status;
            query = query.Where(ws => ws.Status == mappedStatus || ws.Status == request.Status);
        }

        if (request.RoomId.HasValue)
        {
            var roomIdInt = (int)request.RoomId.Value;
            query = query.Where(ws => ws.StaffUser.DentistProfile != null &&
                                      ws.StaffUser.DentistProfile.Department.Rooms.Any(rm => rm.RoomId == roomIdInt));
        }

        var schedules = await query
            .OrderBy(ws => ws.WorkDate)
            .ThenBy(ws => ws.StartTime)
            .ToListAsync(ct);

        var dentistIds = schedules.Select(ws => ws.StaffUserId).Distinct().ToList();
        var leaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffRole == "Dentist" && dentistIds.Contains(l.StaffUserId) && l.Status == "Approved")
            .ToListAsync(ct);

        return schedules.Select(ws => MapToDto(ws, leaves, request.RoomId.HasValue ? (int?)request.RoomId.Value : null)).ToList();
    }

    public async Task<XrayDentistRosterDto?> GetRosterByIdAsync(long rosterId, CancellationToken ct = default)
    {
        var ws = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(x => x.StaffUser)
                .ThenInclude(sp => sp.User)
            .Include(x => x.StaffUser)
                .ThenInclude(sp => sp.DentistProfile!)
                    .ThenInclude(dp => dp.Department)
                        .ThenInclude(d => d.Rooms)
            .FirstOrDefaultAsync(x => x.WorkScheduleId == rosterId && x.StaffRole == "Dentist", ct);

        if (ws == null) return null;

        var leaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffRole == "Dentist" && l.StaffUserId == ws.StaffUserId && l.Status == "Approved")
            .ToListAsync(ct);

        return MapToDto(ws, leaves, null);
    }

    public async Task<XrayDentistRosterDto?> GetActiveRosterForRoomAsync(long roomId, DateTime atDateTimeUtc, CancellationToken ct = default)
    {
        var vnZone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(atDateTimeUtc, vnZone);
        var workDate = DateOnly.FromDateTime(localDateTime);
        var timeOfDay = TimeOnly.FromDateTime(localDateTime);
        var roomIdInt = (int)roomId;

        var ws = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(x => x.StaffUser)
                .ThenInclude(sp => sp.User)
            .Include(x => x.StaffUser)
                .ThenInclude(sp => sp.DentistProfile!)
                    .ThenInclude(dp => dp.Department)
                        .ThenInclude(d => d.Rooms)
            .Where(x => x.StaffRole == "Dentist" &&
                        x.WorkDate == workDate &&
                        x.Status == "Scheduled" &&
                        x.StartTime <= timeOfDay &&
                        x.EndTime >= timeOfDay &&
                        x.StaffUser.DentistProfile != null &&
                        x.StaffUser.DentistProfile.Department.Rooms.Any(rm => rm.RoomId == roomIdInt))
            .FirstOrDefaultAsync(ct);

        if (ws == null) return null;

        var leaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffRole == "Dentist" && l.StaffUserId == ws.StaffUserId && l.Status == "Approved")
            .ToListAsync(ct);

        return MapToDto(ws, leaves, roomIdInt);
    }

    public async Task<List<QualifiedDentistDto>> GetQualifiedDentistsAsync(CancellationToken ct = default)
    {
        var dentists = await _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.StaffProfile)
                .ThenInclude(s => s!.DentistProfile)
                    .ThenInclude(dp => dp!.Department)
            .Where(u => u.Role == UserRole.Dentist && u.Status == "Active" && u.StaffProfile != null && u.StaffProfile.DentistProfile != null)
            .ToListAsync(ct);

        return dentists.Select(u => new QualifiedDentistDto
        {
            UserId = u.UserId,
            FullName = u.StaffProfile?.FullName ?? u.Email ?? $"Bác sĩ #{u.UserId}",
            Email = u.Email ?? string.Empty,
            PhoneNumber = u.PhoneNumber,
            LicenseNumber = u.StaffProfile?.DentistProfile?.LicenseNumber,
            Qualification = u.StaffProfile?.DentistProfile?.Qualification,
            HasRadiationSafetyCert = !string.IsNullOrWhiteSpace(u.StaffProfile?.DentistProfile?.LicenseNumber),
            RadiationCertNumber = u.StaffProfile?.DentistProfile?.LicenseNumber,
            RadiationCertExpiryDate = null
        })
        .OrderByDescending(d => d.HasRadiationSafetyCert)
        .ThenBy(d => d.FullName)
        .ToList();
    }

    public async Task<XrayDentistRosterDto> AssignRosterAsync(AssignXrayRosterRequest request, long adminUserId, CancellationToken ct = default)
    {
        if (request.EndTime <= request.StartTime)
        {
            throw new InvalidOperationException("Thời gian kết thúc ca trực phải lớn hơn thời gian bắt đầu.");
        }

        var dentistProfile = await _dbContext.DentistProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == request.DentistUserId, ct);

        if (dentistProfile == null)
        {
            throw new InvalidOperationException("Không tìm thấy hồ sơ bác sĩ.");
        }

        var onLeave = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .AnyAsync(l => l.StaffRole == "Dentist" &&
                           l.StaffUserId == request.DentistUserId &&
                           l.Status == "Approved" &&
                           l.StartDate <= request.WorkDate &&
                           l.EndDate >= request.WorkDate, ct);

        if (onLeave)
        {
            throw new InvalidOperationException($"Bác sĩ đang trong thời gian nghỉ phép đã được duyệt vào ngày {request.WorkDate:dd/MM/yyyy}, không thể phân công trực.");
        }

        var startDateTime = request.WorkDate.ToDateTime(request.StartTime);
        var endDateTime = request.WorkDate.ToDateTime(request.EndTime);

        var clinicalConflict = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(a => a.AssignedDentistUserId == request.DentistUserId &&
                        (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.InService) &&
                        a.ScheduledStart < endDateTime &&
                        a.ScheduledEnd > startDateTime)
            .FirstOrDefaultAsync(ct);

        if (clinicalConflict != null)
        {
            throw new InvalidOperationException(
                $"Bác sĩ đã có lịch hẹn dịch vụ (#{clinicalConflict.AppointmentServiceId}) " +
                $"trong khung giờ {clinicalConflict.ScheduledStart:HH\\:mm} - {clinicalConflict.ScheduledEnd:HH\\:mm}.");
        }

        var schedule = new StaffWorkSchedule
        {
            StaffUserId = request.DentistUserId,
            StaffRole = "Dentist",
            WorkDate = request.WorkDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = "Scheduled",
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.StaffWorkSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Assigned dentist work schedule #{WorkScheduleId}: Dentist {DentistId}, Date {Date}",
            schedule.WorkScheduleId, schedule.StaffUserId, schedule.WorkDate);

        return (await GetRosterByIdAsync(schedule.WorkScheduleId, ct))!;
    }

    public async Task<XrayDentistRosterDto> ReplaceDentistAsync(long rosterId, ReplaceXrayDentistRequest request, long adminUserId, CancellationToken ct = default)
    {
        var schedule = await _dbContext.StaffWorkSchedules
            .FirstOrDefaultAsync(r => r.WorkScheduleId == rosterId && r.StaffRole == "Dentist", ct);

        if (schedule == null)
        {
            throw new InvalidOperationException("Không tìm thấy ca làm việc của bác sĩ.");
        }

        if (schedule.Status != "Scheduled")
        {
            throw new InvalidOperationException($"Không thể điều chuyển ca làm việc ở trạng thái '{schedule.Status}'.");
        }

        var dentistProfile = await _dbContext.DentistProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == request.NewDentistUserId, ct);

        if (dentistProfile == null)
        {
            throw new InvalidOperationException("Không tìm thấy hồ sơ bác sĩ thay thế.");
        }

        var startDateTime = schedule.WorkDate.ToDateTime(schedule.StartTime);
        var endDateTime = schedule.WorkDate.ToDateTime(schedule.EndTime);

        var clinicalConflict = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(a => a.AssignedDentistUserId == request.NewDentistUserId &&
                        (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.InService) &&
                        a.ScheduledStart < endDateTime &&
                        a.ScheduledEnd > startDateTime)
            .FirstOrDefaultAsync(ct);

        if (clinicalConflict != null)
        {
            throw new InvalidOperationException($"Bác sĩ thay thế đã có lịch khám (#{clinicalConflict.AppointmentServiceId}) trong khoảng thời gian này.");
        }

        schedule.StaffUserId = request.NewDentistUserId;
        schedule.Notes = string.IsNullOrWhiteSpace(schedule.Notes)
            ? $"[Điều chuyển]: {request.Reason.Trim()}"
            : $"{schedule.Notes} | [Điều chuyển]: {request.Reason.Trim()}";
        schedule.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return (await GetRosterByIdAsync(schedule.WorkScheduleId, ct))!;
    }

    public async Task<bool> CancelRosterAsync(long rosterId, string reason, long adminUserId, CancellationToken ct = default)
    {
        var schedule = await _dbContext.StaffWorkSchedules
            .FirstOrDefaultAsync(r => r.WorkScheduleId == rosterId && r.StaffRole == "Dentist", ct);

        if (schedule == null) return false;

        schedule.Status = "Cancelled";
        schedule.Notes = string.IsNullOrWhiteSpace(schedule.Notes)
            ? $"[Hủy ca trực]: {reason.Trim()}"
            : $"{schedule.Notes} | [Hủy ca trực]: {reason.Trim()}";
        schedule.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> IsDentistOnXrayDutyAsync(long dentistUserId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default)
    {
        var vnZone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(startUtc, vnZone);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(endUtc, vnZone);

        var startDate = DateOnly.FromDateTime(localStart);
        var startTime = TimeOnly.FromDateTime(localStart);
        var endTime = TimeOnly.FromDateTime(localEnd);

        return await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .AnyAsync(r => r.StaffRole == "Dentist" &&
                           r.StaffUserId == dentistUserId &&
                           r.WorkDate == startDate &&
                           r.Status == "Scheduled" &&
                           r.StartTime < endTime &&
                           r.EndTime > startTime, ct);
    }

    public async Task<List<XrayDentistRosterDto>> GetDentistXrayScheduleAsync(long dentistUserId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var schedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(r => r.StaffUser)
                .ThenInclude(sp => sp.User)
            .Include(r => r.StaffUser)
                .ThenInclude(sp => sp.DentistProfile!)
                    .ThenInclude(dp => dp.Department)
                        .ThenInclude(d => d.Rooms)
            .Where(r => r.StaffRole == "Dentist" &&
                        r.StaffUserId == dentistUserId &&
                        r.WorkDate >= fromDate &&
                        r.WorkDate <= toDate &&
                        r.Status == "Scheduled")
            .OrderBy(r => r.WorkDate)
            .ThenBy(r => r.StartTime)
            .ToListAsync(ct);

        var leaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffRole == "Dentist" && l.StaffUserId == dentistUserId && l.Status == "Approved")
            .ToListAsync(ct);

        return schedules.Select(ws => MapToDto(ws, leaves, null)).ToList();
    }

    private static XrayDentistRosterDto MapToDto(StaffWorkSchedule ws, List<StaffLeaveRequest> leaves, int? preferredRoomId)
    {
        var isLeave = leaves.Any(l => l.StaffUserId == ws.StaffUserId && l.StartDate <= ws.WorkDate && l.EndDate >= ws.WorkDate);
        var deptRooms = ws.StaffUser?.DentistProfile?.Department?.Rooms;
        var room = preferredRoomId.HasValue
            ? deptRooms?.FirstOrDefault(rm => rm.RoomId == preferredRoomId.Value)
            : deptRooms?.FirstOrDefault();

        return new XrayDentistRosterDto
        {
            RosterId = ws.WorkScheduleId,
            RoomId = room?.RoomId ?? 0,
            RoomCode = room?.RoomCode ?? "XRAY-01",
            RoomName = room?.RoomName ?? ws.StaffUser?.DentistProfile?.Department?.Name ?? "Phòng Chẩn đoán hình ảnh",
            EquipmentId = 0,
            EquipmentCode = "DIAG-01",
            EquipmentName = "Hệ thống Chẩn đoán hình ảnh",
            DentistUserId = ws.StaffUserId,
            DentistFullName = ws.StaffUser?.FullName ?? ws.StaffUser?.User?.Email ?? $"Bác sĩ #{ws.StaffUserId}",
            LicenseNumber = ws.StaffUser?.DentistProfile?.LicenseNumber,
            HasRadiationSafetyCert = !string.IsNullOrWhiteSpace(ws.StaffUser?.DentistProfile?.LicenseNumber),
            RadiationCertNumber = ws.StaffUser?.DentistProfile?.LicenseNumber,
            BackupDentistUserId = null,
            BackupDentistFullName = null,
            WorkDate = ws.WorkDate,
            ShiftType = ws.StartTime.Hour < 12 ? "Morning" : "Afternoon",
            StartTime = ws.StartTime,
            EndTime = ws.EndTime,
            Status = ws.Status == "Scheduled" ? "Active" : ws.Status,
            Notes = ws.Notes,
            IsDentistOnLeave = isLeave,
            CreatedAtUtc = ws.CreatedAt
        };
    }
}
