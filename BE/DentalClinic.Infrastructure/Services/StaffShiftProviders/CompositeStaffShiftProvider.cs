using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Attendance.DTOs;
using DentalClinic.Domain.Enums;

namespace DentalClinic.Infrastructure.Services.StaffShiftProviders;

public class CompositeStaffShiftProvider : IStaffShiftResolver
{
    private readonly DentistShiftProvider _dentistProvider;
    private readonly ReceptionistShiftProvider _receptionistProvider;

    public CompositeStaffShiftProvider(DentistShiftProvider dentistProvider, ReceptionistShiftProvider receptionistProvider)
    {
        _dentistProvider = dentistProvider;
        _receptionistProvider = receptionistProvider;
    }

    public async Task<List<StaffShiftDto>> GetShiftsForStaffAsync(long userId, string role, DateOnly date, CancellationToken ct = default)
    {
        if (string.Equals(role, UserRole.Dentist, StringComparison.OrdinalIgnoreCase))
        {
            return await _dentistProvider.GetShiftsForStaffAsync(userId, date, ct);
        }

        if (string.Equals(role, UserRole.Receptionist, StringComparison.OrdinalIgnoreCase))
        {
            return await _receptionistProvider.GetShiftsForStaffAsync(userId, date, ct);
        }

        return new List<StaffShiftDto>();
    }

    public async Task<List<StaffShiftDto>> GetAllShiftsForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var dentistShifts = await _dentistProvider.GetAllShiftsForDateAsync(date, ct);
        var receptionistShifts = await _receptionistProvider.GetAllShiftsForDateAsync(date, ct);

        return dentistShifts.Concat(receptionistShifts).OrderBy(s => s.WorkDate).ThenBy(s => s.StartTime).ToList();
    }

    public async Task<List<StaffShiftDto>> GetShiftsForStaffRangeAsync(long userId, string role, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        if (string.Equals(role, UserRole.Dentist, StringComparison.OrdinalIgnoreCase))
        {
            return await _dentistProvider.GetShiftsForStaffRangeAsync(userId, fromDate, toDate, ct);
        }

        if (string.Equals(role, UserRole.Receptionist, StringComparison.OrdinalIgnoreCase))
        {
            return await _receptionistProvider.GetShiftsForStaffRangeAsync(userId, fromDate, toDate, ct);
        }

        return new List<StaffShiftDto>();
    }

    public async Task<List<StaffShiftDto>> GetAllShiftsRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var dentistShifts = await _dentistProvider.GetAllShiftsRangeAsync(fromDate, toDate, ct);
        var receptionistShifts = await _receptionistProvider.GetAllShiftsRangeAsync(fromDate, toDate, ct);

        return dentistShifts.Concat(receptionistShifts).OrderBy(s => s.WorkDate).ThenBy(s => s.StartTime).ToList();
    }
}
