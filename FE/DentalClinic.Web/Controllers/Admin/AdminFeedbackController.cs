using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin/Feedback")]
public class AdminFeedbackController : Controller
{
    private readonly IFeedbackApiService _feedbackApi;
    private readonly IAdminApiService _adminApi;

    public AdminFeedbackController(IFeedbackApiService feedbackApi, IAdminApiService adminApi)
    {
        _feedbackApi = feedbackApi;
        _adminApi = adminApi;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        string? status = null,
        long? dentistUserId = null,
        long? departmentId = null,
        int? minRating = null,
        string? search = null,
        int page = 1,
        int pageSize = 15)
    {
        var filter = new FeedbackFilterRequest
        {
            Status = status,
            DentistUserId = dentistUserId,
            DepartmentId = departmentId,
            MinRating = minRating.HasValue ? (byte)minRating.Value : null,
            Search = search,
            Page = page,
            PageSize = pageSize
        };

        var feedbacksTask = _feedbackApi.GetFeedbacksAsync(filter);
        var dentistsTask = _adminApi.GetUsersAsync("Dentist");
        var deptsTask = _adminApi.GetDepartmentsAsync();

        await Task.WhenAll(feedbacksTask, dentistsTask, deptsTask);

        var vm = new AdminFeedbackManageViewModel
        {
            PagedFeedbacks = feedbacksTask.Result.IsSuccess && feedbacksTask.Result.Data != null
                ? feedbacksTask.Result.Data
                : new FeedbackPagedResultDto { Page = page, PageSize = pageSize },
            Dentists = dentistsTask.Result.IsSuccess && dentistsTask.Result.Data != null
                ? dentistsTask.Result.Data
                : new(),
            Departments = deptsTask.Result.IsSuccess && deptsTask.Result.Data != null
                ? deptsTask.Result.Data
                : new(),
            CurrentStatus = status,
            SelectedDentistId = dentistUserId,
            SelectedDepartmentId = departmentId,
            MinRating = minRating,
            SearchKeyword = search,
            CurrentPage = page,
            CurrentPageSize = pageSize
        };

        return View("~/Views/Admin/Feedback/Index.cshtml", vm);
    }

    [HttpPost("Moderate/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Moderate(long id, [FromForm] ModerateFeedbackRequest request, [FromForm] string? returnStatus, [FromForm] int returnPage)
    {
        var result = await _feedbackApi.ModerateFeedbackAsync(id, request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = request.Status == "Approved"
                ? "Đã duyệt hiển thị công khai đánh giá thành công."
                : "Đã ẩn đánh giá khỏi giao diện công khai.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật trạng thái kiểm duyệt đánh giá.";
        }

        return RedirectToAction(nameof(Index), new { status = returnStatus, page = returnPage });
    }

    [HttpPost("Respond/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(long id, [FromForm] RespondFeedbackRequest request, [FromForm] string? returnStatus, [FromForm] int returnPage)
    {
        if (string.IsNullOrWhiteSpace(request.Response))
        {
            TempData["ErrorMessage"] = "Nội dung phản hồi không được để trống.";
            return RedirectToAction(nameof(Index), new { status = returnStatus, page = returnPage });
        }

        var result = await _feedbackApi.RespondFeedbackAsync(id, request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã gửi phản hồi chính thức từ phòng khám đến bệnh nhân thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể gửi phản hồi.";
        }

        return RedirectToAction(nameof(Index), new { status = returnStatus, page = returnPage });
    }
}
