using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Prescription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]
[Route("Patient/Prescriptions")]
[Route("Patient/Prescription")]
public class PatientPrescriptionController : Controller
{
    private readonly IPrescriptionApiService _prescriptionApi;

    public PatientPrescriptionController(IPrescriptionApiService prescriptionApi)
    {
        _prescriptionApi = prescriptionApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var result = await _prescriptionApi.GetMyPrescriptionsAsync();
        var vm = new PatientPrescriptionIndexViewModel
        {
            Prescriptions = result.IsSuccess && result.Data != null ? result.Data : new List<PrescriptionDto>()
        };

        return View("~/Views/Patient/Prescriptions/Index.cshtml", vm);
    }

    [HttpGet("{id:long}/Print")]
    public async Task<IActionResult> Print(long id)
    {
        var result = await _prescriptionApi.GetPrescriptionDetailAsync(id);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn thuốc.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new PrescriptionPrintViewModel
        {
            Prescription = result.Data
        };

        return View("~/Views/Dentist/Prescriptions/Print.cshtml", vm);
    }
}
