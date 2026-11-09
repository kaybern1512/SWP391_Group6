using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IImagingService
{
    Task<DiagnosticOrderDto> CreateDiagnosticOrderAsync(CreateDiagnosticOrderRequest request, long dentistUserId, CancellationToken ct = default);
    Task<List<DiagnosticOrderDto>> GetDiagnosticOrdersByAppointmentAsync(long appointmentId, CancellationToken ct = default);
    Task<DiagnosticOrderDto?> GetDiagnosticOrderByIdAsync(long diagnosticOrderId, CancellationToken ct = default);

    Task<List<AvailableSlotDto>> GetAvailableSlotsAsync(long equipmentId, DateTime dateUtc, string diagnosticType, CancellationToken ct = default);
    Task<ImagingSessionDto> BookImagingSessionAsync(BookImagingSessionRequest request, long createdByUserId, CancellationToken ct = default);
    Task<ImagingSessionDto> CheckInImagingSessionAsync(long imagingSessionId, CancellationToken ct = default);
    Task<ImagingSessionDto> StartImagingSessionAsync(long imagingSessionId, long operatorUserId, CancellationToken ct = default);
    Task<ImagingSessionDto> AcquireImageAsync(long imagingSessionId, CancellationToken ct = default);
    Task<DiagnosticResultDto> UploadDiagnosticResultAsync(UploadDiagnosticResultRequest request, long uploadedByUserId, CancellationToken ct = default);
    Task<ImagingSessionDto> CompleteImagingSessionAsync(long imagingSessionId, CancellationToken ct = default);
    Task<DiagnosticResultDto> ReviewDiagnosticResultAsync(long diagnosticResultId, ReviewDiagnosticResultRequest request, long dentistUserId, CancellationToken ct = default);

    Task<ImagingQueueSummaryDto> GetImagingQueueStatusAsync(long roomId, CancellationToken ct = default);
    Task<ImagingSessionDto> CancelImagingSessionAsync(long imagingSessionId, string reason, long cancelledByUserId, CancellationToken ct = default);

    Task<List<ImagingEquipmentDto>> GetAllEquipmentsAsync(CancellationToken ct = default);
    Task<ImagingEquipmentDto> UpdateEquipmentStatusAsync(long equipmentId, string operationalStatus, CancellationToken ct = default);
    Task<List<ImagingServiceConfigDto>> GetAllServiceConfigsAsync(CancellationToken ct = default);
    Task<ImagingServiceConfigDto> UpdateServiceConfigAsync(long configId, UpdateImagingConfigRequest request, CancellationToken ct = default);

    // Clinical Consultations (Hội chẩn lâm sàng giữa các bác sĩ)
    Task<ClinicalConsultationDto> CreateConsultationAsync(CreateConsultationRequest request, long dentistUserId, CancellationToken ct = default);
    Task<ClinicalConsultationDto> RespondConsultationAsync(long consultationId, RespondConsultationRequest request, long dentistUserId, CancellationToken ct = default);
    Task<List<ClinicalConsultationDto>> GetConsultationsByDentistAsync(long dentistUserId, string type, CancellationToken ct = default);
    Task<List<ClinicalConsultationDto>> GetConsultationsByAppointmentAsync(long appointmentId, CancellationToken ct = default);
    Task<ClinicalConsultationDto?> GetConsultationByIdAsync(long consultationId, CancellationToken ct = default);
}
