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
    private readonly IPostCareApiService _postCareApi;

    public DentistMedicalRecordController(IMedicalRecordApiService medicalRecordApi)
        : this(medicalRecordApi, null!)
    {
    }

    public DentistMedicalRecordController(
        IMedicalRecordApiService medicalRecordApi,
        IPostCareApiService postCareApi)
    {
        _medicalRecordApi = medicalRecordApi;
        _postCareApi = postCareApi;
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

        var hasFile = model.ImagingFile != null && model.ImagingFile.Length > 0;
        var normalizedImagingType = NormalizeImagingType(model.ImagingType);

        // Pre-validate file before creating clinical entry
        if (hasFile)
        {
            if (string.IsNullOrWhiteSpace(normalizedImagingType))
            {
                TempData["ErrorMessage"] = "Vui lòng chọn loại phim X-quang khi đính kèm tệp ảnh.";
                return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
            }

            if (model.ImagingFile!.Length > 15 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "Dung lượng tệp phim không được vượt quá 15 MB.";
                return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".dcm", ".dicom" };
            var ext = Path.GetExtension(model.ImagingFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["ErrorMessage"] = "Định dạng tệp không được hỗ trợ. Chỉ chấp nhận JPG, JPEG, PNG hoặc DICOM (.dcm, .dicom).";
                return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
            }
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

        // Phân biệt: Nếu KHÔNG đính kèm file nhưng có chọn loại phim -> tạo chỉ định chụp chờ thực hiện
        if (!hasFile && !string.IsNullOrWhiteSpace(normalizedImagingType))
        {
            req.ImagingOrders.Add(new CreateImagingRecordRequest
            {
                ImagingType = normalizedImagingType,
                ResultSummary = model.ImagingResultSummary?.Trim(),
                FileUrl = null
            });
        }

        // Bước 1: Lưu ClinicalEntry trước để lấy ID hợp lệ (không tạo bản ghi giả hay gán URL thủ công)
        var result = await _medicalRecordApi.CreateClinicalEntryAsync(req);
        if (!result.IsSuccess || result.Data == null || result.Data.ClinicalEntryId <= 0)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể lưu chẩn đoán bệnh án.";
            return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
        }

        var createdEntryId = result.Data.ClinicalEntryId;

        // Bước 2: Nếu có đính kèm file ảnh, gọi API upload chuyên dụng với ID đợt khám vừa tạo
        if (hasFile)
        {
            var uploadResult = await _medicalRecordApi.UploadImagingRecordAsync(
                createdEntryId,
                model.ImagingFile!,
                normalizedImagingType!,
                model.ImagingResultSummary?.Trim());

            if (uploadResult.IsSuccess)
            {
                TempData["SuccessMessage"] = "Đã ghi nhận chẩn đoán và tải lên phim X-quang thành công!";
            }
            else
            {
                // Thông báo lỗi chi tiết, không báo thành công giả, hướng dẫn bác sĩ upload lại tại đợt khám
                TempData["ErrorMessage"] = $"Đã ghi nhận chẩn đoán bệnh án (#{createdEntryId}) nhưng tải tệp ảnh X-quang thất bại: {uploadResult.Message ?? "Lỗi tải ảnh"}. Bác sĩ có thể bấm nút 'Upload Phim' tại đợt khám để thử lại.";
            }
        }
        else
        {
            TempData["SuccessMessage"] = "Đã ghi nhận chẩn đoán và cập nhật sơ đồ răng thành công!";
        }

        return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId });
    }

    private static string? NormalizeImagingType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return null;
        var t = type.Trim();
        if (t.Contains("Panorama", StringComparison.OrdinalIgnoreCase)) return "Panorama";
        if (t.Contains("Periapical", StringComparison.OrdinalIgnoreCase) || t.Contains("chóp", StringComparison.OrdinalIgnoreCase)) return "Periapical";
        if (t.Contains("Cephalometric", StringComparison.OrdinalIgnoreCase) || t.Contains("sọ nghiêng", StringComparison.OrdinalIgnoreCase)) return "Cephalometric";
        if (t.Contains("Intraoral", StringComparison.OrdinalIgnoreCase) || t.Contains("miệng", StringComparison.OrdinalIgnoreCase)) return "Intraoral";
        return t;
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

        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.Pragma = "no-cache";
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

    [HttpPost("BookFollowUp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BookFollowUp(CreateFollowUpAppointmentRequest request)
    {
        if (request.PatientUserId <= 0)
        {
            TempData["ErrorMessage"] = "Thông tin bệnh nhân không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do hoặc nội dung chỉ định tái khám.";
            return RedirectToAction(nameof(Index), new { patientId = request.PatientUserId });
        }

        if (request.ScheduledStart <= DateTime.Now)
        {
            TempData["ErrorMessage"] = "Thời gian hẹn tái khám phải là thời điểm trong tương lai.";
            return RedirectToAction(nameof(Index), new { patientId = request.PatientUserId });
        }

        var result = await _postCareApi.DoctorBookFollowUpAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đặt lịch tái khám thành công vào lúc {request.ScheduledStart:HH:mm dd/MM/yyyy} (Mã hẹn: #{result.Data?.AppointmentCode})!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể đặt lịch tái khám.";
        }

        return RedirectToAction(nameof(Index), new { patientId = request.PatientUserId });
    }

    [HttpGet("AvailableSlots")]
    public async Task<IActionResult> AvailableSlots(DateTime date, int durationMinutes = 30)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(userIdClaim, out var doctorUserId) || doctorUserId <= 0)
        {
            return BadRequest(new { success = false, message = "Không xác định được danh tính bác sĩ." });
        }

        var result = await _postCareApi.GetFollowUpAvailableSlotsAsync(doctorUserId, date, durationMinutes);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Json(new { success = true, data = result.Data });
    }
}
