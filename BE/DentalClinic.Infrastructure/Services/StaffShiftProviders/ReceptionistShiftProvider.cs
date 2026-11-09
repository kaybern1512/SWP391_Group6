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
        var staff = await _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.Role == UserRole.Receptionist && u.Status == "Active", ct);

        if (staff == null)
        {
            return new List<StaffShiftDto>();
        }

        return GenerateDefaultReceptionistShifts(staff.UserId, staff.StaffProfile?.FullName ?? "Tiếp tân", staff.StaffProfile?.EmployeeCode ?? $"REC-{staff.UserId:D4}", date);
    }

    public async Task<List<StaffShiftDto>> GetAllShiftsForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var receptionists = await _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.StaffProfile)
            .Where(u => u.Role == UserRole.Receptionist && u.Status == "Active")
            .OrderBy(u => u.UserId)
            .ToListAsync(ct);

        var shifts = new List<StaffShiftDto>();
        foreach (var r in receptionists)
        {
            shifts.AddRange(GenerateDefaultReceptionistShifts(r.UserId, r.StaffProfile?.FullName ?? "Tiếp tân", r.StaffProfile?.EmployeeCode ?? $"REC-{r.UserId:D4}", date));
        }

        return shifts;
    }

    public async Task<List<StaffShiftDto>> GetShiftsForStaffRangeAsync(long userId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var staff = await _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.Role == UserRole.Receptionist && u.Status == "Active", ct);

        if (staff == null)
        {
            return new List<StaffShiftDto>();
        }

        var shifts = new List<StaffShiftDto>();
        for (var d = fromDate; d <= toDate; d = d.AddDays(1))
        {
            shifts.AddRange(GenerateDefaultReceptionistShifts(staff.UserId, staff.StaffProfile?.FullName ?? "Tiếp tân", staff.StaffProfile?.EmployeeCode ?? $"REC-{staff.UserId:D4}", d));
        }

        return shifts;
    }

    public async Task<List<StaffShiftDto>> GetAllShiftsRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var receptionists = await _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.StaffProfile)
            .Where(u => u.Role == UserRole.Receptionist && u.Status == "Active")
            .OrderBy(u => u.UserId)
            .ToListAsync(ct);

        var shifts = new List<StaffShiftDto>();
        for (var d = fromDate; d <= toDate; d = d.AddDays(1))
        {
            foreach (var r in receptionists)
            {
                shifts.AddRange(GenerateDefaultReceptionistShifts(r.UserId, r.StaffProfile?.FullName ?? "Tiếp tân", r.StaffProfile?.EmployeeCode ?? $"REC-{r.UserId:D4}", d));
            }
        }

        return shifts;
    }

    private static List<StaffShiftDto> GenerateDefaultReceptionistShifts(long userId, string staffName, string staffCode, DateOnly date)
    {
        return new List<StaffShiftDto>
        {
            new()
            {
                ShiftIdentifier = $"RECEPT_{userId}_{date:yyyyMMdd}_S1",
                UserId = userId,
                StaffName = staffName,
                StaffCode = staffCode,
                Role = UserRole.Receptionist,
                WorkDate = date,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                ShiftName = "Ca Tiếp Tân Sáng (08:00 - 12:00)",
                DepartmentId = null,
                DepartmentName = "Quầy Tiếp Tân",
                Status = "Scheduled",
                Note = "Ca trực tiếp tân quầy"
            },
            new()
            {
                ShiftIdentifier = $"RECEPT_{userId}_{date:yyyyMMdd}_S2",
                UserId = userId,
                StaffName = staffName,
                StaffCode = staffCode,
                Role = UserRole.Receptionist,
                WorkDate = date,
                StartTime = new TimeOnly(13, 0),
                EndTime = new TimeOnly(17, 0),
                ShiftName = "Ca Tiếp Tân Chiều (13:00 - 17:00)",
                DepartmentId = null,
                DepartmentName = "Quầy Tiếp Tân",
                Status = "Scheduled",
                Note = "Ca trực tiếp tân quầy"
            }
        };
    }
}
