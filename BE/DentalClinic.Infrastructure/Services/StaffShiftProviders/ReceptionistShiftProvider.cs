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
        var schedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(s => s.StaffUser)
                .ThenInclude(sp => sp.User)
            .Where(s => s.StaffRole == "Receptionist" && s.StaffUserId == userId && s.WorkDate == date && s.Status != "Cancelled")
            .OrderBy(s => s.StartTime)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            return new List<StaffShiftDto>();
        }

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(lr => lr.StaffRole == "Receptionist" && lr.StaffUserId == userId && lr.Status == "Approved" && lr.StartDate <= date && lr.EndDate >= date)
            .ToListAsync(ct);

        return schedules.Select(s => MapToStaffShiftDto(s, approvedLeaves)).ToList();
    }

    public async Task<List<StaffShiftDto>> GetAllShiftsForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var schedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(s => s.StaffUser)
                .ThenInclude(sp => sp.User)
            .Where(s => s.StaffRole == "Receptionist" && s.WorkDate == date && s.Status != "Cancelled")
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.StaffUserId)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            return new List<StaffShiftDto>();
        }

        var userIds = schedules.Select(s => s.StaffUserId).Distinct().ToList();

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(lr => lr.StaffRole == "Receptionist" && userIds.Contains(lr.StaffUserId) && lr.Status == "Approved" && lr.StartDate <= date && lr.EndDate >= date)
            .ToListAsync(ct);

        return schedules.Select(s => MapToStaffShiftDto(s, approvedLeaves)).ToList();
    }

    public async Task<List<StaffShiftDto>> GetShiftsForStaffRangeAsync(long userId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var schedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(s => s.StaffUser)
                .ThenInclude(sp => sp.User)
            .Where(s => s.StaffRole == "Receptionist" && s.StaffUserId == userId && s.WorkDate >= fromDate && s.WorkDate <= toDate && s.Status != "Cancelled")
            .OrderBy(s => s.WorkDate)
            .ThenBy(s => s.StartTime)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            return new List<StaffShiftDto>();
        }

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(lr => lr.StaffRole == "Receptionist" && lr.StaffUserId == userId && lr.Status == "Approved" && lr.StartDate <= toDate && lr.EndDate >= fromDate)
            .ToListAsync(ct);

        return schedules.Select(s => MapToStaffShiftDto(s, approvedLeaves)).ToList();
    }

    public async Task<List<StaffShiftDto>> GetAllShiftsRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var schedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(s => s.StaffUser)
                .ThenInclude(sp => sp.User)
            .Where(s => s.StaffRole == "Receptionist" && s.WorkDate >= fromDate && s.WorkDate <= toDate && s.Status != "Cancelled")
            .OrderBy(s => s.WorkDate)
            .ThenBy(s => s.StartTime)
            .ThenBy(s => s.StaffUserId)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            return new List<StaffShiftDto>();
        }

        var userIds = schedules.Select(s => s.StaffUserId).Distinct().ToList();

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(lr => lr.StaffRole == "Receptionist" && userIds.Contains(lr.StaffUserId) && lr.Status == "Approved" && lr.StartDate <= toDate && lr.EndDate >= fromDate)
            .ToListAsync(ct);

        return schedules.Select(s => MapToStaffShiftDto(s, approvedLeaves)).ToList();
    }

    private static StaffShiftDto MapToStaffShiftDto(
        Persistence.Entities.StaffWorkSchedule s,
        List<Persistence.Entities.StaffLeaveRequest> approvedLeaves)
    {
        var shiftName = $"Ca Lễ Tân ({s.StartTime:HH\\:mm} - {s.EndTime:HH\\:mm})";

        var dto = new StaffShiftDto
        {
            ShiftIdentifier = $"RECEP_SCHED_{s.WorkScheduleId}",
            SourceScheduleId = s.WorkScheduleId,
            SourceScheduleType = "StaffWorkSchedule",
            UserId = s.StaffUserId,
            StaffName = s.StaffUser?.FullName ?? $"Lễ tân #{s.StaffUserId}",
            StaffCode = $"REC-{s.StaffUserId:D4}",
            Role = UserRole.Receptionist,
            WorkDate = s.WorkDate,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            ShiftName = shiftName,
            DepartmentId = null,
            DepartmentName = "Bộ phận Lễ tân & Tiếp đón",
            Status = s.Status,
            Note = s.Notes
        };

        var userLeavesOnDate = approvedLeaves
            .Where(lr => lr.StaffUserId == s.StaffUserId && lr.StartDate <= s.WorkDate && lr.EndDate >= s.WorkDate)
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
                dto.LeaveType = leave.LeaveType;
                dto.LeaveHours = (decimal)(s.EndTime - s.StartTime).TotalHours;
                dto.Note = (dto.Note + $" | Nghỉ phép ({leave.LeaveType}): {leave.Reason}").TrimStart(' ', '|');
                break;
            }

            if (leave.StartTime.HasValue && leave.EndTime.HasValue
                && leave.StartTime.Value < s.EndTime && leave.EndTime.Value > s.StartTime)
            {
                dto.IsApprovedLeave = true;
                dto.Status = "PartialLeave";
                dto.LeaveType = leave.LeaveType;

                var overlapStart = leave.StartTime.Value > s.StartTime ? leave.StartTime.Value : s.StartTime;
                var overlapEnd = leave.EndTime.Value < s.EndTime ? leave.EndTime.Value : s.EndTime;
                var overlapHours = (decimal)(overlapEnd - overlapStart).TotalHours;

                dto.LeaveStartTime = overlapStart;
                dto.LeaveEndTime = overlapEnd;
                dto.LeaveHours = Math.Max(0, overlapHours);
                dto.Note = (dto.Note + $" | Nghỉ phép một phần ({overlapStart:HH\\:mm} - {overlapEnd:HH\\:mm}): {leave.Reason}").TrimStart(' ', '|');
            }
        }

        if (s.Status == "AbsenceReported")
        {
            dto.IsEmergencyAbsence = true;
            dto.Status = "EmergencyAbsence";
        }

        return dto;
    }
}
