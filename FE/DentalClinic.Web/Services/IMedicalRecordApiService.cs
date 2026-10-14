using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IMedicalRecordApiService
{
    Task<ApiResult<MedicalRecordDto>> GetByPatientIdAsync(long patientUserId);
    Task<ApiResult<MedicalRecordDto>> GetMyMedicalRecordAsync();
    Task<ApiResult<ClinicalEntryDto>> CreateClinicalEntryAsync(CreateClinicalEntryRequest request);
    Task<ApiResult<List<PatientLookupDto>>> SearchPatientsAsync(string? search = null);
    Task<ApiResult<List<OdontogramEntryDto>>> GetCurrentOdontogramAsync(long patientUserId);
    Task<ApiResult<ImagingRecordDto>> AddImagingRecordAsync(CreateImagingRecordRequest request);
}
