using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.ClinicalCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Dentist;

[Authorize(Roles = "Dentist")]
[Route("Dentist/Clinical")]
public class DentistClinicalController : Controller
{
    private readonly IClinicalCoreApiClient _clinicalCoreApi;
    private readonly IAppointmentApiService _appointmentApi;

    public DentistClinicalController(
        IClinicalCoreApiClient clinicalCoreApi,
        IAppointmentApiService appointmentApi)
    {
        _clinicalCoreApi = clinicalCoreApi;
        _appointmentApi = appointmentApi;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Dentist/Clinical/FindPatient.cshtml");

    [HttpGet("OpenPatient")]
    public IActionResult OpenPatient(long patientUserId) =>
        RedirectToAction(nameof(Patient), new { patientUserId });

    [HttpGet("Patients/{patientUserId:long}")]
    public async Task<IActionResult> Patient(long patientUserId, long? departmentId = null)
    {
        if (patientUserId <= 0)
        {
            return BadRequest();
        }

        var model = await BuildWorkspaceAsync(patientUserId, departmentId);
        return View("~/Views/Dentist/Clinical/Patient.cshtml", model);
    }

    [HttpPost("ClinicalEntry")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateClinicalEntry(ClinicalEntryForm form)
    {
        if (!ModelState.IsValid)
        {
            TempData["ClinicalError"] = "Kiểm tra lại thông tin bệnh án và cuộc hẹn.";
            return RedirectToAction(nameof(Patient), new { patientUserId = form.PatientUserId });
        }

        var request = new CreateClinicalEntryRequest
        {
            AppointmentId = form.AppointmentId,
            Symptoms = form.Symptoms,
            ClinicalFindings = form.ClinicalFindings,
            Diagnosis = form.Diagnosis,
            DentalConditions = form.DentalConditions,
            ClinicalNotes = form.ClinicalNotes,
            OdontogramEntries = form.OdontogramEntries,
            ImagingRecords = form.ImagingRecords
                .Where(image => !string.IsNullOrWhiteSpace(image.ImagingType))
                .ToList()
        };

        var result = await _clinicalCoreApi.CreateClinicalEntryAsync(form.PatientUserId, request);
        SetResultMessage(result.IsSuccess, result.Message);
        return RedirectToAction(nameof(Patient), new { patientUserId = form.PatientUserId });
    }

    [HttpPost("TreatmentPlan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTreatmentPlan(TreatmentPlanForm form)
    {
        if (!ModelState.IsValid || form.Items == null || form.Items.Count == 0)
        {
            TempData["ClinicalError"] = "Kế hoạch cần có ít nhất một hạng mục hợp lệ.";
            return RedirectToAction(nameof(Patient), new { patientUserId = form.PatientUserId, departmentId = form.DepartmentId });
        }

        var result = await _clinicalCoreApi.CreateTreatmentPlanAsync(new CreateTreatmentPlanRequest
        {
            PatientUserId = form.PatientUserId,
            DepartmentId = form.DepartmentId,
            Items = form.Items.Select(item => new CreateTreatmentPlanItemRequest
            {
                DepartmentServiceId = item.DepartmentServiceId,
                AssignedDentistUserId = item.AssignedDentistUserId,
                ToothNumber = item.ToothNumber,
                SequenceNo = item.SequenceNo,
                ExpectedDate = item.ExpectedDate,
                EstimatedCost = item.EstimatedCost,
                Note = item.Note
            }).ToList()
        });

        if (result.IsSuccess && result.Data != null)
        {
            TempData["LastCreatedTreatmentPlanId"] = result.Data.TreatmentPlanId;
            if (result.Data.Items.Count > 0)
            {
                TempData["LastCreatedTreatmentPlanItemId"] = result.Data.Items[0].TreatmentPlanItemId;
            }
        }
        SetResultMessage(result.IsSuccess, result.Message);
        return RedirectToAction(nameof(Patient), new { patientUserId = form.PatientUserId, departmentId = form.DepartmentId });
    }

    [HttpPost("TreatmentSession")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTreatmentSession(TreatmentSessionForm form)
    {
        if (!ModelState.IsValid)
        {
            TempData["ClinicalError"] = "Thông tin ca điều trị chưa hợp lệ.";
            return RedirectToAction(nameof(Patient), new { patientUserId = form.PatientUserId });
        }

        var result = await _clinicalCoreApi.CreateTreatmentSessionAsync(new CreateTreatmentSessionRequest
        {
            TreatmentPlanItemId = form.TreatmentPlanItemId,
            AppointmentId = form.AppointmentId,
            TreatmentResult = form.TreatmentResult,
            ClinicalNote = form.ClinicalNote,
            PerformedServices =
            {
                new CreatePerformedServiceRequest
                {
                    DepartmentServiceId = form.DepartmentServiceId,
                    Quantity = form.Quantity,
                    UnitPrice = form.UnitPrice
                }
            }
        });

        if (result.IsSuccess && result.Data != null)
        {
            TempData["LastCreatedTreatmentSessionId"] = result.Data.TreatmentSessionId;
        }
        SetResultMessage(result.IsSuccess, result.Message);
        return RedirectToAction(nameof(Patient), new { patientUserId = form.PatientUserId });
    }

    [HttpPost("Prescription")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePrescription(PrescriptionForm form)
    {
        if (!ModelState.IsValid)
        {
            TempData["ClinicalError"] = "Kiểm tra thông tin đơn thuốc.";
            return RedirectToAction(nameof(Patient), new { patientUserId = form.PatientUserId });
        }

        var result = await _clinicalCoreApi.CreatePrescriptionAsync(new CreatePrescriptionRequest
        {
            TreatmentSessionId = form.TreatmentSessionId,
            GeneralInstructions = form.GeneralInstructions,
            Items = form.Items.Select(item => new CreatePrescriptionItemRequest
            {
                MedicationName = item.MedicationName,
                Dosage = item.Dosage,
                Frequency = item.Frequency,
                Duration = item.Duration,
                Instructions = item.Instructions
            }).ToList()
        });

        SetResultMessage(result.IsSuccess, result.Message);
        return RedirectToAction(nameof(Patient), new { patientUserId = form.PatientUserId });
    }

    [HttpPost("DepartmentReferral")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDepartmentReferral(DepartmentReferralForm form)
    {
        if (!ModelState.IsValid || form.FromDepartmentId == form.ToDepartmentId)
        {
            TempData["ClinicalError"] = "Chuyển khoa cần chọn hai khoa khác nhau và nhập lý do.";
            return RedirectToAction(nameof(Patient), new { patientUserId = form.PatientUserId });
        }

        var result = await _clinicalCoreApi.CreateDepartmentReferralAsync(new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = form.TreatmentSessionId,
            FromDepartmentId = form.FromDepartmentId,
            ToDepartmentId = form.ToDepartmentId,
            Reason = form.Reason
        });

        SetResultMessage(result.IsSuccess, result.Message);
        return RedirectToAction(nameof(Patient), new { patientUserId = form.PatientUserId });
    }

    [HttpPost("FollowUp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFollowUp(FollowUpForm form)
    {
        if (!ModelState.IsValid || form.RecommendedDate <= DateTime.Now)
        {
            TempData["ClinicalError"] = "Ngày tái khám đề xuất phải ở trong tương lai.";
            return RedirectToAction(nameof(Patient), new { patientUserId = form.PatientUserId });
        }

        var result = await _clinicalCoreApi.CreateFollowUpAsync(new CreateFollowUpRequest
        {
            TreatmentSessionId = form.TreatmentSessionId,
            RecommendedDate = form.RecommendedDate,
            Reason = form.Reason
        });

        SetResultMessage(result.IsSuccess, result.Message);
        return RedirectToAction(nameof(Patient), new { patientUserId = form.PatientUserId });
    }

    private async Task<DentistClinicalWorkspaceViewModel> BuildWorkspaceAsync(long patientUserId, long? departmentId)
    {
        var recordTask = _clinicalCoreApi.GetMedicalRecordAsync(patientUserId);
        var departmentsTask = _appointmentApi.GetDepartmentsAsync();
        var appointmentsTask = _appointmentApi.GetDentistTodayAppointmentsAsync();
        await Task.WhenAll(recordTask, departmentsTask, appointmentsTask);

        var recordResult = await recordTask;
        var departmentsResult = await departmentsTask;
        var appointmentsResult = await appointmentsTask;
        var model = new DentistClinicalWorkspaceViewModel
        {
            PatientUserId = patientUserId,
            MedicalRecord = recordResult.IsSuccess ? recordResult.Data : null,
            Departments = departmentsResult.IsSuccess ? departmentsResult.Data ?? new() : new(),
            Appointments = appointmentsResult.IsSuccess
                ? appointmentsResult.Data?.Where(appointment => appointment.PatientUserId == patientUserId).ToList() ?? new()
                : new(),
            LastCreatedTreatmentPlanItemId = ReadTempLong("LastCreatedTreatmentPlanItemId"),
            LastCreatedTreatmentSessionId = ReadTempLong("LastCreatedTreatmentSessionId"),
            SelectedDepartmentId = departmentId,
            Message = TempData["ClinicalError"] as string
        };

        if (!recordResult.IsSuccess && recordResult.StatusCode != 404)
        {
            model.Message ??= recordResult.Message;
        }
        if (!departmentsResult.IsSuccess)
        {
            model.Message ??= departmentsResult.Message;
        }
        if (!appointmentsResult.IsSuccess)
        {
            model.Message ??= appointmentsResult.Message;
        }

        if (departmentId.HasValue)
        {
            var services = await _appointmentApi.GetServicesAsync(departmentId.Value);
            if (services.IsSuccess)
            {
                model.Services = services.Data ?? new();
            }
            else
            {
                model.Message ??= services.Message;
            }
        }

        return model;
    }

    private long? ReadTempLong(string key) =>
        long.TryParse(TempData[key]?.ToString(), out var value) ? value : null;

    private void SetResultMessage(bool success, string? message)
    {
        TempData[success ? "SuccessMessage" : "ErrorMessage"] =
            message ?? (success ? "Đã lưu thành công." : "Không thể hoàn thành thao tác.");
    }
}
