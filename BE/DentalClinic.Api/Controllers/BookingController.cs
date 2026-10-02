using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Booking.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/booking")]
[AllowAnonymous]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpGet("departments")]
    [ProducesResponseType(typeof(ApiResponse<List<DepartmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartments(CancellationToken ct)
    {
        var result = await _bookingService.GetActiveDepartmentsAsync(ct);
        return Ok(result);
    }

    [HttpGet("departments/{departmentId:long}/services")]
    [ProducesResponseType(typeof(ApiResponse<List<DepartmentServiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServices(long departmentId, CancellationToken ct)
    {
        var result = await _bookingService.GetServicesByDepartmentAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpGet("departments/{departmentId:long}/dentists")]
    [ProducesResponseType(typeof(ApiResponse<List<DentistOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDentists(long departmentId, CancellationToken ct)
    {
        var result = await _bookingService.GetDentistsByDepartmentAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpGet("available-slots")]
    [ProducesResponseType(typeof(ApiResponse<List<AvailableSlotDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<AvailableSlotDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAvailableSlots(
        [FromQuery] long departmentId,
        [FromQuery] long serviceId,
        [FromQuery] DateTime date,
        [FromQuery] long? dentistUserId,
        CancellationToken ct)
    {
        var query = new AvailableSlotsQuery
        {
            DepartmentId = departmentId,
            ServiceId = serviceId,
            Date = date,
            DentistUserId = dentistUserId
        };

        var result = await _bookingService.GetAvailableSlotsAsync(query, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
