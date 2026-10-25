using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using Microsoft.AspNetCore.Http;

namespace DentalClinic.Web.Services;

public interface IMedicalRecordApiService
{
    Task<ApiResult<MedicalRecordDto>> GetByPatientIdAsync(long patientUserId);
    Task<ApiResult<MedicalRecordDto>> GetMyMedicalRecordAsync();
    Task<ApiResult<ClinicalEntryDto>> CreateClinicalEntryAsync(CreateClinicalEntryRequest request);
    Task<ApiResult<List<PatientLookupDto>>> SearchPatientsAsync(string? search = null);
    Task<ApiResult<List<OdontogramEntryDto>>> GetCurrentOdontogramAsync(long patientUserId);
    Task<ApiResult<ImagingRecordDto>> AddImagingRecordAsync(CreateImagingRecordRequest request);
    Task<ApiResult<ImagingRecordMetadataDto>> UploadImagingRecordAsync(long clinicalEntryId, IFormFile file, string imagingType, string? resultSummary = null);
    Task<ApiResult<ImagingRecordMetadataDto>> UploadImagingRecordAsync(long clinicalEntryId, Stream fileStream, string fileName, string contentType, string imagingType, string? resultSummary = null);
    Task<ApiResult<List<ImagingRecordDto>>> GetClinicalEntryImagingAsync(long clinicalEntryId);
    Task<ApiResult<bool>> DeleteImagingRecordAsync(long imagingRecordId);
}
