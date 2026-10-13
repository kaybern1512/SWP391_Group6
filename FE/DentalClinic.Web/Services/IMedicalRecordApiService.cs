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

    Task<ApiResult<ImagingRecordDto>> UploadImagingRecordAsync(long clinicalEntryId, System.IO.Stream fileStream, string fileName, string contentType, string imagingType, string? resultSummary);
    Task<ApiResult<List<ImagingRecordDto>>> GetImagingRecordsAsync(long clinicalEntryId);
    Task<ApiResult<bool>> DeleteImagingRecordAsync(long imagingRecordId);
    Task<(System.IO.Stream Stream, string ContentType)?> GetImageContentAsync(long imagingRecordId);
}
