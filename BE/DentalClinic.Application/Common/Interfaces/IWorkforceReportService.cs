using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Attendance.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IWorkforceReportService
{
    Task<ApiResponse<WorkforceMonthlySummaryDto>> GetMonthlyReportAsync(int year, int month, string? roleFilter = null, CancellationToken ct = default);
    Task<ApiResponse<AttendancePeriodClosureDto>> FinalizePeriodAsync(int year, int month, long adminUserId, CancellationToken ct = default);
    Task<ApiResponse<AttendancePeriodClosureDto>> ReopenPeriodAsync(int year, int month, string reason, long adminUserId, CancellationToken ct = default);
    Task<byte[]> ExportMonthlyReportCsvAsync(int year, int month, string? roleFilter = null, CancellationToken ct = default);
}
