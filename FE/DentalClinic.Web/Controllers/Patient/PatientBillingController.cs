using System.Security.Claims;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]
[Route("Patient/Invoices")]
public class PatientBillingController : Controller
{
    private readonly IInvoiceService _invoiceService;
    private readonly IReceiptPdfService _receiptPdfService;

    public PatientBillingController(IInvoiceService invoiceService, IReceiptPdfService receiptPdfService)
    {
        _invoiceService = invoiceService;
        _receiptPdfService = receiptPdfService;
    }

    [HttpGet]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        var patientCode = User.FindFirst("UserCode")?.Value ?? "PT-1001";
        var invoices = _invoiceService.GetForPatient(patientCode);

        var vm = new BillingInvoiceListViewModel
        {
            PatientName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Bệnh nhân",
            Invoices = invoices.ToList()
        };

        return View("~/Views/Patient/Invoices/Index.cshtml", vm);
    }

    [HttpGet("{id:int}")]
    public IActionResult Detail(int id)
    {
        var invoice = _invoiceService.GetById(id);
        if (invoice is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy hóa đơn yêu cầu.";
            return RedirectToAction(nameof(Index));
        }

        var patientCode = User.FindFirst("UserCode")?.Value ?? "PT-1001";
        if (!string.Equals(invoice.PatientCode, patientCode, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xem hóa đơn này.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new BillingInvoiceDetailViewModel { Invoice = invoice };
        return View("~/Views/Patient/Invoices/Detail.cshtml", vm);
    }

    [HttpGet("{id:int}/Receipt")]
    public IActionResult Receipt(int id)
    {
        var invoice = _invoiceService.GetById(id);
        if (invoice is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy biên lai cần xem.";
            return RedirectToAction(nameof(Index));
        }

        var patientCode = User.FindFirst("UserCode")?.Value ?? "PT-1001";
        if (!string.Equals(invoice.PatientCode, patientCode, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xem biên lai này.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new BillingInvoiceDetailViewModel { Invoice = invoice };
        return View("~/Views/Patient/Invoices/Receipt.cshtml", vm);
    }

    [HttpGet("{id:int}/Receipt.pdf")]
    public IActionResult DownloadReceiptPdf(int id)
    {
        var invoice = _invoiceService.GetById(id);
        if (invoice is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy biên lai cần tải.";
            return RedirectToAction(nameof(Index));
        }

        var patientCode = User.FindFirst("UserCode")?.Value ?? "PT-1001";
        if (!string.Equals(invoice.PatientCode, patientCode, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền tải biên lai này.";
            return RedirectToAction(nameof(Index));
        }

        var pdf = _receiptPdfService.GenerateReceipt(invoice);
        return File(pdf, "application/pdf", $"receipt-{invoice.InvoiceCode}.pdf");
    }

    [HttpPost("ConfirmPayment/{id:int}")]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmPayment(int id, [FromForm] string paymentMethod, [FromForm] string? transactionReference)
    {
        var invoice = _invoiceService.GetById(id);
        if (invoice is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy hóa đơn để thanh toán.";
            return RedirectToAction(nameof(Index));
        }

        var patientCode = User.FindFirst("UserCode")?.Value ?? "PT-1001";
        if (!string.Equals(invoice.PatientCode, patientCode, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xác nhận thanh toán cho hóa đơn này.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            TempData["ErrorMessage"] = "Vui lòng chọn phương thức thanh toán.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var confirmed = _invoiceService.TryConfirmPayment(id, paymentMethod, transactionReference, out _);
        if (!confirmed)
        {
            TempData["ErrorMessage"] = "Không thể xác nhận thanh toán cho hóa đơn này.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        TempData["SuccessMessage"] = "Đã xác nhận thanh toán thành công. Biên lai đã được cập nhật.";
        return RedirectToAction(nameof(Receipt), new { id });
    }
}
