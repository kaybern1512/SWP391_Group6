using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Models.Reports;
using RevenueReportDto = DentalClinic.Web.Models.Reports.RevenueReportDto;

namespace DentalClinic.Web.Services;

public interface IReportApiService
{
    Task<ApiResult<RevenueReportDto>> GetRevenueReportAsync(DateTime? fromDate = null, DateTime? toDate = null, long? departmentId = null);
    Task<ApiResult<DepartmentPerformanceReportDto>> GetDepartmentPerformanceReportAsync(long departmentId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<ApiResult<List<DentistPerformanceDto>>> GetAllDentistsPerformanceAsync(long? departmentId = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<(byte[]? Data, string ContentType, string FileName)> DownloadRevenueCsvAsync(DateTime? fromDate = null, DateTime? toDate = null, long? departmentId = null);
    Task<(byte[]? Data, string ContentType, string FileName)> DownloadDentistPerformanceCsvAsync(long? departmentId = null, DateTime? fromDate = null, DateTime? toDate = null);
}
