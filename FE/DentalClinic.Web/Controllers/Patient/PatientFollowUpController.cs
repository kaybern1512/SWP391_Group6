using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.PostCare;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]
[Route("Patient/FollowUps")]
public class PatientFollowUpController : Controller
{
    private readonly IPostCareApiService _postCareApi;

    public PatientFollowUpController(IPostCareApiService postCareApi)
    {
        _postCareApi = postCareApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var result = await _postCareApi.GetPatientFollowUpsAsync();
        var vm = new PatientFollowUpIndexViewModel
        {
            FollowUps = result.IsSuccess && result.Data != null ? result.Data : new List<FollowUpScheduleDto>()
        };

        return View("~/Views/Patient/FollowUps/Index.cshtml", vm);
    }
}
