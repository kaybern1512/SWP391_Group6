using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.DentistLeave.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IAppointmentDisruptionService
{
    Task<ApiResponse<List<AppointmentDisruptionDto>>> GetPendingDisruptionsForPatientAsync(long patientUserId, CancellationToken ct = default);
    Task<ApiResponse<AppointmentDisruptionDto>> GetDisruptionDetailAsync(long disruptionId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<AppointmentDisruptionDto>> GetDisruptionByAppointmentIdAsync(long appointmentId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<List<AlternativeDentistDto>>> GetAvailableAlternativeDentistsAsync(long disruptionId, CancellationToken ct = default);
    Task<ApiResponse<AppointmentDisruptionDto>> PatientChooseNewDentistAsync(long disruptionId, long patientUserId, PatientChooseNewDentistRequest request, CancellationToken ct = default);
    Task<ApiResponse<AppointmentDisruptionDto>> PatientRescheduleDisruptionAsync(long disruptionId, long patientUserId, PatientDisruptionRescheduleRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<AppointmentDisruptionDto>>> GetReceptionistDisruptionsAsync(string? statusFilter = null, bool? urgentOnly = null, CancellationToken ct = default);
    Task<ApiResponse<AppointmentDisruptionDto>> ReceptionistResolveDisruptionAsync(long disruptionId, long staffUserId, ReceptionistResolveDisruptionRequest request, CancellationToken ct = default);
    Task<ApiResponse<int>> ProcessPendingDisruptionEmailsAsync(CancellationToken ct = default);
}
