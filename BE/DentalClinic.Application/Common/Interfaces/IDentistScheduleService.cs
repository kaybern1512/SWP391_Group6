using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.DentistSchedule.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IDentistScheduleService
{
    // ==========================================
    // WORK SCHEDULES
    // ==========================================
    Task<ApiResponse<List<DentistWorkScheduleDto>>> GetSchedulesAsync(DentistScheduleFilterRequest request, CancellationToken ct = default);
    Task<ApiResponse<DentistWorkScheduleDto>> GetScheduleByIdAsync(long scheduleId, CancellationToken ct = default);
    Task<ApiResponse<List<DentistWorkScheduleDto>>> GetMySchedulesAsync(long dentistUserId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default);
    Task<ApiResponse<DentistWorkScheduleDto>> CreateScheduleAsync(CreateDentistScheduleRequest request, long currentUserId, CancellationToken ct = default);
    Task<ApiResponse<DentistWorkScheduleDto>> UpdateScheduleAsync(long scheduleId, UpdateDentistScheduleRequest request, long currentUserId, CancellationToken ct = default);
    Task<ApiResponse<bool>> CancelScheduleAsync(long scheduleId, long currentUserId, CancellationToken ct = default);

    // ==========================================
    // AVAILABILITY
    // ==========================================
    Task<ApiResponse<List<DentistAvailabilityDto>>> GetAvailabilitiesAsync(DentistAvailabilityFilterRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<DentistAvailabilityDto>>> GetMyAvailabilitiesAsync(long dentistUserId, DateTime? fromDateTime, DateTime? toDateTime, CancellationToken ct = default);
    Task<ApiResponse<DentistAvailabilityDto>> CreateAvailabilityAsync(CreateDentistAvailabilityRequest request, long currentUserId, string currentUserRole, CancellationToken ct = default);
    Task<ApiResponse<DentistAvailabilityDto>> UpdateAvailabilityAsync(long availabilityId, UpdateDentistAvailabilityRequest request, long currentUserId, string currentUserRole, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAvailabilityAsync(long availabilityId, long currentUserId, string currentUserRole, CancellationToken ct = default);
}
