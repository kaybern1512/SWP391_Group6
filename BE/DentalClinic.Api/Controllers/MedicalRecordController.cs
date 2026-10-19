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

    [Obsolete("Endpoint này đã deprecated vì lý do bảo mật. Vui lòng sử dụng POST /api/medical-records/clinical-entries/{entryId}/imaging để tải lên tập tin có xác thực.")]
    [HttpPost("imaging")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<ImagingRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddImagingRecord([FromBody] CreateImagingRecordRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _medicalRecordService.AddImagingRecordAsync(currentUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("clinical-entries/{entryId:long}/imaging")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ImagingRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImagingRecord(
        long entryId,
        [FromForm] UploadImagingFileRequest request,
        CancellationToken ct)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(ApiResponse<ImagingRecordDto>.Fail("Vui lòng đính kèm tập tin hình ảnh phim chụp hoặc file DICOM."));
        }

        var currentUserId = GetCurrentUserId();
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        await using var stream = request.File.OpenReadStream();
        var result = await _medicalRecordService.UploadImagingRecordAsync(
            currentUserId,
            currentUserRole,
            entryId,
            request.ImagingType,
            request.ResultSummary,
            stream,
            request.File.FileName,
            request.File.ContentType,
            ct);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpDelete("imaging/{id:long}")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImagingRecord(long id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var result = await _medicalRecordService.DeleteImagingRecordAsync(currentUserId, currentUserRole, id, ct);
        if (!result.Success)
        {
            if (result.Message != null && result.Message.Contains("Không tìm thấy", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("imaging/{id:long}/file")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.Patient},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImagingFile(long id, [FromQuery] bool download = false, CancellationToken ct = default)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var result = await _medicalRecordService.GetImagingFileAsync(currentUserId, currentUserRole, id, ct);
        if (!result.Success || result.Data == null)
        {
            if (result.Message != null && result.Message.Contains("Không tìm thấy", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(ApiResponse.Fail(result.Message));
            }
            if (result.Message != null && result.Message.Contains("không có quyền", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(result.Message));
            }
            return BadRequest(ApiResponse.Fail(result.Message ?? "Không thể truy xuất tệp phim chụp."));
        }

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        if (download && !string.IsNullOrWhiteSpace(result.Data.FileName))
        {
            return File(result.Data.Stream, result.Data.ContentType, result.Data.FileName);
        }

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{result.Data.FileName}\"";
        return File(result.Data.Stream, result.Data.ContentType);
    }
}

public class UploadImagingFileRequest
{
    public IFormFile? File { get; set; }
    public string ImagingType { get; set; } = string.Empty;
    public string? ResultSummary { get; set; }
}
