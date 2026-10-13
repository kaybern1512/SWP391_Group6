using System;
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
    private readonly IPostCareApiService _postCareApiService;

    public ReceptionistFollowUpController(IPostCareApiService postCareApiService)
    {
        _postCareApiService = postCareApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index([FromQuery] string? status, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var result = await _postCareApiService.GetReceptionistFollowUpsAsync(status, fromDate, toDate);

        var vm = new ReceptionistFollowUpsViewModel
        {
            FollowUps = result.Data ?? new(),
            StatusFilter = status,
            FromDate = fromDate,
            ToDate = toDate
        };

        return View("~/Views/Receptionist/FollowUps/Index.cshtml", vm);
    }

    [HttpPost("UpdateStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(long followUpId, UpdateFollowUpStatusRequest request)
    {
        if (followUpId <= 0)
        {
            TempData["ErrorMessage"] = "Mã theo dõi tái khám không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        if (request?.Status == "Completed")
        {
            TempData["ErrorMessage"] = "Lễ tân không có quyền đánh dấu hoàn thành ca tái khám. Chỉ bác sĩ điều trị mới có quyền xác nhận ca tái khám hoàn tất.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _postCareApiService.UpdateFollowUpStatusAsync(followUpId, request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Cập nhật trạng thái theo dõi tái khám thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Cập nhật trạng thái thất bại.";
        }

        return RedirectToAction(nameof(Index));
    }
}
