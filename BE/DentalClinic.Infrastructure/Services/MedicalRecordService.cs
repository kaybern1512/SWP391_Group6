using System;
using System.Collections.Generic;
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
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class MedicalRecordService : IMedicalRecordService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IImageStorageService? _imageStorageService;
    private readonly IDentalImageFileValidator _fileValidator;
    private readonly ILogger<MedicalRecordService> _logger;

    private static readonly string[] All32Teeth = new[]
    {
        // Hàm trên (Maxillary): Phải sang Trái (18 -> 11, 21 -> 28)
        "18", "17", "16", "15", "14", "13", "12", "11",
        "21", "22", "23", "24", "25", "26", "27", "28",
        // Hàm dưới (Mandibular): Trái sang Phải (38 -> 31, 41 -> 48)
        "48", "47", "46", "45", "44", "43", "42", "41",
        "31", "32", "33", "34", "35", "36", "37", "38"
    };

    public MedicalRecordService(
        DentalClinicDbContext dbContext,
        ILogger<MedicalRecordService> logger)
        : this(dbContext, null, null, logger)
    {
    }

    public MedicalRecordService(
        DentalClinicDbContext dbContext,
        IImageStorageService? imageStorageService,
        IDentalImageFileValidator? fileValidator,
        ILogger<MedicalRecordService> logger)
    {
        _dbContext = dbContext;
        _imageStorageService = imageStorageService;
        _fileValidator = fileValidator ?? new DentalImageFileValidator();
        _logger = logger;
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
            .Include(r => r.ClinicalEntries)
                .ThenInclude(c => c.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(r => r.ClinicalEntries)
                .ThenInclude(c => c.Appointment)
            .Include(r => r.ClinicalEntries)
                .ThenInclude(c => c.OdontogramEntries)
            .Include(r => r.ClinicalEntries)
                .ThenInclude(c => c.ImagingRecords)
            .FirstOrDefaultAsync(r => r.PatientUserId == patientUserId, ct);

        if (record == null)
        {
            // Tự động khởi tạo hồ sơ bệnh án điện tử nếu chưa có
            record = new DentalMedicalRecord
            {
                PatientUserId = patientUserId,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.DentalMedicalRecords.Add(record);
            await _dbContext.SaveChangesAsync(ct);
        }

        var clinicalDtoList = record.ClinicalEntries
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ClinicalEntryDto
            {
                ClinicalEntryId = c.ClinicalEntryId,
                MedicalRecordId = c.MedicalRecordId,
                AppointmentId = c.AppointmentId,
                AppointmentCode = c.Appointment?.AppointmentCode,
                DentistUserId = c.DentistUserId,
                DentistName = c.DentistUser?.User?.FullName ?? "Bác sĩ điều trị",
                Symptoms = c.Symptoms,
                ClinicalFindings = c.ClinicalFindings,
                Diagnosis = c.Diagnosis,
                DentalConditions = c.DentalConditions,
                ClinicalNotes = c.ClinicalNotes,
                CreatedAt = c.CreatedAt,
                OdontogramEntries = c.OdontogramEntries.Select(o => new OdontogramEntryDto
                {
                    OdontogramEntryId = o.OdontogramEntryId,
                    ToothNumber = o.ToothNumber,
                    Surface = o.Surface,
                    Condition = o.Condition,
                    Note = o.Note
                }).ToList(),
                ImagingRecords = c.ImagingRecords.Select(img => new ImagingRecordDto
                {
                    ImagingRecordId = img.ImagingRecordId,
                    ClinicalEntryId = img.ClinicalEntryId,
                    ImagingType = img.ImagingType,
                    FileUrl = img.FileUrl,
                    ResultSummary = img.ResultSummary,
                    CreatedAt = img.CreatedAt
                }).ToList()
            }).ToList();

        // Tính toán trạng thái hiện thời của 32 răng (lấy trạng thái mới nhất ghi nhận cho mỗi răng)
        var currentOdontogram = ComputeCurrentOdontogram(record.ClinicalEntries);

        var dto = new MedicalRecordDto
        {
            MedicalRecordId = record.MedicalRecordId,
            PatientUserId = patient.UserId,
            PatientCode = patient.PatientCode,
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
            .Include(d => d.User)
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

        var entry = new ClinicalEntry
        {
            MedicalRecordId = record.MedicalRecordId,
            AppointmentId = request.AppointmentId,
            DentistUserId = dentistUserId,
            Symptoms = request.Symptoms?.Trim(),
            ClinicalFindings = request.ClinicalFindings?.Trim(),
            Diagnosis = request.Diagnosis.Trim(),
            DentalConditions = request.DentalConditions?.Trim(),
            ClinicalNotes = request.ClinicalNotes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ClinicalEntries.Add(entry);
        await _dbContext.SaveChangesAsync(ct);

        // Lưu các răng có chẩn đoán/thay đổi trạng thái
        if (request.ToothConditions != null && request.ToothConditions.Any())
        {
            foreach (var tooth in request.ToothConditions)
            {
                if (!string.IsNullOrWhiteSpace(tooth.ToothNumber))
                {
                    var oEntry = new OdontogramEntry
                    {
                        ClinicalEntryId = entry.ClinicalEntryId,
                        ToothNumber = tooth.ToothNumber.Trim(),
                        Surface = tooth.Surface?.Trim() ?? "All",
                        Condition = string.IsNullOrWhiteSpace(tooth.Condition) ? "Healthy" : tooth.Condition.Trim(),
                        Note = tooth.Note?.Trim()
                    };
                    _dbContext.OdontogramEntries.Add(oEntry);
                }
            }
            await _dbContext.SaveChangesAsync(ct);
        }

        // Lưu chỉ định chụp X-quang / chẩn đoán hình ảnh nếu có
        if (request.ImagingOrders != null && request.ImagingOrders.Any())
        {
            foreach (var img in request.ImagingOrders)
            {
                if (!string.IsNullOrWhiteSpace(img.ImagingType))
                {
                    var imgRecord = new ImagingRecord
                    {
                        ClinicalEntryId = entry.ClinicalEntryId,
                        ImagingType = img.ImagingType.Trim(),
                        FileUrl = img.FileUrl?.Trim(),
                        ResultSummary = img.ResultSummary?.Trim(),
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.ImagingRecords.Add(imgRecord);
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
                        var oldStatus = appointment.Status;
                        appointment.Status = AppointmentStatus.Completed;
                        appointment.CompletedAt = DateTime.UtcNow;

                        _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                        {
                            AppointmentId = appointment.AppointmentId,
                            OldStatus = oldStatus,
                            NewStatus = AppointmentStatus.Completed,
                            ChangedByUserId = dentistUserId,
                            Note = "Bác sĩ hoàn tất khám và lưu chẩn đoán bệnh án.",
                            ChangedAt = DateTime.UtcNow
                        });
                    }
                }
                else
                {
                    _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                    {
                        AppointmentId = appointment.AppointmentId,
                        OldStatus = appointment.Status,
                        NewStatus = appointment.Status,
                        ChangedByUserId = dentistUserId,
                        Note = "Bác sĩ ghi nhận chẩn đoán lâm sàng & sơ đồ răng bệnh án.",
                        ChangedAt = DateTime.UtcNow
                    });
                }
            }
        }

        record.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        var savedEntry = await _dbContext.ClinicalEntries
            .Include(c => c.DentistUser)
            .Include(c => c.Appointment)
            .Include(c => c.OdontogramEntries)
            .Include(c => c.ImagingRecords)
            .FirstOrDefaultAsync(c => c.ClinicalEntryId == entry.ClinicalEntryId, ct);

        var resultDto = new ClinicalEntryDto
        {
            ClinicalEntryId = savedEntry!.ClinicalEntryId,
            MedicalRecordId = savedEntry.MedicalRecordId,
            AppointmentId = savedEntry.AppointmentId,
            AppointmentCode = savedEntry.Appointment?.AppointmentCode,
            DentistUserId = savedEntry.DentistUserId,
            DentistName = dentist.User?.FullName ?? "Bác sĩ điều trị",
            Symptoms = savedEntry.Symptoms,
            ClinicalFindings = savedEntry.ClinicalFindings,
            Diagnosis = savedEntry.Diagnosis,
            DentalConditions = savedEntry.DentalConditions,
            ClinicalNotes = savedEntry.ClinicalNotes,
            CreatedAt = savedEntry.CreatedAt,
            OdontogramEntries = savedEntry.OdontogramEntries.Select(o => new OdontogramEntryDto
            {
                OdontogramEntryId = o.OdontogramEntryId,
                ToothNumber = o.ToothNumber,
                Surface = o.Surface,
                Condition = o.Condition,
                Note = o.Note
            }).ToList()
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
                p.PatientCode.ToLower().Contains(k) ||
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
                PatientCode = p.PatientCode,
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
            .Include(r => r.ClinicalEntries)
                .ThenInclude(c => c.OdontogramEntries)
            .FirstOrDefaultAsync(r => r.PatientUserId == patientUserId, ct);

        if (record == null)
        {
            // Trả về mặc định 32 răng khỏe mạnh
            var emptyOdontogram = All32Teeth.Select(t => new OdontogramEntryDto
            {
                ToothNumber = t,
                Condition = "Healthy",
                Surface = "All"
            }).ToList();
            return ApiResponse<List<OdontogramEntryDto>>.Ok(emptyOdontogram);
        }

        var result = ComputeCurrentOdontogram(record.ClinicalEntries);
        return ApiResponse<List<OdontogramEntryDto>>.Ok(result);
    }

    private static List<OdontogramEntryDto> ComputeCurrentOdontogram(IEnumerable<ClinicalEntry> entries)
    {
        var toothLatestCondition = new Dictionary<string, OdontogramEntry>();

        // Duyệt các lần khám từ cũ đến mới để lần khám mới nhất ghi đè trạng thái răng
        foreach (var entry in entries.OrderBy(e => e.CreatedAt))
        {
            foreach (var tooth in entry.OdontogramEntries)
            {
                toothLatestCondition[tooth.ToothNumber] = tooth;
            }
        }

        var list = new List<OdontogramEntryDto>();
        foreach (var toothNum in All32Teeth)
        {
            if (toothLatestCondition.TryGetValue(toothNum, out var oEntry))
            {
                list.Add(new OdontogramEntryDto
                {
                    OdontogramEntryId = oEntry.OdontogramEntryId,
                    ToothNumber = toothNum,
                    Surface = oEntry.Surface,
                    Condition = oEntry.Condition,
                    Note = oEntry.Note
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
            .FirstOrDefaultAsync(c => c.ClinicalEntryId == request.ClinicalEntryId, ct);

        if (entry == null)
        {
            return ApiResponse<ImagingRecordDto>.Fail("Không tìm thấy đợt khám lâm sàng tương ứng.");
        }

        var imagingRecord = new ImagingRecord
        {
            ClinicalEntryId = request.ClinicalEntryId,
            ImagingType = request.ImagingType.Trim(),
            FileUrl = request.FileUrl?.Trim(),
            ResultSummary = request.ResultSummary?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ImagingRecords.Add(imagingRecord);
        entry.MedicalRecord.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        var dto = new ImagingRecordDto
        {
            ImagingRecordId = imagingRecord.ImagingRecordId,
            ClinicalEntryId = imagingRecord.ClinicalEntryId,
            ImagingType = imagingRecord.ImagingType,
            FileUrl = imagingRecord.FileUrl,
            ResultSummary = imagingRecord.ResultSummary,
            CreatedAt = imagingRecord.CreatedAt
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

        // 1. Validate request structure and rules via Day 01 validator
        var requestValidator = new UploadImagingRecordRequestValidator();
        var valResult = await requestValidator.ValidateAsync(request, ct);
        if (!valResult.IsValid)
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail(
                valResult.Errors.First().ErrorMessage,
                valResult.Errors.Select(e => e.ErrorMessage).ToList());
        }

        // 2. Strict classification check against supported dental imaging types
        if (!DentalImagingType.IsValid(request.ImagingType))
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail(
                "Loại phim chụp không hợp lệ. Chỉ chấp nhận 4 loại: Panorama, Periapical, Cephalometric, Intraoral.");
        }

        var normalizedType = DentalImagingType.Normalize(request.ImagingType) ?? request.ImagingType.Trim();

        // 3. Secure file validation (size, extension, mime, binary signatures)
        var fileValidation = _fileValidator.Validate(request.File);
        if (!fileValidation.IsValid)
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail(fileValidation.ErrorMessage ?? "Tập tin hình ảnh không hợp lệ.");
        }

        // 4. Verify ClinicalEntry exists
        var entry = await _dbContext.ClinicalEntries
            .Include(c => c.MedicalRecord)
            .Include(c => c.Appointment)
            .FirstOrDefaultAsync(c => c.ClinicalEntryId == request.ClinicalEntryId, ct);

        if (entry == null)
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail("Không tìm thấy đợt khám lâm sàng tương ứng.");
        }

        // 5. Authorization & access control: dentist must be assigned to this clinical entry/appointment
        if (currentUserId <= 0)
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail("Người dùng chưa đăng nhập hoặc danh tính không hợp lệ.");
        }

        if (string.Equals(currentUserRole, UserRole.Dentist, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(currentUserRole))
        {
            var isAssignedDentist = entry.DentistUserId == currentUserId ||
                (entry.Appointment != null && entry.Appointment.AssignedDentistUserId == currentUserId);

            if (!isAssignedDentist)
            {
                return ApiResponse<ImagingRecordMetadataDto>.Fail(
                    "Bác sĩ không có quyền thao tác tải lên phim chụp cho đợt khám của bác sĩ khác.");
            }
        }
        else if (!string.Equals(currentUserRole, UserRole.SystemAdministrator, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(currentUserRole, UserRole.DepartmentManager, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail(
                "Bạn không có quyền thực hiện thao tác tải lên hình ảnh y tế.");
        }

        // 6. Upload file through storage service
        if (_imageStorageService == null)
        {
            _logger.LogError("Image storage service is not configured or resolved.");
            return ApiResponse<ImagingRecordMetadataDto>.Fail("Dịch vụ lưu trữ hình ảnh chưa sẵn sàng.");
        }

        DentalImageUploadResult uploadResult;
        try
        {
            var subFolder = $"entry_{entry.ClinicalEntryId}";
            uploadResult = await _imageStorageService.UploadDentalImageAsync(request.File, subFolder, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload dental imaging file to storage for ClinicalEntry {EntryId}", entry.ClinicalEntryId);
            return ApiResponse<ImagingRecordMetadataDto>.Fail($"Tải hình ảnh lên hệ thống lưu trữ thất bại: {ex.Message}");
        }

        if (uploadResult == null || string.IsNullOrWhiteSpace(uploadResult.Url))
        {
            return ApiResponse<ImagingRecordMetadataDto>.Fail("Không nhận được thông tin tệp tải lên từ hệ thống lưu trữ.");
        }

        // 7. Save record into database (ImagingRecords) with rollback cleanup on error
        var imagingRecord = new ImagingRecord
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = normalizedType,
            FileUrl = uploadResult.Url,
            ResultSummary = string.IsNullOrWhiteSpace(request.ResultSummary) ? null : request.ResultSummary.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _dbContext.ImagingRecords.Add(imagingRecord);
            if (entry.MedicalRecord != null)
            {
                entry.MedicalRecord.UpdatedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception dbEx)
        {
            _logger.LogError(dbEx, "Database save failed for ImagingRecord in ClinicalEntry {EntryId}. Cleaning up uploaded storage asset {PublicId}.",
                entry.ClinicalEntryId, uploadResult.PublicId);

            // Clean up uploaded file to prevent orphaned storage assets
            if (!string.IsNullOrEmpty(uploadResult.PublicId))
            {
                try
                {
                    await _imageStorageService.DeleteDentalImageAsync(uploadResult.PublicId, CancellationToken.None);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to rollback/delete storage asset {PublicId} after database save error.", uploadResult.PublicId);
                }
            }

            return ApiResponse<ImagingRecordMetadataDto>.Fail("Lưu thông tin hồ sơ hình ảnh vào cơ sở dữ liệu thất bại.");
        }

        // 8. Return response matching architecture
        var responseDto = new ImagingRecordMetadataDto
        {
            ImagingRecordId = imagingRecord.ImagingRecordId,
            ClinicalEntryId = imagingRecord.ClinicalEntryId,
            ImagingType = imagingRecord.ImagingType,
            FileUrl = imagingRecord.FileUrl,
            ResultSummary = imagingRecord.ResultSummary,
            CreatedAt = imagingRecord.CreatedAt,
            OriginalFileName = request.File.FileName,
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
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClinicalEntryId == clinicalEntryId, ct);

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
                    (entry.Appointment != null && entry.Appointment.AssignedDentistUserId == currentUserId);

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
            else if (!string.Equals(currentUserRole, UserRole.SystemAdministrator, StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(currentUserRole, UserRole.DepartmentManager, StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<List<ImagingRecordDto>>.Fail(
                    "Bạn không có quyền truy cập danh sách hình ảnh y tế.");
            }
        }

        var images = await _dbContext.ImagingRecords
            .Where(img => img.ClinicalEntryId == clinicalEntryId)
            .OrderByDescending(img => img.CreatedAt)
            .Select(img => new ImagingRecordDto
            {
                ImagingRecordId = img.ImagingRecordId,
                ClinicalEntryId = img.ClinicalEntryId,
                ImagingType = img.ImagingType,
                FileUrl = img.FileUrl,
                ViewUrl = $"/api/medical-records/imaging/{img.ImagingRecordId}/content",
                ResultSummary = img.ResultSummary,
                CreatedAt = img.CreatedAt
            })
            .AsNoTracking()
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

        var img = await _dbContext.ImagingRecords
            .Include(i => i.ClinicalEntry)
                .ThenInclude(c => c.MedicalRecord)
            .Include(i => i.ClinicalEntry)
                .ThenInclude(c => c.Appointment)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ImagingRecordId == imagingRecordId, ct);

        if (img == null)
        {
            return ApiResponse<ImagingRecordDto>.Fail("Không tìm thấy bản ghi hình ảnh tương ứng.");
        }

        var entry = img.ClinicalEntry;
        if (entry == null)
        {
            return ApiResponse<ImagingRecordDto>.Fail("Không tìm thấy đợt khám lâm sàng của bản ghi hình ảnh.");
        }

        if (string.Equals(currentUserRole, UserRole.Dentist, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(currentUserRole))
        {
            var isAssignedDentist = entry.DentistUserId == currentUserId ||
                (entry.Appointment != null && entry.Appointment.AssignedDentistUserId == currentUserId);

            if (!isAssignedDentist)
            {
                return ApiResponse<ImagingRecordDto>.Fail(
                    "Bác sĩ không có quyền thao tác truy cập hình ảnh của đợt khám của bác sĩ khác.");
            }
        }
        else if (string.Equals(currentUserRole, UserRole.Patient, StringComparison.OrdinalIgnoreCase))
        {
            if (entry.MedicalRecord == null || entry.MedicalRecord.PatientUserId != currentUserId)
            {
                return ApiResponse<ImagingRecordDto>.Fail(
                    "Bệnh nhân không có quyền truy cập hồ sơ hình ảnh của bệnh nhân khác.");
            }
        }
        else if (!string.Equals(currentUserRole, UserRole.SystemAdministrator, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(currentUserRole, UserRole.DepartmentManager, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<ImagingRecordDto>.Fail(
                "Bạn không có quyền truy cập hồ sơ hình ảnh y tế.");
        }

        var dto = new ImagingRecordDto
        {
            ImagingRecordId = img.ImagingRecordId,
            ClinicalEntryId = img.ClinicalEntryId,
            ImagingType = img.ImagingType,
            FileUrl = img.FileUrl,
            ViewUrl = $"/api/medical-records/imaging/{img.ImagingRecordId}/content",
            ResultSummary = img.ResultSummary,
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

        var img = await _dbContext.ImagingRecords
            .Include(i => i.ClinicalEntry)
                .ThenInclude(c => c.MedicalRecord)
            .Include(i => i.ClinicalEntry)
                .ThenInclude(c => c.Appointment)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ImagingRecordId == imagingRecordId, ct);

        if (img == null)
        {
            return ApiResponse<DentalImageStreamResult>.Fail("Không tìm thấy bản ghi hình ảnh tương ứng.");
        }

        var entry = img.ClinicalEntry;
        if (entry == null)
        {
            return ApiResponse<DentalImageStreamResult>.Fail("Không tìm thấy đợt khám lâm sàng của bản ghi hình ảnh.");
        }

        if (string.Equals(currentUserRole, UserRole.Dentist, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(currentUserRole))
        {
            var isAssignedDentist = entry.DentistUserId == currentUserId ||
                (entry.Appointment != null && entry.Appointment.AssignedDentistUserId == currentUserId);

            if (!isAssignedDentist)
            {
                return ApiResponse<DentalImageStreamResult>.Fail(
                    "Bác sĩ không có quyền thao tác truy cập hình ảnh của đợt khám của bác sĩ khác.");
            }
        }
        else if (string.Equals(currentUserRole, UserRole.Patient, StringComparison.OrdinalIgnoreCase))
        {
            if (entry.MedicalRecord == null || entry.MedicalRecord.PatientUserId != currentUserId)
            {
                return ApiResponse<DentalImageStreamResult>.Fail(
                    "Bệnh nhân không có quyền truy cập hồ sơ hình ảnh của bệnh nhân khác.");
            }
        }
        else if (!string.Equals(currentUserRole, UserRole.SystemAdministrator, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(currentUserRole, UserRole.DepartmentManager, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<DentalImageStreamResult>.Fail(
                "Bạn không có quyền truy cập hồ sơ hình ảnh y tế.");
        }

        if (string.IsNullOrWhiteSpace(img.FileUrl))
        {
            return ApiResponse<DentalImageStreamResult>.Fail("Bản ghi hình ảnh không có liên kết tệp hợp lệ.");
        }

        if (_imageStorageService == null)
        {
            return ApiResponse<DentalImageStreamResult>.Fail("Dịch vụ lưu trữ hình ảnh chưa sẵn sàng.");
        }

        var stream = await _imageStorageService.GetImageStreamAsync(img.FileUrl, ct);
        if (stream == null)
        {
            return ApiResponse<DentalImageStreamResult>.Fail("Không thể tải luồng dữ liệu hình ảnh từ hệ thống lưu trữ.");
        }

        var ext = Path.GetExtension(img.FileUrl.Split('?')[0]).ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".dcm" or ".dicom" => "application/dicom",
            _ => "application/octet-stream"
        };
        var fileName = $"imaging_{img.ImagingRecordId}_{img.ImagingType.ToLowerInvariant()}{ext}";

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

        var img = await _dbContext.ImagingRecords
            .Include(i => i.ClinicalEntry)
                .ThenInclude(c => c.MedicalRecord)
            .Include(i => i.ClinicalEntry)
                .ThenInclude(c => c.Appointment)
            .FirstOrDefaultAsync(i => i.ImagingRecordId == imagingRecordId, ct);

        if (img == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy bản ghi hình ảnh tương ứng.");
        }

        var entry = img.ClinicalEntry;
        if (entry == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đợt khám lâm sàng của bản ghi hình ảnh.");
        }

        // 1. Role-based & ownership validation
        if (string.Equals(currentUserRole, UserRole.Patient, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<bool>.Fail("Bệnh nhân không có quyền xóa hình ảnh bệnh án.");
        }

        if (string.Equals(currentUserRole, UserRole.Dentist, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(currentUserRole))
        {
            var isAssignedDentist = entry.DentistUserId == currentUserId ||
                (entry.Appointment != null && entry.Appointment.AssignedDentistUserId == currentUserId);

            if (!isAssignedDentist)
            {
                return ApiResponse<bool>.Fail("Bác sĩ không có quyền thao tác xóa hình ảnh của đợt khám của bác sĩ khác.");
            }
        }
        else if (!string.Equals(currentUserRole, UserRole.SystemAdministrator, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(currentUserRole, UserRole.DepartmentManager, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền thực hiện thao tác xóa hình ảnh y tế.");
        }

        // 2. Storage asset cleanup
        if (!string.IsNullOrWhiteSpace(img.FileUrl))
        {
            if (_imageStorageService == null)
            {
                _logger.LogError("Image storage service is not configured for deleting ImagingRecord {Id}", imagingRecordId);
                return ApiResponse<bool>.Fail("Dịch vụ lưu trữ hình ảnh chưa sẵn sàng để xóa tệp an toàn.");
            }

            if (TryExtractDentalImagingPublicId(img.FileUrl, entry.ClinicalEntryId, out var publicId))
            {
                try
                {
                    var deleted = await _imageStorageService.DeleteDentalImageAsync(publicId!, ct);
                    if (!deleted)
                    {
                        _logger.LogWarning("Storage provider failed to delete asset with public ID {PublicId} for ImagingRecord {Id}", publicId, imagingRecordId);
                        return ApiResponse<bool>.Fail("Xóa tệp hình ảnh từ hệ thống lưu trữ thất bại. Thao tác xóa đã bị hủy để đảm bảo tính toàn vẹn dữ liệu.");
                    }
                }
                catch (Exception storageEx)
                {
                    _logger.LogError(storageEx, "Exception occurred while deleting storage asset {PublicId} for ImagingRecord {Id}", publicId, imagingRecordId);
                    return ApiResponse<bool>.Fail($"Xóa tệp hình ảnh từ hệ thống lưu trữ thất bại: {storageEx.Message}");
                }
            }
            else
            {
                _logger.LogWarning("FileUrl '{FileUrl}' does not match standard Cloudinary dental imaging pattern for ClinicalEntry {EntryId}. Skipping cloud storage deletion to avoid deleting unintended resources.",
                    img.FileUrl, entry.ClinicalEntryId);
            }
        }

        // 3. Database deletion
        try
        {
            _dbContext.ImagingRecords.Remove(img);
            if (entry.MedicalRecord != null)
            {
                entry.MedicalRecord.UpdatedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception dbEx)
        {
            _logger.LogError(dbEx, "Failed to delete ImagingRecord {Id} from database.", imagingRecordId);
            return ApiResponse<bool>.Fail("Xóa bản ghi hình ảnh khỏi cơ sở dữ liệu thất bại.");
        }

        return ApiResponse<bool>.Ok(true, "Đã xóa bản ghi hình ảnh y tế thành công.");
    }

    /// <summary>
    /// Safely extracts Cloudinary public ID only if it strictly matches the dental imaging path convention
    /// for the specific clinical entry, ensuring no unintended files of other entries or patients are deleted.
    /// </summary>
    public static bool TryExtractDentalImagingPublicId(string? fileUrl, long clinicalEntryId, out string? publicId)
    {
        publicId = null;
        if (string.IsNullOrWhiteSpace(fileUrl) || clinicalEntryId <= 0)
        {
            return false;
        }

        var expectedPrefix = $"dental-clinic/medical-imaging/entry_{clinicalEntryId}/";
        var index = fileUrl.IndexOf(expectedPrefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

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
}
