using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Reports.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IReportService
{
    Task<ApiResponse<RevenueReportDto>> GetRevenueReportAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? departmentId = null,
        CancellationToken ct = default);

    Task<ApiResponse<DepartmentPerformanceReportDto>> GetDepartmentPerformanceReportAsync(
        long departmentId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default);

    Task<ApiResponse<List<DentistPerformanceDto>>> GetAllDentistsPerformanceAsync(
        long? departmentId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default);

    Task<byte[]> ExportInvoicesCsvAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? departmentId = null,
        CancellationToken ct = default);

    Task<byte[]> ExportDentistPerformanceCsvAsync(
        long? departmentId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default);
}
