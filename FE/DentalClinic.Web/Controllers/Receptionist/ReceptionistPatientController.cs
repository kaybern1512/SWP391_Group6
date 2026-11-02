using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Receptionist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist")]
[Route("Receptionist/Patients")]
public class ReceptionistPatientController : Controller
{
    private readonly IAdminApiService _adminApi;
    private readonly IAppointmentApiService _appointmentApi;

    public ReceptionistPatientController(
        IAdminApiService adminApi,
        IAppointmentApiService appointmentApi)
    {
        _adminApi = adminApi;
        _appointmentApi = appointmentApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search = null)
    {
        ApiResult<List<AdminUserDto>>? result = null;
        if (_appointmentApi != null)
        {
            result = await _appointmentApi.GetPatientsAsync(search);
        }

        if ((result == null || (!result.IsSuccess && result.Data == null)) && _adminApi != null)
        {
            var fallback = await _adminApi.GetUsersAsync("Patient", search);
            if (fallback != null)
            {
                result = fallback;
            }
        }

        var patients = result != null && result.IsSuccess && result.Data != null 
            ? result.Data 
            : new List<AdminUserDto>();

        var vm = new ReceptionistPatientListViewModel
        {
            Patients = patients,
            Search = search,
            NewPatient = new WalkInPatientFormModel()
        };

        return View("~/Views/Receptionist/Patients/Index.cshtml", vm);
    }

    [HttpPost("RegisterWalkIn")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterWalkIn([Bind(Prefix = "NewPatient")] WalkInPatientFormModel? model)
    {
        if ((model == null || string.IsNullOrWhiteSpace(model.FullName)) && Request?.HasFormContentType == true)
        {
            var form = Request.Form;
            model = new WalkInPatientFormModel
            {
                FullName = form["FullName"].FirstOrDefault() ?? form["NewPatient.FullName"].FirstOrDefault() ?? "",
                Email = form["Email"].FirstOrDefault() ?? form["NewPatient.Email"].FirstOrDefault() ?? "",
                PhoneNumber = form["PhoneNumber"].FirstOrDefault() ?? form["NewPatient.PhoneNumber"].FirstOrDefault() ?? "",
                Gender = form["Gender"].FirstOrDefault() ?? form["NewPatient.Gender"].FirstOrDefault() ?? "Nam",
                DateOfBirth = DateTime.TryParse(form["DateOfBirth"].FirstOrDefault() ?? form["NewPatient.DateOfBirth"].FirstOrDefault(), out var dob) ? dob : null
            };
        }

        if (model == null || string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.PhoneNumber) || string.IsNullOrWhiteSpace(model.Email))
        {
            TempData["ErrorMessage"] = "Thông tin bệnh nhân chưa hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Index));
        }

        var regRequest = new RegisterWalkInPatientRequest
        {
            FullName = model.FullName.Trim(),
            Email = model.Email.Trim().ToLowerInvariant(),
            PhoneNumber = model.PhoneNumber.Trim(),
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender
        };

        var result = await _appointmentApi.RegisterWalkInPatientAsync(regRequest);
        if (result.IsSuccess && result.Data != null)
        {
            TempData["SuccessMessage"] = $"Tiếp nhận hồ sơ bệnh nhân {result.Data.FullName} (Mã: {result.Data.PatientCode}) thành công! Bạn có thể đặt lịch ngay.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tạo hồ sơ bệnh nhân.";
        }

        return RedirectToAction(nameof(Index));
    }
}
