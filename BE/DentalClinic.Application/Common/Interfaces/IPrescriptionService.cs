using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Prescriptions.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IPrescriptionService
{
    Task<ApiResponse<List<PrescriptionDto>>> GetDentistPrescriptionsAsync(long dentistUserId, string? search = null, CancellationToken ct = default);
    Task<ApiResponse<List<PrescriptionDto>>> GetPatientPrescriptionsAsync(long patientUserId, CancellationToken ct = default);
    Task<ApiResponse<PrescriptionDto>> GetPrescriptionDetailAsync(long prescriptionId, CancellationToken ct = default);
    Task<ApiResponse<PrescriptionDto>> CreatePrescriptionAsync(long dentistUserId, CreatePrescriptionRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<PrescriptionEligibleAppointmentDto>>> GetEligibleAppointmentsForPrescriptionAsync(long dentistUserId, CancellationToken ct = default);
}
