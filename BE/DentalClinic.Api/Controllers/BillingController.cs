using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;

    public BillingController(IBillingService billingService)
    {
        _billingService = billingService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }

    // ==========================================
    // RECEPTIONIST & ADMIN ENDPOINTS
    // ==========================================

    [HttpPost("invoices")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        var staffUserId = GetCurrentUserId();
        var result = await _billingService.CreateInvoiceAsync(staffUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("payments")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessPayment([FromBody] ProcessPaymentRequest request, CancellationToken ct)
    {
        var staffUserId = GetCurrentUserId();
        var result = await _billingService.ProcessPaymentAsync(staffUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("invoices")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<InvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] string? status = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var result = await _billingService.GetInvoicesAsync(status, searchTerm, fromDate, toDate, ct);
        return Ok(result);
    }

    [HttpGet("appointments/billable")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<CompletedAppointmentForBillingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBillableAppointments(CancellationToken ct)
    {
        var result = await _billingService.GetCompletedAppointmentsForBillingAsync(ct);
        return Ok(result);
    }

    [HttpGet("appointments/{appointmentId:long}/preparation")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentBillingPreparationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentBillingPreparationDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPreparation(long appointmentId, CancellationToken ct)
    {
        var result = await _billingService.GetAppointmentBillingPreparationAsync(appointmentId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    // ==========================================
    // INVOICE DETAILS & APPOINTMENT LOOKUP
    // ==========================================

    [HttpGet("invoices/{invoiceId:long}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoiceById(long invoiceId, CancellationToken ct)
    {
        var result = await _billingService.GetInvoiceByIdAsync(invoiceId, ct);
        if (!result.Success || result.Data == null)
        {
            return NotFound(result);
        }

        // If patient, ensure this invoice belongs to them
        var role = GetCurrentUserRole();
        var userId = GetCurrentUserId();
        if (role == UserRole.Patient && result.Data.PatientUserId != userId)
        {
            return Forbid();
        }

        return Ok(result);
    }

    [HttpGet("appointments/{appointmentId:long}/invoice")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoiceByAppointmentId(long appointmentId, CancellationToken ct)
    {
        var result = await _billingService.GetInvoiceByAppointmentIdAsync(appointmentId, ct);
        if (!result.Success || result.Data == null)
        {
            return NotFound(result);
        }

        var role = GetCurrentUserRole();
        var userId = GetCurrentUserId();
        if (role == UserRole.Patient && result.Data.PatientUserId != userId)
        {
            return Forbid();
        }

        return Ok(result);
    }

    // ==========================================
    // PATIENT ENDPOINTS
    // ==========================================

    [HttpGet("patient/invoices")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<List<InvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientInvoices(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính bệnh nhân."));

        var result = await _billingService.GetPatientInvoicesAsync(userId, ct);
        return Ok(result);
    }
}
