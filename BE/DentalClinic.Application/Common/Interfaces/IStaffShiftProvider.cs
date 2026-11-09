using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Attendance.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

/// <summary>
/// Abstraction for resolving employee shifts across roles.
/// Isolates attendance recording from role-specific work scheduling tables.
/// </summary>
public interface IStaffShiftProvider
{
    /// <summary>Role this provider specializes in (e.g. "Dentist", "Receptionist").</summary>
    string SupportedRole { get; }

    /// <summary>Retrieves scheduled shifts for a specific staff member on a specific calendar date.</summary>
    Task<List<StaffShiftDto>> GetShiftsForStaffAsync(long userId, DateOnly date, CancellationToken ct = default);

    /// <summary>Retrieves scheduled shifts for all staff of this role on a specific calendar date.</summary>
    Task<List<StaffShiftDto>> GetAllShiftsForDateAsync(DateOnly date, CancellationToken ct = default);

    /// <summary>Retrieves scheduled shifts for a specific staff member across a date range.</summary>
    Task<List<StaffShiftDto>> GetShiftsForStaffRangeAsync(long userId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    /// <summary>Retrieves all scheduled shifts across a date range for all staff of this role.</summary>
    Task<List<StaffShiftDto>> GetAllShiftsRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}
