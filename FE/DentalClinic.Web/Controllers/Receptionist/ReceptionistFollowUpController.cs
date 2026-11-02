using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.PostCare;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist,SystemAdministrator")]
[Route("Receptionist/FollowUps")]
public class ReceptionistFollowUpController : Controller
{
    private readonly IPostCareApiService _postCareApi;

    public ReceptionistFollowUpController(IPostCareApiService postCareApi)
    {
        _postCareApi = postCareApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? tab = "due")
    {
        var result = await _postCareApi.GetReceptionistFollowUpsAsync();
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tải danh sách bệnh nhân cần theo dõi tái khám.";
        }

        ViewBag.ActiveTab = tab ?? "due";

        var vm = new PatientFollowUpIndexViewModel
        {
            FollowUps = result.Data ?? new List<FollowUpScheduleDto>()
        };

        return View("~/Views/Receptionist/FollowUps/Index.cshtml", vm);
    }

    [HttpPost("Confirm/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(long id, [FromForm] string? tab = "due")
    {
        var req = new UpdateFollowUpStatusRequest
        {
            Status = "Confirmed",
            Note = "Lễ tân đã gọi điện xác nhận bệnh nhân sẽ đến đúng hẹn."
        };
        var result = await _postCareApi.UpdateFollowUpStatusAsync(id, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã xác nhận cuộc gọi và chuyển lịch tái khám sang trạng thái [Đã Xác Nhận].";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật trạng thái.";
        }
        return RedirectToAction(nameof(Index), new { tab });
    }

    [HttpPost("Complete/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(long id, [FromForm] string? tab = "due")
    {
        var req = new UpdateFollowUpStatusRequest
        {
            Status = "Completed",
            Note = "Bệnh nhân đã đến tái khám hoàn tất."
        };
        var result = await _postCareApi.UpdateFollowUpStatusAsync(id, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã đánh dấu lịch tái khám [Hoàn Tất].";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật trạng thái.";
        }
        return RedirectToAction(nameof(Index), new { tab });
    }

    [HttpPost("Reschedule/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(long id, [FromForm] DateTime newDate, [FromForm] string? note, [FromForm] string? tab = "due")
    {
        if (newDate <= DateTime.Today)
        {
            TempData["ErrorMessage"] = "Ngày dời hẹn phải từ ngày mai trở đi.";
            return RedirectToAction(nameof(Index), new { tab });
        }

        var req = new UpdateFollowUpStatusRequest
        {
            Status = "Pending",
            NewRecommendedDate = newDate,
            Note = string.IsNullOrWhiteSpace(note) ? "Bệnh nhân xin dời lịch tái khám" : note
        };
        var result = await _postCareApi.UpdateFollowUpStatusAsync(id, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã dời lịch tái khám sang ngày {newDate:dd/MM/yyyy}.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể dời lịch tái khám.";
        }
        return RedirectToAction(nameof(Index), new { tab });
    }
}