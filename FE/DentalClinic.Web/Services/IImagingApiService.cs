using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IImagingApiService
{
    Task<ApiResult<DiagnosticOrderDto>> CreateOrderAsync(CreateDiagnosticOrderRequest request);
    Task<ApiResult<List<DiagnosticOrderDto>>> GetOrdersByAppointmentAsync(long appointmentId);
    Task<ApiResult<DiagnosticOrderDto>> GetOrderByIdAsync(long orderId);
    Task<ApiResult<List<ImagingAvailableSlotDto>>> GetAvailableSlotsAsync(long equipmentId, DateTime date, string diagnosticType);
    Task<ApiResult<ImagingSessionDto>> BookSessionAsync(BookImagingSessionRequest request);
    Task<ApiResult<ImagingSessionDto>> CheckInSessionAsync(long sessionId);
    Task<ApiResult<ImagingSessionDto>> StartSessionAsync(long sessionId);
    Task<ApiResult<ImagingSessionDto>> AcquireImageAsync(long sessionId);
    Task<ApiResult<DiagnosticResultDto>> UploadResultAsync(UploadDiagnosticResultRequest request);
    Task<ApiResult<ImagingSessionDto>> CompleteSessionAsync(long sessionId);
    Task<ApiResult<DiagnosticResultDto>> ReviewResultAsync(long resultId, ReviewDiagnosticResultRequest request);
    Task<ApiResult<ImagingSessionDto>> CancelSessionAsync(long sessionId, string reason);
    Task<ApiResult<ImagingQueueSummaryDto>> GetQueueStatusAsync(long roomId);
    Task<ApiResult<List<ImagingEquipmentDto>>> GetAllEquipmentsAsync();
    Task<ApiResult<ImagingEquipmentDto>> UpdateEquipmentStatusAsync(long equipmentId, UpdateEquipmentStatusRequest request);
    Task<ApiResult<List<ImagingServiceConfigDto>>> GetAllConfigsAsync();
    Task<ApiResult<ImagingServiceConfigDto>> UpdateConfigAsync(long configId, UpdateImagingConfigRequest request);

    // Clinical Consultations
    Task<ApiResult<ClinicalConsultationDto>> CreateConsultationAsync(CreateConsultationRequest request);
    Task<ApiResult<ClinicalConsultationDto>> RespondConsultationAsync(long consultationId, RespondConsultationRequest request);
    Task<ApiResult<List<ClinicalConsultationDto>>> GetDentistConsultationsAsync(string? type = null);
    Task<ApiResult<List<ClinicalConsultationDto>>> GetAppointmentConsultationsAsync(long appointmentId);
    Task<ApiResult<ClinicalConsultationDto>> GetConsultationByIdAsync(long consultationId);

    // Dedicated X-Ray Dentist Roster
    Task<ApiResult<List<XrayDentistRosterDto>>> GetRostersAsync(DateOnly? workDate = null, long? roomId = null, long? dentistUserId = null, string? status = null);
    Task<ApiResult<List<QualifiedDentistDto>>> GetQualifiedDentistsAsync();
    Task<ApiResult<XrayDentistRosterDto>> AssignRosterAsync(AssignXrayRosterRequest request);
    Task<ApiResult<XrayDentistRosterDto>> ReplaceDentistAsync(long rosterId, ReplaceXrayDentistRequest request);
    Task<ApiResult<bool>> CancelRosterAsync(long rosterId, string? reason = null);
    Task<ApiResult<List<XrayDentistRosterDto>>> GetMyXrayDutyAsync(DateOnly? fromDate = null, DateOnly? toDate = null);
}
