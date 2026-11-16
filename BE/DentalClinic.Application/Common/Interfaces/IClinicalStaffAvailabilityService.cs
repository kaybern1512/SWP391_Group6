using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DentalClinic.Application.Common.Interfaces;

public interface IClinicalStaffAvailabilityService
{
    Task<(bool IsAvailable, string? Reason)> CheckDentistAvailabilityAsync(
        long dentistUserId,
        DateTime start,
        DateTime end,
        int? departmentId = null,
        long? excludeAppointmentId = null,
        CancellationToken ct = default);

    Task<(bool IsAvailable, string? Reason)> CheckChairAvailabilityAsync(
        int chairId,
        DateTime start,
        DateTime end,
        long? excludeAppointmentId = null,
        CancellationToken ct = default);

    Task<(bool CanApprove, string? Reason)> ValidateXrayShiftLeaveCoverageAsync(
        long dentistUserId,
        DateOnly startDate,
        DateOnly endDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        CancellationToken ct = default);

    Task<List<long>> GetAvailableDentistUserIdsAsync(
        int departmentId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default);
}
