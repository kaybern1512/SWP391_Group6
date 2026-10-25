using System.Threading.Tasks;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.MedicalRecord;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]
[Route("Patient/MedicalRecord")]
[Route("Patient/MedicalRecords")]
public class PatientMedicalRecordController : Controller
{
    private readonly IMedicalRecordApiService _medicalRecordApi;

    public PatientMedicalRecordController(IMedicalRecordApiService medicalRecordApi)
    {
        _medicalRecordApi = medicalRecordApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var result = await _medicalRecordApi.GetMyMedicalRecordAsync();
        var vm = new PatientMedicalRecordIndexViewModel
        {
            Record = result != null && result.IsSuccess ? result.Data : null
        };

        return View("~/Views/Patient/MedicalRecords/Index.cshtml", vm);
    }

    [HttpGet("ImagingContent/{imagingRecordId:long}")]
    public async Task<IActionResult> ImagingContent(long imagingRecordId)
    {
        if (imagingRecordId <= 0)
        {
            return NotFound();
        }

        var (stream, contentType, statusCode) = await _medicalRecordApi.GetImagingStreamAsync(imagingRecordId);
        if (statusCode == 401)
        {
            return Unauthorized();
        }

        if (statusCode == 403)
        {
            return Forbid();
        }

        if (statusCode == 404 || stream == null)
        {
            return NotFound();
        }

        if (statusCode != 200)
        {
            return StatusCode(statusCode);
        }

        return File(stream, contentType);
    }
}
