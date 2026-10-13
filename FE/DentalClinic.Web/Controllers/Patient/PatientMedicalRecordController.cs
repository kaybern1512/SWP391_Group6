using System.Threading.Tasks;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.MedicalRecord;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]
[Route("Patient/MedicalRecord")]
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
            Record = result.IsSuccess ? result.Data : null
        };

        return View("~/Views/Patient/MedicalRecords/Index.cshtml", vm);
    }

    [HttpGet("ImageContent/{id:long}")]
    public async Task<IActionResult> ImageContent(long id)
    {
        var result = await _medicalRecordApi.GetImageContentAsync(id);
        if (result == null)
        {
            return NotFound();
        }

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        var contentType = result.Value.ContentType.ToLowerInvariant();
        if (!allowedTypes.Contains(contentType))
        {
            return BadRequest();
        }

        Response.Headers["Cache-Control"] = "private, no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(result.Value.Stream, contentType);
    }
}
