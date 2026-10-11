using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.ClinicalCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]
[Route("Patient/MedicalRecord")]
public class PatientMedicalRecordController : Controller
{
    private readonly IClinicalCoreApiClient _clinicalCoreApi;

    public PatientMedicalRecordController(IClinicalCoreApiClient clinicalCoreApi)
    {
        _clinicalCoreApi = clinicalCoreApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (!long.TryParse(claim, out var patientUserId) || patientUserId <= 0)
        {
            return Forbid();
        }

        var result = await _clinicalCoreApi.GetMedicalRecordAsync(patientUserId);
        if (!result.IsSuccess && result.StatusCode == 403)
        {
            return Forbid();
        }

        var model = new PatientMedicalRecordViewModel
        {
            MedicalRecord = result.IsSuccess ? result.Data : null,
            Message = result.IsSuccess ? null : result.Message
        };

        return View("~/Views/Patient/MedicalRecord/Index.cshtml", model);
    }
}
