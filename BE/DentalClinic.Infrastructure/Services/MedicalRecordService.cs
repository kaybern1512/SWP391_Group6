using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class MedicalRecordService : IMedicalRecordService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IMedicalImageStorageService _storageService;
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
        IMedicalImageStorageService storageService,
        ILogger<MedicalRecordService> logger)
    {
        _dbContext = dbContext;
        _storageService = storageService;
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
                    FileUrl = $"/api/medical-records/imaging/{img.ImagingRecordId}/content",
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

    public async Task<ApiResponse<ImagingRecordDto>> UploadImagingRecordAsync(
        long currentUserId,
        string currentUserRole,
        long clinicalEntryId,
        Stream fileStream,
        string fileName,
        string contentType,
        string imagingType,
        string? resultSummary,
        CancellationToken ct = default)
    {
        if (currentUserRole != UserRole.Dentist && currentUserRole != UserRole.SystemAdministrator)
        {
            return ApiResponse<ImagingRecordDto>.Fail("Chỉ bác sĩ điều trị hoặc quản trị viên mới có quyền tải lên ảnh chẩn đoán.");
        }

        var entry = await _dbContext.ClinicalEntries
            .Include(c => c.MedicalRecord)
            .FirstOrDefaultAsync(c => c.ClinicalEntryId == clinicalEntryId, ct);

        if (entry == null)
        {
            return ApiResponse<ImagingRecordDto>.Fail("Không tìm thấy đợt khám lâm sàng được chỉ định.");
        }

        if (currentUserRole == UserRole.Dentist && entry.DentistUserId != currentUserId && currentUserRole != UserRole.SystemAdministrator)
        {
            return ApiResponse<ImagingRecordDto>.Fail("Bạn không có quyền tải lên ảnh cho ca khám do bác sĩ khác phụ trách.");
        }

        var normalizedType = imagingType?.Trim();
        var validTypes = new[] { "Panorama", "Periapical", "Cephalometric", "Intraoral" };
        if (string.IsNullOrWhiteSpace(normalizedType) || !validTypes.Contains(normalizedType, StringComparer.OrdinalIgnoreCase))
        {
            return ApiResponse<ImagingRecordDto>.Fail("Loại ảnh chụp không hợp lệ. Chỉ chấp nhận: Panorama, Periapical, Cephalometric, Intraoral.");
        }

        var canonicalType = validTypes.First(t => t.Equals(normalizedType, StringComparison.OrdinalIgnoreCase));

        var uploadResult = await _storageService.SaveImageAsync(fileStream, fileName, contentType, ct);
        if (!uploadResult.Success)
        {
            return ApiResponse<ImagingRecordDto>.Fail(uploadResult.ErrorMessage ?? "Lưu tệp ảnh thất bại.");
        }

        var imagingRecord = new ImagingRecord
        {
            ClinicalEntryId = clinicalEntryId,
            ImagingType = canonicalType,
            FileUrl = uploadResult.StoredIdentifier,
            ResultSummary = resultSummary?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ImagingRecords.Add(imagingRecord);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu dữ liệu ImagingRecord vào DB. Đang dọn dẹp file storage...");
            if (!string.IsNullOrEmpty(uploadResult.StoredIdentifier))
            {
                await _storageService.DeleteImageAsync(uploadResult.StoredIdentifier, ct);
            }
            return ApiResponse<ImagingRecordDto>.Fail("Lỗi lưu trữ thông tin ảnh vào cơ sở dữ liệu. Thao tác đã được hủy.");
        }

        var dto = new ImagingRecordDto
        {
            ImagingRecordId = imagingRecord.ImagingRecordId,
            ClinicalEntryId = imagingRecord.ClinicalEntryId,
            ImagingType = imagingRecord.ImagingType,
            FileUrl = $"/api/medical-records/imaging/{imagingRecord.ImagingRecordId}/content",
            ResultSummary = imagingRecord.ResultSummary,
            CreatedAt = imagingRecord.CreatedAt
        };

        return ApiResponse<ImagingRecordDto>.Ok(dto, "Tải lên ảnh chẩn đoán thành công.");
    }

    public async Task<ApiResponse<List<ImagingRecordDto>>> GetImagingRecordsByClinicalEntryAsync(
        long currentUserId,
        string currentUserRole,
        long clinicalEntryId,
        CancellationToken ct = default)
    {
        var entry = await _dbContext.ClinicalEntries
            .AsNoTracking()
            .Include(c => c.MedicalRecord)
            .Include(c => c.ImagingRecords)
            .FirstOrDefaultAsync(c => c.ClinicalEntryId == clinicalEntryId, ct);

        if (entry == null)
        {
            return ApiResponse<List<ImagingRecordDto>>.Fail("Không tìm thấy đợt khám lâm sàng.");
        }

        var patientUserId = entry.MedicalRecord.PatientUserId;

        // BOLA / IDOR Verification
        if (currentUserRole == UserRole.Patient)
        {
            if (patientUserId != currentUserId)
            {
                return ApiResponse<List<ImagingRecordDto>>.Fail("Bạn không có quyền truy cập ảnh bệnh án của bệnh nhân khác.");
            }
        }
        else if (currentUserRole == UserRole.Dentist)
        {
            var isTreatingDentist = entry.DentistUserId == currentUserId ||
                                   await HasDentistTreatmentRelationshipAsync(currentUserId, patientUserId, ct);
            if (!isTreatingDentist)
            {
                return ApiResponse<List<ImagingRecordDto>>.Fail("Bạn không có quyền truy cập ảnh chẩn đoán của bệnh nhân này do không phụ trách ca điều trị.");
            }
        }
        else if (currentUserRole != UserRole.SystemAdministrator)
        {
            return ApiResponse<List<ImagingRecordDto>>.Fail("Vai trò của bạn không có quyền xem ảnh y tế.");
        }

        var dtoList = entry.ImagingRecords
            .OrderByDescending(img => img.CreatedAt)
            .Select(img => new ImagingRecordDto
            {
                ImagingRecordId = img.ImagingRecordId,
                ClinicalEntryId = img.ClinicalEntryId,
                ImagingType = img.ImagingType,
                FileUrl = $"/api/medical-records/imaging/{img.ImagingRecordId}/content",
                ResultSummary = img.ResultSummary,
                CreatedAt = img.CreatedAt
            })
            .ToList();

        return ApiResponse<List<ImagingRecordDto>>.Ok(dtoList);
    }

    public async Task<ApiResponse<MedicalImageStreamResult>> GetImagingRecordContentAsync(
        long currentUserId,
        string currentUserRole,
        long imagingRecordId,
        CancellationToken ct = default)
    {
        var record = await _dbContext.ImagingRecords
            .AsNoTracking()
            .Include(r => r.ClinicalEntry)
                .ThenInclude(c => c.MedicalRecord)
            .FirstOrDefaultAsync(r => r.ImagingRecordId == imagingRecordId, ct);

        if (record == null)
        {
            return ApiResponse<MedicalImageStreamResult>.Fail("Không tìm thấy dữ liệu ảnh chẩn đoán.");
        }

        var patientUserId = record.ClinicalEntry.MedicalRecord.PatientUserId;

        // BOLA / IDOR Verification
        if (currentUserRole == UserRole.Patient)
        {
            if (patientUserId != currentUserId)
            {
                return ApiResponse<MedicalImageStreamResult>.Fail("Bạn không có quyền xem hình ảnh của bệnh nhân khác.");
            }
        }
        else if (currentUserRole == UserRole.Dentist)
        {
            var isTreatingDentist = record.ClinicalEntry.DentistUserId == currentUserId ||
                                   await HasDentistTreatmentRelationshipAsync(currentUserId, patientUserId, ct);
            if (!isTreatingDentist)
            {
                return ApiResponse<MedicalImageStreamResult>.Fail("Bạn không có quyền xem ảnh chẩn đoán của bệnh nhân này do không phụ trách ca điều trị.");
            }
        }
        else if (currentUserRole != UserRole.SystemAdministrator)
        {
            return ApiResponse<MedicalImageStreamResult>.Fail("Vai trò của bạn không có quyền xem ảnh y tế.");
        }

        if (string.IsNullOrEmpty(record.FileUrl))
        {
            return ApiResponse<MedicalImageStreamResult>.Fail("Đường dẫn tệp hình ảnh không hợp lệ.");
        }

        var streamResult = await _storageService.GetImageStreamAsync(record.FileUrl, ct);
        if (streamResult == null)
        {
            return ApiResponse<MedicalImageStreamResult>.Fail("Tệp hình ảnh không tồn tại trong hệ thống lưu trữ.");
        }

        return ApiResponse<MedicalImageStreamResult>.Ok(streamResult);
    }

    public async Task<ApiResponse<bool>> DeleteImagingRecordAsync(
        long currentUserId,
        string currentUserRole,
        long imagingRecordId,
        CancellationToken ct = default)
    {
        if (currentUserRole == UserRole.Patient)
        {
            return ApiResponse<bool>.Fail("Bệnh nhân không có quyền xóa ảnh bệnh án.");
        }

        var record = await _dbContext.ImagingRecords
            .Include(r => r.ClinicalEntry)
            .FirstOrDefaultAsync(r => r.ImagingRecordId == imagingRecordId, ct);

        if (record == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy ảnh chẩn đoán cần xóa.");
        }

        if (currentUserRole == UserRole.Dentist && record.ClinicalEntry.DentistUserId != currentUserId && currentUserRole != UserRole.SystemAdministrator)
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền xóa ảnh do bác sĩ khác đăng tải.");
        }

        var fileIdentifier = record.FileUrl;
        _dbContext.ImagingRecords.Remove(record);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa ImagingRecord {Id} khỏi database.", imagingRecordId);
            return ApiResponse<bool>.Fail("Lỗi khi xóa thông tin ảnh khỏi cơ sở dữ liệu.");
        }

        if (!string.IsNullOrEmpty(fileIdentifier))
        {
            try
            {
                await _storageService.DeleteImageAsync(fileIdentifier, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Đã xóa metadata ImagingRecord {Id} nhưng xóa file storage {Path} thất bại.", imagingRecordId, fileIdentifier);
            }
        }

        return ApiResponse<bool>.Ok(true, "Đã xóa ảnh chẩn đoán thành công.");
    }

    private async Task<bool> HasDentistTreatmentRelationshipAsync(long dentistUserId, long patientUserId, CancellationToken ct)
    {
        if (dentistUserId <= 0 || patientUserId <= 0)
        {
            return false;
        }

        // 1. Kiểm tra qua đợt khám lâm sàng trong hồ sơ bệnh án
        var hasClinicalEntry = await _dbContext.ClinicalEntries
            .AnyAsync(ce => ce.DentistUserId == dentistUserId && ce.MedicalRecord.PatientUserId == patientUserId, ct);
        if (hasClinicalEntry) return true;

        // 2. Kiểm tra qua lịch hẹn khám điều trị
        var hasAppointment = await _dbContext.Appointments
            .AnyAsync(a => a.PatientUserId == patientUserId && (a.AssignedDentistUserId == dentistUserId || a.RequestedDentistUserId == dentistUserId), ct);
        if (hasAppointment) return true;

        // 3. Kiểm tra qua kế hoạch điều trị hoặc các buổi điều trị
        var hasTreatmentPlan = await _dbContext.TreatmentPlans
            .AnyAsync(tp => tp.PatientUserId == patientUserId &&
                (tp.CreatedByDentistUserId == dentistUserId ||
                 tp.TreatmentPlanItems.Any(tpi => tpi.TreatmentSessions.Any(ts => ts.DentistUserId == dentistUserId))), ct);
        if (hasTreatmentPlan) return true;

        // 4. Kiểm tra qua chuyển tuyến chuyên khoa
        var hasReferral = await _dbContext.DepartmentReferrals
            .AnyAsync(dr => dr.AssignedDentistUserId == dentistUserId &&
                            dr.TreatmentSession != null &&
                            dr.TreatmentSession.Appointment != null &&
                            dr.TreatmentSession.Appointment.PatientUserId == patientUserId, ct);
        if (hasReferral) return true;

        return false;
    }
}
