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
    private readonly IMedicineApiService? _medicineApi;

    public PatientPrescriptionController(
        IPrescriptionApiService prescriptionApi,
        IMedicineApiService? medicineApi = null)
    {
        _prescriptionApi = prescriptionApi;
        _medicineApi = medicineApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var result = await _prescriptionApi.GetMyPrescriptionsAsync();
        var prescriptions = result.IsSuccess && result.Data != null ? result.Data : new List<PrescriptionDto>();

        var statusMap = new Dictionary<long, PrescriptionDispensationStatusDto>();
        if (_medicineApi != null)
        {
            foreach (var p in prescriptions)
            {
                var statusResult = await _medicineApi.GetPrescriptionDispensationStatusAsync(p.PrescriptionId);
                if (statusResult.IsSuccess && statusResult.Data != null)
                {
                    statusMap[p.PrescriptionId] = statusResult.Data;
                }
            }
        }

        var vm = new PatientPrescriptionIndexViewModel
        {
            Prescriptions = prescriptions,
            DispensationStatusMap = statusMap
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
