using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Attendance.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IStaffShiftResolver
{
    Task<List<StaffShiftDto>> GetShiftsForStaffAsync(long userId, string role, DateOnly date, CancellationToken ct = default);
    Task<List<StaffShiftDto>> GetAllShiftsForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<List<StaffShiftDto>> GetShiftsForStaffRangeAsync(long userId, string role, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
    Task<List<StaffShiftDto>> GetAllShiftsRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}
