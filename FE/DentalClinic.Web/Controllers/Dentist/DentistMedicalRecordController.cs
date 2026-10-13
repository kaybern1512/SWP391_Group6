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
    public async Task<IActionResult> Index(long? patientId = null, string? search = null)
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
                PatientUserId = targetPatientId ?? 0
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
            ToothConditions = toothConditions
        };

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
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> UploadImaging(
        long patientUserId,
        long clinicalEntryId,
        Microsoft.AspNetCore.Http.IFormFile? imageFile,
        string imagingType,
        string? resultSummary)
    {
        if (imageFile == null || imageFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn một tệp hình ảnh để tải lên.";
            return RedirectToAction(nameof(Index), new { patientId = patientUserId });
        }

        using var stream = imageFile.OpenReadStream();
        var result = await _medicalRecordApi.UploadImagingRecordAsync(
            clinicalEntryId,
            stream,
            imageFile.FileName,
            imageFile.ContentType,
            imagingType,
            resultSummary);

        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã tải lên và lưu trữ ảnh chẩn đoán/X-quang thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Tải ảnh thất bại.";
        }

        return RedirectToAction(nameof(Index), new { patientId = patientUserId });
    }

    [HttpPost("DeleteImaging")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImaging(long patientUserId, long imagingRecordId)
    {
        var result = await _medicalRecordApi.DeleteImagingRecordAsync(imagingRecordId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã xóa ảnh chẩn đoán thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Xóa ảnh thất bại.";
        }

        return RedirectToAction(nameof(Index), new { patientId = patientUserId });
    }

    [HttpGet("ImageContent/{id:long}")]
    public async Task<IActionResult> ImageContent(long id)
    {
        var result = await _medicalRecordApi.GetImageContentAsync(id);
        if (result == null)
        {
            return NotFound();
        }

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        var contentType = result.Value.ContentType.ToLowerInvariant();
        if (!allowedTypes.Contains(contentType))
        {
            return BadRequest();
        }

        Response.Headers["Cache-Control"] = "private, no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(result.Value.Stream, contentType);
    }
}
