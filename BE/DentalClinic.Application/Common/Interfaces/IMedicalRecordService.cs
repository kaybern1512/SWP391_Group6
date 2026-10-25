using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.MedicalRecords.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IMedicalRecordService
{
    Task<ApiResponse<MedicalRecordDto>> GetMedicalRecordByPatientIdAsync(long patientUserId, CancellationToken ct = default);
    Task<ApiResponse<ClinicalEntryDto>> CreateClinicalEntryAsync(long dentistUserId, CreateClinicalEntryRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<PatientLookupDto>>> SearchPatientsForMedicalRecordAsync(string? keyword, CancellationToken ct = default);
    Task<ApiResponse<List<OdontogramEntryDto>>> GetCurrentOdontogramAsync(long patientUserId, CancellationToken ct = default);
    Task<ApiResponse<ImagingRecordDto>> AddImagingRecordAsync(long dentistUserId, CreateImagingRecordRequest request, CancellationToken ct = default);
    Task<ApiResponse<ImagingRecordMetadataDto>> UploadImagingRecordAsync(long currentUserId, string? currentUserRole, UploadImagingRecordRequest request, CancellationToken ct = default);
    Task<ApiResponse<ImagingRecordMetadataDto>> UploadImagingRecordAsync(long currentUserId, UploadImagingRecordRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<ImagingRecordDto>>> GetImagingRecordsByClinicalEntryIdAsync(long clinicalEntryId, CancellationToken ct = default);
    Task<ApiResponse<List<ImagingRecordDto>>> GetImagingRecordsByClinicalEntryIdAsync(long currentUserId, string? currentUserRole, long clinicalEntryId, CancellationToken ct = default);
    Task<ApiResponse<ImagingRecordDto>> GetImagingRecordByIdAsync(long currentUserId, string? currentUserRole, long imagingRecordId, CancellationToken ct = default);
    Task<ApiResponse<DentalImageStreamResult>> GetImagingFileStreamAsync(long currentUserId, string? currentUserRole, long imagingRecordId, CancellationToken ct = default);
}
