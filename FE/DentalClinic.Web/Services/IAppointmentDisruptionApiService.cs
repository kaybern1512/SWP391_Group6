using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IAppointmentDisruptionApiService
{
    Task<ApiResult<List<AppointmentDisruptionDto>>> GetMyPendingDisruptionsAsync();
    Task<ApiResult<AppointmentDisruptionDto>> GetDisruptionDetailAsync(long disruptionId);
    Task<ApiResult<AppointmentDisruptionDto>> GetDisruptionByAppointmentIdAsync(long appointmentId);
    Task<ApiResult<List<AlternativeDentistDto>>> GetAvailableAlternativeDentistsAsync(long disruptionId);
    Task<ApiResult<AppointmentDisruptionDto>> PatientChooseNewDentistAsync(long disruptionId, PatientChooseNewDentistRequest request);
    Task<ApiResult<AppointmentDisruptionDto>> PatientRescheduleDisruptionAsync(long disruptionId, PatientDisruptionRescheduleRequest request);
    Task<ApiResult<List<AppointmentDisruptionDto>>> GetReceptionistDisruptionsAsync(string? status = null, bool? urgentOnly = null);
    Task<ApiResult<AppointmentDisruptionDto>> ReceptionistResolveDisruptionAsync(long disruptionId, ReceptionistResolveDisruptionRequest request);
    Task<ApiResult<int>> ProcessPendingDisruptionEmailsAsync();
}
