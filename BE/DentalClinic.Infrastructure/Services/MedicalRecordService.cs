using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Common.Validation;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Application.Validators;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class MedicalRecordService : IMedicalRecordService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IMedicalImagingStorageService? _imagingStorageService;
    private readonly IDentalImageFileValidator _fileValidator;
    private readonly ILogger<MedicalRecordService> _logger;

    private static readonly string[] All32Teeth = new[]
    {
        "18", "17", "16", "15", "14", "13", "12", "11",
        "21", "22", "23", "24", "25", "26", "27", "28",
        "48", "47", "46", "45", "44", "43", "42", "41",
        "31", "32", "33", "34", "35", "36", "37", "38"
    };

    public MedicalRecordService(
        DentalClinicDbContext dbContext,
        ILogger<MedicalRecordService> logger)
        : this(dbContext, (IMedicalImagingStorageService?)null, null, logger)
    {
    }

    [ActivatorUtilitiesConstructor]
    public MedicalRecordService(
        DentalClinicDbContext dbContext,
        IMedicalImagingStorageService? imagingStorageService,
        IDentalImageFileValidator? fileValidator,
        ILogger<MedicalRecordService> logger)
    {
        _dbContext = dbContext;
        _imagingStorageService = imagingStorageService;
        _fileValidator = fileValidator ?? new DentalImageFileValidator();
        _logger = logger;
    }

    public MedicalRecordService(
        DentalClinicDbContext dbContext,
        IImageStorageService? imageStorageService,
        IDentalImageFileValidator? fileValidator,
        ILogger<MedicalRecordService> logger)
        : this(
            dbContext,
            imageStorageService is IMedicalImagingStorageService mis
                ? mis
                : (imageStorageService != null ? new MedicalImagingStorageServiceAdapter(imageStorageService) : null),
            fileValidator,
            logger)
    {
    }

    public async Task<ApiResponse<MedicalRecordDto>> GetMedicalRecordByPatientIdAsync(long patientUserId, CancellationToken ct = default)
    {
        var patient = await _dbContext.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == patientUserId, ct);

        if (patient == null)
        {
            return ApiResponse<MedicalRecordDto>.Fail("Không tìm thấy hồ sơ bệnh nhân.");
        }

        var record = await _dbContext.DentalMedicalRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.PatientUserId == patientUserId, ct);

        if (record == null)
        {
            var newRecord = new DentalMedicalRecord
            {
                PatientUserId = patientUserId,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.DentalMedicalRecords.Add(newRecord);
            await _dbContext.SaveChangesAsync(ct);
            record = newRecord;
        }

        var clinicalEntries = await _dbContext.ClinicalEntries
            .AsNoTracking()
            .Where(c => c.MedicalRecordId == record.RecordId)
            .Include(c => c.Dentist)
                .ThenInclude(d => d.StaffProfile)
            .Include(c => c.Appointment)
            .ToListAsync(ct);

        var odontogramEntries = await _dbContext.OdontogramEntries
            .AsNoTracking()
            .Where(o => o.MedicalRecordId == record.RecordId && o.IsActive)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(ct);

        var apptIds = clinicalEntries.Where(c => c.AppointmentId.HasValue).Select(c => c.AppointmentId!.Value).Distinct().ToList();
        var diagnostics = await _dbContext.Diagnostics
            .AsNoTracking()
            .Include(d => d.AppointmentService)
            .Where(d => apptIds.Contains(d.AppointmentService.AppointmentId))
            .ToListAsync(ct);

        var oDtoList = odontogramEntries.Select(o => new OdontogramEntryDto
        {
            OdontogramEntryId = o.OdontogramId,
            ToothNumber = o.ToothNumber,
            Surface = o.Surface ?? "All",
            Condition = o.Condition,
            Note = o.DiagnosisNotes
        }).ToList();

        var clinicalDtoList = clinicalEntries
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ClinicalEntryDto
            {
                ClinicalEntryId = c.EntryId,
                MedicalRecordId = c.MedicalRecordId,
                AppointmentId = c.AppointmentId,
                AppointmentCode = c.AppointmentId.HasValue ? $"APT-{c.AppointmentId.Value:D6}" : null,
                DentistUserId = c.DentistUserId,
                DentistName = c.Dentist?.StaffProfile?.FullName ?? "Bác sĩ điều trị",
                Symptoms = c.ChiefComplaint,
                ClinicalFindings = c.ClinicalExamination,
                Diagnosis = c.Diagnosis ?? string.Empty,
                DentalConditions = c.TreatmentDone,
                ClinicalNotes = c.TreatmentDone,
                CreatedAt = c.CreatedAt,
                OdontogramEntries = oDtoList,
                ImagingRecords = diagnostics
                    .Where(d => c.AppointmentId.HasValue && d.AppointmentService.AppointmentId == c.AppointmentId.Value)
                    .Select(img => new ImagingRecordDto
                    {
                        ImagingRecordId = img.DiagnosticId,
                        ClinicalEntryId = c.EntryId,
                        ImagingType = img.ImagingType ?? "X-Ray",
                        FileUrl = img.FileUrl,
                        ResultSummary = img.Findings ?? img.ClinicalIndication,
                        CreatedAt = img.CreatedAt
                    }).ToList()
            }).ToList();

        var currentOdontogram = ComputeCurrentOdontogramFromList(odontogramEntries);

        var dto = new MedicalRecordDto
        {
            MedicalRecordId = record.RecordId,
            PatientUserId = patient.UserId,
            PatientCode = $"BN-{patient.UserId}",
            PatientName = patient.FullName,
            PatientPhone = patient.User?.PhoneNumber,
            Gender = patient.Gender,
            DateOfBirth = patient.DateOfBirth,
            Address = patient.Address,
            EmergencyContact = patient.EmergencyContact,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
            ClinicalEntries = clinicalDtoList,
            CurrentOdontogram = currentOdontogram
        };

        return ApiResponse<MedicalRecordDto>.Ok(dto);
    }

    public async Task<ApiResponse<ClinicalEntryDto>> CreateClinicalEntryAsync(long dentistUserId, CreateClinicalEntryRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Diagnosis))
        {
            return ApiResponse<ClinicalEntryDto>.Fail("Vui lòng nhập chẩn đoán lâm sàng của bệnh nhân.");
        }

        var patient = await _dbContext.PatientProfiles
            .FirstOrDefaultAsync(p => p.UserId == request.PatientUserId, ct);

        if (patient == null)
        {
            return ApiResponse<ClinicalEntryDto>.Fail("Không tìm thấy thông tin bệnh nhân.");
        }

        var dentist = await _dbContext.DentistProfiles
            .Include(d => d.StaffProfile)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null)
        {
            return ApiResponse<ClinicalEntryDto>.Fail("Không tìm thấy thông tin bác sĩ điều trị.");
        }

        var record = await _dbContext.DentalMedicalRecords
            .FirstOrDefaultAsync(r => r.PatientUserId == request.PatientUserId, ct);

        if (record == null)
        {
            record = new DentalMedicalRecord
            {
                PatientUserId = request.PatientUserId,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.DentalMedicalRecords.Add(record);
            await _dbContext.SaveChangesAsync(ct);
        }

        long? apptSvcId = null;
        if (request.AppointmentId.HasValue)
        {
            var svc = await _dbContext.AppointmentServices
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.AppointmentId == request.AppointmentId.Value, ct);
            apptSvcId = svc?.AppointmentServiceId;
        }

        var entry = new ClinicalEntry
        {
            MedicalRecordId = record.RecordId,
            PatientUserId = request.PatientUserId,
            AppointmentId = request.AppointmentId,
            AppointmentServiceId = apptSvcId,
            DentistUserId = dentistUserId,
            ChiefComplaint = request.Symptoms?.Trim(),
            ClinicalExamination = request.ClinicalFindings?.Trim(),
            Diagnosis = request.Diagnosis.Trim(),
            TreatmentDone = string.IsNullOrWhiteSpace(request.ClinicalNotes) ? request.DentalConditions?.Trim() : request.ClinicalNotes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ClinicalEntries.Add(entry);
        await _dbContext.SaveChangesAsync(ct);

        var createdTeeth = new List<OdontogramEntryDto>();
        if (request.ToothConditions != null && request.ToothConditions.Any())
        {
            foreach (var tooth in request.ToothConditions)
            {
                if (!string.IsNullOrWhiteSpace(tooth.ToothNumber))
                {
                    var oEntry = new OdontogramEntry
                    {
                        MedicalRecordId = record.RecordId,
                        DentistUserId = dentistUserId,
                        ToothNumber = tooth.ToothNumber.Trim(),
                        Surface = tooth.Surface?.Trim() ?? "All",
                        Condition = string.IsNullOrWhiteSpace(tooth.Condition) ? "Healthy" : tooth.Condition.Trim(),
                        DiagnosisNotes = tooth.Note?.Trim(),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.OdontogramEntries.Add(oEntry);
                    createdTeeth.Add(new OdontogramEntryDto
                    {
                        ToothNumber = oEntry.ToothNumber,
                        Surface = oEntry.Surface,
                        Condition = oEntry.Condition,
                        Note = oEntry.DiagnosisNotes
                    });
                }
            }
            await _dbContext.SaveChangesAsync(ct);
        }

        if (request.ImagingOrders != null && request.ImagingOrders.Any() && apptSvcId.HasValue)
        {
            var svc = await _dbContext.AppointmentServices.AsNoTracking().FirstOrDefaultAsync(s => s.AppointmentServiceId == apptSvcId.Value, ct);
            int deptSvcId = svc?.DepartmentServiceId ?? 1;
            foreach (var img in request.ImagingOrders)
            {
                if (!string.IsNullOrWhiteSpace(img.ImagingType))
                {
                    var diag = new Diagnostic
                    {
                        AppointmentServiceId = apptSvcId.Value,
                        DepartmentServiceId = deptSvcId,
                        OrderingDentistUserId = dentistUserId,
                        ClinicalIndication = img.ResultSummary?.Trim() ?? "Chỉ định chụp phim",
                        ImagingType = img.ImagingType.Trim(),
                        FileUrl = img.FileUrl?.Trim(),
                        Findings = img.ResultSummary?.Trim(),
                        Status = string.IsNullOrWhiteSpace(img.FileUrl) ? "Ordered" : "Completed",
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.Diagnostics.Add(diag);
                }
            }
            await _dbContext.SaveChangesAsync(ct);
        }

        if (request.AppointmentId.HasValue)
        {
            var appointment = await _dbContext.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId.Value, ct);

            if (appointment != null)
            {
                if (request.CompleteAppointment)
                {
                    if (appointment.Status != AppointmentStatus.Completed && appointment.Status != AppointmentStatus.Cancelled)
                    {
                        appointment.Status = AppointmentStatus.Completed;
                        appointment.CompletedAt = DateTime.UtcNow;
                        appointment.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
        }

        record.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        var savedEntry = await _dbContext.ClinicalEntries
            .Include(c => c.Appointment)
            .FirstOrDefaultAsync(c => c.EntryId == entry.EntryId, ct);

        var resultDto = new ClinicalEntryDto
        {
            ClinicalEntryId = savedEntry!.EntryId,
            MedicalRecordId = savedEntry.MedicalRecordId,
            AppointmentId = savedEntry.AppointmentId,
            AppointmentCode = savedEntry.AppointmentId.HasValue ? $"APT-{savedEntry.AppointmentId.Value:D6}" : null,
            DentistUserId = savedEntry.DentistUserId,
            DentistName = dentist.StaffProfile?.FullName ?? "Bác sĩ điều trị",
            Symptoms = savedEntry.ChiefComplaint,
            ClinicalFindings = savedEntry.ClinicalExamination,
            Diagnosis = savedEntry.Diagnosis ?? string.Empty,
            DentalConditions = savedEntry.TreatmentDone,
            ClinicalNotes = savedEntry.TreatmentDone,
            CreatedAt = savedEntry.CreatedAt,
            OdontogramEntries = createdTeeth
        };

        return ApiResponse<ClinicalEntryDto>.Ok(resultDto, "Đã ghi nhận chẩn đoán bệnh án điện tử thành công.");
    }

    public async Task<ApiResponse<List<PatientLookupDto>>> SearchPatientsForMedicalRecordAsync(string? keyword, CancellationToken ct = default)
    {
        var query = _dbContext.PatientProfiles
            .Include(p => p.User)
            .Include(p => p.DentalMedicalRecord)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.FullName.ToLower().Contains(k) ||
                (p.User.PhoneNumber != null && p.User.PhoneNumber.Contains(k)) ||
                (p.User.Email != null && p.User.Email.ToLower().Contains(k)));
        }

        var patients = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(30)
            .Select(p => new PatientLookupDto
            {
                PatientUserId = p.UserId,
                PatientCode = $"BN-{p.UserId}",
                FullName = p.FullName,
                PhoneNumber = p.User.PhoneNumber,
                Email = p.User.Email,
                DateOfBirth = p.DateOfBirth,
                Gender = p.Gender,
                HasMedicalRecord = p.DentalMedicalRecord != null
            })
            .ToListAsync(ct);

        return ApiResponse<List<PatientLookupDto>>.Ok(patients);
    }

    public async Task<ApiResponse<List<OdontogramEntryDto>>> GetCurrentOdontogramAsync(long patientUserId, CancellationToken ct = default)
    {
        var record = await _dbContext.DentalMedicalRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.PatientUserId == patientUserId, ct);

        if (record == null)
        {
            var emptyOdontogram = All32Teeth.Select(t => new OdontogramEntryDto
            {
                ToothNumber = t,
                Condition = "Healthy",
                Surface = "All"
            }).ToList();
            return ApiResponse<List<OdontogramEntryDto>>.Ok(emptyOdontogram);
        }

        var odontogramEntries = await _dbContext.OdontogramEntries
            .AsNoTracking()
            .Where(o => o.MedicalRecordId == record.RecordId && o.IsActive)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(ct);

        var result = ComputeCurrentOdontogramFromList(odontogramEntries);
        return ApiResponse<List<OdontogramEntryDto>>.Ok(result);
    }

    private static List<OdontogramEntryDto> ComputeCurrentOdontogramFromList(IEnumerable<OdontogramEntry> entries)
    {
        var toothLatestCondition = new Dictionary<string, OdontogramEntry>();

        foreach (var tooth in entries.OrderBy(e => e.CreatedAt))
        {
            toothLatestCondition[tooth.ToothNumber] = tooth;
        }

        var list = new List<OdontogramEntryDto>();
        foreach (var toothNum in All32Teeth)
        {
            if (toothLatestCondition.TryGetValue(toothNum, out var oEntry))
            {
                list.Add(new OdontogramEntryDto
                {
                    OdontogramEntryId = oEntry.OdontogramId,
                    ToothNumber = toothNum,
                    Surface = oEntry.Surface ?? "All",
                    Condition = oEntry.Condition,
                    Note = oEntry.DiagnosisNotes
                });
            }
            else
            {
                list.Add(new OdontogramEntryDto
                {
                    ToothNumber = toothNum,
                    Surface = "All",
                    Condition = "Healthy",
                    Note = null
                });
            }
        }

        return list;
    }

    public async Task<ApiResponse<ImagingRecordDto>> AddImagingRecordAsync(long dentistUserId, CreateImagingRecordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ImagingType))
        {
            return ApiResponse<ImagingRecordDto>.Fail("Vui lòng chọn hoặc nhập loại phim chụp / chẩn đoán hình ảnh.");
        }

        var entry = await _dbContext.ClinicalEntries
            .Include(c => c.MedicalRecord)
            .FirstOrDefaultAsync(c => c.EntryId == request.ClinicalEntryId, ct);

        if (entry == null)
        {
            return ApiResponse<ImagingRecordDto>.Fail("Không tìm thấy đợt khám lâm sàng tương ứng.");
        }

        long apptSvcId = entry.AppointmentServiceId ?? 0;
        int deptSvcId = 1;
        if (apptSvcId <= 0 && entry.AppointmentId.HasValue)
        {
            var svc = await _dbContext.AppointmentServices.AsNoTracking().FirstOrDefaultAsync(s => s.AppointmentId == entry.AppointmentId.Value, ct);
            if (svc != null)
            {
                apptSvcId = svc.AppointmentServiceId;
                deptSvcId = svc.DepartmentServiceId;
            }
        }
        else if (apptSvcId > 0)
        {
            var svc = await _dbContext.AppointmentServices.AsNoTracking().FirstOrDefaultAsync(s => s.AppointmentServiceId == apptSvcId, ct);
            if (svc != null) deptSvcId = svc.DepartmentServiceId;
        }

        if (apptSvcId <= 0)
        {
            var anySvc = await _dbContext.AppointmentServices.AsNoTracking().FirstOrDefaultAsync(ct);
            if (anySvc == null)
                return ApiResponse<ImagingRecordDto>.Fail("Không tìm thấy dịch vụ cuộc hẹn liên kết để lưu chẩn đoán hình ảnh.");
            apptSvcId = anySvc.AppointmentServiceId;
            deptSvcId = anySvc.DepartmentServiceId;
        }

        var diag = new Diagnostic
        {
            AppointmentServiceId = apptSvcId,
            DepartmentServiceId = deptSvcId,
            OrderingDentistUserId = dentistUserId > 0 ? dentistUserId : entry.DentistUserId,
            ClinicalIndication = request.ResultSummary?.Trim() ?? "Chẩn đoán hình ảnh",
            ImagingType = request.ImagingType.Trim(),
            FileUrl = request.FileUrl?.Trim(),
            Findings = request.ResultSummary?.Trim(),
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        _dbContext.Diagnostics.Add(diag);
        entry.MedicalRecord.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        var dto = new ImagingRecordDto
        {
            ImagingRecordId = diag.DiagnosticId,
            ClinicalEntryId = entry.EntryId,
            ImagingType = diag.ImagingType,
            FileUrl = diag.FileUrl,
            ResultSummary = diag.Findings,
            CreatedAt = diag.CreatedAt
        };

        return ApiResponse<ImagingRecordDto>.Ok(dto, "Lưu thông tin phim chụp X-quang thành công.");
    }

    public Task<ApiResponse<ImagingRecordMetadataDto>> UploadImagingRecordAsync(
        long currentUserId,
        UploadImagingRecordRequest request,
        CancellationToken ct = default)
    {
        return UploadImagingRecordAsync(currentUserId, null, request, ct);
    }

    public async Task<ApiResponse<ImagingRecordMetadataDto>> UploadImagingRecordAsync(
        long currentUserId,
        string? currentUserRole,
        UploadImagingRecordRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail("Dữ liệu yêu cầu tải lên không được để trống.");
        }

        var requestValidator = new UploadImagingRecordRequestValidator();
        var valResult = await requestValidator.ValidateAsync(request, ct);
        if (!valResult.IsValid)
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail(
                valResult.Errors.First().ErrorMessage,
                valResult.Errors.Select(e => e.ErrorMessage).ToList());
        }

        if (!DentalImagingType.IsValid(request.ImagingType))
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail(
                "Loại phim chụp không hợp lệ. Chỉ chấp nhận 4 loại: Panorama, Periapical, Cephalometric, Intraoral.");
        }

        var normalizedType = DentalImagingType.Normalize(request.ImagingType) ?? request.ImagingType.Trim();

        var fileValidation = _fileValidator.Validate(request.File);
        if (!fileValidation.IsValid)
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail(fileValidation.ErrorMessage ?? "Tập tin hình ảnh không hợp lệ.");
        }

        var entry = await _dbContext.ClinicalEntries
            .Include(c => c.MedicalRecord)
            .Include(c => c.Appointment)
                .ThenInclude(a => a!.AppointmentServices)
            .FirstOrDefaultAsync(c => c.EntryId == request.ClinicalEntryId, ct);

        if (entry == null)
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail("Không tìm thấy đợt khám lâm sàng tương ứng.");
        }

        if (currentUserId <= 0)
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail("Người dùng chưa đăng nhập hoặc danh tính không hợp lệ.");
        }

        if (string.Equals(currentUserRole, UserRole.Dentist, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(currentUserRole))
        {
            var isAssignedDentist = entry.DentistUserId == currentUserId ||
                (entry.Appointment != null && entry.Appointment.AppointmentServices.Any(s => s.AssignedDentistUserId == currentUserId));

            if (!isAssignedDentist)
            {
                return ApiResponse<ImagingRecordMetadataDto>.Fail(
                    "Bác sĩ không có quyền thao tác tải lên phim chụp cho đợt khám của bác sĩ khác.");
            }
        }
        else if (!string.Equals(currentUserRole, UserRole.SystemAdministrator, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail(
                "Bạn không có quyền thực hiện thao tác tải lên hình ảnh y tế.");
        }

        if (_imagingStorageService == null)
        {
            _logger.LogError("Image storage service is not configured or resolved.");
            return ApiResponse<ImagingRecordMetadataDto>.Fail("Dịch vụ lưu trữ hình ảnh chưa sẵn sàng.");
        }

        DentalImageUploadResult uploadResult;
        try
        {
            var subFolder = $"entry_{entry.EntryId}";
            uploadResult = await _imagingStorageService.UploadDentalImageAsync(request.File, subFolder, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload dental imaging file to storage for ClinicalEntry {EntryId}", entry.EntryId);
            return ApiResponse<ImagingRecordMetadataDto>.Fail($"Tải hình ảnh lên hệ thống lưu trữ thất bại: {ex.Message}");
        }

        if (uploadResult == null || string.IsNullOrWhiteSpace(uploadResult.Url))
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail("Không nhận được thông tin tệp tải lên từ hệ thống lưu trữ.");
        }

        long apptSvcId = entry.AppointmentServiceId ?? entry.Appointment?.AppointmentServices.FirstOrDefault()?.AppointmentServiceId ?? 0;
        int deptSvcId = entry.Appointment?.AppointmentServices.FirstOrDefault()?.DepartmentServiceId ?? 1;
        if (apptSvcId <= 0)
        {
            var fallbackSvc = await _dbContext.AppointmentServices.AsNoTracking().FirstOrDefaultAsync(ct);
            apptSvcId = fallbackSvc?.AppointmentServiceId ?? 1;
            deptSvcId = fallbackSvc?.DepartmentServiceId ?? 1;
        }

        var diag = new Diagnostic
        {
            AppointmentServiceId = apptSvcId,
            DepartmentServiceId = deptSvcId,
            OrderingDentistUserId = entry.DentistUserId,
            UploadedByUserId = currentUserId,
            ClinicalIndication = string.IsNullOrWhiteSpace(request.ResultSummary) ? $"[Entry:{entry.EntryId}] Chụp phim" : request.ResultSummary.Trim(),
            ImagingType = normalizedType,
            FileUrl = uploadResult.Url,
            Findings = string.IsNullOrWhiteSpace(request.ResultSummary) ? null : request.ResultSummary.Trim(),
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        try
        {
            _dbContext.Diagnostics.Add(diag);
            if (entry.MedicalRecord != null)
            {
                entry.MedicalRecord.UpdatedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception dbEx)
        {
            _logger.LogError(dbEx, "Database save failed for Diagnostic in ClinicalEntry {EntryId}. Cleaning up uploaded storage asset {PublicId}.",
                entry.EntryId, uploadResult.PublicId);

            if (!string.IsNullOrEmpty(uploadResult.PublicId))
            {
                try
                {
                    await _imagingStorageService.DeleteDentalImageAsync(uploadResult.PublicId, CancellationToken.None);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to rollback/delete storage asset {PublicId} after database save error.", uploadResult.PublicId);
                }
            }

            return ApiResponse<ImagingRecordMetadataDto>.Fail("Lưu thông tin hồ sơ hình ảnh vào cơ sở dữ liệu thất bại.");
        }

        var responseDto = new ImagingRecordMetadataDto
        {
            ImagingRecordId = diag.DiagnosticId,
            ClinicalEntryId = entry.EntryId,
            ImagingType = diag.ImagingType,
            FileUrl = diag.FileUrl,
            ResultSummary = diag.Findings,
            CreatedAt = diag.CreatedAt,
            OriginalFileName = Path.GetFileName(request.File.FileName),
            MimeType = request.File.ContentType,
            FileSizeBytes = uploadResult.FileSizeBytes,
            StorageProvider = uploadResult.StorageProvider
        };

        return ApiResponse<ImagingRecordMetadataDto>.Ok(responseDto, "Tải lên và lưu hồ sơ hình ảnh X-quang thành công.");
    }

    public Task<ApiResponse<List<ImagingRecordDto>>> GetImagingRecordsByClinicalEntryIdAsync(
        long clinicalEntryId,
        CancellationToken ct = default)
    {
        return GetImagingRecordsByClinicalEntryIdAsync(0, null, clinicalEntryId, ct);
    }

    public async Task<ApiResponse<List<ImagingRecordDto>>> GetImagingRecordsByClinicalEntryIdAsync(
        long currentUserId,
        string? currentUserRole,
        long clinicalEntryId,
        CancellationToken ct = default)
    {
        if (clinicalEntryId <= 0)
        {
            return ApiResponse<List<ImagingRecordDto>>.Fail("Mã đợt khám (ClinicalEntryId) không hợp lệ.");
        }

        var entry = await _dbContext.ClinicalEntries
            .Include(c => c.MedicalRecord)
            .Include(c => c.Appointment)
                .ThenInclude(a => a!.AppointmentServices)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.EntryId == clinicalEntryId, ct);

        if (entry == null)
        {
            return ApiResponse<List<ImagingRecordDto>>.Fail("Không tìm thấy đợt khám lâm sàng tương ứng.");
        }

        if (currentUserId > 0 || !string.IsNullOrEmpty(currentUserRole))
        {
            if (currentUserId <= 0)
            {
                return ApiResponse<List<ImagingRecordDto>>.Fail("Người dùng chưa đăng nhập hoặc danh tính không hợp lệ.");
            }

            if (string.Equals(currentUserRole, UserRole.Dentist, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(currentUserRole))
            {
                var isAssignedDentist = entry.DentistUserId == currentUserId ||
                    (entry.Appointment != null && entry.Appointment.AppointmentServices.Any(s => s.AssignedDentistUserId == currentUserId));

                if (!isAssignedDentist)
                {
                    return ApiResponse<List<ImagingRecordDto>>.Fail(
                        "Bác sĩ không có quyền thao tác truy cập hình ảnh của đợt khám của bác sĩ khác.");
                }
            }
            else if (string.Equals(currentUserRole, UserRole.Patient, StringComparison.OrdinalIgnoreCase))
            {
                if (entry.MedicalRecord == null || entry.MedicalRecord.PatientUserId != currentUserId)
                {
                    return ApiResponse<List<ImagingRecordDto>>.Fail(
                        "Bệnh nhân không có quyền truy cập hồ sơ hình ảnh của bệnh nhân khác.");
                }
            }
            else if (!string.Equals(currentUserRole, UserRole.SystemAdministrator, StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<List<ImagingRecordDto>>.Fail(
                    "Bạn không có quyền truy cập danh sách hình ảnh y tế.");
            }
        }

        var apptId = entry.AppointmentId;
        var images = await _dbContext.Diagnostics
            .AsNoTracking()
            .Include(d => d.AppointmentService)
            .Where(img => (entry.AppointmentServiceId.HasValue && img.AppointmentServiceId == entry.AppointmentServiceId.Value) ||
                          (apptId.HasValue && img.AppointmentService.AppointmentId == apptId.Value) ||
                          (img.FileUrl != null && img.FileUrl.Contains($"entry_{clinicalEntryId}/")))
            .OrderByDescending(img => img.CreatedAt)
            .Select(img => new ImagingRecordDto
            {
                ImagingRecordId = img.DiagnosticId,
                ClinicalEntryId = clinicalEntryId,
                ImagingType = img.ImagingType ?? "X-Ray",
                FileUrl = img.FileUrl,
                ViewUrl = $"/api/medical-records/imaging/{img.DiagnosticId}/content",
                ResultSummary = img.Findings,
                CreatedAt = img.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<ImagingRecordDto>>.Ok(images);
    }

    public async Task<ApiResponse<ImagingRecordDto>> GetImagingRecordByIdAsync(
        long currentUserId,
        string? currentUserRole,
        long imagingRecordId,
        CancellationToken ct = default)
    {
        if (imagingRecordId <= 0)
        {
            return ApiResponse<ImagingRecordDto>.Fail("Mã bản ghi hình ảnh không hợp lệ.");
        }

        if (currentUserId <= 0)
        {
            return ApiResponse<ImagingRecordDto>.Fail("Người dùng chưa đăng nhập hoặc danh tính không hợp lệ.");
        }

        var img = await _dbContext.Diagnostics
            .Include(i => i.AppointmentService)
                .ThenInclude(s => s.Appointment)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.DiagnosticId == imagingRecordId, ct);

        if (img == null)
        {
            return ApiResponse<ImagingRecordDto>.Fail("Không tìm thấy bản ghi hình ảnh tương ứng.");
        }

        if (string.Equals(currentUserRole, UserRole.Dentist, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(currentUserRole))
        {
            var isAssignedDentist = img.OrderingDentistUserId == currentUserId ||
                img.AppointmentService?.AssignedDentistUserId == currentUserId;

            if (!isAssignedDentist)
            {
                return ApiResponse<ImagingRecordDto>.Fail(
                    "Bác sĩ không có quyền thao tác truy cập hình ảnh của đợt khám của bác sĩ khác.");
            }
        }
        else if (string.Equals(currentUserRole, UserRole.Patient, StringComparison.OrdinalIgnoreCase))
        {
            if (img.AppointmentService?.Appointment?.PatientUserId != currentUserId)
            {
                return ApiResponse<ImagingRecordDto>.Fail(
                    "Bệnh nhân không có quyền truy cập hồ sơ hình ảnh của bệnh nhân khác.");
            }
        }
        else if (!string.Equals(currentUserRole, UserRole.SystemAdministrator, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<ImagingRecordDto>.Fail(
                "Bạn không có quyền truy cập hồ sơ hình ảnh y tế.");
        }

        var dto = new ImagingRecordDto
        {
            ImagingRecordId = img.DiagnosticId,
            ClinicalEntryId = img.AppointmentServiceId,
            ImagingType = img.ImagingType ?? "X-Ray",
            FileUrl = img.FileUrl,
            ViewUrl = $"/api/medical-records/imaging/{img.DiagnosticId}/content",
            ResultSummary = img.Findings,
            CreatedAt = img.CreatedAt
        };

        return ApiResponse<ImagingRecordDto>.Ok(dto);
    }

    public async Task<ApiResponse<DentalImageStreamResult>> GetImagingFileStreamAsync(
        long currentUserId,
        string? currentUserRole,
        long imagingRecordId,
        CancellationToken ct = default)
    {
        if (imagingRecordId <= 0)
        {
            return ApiResponse<DentalImageStreamResult>.Fail("Mã bản ghi hình ảnh không hợp lệ.");
        }

        if (currentUserId <= 0)
        {
            return ApiResponse<DentalImageStreamResult>.Fail("Người dùng chưa đăng nhập hoặc danh tính không hợp lệ.");
        }

        var img = await _dbContext.Diagnostics
            .Include(i => i.AppointmentService)
                .ThenInclude(s => s.Appointment)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.DiagnosticId == imagingRecordId, ct);

        if (img == null)
        {
            return ApiResponse<DentalImageStreamResult>.Fail("Không tìm thấy bản ghi hình ảnh tương ứng.");
        }

        if (string.Equals(currentUserRole, UserRole.Dentist, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(currentUserRole))
        {
            var isAssignedDentist = img.OrderingDentistUserId == currentUserId ||
                img.AppointmentService?.AssignedDentistUserId == currentUserId;

            if (!isAssignedDentist)
            {
                return ApiResponse<DentalImageStreamResult>.Fail(
                    "Bác sĩ không có quyền thao tác truy cập hình ảnh của đợt khám của bác sĩ khác.");
            }
        }
        else if (string.Equals(currentUserRole, UserRole.Patient, StringComparison.OrdinalIgnoreCase))
        {
            if (img.AppointmentService?.Appointment?.PatientUserId != currentUserId)
            {
                return ApiResponse<DentalImageStreamResult>.Fail(
                    "Bệnh nhân không có quyền truy cập hồ sơ hình ảnh của bệnh nhân khác.");
            }
        }
        else if (!string.Equals(currentUserRole, UserRole.SystemAdministrator, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<DentalImageStreamResult>.Fail(
                "Bạn không có quyền truy cập hồ sơ hình ảnh y tế.");
        }

        if (string.IsNullOrWhiteSpace(img.FileUrl))
        {
            return ApiResponse<DentalImageStreamResult>.Fail("Bản ghi hình ảnh không có liên kết tệp hợp lệ.");
        }

        if (_imagingStorageService == null)
        {
            return ApiResponse<DentalImageStreamResult>.Fail("Dịch vụ lưu trữ hình ảnh chưa sẵn sàng.");
        }

        var stream = await _imagingStorageService.GetImageStreamAsync(img.FileUrl, ct);
        if (stream == null)
        {
            return ApiResponse<DentalImageStreamResult>.Fail("Không thể tải luồng dữ liệu hình ảnh từ hệ thống lưu trữ.");
        }

        var ext = Path.GetExtension(img.FileUrl.Split('?')[0]).ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".dcm" or ".dicom" => "application/dicom",
            _ => "application/octet-stream"
        };
        var fileName = $"imaging_{img.DiagnosticId}_{(img.ImagingType ?? "xray").ToLowerInvariant()}{ext}";

        return ApiResponse<DentalImageStreamResult>.Ok(new DentalImageStreamResult
        {
            Stream = stream,
            ContentType = contentType,
            FileName = fileName
        });
    }

    public Task<ApiResponse<bool>> DeleteImagingRecordAsync(long currentUserId, long imagingRecordId, CancellationToken ct = default)
    {
        return DeleteImagingRecordAsync(currentUserId, null, imagingRecordId, ct);
    }

    public async Task<ApiResponse<bool>> DeleteImagingRecordAsync(
        long currentUserId,
        string? currentUserRole,
        long imagingRecordId,
        CancellationToken ct = default)
    {
        if (imagingRecordId <= 0)
        {
            return ApiResponse<bool>.Fail("Mã bản ghi hình ảnh không hợp lệ.");
        }

        if (currentUserId <= 0)
        {
            return ApiResponse<bool>.Fail("Người dùng chưa đăng nhập hoặc danh tính không hợp lệ.");
        }

        var img = await _dbContext.Diagnostics
            .Include(i => i.AppointmentService)
                .ThenInclude(s => s.Appointment)
            .FirstOrDefaultAsync(i => i.DiagnosticId == imagingRecordId, ct);

        if (img == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy bản ghi hình ảnh tương ứng.");
        }

        if (string.Equals(currentUserRole, UserRole.Patient, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<bool>.Fail("Bệnh nhân không có quyền xóa hình ảnh bệnh án.");
        }

        if (string.Equals(currentUserRole, UserRole.Dentist, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(currentUserRole))
        {
            var isAssignedDentist = img.OrderingDentistUserId == currentUserId ||
                img.AppointmentService?.AssignedDentistUserId == currentUserId;

            if (!isAssignedDentist)
            {
                return ApiResponse<bool>.Fail("Bác sĩ không có quyền thao tác xóa hình ảnh của đợt khám của bác sĩ khác.");
            }
        }
        else if (!string.Equals(currentUserRole, UserRole.SystemAdministrator, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền thực hiện thao tác xóa hình ảnh y tế.");
        }

        _dbContext.Diagnostics.Remove(img);
        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Đã xóa bản ghi hình ảnh y tế thành công.");
    }

    public static bool TryExtractDentalImagingPublicId(string? fileUrl, long clinicalEntryId, out string? publicId)
    {
        publicId = null;
        if (string.IsNullOrWhiteSpace(fileUrl) || clinicalEntryId <= 0)
        {
            return false;
        }

        var localPrefix = $"local://medical-imaging/entry_{clinicalEntryId}/";
        if (fileUrl.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
        {
            publicId = fileUrl;
            return true;
        }

        var expectedPrefix = $"dental-clinic/medical-imaging/entry_{clinicalEntryId}/";
        var index = fileUrl.IndexOf(expectedPrefix, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var path = fileUrl.Substring(index);
            var qIdx = path.IndexOf('?');
            if (qIdx >= 0)
            {
                path = path.Substring(0, qIdx);
            }

            var lastSlash = path.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                var fileName = path.Substring(lastSlash + 1);
                var dotIdx = fileName.LastIndexOf('.');
                if (dotIdx > 0)
                {
                    path = path.Substring(0, lastSlash + 1 + dotIdx);
                }
            }

            publicId = path;
            return true;
        }

        return false;
    }
}

internal class MedicalImagingStorageServiceAdapter : IMedicalImagingStorageService
{
    private readonly IImageStorageService _inner;

    public MedicalImagingStorageServiceAdapter(IImageStorageService inner)
    {
        _inner = inner;
    }

    public string StorageProviderName => "Adapter";

    public bool IsLocalReference(string? fileUrl) =>
        fileUrl != null && fileUrl.StartsWith(LocalImageStorageService.SchemePrefix, StringComparison.OrdinalIgnoreCase);

    public Task<DentalImageUploadResult> UploadDentalImageAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        string? subFolder = null,
        CancellationToken ct = default)
        => _inner.UploadDentalImageAsync(fileStream, originalFileName, contentType, subFolder, ct);

    public Task<DentalImageUploadResult> UploadDentalImageAsync(
        DentalImageFileInput file,
        string? subFolder = null,
        CancellationToken ct = default)
        => _inner.UploadDentalImageAsync(file, subFolder, ct);

    public Task<bool> DeleteDentalImageAsync(string fileReferenceOrPublicId, CancellationToken ct = default)
        => _inner.DeleteDentalImageAsync(fileReferenceOrPublicId, ct);

    public Task<Stream?> GetImageStreamAsync(string fileUrl, CancellationToken ct = default)
        => _inner.GetImageStreamAsync(fileUrl, ct);
}
