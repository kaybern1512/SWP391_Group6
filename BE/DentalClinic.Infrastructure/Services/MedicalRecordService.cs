using System;
using System.Collections.Generic;
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
    private readonly ILogger<MedicalRecordService> _logger;
    private readonly IImageStorageService _imageStorageService;

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
        : this(dbContext, logger, null!)
    {
    }

    public MedicalRecordService(
        DentalClinicDbContext dbContext,
        ILogger<MedicalRecordService> logger,
        IImageStorageService imageStorageService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _imageStorageService = imageStorageService;
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
                    ImagingType = img.ImagingType,
                    FileUrl = FormatAuthorizedImagingUrl(img.ImagingRecordId, img.FileUrl, img.ImagingType),
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

        // BẢO MẬT: Bác sĩ chỉ có thể thêm phim chụp cho ca khám do chính mình phụ trách
        if (entry.DentistUserId != dentistUserId)
        {
            return ApiResponse<ImagingRecordDto>.Fail("Bạn chỉ có thể thêm phim chụp cho ca khám lâm sàng do chính mình phụ trách.");
        }

        // BẢO MẬT: Chống SSRF / Untrusted Storage Reference khi nhập FileUrl trực tiếp
        if (!string.IsNullOrWhiteSpace(request.FileUrl))
        {
            var trimmedUrl = request.FileUrl.Trim();
            var isSafeLocal = trimmedUrl.StartsWith("App_Data/uploads/imaging", StringComparison.OrdinalIgnoreCase)
                || trimmedUrl.StartsWith("local://", StringComparison.OrdinalIgnoreCase);

            var isSafeCloudinary = CloudinaryImageStorageService.TryValidateCloudinaryUrl(trimmedUrl, null, out _, out _);

            if (!isSafeLocal && !isSafeCloudinary)
            {
                return ApiResponse<ImagingRecordDto>.Fail("Không chấp nhận đường dẫn hình ảnh từ nguồn bên ngoài chưa được hệ thống xác minh. Vui lòng sử dụng tính năng tải lên tệp trực tiếp.");
            }
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
            ImagingType = imagingRecord.ImagingType,
            FileUrl = FormatAuthorizedImagingUrl(imagingRecord.ImagingRecordId, imagingRecord.FileUrl, imagingRecord.ImagingType),
            ResultSummary = imagingRecord.ResultSummary,
            CreatedAt = imagingRecord.CreatedAt
        };

        return ApiResponse<ImagingRecordDto>.Ok(dto, "Lưu thông tin phim chụp X-quang thành công.");
    }

    public async Task<ApiResponse<ImagingRecordDto>> UploadImagingRecordAsync(
        long currentUserId,
        string currentUserRole,
        long clinicalEntryId,
        string imagingType,
        string? resultSummary,
        System.IO.Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imagingType))
        {
            return ApiResponse<ImagingRecordDto>.Fail("Vui lòng chọn hoặc nhập loại phim chụp / chẩn đoán hình ảnh.");
        }

        var entry = await _dbContext.ClinicalEntries
            .Include(c => c.MedicalRecord)
            .FirstOrDefaultAsync(c => c.ClinicalEntryId == clinicalEntryId, ct);

        if (entry == null)
        {
            return ApiResponse<ImagingRecordDto>.Fail("Không tìm thấy đợt khám lâm sàng tương ứng.");
        }

        if (currentUserRole != UserRole.Dentist &&
            currentUserRole != UserRole.SystemAdministrator &&
            currentUserRole != UserRole.Receptionist)
        {
            return ApiResponse<ImagingRecordDto>.Fail("Bạn không có quyền tải lên phim chụp y tế.");
        }

        // 1. Upload lên Cloudinary (hoặc local fallback nếu chưa cấu hình)
        string fileUrl;
        try
        {
            fileUrl = await _imageStorageService.UploadImagingRecordAsync(
                entry.MedicalRecord.PatientUserId,
                clinicalEntryId,
                fileStream,
                fileName,
                contentType,
                ct);
        }
        catch (ArgumentException aex)
        {
            return ApiResponse<ImagingRecordDto>.Fail(aex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi upload hình ảnh phim chụp cho ClinicalEntry {EntryId}", clinicalEntryId);
            return ApiResponse<ImagingRecordDto>.Fail($"Không thể tải hình ảnh lên dịch vụ lưu trữ: {ex.Message}");
        }

        // 2. Lưu metadata và URL vào ImagingRecords trong Database
        var imagingRecord = new ImagingRecord
        {
            ClinicalEntryId = clinicalEntryId,
            ImagingType = imagingType.Trim(),
            FileUrl = fileUrl,
            ResultSummary = resultSummary?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ImagingRecords.Add(imagingRecord);
        entry.MedicalRecord.UpdatedAt = DateTime.UtcNow;

        // 3. Compensating transaction: Nếu lưu DB thất bại, xóa asset vừa upload để tránh file mồ côi
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lưu thông tin phim chụp vào CSDL thất bại. Đang dọn dẹp asset vừa upload để tránh file mồ côi: {FileUrl}", fileUrl);
            try
            {
                await _imageStorageService.DeleteImageAsync(fileUrl, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Không thể dọn dẹp file mồ côi {FileUrl}", fileUrl);
            }

            return ApiResponse<ImagingRecordDto>.Fail("Lỗi khi lưu thông tin phim chụp vào cơ sở dữ liệu. Tệp hình ảnh đã được thu hồi an toàn.");
        }

        var dto = new ImagingRecordDto
        {
            ImagingRecordId = imagingRecord.ImagingRecordId,
            ImagingType = imagingRecord.ImagingType,
            FileUrl = FormatAuthorizedImagingUrl(imagingRecord.ImagingRecordId, imagingRecord.FileUrl, imagingRecord.ImagingType),
            ResultSummary = imagingRecord.ResultSummary,
            CreatedAt = imagingRecord.CreatedAt
        };

        return ApiResponse<ImagingRecordDto>.Ok(dto, "Tải lên và lưu thông tin phim chụp X-quang thành công.");
    }

    public async Task<ApiResponse<bool>> DeleteImagingRecordAsync(
        long currentUserId,
        string currentUserRole,
        long imagingRecordId,
        CancellationToken ct = default)
    {
        var record = await _dbContext.ImagingRecords
            .Include(i => i.ClinicalEntry)
                .ThenInclude(c => c.MedicalRecord)
            .FirstOrDefaultAsync(i => i.ImagingRecordId == imagingRecordId, ct);

        if (record == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy thông tin phim chụp X-quang cần xóa.");
        }

        // Kiểm tra quyền: Chỉ SystemAdministrator hoặc Bác sĩ phụ trách ca khám được phép xóa
        var isAdmin = currentUserRole == UserRole.SystemAdministrator;
        var isDentist = currentUserRole == UserRole.Dentist;

        if (!isAdmin && !isDentist)
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền xóa phim chụp này trong hồ sơ bệnh án.");
        }

        // Bác sĩ chỉ được phép xóa phim chụp thuộc ca khám do chính mình phụ trách
        if (isDentist)
        {
            if (record.ClinicalEntry == null || record.ClinicalEntry.DentistUserId != currentUserId)
            {
                return ApiResponse<bool>.Fail("Bạn chỉ có quyền xóa phim chụp thuộc ca khám lâm sàng do chính mình phụ trách.");
            }
        }

        var fileUrl = record.FileUrl;

        _dbContext.ImagingRecords.Remove(record);
        if (record.ClinicalEntry?.MedicalRecord != null)
        {
            record.ClinicalEntry.MedicalRecord.UpdatedAt = DateTime.UtcNow;
        }

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa bản ghi ImagingRecord {Id} trong CSDL", imagingRecordId);
            return ApiResponse<bool>.Fail("Không thể xóa thông tin phim chụp trong cơ sở dữ liệu.");
        }

        // Đồng bộ xóa asset trên Cloudinary/Storage
        if (!string.IsNullOrWhiteSpace(fileUrl))
        {
            try
            {
                await _imageStorageService.DeleteImageAsync(fileUrl, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Đã xóa metadata DB thành công nhưng không thể xóa asset lưu trữ: {FileUrl}", fileUrl);
            }
        }

        return ApiResponse<bool>.Ok(true, "Đã xóa phim chụp X-quang thành công.");
    }

    public async Task<ApiResponse<StorageFileStreamResult>> GetImagingFileAsync(
        long currentUserId,
        string currentUserRole,
        long imagingRecordId,
        CancellationToken ct = default)
    {
        var record = await _dbContext.ImagingRecords
            .Include(i => i.ClinicalEntry)
                .ThenInclude(c => c.MedicalRecord)
            .FirstOrDefaultAsync(i => i.ImagingRecordId == imagingRecordId, ct);

        if (record == null)
        {
            return ApiResponse<StorageFileStreamResult>.Fail("Không tìm thấy thông tin phim chụp X-quang.");
        }

        if (record.ClinicalEntry == null || record.ClinicalEntry.MedicalRecord == null)
        {
            return ApiResponse<StorageFileStreamResult>.Fail("Dữ liệu hồ sơ bệnh án liên kết không hợp lệ.");
        }

        var patientUserId = record.ClinicalEntry.MedicalRecord.PatientUserId;

        // Phân quyền bảo mật:
        if (currentUserRole == UserRole.Patient)
        {
            // Bệnh nhân chỉ xem được phim chụp thuộc hồ sơ bệnh án của chính mình
            if (patientUserId != currentUserId)
            {
                return ApiResponse<StorageFileStreamResult>.Fail("Bạn không có quyền truy cập phim chụp của bệnh nhân khác.");
            }
        }
        else if (currentUserRole == UserRole.Dentist)
        {
            // Bác sĩ chỉ xem được phim chụp nếu:
            // 1. Là bác sĩ trực tiếp thực hiện ca khám này, HOẶC
            // 2. Có mối quan hệ điều trị/khám qua ca khám lâm sàng khác trong hồ sơ bệnh án, HOẶC
            // 3. Có lịch hẹn điều trị hợp lệ (Confirmed, CheckedIn, Waiting, Called, InService, Completed) với bệnh nhân
            // TUYỆT ĐỐI KHÔNG cấp quyền dựa trên lịch hẹn đã hủy (Cancelled), rút lui (Withdrawn), từ chối (Rejected), hết hạn (Expired) hoặc vắng mặt (NoShow)
            var validAppointmentStatuses = new[]
            {
                AppointmentStatus.Confirmed,
                AppointmentStatus.CheckedIn,
                AppointmentStatus.Waiting,
                AppointmentStatus.Called,
                AppointmentStatus.InService,
                AppointmentStatus.Completed
            };

            var isDirectDentist = record.ClinicalEntry.DentistUserId == currentUserId;

            var isPriorTreatingDentist = await _dbContext.ClinicalEntries
                .AnyAsync(c => c.DentistUserId == currentUserId && c.MedicalRecord.PatientUserId == patientUserId, ct);

            var hasValidAppointment = await _dbContext.Appointments
                .AnyAsync(a => (a.AssignedDentistUserId == currentUserId || a.RequestedDentistUserId == currentUserId)
                    && a.PatientUserId == patientUserId
                    && validAppointmentStatuses.Contains(a.Status), ct);

            var hasAccess = isDirectDentist || isPriorTreatingDentist || hasValidAppointment;

            if (!hasAccess)
            {
                return ApiResponse<StorageFileStreamResult>.Fail("Bạn không có quyền truy cập phim chụp của bệnh nhân này.");
            }
        }
        else if (currentUserRole == UserRole.SystemAdministrator)
        {
            // Admin có quyền xem phục vụ quản trị hệ thống
        }
        else
        {
            // Các vai trò khác (Lễ tân, Trưởng khoa không điều trị...) không được xem trực tiếp tệp phim chụp
            return ApiResponse<StorageFileStreamResult>.Fail("Vai trò của bạn không được phép xem nội dung tệp phim chụp.");
        }

        if (string.IsNullOrWhiteSpace(record.FileUrl))
        {
            return ApiResponse<StorageFileStreamResult>.Fail("Phim chụp này không có tệp kỹ thuật số (bản cứng lưu tại phòng X-quang).");
        }

        var streamResult = await _imageStorageService.GetImageStreamAsync(record.FileUrl, ct);
        if (streamResult == null || streamResult.Stream == null)
        {
            return ApiResponse<StorageFileStreamResult>.Fail("Không thể truy xuất tập tin hình ảnh từ dịch vụ lưu trữ.");
        }

        // Đảm bảo tên file an toàn cho download
        var ext = System.IO.Path.GetExtension(streamResult.FileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = streamResult.ContentType switch
            {
                "application/dicom" => ".dcm",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }
        streamResult.FileName = $"imaging_{record.ImagingRecordId}_{patientUserId}{ext}";

        return ApiResponse<StorageFileStreamResult>.Ok(streamResult);
    }

    private static string? FormatAuthorizedImagingUrl(long imagingRecordId, string? fileUrl, string imagingType)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return null;
        }

        var isDcm = fileUrl.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase)
            || imagingType.Contains("DICOM", StringComparison.OrdinalIgnoreCase);

        // Trả về URI API xác thực của Backend thay vì URL lưu trữ gốc (Cloudinary/App_Data)
        return $"/api/medical-records/imaging/{imagingRecordId}/file{(isDcm ? ".dcm" : "")}";
    }
}
