using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ClinicalCore.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/clinical-core")]
[Authorize]
public class ClinicalCoreController : ControllerBase
{
    private readonly IClinicalCoreService _clinicalCoreService;

    public ClinicalCoreController(IClinicalCoreService clinicalCoreService)
    {
        _clinicalCoreService = clinicalCoreService;
    }

    [HttpGet("patients/{patientUserId:long}/medical-record")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<DentalMedicalRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DentalMedicalRecordDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<DentalMedicalRecordDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMedicalRecord(long patientUserId, CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse<DentalMedicalRecordDto>.Fail("Không thể xác định danh tính người dùng."));
        }

        var result = await _clinicalCoreService.GetMedicalRecordAsync(patientUserId, userId, role, ct);
        return ToResult(result);
    }

    [HttpPost("patients/{patientUserId:long}/medical-record")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager}")]
    [ProducesResponseType(typeof(ApiResponse<DentalMedicalRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DentalMedicalRecordDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetOrCreateMedicalRecord(long patientUserId, CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse<DentalMedicalRecordDto>.Fail("Không thể xác định danh tính người dùng."));
        }

        var result = await _clinicalCoreService.GetOrCreateMedicalRecordAsync(patientUserId, userId, role, ct);
        return ToResult(result);
    }

    [HttpPost("patients/{patientUserId:long}/clinical-entries")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalEntryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateClinicalEntry(
        long patientUserId,
        [FromBody] CreateClinicalEntryRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse<ClinicalEntryDto>.Fail("Không thể xác định danh tính người dùng."));
        }

        request.PatientUserId = patientUserId;
        request.DentistUserId = userId;

        var result = await _clinicalCoreService.CreateClinicalEntryAsync(request, userId, role, ct);
        return ToResult(result);
    }

    [HttpGet("patients/{patientUserId:long}/treatment-plans")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> GetTreatmentPlans(long patientUserId, CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.GetTreatmentPlansForPatientAsync(patientUserId, userId, role, ct));
    }

    [HttpGet("treatment-plans/{treatmentPlanId:long}")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> GetTreatmentPlan(long treatmentPlanId, CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.GetTreatmentPlanByIdAsync(treatmentPlanId, userId, role, ct));
    }

    [HttpPost("treatment-plans")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<TreatmentPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TreatmentPlanDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTreatmentPlan(
        [FromBody] CreateTreatmentPlanRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse<TreatmentPlanDto>.Fail("Không thể xác định danh tính người dùng."));
        }

        request.CreatedByDentistUserId = userId;

        var result = await _clinicalCoreService.CreateTreatmentPlanAsync(request, userId, role, ct);
        return ToResult(result);
    }

    [HttpPatch("treatment-plans/{treatmentPlanId:long}/status")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> UpdateTreatmentPlanStatus(
        long treatmentPlanId,
        [FromBody] UpdateTreatmentPlanStatusRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.UpdateTreatmentPlanStatusAsync(
            treatmentPlanId, request, userId, role, ct));
    }

    [HttpPatch("treatment-plan-items/{treatmentPlanItemId:long}/status")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> UpdateTreatmentPlanItemStatus(
        long treatmentPlanItemId,
        [FromBody] UpdateTreatmentPlanItemStatusRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.UpdateTreatmentPlanItemStatusAsync(
            treatmentPlanItemId, request, userId, role, ct));
    }

    [HttpGet("patients/{patientUserId:long}/treatment-sessions")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> GetTreatmentSessions(long patientUserId, CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.GetTreatmentSessionsForPatientAsync(patientUserId, userId, role, ct));
    }

    [HttpPost("treatment-sessions")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<TreatmentSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TreatmentSessionDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTreatmentSession(
        [FromBody] CreateTreatmentSessionRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse<TreatmentSessionDto>.Fail("Không thể xác định danh tính người dùng."));
        }

        request.DentistUserId = userId;

        var result = await _clinicalCoreService.CreateTreatmentSessionAsync(request, userId, role, ct);
        return ToResult(result);
    }

    [HttpGet("patients/{patientUserId:long}/prescriptions")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> GetPrescriptions(long patientUserId, CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.GetPrescriptionsForPatientAsync(patientUserId, userId, role, ct));
    }

    [HttpGet("prescriptions/{prescriptionId:long}")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> GetPrescription(long prescriptionId, CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.GetPrescriptionByIdAsync(prescriptionId, userId, role, ct));
    }

    [HttpGet("treatment-sessions/{treatmentSessionId:long}/prescription")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> GetPrescriptionForTreatmentSession(long treatmentSessionId, CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.GetPrescriptionForTreatmentSessionAsync(
            treatmentSessionId, userId, role, ct));
    }

    [HttpPost("prescriptions")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<PrescriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PrescriptionDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePrescription(
        [FromBody] CreatePrescriptionRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse<PrescriptionDto>.Fail("Không thể xác định danh tính người dùng."));
        }

        var result = await _clinicalCoreService.CreatePrescriptionAsync(request, userId, role, ct);
        return ToResult(result);
    }

    [HttpGet("patients/{patientUserId:long}/department-referrals")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> GetPatientDepartmentReferrals(long patientUserId, CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.GetDepartmentReferralsForPatientAsync(
            patientUserId, userId, role, ct));
    }

    [HttpGet("department-referrals")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> GetDepartmentReferrals([FromQuery] string? status, CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.GetDepartmentReferralsForManagerAsync(
            userId, role, status, ct));
    }

    [HttpPost("department-referrals")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager}")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentReferralDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentReferralDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDepartmentReferral(
        [FromBody] CreateDepartmentReferralRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse<DepartmentReferralDto>.Fail("Không thể xác định danh tính người dùng."));
        }

        var result = await _clinicalCoreService.CreateDepartmentReferralAsync(request, userId, role, ct);
        return ToResult(result);
    }

    [HttpPost("department-referrals/{referralId:long}/review")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> ReviewDepartmentReferral(
        long referralId,
        [FromBody] ReviewDepartmentReferralRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.ReviewDepartmentReferralAsync(
            referralId, request, userId, role, ct));
    }

    [HttpPost("department-referrals/{referralId:long}/assign")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> AssignDepartmentReferral(
        long referralId,
        [FromBody] AssignDepartmentReferralRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.AssignDepartmentReferralAsync(
            referralId, request, userId, role, ct));
    }

    [HttpPost("department-referrals/{referralId:long}/complete")]
    [Authorize(Roles = UserRole.Dentist)]
    public async Task<IActionResult> CompleteDepartmentReferral(long referralId, CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.CompleteDepartmentReferralAsync(
            referralId, userId, role, ct));
    }

    [HttpGet("patients/{patientUserId:long}/follow-ups")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> GetFollowUps(long patientUserId, CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.GetFollowUpsForPatientAsync(
            patientUserId, userId, role, ct));
    }

    [HttpPost("follow-ups")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<FollowUpScheduleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FollowUpScheduleDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFollowUp(
        [FromBody] CreateFollowUpRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse<FollowUpScheduleDto>.Fail("Không thể xác định danh tính người dùng."));
        }

        var result = await _clinicalCoreService.CreateFollowUpAsync(request, userId, role, ct);
        return ToResult(result);
    }

    [HttpPost("follow-ups/{followUpId:long}/appointment")]
    [Authorize(Roles = UserRole.Patient)]
    public async Task<IActionResult> LinkFollowUpAppointment(
        long followUpId,
        [FromBody] LinkFollowUpAppointmentRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));
        }

        return ToResult(await _clinicalCoreService.LinkFollowUpAppointmentAsync(
            followUpId, request, userId, role, ct));
    }

    private bool TryGetCurrentUser(out long userId, out string role)
    {
        var idValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        return long.TryParse(idValue, out userId) && userId > 0;
    }

    private IActionResult ToResult<T>(ApiResponse<T> result)
    {
        if (result.Success)
        {
            return Ok(result);
        }

        if (result.Message?.Contains("quyền", System.StringComparison.OrdinalIgnoreCase) == true
            || result.Message?.StartsWith("Bạn chỉ được", System.StringComparison.OrdinalIgnoreCase) == true)
        {
            return StatusCode(StatusCodes.Status403Forbidden, result);
        }

        if (result.Message?.Contains("không tìm thấy", System.StringComparison.OrdinalIgnoreCase) == true
            || result.Message?.Contains("không tồn tại", System.StringComparison.OrdinalIgnoreCase) == true)
        {
            return NotFound(result);
        }

        return BadRequest(result);
    }
}
