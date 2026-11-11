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
        var query = _dbContext.XrayDentistRosters
            .AsNoTracking()
            .Include(r => r.Room)
            .Include(r => r.Equipment)
            .Include(r => r.DentistUser)
                .ThenInclude(u => u.StaffProfile)
                    .ThenInclude(s => s!.DentistProfile)
            .Include(r => r.BackupDentistUser)
                .ThenInclude(u => u!.StaffProfile)
            .AsQueryable();

        if (request.WorkDate.HasValue)
        {
            query = query.Where(r => r.WorkDate == request.WorkDate.Value);
        }

        if (request.RoomId.HasValue)
        {
            query = query.Where(r => r.RoomId == request.RoomId.Value);
        }

        if (request.DentistUserId.HasValue)
        {
            query = query.Where(r => r.DentistUserId == request.DentistUserId.Value || r.BackupDentistUserId == request.DentistUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(r => r.Status == request.Status);
        }

        var rosters = await query
            .OrderBy(r => r.WorkDate)
            .ThenBy(r => r.StartTime)
            .ToListAsync(ct);

        // Fetch leave requests for dentists to check leave status
        var dentistIds = rosters.Select(r => r.DentistUserId).Distinct().ToList();
        var leaves = await _dbContext.DentistLeaveRequests
            .AsNoTracking()
            .Where(l => dentistIds.Contains(l.DentistUserId) && l.Status == "Approved")
            .ToListAsync(ct);

        return rosters.Select(r => MapToDto(r, leaves)).ToList();
    }

    public async Task<XrayDentistRosterDto?> GetRosterByIdAsync(long rosterId, CancellationToken ct = default)
    {
        var r = await _dbContext.XrayDentistRosters
            .AsNoTracking()
            .Include(x => x.Room)
            .Include(x => x.Equipment)
            .Include(x => x.DentistUser)
                .ThenInclude(u => u.StaffProfile)
                    .ThenInclude(s => s!.DentistProfile)
            .Include(x => x.BackupDentistUser)
                .ThenInclude(u => u!.StaffProfile)
            .FirstOrDefaultAsync(x => x.RosterId == rosterId, ct);

        if (r == null) return null;

        var leaves = await _dbContext.DentistLeaveRequests
            .AsNoTracking()
            .Where(l => l.DentistUserId == r.DentistUserId && l.Status == "Approved")
            .ToListAsync(ct);

        return MapToDto(r, leaves);
    }

    public async Task<XrayDentistRosterDto?> GetActiveRosterForRoomAsync(long roomId, DateTime atDateTimeUtc, CancellationToken ct = default)
    {
        // Convert to local clinic time (+7 GMT for Vietnam)
        var vnZone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(atDateTimeUtc, vnZone);
        var workDate = DateOnly.FromDateTime(localDateTime);
        var timeOfDay = TimeOnly.FromDateTime(localDateTime);

        var r = await _dbContext.XrayDentistRosters
            .AsNoTracking()
            .Include(x => x.Room)
            .Include(x => x.Equipment)
            .Include(x => x.DentistUser)
                .ThenInclude(u => u.StaffProfile)
                    .ThenInclude(s => s!.DentistProfile)
            .Include(x => x.BackupDentistUser)
                .ThenInclude(u => u!.StaffProfile)
            .Where(x => x.RoomId == roomId &&
                        x.WorkDate == workDate &&
                        x.Status == "Active" &&
                        x.StartTime <= timeOfDay &&
                        x.EndTime >= timeOfDay)
            .FirstOrDefaultAsync(ct);

        if (r == null) return null;

        var leaves = await _dbContext.DentistLeaveRequests
            .AsNoTracking()
            .Where(l => l.DentistUserId == r.DentistUserId && l.Status == "Approved")
            .ToListAsync(ct);

        return MapToDto(r, leaves);
    }

    public async Task<List<QualifiedDentistDto>> GetQualifiedDentistsAsync(CancellationToken ct = default)
    {
        var dentists = await _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.StaffProfile)
                .ThenInclude(s => s!.DentistProfile)
            .Where(u => u.Role == UserRole.Dentist && u.Status == "Active")
            .ToListAsync(ct);

        return dentists.Select(u => new QualifiedDentistDto
        {
            UserId = u.UserId,
            FullName = u.StaffProfile?.FullName ?? u.Email,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            LicenseNumber = u.StaffProfile?.DentistProfile?.LicenseNumber,
            Qualification = u.StaffProfile?.DentistProfile?.Qualification,
            HasRadiationSafetyCert = u.StaffProfile?.DentistProfile?.HasRadiationSafetyCert ?? false,
            RadiationCertNumber = u.StaffProfile?.DentistProfile?.RadiationCertNumber,
            RadiationCertExpiryDate = u.StaffProfile?.DentistProfile?.RadiationCertExpiryDate
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

        // 1. Qualification Check: Dentist MUST have Radiation Safety Certificate
        var dentistProfile = await _dbContext.DentistProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == request.DentistUserId, ct);

        if (dentistProfile == null || !dentistProfile.HasRadiationSafetyCert)
        {
            throw new InvalidOperationException("Bác sĩ chưa có chứng chỉ an toàn bức xạ, không đủ điều kiện phân công trực phòng X-quang.");
        }

        if (request.BackupDentistUserId.HasValue && request.BackupDentistUserId.Value > 0)
        {
            var backupProfile = await _dbContext.DentistProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == request.BackupDentistUserId.Value, ct);

            if (backupProfile == null || !backupProfile.HasRadiationSafetyCert)
            {
                throw new InvalidOperationException("Bác sĩ dự phòng chưa có chứng chỉ an toàn bức xạ.");
            }
        }

        // 2. Conflict Check with Approved Leave
        var onLeave = await _dbContext.DentistLeaveRequests
            .AsNoTracking()
            .AnyAsync(l => l.DentistUserId == request.DentistUserId &&
                           l.Status == "Approved" &&
                           l.StartDate <= request.WorkDate &&
                           l.EndDate >= request.WorkDate, ct);

        if (onLeave)
        {
            throw new InvalidOperationException($"Bác sĩ đang trong thời gian nghỉ phép đã được duyệt vào ngày {request.WorkDate:dd/MM/yyyy}, không thể phân công trực.");
        }

        // 3. Conflict Check with Normal Clinical Appointments
        var startDateTime = request.WorkDate.ToDateTime(request.StartTime);
        var endDateTime = request.WorkDate.ToDateTime(request.EndTime);

        var clinicalConflict = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.PatientUser)
                .ThenInclude(p => p.User)
            .Where(a => a.AssignedDentistUserId == request.DentistUserId &&
                        (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.InService) &&
                        a.ScheduledStart < endDateTime &&
                        a.ScheduledEnd > startDateTime)
            .FirstOrDefaultAsync(ct);

        if (clinicalConflict != null)
        {
            throw new InvalidOperationException(
                $"Bác sĩ đã có lịch hẹn khám lâm sàng ({clinicalConflict.AppointmentCode}) " +
                $"trong khung giờ {clinicalConflict.ScheduledStart:HH\\:mm} - {clinicalConflict.ScheduledEnd:HH\\:mm}. " +
                $"Vui lòng điều chuyển lịch khám trước khi phân công trực phòng X-quang.");
        }

        // 4. Overlap Check for Room / Equipment
        var roomConflict = await _dbContext.XrayDentistRosters
            .AsNoTracking()
            .AnyAsync(r => r.RoomId == request.RoomId &&
                           r.WorkDate == request.WorkDate &&
                           r.Status == "Active" &&
                           r.StartTime < request.EndTime &&
                           r.EndTime > request.StartTime, ct);

        if (roomConflict)
        {
            throw new InvalidOperationException("Phòng X-quang đã có bác sĩ trực trong khung giờ này.");
        }

        var roster = new XrayDentistRoster
        {
            RoomId = request.RoomId,
            EquipmentId = request.EquipmentId,
            DentistUserId = request.DentistUserId,
            BackupDentistUserId = request.BackupDentistUserId > 0 ? request.BackupDentistUserId : null,
            WorkDate = request.WorkDate,
            ShiftType = string.IsNullOrWhiteSpace(request.ShiftType) ? "Morning" : request.ShiftType.Trim(),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = "Active",
            Notes = request.Notes?.Trim(),
            CreatedByUserId = adminUserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.XrayDentistRosters.Add(roster);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Assigned X-ray duty roster #{RosterId}: Room {RoomId}, Dentist {DentistId}, Date {Date}, Shift {Shift}",
            roster.RosterId, roster.RoomId, roster.DentistUserId, roster.WorkDate, roster.ShiftType);

        return (await GetRosterByIdAsync(roster.RosterId, ct))!;
    }

    public async Task<XrayDentistRosterDto> ReplaceDentistAsync(long rosterId, ReplaceXrayDentistRequest request, long adminUserId, CancellationToken ct = default)
    {
        var roster = await _dbContext.XrayDentistRosters
            .FirstOrDefaultAsync(r => r.RosterId == rosterId, ct);

        if (roster == null)
        {
            throw new InvalidOperationException("Không tìm thấy phân công trực phòng X-quang.");
        }

        if (roster.Status != "Active")
        {
            throw new InvalidOperationException($"Không thể điều chuyển ca trực ở trạng thái '{roster.Status}'.");
        }

        // Qualification check on replacement dentist
        var dentistProfile = await _dbContext.DentistProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == request.NewDentistUserId, ct);

        if (dentistProfile == null || !dentistProfile.HasRadiationSafetyCert)
        {
            throw new InvalidOperationException("Bác sĩ thay thế chưa có chứng chỉ an toàn bức xạ.");
        }

        // Conflict check with clinical appointments
        var startDateTime = roster.WorkDate.ToDateTime(roster.StartTime);
        var endDateTime = roster.WorkDate.ToDateTime(roster.EndTime);

        var clinicalConflict = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.AssignedDentistUserId == request.NewDentistUserId &&
                        (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.InService) &&
                        a.ScheduledStart < endDateTime &&
                        a.ScheduledEnd > startDateTime)
            .FirstOrDefaultAsync(ct);

        if (clinicalConflict != null)
        {
            throw new InvalidOperationException($"Bác sĩ thay thế đã có lịch khám lâm sàng ({clinicalConflict.AppointmentCode}) trong khoảng thời gian này.");
        }

        roster.DentistUserId = request.NewDentistUserId;
        if (request.NewBackupDentistUserId.HasValue && request.NewBackupDentistUserId.Value > 0)
        {
            roster.BackupDentistUserId = request.NewBackupDentistUserId.Value;
        }
        roster.Notes = string.IsNullOrWhiteSpace(roster.Notes)
            ? $"[Điều chuyển]: {request.Reason.Trim()}"
            : $"{roster.Notes} | [Điều chuyển]: {request.Reason.Trim()}";
        roster.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Replaced dentist on X-ray duty roster #{RosterId} with Dentist #{NewDentistId}", rosterId, request.NewDentistUserId);

        return (await GetRosterByIdAsync(roster.RosterId, ct))!;
    }

    public async Task<bool> CancelRosterAsync(long rosterId, string reason, long adminUserId, CancellationToken ct = default)
    {
        var roster = await _dbContext.XrayDentistRosters
            .FirstOrDefaultAsync(r => r.RosterId == rosterId, ct);

        if (roster == null) return false;

        roster.Status = "Cancelled";
        roster.Notes = string.IsNullOrWhiteSpace(roster.Notes)
            ? $"[Hủy ca trực]: {reason.Trim()}"
            : $"{roster.Notes} | [Hủy ca trực]: {reason.Trim()}";
        roster.UpdatedAtUtc = DateTime.UtcNow;

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

        return await _dbContext.XrayDentistRosters
            .AsNoTracking()
            .AnyAsync(r => r.DentistUserId == dentistUserId &&
                           r.WorkDate == startDate &&
                           r.Status == "Active" &&
                           r.StartTime < endTime &&
                           r.EndTime > startTime, ct);
    }

    public async Task<List<XrayDentistRosterDto>> GetDentistXrayScheduleAsync(long dentistUserId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var query = await _dbContext.XrayDentistRosters
            .AsNoTracking()
            .Include(r => r.Room)
            .Include(r => r.Equipment)
            .Include(r => r.DentistUser)
                .ThenInclude(u => u.StaffProfile)
                    .ThenInclude(s => s!.DentistProfile)
            .Include(r => r.BackupDentistUser)
                .ThenInclude(u => u!.StaffProfile)
            .Where(r => (r.DentistUserId == dentistUserId || r.BackupDentistUserId == dentistUserId) &&
                        r.WorkDate >= fromDate &&
                        r.WorkDate <= toDate &&
                        r.Status == "Active")
            .OrderBy(r => r.WorkDate)
            .ThenBy(r => r.StartTime)
            .ToListAsync(ct);

        var leaves = await _dbContext.DentistLeaveRequests
            .AsNoTracking()
            .Where(l => l.DentistUserId == dentistUserId && l.Status == "Approved")
            .ToListAsync(ct);

        return query.Select(r => MapToDto(r, leaves)).ToList();
    }

    private static XrayDentistRosterDto MapToDto(XrayDentistRoster r, List<DentistLeaveRequest> leaves)
    {
        var isLeave = leaves.Any(l => l.DentistUserId == r.DentistUserId && l.StartDate <= r.WorkDate && l.EndDate >= r.WorkDate);

        return new XrayDentistRosterDto
        {
            RosterId = r.RosterId,
            RoomId = r.RoomId,
            RoomCode = r.Room?.RoomCode ?? "XRAY-01",
            RoomName = r.Room?.RoomName ?? "Phòng Chụp X-quang",
            EquipmentId = r.EquipmentId,
            EquipmentCode = r.Equipment?.EquipmentCode ?? "XRAY-MACH-01",
            EquipmentName = r.Equipment?.EquipmentName ?? "Máy Chụp X-Quang Kỹ Thuật Số",
            DentistUserId = r.DentistUserId,
            DentistFullName = r.DentistUser?.StaffProfile?.FullName ?? r.DentistUser?.Email ?? $"Bác sĩ #{r.DentistUserId}",
            LicenseNumber = r.DentistUser?.StaffProfile?.DentistProfile?.LicenseNumber,
            HasRadiationSafetyCert = r.DentistUser?.StaffProfile?.DentistProfile?.HasRadiationSafetyCert ?? false,
            RadiationCertNumber = r.DentistUser?.StaffProfile?.DentistProfile?.RadiationCertNumber,
            BackupDentistUserId = r.BackupDentistUserId,
            BackupDentistFullName = r.BackupDentistUser?.StaffProfile?.FullName ?? (r.BackupDentistUserId.HasValue ? $"Bác sĩ #{r.BackupDentistUserId}" : null),
            WorkDate = r.WorkDate,
            ShiftType = r.ShiftType,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            Status = r.Status,
            Notes = r.Notes,
            IsDentistOnLeave = isLeave,
            CreatedAtUtc = r.CreatedAtUtc
        };
    }
}
