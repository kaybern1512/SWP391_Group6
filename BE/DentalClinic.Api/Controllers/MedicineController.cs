using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Medicines.DTOs;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/medicines")]
public class MedicineController : ControllerBase
{
    private readonly IMedicineService _medicineService;

    public MedicineController(IMedicineService medicineService)
    {
        _medicineService = medicineService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpGet]
    [Authorize(Roles = "SystemAdministrator,Dentist")]
    [ProducesResponseType(typeof(ApiResponse<List<MedicineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMedicines([FromQuery] MedicineSearchFilter filter)
    {
        var list = await _medicineService.GetMedicinesAsync(filter);
        return Ok(ApiResponse<List<MedicineDto>>.Ok(list));
    }

    [HttpGet("inventory-summary")]
    [Authorize(Roles = "SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<MedicineInventorySummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventorySummary()
    {
        var summary = await _medicineService.GetInventorySummaryAsync();
        return Ok(ApiResponse<MedicineInventorySummaryDto>.Ok(summary));
    }

    [HttpGet("available-for-prescription")]
    [Authorize(Roles = "Dentist,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<List<MedicineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableForPrescription([FromQuery] string? search)
    {
        var list = await _medicineService.GetAvailableMedicinesForDentistAsync(search);
        return Ok(ApiResponse<List<MedicineDto>>.Ok(list));
    }

    [HttpGet("transactions")]
    [Authorize(Roles = "SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<List<MedicineStockTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStockTransactions(
        [FromQuery] long? batchId, [FromQuery] long? medicineId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var list = await _medicineService.GetStockTransactionsAsync(batchId, medicineId, page, pageSize);
        return Ok(ApiResponse<List<MedicineStockTransactionDto>>.Ok(list));
    }

    [HttpGet("{id:long}")]
    [Authorize(Roles = "SystemAdministrator,Dentist")]
    [ProducesResponseType(typeof(ApiResponse<MedicineDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMedicineById(long id)
    {
        var detail = await _medicineService.GetMedicineByIdAsync(id);
        if (detail == null)
            return NotFound(ApiResponse<object>.Fail($"Không tìm thấy thuốc với ID {id}."));

        return Ok(ApiResponse<MedicineDetailDto>.Ok(detail));
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<MedicineDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMedicine([FromBody] CreateMedicineRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var created = await _medicineService.CreateMedicineAsync(request, userId);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<MedicineDto>.Ok(created, "Tạo danh mục thuốc thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<MedicineDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMedicine(long id, [FromBody] UpdateMedicineRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var updated = await _medicineService.UpdateMedicineAsync(id, request, userId);
            return Ok(ApiResponse<MedicineDto>.Ok(updated, "Cập nhật thuốc thành công."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail($"Không tìm thấy thuốc với ID {id}."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPatch("{id:long}/status")]
    [Authorize(Roles = "SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeMedicineStatus(long id, [FromBody] string status)
    {
        var userId = GetCurrentUserId();
        var success = await _medicineService.ChangeMedicineStatusAsync(id, status, userId);
        if (!success)
            return NotFound(ApiResponse<object>.Fail($"Không tìm thấy thuốc với ID {id}."));

        return Ok(ApiResponse<object>.Ok(null, "Cập nhật trạng thái thuốc thành công."));
    }

    [HttpGet("{id:long}/batches")]
    [Authorize(Roles = "SystemAdministrator,Dentist")]
    [ProducesResponseType(typeof(ApiResponse<List<MedicineBatchDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatches(long id)
    {
        var batches = await _medicineService.GetBatchesByMedicineIdAsync(id);
        return Ok(ApiResponse<List<MedicineBatchDto>>.Ok(batches));
    }

    [HttpPost("{id:long}/batches")]
    [Authorize(Roles = "SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<MedicineBatchDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportBatch(long id, [FromBody] ImportMedicineBatchRequest request)
    {
        try
        {
            request.MedicineId = id;
            var userId = GetCurrentUserId();
            var batch = await _medicineService.ImportBatchAsync(request, userId);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<MedicineBatchDto>.Ok(batch, "Nhập lô thuốc thành công."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail($"Không tìm thấy thuốc với ID {id}."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("batches/{batchId:long}/adjust")]
    [Authorize(Roles = "SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<MedicineStockTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdjustStock(long batchId, [FromBody] StockAdjustmentRequest request)
    {
        try
        {
            request.MedicineBatchId = batchId;
            var userId = GetCurrentUserId();
            var tx = await _medicineService.AdjustStockAsync(request, userId);
            return Ok(ApiResponse<MedicineStockTransactionDto>.Ok(tx, "Điều chỉnh tồn kho thành công và đã ghi nhật ký kiểm toán."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail($"Không tìm thấy lô thuốc với ID {batchId}."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ==========================================
    // DISPENSATION ENDPOINTS (WF-16 & WF-17)
    // ==========================================

    [HttpPost("dispensations")]
    [Authorize(Roles = "Dentist,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<MedicineDispensationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DispenseMedicine([FromBody] CreateMedicineDispensationRequest request)
    {
        try
        {
            var dentistUserId = GetCurrentUserId();
            var result = await _medicineService.DispenseMedicineAsync(request, dentistUserId);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<MedicineDispensationDto>.Ok(result, "Cấp thuốc từ kho thành công, đã trừ tồn kho và ghi nhật ký kiểm toán."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("dispensations/appointment/{appointmentId:long}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<MedicineDispensationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDispensationsByAppointment(long appointmentId)
    {
        var list = await _medicineService.GetDispensationsByAppointmentIdAsync(appointmentId);
        return Ok(ApiResponse<List<MedicineDispensationDto>>.Ok(list));
    }

    [HttpGet("dispensations/prescription/{prescriptionId:long}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PrescriptionDispensationStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrescriptionDispensationStatus(long prescriptionId)
    {
        var status = await _medicineService.GetPrescriptionDispensationStatusAsync(prescriptionId);
        return Ok(ApiResponse<PrescriptionDispensationStatusDto>.Ok(status));
    }

}
