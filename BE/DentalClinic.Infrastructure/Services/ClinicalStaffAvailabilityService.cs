using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class ClinicalStaffAvailabilityService : IClinicalStaffAvailabilityService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly ILogger<ClinicalStaffAvailabilityService> _logger;

    public ClinicalStaffAvailabilityService(
        DentalClinicDbContext dbContext,
        ILogger<ClinicalStaffAvailabilityService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<(bool IsAvailable, string? Reason)> CheckDentistAvailabilityAsync(
        long dentistUserId,
        DateTime start,
        DateTime end,
        int? departmentId = null,
        long? excludeAppointmentId = null,
        CancellationToken ct = default)
    {
        if (dentistUserId <= 0)
        {
            return (false, "Mã bác sĩ không hợp lệ.");
        }

        var dentist = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.StaffProfile)
                .ThenInclude(s => s!.User)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null || dentist.StaffProfile == null || dentist.StaffProfile.User == null)
        {
            return (false, "Không tìm thấy hồ sơ bác sĩ.");
        }

        if (dentist.StaffProfile.EmploymentStatus != "Active" || dentist.StaffProfile.User.Status != AccountStatus.Active)
        {
            return (false, "Tài khoản bác sĩ hiện đang bị khóa hoặc đã ngừng công tác.");
        }

        if (departmentId.HasValue && dentist.DepartmentId != departmentId.Value)
        {
            return (false, "Bác sĩ không thuộc chuyên khoa được yêu cầu.");
        }

        var reqStartDate = DateOnly.FromDateTime(start);
        var reqEndDate = DateOnly.FromDateTime(end);

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffRole == UserRole.Dentist &&
                        l.StaffUserId == dentistUserId &&
                        l.Status == DentistLeaveStatus.Approved &&
                        l.StartDate <= reqEndDate &&
                        l.EndDate >= reqStartDate)
            .ToListAsync(ct);

        foreach (var leave in approvedLeaves)
        {
            if (!leave.StartTime.HasValue || !leave.EndTime.HasValue)
            {
                return (false, $"Bác sĩ đang trong thời gian nghỉ phép đã được phê duyệt (từ ngày {leave.StartDate:dd/MM/yyyy} đến {leave.EndDate:dd/MM/yyyy}).");
            }

            var overlapStart = leave.StartDate > reqStartDate ? leave.StartDate : reqStartDate;
            var overlapEnd = leave.EndDate < reqEndDate ? leave.EndDate : reqEndDate;

            for (var curDate = overlapStart; curDate <= overlapEnd; curDate = curDate.AddDays(1))
            {
                var leaveStartDt = curDate.ToDateTime(leave.StartTime.Value);
                var leaveEndDt = curDate.ToDateTime(leave.EndTime.Value);

                if (start < leaveEndDt && end > leaveStartDt)
                {
                    return (false, $"Bác sĩ đang trong thời gian nghỉ phép đã được phê duyệt ({leaveStartDt:dd/MM/yyyy HH\\:mm} - {leaveEndDt:HH\\:mm}).");
                }
            }
        }

        var activeStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var conflictQuery = _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(s => s.AssignedDentistUserId == dentistUserId &&
                        activeStatuses.Contains(s.Status) &&
                        s.ScheduledStart < end &&
                        s.ScheduledEnd > start);

        if (excludeAppointmentId.HasValue)
        {
            conflictQuery = conflictQuery.Where(s => s.AppointmentId != excludeAppointmentId.Value);
        }

        var hasConflict = await conflictQuery.AnyAsync(ct);

        if (hasConflict)
        {
            return (false, "Bác sĩ đã có lịch hẹn khác trong khung giờ này.");
        }

        return (true, null);
    }

    public async Task<(bool IsAvailable, string? Reason)> CheckChairAvailabilityAsync(
        int chairId,
        DateTime start,
        DateTime end,
        long? excludeAppointmentId = null,
        CancellationToken ct = default)
    {
        if (chairId <= 0)
        {
            return (false, "Mã ghế nha khoa không hợp lệ.");
        }

        var chair = await _dbContext.DentalChairs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.DentalChairId == chairId, ct);

        if (chair == null)
        {
            return (false, "Không tìm thấy ghế nha khoa.");
        }

        if (string.Equals(chair.Status, "Maintenance", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Ghế nha khoa '{chair.ChairCode}' đang trong quá trình bảo trì. Không thể phân bổ hoặc bắt đầu điều trị.");
        }

        if (string.Equals(chair.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Ghế nha khoa '{chair.ChairCode}' đã ngừng hoạt động.");
        }

        if (!string.Equals(chair.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Ghế nha khoa '{chair.ChairCode}' hiện không khả dụng (Trạng thái: {chair.Status}).");
        }

        var activeStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var query = _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(s => s.DentalChairId == chairId &&
                        activeStatuses.Contains(s.Status) &&
                        s.ScheduledStart < end &&
                        s.ScheduledEnd > start);

        if (excludeAppointmentId.HasValue)
        {
            query = query.Where(s => s.AppointmentId != excludeAppointmentId.Value);
        }

        var conflict = await query.FirstOrDefaultAsync(ct);
        if (conflict != null)
        {
            return (false, $"Ghế nha khoa '{chair.ChairCode}' đã được sử dụng cho ca khám khác trong khung giờ này.");
        }

        return (true, null);
    }

    public async Task<(bool CanApprove, string? Reason)> ValidateXrayShiftLeaveCoverageAsync(
        long dentistUserId,
        DateOnly startDate,
        DateOnly endDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        CancellationToken ct = default)
    {
        var xraySchedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Where(ws => ws.StaffRole == UserRole.Dentist &&
                        ws.StaffUserId == dentistUserId &&
                        ws.WorkDate >= startDate &&
                        ws.WorkDate <= endDate &&
                        ws.Status == "Scheduled" &&
                        ws.Notes != null &&
                        (ws.Notes.Contains("X-quang") || ws.Notes.Contains("X-ray") || ws.Notes.Contains("R003")))
            .ToListAsync(ct);

        if (!xraySchedules.Any())
        {
            return (true, null);
        }

        foreach (var shift in xraySchedules)
        {
            if (startTime.HasValue && endTime.HasValue)
            {
                if (shift.EndTime <= startTime.Value || shift.StartTime >= endTime.Value)
                {
                    continue;
                }
            }

            var otherXrayDentists = await _dbContext.StaffWorkSchedules
                .AsNoTracking()
                .Where(ws => ws.StaffRole == UserRole.Dentist &&
                            ws.StaffUserId != dentistUserId &&
                            ws.WorkDate == shift.WorkDate &&
                            ws.Status == "Scheduled" &&
                            ws.StartTime < shift.EndTime &&
                            ws.EndTime > shift.StartTime &&
                            ws.Notes != null &&
                            (ws.Notes.Contains("X-quang") || ws.Notes.Contains("X-ray") || ws.Notes.Contains("R003")))
                .Select(ws => ws.StaffUserId)
                .Distinct()
                .ToListAsync(ct);

            int availableOtherCount = 0;
            foreach (var otherDentistId in otherXrayDentists)
            {
                var isOnLeave = await _dbContext.StaffLeaveRequests
                    .AsNoTracking()
                    .AnyAsync(l => l.StaffRole == UserRole.Dentist &&
                                   l.StaffUserId == otherDentistId &&
                                   l.Status == DentistLeaveStatus.Approved &&
                                   l.StartDate <= shift.WorkDate &&
                                   l.EndDate >= shift.WorkDate &&
                                   (!l.StartTime.HasValue || !l.EndTime.HasValue ||
                                    (l.StartTime.Value < shift.EndTime && l.EndTime.Value > shift.StartTime)), ct);

                if (!isOnLeave)
                {
                    availableOtherCount++;
                }
            }

            if (availableOtherCount < 1)
            {
                return (false, $"Không thể phê duyệt đơn nghỉ phép: Phòng chụp X-quang yêu cầu duy trì tối thiểu một bác sĩ trực trong ca ngày {shift.WorkDate:dd/MM/yyyy} ({shift.StartTime:HH\\:mm} - {shift.EndTime:HH\\:mm}). Nếu phê duyệt, ca trực sẽ không có bác sĩ phụ trách.");
            }
        }

        return (true, null);
    }

    public async Task<List<long>> GetAvailableDentistUserIdsAsync(
        int departmentId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        var dentists = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.StaffProfile)
                .ThenInclude(sp => sp!.User)
            .Where(d => d.DepartmentId == departmentId &&
                        d.StaffProfile != null &&
                        d.StaffProfile.EmploymentStatus == "Active" &&
                        d.StaffProfile.User != null &&
                        d.StaffProfile.User.Status == AccountStatus.Active)
            .Select(d => d.UserId)
            .ToListAsync(ct);

        var availableDentistIds = new List<long>();
        foreach (var dId in dentists)
        {
            var (isAvail, _) = await CheckDentistAvailabilityAsync(dId, start, end, departmentId, null, ct);
            if (isAvail)
            {
                availableDentistIds.Add(dId);
            }
        }

        return availableDentistIds;
    }
}
