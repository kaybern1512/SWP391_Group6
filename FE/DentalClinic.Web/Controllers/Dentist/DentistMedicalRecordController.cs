using DentalClinic.Web.Helpers;
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
using Microsoft.Extensions.DependencyInjection;

namespace DentalClinic.Web.Controllers.Dentist;

[Authorize(Roles = "Dentist")]
[Route("Dentist/MedicalRecords")]
public class DentistMedicalRecordController : Controller
{
    private readonly IMedicalRecordApiService _medicalRecordApi;
    private readonly IAppointmentApiService? _appointmentApiService;
    private readonly IPostCareApiService? _postCareApi;
    private readonly ITreatmentApiService? _treatmentApi;
    private readonly IPrescriptionApiService? _prescriptionApi;
    private readonly IMedicineApiService? _medicineApi;
    private readonly ICareEpisodeApiService? _careEpisodeApi;

    [ActivatorUtilitiesConstructor]
    public DentistMedicalRecordController(
        IMedicalRecordApiService medicalRecordApi,
        IAppointmentApiService appointmentApiService,
        IPostCareApiService postCareApi,
        ITreatmentApiService treatmentApi,
        IPrescriptionApiService prescriptionApi,
        IMedicineApiService medicineApi,
        ICareEpisodeApiService? careEpisodeApi = null)
    {
        _medicalRecordApi = medicalRecordApi;
        _appointmentApiService = appointmentApiService;
        _postCareApi = postCareApi;
        _treatmentApi = treatmentApi;
        _prescriptionApi = prescriptionApi;
        _medicineApi = medicineApi;
        _careEpisodeApi = careEpisodeApi;
    }

    public DentistMedicalRecordController(
        IMedicalRecordApiService medicalRecordApi,
        IAppointmentApiService appointmentApiService,
        IPostCareApiService postCareApi)
        : this(medicalRecordApi, appointmentApiService, postCareApi, null!, null!, null!)
    {
    }

    public DentistMedicalRecordController(
        IMedicalRecordApiService medicalRecordApi,
        IPostCareApiService postCareApi)
        : this(medicalRecordApi, null!, postCareApi, null!, null!, null!)
    {
    }

    public DentistMedicalRecordController(
        IMedicalRecordApiService medicalRecordApi,
        IAppointmentApiService appointmentApiService)
        : this(medicalRecordApi, appointmentApiService, null!, null!, null!, null!)
    {
    }

    public DentistMedicalRecordController(IMedicalRecordApiService medicalRecordApi)
        : this(medicalRecordApi, null!, null!, null!, null!, null!)
    {
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(long? patientId = null, string? search = null, long? appointmentId = null, bool isExamining = false)
    {
        AppointmentDto? activeAppt = null;
        if (appointmentId.HasValue && appointmentId.Value > 0 && _appointmentApiService != null)
        {
            var apptRes = await _appointmentApiService.GetAppointmentDetailAsync(appointmentId.Value);
            if (apptRes.IsSuccess && apptRes.Data != null)
            {
                activeAppt = apptRes.Data;
                if (!patientId.HasValue || patientId.Value <= 0)
                {
                    patientId = activeAppt.PatientUserId;
                }
            }
        }

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

        // If active appointment was not passed, detect if patient has an active appointment today
        if (activeAppt == null && targetPatientId.HasValue && _appointmentApiService != null)
        {
            var todayApptsResult = await _appointmentApiService.GetDentistTodayAppointmentsAsync();
            if (todayApptsResult.IsSuccess && todayApptsResult.Data != null)
            {
                activeAppt = todayApptsResult.Data.FirstOrDefault(a => a.PatientUserId == targetPatientId.Value && (a.Status == "InService" || a.Status == "CheckedIn"));
                if (activeAppt != null && !appointmentId.HasValue)
                {
                    appointmentId = activeAppt.AppointmentId;
                }
            }
        }

        // Load department services and dentists for clinical additional service ordering
        var services = new List<DepartmentServiceDto>();
        var dentists = new List<DentistOptionDto>();
        var chairs = new List<DentalChairOptionDto>();

        if (_appointmentApiService != null)
        {
            var deptId = activeAppt?.DepartmentId;
            if (deptId.HasValue && deptId.Value > 0)
            {
                var sResult = await _appointmentApiService.GetServicesAsync(deptId.Value);
                if (sResult.IsSuccess && sResult.Data != null) services = sResult.Data;

                var dResult = await _appointmentApiService.GetDentistsAsync(deptId.Value);
                if (dResult.IsSuccess && dResult.Data != null) dentists = dResult.Data;
            }
            else
            {
                var allDepts = await _appointmentApiService.GetDepartmentsAsync();
                if (allDepts.IsSuccess && allDepts.Data != null && allDepts.Data.Count > 0)
                {
                    var sResult = await _appointmentApiService.GetServicesAsync(allDepts.Data[0].DepartmentId);
                    if (sResult.IsSuccess && sResult.Data != null) services = sResult.Data;

                    var dResult = await _appointmentApiService.GetDentistsAsync(allDepts.Data[0].DepartmentId);
                    if (dResult.IsSuccess && dResult.Data != null) dentists = dResult.Data;
                }
            }

            if (appointmentId.HasValue && appointmentId.Value > 0)
            {
                var cResult = await _appointmentApiService.GetDentistRoomChairsAsync(appointmentId.Value);
                if (cResult.IsSuccess && cResult.Data != null)
                {
                    chairs = cResult.Data;
                }
            }
        }

        // Performed services for active appointment
        var performedServices = new List<PerformedServiceDto>();
        if (appointmentId.HasValue && appointmentId.Value > 0 && _treatmentApi != null)
        {
            var perfResult = await _treatmentApi.GetPerformedServicesByAppointmentAsync(appointmentId.Value);
            if (perfResult.IsSuccess && perfResult.Data != null)
            {
                performedServices = perfResult.Data;
            }
        }

        // Patient treatment plans
        var patientPlans = new List<TreatmentPlanDto>();
        if (targetPatientId.HasValue && _treatmentApi != null)
        {
            var plansResult = await _treatmentApi.GetDentistTreatmentPlansAsync(search: null);
            if (plansResult.IsSuccess && plansResult.Data != null)
            {
                patientPlans = plansResult.Data.Where(p => p.PatientUserId == targetPatientId.Value).ToList();
            }
        }

        // Active prescription and chairside dispensations
        PrescriptionDto? activePrescription = null;
        var dispensations = new List<MedicineDispensationDto>();
        var availableMedicines = new List<MedicineDto>();

        if (appointmentId.HasValue && appointmentId.Value > 0)
        {
            if (_prescriptionApi != null)
            {
                var presList = await _prescriptionApi.GetDentistPrescriptionsAsync();
                if (presList.IsSuccess && presList.Data != null)
                {
                    activePrescription = presList.Data.FirstOrDefault(p => p.AppointmentId == appointmentId.Value);
                }
            }

            if (_medicineApi != null)
            {
                var dispResult = await _medicineApi.GetDispensationsByAppointmentIdAsync(appointmentId.Value);
                if (dispResult.IsSuccess && dispResult.Data != null)
                {
                    dispensations = dispResult.Data;
                }

                var medResult = await _medicineApi.GetAvailableForPrescriptionAsync();
                if (medResult.IsSuccess && medResult.Data != null)
                {
                    availableMedicines = medResult.Data;
                }
            }
        }

        var isCompleted = activeAppt != null && (activeAppt.Status == "Completed" || activeAppt.Status == "Cancelled");
        var isCurrentlyExamining = !isCompleted && (isExamining || (activeAppt != null && activeAppt.Status == "InService"));

        var vm = new DentistMedicalRecordIndexViewModel
        {
            SelectedPatientUserId = targetPatientId,
            SelectedRecord = selectedRecord,
            Patients = patients,
            Search = search,
            ActiveAppointmentId = appointmentId,
            ActiveAppointment = activeAppt,
            IsExamining = isCurrentlyExamining,
            DepartmentServices = services,
            DepartmentDentists = dentists,
            AvailableChairs = chairs,
            PerformedServices = performedServices,
            PatientTreatmentPlans = patientPlans,
            ActivePrescription = activePrescription,
            Dispensations = dispensations,
            AvailableMedicines = availableMedicines,
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
            return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId, appointmentId = model.AppointmentId, isExamining = true });
        }

        var hasFile = model.ImagingFile != null && model.ImagingFile.Length > 0;
        var normalizedImagingType = NormalizeImagingType(model.ImagingType);

        // Pre-validate file before creating clinical entry
        if (hasFile)
        {
            if (string.IsNullOrWhiteSpace(normalizedImagingType))
            {
                TempData["ErrorMessage"] = "Vui lòng chọn loại phim X-quang khi đính kèm tệp ảnh.";
                return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId, appointmentId = model.AppointmentId, isExamining = model.AppointmentId.HasValue });
            }

            if (model.ImagingFile!.Length > 15 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "Dung lượng tệp phim không được vượt quá 15 MB.";
                return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId, appointmentId = model.AppointmentId, isExamining = model.AppointmentId.HasValue });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".dcm", ".dicom" };
            var ext = Path.GetExtension(model.ImagingFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["ErrorMessage"] = "Định dạng tệp không được hỗ trợ. Chỉ chấp nhận JPG, JPEG, PNG hoặc DICOM (.dcm, .dicom).";
                return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId, appointmentId = model.AppointmentId, isExamining = model.AppointmentId.HasValue });
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
            return RedirectToAction(nameof(Index), new { patientId = model.PatientUserId, appointmentId = model.AppointmentId, isExamining = model.AppointmentId.HasValue });
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
                TempData["ErrorMessage"] = $"Đã ghi nhận chẩn đoán bệnh án (#{createdEntryId}) nhưng tải tệp ảnh X-quang thất bại: {uploadResult.Message ?? "Lỗi tải ảnh"}. Bác sĩ có thể bấm nút 'Upload Phim' tại đợt khám để thử lại.";
            }
        }
        else
        {
            TempData["SuccessMessage"] = "Đã ghi nhận chẩn đoán và cập nhật sơ đồ răng thành công!";
        }

                if (model.CompleteAppointment)
        {
            return RedirectToAction("Today", "DentistAppointment");
        }

        return RedirectToAction(nameof(Index), new
        {
            patientId = model.PatientUserId,
            appointmentId = model.AppointmentId,
            isExamining = model.AppointmentId.HasValue && !model.CompleteAppointment
        });
    }

    [HttpPost("CompleteExam")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteExam(long appointmentId, long patientUserId, string? notes = null)
    {
        if (_appointmentApiService != null)
        {
            var result = await _appointmentApiService.DentistCompleteAppointmentAsync(appointmentId, notes);
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = result.Message ?? "Đã hoàn thành ca khám và điều trị cho bệnh nhân thành công!";
                return RedirectToAction("Today", "DentistAppointment");
            }
            else
            {
                TempData["ErrorMessage"] = result.Message ?? "Không thể hoàn thành ca khám.";
                return RedirectToAction(nameof(Index), new { patientId = patientUserId, appointmentId, isExamining = true });
            }
        }

        TempData["SuccessMessage"] = "Đã hoàn thành ca khám thành công!";
        return RedirectToAction("Today", "DentistAppointment");
    }

    [HttpPost("AddAdditionalService")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAdditionalService(
        long appointmentId,
        long patientUserId,
        long serviceId,
        int quantity = 1,
        decimal? unitPrice = null,
        string? executionMode = null,
        long? dentalChairId = null,
        long? targetDentistUserId = null,
        string? notes = null)
    {
        if (serviceId <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn dịch vụ muốn bổ sung / thực hiện.";
            return RedirectToAction(nameof(Index), new { patientId = patientUserId, appointmentId, isExamining = true });
        }

        // CareEpisode Synchronization: Same Doctor task vs Cross-Doctor handoff
        if (_careEpisodeApi != null && appointmentId > 0)
        {
            if (executionMode == "delegate")
            {
                var apptDetail = _appointmentApiService != null ? await _appointmentApiService.GetAppointmentDetailAsync(appointmentId) : null;
                var toDeptId = apptDetail?.Data?.DepartmentId ?? 1;
                await _careEpisodeApi.CreateInternalReferralAsync(new CreateCareEpisodeReferralRequest
                {
                    SourceAppointmentId = appointmentId,
                    RequestedServiceId = serviceId,
                    ToDepartmentId = toDeptId,
                    PreferredDentistUserId = targetDentistUserId,
                    Reason = notes ?? "Chuyển tiếp dịch vụ trong đợt khám",
                    Urgency = "Standard"
                });
            }
            else
            {
                await _careEpisodeApi.AddServiceTaskAsync(new CreateEpisodeServiceTaskRequest
                {
                    AppointmentId = appointmentId,
                    DepartmentServiceId = serviceId,
                    Quantity = quantity > 0 ? quantity : 1,
                    Notes = notes
                });
            }
        }

        // Section 6: Ghi nhận đúng PerformedServices thực tế tại ghế điều trị khi bác sĩ tự thực hiện
        if (executionMode != "delegate" && _treatmentApi != null && appointmentId > 0)
        {
            var req = new AddPerformedServiceToAppointmentRequest
            {
                DepartmentServiceId = serviceId,
                Quantity = quantity > 0 ? quantity : 1,
                UnitPrice = unitPrice,
                Note = notes
            };
            var addRes = await _treatmentApi.AddPerformedServiceToAppointmentAsync(appointmentId, req);
            if (!addRes.IsSuccess)
            {
                TempData["ErrorMessage"] = addRes.Message ?? "Không thể ghi nhận dịch vụ đã thực hiện.";
                return RedirectToAction(nameof(Index), new { patientId = patientUserId, appointmentId, isExamining = true });
            }
        }

        // If doctor performs self and picked chair
        if (executionMode != "delegate" && dentalChairId.HasValue && dentalChairId.Value > 0 && _appointmentApiService != null)
        {
            var apptDetail = await _appointmentApiService.GetAppointmentDetailAsync(appointmentId);
            if (apptDetail?.Data != null && apptDetail.Data.Status != "Completed" && apptDetail.Data.Status != "Cancelled")
            {
                await _appointmentApiService.DentistAssignChairAsync(appointmentId, dentalChairId.Value);
            }
        }

        if (executionMode == "delegate" && targetDentistUserId.HasValue && targetDentistUserId.Value > 0)
        {
            TempData["SuccessMessage"] = "Đã chuyển tiếp chỉ định dịch vụ cho bác sĩ phụ trách trong chuyên khoa thành công!";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã ghi nhận dịch vụ thực hiện tại ghế thành công! Dịch vụ đã được cập nhật vào danh mục thanh toán viện phí.";
        }

        return RedirectToAction(nameof(Index), new { patientId = patientUserId, appointmentId, isExamining = true });
    }

    [HttpPost("DeletePerformedService")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePerformedService(long performedServiceId, long appointmentId, long patientUserId)
    {
        if (_treatmentApi != null && performedServiceId > 0)
        {
            var res = await _treatmentApi.DeletePerformedServiceAsync(performedServiceId);
            if (res.IsSuccess)
            {
                TempData["SuccessMessage"] = "Đã xóa dịch vụ thực hiện khỏi ca khám thành công.";
            }
            else
            {
                TempData["ErrorMessage"] = res.Message ?? "Không thể xóa dịch vụ thực hiện.";
            }
        }
        return RedirectToAction(nameof(Index), new { patientId = patientUserId, appointmentId, isExamining = true });
    }

    [HttpPost("CreatePrescriptionQuick")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePrescriptionQuick(
        long appointmentId,
        long patientUserId,
        string? generalInstructions,
        List<string> medicationNames,
        List<string?> dosages,
        List<string?> frequencies,
        List<string?> durations,
        List<string?> instructions)
    {
        if (_prescriptionApi == null || appointmentId <= 0)
        {
            TempData["ErrorMessage"] = "Dịch vụ kê đơn chưa sẵn sàng.";
            return RedirectToAction(nameof(Index), new { patientId = patientUserId, appointmentId, isExamining = true });
        }

        var items = new List<CreatePrescriptionItemInput>();
        if (medicationNames != null)
        {
            for (int i = 0; i < medicationNames.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(medicationNames[i]))
                {
                    items.Add(new CreatePrescriptionItemInput
                    {
                        MedicationName = medicationNames[i].Trim(),
                        Dosage = dosages != null && i < dosages.Count ? dosages[i]?.Trim() : null,
                        Frequency = frequencies != null && i < frequencies.Count ? frequencies[i]?.Trim() : null,
                        Duration = durations != null && i < durations.Count ? durations[i]?.Trim() : null,
                        Instructions = instructions != null && i < instructions.Count ? instructions[i]?.Trim() : null
                    });
                }
            }
        }

        if (!items.Any())
        {
            TempData["ErrorMessage"] = "Đơn thuốc phải có ít nhất một loại thuốc.";
            return RedirectToAction(nameof(Index), new { patientId = patientUserId, appointmentId, isExamining = true });
        }

        var req = new CreatePrescriptionRequest
        {
            AppointmentId = appointmentId,
            GeneralInstructions = generalInstructions,
            Items = items
        };

        var presRes = await _prescriptionApi.CreatePrescriptionAsync(req);
        if (presRes.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã kê đơn thuốc điện tử thành công! Bác sĩ có thể cấp phát thuốc ngay tại ghế.";
        }
        else
        {
            TempData["ErrorMessage"] = presRes.Message ?? "Không thể tạo đơn thuốc.";
        }

        return RedirectToAction(nameof(Index), new { patientId = patientUserId, appointmentId, isExamining = true });
    }

    [HttpPost("DispenseMedicineQuick")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DispenseMedicineQuick(
        long appointmentId,
        long patientUserId,
        long prescriptionId,
        long medicineId,
        long batchId,
        int quantity,
        long? prescriptionItemId = null,
        string? notes = null)
    {
        if (_medicineApi == null || prescriptionId <= 0 || batchId <= 0 || quantity <= 0)
        {
            TempData["ErrorMessage"] = "Thông tin cấp phát thuốc không hợp lệ. Vui lòng chọn lô thuốc và nhập số lượng lớn hơn 0.";
            return RedirectToAction(nameof(Index), new { patientId = patientUserId, appointmentId, isExamining = true });
        }

        var req = new CreateMedicineDispensationRequest
        {
            PrescriptionId = prescriptionId,
            Notes = notes,
            Items = new List<DispenseItemInput>
            {
                new DispenseItemInput
                {
                    PrescriptionItemId = prescriptionItemId,
                    MedicineId = medicineId,
                    MedicineBatchId = batchId,
                    Quantity = quantity
                }
            }
        };

        var dispRes = await _medicineApi.DispenseMedicineAsync(req);
        if (dispRes.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã cấp phát {quantity} đơn vị thuốc cho bệnh nhân thành công! Kho dược nội bộ đã được cập nhật tự động.";
        }
        else
        {
            TempData["ErrorMessage"] = dispRes.Message ?? "Không thể cấp phát thuốc.";
        }

        return RedirectToAction(nameof(Index), new { patientId = patientUserId, appointmentId, isExamining = true });
    }

    [HttpGet("GetBatchesForMedicine")]
    public async Task<IActionResult> GetBatchesForMedicine(long medicineId)
    {
        if (_medicineApi == null || medicineId <= 0) return Json(new { success = false, data = new List<object>() });
        var result = await _medicineApi.GetBatchesAsync(medicineId);
        return Json(new { success = result.IsSuccess, data = result.Data ?? new() });
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

        if (request.ScheduledStart <= ClinicTimeHelper.Now)
        {
            TempData["ErrorMessage"] = "Thời gian hẹn tái khám phải là thời điểm trong tương lai.";
            return RedirectToAction(nameof(Index), new { patientId = request.PatientUserId });
        }

        if (_postCareApi == null)
        {
            TempData["ErrorMessage"] = "Dịch vụ tái khám chưa được cấu hình.";
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

        if (_postCareApi == null)
        {
            return BadRequest(new { success = false, message = "Dịch vụ tái khám chưa được cấu hình." });
        }

        var result = await _postCareApi.GetFollowUpAvailableSlotsAsync(doctorUserId, date, durationMinutes);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Json(new { success = true, data = result.Data });
    }
}
