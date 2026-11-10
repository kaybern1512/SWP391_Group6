using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Attendance.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services.StaffShiftProviders;

public class ReceptionistShiftProvider : IStaffShiftProvider
{
    private readonly DentalClinicDbContext _dbContext;

    public ReceptionistShiftProvider(DentalClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string SupportedRole => UserRole.Receptionist;

    public async Task<List<StaffShiftDto>> GetShiftsForStaffAsync(long userId, DateOnly date, CancellationToken ct = default)
    {
        var schedules = await _dbContext.ReceptionistWorkSchedules
            .AsNoTracking()
            .Include(s => s.ShiftTemplate)
            .Include(s => s.ReceptionistUser)
                .ThenInclude(u => u.StaffProfile)
            .Where(s => s.ReceptionistUserId == userId && s.WorkDate == date && s.Status == "Scheduled" && s.IsPublished)
            .OrderBy(s => s.StartTime)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            return new List<StaffShiftDto>();
        }

        var approvedLeaves = await _dbContext.ReceptionistLeaveRequestPeriods
            .AsNoTracking()
            .Include(p => p.LeaveRequest)
            .Where(p => p.LeaveRequest.ReceptionistUserId == userId && p.LeaveDate == date && p.Status == "Approved")
            .ToListAsync(ct);

        var incidents = await _dbContext.ReceptionistCoverageIncidents
            .AsNoTracking()
            .Where(i => i.AffectedReceptionistUserId == userId && i.WorkDate == date && i.IncidentType == "EmergencyAbsence")
            .ToListAsync(ct);

        return schedules.Select(s => MapToStaffShiftDto(s, approvedLeaves, incidents)).ToList();
    }

    public async Task<List<StaffShiftDto>> GetAllShiftsForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var schedules = await _dbContext.ReceptionistWorkSchedules
            .AsNoTracking()
            .Include(s => s.ShiftTemplate)
            .Include(s => s.ReceptionistUser)
                .ThenInclude(u => u.StaffProfile)
            .Where(s => s.WorkDate == date && s.Status == "Scheduled" && s.IsPublished)
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.ReceptionistUserId)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            return new List<StaffShiftDto>();
        }

        var userIds = schedules.Select(s => s.ReceptionistUserId).Distinct().ToList();

        var approvedLeaves = await _dbContext.ReceptionistLeaveRequestPeriods
            .AsNoTracking()
            .Include(p => p.LeaveRequest)
            .Where(p => userIds.Contains(p.LeaveRequest.ReceptionistUserId) && p.LeaveDate == date && p.Status == "Approved")
            .ToListAsync(ct);

        var incidents = await _dbContext.ReceptionistCoverageIncidents
            .AsNoTracking()
            .Where(i => i.AffectedReceptionistUserId.HasValue && userIds.Contains(i.AffectedReceptionistUserId.Value) && i.WorkDate == date && i.IncidentType == "EmergencyAbsence")
            .ToListAsync(ct);

        return schedules.Select(s => MapToStaffShiftDto(s, approvedLeaves, incidents)).ToList();
    }

    public async Task<List<StaffShiftDto>> GetShiftsForStaffRangeAsync(long userId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var schedules = await _dbContext.ReceptionistWorkSchedules
            .AsNoTracking()
            .Include(s => s.ShiftTemplate)
            .Include(s => s.ReceptionistUser)
                .ThenInclude(u => u.StaffProfile)
            .Where(s => s.ReceptionistUserId == userId && s.WorkDate >= fromDate && s.WorkDate <= toDate && s.Status == "Scheduled" && s.IsPublished)
            .OrderBy(s => s.WorkDate)
            .ThenBy(s => s.StartTime)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            return new List<StaffShiftDto>();
        }

        var approvedLeaves = await _dbContext.ReceptionistLeaveRequestPeriods
            .AsNoTracking()
            .Include(p => p.LeaveRequest)
            .Where(p => p.LeaveRequest.ReceptionistUserId == userId && p.LeaveDate >= fromDate && p.LeaveDate <= toDate && p.Status == "Approved")
            .ToListAsync(ct);

        var incidents = await _dbContext.ReceptionistCoverageIncidents
            .AsNoTracking()
            .Where(i => i.AffectedReceptionistUserId == userId && i.WorkDate >= fromDate && i.WorkDate <= toDate && i.IncidentType == "EmergencyAbsence")
            .ToListAsync(ct);

        return schedules.Select(s => MapToStaffShiftDto(s, approvedLeaves, incidents)).ToList();
    }

    public async Task<List<StaffShiftDto>> GetAllShiftsRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var schedules = await _dbContext.ReceptionistWorkSchedules
            .AsNoTracking()
            .Include(s => s.ShiftTemplate)
            .Include(s => s.ReceptionistUser)
                .ThenInclude(u => u.StaffProfile)
            .Where(s => s.WorkDate >= fromDate && s.WorkDate <= toDate && s.Status == "Scheduled" && s.IsPublished)
            .OrderBy(s => s.WorkDate)
            .ThenBy(s => s.StartTime)
            .ThenBy(s => s.ReceptionistUserId)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            return new List<StaffShiftDto>();
        }

        var userIds = schedules.Select(s => s.ReceptionistUserId).Distinct().ToList();

        var approvedLeaves = await _dbContext.ReceptionistLeaveRequestPeriods
            .AsNoTracking()
            .Include(p => p.LeaveRequest)
            .Where(p => userIds.Contains(p.LeaveRequest.ReceptionistUserId) && p.LeaveDate >= fromDate && p.LeaveDate <= toDate && p.Status == "Approved")
            .ToListAsync(ct);

        var incidents = await _dbContext.ReceptionistCoverageIncidents
            .AsNoTracking()
            .Where(i => i.AffectedReceptionistUserId.HasValue && userIds.Contains(i.AffectedReceptionistUserId.Value) && i.WorkDate >= fromDate && i.WorkDate <= toDate && i.IncidentType == "EmergencyAbsence")
            .ToListAsync(ct);

        return schedules.Select(s => MapToStaffShiftDto(s, approvedLeaves, incidents)).ToList();
    }

    private static StaffShiftDto MapToStaffShiftDto(
        Persistence.Entities.ReceptionistWorkSchedule s,
        List<Persistence.Entities.ReceptionistLeaveRequestPeriod> approvedLeaves,
        List<Persistence.Entities.ReceptionistCoverageIncident> incidents)
    {
        var shiftName = s.ShiftTemplate?.TemplateName
            ?? $"Ca Lễ Tân ({s.StartTime:HH\\:mm} - {s.EndTime:HH\\:mm})";

        var dto = new StaffShiftDto
        {
            ShiftIdentifier = $"RECEP_SCHED_{s.ReceptionistScheduleId}",
            SourceScheduleId = s.ReceptionistScheduleId,
            SourceScheduleType = "ReceptionistWorkSchedule",
            UserId = s.ReceptionistUserId,
            StaffName = s.ReceptionistUser?.StaffProfile?.FullName ?? $"Lễ tân #{s.ReceptionistUserId}",
            StaffCode = s.ReceptionistUser?.StaffProfile?.EmployeeCode ?? $"REC-{s.ReceptionistUserId:D4}",
            Role = UserRole.Receptionist,
            WorkDate = s.WorkDate,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            ShiftName = shiftName,
            DepartmentId = null,
            DepartmentName = "Bộ phận Lễ tân & Tiếp đón",
            Status = "Scheduled",
            Note = s.Notes
        };

        // Check Approved Leaves
        var userLeavesOnDate = approvedLeaves
            .Where(p => p.LeaveRequest.ReceptionistUserId == s.ReceptionistUserId && p.LeaveDate == s.WorkDate)
            .ToList();

        foreach (var leave in userLeavesOnDate)
        {
            bool isFullDay = !leave.StartTime.HasValue && !leave.EndTime.HasValue;
            bool coversShift = leave.StartTime.HasValue && leave.EndTime.HasValue
                && leave.StartTime.Value <= s.StartTime && leave.EndTime.Value >= s.EndTime;

            if (isFullDay || coversShift)
            {
                dto.IsApprovedLeave = true;
                dto.Status = "ApprovedLeave";
                dto.LeaveType = leave.LeaveRequest.LeaveType;
                dto.LeaveHours = (decimal)(s.EndTime - s.StartTime).TotalHours;
                dto.Note = (dto.Note + $" | Nghỉ phép ({leave.LeaveRequest.LeaveType}): {leave.LeaveRequest.Reason}").TrimStart(' ', '|');
                break;
            }

            // Check partial overlap
            if (leave.StartTime.HasValue && leave.EndTime.HasValue
                && leave.StartTime.Value < s.EndTime && leave.EndTime.Value > s.StartTime)
            {
                dto.IsApprovedLeave = true;
                dto.Status = "PartialLeave";
                dto.LeaveType = leave.LeaveRequest.LeaveType;

                var overlapStart = leave.StartTime.Value > s.StartTime ? leave.StartTime.Value : s.StartTime;
                var overlapEnd = leave.EndTime.Value < s.EndTime ? leave.EndTime.Value : s.EndTime;
                var overlapHours = (decimal)(overlapEnd - overlapStart).TotalHours;

                dto.LeaveStartTime = overlapStart;
                dto.LeaveEndTime = overlapEnd;
                dto.LeaveHours = Math.Max(0, overlapHours);
                dto.Note = (dto.Note + $" | Nghỉ phép một phần ({overlapStart:HH\\:mm} - {overlapEnd:HH\\:mm}): {leave.LeaveRequest.Reason}").TrimStart(' ', '|');
            }
        }

        // Check Emergency Absence
        var incident = incidents.FirstOrDefault(i =>
            i.AffectedReceptionistUserId == s.ReceptionistUserId &&
            i.WorkDate == s.WorkDate &&
            i.ShiftStartTime == s.StartTime);

        if (incident != null)
        {
            dto.IsEmergencyAbsence = true;
            dto.Status = "EmergencyAbsence";
            dto.Note = (dto.Note + $" | Vắng mặt khẩn cấp/sự cố: {incident.Reason}").TrimStart(' ', '|');
        }

        return dto;
    }
}
