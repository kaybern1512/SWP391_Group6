using DentalClinic.Web.ViewModels.Invoices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DentalClinic.Web.Services;

public class ReceiptPdfService : IReceiptPdfService
{
    static ReceiptPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateReceipt(BillingInvoiceRecordViewModel invoice)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(28);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                var receiptDate = invoice.PaidAt ?? DateTime.Now;

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text("DentalCare Clinic").SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);
                        column.Item().Text("Biên lai thanh toán").FontSize(16).SemiBold();
                        column.Item().Text($"Mã hóa đơn: {invoice.InvoiceCode}");
                    });

                    row.ConstantItem(150).AlignRight().Text(receiptDate.ToString("dd/MM/yyyy HH:mm"))
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken2);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.2f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Dịch vụ").SemiBold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(6).AlignCenter().Text("SL").SemiBold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(6).AlignRight().Text("Đơn giá").SemiBold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(6).AlignRight().Text("Thành tiền").SemiBold();
                        });

                        foreach (var item in invoice.Items)
                        {
                            table.Cell().Padding(6).Text(item.Description);
                            table.Cell().Padding(6).AlignCenter().Text(item.Quantity.ToString());
                            table.Cell().Padding(6).AlignRight().Text($"{item.UnitPrice:N0} đ");
                            table.Cell().Padding(6).AlignRight().Text($"{item.Amount:N0} đ");
                        }
                    });

                    column.Item().AlignRight().Column(meta =>
                    {
                        meta.Item().Text($"Tổng tiền: {invoice.TotalAmount:N0} đ").SemiBold().FontSize(14).FontColor(Colors.Green.Darken2);
                        meta.Item().Text($"Phương thức: {invoice.PaymentMethod ?? "Chưa xác định"}");
                        meta.Item().Text($"Mã giao dịch: {invoice.PaymentReference ?? "-"}");
                    });

                    column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(info =>
                    {
                        info.Item().Text("Thông tin bệnh nhân");
                        info.Item().Text($"Tên: {invoice.PatientName}");
                        info.Item().Text($"Mã BN: {invoice.PatientCode}");
                        info.Item().Text($"Ngày khám: {invoice.AppointmentDate:dd/MM/yyyy}");
                    });
                });

                page.Footer().AlignRight().Text("Cảm ơn quý khách đã tin tưởng DentalCare Clinic").FontSize(9).FontColor(Colors.Grey.Darken2);
            });
        }).GeneratePdf();
    }
}
