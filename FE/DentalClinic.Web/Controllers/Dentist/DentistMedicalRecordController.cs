using System;
using System.Collections.Generic;
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

    [HttpPost("AddImagingRecord")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddImagingRecord(AddImagingRecordFormModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn loại phim chụp cận lâm sàng.";
            return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
        }

        // Ưu tiên 1: Upload file từ máy tính
        if (model.ImageFile != null && model.ImageFile.Length > 0)
        {
            if (model.ImageFile.Length > 15 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "Kích thước tập tin vượt quá giới hạn tối đa 15 MB.";
                return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
            }

            await using var stream = model.ImageFile.OpenReadStream();
            var uploadResult = await _medicalRecordApi.UploadImagingFileAsync(
                model.ClinicalEntryId,
                model.ImagingType.Trim(),
                model.ResultSummary?.Trim(),
                stream,
                model.ImageFile.FileName,
                model.ImageFile.ContentType);

            if (uploadResult.IsSuccess)
            {
                TempData["SuccessMessage"] = "Đã tải lên và lưu kết quả phim chụp X-quang thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = uploadResult.Message ?? "Không thể tải lên phim chụp.";
            }

            return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
        }

        // Ưu tiên 2: Lưu URL có sẵn (backward compatibility)
        if (!string.IsNullOrWhiteSpace(model.FileUrl))
        {
            var req = new CreateImagingRecordRequest
            {
                ClinicalEntryId = model.ClinicalEntryId,
                ImagingType = model.ImagingType.Trim(),
                FileUrl = model.FileUrl.Trim(),
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

        TempData["ErrorMessage"] = "Vui lòng chọn tập tin ảnh phim chụp từ máy tính hoặc cung cấp đường dẫn ảnh.";
        return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
    }

    [HttpPost("DeleteImagingRecord")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImagingRecord(long imagingRecordId, long patientUserId)
    {
        var result = await _medicalRecordApi.DeleteImagingRecordAsync(imagingRecordId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã xóa phim chụp X-quang khỏi hồ sơ bệnh án thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể xóa phim chụp X-quang.";
        }

        return RedirectToAction(nameof(Index), new { patientId = patientUserId });
    }

    [HttpGet("ImagingFile/{id:long}")]
    public async Task<IActionResult> GetImagingFile(long id, [FromQuery] bool download = false)
    {
        var streamResult = await _medicalRecordApi.GetImagingFileStreamAsync(id, download);
        if (streamResult == null)
        {
            return NotFound();
        }

        if (streamResult.StatusCode == 401)
        {
            streamResult.Dispose();
            return Unauthorized();
        }
        if (streamResult.StatusCode == 403)
        {
            streamResult.Dispose();
            return Forbid();
        }
        if (streamResult.StatusCode == 404 || streamResult.Stream == null)
        {
            streamResult.Dispose();
            return NotFound();
        }

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        if (download && !string.IsNullOrWhiteSpace(streamResult.FileName))
        {
            return File(streamResult.Stream, streamResult.ContentType, streamResult.FileName);
        }

        return File(streamResult.Stream, streamResult.ContentType);
    }
}
