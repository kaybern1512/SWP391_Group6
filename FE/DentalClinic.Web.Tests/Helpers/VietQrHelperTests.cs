using System;
using DentalClinic.Web.Helpers;
using FluentAssertions;
using Xunit;

namespace DentalClinic.Web.Tests.Helpers;

public class VietQrHelperTests
{
    [Fact]
    public void GenerateQrUrl_WithOddAmount_189500_GeneratesCorrectAmountWithoutRoundingToThousand()
    {
        // Arrange
        var invoiceCode = "INV-20261109-001";
        decimal amount = 189500.00m;

        // Act
        var url = VietQrHelper.GenerateQrUrl(invoiceCode, amount);

        // Assert
        url.Should().Contain("amount=189500");
        url.Should().NotContain("amount=189000");
        url.Should().NotContain("amount=190000");
        url.Should().Contain("THANH%20TOAN%20INV-20261109-001");
        url.Should().Contain("MB-039123456789-compact2.png");
    }

    [Fact]
    public void GenerateQrUrl_WithOddAmount_200500_PreservesExactDongAmount()
    {
        // Arrange
        var invoiceCode = "INV-20261109-002";
        decimal amount = 200500m;

        // Act
        var url = VietQrHelper.GenerateQrUrl(invoiceCode, amount);

        // Assert
        url.Should().Contain("amount=200500");
    }

    [Fact]
    public void GenerateQrUrl_WithArbitraryOddAmount_99999_PreservesExactValue()
    {
        // Arrange
        var invoiceCode = "INV-20261109-003";
        decimal amount = 99999m;

        // Act
        var url = VietQrHelper.GenerateQrUrl(invoiceCode, amount);

        // Assert
        url.Should().Contain("amount=99999");
    }

    [Fact]
    public void GenerateQrUrl_WithZeroOrNegativeAmount_ClampsToZero()
    {
        // Arrange
        var invoiceCode = "INV-20261109-004";
        decimal negativeAmount = -50000m;

        // Act
        var url = VietQrHelper.GenerateQrUrl(invoiceCode, negativeAmount);

        // Assert
        url.Should().Contain("amount=0");
    }

    [Fact]
    public void GenerateQrUrl_TrimsInvoiceCodeAndEscapesAccountName()
    {
        // Arrange
        var invoiceCode = "  INV-CODE-WITH-SPACES  ";
        decimal amount = 500000m;

        // Act
        var url = VietQrHelper.GenerateQrUrl(invoiceCode, amount, accountName: "NHA KHOA DENTALCARE & SPA");

        // Assert
        url.Should().Contain("addInfo=THANH%20TOAN%20INV-CODE-WITH-SPACES");
        url.Should().Contain("accountName=NHA%20KHOA%20DENTALCARE%20%26%20SPA");
    }
}
