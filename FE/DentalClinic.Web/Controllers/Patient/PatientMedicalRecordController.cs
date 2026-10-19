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

    [HttpGet("ImagingFile/{id:long}")]
    public async Task<IActionResult> GetImagingFile(long id, [FromQuery] bool download = false)
    {
        var streamResult = await _medicalRecordApi.GetImagingFileStreamAsync(id, download);
        if (streamResult == null)
        {
            return NotFound();
        }

        if (streamResult.StatusCode == 401)
        {
            streamResult.Dispose();
            return Unauthorized();
        }
        if (streamResult.StatusCode == 403)
        {
            streamResult.Dispose();
            return Forbid();
        }
        if (streamResult.StatusCode == 404 || streamResult.Stream == null)
        {
            streamResult.Dispose();
            return NotFound();
        }

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        if (download && !string.IsNullOrWhiteSpace(streamResult.FileName))
        {
            return File(streamResult.Stream, streamResult.ContentType, streamResult.FileName);
        }

        return File(streamResult.Stream, streamResult.ContentType);
    }
}
