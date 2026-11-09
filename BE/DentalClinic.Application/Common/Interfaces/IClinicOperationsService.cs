using System;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Operations.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IClinicOperationsService
{
    Task<ApiResponse<ClinicOperationsDashboardDto>> GetOperationsDashboardAsync(DateOnly? targetDate = null, CancellationToken ct = default);
}
