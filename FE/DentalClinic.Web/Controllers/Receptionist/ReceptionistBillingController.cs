using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist,SystemAdministrator")]
[Route("Receptionist/Billing")]
public class ReceptionistBillingController : Controller
{
    private readonly IBillingApiService _billingApiService;

    public ReceptionistBillingController(IBillingApiService billingApiService)
    {
        _billingApiService = billingApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var invoicesTask = _billingApiService.GetInvoicesAsync(status, search, fromDate, toDate);
        var billableTask = _billingApiService.GetBillableAppointmentsAsync();

        await Task.WhenAll(invoicesTask, billableTask);

        var invoices = invoicesTask.Result.Data ?? new List<InvoiceDto>();
        var billableAppointments = billableTask.Result.Data ?? new List<CompletedAppointmentForBillingDto>();

        ViewBag.CurrentStatus = status;
        ViewBag.SearchTerm = search;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        ViewBag.BillableAppointments = billableAppointments;

        return View("~/Views/Receptionist/Billing/Index.cshtml", invoices);
    }

    [HttpGet("Create/{appointmentId:long}")]
    public async Task<IActionResult> Create(long appointmentId)
    {
        var result = await _billingApiService.GetPreparationAsync(appointmentId);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể lấy thông tin ca khám để lập hóa đơn.";
            return RedirectToAction(nameof(Index));
        }

        var prep = result.Data;
        if (prep.HasInvoice && prep.ExistingInvoiceId.HasValue)
        {
            TempData["InfoMessage"] = $"Ca khám này đã có hóa đơn #{prep.ExistingInvoiceCode}.";
            return RedirectToAction(nameof(Detail), new { id = prep.ExistingInvoiceId.Value });
        }

        return View("~/Views/Receptionist/Billing/Create.cshtml", prep);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request)
    {
        if (request == null || request.AppointmentId <= 0)
        {
            return Json(new { success = false, message = "Dữ liệu hóa đơn không hợp lệ." });
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return Json(new { success = false, message = "Vui lòng thêm ít nhất một khoản mục dịch vụ / viện phí." });
        }

        var result = await _billingApiService.CreateInvoiceAsync(request);
        if (result.IsSuccess && result.Data != null)
        {
            TempData["SuccessMessage"] = $"Lập hóa đơn #{result.Data.InvoiceCode} thành công!";
            return Json(new { success = true, invoiceId = result.Data.InvoiceId, redirectUrl = Url.Action("Detail", new { id = result.Data.InvoiceId }) });
        }

        return Json(new { success = false, message = result.Message ?? "Không thể tạo hóa đơn." });
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

        return View("~/Views/Receptionist/Billing/Detail.cshtml", result.Data);
    }

    [HttpPost("Pay")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(ProcessPaymentRequest request)
    {
        if (request == null || request.InvoiceId <= 0 || request.Amount <= 0)
        {
            TempData["ErrorMessage"] = "Thông tin thanh toán không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _billingApiService.ProcessPaymentAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Ghi nhận thanh toán thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Giao dịch thanh toán thất bại.";
        }

        return RedirectToAction(nameof(Detail), new { id = request.InvoiceId });
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
