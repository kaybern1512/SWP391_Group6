using DentalClinic.Web.Models.Api;
using DentalClinic.Web.ViewModels.Invoices;

namespace DentalClinic.Web.Services;

public interface IInvoiceApiClient
{
    Task<ApiResult<List<BillingInvoiceRecordViewModel>>> GetAllInvoices();
    Task<ApiResult<BillingInvoiceRecordViewModel>> GetInvoiceById(int invoiceId);
    Task<ApiResult<BillingInvoiceRecordViewModel>> ConfirmPayment(int invoiceId, string paymentMethod, string? transactionReference);
    Task<ApiResult<InvoiceReportViewModel>> GetInvoiceReport();
}
