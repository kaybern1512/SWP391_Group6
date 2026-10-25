using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.MedicalRecord;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Dentist;

[Authorize(Roles = "Dentist")]
[Route("Dentist/MedicalRecords")]
public class DentistMedicalRecordController : Controller
{
    private readonly IMedicalRecordApiService _medicalRecordApi;

    public DentistMedicalRecordController(IMedicalRecordApiService medicalRecordApi)
    {
        _medicalRecordApi = medicalRecordApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(long? patientId = null, string? search = null, long? appointmentId = null)
    {
        var patientListResult = await _medicalRecordApi.SearchPatientsAsync(search);
        var patients = patientListResult.IsSuccess && patientListResult.Data != null ? patientListResult.Data : new List<PatientLookupDto>();

        long? targetPatientId = patientId;
        if (!targetPatientId.HasValue && patients.Count > 0)
        {
            targetPatientId = patients[0].PatientUserId;
        }

        MedicalRecordDto? selectedRecord = null;
        if (targetPatientId.HasValue)
        {
            var recordResult = await _medicalRecordApi.GetByPatientIdAsync(targetPatientId.Value);
            if (recordResult.IsSuccess)
            {
                selectedRecord = recordResult.Data;
            }
        }

        var vm = new DentistMedicalRecordIndexViewModel
        {
            SelectedPatientUserId = targetPatientId,
            SelectedRecord = selectedRecord,
            Patients = patients,
            Search = search,
            NewEntryForm = new CreateClinicalEntryFormModel
            {
                PatientUserId = targetPatientId ?? 0,
                AppointmentId = appointmentId
            }
        };

        return View("~/Views/Dentist/MedicalRecords/Index.cshtml", vm);
    }

    [HttpPost("CreateEntry")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEntry(CreateClinicalEntryFormModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ chẩn đoán lâm sàng.";
            return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
        }

        var toothConditions = new List<ToothConditionInput>();
        if (!string.IsNullOrWhiteSpace(model.ToothConditionsJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<ToothConditionInput>>(model.ToothConditionsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null)
                {
                    toothConditions = parsed;
                }
            }
            catch
            {
                // Ignore parse errors, proceed with empty tooth conditions
            }
        }

        var req = new CreateClinicalEntryRequest
        {
            PatientUserId = model.PatientUserId,
            AppointmentId = model.AppointmentId,
            Symptoms = model.Symptoms?.Trim(),
            ClinicalFindings = model.ClinicalFindings?.Trim(),
            Diagnosis = model.Diagnosis.Trim(),
            DentalConditions = model.DentalConditions?.Trim(),
            ClinicalNotes = model.ClinicalNotes?.Trim(),
            CompleteAppointment = model.CompleteAppointment,
            ToothConditions = toothConditions
        };

        if (!string.IsNullOrWhiteSpace(model.ImagingType))
        {
            req.ImagingOrders.Add(new CreateImagingRecordRequest
            {
                ImagingType = model.ImagingType.Trim(),
                ResultSummary = model.ImagingResultSummary?.Trim(),
                FileUrl = model.ImagingFileUrl?.Trim()
            });
        }

        var result = await _medicalRecordApi.CreateClinicalEntryAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã ghi nhận chẩn đoán và cập nhật sơ đồ răng thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể lưu chẩn đoán bệnh án.";
        }

        return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
    }

    [HttpPost("UploadImaging")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImaging([FromForm] UploadDentalImagingFormModel model)
    {
        if (model.ClinicalEntryId <= 0)
        {
            TempData["ErrorMessage"] = "Mã ca khám không hợp lệ.";
            return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
        }

        if (model.File == null || model.File.Length == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn tập tin phim X-quang hoặc ảnh răng.";
            return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
        }

        if (model.File.Length > 15 * 1024 * 1024)
        {
            TempData["ErrorMessage"] = "Dung lượng tệp phim không được vượt quá 15 MB.";
            return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".dcm", ".dicom" };
        var ext = Path.GetExtension(model.File.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            TempData["ErrorMessage"] = "Định dạng tệp không được hỗ trợ. Chỉ chấp nhận JPG, JPEG, PNG hoặc DICOM (.dcm, .dicom).";
            return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
        }

        if (string.IsNullOrWhiteSpace(model.ImagingType))
        {
            TempData["ErrorMessage"] = "Vui lòng chọn loại phim X-quang.";
            return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
        }

        var result = await _medicalRecordApi.UploadImagingRecordAsync(
            model.ClinicalEntryId,
            model.File,
            model.ImagingType.Trim(),
            model.ResultSummary?.Trim());

        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Tải lên phim X-quang / ảnh răng thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tải lên phim chụp X-quang.";
        }

        return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
    }

    [HttpPost("AddImagingRecord")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddImagingRecord(AddImagingRecordFormModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn loại phim chụp cận lâm sàng.";
            return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
        }

        var req = new CreateImagingRecordRequest
        {
            ClinicalEntryId = model.ClinicalEntryId,
            ImagingType = model.ImagingType.Trim(),
            FileUrl = model.FileUrl?.Trim(),
            ResultSummary = model.ResultSummary?.Trim()
        };

        var result = await _medicalRecordApi.AddImagingRecordAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã lưu kết quả phim chụp X-quang thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể lưu thông tin phim chụp.";
        }

        return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
    }

    [HttpGet("ImagingContent/{imagingRecordId:long}")]
    public async Task<IActionResult> ImagingContent(long imagingRecordId)
    {
        if (imagingRecordId <= 0)
        {
            return NotFound();
        }

        var (stream, contentType, statusCode) = await _medicalRecordApi.GetImagingStreamAsync(imagingRecordId);
        if (statusCode == 401)
        {
            return Unauthorized();
        }

        if (statusCode == 403)
        {
            return Forbid();
        }

        if (statusCode == 404 || stream == null)
        {
            return NotFound();
        }

        if (statusCode != 200)
        {
            return StatusCode(statusCode);
        }

        return File(stream, contentType);
    }

    [HttpPost("DeleteImaging")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImaging(long imagingRecordId, long patientUserId)
    {
        if (imagingRecordId <= 0)
        {
            TempData["ErrorMessage"] = "Mã bản ghi hình ảnh không hợp lệ.";
            return RedirectToAction(nameof(Index), new { patientId = patientUserId });
        }

        var result = await _medicalRecordApi.DeleteImagingRecordAsync(imagingRecordId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã xóa phim X-quang thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể xóa phim X-quang.";
        }

        return RedirectToAction(nameof(Index), new { patientId = patientUserId });
    }
}
