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

public class DentalImagingUploadForm
{
    public IFormFile? File { get; set; }
    public string? ImagingType { get; set; }
    public string? ResultSummary { get; set; }
}

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

    private string GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
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

    [HttpPost("imaging")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.Receptionist}")]
    [ProducesResponseType(typeof(ApiResponse<ImagingRecordDto>), StatusCodes.Status200OK)]
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

    [HttpPost("clinical-entries/{id:long}/imaging")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator},{UserRole.DepartmentManager}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ImagingRecordMetadataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ImagingRecordMetadataDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ImagingRecordMetadataDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<ImagingRecordMetadataDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<ImagingRecordMetadataDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadClinicalEntryImaging(
        [FromRoute] long id,
        [FromForm] DentalImagingUploadForm form,
        CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized(ApiResponse<ImagingRecordMetadataDto>.Fail("Không xác định được danh tính người dùng đăng nhập."));
        }

        var currentUserRole = GetCurrentUserRole();

        if (id <= 0)
        {
            return BadRequest(ApiResponse<ImagingRecordMetadataDto>.Fail("Mã đợt khám (ClinicalEntryId) không hợp lệ."));
        }

        if (form.File == null || form.File.Length == 0)
        {
            return BadRequest(ApiResponse<ImagingRecordMetadataDto>.Fail("Tập tin hình ảnh không được để trống."));
        }

        if (string.IsNullOrWhiteSpace(form.ImagingType))
        {
            return BadRequest(ApiResponse<ImagingRecordMetadataDto>.Fail("Loại phim chụp không được để trống."));
        }

        await using var stream = form.File.OpenReadStream();

        var uploadRequest = new UploadImagingRecordRequest
        {
            ClinicalEntryId = id,
            ImagingType = form.ImagingType.Trim(),
            ResultSummary = form.ResultSummary,
            File = new DentalImageFileInput
            {
                Content = stream,
                FileName = form.File.FileName,
                ContentType = form.File.ContentType,
                Length = form.File.Length
            }
        };

        var result = await _medicalRecordService.UploadImagingRecordAsync(currentUserId, currentUserRole, uploadRequest, ct);

        if (!result.Success)
        {
            if (result.Message != null && result.Message.Contains("Không tìm thấy đợt khám"))
            {
                return NotFound(result);
            }

            if (result.Message != null && (result.Message.Contains("không có quyền") || result.Message.Contains("quyền thao tác")))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }

            return BadRequest(result);
        }

        return Ok(result);
    }
}
