using System.Threading.Tasks;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Treatment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]
[Route("Patient/TreatmentPlans")]
public class PatientTreatmentPlanController : Controller
{
    private readonly ITreatmentApiService _treatmentApiService;

    public PatientTreatmentPlanController(ITreatmentApiService treatmentApiService)
    {
        _treatmentApiService = treatmentApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var result = await _treatmentApiService.GetPatientMyPlansAsync();
        var vm = new PatientTreatmentPlanListViewModel
        {
            Plans = result.Data ?? new()
        };

        return View("~/Views/Patient/TreatmentPlans/Index.cshtml", vm);
    }

    [HttpGet("Detail/{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var result = await _treatmentApiService.GetTreatmentPlanDetailAsync(id);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy phác đồ điều trị.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new DentistTreatmentPlanDetailViewModel
        {
            Plan = result.Data
        };

        return View("~/Views/Patient/TreatmentPlans/Detail.cshtml", vm);
    }
}
