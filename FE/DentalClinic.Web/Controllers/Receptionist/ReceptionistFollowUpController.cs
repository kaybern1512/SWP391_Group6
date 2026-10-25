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
    public async Task<IActionResult> Index()
    {
        var result = await _postCareApi.GetReceptionistFollowUpsAsync();
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tải danh sách bệnh nhân cần theo dõi tái khám.";
        }

        var vm = new PatientFollowUpIndexViewModel
        {
            FollowUps = result.Data ?? new List<FollowUpScheduleDto>()
        };

        return View("~/Views/Receptionist/FollowUps/Index.cshtml", vm);
    }
}
