using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/clinical-diagnostics")]
[Authorize]
public class ClinicalDiagnosticsController : ControllerBase
{
    private readonly IImagingService _imagingService;
    private readonly IMedicalImagingStorageService _imagingStorageService;
    private readonly DentalClinicDbContext _dbContext;
    private readonly IXrayRosterService _xrayRosterService;

    public ClinicalDiagnosticsController(
        IImagingService imagingService,
        IMedicalImagingStorageService imagingStorageService,
        DentalClinicDbContext dbContext,
        IXrayRosterService xrayRosterService)
    {
        _imagingService = imagingService;
        _imagingStorageService = imagingStorageService;
        _dbContext = dbContext;
        _xrayRosterService = xrayRosterService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpPost("orders")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<DiagnosticOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateDiagnosticOrderRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _imagingService.CreateDiagnosticOrderAsync(request, userId, ct);
            return Ok(ApiResponse<DiagnosticOrderDto>.Ok(result, "Tạo chỉ định chẩn đoán hình ảnh thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<DiagnosticOrderDto>.Fail(ex.Message));
        }
    }

    [HttpGet("orders/appointment/{appointmentId}")]
    [ProducesResponseType(typeof(ApiResponse<List<DiagnosticOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrdersByAppointment(long appointmentId, CancellationToken ct)
    {
        try
        {
            var result = await _imagingService.GetDiagnosticOrdersByAppointmentAsync(appointmentId, ct);
            return Ok(ApiResponse<List<DiagnosticOrderDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<DiagnosticOrderDto>>.Fail(ex.Message));
        }
    }

    [HttpGet("orders/{orderId}")]
    [ProducesResponseType(typeof(ApiResponse<DiagnosticOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrderById(long orderId, CancellationToken ct)
    {
        try
        {
            var result = await _imagingService.GetDiagnosticOrderByIdAsync(orderId, ct);
            if (result == null)
                return NotFound(ApiResponse<DiagnosticOrderDto>.Fail("Không tìm thấy chỉ định chẩn đoán."));

            return Ok(ApiResponse<DiagnosticOrderDto>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<DiagnosticOrderDto>.Fail(ex.Message));
        }
    }

    [HttpGet("slots")]
    [ProducesResponseType(typeof(ApiResponse<List<AvailableSlotDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableSlots(
        [FromQuery] long equipmentId,
        [FromQuery] DateTime date,
        [FromQuery] string diagnosticType,
        CancellationToken ct)
    {
        try
        {
            var result = await _imagingService.GetAvailableSlotsAsync(equipmentId, date, diagnosticType, ct);
            return Ok(ApiResponse<List<AvailableSlotDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<AvailableSlotDto>>.Fail(ex.Message));
        }
    }

    [HttpPost("sessions/book")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<ImagingSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BookSession([FromBody] BookImagingSessionRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _imagingService.BookImagingSessionAsync(request, userId, ct);
            return Ok(ApiResponse<ImagingSessionDto>.Ok(result, "Đặt lịch phiên chụp X-quang thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ImagingSessionDto>.Fail(ex.Message));
        }
    }

    [HttpPost("sessions/{sessionId}/check-in")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<ImagingSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckInSession(long sessionId, CancellationToken ct)
    {
        try
        {
            var result = await _imagingService.CheckInImagingSessionAsync(sessionId, ct);
            return Ok(ApiResponse<ImagingSessionDto>.Ok(result, $"Check-in thành công. Số thứ tự: {result.QueueNumber}"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ImagingSessionDto>.Fail(ex.Message));
        }
    }

    [HttpPost("sessions/{sessionId}/start")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<ImagingSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StartSession(long sessionId, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _imagingService.StartImagingSessionAsync(sessionId, userId, ct);
            return Ok(ApiResponse<ImagingSessionDto>.Ok(result, "Bắt đầu ca chụp X-quang thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ImagingSessionDto>.Fail(ex.Message));
        }
    }

    [HttpPost("sessions/{sessionId}/acquire")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<ImagingSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AcquireImage(long sessionId, CancellationToken ct)
    {
        try
        {
            var result = await _imagingService.AcquireImageAsync(sessionId, ct);
            return Ok(ApiResponse<ImagingSessionDto>.Ok(result, "Ghi nhận hình ảnh thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ImagingSessionDto>.Fail(ex.Message));
        }
    }

    [HttpPost("results/upload")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<DiagnosticResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadResult([FromBody] UploadDiagnosticResultRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _imagingService.UploadDiagnosticResultAsync(request, userId, ct);
            return Ok(ApiResponse<DiagnosticResultDto>.Ok(result, "Tải lên kết quả chẩn đoán hình ảnh thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<DiagnosticResultDto>.Fail(ex.Message));
        }
    }

    [HttpPost("sessions/{sessionId}/complete")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<ImagingSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteSession(long sessionId, CancellationToken ct)
    {
        try
        {
            var result = await _imagingService.CompleteImagingSessionAsync(sessionId, ct);
            return Ok(ApiResponse<ImagingSessionDto>.Ok(result, "Hoàn tất phiên chụp X-quang thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ImagingSessionDto>.Fail(ex.Message));
        }
    }

    [HttpPost("results/{resultId}/review")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<DiagnosticResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReviewResult(long resultId, [FromBody] ReviewDiagnosticResultRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _imagingService.ReviewDiagnosticResultAsync(resultId, request, userId, ct);
            return Ok(ApiResponse<DiagnosticResultDto>.Ok(result, "Bác sĩ đánh giá và lưu kết quả chẩn đoán thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<DiagnosticResultDto>.Fail(ex.Message));
        }
    }

    [HttpPost("sessions/{sessionId}/cancel")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<ImagingSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelSession(long sessionId, [FromQuery] string reason, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _imagingService.CancelImagingSessionAsync(sessionId, reason, userId, ct);
            return Ok(ApiResponse<ImagingSessionDto>.Ok(result, "Hủy phiên chụp X-quang thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ImagingSessionDto>.Fail(ex.Message));
        }
    }

    [HttpGet("queue/{roomId}")]
    [ProducesResponseType(typeof(ApiResponse<ImagingQueueSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueueStatus(long roomId, CancellationToken ct)
    {
        try
        {
            var result = await _imagingService.GetImagingQueueStatusAsync(roomId, ct);
            return Ok(ApiResponse<ImagingQueueSummaryDto>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ImagingQueueSummaryDto>.Fail(ex.Message));
        }
    }

    [HttpGet("equipments")]
    [ProducesResponseType(typeof(ApiResponse<List<ImagingEquipmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllEquipments(CancellationToken ct)
    {
        try
        {
            var result = await _imagingService.GetAllEquipmentsAsync(ct);
            return Ok(ApiResponse<List<ImagingEquipmentDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<ImagingEquipmentDto>>.Fail(ex.Message));
        }
    }

    [HttpPut("equipments/{equipmentId}/status")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<ImagingEquipmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateEquipmentStatus(long equipmentId, [FromBody] UpdateEquipmentStatusRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _imagingService.UpdateEquipmentStatusAsync(equipmentId, request.OperationalStatus, ct);
            return Ok(ApiResponse<ImagingEquipmentDto>.Ok(result, "Cập nhật trạng thái thiết bị thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ImagingEquipmentDto>.Fail(ex.Message));
        }
    }

    [HttpGet("configs")]
    [ProducesResponseType(typeof(ApiResponse<List<ImagingServiceConfigDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllConfigs(CancellationToken ct)
    {
        try
        {
            var result = await _imagingService.GetAllServiceConfigsAsync(ct);
            return Ok(ApiResponse<List<ImagingServiceConfigDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<ImagingServiceConfigDto>>.Fail(ex.Message));
        }
    }

    [HttpPut("configs/{configId}")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<ImagingServiceConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateConfig(long configId, [FromBody] UpdateImagingConfigRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _imagingService.UpdateServiceConfigAsync(configId, request, ct);
            return Ok(ApiResponse<ImagingServiceConfigDto>.Ok(result, "Cập nhật cấu hình dịch vụ chẩn đoán thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ImagingServiceConfigDto>.Fail(ex.Message));
        }
    }

    [HttpPost("results/upload-file")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<DiagnosticResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadResultFile(
        [FromForm] IFormFile file,
        [FromForm] long diagnosticOrderId,
        [FromForm] long imagingSessionId,
        [FromForm] long? equipmentId,
        [FromForm] string? bodyPart,
        [FromForm] string? findings,
        [FromForm] string? reviewNotes,
        CancellationToken ct)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse<DiagnosticResultDto>.Fail("Vui lòng đính kèm tệp hình ảnh chẩn đoán hợp lệ."));

            var userId = GetCurrentUserId();
            using var stream = file.OpenReadStream();
            var uploadResult = await _imagingStorageService.UploadDentalImageAsync(stream, file.FileName, file.ContentType, "clinical-imaging", ct);

            if (uploadResult == null || string.IsNullOrEmpty(uploadResult.Url))
                return BadRequest(ApiResponse<DiagnosticResultDto>.Fail("Không thể lưu trữ tệp hình ảnh chẩn đoán."));

            var request = new UploadDiagnosticResultRequest
            {
                DiagnosticOrderId = diagnosticOrderId,
                ImagingSessionId = imagingSessionId,
                EquipmentId = equipmentId,
                FileUrl = uploadResult.Url,
                ThumbnailUrl = uploadResult.Url,
                ImageFormat = System.IO.Path.GetExtension(file.FileName).TrimStart('.').ToUpperInvariant(),
                Findings = findings,
                PerformedAtUtc = DateTime.UtcNow
            };

            var result = await _imagingService.UploadDiagnosticResultAsync(request, userId, ct);
            return Ok(ApiResponse<DiagnosticResultDto>.Ok(result, "Tải lên tệp chẩn đoán hình ảnh thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<DiagnosticResultDto>.Fail(ex.Message));
        }
    }

    [HttpGet("results/{resultId}/image")]
    [Authorize]
    public async Task<IActionResult> GetResultImageStream(long resultId, CancellationToken ct)
    {
        try
        {
            var result = await _dbContext.DiagnosticResults
                .AsNoTracking()
                .Include(r => r.DiagnosticOrder)
                .FirstOrDefaultAsync(r => r.DiagnosticResultId == resultId, ct);

            if (result == null)
                return NotFound("Không tìm thấy kết quả chẩn đoán.");

            var stream = await _imagingStorageService.GetImageStreamAsync(result.FileUrl, ct);
            if (stream == null)
                return NotFound("Không thể tải luồng dữ liệu hình ảnh từ bộ lưu trữ.");

            var mimeType = "image/jpeg";
            if (result.FileUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) mimeType = "image/png";
            else if (result.FileUrl.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase)) mimeType = "application/dicom";

            return File(stream, mimeType);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ==========================================
    // CLINICAL CONSULTATIONS (HỘI CHẨN CHUYÊN MÔN)
    // ==========================================

    [HttpPost("consultations")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalConsultationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateConsultation([FromBody] CreateConsultationRequest request, CancellationToken ct)
    {
        try
        {
            var dentistUserId = GetCurrentUserId();
            var result = await _imagingService.CreateConsultationAsync(request, dentistUserId, ct);
            return Ok(ApiResponse<ClinicalConsultationDto>.Ok(result, "Gửi yêu cầu hội chẩn chuyên môn thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ClinicalConsultationDto>.Fail(ex.Message));
        }
    }

    [HttpPost("consultations/{id}/respond")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalConsultationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RespondConsultation(long id, [FromBody] RespondConsultationRequest request, CancellationToken ct)
    {
        try
        {
            var dentistUserId = GetCurrentUserId();
            var result = await _imagingService.RespondConsultationAsync(id, request, dentistUserId, ct);
            return Ok(ApiResponse<ClinicalConsultationDto>.Ok(result, "Gửi phản hồi ý kiến hội chẩn thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ClinicalConsultationDto>.Fail(ex.Message));
        }
    }

    [HttpGet("consultations/dentist")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<List<ClinicalConsultationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDentistConsultations([FromQuery] string? type, CancellationToken ct)
    {
        try
        {
            var dentistUserId = GetCurrentUserId();
            var result = await _imagingService.GetConsultationsByDentistAsync(dentistUserId, type ?? "all", ct);
            return Ok(ApiResponse<List<ClinicalConsultationDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<ClinicalConsultationDto>>.Fail(ex.Message));
        }
    }

    [HttpGet("consultations/appointment/{appointmentId}")]
    [ProducesResponseType(typeof(ApiResponse<List<ClinicalConsultationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAppointmentConsultations(long appointmentId, CancellationToken ct)
    {
        try
        {
            var result = await _imagingService.GetConsultationsByAppointmentAsync(appointmentId, ct);
            return Ok(ApiResponse<List<ClinicalConsultationDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<ClinicalConsultationDto>>.Fail(ex.Message));
        }
    }

    [HttpGet("consultations/{id}")]
    [ProducesResponseType(typeof(ApiResponse<ClinicalConsultationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConsultationById(long id, CancellationToken ct)
    {
        try
        {
            var result = await _imagingService.GetConsultationByIdAsync(id, ct);
            if (result == null)
                return NotFound(ApiResponse<ClinicalConsultationDto>.Fail("Không tìm thấy thông tin hội chẩn."));

            return Ok(ApiResponse<ClinicalConsultationDto>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ClinicalConsultationDto>.Fail(ex.Message));
        }
    }

    // =========================================================================
    // DEDICATED X-RAY DENTIST ROSTER ENDPOINTS
    // =========================================================================

    [HttpGet("roster")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<XrayDentistRosterDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRosters(
        [FromQuery] DateOnly? workDate,
        [FromQuery] long? roomId,
        [FromQuery] long? dentistUserId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        try
        {
            var filter = new XrayRosterFilterRequest
            {
                WorkDate = workDate,
                RoomId = roomId,
                DentistUserId = dentistUserId,
                Status = status
            };
            var result = await _xrayRosterService.GetRostersAsync(filter, ct);
            return Ok(ApiResponse<List<XrayDentistRosterDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<XrayDentistRosterDto>>.Fail(ex.Message));
        }
    }

    [HttpGet("roster/qualified-dentists")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<QualifiedDentistDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQualifiedDentists(CancellationToken ct)
    {
        try
        {
            var result = await _xrayRosterService.GetQualifiedDentistsAsync(ct);
            return Ok(ApiResponse<List<QualifiedDentistDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<QualifiedDentistDto>>.Fail(ex.Message));
        }
    }

    [HttpGet("roster/{id}")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<XrayDentistRosterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRosterById(long id, CancellationToken ct)
    {
        try
        {
            var result = await _xrayRosterService.GetRosterByIdAsync(id, ct);
            if (result == null)
                return NotFound(ApiResponse<XrayDentistRosterDto>.Fail("Không tìm thấy phân công trực phòng X-quang."));

            return Ok(ApiResponse<XrayDentistRosterDto>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<XrayDentistRosterDto>.Fail(ex.Message));
        }
    }

    [HttpPost("roster")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<XrayDentistRosterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignRoster([FromBody] AssignXrayRosterRequest request, CancellationToken ct)
    {
        try
        {
            var adminUserId = GetCurrentUserId();
            var result = await _xrayRosterService.AssignRosterAsync(request, adminUserId, ct);
            return Ok(ApiResponse<XrayDentistRosterDto>.Ok(result, "Phân công bác sĩ trực phòng X-quang thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<XrayDentistRosterDto>.Fail(ex.Message));
        }
    }

    [HttpPost("roster/{id}/replace")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<XrayDentistRosterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplaceDentist(long id, [FromBody] ReplaceXrayDentistRequest request, CancellationToken ct)
    {
        try
        {
            var adminUserId = GetCurrentUserId();
            var result = await _xrayRosterService.ReplaceDentistAsync(id, request, adminUserId, ct);
            return Ok(ApiResponse<XrayDentistRosterDto>.Ok(result, "Điều chuyển bác sĩ trực phòng X-quang thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<XrayDentistRosterDto>.Fail(ex.Message));
        }
    }

    [HttpDelete("roster/{id}")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelRoster(long id, [FromQuery] string? reason, CancellationToken ct)
    {
        try
        {
            var adminUserId = GetCurrentUserId();
            var result = await _xrayRosterService.CancelRosterAsync(id, reason ?? "Quản trị viên hủy phân công trực", adminUserId, ct);
            return Ok(ApiResponse<bool>.Ok(result, "Hủy phân công trực phòng X-quang thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<bool>.Fail(ex.Message));
        }
    }

    [HttpGet("roster/my-duty")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<List<XrayDentistRosterDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyXrayDuty(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct)
    {
        try
        {
            var dentistUserId = GetCurrentUserId();
            var from = fromDate ?? DateOnly.FromDateTime(DateTime.Today);
            var to = toDate ?? from.AddDays(7);
            var result = await _xrayRosterService.GetDentistXrayScheduleAsync(dentistUserId, from, to, ct);
            return Ok(ApiResponse<List<XrayDentistRosterDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<XrayDentistRosterDto>>.Fail(ex.Message));
        }
    }
}
