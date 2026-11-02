using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Admin;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin/Medicine")]
public class AdminMedicineController : Controller
{
    private readonly IMedicineApiService _medicineApiService;

    public AdminMedicineController(IMedicineApiService medicineApiService)
    {
        _medicineApiService = medicineApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index([FromQuery] MedicineSearchFilter filter)
    {
        filter ??= new MedicineSearchFilter();

        var summaryRes = await _medicineApiService.GetInventorySummaryAsync();
        var medicinesRes = await _medicineApiService.GetMedicinesAsync(filter);

        var vm = new AdminMedicineIndexViewModel
        {
            Summary = summaryRes.IsSuccess && summaryRes.Data != null ? summaryRes.Data : new MedicineInventorySummaryDto(),
            Medicines = medicinesRes.IsSuccess && medicinesRes.Data != null ? medicinesRes.Data : new(),
            Filter = filter
        };

        return View("~/Views/Admin/Medicine/Index.cshtml", vm);
    }

    [HttpGet("Detail/{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var res = await _medicineApiService.GetMedicineByIdAsync(id);
        if (!res.IsSuccess || res.Data == null)
        {
            TempData["ErrorMessage"] = res.Message ?? $"Không tìm thấy thuốc với ID {id}.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new AdminMedicineDetailViewModel
        {
            Medicine = res.Data,
            NewBatch = new ImportMedicineBatchRequest
            {
                MedicineId = id,
                ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12))
            },
            Adjustment = new StockAdjustmentRequest()
        };

        return View("~/Views/Admin/Medicine/Detail.cshtml", vm);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateMedicineRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MedicineCode) || string.IsNullOrWhiteSpace(request.MedicineName))
        {
            TempData["ErrorMessage"] = "Mã thuốc và tên thuốc là bắt buộc.";
            return RedirectToAction(nameof(Index));
        }

        var res = await _medicineApiService.CreateMedicineAsync(request);
        if (res.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Tạo mới danh mục thuốc '{request.MedicineName}' thành công.";
            return RedirectToAction(nameof(Detail), new { id = res.Data!.MedicineId });
        }

        TempData["ErrorMessage"] = res.Message ?? "Không thể tạo thuốc mới.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Edit/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, UpdateMedicineRequest request)
    {
        var res = await _medicineApiService.UpdateMedicineAsync(id, request);
        if (res.IsSuccess)
        {
            TempData["SuccessMessage"] = "Cập nhật thông tin thuốc thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = res.Message ?? "Không thể cập nhật thông tin thuốc.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("ToggleStatus/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(long id, string status)
    {
        var res = await _medicineApiService.ChangeMedicineStatusAsync(id, status);
        if (res.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái thuốc thành '{status}'.";
        }
        else
        {
            TempData["ErrorMessage"] = res.Message ?? "Không thể thay đổi trạng thái thuốc.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("ImportBatch/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportBatch(long id, ImportMedicineBatchRequest request)
    {
        request.MedicineId = id;
        var res = await _medicineApiService.ImportBatchAsync(id, request);
        if (res.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Nhập kho lô thuốc '{request.BatchNumber}' ({request.Quantity} đơn vị) thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = res.Message ?? "Không thể nhập lô thuốc.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("AdjustStock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustStock(long batchId, long medicineId, StockAdjustmentRequest request)
    {
        request.MedicineBatchId = batchId;
        var res = await _medicineApiService.AdjustStockAsync(batchId, request);
        if (res.IsSuccess)
        {
            TempData["SuccessMessage"] = "Điều chỉnh tồn kho kiểm kê thành công và đã ghi nhận nhật ký kiểm toán.";
        }
        else
        {
            TempData["ErrorMessage"] = res.Message ?? "Không thể điều chỉnh tồn kho.";
        }

        return RedirectToAction(nameof(Detail), new { id = medicineId });
    }

    [HttpGet("Transactions")]
    public async Task<IActionResult> Transactions([FromQuery] long? batchId, [FromQuery] long? medicineId, [FromQuery] int page = 1)
    {
        var res = await _medicineApiService.GetStockTransactionsAsync(batchId, medicineId, page, 50);

        var vm = new AdminMedicineTransactionsViewModel
        {
            Transactions = res.IsSuccess && res.Data != null ? res.Data : new(),
            BatchId = batchId,
            MedicineId = medicineId,
            Page = page,
            PageSize = 50
        };

        return View("~/Views/Admin/Medicine/Transactions.cshtml", vm);
    }
}
