using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.Billing;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Invoices;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Services;

public class InvoiceServiceTests
{
    [Fact]
    public void TryConfirmPayment_WhenApiFails_DoesNotMarkSeedInvoicePaid()
    {
        var seedInvoice = BillingSeedData.GetById(1)!;
        var originalStatus = seedInvoice.PaymentStatus;
        var apiClient = new Mock<IInvoiceApiClient>();
        apiClient
            .Setup(client => client.ConfirmPayment(1, "Cash", null))
            .ReturnsAsync(ApiResult<BillingInvoiceRecordViewModel>.Failure("API unavailable", statusCode: 503));

        var service = new InvoiceService(apiClient.Object);

        var confirmed = service.TryConfirmPayment(1, "Cash", null, out var invoice);

        Assert.False(confirmed);
        Assert.Null(invoice);
        Assert.Equal(originalStatus, seedInvoice.PaymentStatus);
    }

    [Fact]
    public void TryConfirmPayment_WithoutApiClient_FailsInsteadOfSimulatingPayment()
    {
        var seedInvoice = BillingSeedData.GetById(1)!;
        var originalStatus = seedInvoice.PaymentStatus;
        var service = new InvoiceService();

        var confirmed = service.TryConfirmPayment(1, "Cash", null, out var invoice);

        Assert.False(confirmed);
        Assert.Null(invoice);
        Assert.Equal(originalStatus, seedInvoice.PaymentStatus);
    }
}
