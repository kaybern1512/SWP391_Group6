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
    {
        _dbContext = dbContext;
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
            ImagingType = imagingRecord.ImagingType,
            FileUrl = imagingRecord.FileUrl,
            ResultSummary = imagingRecord.ResultSummary,
            CreatedAt = imagingRecord.CreatedAt
        };

        return ApiResponse<ImagingRecordDto>.Ok(dto, "Lưu thông tin phim chụp X-quang thành công.");
    }
}
