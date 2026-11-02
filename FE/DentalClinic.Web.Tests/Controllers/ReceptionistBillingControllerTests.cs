using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Receptionist;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class ReceptionistBillingControllerTests
{
    private readonly Mock<IBillingApiService> _billingApiMock;

    public ReceptionistBillingControllerTests()
    {
        _billingApiMock = new Mock<IBillingApiService>();
    }

    private static ControllerContext CreateContext(long userId, string role, string name = "Receptionist")
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "CookieAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        return new ControllerContext { HttpContext = httpContext };
    }

    private static TempDataDictionary CreateTempData()
    {
        return new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public void Controller_MustHaveAuthorizeAttribute_RestrictingToReceptionistAndAdmin()
    {
        var authAttr = typeof(ReceptionistBillingController).GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Roles.Should().Contain("Receptionist");
        authAttr.Roles.Should().Contain("SystemAdministrator");
    }

    [Fact]
    public void CreateAction_MustHaveValidateAntiForgeryTokenAttribute_AndMustNotHaveIgnoreAntiforgery()
    {
        var method = typeof(ReceptionistBillingController).GetMethod(
            nameof(ReceptionistBillingController.Create),
            new[] { typeof(CreateInvoiceRequest) });
        method.Should().NotBeNull();

        var validateAttr = method!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>();
        var ignoreAttr = method.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>();

        validateAttr.Should().NotBeNull("Create POST action must enforce CSRF protection via [ValidateAntiForgeryToken]");
        ignoreAttr.Should().BeNull("[IgnoreAntiforgeryToken] must never be applied to Create POST action");
    }

    [Fact]
    public void PayAction_MustHaveValidateAntiForgeryTokenAttribute()
    {
        var method = typeof(ReceptionistBillingController).GetMethod(
            nameof(ReceptionistBillingController.Pay),
            new[] { typeof(ProcessPaymentRequest) });
        method.Should().NotBeNull();

        var validateAttr = method!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>();
        validateAttr.Should().NotBeNull("Pay POST action must enforce CSRF protection via [ValidateAntiForgeryToken]");
    }

    [Fact]
    public async Task Create_ValidRequest_CallsService_AndReturnsSuccessJson()
    {
        var request = new CreateInvoiceRequest
        {
            AppointmentId = 116,
            DiscountAmount = 10000,
            TaxPercent = 8,
            Note = "Test invoice",
            Items = new List<CreateInvoiceItemRequest>
            {
                new()
                {
                    Description = "Khám tổng quát & Tư vấn",
                    Quantity = 1,
                    UnitPrice = 100000.50m
                }
            }
        };

        var expectedDto = new InvoiceDto
        {
            InvoiceId = 55,
            InvoiceCode = "INV-20261102-001",
            AppointmentId = 116,
            TotalAmount = 98000.54m,
            Status = "Unpaid"
        };

        _billingApiMock.Setup(x => x.CreateInvoiceAsync(It.IsAny<CreateInvoiceRequest>()))
            .ReturnsAsync(ApiResult<InvoiceDto>.Success(expectedDto));

        var mockUrl = new Mock<IUrlHelper>();
        mockUrl.Setup(x => x.Action(It.IsAny<UrlActionContext>()))
            .Returns("/Receptionist/Billing/Detail/55");

        var controller = new ReceptionistBillingController(_billingApiMock.Object)
        {
            ControllerContext = CreateContext(2, "Receptionist"),
            TempData = CreateTempData(),
            Url = mockUrl.Object
        };

        var result = await controller.Create(request);

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.Value.Should().NotBeNull();

        var successVal = jsonResult.Value!.GetType().GetProperty("success")?.GetValue(jsonResult.Value) as bool?;
        var invoiceIdVal = jsonResult.Value!.GetType().GetProperty("invoiceId")?.GetValue(jsonResult.Value) as long?;

        successVal.Should().BeTrue();
        invoiceIdVal.Should().Be(55);
    }

    [Fact]
    public async Task Create_InvalidRequest_NullOrEmptyItems_ReturnsErrorJson()
    {
        var controller = new ReceptionistBillingController(_billingApiMock.Object)
        {
            ControllerContext = CreateContext(2, "Receptionist"),
            TempData = CreateTempData()
        };

        var emptyItemsRequest = new CreateInvoiceRequest
        {
            AppointmentId = 116,
            Items = new List<CreateInvoiceItemRequest>()
        };

        var result = await controller.Create(emptyItemsRequest);

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.Value.Should().NotBeNull();

        var successVal = jsonResult.Value!.GetType().GetProperty("success")?.GetValue(jsonResult.Value) as bool?;
        var messageVal = jsonResult.Value!.GetType().GetProperty("message")?.GetValue(jsonResult.Value) as string;

        successVal.Should().BeFalse();
        messageVal.Should().Contain("ít nhất một khoản mục");

        _billingApiMock.Verify(x => x.CreateInvoiceAsync(It.IsAny<CreateInvoiceRequest>()), Times.Never);
    }

    [Fact]
    public async Task Antiforgery_HeaderValidation_EnforcesValidTokens_RejectsMissingOrInvalidTokens()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "RequestVerificationToken";
        });

        var provider = services.BuildServiceProvider();
        var antiforgery = provider.GetRequiredService<IAntiforgery>();
        var antiforgeryOptions = provider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        // 1. Missing Token / Header
        var contextMissing = new DefaultHttpContext();
        Func<Task> actMissing = async () => await antiforgery.ValidateRequestAsync(contextMissing);
        await actMissing.Should().ThrowAsync<AntiforgeryValidationException>(
            "Requests lacking antiforgery cookie and header must be rejected");

        // 2. Invalid Token Header
        var contextInvalid = new DefaultHttpContext();
        contextInvalid.Request.Headers["RequestVerificationToken"] = "malicious_spoofed_csrf_token_xyz";
        Func<Task> actInvalid = async () => await antiforgery.ValidateRequestAsync(contextInvalid);
        await actInvalid.Should().ThrowAsync<AntiforgeryValidationException>(
            "Requests with an invalid or mismatched token must be rejected");

        // 3. Valid Token Generation and Header Verification
        var contextGen = new DefaultHttpContext();
        var tokenSet = antiforgery.GetAndStoreTokens(contextGen);
        tokenSet.RequestToken.Should().NotBeNullOrEmpty();
        tokenSet.CookieToken.Should().NotBeNullOrEmpty();

        var cookieName = antiforgeryOptions.Cookie.Name ?? ".AspNetCore.Antiforgery";

        var contextVerification = new DefaultHttpContext();
        contextVerification.Request.Headers["Cookie"] = $"{cookieName}={tokenSet.CookieToken}";
        contextVerification.Request.Headers["RequestVerificationToken"] = tokenSet.RequestToken;

        var isRequestValid = await antiforgery.IsRequestValidAsync(contextVerification);
        isRequestValid.Should().BeTrue("A request with matching cookie and RequestVerificationToken header must succeed");
    }
}
