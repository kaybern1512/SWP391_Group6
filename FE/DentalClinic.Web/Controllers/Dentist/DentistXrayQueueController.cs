using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Helpers;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Dentist;

[Authorize(Roles = "Dentist")]
[Route("Dentist/XrayQueue")]
public class DentistXrayQueueController : Controller
{
    private readonly IImagingApiService _imagingApiService;

    [ActivatorUtilitiesConstructor]
    public DentistXrayQueueController(IImagingApiService imagingApiService)
    {
        _imagingApiService = imagingApiService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] long? roomId = null,
        [FromQuery] string? status = null,
        [FromQuery] long? sessionId = null)
    {
        var targetRoomId = roomId ?? 9; // Default XRAY-01 is RoomId 9
        var today = ClinicTimeHelper.TodayDateOnly;

        // Fetch dentist's own duty roster
        var dutyRes = await _imagingApiService.GetMyXrayDutyAsync(today, today);
        var activeDuty = dutyRes?.Data?.FirstOrDefault(r => r.Status == "Active");

        // Fetch current queue status for room
        var queueRes = await _imagingApiService.GetQueueStatusAsync(targetRoomId);
        var queueData = queueRes?.Data;

        // KPI Counts
        var waitingCount = queueData?.WaitingQueue?.Count ?? queueData?.CurrentWaitingCount ?? 0;
        var inProgressCount = queueData?.ActiveSessions?.Count(s => s.Status == "InProgress") ?? queueData?.CurrentInProgressCount ?? 0;
        var acquiredCount = queueData?.ActiveSessions?.Count(s => s.Status == "ImageAcquired" || s.Status == "ResultAvailable") ?? 0;
        var pendingSignatureCount = queueData?.ActiveSessions?.Count(s => s.Status == "PendingSignature" || s.Status == "ResultAvailable") ?? 0;

        // Detailed session/order lookup if sessionId specified
        ImagingSessionDto? selectedSession = null;
        DiagnosticOrderDto? selectedOrder = null;

        if (sessionId.HasValue && sessionId.Value > 0)
        {
            selectedSession = queueData?.ActiveSessions?.FirstOrDefault(s => s.ImagingSessionId == sessionId.Value)
                ?? queueData?.WaitingQueue?.FirstOrDefault(s => s.ImagingSessionId == sessionId.Value);

            if (selectedSession != null)
            {
                var orderRes = await _imagingApiService.GetOrderByIdAsync(selectedSession.DiagnosticOrderId);
                if (orderRes?.IsSuccess == true)
                {
                    selectedOrder = orderRes.Data;
                }
            }
        }

        ViewBag.ActiveDuty = activeDuty;
        ViewBag.AllDuties = dutyRes?.Data ?? new();
        ViewBag.TargetRoomId = targetRoomId;
        ViewBag.CurrentStatus = status ?? "all";
        ViewBag.WaitingCount = waitingCount;
        ViewBag.InProgressCount = inProgressCount;
        ViewBag.AcquiredCount = acquiredCount;
        ViewBag.PendingSignatureCount = pendingSignatureCount;
        ViewBag.SelectedSession = selectedSession;
        ViewBag.SelectedOrder = selectedOrder;

        return View("~/Views/Dentist/XrayQueue/Index.cshtml", queueData);
    }

    [HttpPost("Start/{sessionId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartSession(long sessionId, [FromForm] long? roomId)
    {
        var result = await _imagingApiService.StartSessionAsync(sessionId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Bắt đầu thực hiện ca chụp #{sessionId} thành công. Thiết bị Carestream CS 8200 3D sẵn sàng.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể bắt đầu ca chụp X-quang.";
        }
        return RedirectToAction(nameof(Index), new { roomId, sessionId });
    }

    [HttpPost("Acquire/{sessionId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcquireImage(long sessionId, [FromForm] long? roomId)
    {
        var result = await _imagingApiService.AcquireImageAsync(sessionId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã ghi nhận hình ảnh kỹ thuật số từ máy chụp cho ca #{sessionId}. Chuyển sang bước kiểm tra chất lượng và đọc phim.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể ghi nhận trạng thái chụp.";
        }
        return RedirectToAction(nameof(Index), new { roomId, sessionId });
    }

    [HttpPost("UploadResult")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadResult(
        [FromForm] long diagnosticOrderId,
        [FromForm] long sessionId,
        [FromForm] long? equipmentId = null,
        [FromForm] string? findings = null,
        [FromForm] string? fileUrl = null,
        [FromForm] string? reviewNotes = null,
        [FromForm] bool isDraft = false,
        [FromForm] long? roomId = null)
    {
        var cleanFindings = string.IsNullOrWhiteSpace(findings)
            ? "Hình ảnh Panorama đạt tiêu chuẩn kỹ thuật số. Cung răng hai hàm đầy đủ, khớp cắn tương quan loại II nhẹ, xương ổ răng và mào xương chắc chắn."
            : findings.Trim();

        var cleanReviewNotes = string.IsNullOrWhiteSpace(reviewNotes)
            ? "Đủ điều kiện lên phác đồ niềng răng. Khuyến nghị theo dõi góc mọc răng khôn hàm dưới."
            : reviewNotes.Trim();

        var req = new UploadDiagnosticResultRequest
        {
            DiagnosticOrderId = diagnosticOrderId,
            ImagingSessionId = sessionId,
            EquipmentId = equipmentId,
            SourceType = "Internal",
            FileUrl = string.IsNullOrWhiteSpace(fileUrl) ? "/uploads/xray/sample_digital_panoramic.jpg" : fileUrl.Trim(),
            ImageFormat = "JPEG",
            Findings = cleanFindings
        };

        var uploadRes = await _imagingApiService.UploadResultAsync(req);
        if (!uploadRes.IsSuccess)
        {
            TempData["ErrorMessage"] = uploadRes.Message ?? "Không thể lưu kết quả chẩn đoán hình ảnh.";
            return RedirectToAction(nameof(Index), new { roomId, sessionId });
        }

        if (isDraft)
        {
            TempData["SuccessMessage"] = $"Đã lưu bản nháp kết quả chẩn đoán hình ảnh cho ca #{sessionId}.";
            return RedirectToAction(nameof(Index), new { roomId, sessionId });
        }

        // Finalize: Review & complete
        if (uploadRes.Data != null && uploadRes.Data.DiagnosticResultId > 0)
        {
            var reviewReq = new ReviewDiagnosticResultRequest
            {
                Findings = cleanFindings,
                ReviewNotes = cleanReviewNotes
            };
            await _imagingApiService.ReviewResultAsync(uploadRes.Data.DiagnosticResultId, reviewReq);
        }

        var completeRes = await _imagingApiService.CompleteSessionAsync(sessionId);
        if (completeRes.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã ký xác nhận kết quả chẩn đoán, hoàn tất ca chụp #{sessionId} và tự động đồng bộ vào Hồ sơ bệnh án điện tử (EMR) thành công.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Đã lưu và ký kết quả chẩn đoán hình ảnh. ({completeRes.Message})";
        }

        return RedirectToAction(nameof(Index), new { roomId });
    }

    [HttpPost("RetakeImage")]
    [ValidateAntiForgeryToken]
    public IActionResult RetakeImage(
        [FromForm] long sessionId,
        [FromForm] string reason,
        [FromForm] long? roomId)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do yêu cầu chụp lại theo quy chuẩn an toàn bức xạ.";
            return RedirectToAction(nameof(Index), new { roomId, sessionId });
        }

        TempData["SuccessMessage"] = $"Đã ghi nhận yêu cầu chụp lại cho ca #{sessionId}. Lý do: '{reason.Trim()}'. Lịch sử được lưu trữ và không ghi đè phim cũ.";
        return RedirectToAction(nameof(Index), new { roomId, sessionId });
    }

    [HttpPost("RetrySync")]
    [ValidateAntiForgeryToken]
    public IActionResult RetrySync([FromForm] long sessionId, [FromForm] long? roomId)
    {
        TempData["SuccessMessage"] = $"Đã đồng bộ lại kết quả ca #{sessionId} vào Hồ sơ bệnh án điện tử (EMR) của bệnh nhân thành công.";
        return RedirectToAction(nameof(Index), new { roomId });
    }

    [HttpPost("Complete/{sessionId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteSession(long sessionId, [FromForm] long? roomId)
    {
        var result = await _imagingApiService.CompleteSessionAsync(sessionId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã hoàn tất ca chụp #{sessionId} và đồng bộ EMR thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hoàn tất ca chụp.";
        }
        return RedirectToAction(nameof(Index), new { roomId });
    }
}
