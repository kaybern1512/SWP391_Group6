using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Prescription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DentalClinic.Web.Controllers.Dentist;

[Authorize(Roles = "Dentist")]
[Route("Dentist/Prescriptions")]
public class DentistPrescriptionController : Controller
{
    private readonly IPrescriptionApiService _prescriptionApi;
    private readonly IMedicineApiService _medicineApi;
    private readonly IAppointmentApiService? _appointmentApi;

    [ActivatorUtilitiesConstructor]
    public DentistPrescriptionController(
        IPrescriptionApiService prescriptionApi,
        IMedicineApiService medicineApi,
        IAppointmentApiService appointmentApi)
    {
        _prescriptionApi = prescriptionApi;
        _medicineApi = medicineApi;
        _appointmentApi = appointmentApi;
    }

    public DentistPrescriptionController(
        IPrescriptionApiService prescriptionApi,
        IMedicineApiService medicineApi)
        : this(prescriptionApi, medicineApi, null!)
    {
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search = null, long? appointmentId = null, bool autoOpen = false, string? returnUrl = null)
    {
        var presResult = await _prescriptionApi.GetDentistPrescriptionsAsync(search);
        var apptResult = await _prescriptionApi.GetEligibleAppointmentsAsync();
        var medResult = await _medicineApi.GetAvailableForPrescriptionAsync();

        var prescriptions = presResult.IsSuccess && presResult.Data != null ? presResult.Data : new List<PrescriptionDto>();
        var eligibleAppts = apptResult?.IsSuccess == true && apptResult.Data != null ? apptResult.Data : new List<PrescriptionEligibleAppointmentDto>();
        var statusMap = new Dictionary<long, PrescriptionDispensationStatusDto>();

        foreach (var p in prescriptions)
        {
            var stRes = await _medicineApi.GetPrescriptionDispensationStatusAsync(p.PrescriptionId);
            if (stRes != null && stRes.IsSuccess && stRes.Data != null)
            {
                statusMap[p.PrescriptionId] = stRes.Data;
            }
        }

        PrescriptionEligibleAppointmentDto? targetAppt = null;
        if (appointmentId.HasValue && appointmentId.Value > 0)
        {
            targetAppt = eligibleAppts.FirstOrDefault(a => a.AppointmentId == appointmentId.Value);
            if (targetAppt == null && _appointmentApi != null)
            {
                var apptDetail = await _appointmentApi.GetAppointmentDetailAsync(appointmentId.Value);
                if (apptDetail?.IsSuccess == true && apptDetail.Data != null)
                {
                    targetAppt = new PrescriptionEligibleAppointmentDto
                    {
                        AppointmentId = apptDetail.Data.AppointmentId,
                        AppointmentCode = apptDetail.Data.AppointmentCode,
                        PatientUserId = apptDetail.Data.PatientUserId,
                        PatientName = apptDetail.Data.PatientName,
                        PatientCode = apptDetail.Data.PatientCode,
                        PatientPhone = apptDetail.Data.PatientPhone,
                        DepartmentName = apptDetail.Data.DepartmentName,
                        ServiceName = apptDetail.Data.ServiceName,
                        ScheduledStart = apptDetail.Data.ScheduledStart,
                        Status = apptDetail.Data.Status,
                        HasPrescription = false
                    };
                    eligibleAppts.Insert(0, targetAppt);
                }
            }
        }

        var existingPres = targetAppt != null
            ? prescriptions.FirstOrDefault(p => p.AppointmentId == targetAppt.AppointmentId)
            : null;

        var vm = new DentistPrescriptionIndexViewModel
        {
            Prescriptions = prescriptions,
            EligibleAppointments = eligibleAppts,
            Search = search,
            TargetAppointmentId = appointmentId,
            TargetAppointment = targetAppt,
            AutoOpenModal = (autoOpen || (targetAppt != null && existingPres == null)),
            ReturnUrl = returnUrl,
            ExistingPrescriptionForTarget = existingPres,
            NewPrescriptionForm = new CreatePrescriptionFormModel
            {
                AppointmentId = appointmentId ?? 0,
                ReturnUrl = returnUrl
            },
            AvailableMedicines = medResult?.IsSuccess == true && medResult.Data != null ? medResult.Data : new List<MedicineDto>(),
            DispensationStatusMap = statusMap
        };

        return View("~/Views/Dentist/Prescriptions/Index.cshtml", vm);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePrescriptionFormModel model)
    {
        if (model.AppointmentId <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn ca khám để kê đơn thuốc.";
            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }
            return RedirectToAction(nameof(Index));
        }

        var items = new List<CreatePrescriptionItemInput>();
        if (!string.IsNullOrWhiteSpace(model.ItemsJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<CreatePrescriptionItemInput>>(model.ItemsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null)
                {
                    items = parsed;
                }
            }
            catch
            {
                // Ignore parse errors
            }
        }

        if (items.Count == 0)
        {
            TempData["ErrorMessage"] = "Đơn thuốc cần có ít nhất 1 loại thuốc.";
            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }
            return RedirectToAction(nameof(Index), new { appointmentId = model.AppointmentId, autoOpen = true, returnUrl = model.ReturnUrl });
        }

        var req = new CreatePrescriptionRequest
        {
            AppointmentId = model.AppointmentId,
            GeneralInstructions = model.GeneralInstructions?.Trim(),
            Items = items
        };

        var result = await _prescriptionApi.CreatePrescriptionAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã kê đơn thuốc thành công cho ca khám {result.Data?.AppointmentCode}!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể lưu đơn thuốc.";
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction(nameof(Index), new { appointmentId = model.AppointmentId });
    }

    [HttpGet("{id:long}/Print")]
    public async Task<IActionResult> Print(long id)
    {
        var result = await _prescriptionApi.GetPrescriptionDetailAsync(id);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn thuốc cần in.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new PrescriptionPrintViewModel
        {
            Prescription = result.Data
        };

        return View("~/Views/Dentist/Prescriptions/Print.cshtml", vm);
    }

    [HttpPost("Dispense")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispense(DispensePrescriptionFormModel model)
    {
        if (model.PrescriptionId <= 0)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn thuốc hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var items = new List<DispenseItemInput>();
        if (!string.IsNullOrWhiteSpace(model.ItemsJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<DispenseItemInput>>(model.ItemsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null) items = parsed;
            }
            catch { }
        }

        if (items.Count == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn ít nhất 1 loại thuốc từ kho và nhập số lượng cấp phát.";
            return RedirectToAction(nameof(Index));
        }

        var req = new CreateMedicineDispensationRequest
        {
            PrescriptionId = model.PrescriptionId,
            Notes = model.Notes?.Trim(),
            Items = items
        };

        var result = await _medicineApi.DispenseMedicineAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã cấp thuốc thành công từ kho cho đơn #RX-{model.PrescriptionId}! Hệ thống đã tự động trừ tồn kho và đồng bộ sang Hóa đơn viện phí.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hoàn tất cấp phát thuốc từ kho.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:long}/Batches")]
    public async Task<IActionResult> GetBatches(long id)
    {
        var result = await _medicineApi.GetBatchesAsync(id);
        return Json(result);
    }
}
