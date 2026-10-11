using System;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Treatment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Dentist;

[Authorize(Roles = "Dentist")]
[Route("Dentist/TreatmentPlans")]
public class DentistTreatmentPlanController : Controller
{
    private readonly ITreatmentApiService _treatmentApiService;

    public DentistTreatmentPlanController(ITreatmentApiService treatmentApiService)
    {
        _treatmentApiService = treatmentApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? status, string? search)
    {
        var result = await _treatmentApiService.GetDentistTreatmentPlansAsync(status, search);
        var plans = result.Data ?? new();

        var vm = new DentistTreatmentPlanListViewModel
        {
            Plans = plans,
            StatusFilter = status,
            SearchTerm = search,
            TotalPlans = plans.Count,
            ActivePlans = plans.Count(p => p.Status == "Active"),
            CompletedPlans = plans.Count(p => p.Status == "Completed")
        };

        return View("~/Views/Dentist/TreatmentPlans/Index.cshtml", vm);
    }

    [HttpGet("Detail/{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var result = await _treatmentApiService.GetTreatmentPlanDetailAsync(id);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy kế hoạch điều trị.";
            return RedirectToAction(nameof(Index));
        }

        var servicesResult = await _treatmentApiService.GetServiceOptionsAsync(result.Data.DepartmentId);
        var dentistsResult = await _treatmentApiService.GetDentistOptionsAsync(result.Data.DepartmentId);

        var vm = new DentistTreatmentPlanDetailViewModel
        {
            Plan = result.Data,
            Services = servicesResult.Data ?? new(),
            Dentists = dentistsResult.Data ?? new()
        };

        return View("~/Views/Dentist/TreatmentPlans/Detail.cshtml", vm);
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        var patientsResult = await _treatmentApiService.GetPatientOptionsAsync();
        var servicesResult = await _treatmentApiService.GetServiceOptionsAsync();
        var dentistsResult = await _treatmentApiService.GetDentistOptionsAsync();

        var vm = new DentistCreateTreatmentPlanViewModel
        {
            Patients = patientsResult.Data ?? new(),
            Services = servicesResult.Data ?? new(),
            Dentists = dentistsResult.Data ?? new()
        };

        return View("~/Views/Dentist/TreatmentPlans/Create.cshtml", vm);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTreatmentPlanRequest request)
    {
        if (request.PatientUserId <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn bệnh nhân tiếp nhận phác đồ.";
            return RedirectToAction(nameof(Create));
        }

        var result = await _treatmentApiService.CreateTreatmentPlanAsync(request);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tạo kế hoạch điều trị.";
            return RedirectToAction(nameof(Create));
        }

        TempData["SuccessMessage"] = $"Đã tạo phác đồ điều trị thành công (Mã KH: #{result.Data.TreatmentPlanId}).";
        return RedirectToAction(nameof(Detail), new { id = result.Data.TreatmentPlanId });
    }

    [HttpPost("AddPhase/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPhase(long id, CreateTreatmentPlanItemInput input)
    {
        var result = await _treatmentApiService.AddTreatmentPlanItemAsync(id, input);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể bổ sung giai đoạn điều trị.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã bổ sung giai đoạn điều trị mới vào phác đồ.";
        }
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("UpdateStatus/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(long id, string status)
    {
        var result = await _treatmentApiService.UpdatePlanStatusAsync(id, status);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật trạng thái kế hoạch.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái phác đồ sang [{status}].";
        }
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("UpdateItemStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItemStatus(long planId, long itemId, string status)
    {
        var result = await _treatmentApiService.UpdateItemStatusAsync(itemId, status);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật trạng thái giai đoạn.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã cập nhật trạng thái giai đoạn điều trị.";
        }
        return RedirectToAction(nameof(Detail), new { id = planId });
    }

    [HttpGet("Sessions")]
    public async Task<IActionResult> Sessions(string? search)
    {
        var sessionsResult = await _treatmentApiService.GetDentistTreatmentSessionsAsync(search);
        var phasesResult = await _treatmentApiService.GetEligiblePhasesAsync();
        var servicesResult = await _treatmentApiService.GetServiceOptionsAsync();

        var vm = new DentistTreatmentSessionListViewModel
        {
            Sessions = sessionsResult.Data ?? new(),
            EligiblePhases = phasesResult.Data ?? new(),
            Services = servicesResult.Data ?? new(),
            SearchTerm = search
        };

        return View("~/Views/Dentist/TreatmentPlans/Sessions.cshtml", vm);
    }

    [HttpPost("RecordSession")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordSession(CreateTreatmentSessionRequest request, long? returnPlanId)
    {
        if (request.TreatmentPlanItemId <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn giai đoạn phác đồ cần ghi nhận buổi điều trị.";
            return RedirectToAction(nameof(Sessions));
        }

        var result = await _treatmentApiService.CreateTreatmentSessionAsync(request);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể lưu buổi điều trị.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã ghi nhận buổi điều trị và dịch vụ thực hiện thành công.";
        }

        if (returnPlanId.HasValue && returnPlanId.Value > 0)
        {
            return RedirectToAction(nameof(Detail), new { id = returnPlanId.Value });
        }

        return RedirectToAction(nameof(Sessions));
    }
}
