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

    public ClinicalDiagnosticsController(IImagingService imagingService)
    {
        _imagingService = imagingService;
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
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.Dentist},{UserRole.SystemAdministrator}")]
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
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.Dentist},{UserRole.SystemAdministrator}")]
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
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
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
}
