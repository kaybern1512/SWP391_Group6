using DentalClinic.Web.ViewModels.Invoices;

namespace DentalClinic.Web.Services;

public interface IInvoiceService
{
    IReadOnlyList<BillingInvoiceRecordViewModel> GetAll();
    IReadOnlyList<BillingInvoiceRecordViewModel> GetForPatient(string patientCode);
    IReadOnlyList<BillingInvoiceRecordViewModel> GetPendingForReception();
    BillingInvoiceRecordViewModel? GetById(int id);
    IReadOnlyList<PaymentHistoryItemViewModel> GetPaymentHistory(int invoiceId);
    bool TryConfirmPayment(int id, string paymentMethod, string? transactionReference, out BillingInvoiceRecordViewModel? invoice);
}
