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
        var schedules = await _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.DentistUser)
                .ThenInclude(dp => dp.User)
            .Include(ws => ws.Department)
            .Where(ws => ws.DentistUserId == userId && ws.WorkDate == date && ws.Status == "Scheduled")
            .OrderBy(ws => ws.StartTime)
            .ToListAsync(ct);

        return schedules.Select(MapToShiftDto).ToList();
    }

    public async Task<List<StaffShiftDto>> GetAllShiftsForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var schedules = await _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.DentistUser)
                .ThenInclude(dp => dp.User)
            .Include(ws => ws.Department)
            .Where(ws => ws.WorkDate == date && ws.Status == "Scheduled")
            .OrderBy(ws => ws.StartTime)
            .ToListAsync(ct);

        return schedules.Select(MapToShiftDto).ToList();
    }

    public async Task<List<StaffShiftDto>> GetShiftsForStaffRangeAsync(long userId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var schedules = await _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.DentistUser)
                .ThenInclude(dp => dp.User)
            .Include(ws => ws.Department)
            .Where(ws => ws.DentistUserId == userId && ws.WorkDate >= fromDate && ws.WorkDate <= toDate && ws.Status == "Scheduled")
            .OrderBy(ws => ws.WorkDate)
            .ThenBy(ws => ws.StartTime)
            .ToListAsync(ct);

        return schedules.Select(MapToShiftDto).ToList();
    }

    public async Task<List<StaffShiftDto>> GetAllShiftsRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var schedules = await _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.DentistUser)
                .ThenInclude(dp => dp.User)
            .Include(ws => ws.Department)
            .Where(ws => ws.WorkDate >= fromDate && ws.WorkDate <= toDate && ws.Status == "Scheduled")
            .OrderBy(ws => ws.WorkDate)
            .ThenBy(ws => ws.StartTime)
            .ToListAsync(ct);

        return schedules.Select(MapToShiftDto).ToList();
    }

    private static StaffShiftDto MapToShiftDto(Persistence.Entities.DentistWorkSchedule ws)
    {
        return new StaffShiftDto
        {
            ShiftIdentifier = $"DENTIST_SCHED_{ws.DentistScheduleId}",
            UserId = ws.DentistUserId,
            StaffName = ws.DentistUser?.User?.FullName ?? "Bác sĩ",
            StaffCode = ws.DentistUser?.User?.EmployeeCode ?? $"DENT-{ws.DentistUserId:D4}",
            Role = UserRole.Dentist,
            WorkDate = ws.WorkDate,
            StartTime = ws.StartTime,
            EndTime = ws.EndTime,
            ShiftName = $"Ca Khám ({ws.StartTime:HH\\:mm} - {ws.EndTime:HH\\:mm})",
            DepartmentId = ws.DepartmentId,
            DepartmentName = ws.Department?.Name ?? string.Empty,
            Status = ws.Status,
            Note = ws.Note
        };
    }
}
