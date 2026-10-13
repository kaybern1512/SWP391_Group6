using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/medical-records")]
[Authorize]
public class MedicalRecordController : ControllerBase
{
    private readonly IMedicalRecordService _medicalRecordService;

    public MedicalRecordController(IMedicalRecordService medicalRecordService)
    {
        _medicalRecordService = medicalRecordService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpGet("patient/{patientUserId:long}")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator},{UserRole.Receptionist},{UserRole.DepartmentManager}")]
    [ProducesResponseType(typeof(ApiResponse<MedicalRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPatientId(long patientUserId, CancellationToken ct)
    {
        var result = await _medicalRecordService.GetMedicalRecordByPatientIdAsync(patientUserId, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpGet("my")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<MedicalRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyMedicalRecord(CancellationToken ct)
    {
        var patientUserId = GetCurrentUserId();
        var result = await _medicalRecordService.GetMedicalRecordByPatientIdAsync(patientUserId, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpPost("entry")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateClinicalEntry([FromBody] CreateClinicalEntryRequest request, CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _medicalRecordService.CreateClinicalEntryAsync(dentistUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value
            ?? User.FindFirst("role")?.Value
            ?? string.Empty;
    }

    [HttpGet("patients")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<PatientLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchPatients([FromQuery] string? search, CancellationToken ct)
    {
        var result = await _medicalRecordService.SearchPatientsForMedicalRecordAsync(search, ct);
        return Ok(result);
    }

    [HttpGet("patient/{patientUserId:long}/odontogram")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<OdontogramEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentOdontogram(long patientUserId, CancellationToken ct)
    {
        var result = await _medicalRecordService.GetCurrentOdontogramAsync(patientUserId, ct);
        return Ok(result);
    }

    [HttpPost("clinical-entries/{clinicalEntryId:long}/imaging")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ImagingRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadImaging(
        long clinicalEntryId,
        [FromForm] IFormFile? file,
        [FromForm] string imagingType,
        [FromForm] string? resultSummary,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<ImagingRecordDto>.Fail("Vui lòng chọn tệp hình ảnh để tải lên."));
        }

        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();

        using var stream = file.OpenReadStream();
        var result = await _medicalRecordService.UploadImagingRecordAsync(
            currentUserId,
            currentUserRole,
            clinicalEntryId,
            stream,
            file.FileName,
            file.ContentType,
            imagingType,
            resultSummary,
            ct);

        if (!result.Success)
        {
            var msg = result.Message ?? string.Empty;
            if (msg.Contains("không có quyền", System.StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            if (msg.Contains("Không tìm thấy", System.StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("clinical-entries/{clinicalEntryId:long}/imaging")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<ImagingRecordDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImagingRecords(long clinicalEntryId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();

        var result = await _medicalRecordService.GetImagingRecordsByClinicalEntryAsync(
            currentUserId,
            currentUserRole,
            clinicalEntryId,
            ct);

        if (!result.Success)
        {
            var msg = result.Message ?? string.Empty;
            if (msg.Contains("không có quyền", System.StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            if (msg.Contains("Không tìm thấy", System.StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("imaging/{imagingRecordId:long}/content")]
    [Authorize]
    public async Task<IActionResult> GetImagingContent(long imagingRecordId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();

        var result = await _medicalRecordService.GetImagingRecordContentAsync(
            currentUserId,
            currentUserRole,
            imagingRecordId,
            ct);

        if (!result.Success || result.Data == null)
        {
            var msg = result.Message ?? string.Empty;
            if (msg.Contains("không có quyền", System.StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            return NotFound(result);
        }

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        var canonicalMime = result.Data.ContentType.ToLowerInvariant();
        if (!allowedTypes.Contains(canonicalMime))
        {
            return BadRequest(ApiResponse<string>.Fail("Định dạng tệp không hợp lệ."));
        }

        Response.Headers["Cache-Control"] = "private, no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(result.Data.Stream, canonicalMime);
    }

    [HttpDelete("imaging/{imagingRecordId:long}")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteImaging(long imagingRecordId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();

        var result = await _medicalRecordService.DeleteImagingRecordAsync(
            currentUserId,
            currentUserRole,
            imagingRecordId,
            ct);

        if (!result.Success)
        {
            var msg = result.Message ?? string.Empty;
            if (msg.Contains("không có quyền", System.StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            if (msg.Contains("Không tìm thấy", System.StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }

        return Ok(result);
    }
}
