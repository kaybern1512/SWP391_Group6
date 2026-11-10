using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IReceptionistCoverageService
{
    /// <summary>
    /// Validates RULE 1: Maximum 2 DISTINCT receptionists approved on leave per calendar date.
    /// Returns false if candidateUserId would be the 3rd distinct receptionist on leave.
    /// </summary>
    Task<(bool IsAllowed, int CurrentApprovedCount, string Message)> ValidateDailyMaxLeavesAsync(DateOnly date, long candidateUserId, CancellationToken ct = default);

    /// <summary>
    /// Validates RULE 2: Minimum 2 available receptionists per shift interval across all active sub-windows.
    /// </summary>
    Task<List<ShiftCoverageStatusDto>> CheckShiftCoverageForDateAsync(DateOnly date, long? candidateLeaveUserId = null, TimeOnly? leaveStart = null, TimeOnly? leaveEnd = null, CancellationToken ct = default);

    Task<ApiResponse<ReceptionistCoverageIncidentDto>> ReportEmergencyAbsenceAsync(ReportEmergencyAbsenceRequest request, long reportedByUserId, CancellationToken ct = default);
    Task<ApiResponse<List<ReceptionistCoverageIncidentDto>>> GetCoverageIncidentsAsync(CoverageIncidentFilterRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> ResolveCoverageIncidentAsync(long incidentId, ResolveCoverageIncidentRequest request, long adminUserId, CancellationToken ct = default);
}
