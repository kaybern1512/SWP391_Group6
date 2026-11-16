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

public class DentistShiftProvider : IStaffShiftProvider
{
    private readonly DentalClinicDbContext _dbContext;

    public DentistShiftProvider(DentalClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string SupportedRole => UserRole.Dentist;

    public async Task<List<StaffShiftDto>> GetShiftsForStaffAsync(long userId, DateOnly date, CancellationToken ct = default)
    {
        var schedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.User)
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.DentistProfile!)
                    .ThenInclude(dp => dp.Department)
            .Where(ws => ws.StaffRole == "Dentist" && ws.StaffUserId == userId && ws.WorkDate == date && (ws.Status == "Scheduled" || ws.Status == "Completed"))
            .OrderBy(ws => ws.StartTime)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            return new List<StaffShiftDto>();
        }

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(lr => lr.StaffRole == "Dentist" && lr.StaffUserId == userId && lr.Status == "Approved" && lr.StartDate <= date && lr.EndDate >= date)
            .ToListAsync(ct);

        return schedules.Select(ws => MapToStaffShiftDto(ws, approvedLeaves)).ToList();
    }

    public async Task<List<StaffShiftDto>> GetAllShiftsForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var schedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.User)
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.DentistProfile!)
                    .ThenInclude(dp => dp.Department)
            .Where(ws => ws.StaffRole == "Dentist" && ws.WorkDate == date && (ws.Status == "Scheduled" || ws.Status == "Completed"))
            .OrderBy(ws => ws.StartTime)
            .ThenBy(ws => ws.StaffUserId)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            return new List<StaffShiftDto>();
        }

        var dentistIds = schedules.Select(ws => ws.StaffUserId).Distinct().ToList();

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(lr => lr.StaffRole == "Dentist" && dentistIds.Contains(lr.StaffUserId) && lr.Status == "Approved" && lr.StartDate <= date && lr.EndDate >= date)
            .ToListAsync(ct);

        return schedules.Select(ws => MapToStaffShiftDto(ws, approvedLeaves)).ToList();
    }

    public async Task<List<StaffShiftDto>> GetShiftsForStaffRangeAsync(long userId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var schedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.User)
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.DentistProfile!)
                    .ThenInclude(dp => dp.Department)
            .Where(ws => ws.StaffRole == "Dentist" && ws.StaffUserId == userId && ws.WorkDate >= fromDate && ws.WorkDate <= toDate && (ws.Status == "Scheduled" || ws.Status == "Completed"))
            .OrderBy(ws => ws.WorkDate)
            .ThenBy(ws => ws.StartTime)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            return new List<StaffShiftDto>();
        }

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(lr => lr.StaffRole == "Dentist" && lr.StaffUserId == userId && lr.Status == "Approved" && lr.StartDate <= toDate && lr.EndDate >= fromDate)
            .ToListAsync(ct);

        return schedules.Select(ws => MapToStaffShiftDto(ws, approvedLeaves)).ToList();
    }

    public async Task<List<StaffShiftDto>> GetAllShiftsRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var schedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.User)
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.DentistProfile!)
                    .ThenInclude(dp => dp.Department)
            .Where(ws => ws.StaffRole == "Dentist" && ws.WorkDate >= fromDate && ws.WorkDate <= toDate && (ws.Status == "Scheduled" || ws.Status == "Completed"))
            .OrderBy(ws => ws.WorkDate)
            .ThenBy(ws => ws.StartTime)
            .ThenBy(ws => ws.StaffUserId)
            .ToListAsync(ct);

        if (!schedules.Any())
        {
            return new List<StaffShiftDto>();
        }

        var dentistIds = schedules.Select(ws => ws.StaffUserId).Distinct().ToList();

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(lr => lr.StaffRole == "Dentist" && dentistIds.Contains(lr.StaffUserId) && lr.Status == "Approved" && lr.StartDate <= toDate && lr.EndDate >= fromDate)
            .ToListAsync(ct);

        return schedules.Select(ws => MapToStaffShiftDto(ws, approvedLeaves)).ToList();
    }

    private static StaffShiftDto MapToStaffShiftDto(
        Persistence.Entities.StaffWorkSchedule ws,
        List<Persistence.Entities.StaffLeaveRequest> approvedLeaves)
    {
        var staffName = ws.StaffUser?.FullName
            ?? ws.StaffUser?.User?.Email
            ?? $"Bác sĩ #{ws.StaffUserId}";

        var staffCode = $"DENT-{ws.StaffUserId:D4}";
        var department = ws.StaffUser?.DentistProfile?.Department;

        var dto = new StaffShiftDto
        {
            ShiftIdentifier = $"DENTIST_SCHED_{ws.WorkScheduleId}",
            SourceScheduleId = ws.WorkScheduleId,
            SourceScheduleType = "StaffWorkSchedule",
            UserId = ws.StaffUserId,
            StaffName = staffName,
            StaffCode = staffCode,
            Role = UserRole.Dentist,
            WorkDate = ws.WorkDate,
            StartTime = ws.StartTime,
            EndTime = ws.EndTime,
            ShiftName = $"Ca Khám ({ws.StartTime:HH\\:mm} - {ws.EndTime:HH\\:mm})",
            DepartmentId = department?.DepartmentId,
            DepartmentName = department?.Name ?? string.Empty,
            Status = ws.Status,
            Note = ws.Notes
        };

        var userLeaves = approvedLeaves
            .Where(lr => lr.StaffUserId == ws.StaffUserId && lr.StartDate <= ws.WorkDate && lr.EndDate >= ws.WorkDate)
            .ToList();

        foreach (var leave in userLeaves)
        {
            bool isFullDay = !leave.StartTime.HasValue && !leave.EndTime.HasValue;
            bool coversShift = leave.StartTime.HasValue && leave.EndTime.HasValue
                && leave.StartTime.Value <= ws.StartTime && leave.EndTime.Value >= ws.EndTime;

            if (isFullDay || coversShift)
            {
                dto.IsApprovedLeave = true;
                dto.Status = "ApprovedLeave";
                dto.LeaveType = leave.LeaveType;
                dto.LeaveHours = (decimal)(ws.EndTime - ws.StartTime).TotalHours;
                dto.Note = (dto.Note + $" | Nghỉ phép ({leave.LeaveType}): {leave.Reason}").TrimStart(' ', '|');
                break;
            }

            if (leave.StartTime.HasValue && leave.EndTime.HasValue
                && leave.StartTime.Value < ws.EndTime && leave.EndTime.Value > ws.StartTime)
            {
                dto.IsApprovedLeave = true;
                dto.Status = "PartialLeave";
                dto.LeaveType = leave.LeaveType;

                var overlapStart = leave.StartTime.Value > ws.StartTime ? leave.StartTime.Value : ws.StartTime;
                var overlapEnd = leave.EndTime.Value < ws.EndTime ? leave.EndTime.Value : ws.EndTime;
                var overlapHours = (decimal)(overlapEnd - overlapStart).TotalHours;

                dto.LeaveStartTime = overlapStart;
                dto.LeaveEndTime = overlapEnd;
                dto.LeaveHours = Math.Max(0, overlapHours);
                dto.Note = (dto.Note + $" | Nghỉ phép một phần ({overlapStart:HH\\:mm} - {overlapEnd:HH\\:mm}): {leave.Reason}").TrimStart(' ', '|');
            }
        }

        return dto;
    }
}
