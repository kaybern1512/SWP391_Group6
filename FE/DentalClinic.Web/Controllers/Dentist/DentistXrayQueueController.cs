using System;
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
    public async Task<IActionResult> Index([FromQuery] long? roomId = null)
    {
        var targetRoomId = roomId ?? 9; // Default XRAY-01 is RoomId 9
        var today = ClinicTimeHelper.TodayDateOnly;

        // Fetch dentist's own duty roster
        var dutyRes = await _imagingApiService.GetMyXrayDutyAsync(today, today);
        var activeDuty = dutyRes?.Data?.FirstOrDefault(r => r.Status == "Active");

        // Fetch current queue status for room
        var queueRes = await _imagingApiService.GetQueueStatusAsync(targetRoomId);

        ViewBag.ActiveDuty = activeDuty;
        ViewBag.AllDuties = dutyRes?.Data ?? new();
        ViewBag.TargetRoomId = targetRoomId;

        return View("~/Views/Dentist/XrayQueue/Index.cshtml", queueRes?.Data);
    }

    [HttpPost("Start/{sessionId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartSession(long sessionId, [FromForm] long? roomId)
    {
        var result = await _imagingApiService.StartSessionAsync(sessionId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Bắt đầu thực hiện ca chụp #{sessionId} thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể bắt đầu ca chụp X-quang.";
        }
        return RedirectToAction(nameof(Index), new { roomId });
    }

    [HttpPost("Acquire/{sessionId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcquireImage(long sessionId, [FromForm] long? roomId)
    {
        var result = await _imagingApiService.AcquireImageAsync(sessionId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã ghi nhận chụp hình ảnh cho ca #{sessionId}.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể ghi nhận trạng thái chụp.";
        }
        return RedirectToAction(nameof(Index), new { roomId });
    }

    [HttpPost("UploadResult")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadResult(
        [FromForm] long diagnosticOrderId,
        [FromForm] long sessionId,
        [FromForm] long? equipmentId,
        [FromForm] string? findings,
        [FromForm] string? fileUrl,
        [FromForm] long? roomId)
    {
        var req = new UploadDiagnosticResultRequest
        {
            DiagnosticOrderId = diagnosticOrderId,
            ImagingSessionId = sessionId,
            EquipmentId = equipmentId,
            SourceType = "Internal",
            FileUrl = string.IsNullOrWhiteSpace(fileUrl) ? "/uploads/xray/sample_digital_panoramic.jpg" : fileUrl.Trim(),
            ImageFormat = "JPEG",
            Findings = string.IsNullOrWhiteSpace(findings) ? "Hình ảnh đạt chuẩn kỹ thuật số. Cung răng và cấu trúc xương hàm rõ nét." : findings.Trim()
        };

        var uploadRes = await _imagingApiService.UploadResultAsync(req);
        if (uploadRes.IsSuccess)
        {
            var completeRes = await _imagingApiService.CompleteSessionAsync(sessionId);
            if (completeRes.IsSuccess)
            {
                TempData["SuccessMessage"] = $"Đã lưu kết quả chẩn đoán và hoàn tất ca chụp #{sessionId} thành công.";
            }
            else
            {
                TempData["SuccessMessage"] = $"Đã lưu kết quả chẩn đoán hình ảnh. ({completeRes.Message})";
            }
        }
        else
        {
            TempData["ErrorMessage"] = uploadRes.Message ?? "Không thể lưu kết quả chẩn đoán hình ảnh.";
        }

        return RedirectToAction(nameof(Index), new { roomId });
    }

    [HttpPost("Complete/{sessionId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteSession(long sessionId, [FromForm] long? roomId)
    {
        var result = await _imagingApiService.CompleteSessionAsync(sessionId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã hoàn tất ca chụp #{sessionId}.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hoàn tất ca chụp.";
        }
        return RedirectToAction(nameof(Index), new { roomId });
    }
}
