using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IPrescriptionApiService
{
    Task<ApiResult<List<PrescriptionDto>>> GetDentistPrescriptionsAsync(string? search = null);
    Task<ApiResult<List<PrescriptionEligibleAppointmentDto>>> GetEligibleAppointmentsAsync();
    Task<ApiResult<List<PrescriptionDto>>> GetMyPrescriptionsAsync();
    Task<ApiResult<PrescriptionDto>> GetPrescriptionDetailAsync(long prescriptionId);
    Task<ApiResult<PrescriptionDto>> CreatePrescriptionAsync(CreatePrescriptionRequest request);
}
