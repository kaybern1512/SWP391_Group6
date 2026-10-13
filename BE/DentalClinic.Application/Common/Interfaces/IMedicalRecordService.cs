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

    Task<ApiResponse<ImagingRecordDto>> UploadImagingRecordAsync(
        long currentUserId,
        string currentUserRole,
        long clinicalEntryId,
        System.IO.Stream fileStream,
        string fileName,
        string contentType,
        string imagingType,
        string? resultSummary,
        CancellationToken ct = default);

    Task<ApiResponse<List<ImagingRecordDto>>> GetImagingRecordsByClinicalEntryAsync(
        long currentUserId,
        string currentUserRole,
        long clinicalEntryId,
        CancellationToken ct = default);

    Task<ApiResponse<MedicalImageStreamResult>> GetImagingRecordContentAsync(
        long currentUserId,
        string currentUserRole,
        long imagingRecordId,
        CancellationToken ct = default);

    Task<ApiResponse<bool>> DeleteImagingRecordAsync(
        long currentUserId,
        string currentUserRole,
        long imagingRecordId,
        CancellationToken ct = default);
}
