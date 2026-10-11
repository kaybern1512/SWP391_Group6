using DentalClinic.Web.ViewModels.Invoices;

namespace DentalClinic.Web.Services;

public interface IReceiptPdfService
{
    byte[] GenerateReceipt(BillingInvoiceRecordViewModel invoice);
}
