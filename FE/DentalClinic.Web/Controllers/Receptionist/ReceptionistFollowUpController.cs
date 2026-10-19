using System;
using System.Linq;
using System.Threading.Tasks;
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
    public async Task<IActionResult> Index(
        DateTime? fromDate,
        DateTime? toDate,
        string? status,
        string? quickFilter = "all",
        string? search = null)
    {
        var now = DentalClinic.Web.Helpers.ClinicDateTimeHelper.GetClinicToday();

        DateTime? queryFrom = fromDate;
        DateTime? queryTo = toDate;

        if (quickFilter == "today")
        {
            queryFrom = now;
            queryTo = now;
        }
        else if (quickFilter == "week")
        {
            queryFrom = now;
            queryTo = now.AddDays(7);
        }

        var result = await _postCareApiService.GetReceptionistFollowUpsAsync(queryFrom, queryTo, status);
        var followUps = result.Data ?? new();

        if (quickFilter == "overdue")
        {
            followUps = followUps.Where(f => f.IsOverdue).ToList();
        }
        else if (quickFilter == "upcoming")
        {
            followUps = followUps.Where(f => f.IsUpcoming).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            followUps = followUps.Where(f =>
                (!string.IsNullOrEmpty(f.PatientName) && f.PatientName.ToLower().Contains(s)) ||
                (!string.IsNullOrEmpty(f.PatientCode) && f.PatientCode.ToLower().Contains(s)) ||
                (!string.IsNullOrEmpty(f.PhoneNumber) && f.PhoneNumber.Contains(s)) ||
                (!string.IsNullOrEmpty(f.DentistName) && f.DentistName.ToLower().Contains(s)) ||
                (!string.IsNullOrEmpty(f.DepartmentName) && f.DepartmentName.ToLower().Contains(s))
            ).ToList();
        }

        var vm = new ReceptionistFollowUpIndexViewModel
        {
            FollowUps = followUps,
            FromDate = fromDate,
            ToDate = toDate,
            StatusFilter = status,
            QuickFilter = quickFilter ?? "all",
            SearchKeyword = search
        };

        return View("~/Views/Receptionist/FollowUps/Index.cshtml", vm);
    }
}
