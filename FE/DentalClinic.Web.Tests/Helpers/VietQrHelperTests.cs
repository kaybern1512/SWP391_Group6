using System;
using DentalClinic.Web.Helpers;
using FluentAssertions;
using Xunit;

namespace DentalClinic.Web.Tests.Helpers;

public class VietQrHelperTests
{
    [Theory]
    [InlineData("INV-001", 189000, 189000)]
    [InlineData("INV-002", 189500, 189500)]
    [InlineData("INV-003", 200500, 200500)]
    [InlineData("INV-004", 99999, 99999)]
    public void GenerateQrUrl_ExactVndAmounts_PreservesExactValueWithoutRoundingToThousand(
        string invoiceCode, decimal amount, long expectedAmount)
    {
        // Act
        var url = VietQrHelper.GenerateQrUrl(invoiceCode, amount);

        // Assert
        url.Should().Contain($"amount={expectedAmount}");
        url.Should().Contain($"addInfo=THANH%20TOAN%20{invoiceCode}");
        url.Should().Contain("MB-039123456789-compact2.png");
        url.Should().Contain("accountName=NHA%20KHOA%20DENTALCARE");

        // Explicitly assert it does NOT round to thousand dong
        if (amount == 189500)
        {
            url.Should().NotContain("amount=190000");
        }
        if (amount == 99999)
        {
            url.Should().NotContain("amount=100000");
        }
        if (amount == 200500)
        {
            url.Should().NotContain("amount=201000");
        }
    }

    [Fact]
    public void GenerateQrUrl_ZeroAmount_GeneratesValidUrlWithZeroAmount()
    {
        var url = VietQrHelper.GenerateQrUrl("INV-ZERO", 0);

        url.Should().Contain("amount=0");
        url.Should().Contain("addInfo=THANH%20TOAN%20INV-ZERO");
    }

    [Fact]
    public void GenerateQrUrl_CustomTemplate_GeneratesExpectedTemplate()
    {
        var url = VietQrHelper.GenerateQrUrl("INV-CUSTOM", 189500, template: "compact");

        url.Should().Contain("MB-039123456789-compact.png");
        url.Should().Contain("amount=189500");
    }
}
