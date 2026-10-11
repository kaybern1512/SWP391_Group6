using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist,SystemAdministrator")]
[Route("Receptionist/Invoices")]
public class ReceptionistBillingController : Controller
{
    private readonly IInvoiceService _invoiceService;
    private readonly IReceiptPdfService _receiptPdfService;

    public ReceptionistBillingController(IInvoiceService invoiceService, IReceiptPdfService receiptPdfService)
    {
        _invoiceService = invoiceService;
        _receiptPdfService = receiptPdfService;
    }

    [HttpGet]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        var invoices = _invoiceService.GetPendingForReception();

        var vm = new BillingInvoiceListViewModel
        {
            PatientName = "Reception Desk",
            Invoices = invoices.ToList()
        };

        return View("~/Views/Receptionist/Invoices/Index.cshtml", vm);
    }

    [HttpGet("Report")]
    public IActionResult Report()
    {
        var invoices = _invoiceService.GetAll();
        var report = new InvoiceReportViewModel
        {
            TotalInvoices = invoices.Count,
            PaidInvoices = invoices.Count(i => i.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase)),
            PendingInvoices = invoices.Count(i => !i.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase)),
            TotalRevenue = invoices.Where(i => i.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase)).Sum(i => i.TotalAmount),
            PendingAmount = invoices.Where(i => !i.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase)).Sum(i => i.TotalAmount),
            RevenueThisMonth = invoices.Where(i => i.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase) && i.PaidAt.HasValue && i.PaidAt.Value >= DateTime.UtcNow.AddDays(-30)).Sum(i => i.TotalAmount),
            LatestInvoiceDate = invoices.Any() ? invoices.Max(i => i.CreatedAt) : null,
            GeneratedAt = DateTime.UtcNow
        };

        return View("~/Views/Receptionist/Invoices/Report.cshtml", report);
    }

    [HttpGet("{id:int}")]
    public IActionResult Detail(int id)
    {
        var invoice = _invoiceService.GetById(id);
        if (invoice is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy hóa đơn cần xử lý.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new BillingInvoiceDetailViewModel { Invoice = invoice };
        return View("~/Views/Receptionist/Invoices/Detail.cshtml", vm);
    }

    [HttpGet("{id:int}/Receipt")]
    public IActionResult Receipt(int id)
    {
        var invoice = _invoiceService.GetById(id);
        if (invoice is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy biên lai.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new BillingInvoiceDetailViewModel { Invoice = invoice };
        return View("~/Views/Receptionist/Invoices/Receipt.cshtml", vm);
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
            TempData["ErrorMessage"] = "Không tìm thấy hóa đơn để xác nhận thanh toán.";
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

        TempData["SuccessMessage"] = "Đã ghi nhận thanh toán và xuất biên lai cho bệnh nhân.";
        return RedirectToAction(nameof(Receipt), new { id });
    }
}
