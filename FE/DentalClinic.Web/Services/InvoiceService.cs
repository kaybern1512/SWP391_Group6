using DentalClinic.Web.Models.Billing;
using DentalClinic.Web.ViewModels.Invoices;

namespace DentalClinic.Web.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IInvoiceApiClient? _apiClient;

    public InvoiceService(IInvoiceApiClient? invoiceApiClient = null)
    {
        _apiClient = invoiceApiClient;
    }

    public IReadOnlyList<BillingInvoiceRecordViewModel> GetAll()
    {
        var apiInvoices = TryGetFromApi();
        if (apiInvoices != null) return apiInvoices;
        return BillingSeedData.Invoices.OrderByDescending(x => x.CreatedAt).ToList();
    }

    public IReadOnlyList<BillingInvoiceRecordViewModel> GetForPatient(string patientCode)
    {
        if (string.IsNullOrWhiteSpace(patientCode))
        {
            return Array.Empty<BillingInvoiceRecordViewModel>();
        }

        var apiInvoices = TryGetFromApi();
        if (apiInvoices != null)
        {
            return apiInvoices
                .Where(invoice => string.Equals(invoice.PatientCode, patientCode, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(invoice => invoice.CreatedAt)
                .ToList();
        }

        return BillingSeedData.Invoices
            .Where(invoice => string.Equals(invoice.PatientCode, patientCode, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(invoice => invoice.CreatedAt)
            .ToList();
    }

    public IReadOnlyList<BillingInvoiceRecordViewModel> GetPendingForReception()
    {
        var apiInvoices = TryGetFromApi();
        if (apiInvoices != null)
        {
            return apiInvoices
                .Where(invoice => invoice.PaymentStatus == "Pending" || invoice.AppointmentStatus == "Completed")
                .OrderByDescending(invoice => invoice.CreatedAt)
                .ToList();
        }

        return BillingSeedData.Invoices
            .Where(invoice => invoice.PaymentStatus == "Pending" || invoice.AppointmentStatus == "Completed")
            .OrderByDescending(invoice => invoice.CreatedAt)
            .ToList();
    }

    public BillingInvoiceRecordViewModel? GetById(int id)
    {
        var apiInvoice = TryGetByIdFromApi(id);
        if (apiInvoice != null) return apiInvoice;
        return BillingSeedData.GetById(id);
    }

    public IReadOnlyList<PaymentHistoryItemViewModel> GetPaymentHistory(int invoiceId)
    {
        var invoice = GetById(invoiceId);
        if (invoice == null)
        {
            return Array.Empty<PaymentHistoryItemViewModel>();
        }

        return (invoice.PaymentHistory ?? new List<PaymentHistoryItemViewModel>())
            .OrderByDescending(x => x.At)
            .ToList();
    }

    public bool TryConfirmPayment(int id, string paymentMethod, string? transactionReference, out BillingInvoiceRecordViewModel? invoice)
    {
        invoice = GetById(id);
        if (invoice == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            return false;
        }

        if (_apiClient != null)
        {
            var apiResult = _apiClient.ConfirmPayment(id, paymentMethod.Trim(), transactionReference).GetAwaiter().GetResult();
            if (apiResult.IsSuccess && apiResult.Data != null)
            {
                invoice = apiResult.Data;
                return true;
            }
        }

        BillingSeedData.MarkAsPaid(id, paymentMethod.Trim(), transactionReference);
        invoice = BillingSeedData.GetById(id);
        return invoice != null;
    }

    private List<BillingInvoiceRecordViewModel>? TryGetFromApi()
    {
        if (_apiClient == null) return null;
        var result = _apiClient.GetAllInvoices().GetAwaiter().GetResult();
        return result.IsSuccess ? result.Data?.ToList() : null;
    }

    private BillingInvoiceRecordViewModel? TryGetByIdFromApi(int id)
    {
        if (_apiClient == null) return null;
        var result = _apiClient.GetInvoiceById(id).GetAwaiter().GetResult();
        return result.IsSuccess ? result.Data : null;
    }
}
