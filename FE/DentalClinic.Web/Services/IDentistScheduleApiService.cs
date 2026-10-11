using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IDentistScheduleApiService
{
    // Work Schedules
    Task<ApiResult<List<DentistWorkScheduleDto>>> GetSchedulesAsync(DentistScheduleFilterRequest request);
    Task<ApiResult<List<DentistWorkScheduleDto>>> GetMySchedulesAsync(DateOnly? fromDate = null, DateOnly? toDate = null);
    Task<ApiResult<DentistWorkScheduleDto>> GetScheduleByIdAsync(long id);
    Task<ApiResult<DentistWorkScheduleDto>> CreateScheduleAsync(CreateDentistScheduleRequest request);
    Task<ApiResult<DentistWorkScheduleDto>> UpdateScheduleAsync(long id, UpdateDentistScheduleRequest request);
    Task<ApiResult> CancelScheduleAsync(long id);

    // Availability
    Task<ApiResult<List<DentistAvailabilityDto>>> GetAvailabilitiesAsync(DentistAvailabilityFilterRequest request);
    Task<ApiResult<List<DentistAvailabilityDto>>> GetMyAvailabilitiesAsync(DateTime? fromDateTime = null, DateTime? toDateTime = null);
    Task<ApiResult<DentistAvailabilityDto>> CreateAvailabilityAsync(CreateDentistAvailabilityRequest request);
    Task<ApiResult<DentistAvailabilityDto>> UpdateAvailabilityAsync(long id, UpdateDentistAvailabilityRequest request);
    Task<ApiResult> DeleteAvailabilityAsync(long id);
}
