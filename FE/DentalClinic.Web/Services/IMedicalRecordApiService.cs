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
    Task<ApiResult<ImagingRecordDto>> UploadImagingFileAsync(long clinicalEntryId, string imagingType, string? resultSummary, System.IO.Stream fileStream, string fileName, string contentType);
    Task<ApiResult<bool>> DeleteImagingRecordAsync(long imagingRecordId);
    Task<ApiFileStreamResult?> GetImagingFileStreamAsync(long imagingRecordId, bool download = false);
}
