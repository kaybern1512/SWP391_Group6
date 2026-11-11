using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IXrayRosterService
{
    Task<List<XrayDentistRosterDto>> GetRostersAsync(XrayRosterFilterRequest request, CancellationToken ct = default);
    Task<XrayDentistRosterDto?> GetRosterByIdAsync(long rosterId, CancellationToken ct = default);
    Task<XrayDentistRosterDto?> GetActiveRosterForRoomAsync(long roomId, DateTime atDateTimeUtc, CancellationToken ct = default);
    Task<List<QualifiedDentistDto>> GetQualifiedDentistsAsync(CancellationToken ct = default);
    Task<XrayDentistRosterDto> AssignRosterAsync(AssignXrayRosterRequest request, long adminUserId, CancellationToken ct = default);
    Task<XrayDentistRosterDto> ReplaceDentistAsync(long rosterId, ReplaceXrayDentistRequest request, long adminUserId, CancellationToken ct = default);
    Task<bool> CancelRosterAsync(long rosterId, string reason, long adminUserId, CancellationToken ct = default);
    Task<bool> IsDentistOnXrayDutyAsync(long dentistUserId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default);
    Task<List<XrayDentistRosterDto>> GetDentistXrayScheduleAsync(long dentistUserId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}
