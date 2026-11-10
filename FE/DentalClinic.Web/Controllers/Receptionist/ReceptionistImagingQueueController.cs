using System;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist,SystemAdministrator")]
[Route("Receptionist/ImagingQueue")]
public class ReceptionistImagingQueueController : Controller
{
    private readonly IImagingApiService _imagingApiService;

    public ReceptionistImagingQueueController(IImagingApiService imagingApiService)
    {
        _imagingApiService = imagingApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index([FromQuery] long? roomId = null)
    {
        var equipmentsRes = await _imagingApiService.GetAllEquipmentsAsync();
        var equipments = equipmentsRes.Data ?? new();

        var targetRoomId = roomId ?? equipments.FirstOrDefault()?.RoomId ?? 1;

        var queueRes = await _imagingApiService.GetQueueStatusAsync(targetRoomId);
        var queue = queueRes.Data ?? new ImagingQueueSummaryDto
        {
            RoomId = targetRoomId,
            RoomCode = "XRAY-01",
            RoomName = "Phòng Chụp X-quang 01",
            MaxWaitingPatients = 3,
            TargetMaxWaitMinutes = 20,
            EscalationWaitMinutes = 30
        };

        var configsRes = await _imagingApiService.GetAllConfigsAsync();

        ViewBag.Equipments = equipments;
        ViewBag.Configs = configsRes.Data ?? new();
        ViewBag.SelectedRoomId = targetRoomId;

        return View("~/Views/Receptionist/ImagingQueue/Index.cshtml", queue);
    }

    [HttpPost("CheckIn/{sessionId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(long sessionId)
    {
        var result = await _imagingApiService.CheckInSessionAsync(sessionId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã tiếp nhận bệnh nhân vào hàng đợi (Số thứ tự: {result.Data?.QueueNumber}). Thời gian chờ ước tính: {result.Data?.EstimatedWaitMinutes} phút.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể check-in vào hàng đợi.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Start/{sessionId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(long sessionId)
    {
        var result = await _imagingApiService.StartSessionAsync(sessionId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã bắt đầu ca chụp X-quang.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể bắt đầu ca chụp.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Acquire/{sessionId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Acquire(long sessionId)
    {
        var result = await _imagingApiService.AcquireImageAsync(sessionId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã ghi nhận chụp phim thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể ghi nhận hình ảnh.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(UploadDiagnosticResultRequest request)
    {
        var result = await _imagingApiService.UploadResultAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Tải lên kết quả hình ảnh X-quang thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tải lên kết quả.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Complete/{sessionId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(long sessionId)
    {
        var result = await _imagingApiService.CompleteSessionAsync(sessionId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Ca chụp đã hoàn tất và sẵn sàng cho đánh giá lâm sàng & xuất hóa đơn.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hoàn tất ca chụp.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Cancel/{sessionId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long sessionId, [FromForm] string reason)
    {
        var result = await _imagingApiService.CancelSessionAsync(sessionId, string.IsNullOrWhiteSpace(reason) ? "Hủy bởi Lễ tân" : reason);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã hủy ca chụp thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hủy ca chụp.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Book")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookImagingSessionRequest request)
    {
        var result = await _imagingApiService.BookSessionAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đặt lịch chụp thành công cho chỉ định #{request.DiagnosticOrderId}.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể đặt lịch phiên chụp.";
        }
        return RedirectToAction(nameof(Index));
    }
}
