using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Invoices.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }

    [HttpGet("api/invoices")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<System.Collections.Generic.List<InvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoices(CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized(ApiResponse.Fail("Bạn cần đăng nhập để xem hóa đơn."));
        }

        var role = GetCurrentUserRole();
        var result = await _invoiceService.GetInvoicesAsync(currentUserId, role, ct);
        return Ok(result);
    }

    [HttpGet("api/invoices/report")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoiceReport(CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized(ApiResponse.Fail("Bạn cần đăng nhập để xem báo cáo hóa đơn."));
        }

        var role = GetCurrentUserRole();
        var result = await _invoiceService.GetInvoiceReportAsync(currentUserId, role, ct);
        return Ok(result);
    }

    [HttpGet("api/invoices/{invoiceId:long}")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> GetInvoiceById(long invoiceId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized(ApiResponse.Fail("Bạn cần đăng nhập để xem hóa đơn."));
        }

        var role = GetCurrentUserRole();
        var result = await _invoiceService.GetInvoiceByIdAsync(invoiceId, currentUserId, role, ct);

        if (!result.Success)
        {
            return result.Message?.Contains("quyền") == true ? Forbid() : NotFound(result);
        }

        return Ok(result);
    }

    [HttpPost("api/invoices/{invoiceId:long}/confirm-payment")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmPayment(long invoiceId, [FromBody] ConfirmPaymentRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized(ApiResponse.Fail("Bạn cần đăng nhập để xác nhận thanh toán."));
        }

        var role = GetCurrentUserRole();
        var result = await _invoiceService.ConfirmPaymentAsync(invoiceId, currentUserId, role, request, ct);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("api/patient/invoices")]
    [Authorize(Roles = UserRole.Patient)]
    public async Task<IActionResult> GetPatientInvoices(CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized(ApiResponse.Fail("Bạn cần đăng nhập để xem hóa đơn."));
        }

        var result = await _invoiceService.GetPatientInvoicesAsync(currentUserId, ct);
        return Ok(result);
    }

    [HttpGet("api/patient/invoices/{invoiceId:long}")]
    [Authorize(Roles = UserRole.Patient)]
    public async Task<IActionResult> GetPatientInvoiceById(long invoiceId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized(ApiResponse.Fail("Bạn cần đăng nhập để xem hóa đơn."));
        }

        var role = GetCurrentUserRole();
        var result = await _invoiceService.GetInvoiceByIdAsync(invoiceId, currentUserId, role, ct);
        if (!result.Success)
        {
            return result.Message?.Contains("quyền") == true ? Forbid() : NotFound(result);
        }

        return Ok(result);
    }

    [HttpGet("api/receptionist/invoices")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> GetReceptionistInvoices(CancellationToken ct)
    {
        var result = await _invoiceService.GetReceptionistInvoicesAsync(ct);
        return Ok(result);
    }

    [HttpGet("api/receptionist/invoices/{invoiceId:long}")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    public async Task<IActionResult> GetReceptionistInvoiceById(long invoiceId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var role = GetCurrentUserRole();
        var result = await _invoiceService.GetInvoiceByIdAsync(invoiceId, currentUserId, role, ct);
        if (!result.Success)
        {
            return result.Message?.Contains("quyền") == true ? Forbid() : NotFound(result);
        }

        return Ok(result);
    }
}
