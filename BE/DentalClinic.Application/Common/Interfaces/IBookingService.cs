using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Booking.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IBookingService
{
    Task<ApiResponse<List<DepartmentDto>>> GetActiveDepartmentsAsync(CancellationToken ct = default);
    Task<ApiResponse<List<DepartmentServiceDto>>> GetServicesByDepartmentAsync(long departmentId, CancellationToken ct = default);
    Task<ApiResponse<List<DentistOptionDto>>> GetDentistsByDepartmentAsync(long departmentId, CancellationToken ct = default);
    Task<ApiResponse<List<AvailableSlotDto>>> GetAvailableSlotsAsync(AvailableSlotsQuery query, CancellationToken ct = default);
}
