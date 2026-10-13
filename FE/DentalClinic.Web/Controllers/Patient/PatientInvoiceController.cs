using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]
[Route("Patient/Invoices")]
public class PatientInvoiceController : Controller
{
    private readonly IBillingApiService _billingApiService;

    public PatientInvoiceController(IBillingApiService billingApiService)
    {
        _billingApiService = billingApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var result = await _billingApiService.GetPatientInvoicesAsync();
        var list = result.Data ?? new List<InvoiceDto>();
        return View("~/Views/Patient/Invoices/Index.cshtml", list);
    }

    [HttpGet("Detail/{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var result = await _billingApiService.GetInvoiceByIdAsync(id);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy hóa đơn.";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Patient/Invoices/Detail.cshtml", result.Data);
    }

    [HttpGet("Print/{id:long}")]
    public async Task<IActionResult> Print(long id)
    {
        var result = await _billingApiService.GetInvoiceByIdAsync(id);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy thông tin hóa đơn.";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Receptionist/Billing/Print.cshtml", result.Data);
    }
}
