using DentalClinic.Web.ViewModels.Invoices;

namespace DentalClinic.Web.Models.Billing;

public static class BillingSeedData
{
    public static List<BillingInvoiceRecordViewModel> Invoices { get; } = new()
    {
        CreateInvoice(
            1,
            "INV-2026-1001",
            101,
            "PT-1001",
            "Nguyễn Văn A",
            DateTime.Today.AddDays(-1),
            "Pending",
            new[]
            {
                new BillingInvoiceItemViewModel { Description = "Khám tổng quát", Quantity = 1, UnitPrice = 250000m },
                new BillingInvoiceItemViewModel { Description = "Chữa răng sữa", Quantity = 1, UnitPrice = 600000m },
                new BillingInvoiceItemViewModel { Description = "Thuốc kháng sinh", Quantity = 2, UnitPrice = 120000m }
            },
            new[]
            {
                new PaymentHistoryItemViewModel { Event = "Hóa đơn được tạo", Method = "System", Reference = "AUTOGEN-INV-1001", At = DateTime.Today.AddDays(-1).AddHours(10), Amount = 0m }
            }),
        CreateInvoice(
            2,
            "INV-2026-1002",
            102,
            "PT-1001",
            "Nguyễn Văn A",
            DateTime.Today.AddDays(-5),
            "Paid",
            new[]
            {
                new BillingInvoiceItemViewModel { Description = "Tẩy trắng răng", Quantity = 1, UnitPrice = 750000m },
                new BillingInvoiceItemViewModel { Description = "Bảo quản chất liệu", Quantity = 1, UnitPrice = 180000m }
            },
            new[]
            {
                new PaymentHistoryItemViewModel { Event = "Hóa đơn được tạo", Method = "System", Reference = "AUTOGEN-INV-1002", At = DateTime.Today.AddDays(-5).AddHours(13), Amount = 0m },
                new PaymentHistoryItemViewModel { Event = "Đã thanh toán", Method = "Tiền mặt", Reference = "CASH-INV-1002", At = DateTime.Today.AddDays(-5).AddHours(14), Amount = 930000m }
            },
            "Tiền mặt",
            "CASH-INV-1002",
            DateTime.Today.AddDays(-5).AddHours(14)),
        CreateInvoice(
            3,
            "INV-2026-1003",
            103,
            "PT-1002",
            "Trần Thị B",
            DateTime.Today.AddDays(-2),
            "Pending",
            new[]
            {
                new BillingInvoiceItemViewModel { Description = "Khám định kỳ", Quantity = 1, UnitPrice = 220000m },
                new BillingInvoiceItemViewModel { Description = "X-quang răng", Quantity = 1, UnitPrice = 430000m },
                new BillingInvoiceItemViewModel { Description = "Thuốc giảm đau", Quantity = 1, UnitPrice = 95000m }
            },
            new[]
            {
                new PaymentHistoryItemViewModel { Event = "Hóa đơn được tạo", Method = "System", Reference = "AUTOGEN-INV-1003", At = DateTime.Today.AddDays(-2).AddHours(9), Amount = 0m }
            })
    };

    private static BillingInvoiceRecordViewModel CreateInvoice(
        int invoiceId,
        string invoiceCode,
        int appointmentId,
        string patientCode,
        string patientName,
        DateTime appointmentDate,
        string paymentStatus,
        IEnumerable<BillingInvoiceItemViewModel> items,
        IEnumerable<PaymentHistoryItemViewModel> history,
        string? paymentMethod = null,
        string? paymentReference = null,
        DateTime? paidAt = null)
    {
        var record = new BillingInvoiceRecordViewModel
        {
            InvoiceId = invoiceId,
            InvoiceCode = invoiceCode,
            AppointmentId = appointmentId,
            PatientCode = patientCode,
            PatientName = patientName,
            AppointmentDate = appointmentDate,
            AppointmentStatus = "Completed",
            PaymentStatus = paymentStatus,
            PaymentMethod = paymentMethod,
            PaymentReference = paymentReference,
            PaidAt = paidAt,
            CreatedAt = appointmentDate.AddHours(9),
            Items = items.ToList(),
            PaymentHistory = history.ToList()
        };

        if (record.PaymentStatus == "Paid" && record.PaidAt.HasValue && record.PaymentMethod != null)
        {
            record.PaymentHistory.Add(new PaymentHistoryItemViewModel
            {
                Event = "Xác nhận thanh toán",
                Method = record.PaymentMethod,
                Reference = record.PaymentReference ?? record.PaymentMethod,
                At = record.PaidAt.Value,
                Amount = record.TotalAmount
            });
        }

        return record;
    }

    public static BillingInvoiceRecordViewModel? GetById(int id)
        => Invoices.FirstOrDefault(invoice => invoice.InvoiceId == id);

    public static void MarkAsPaid(int id, string paymentMethod, string? transactionReference)
    {
        var invoice = GetById(id);
        if (invoice is null)
        {
            return;
        }

        invoice.PaymentStatus = "Paid";
        invoice.PaymentMethod = paymentMethod;
        invoice.PaymentReference = string.IsNullOrWhiteSpace(transactionReference)
            ? $"{paymentMethod}-INV-{invoice.InvoiceId}"
            : transactionReference.Trim();
        invoice.PaidAt = DateTime.Now;
        invoice.AppointmentStatus = "Completed";

        invoice.PaymentHistory ??= new List<PaymentHistoryItemViewModel>();
        invoice.PaymentHistory.Add(new PaymentHistoryItemViewModel
        {
            Event = "Xác nhận thanh toán",
            Method = paymentMethod,
            Reference = invoice.PaymentReference,
            At = invoice.PaidAt.Value,
            Amount = invoice.TotalAmount
        });
    }
}
