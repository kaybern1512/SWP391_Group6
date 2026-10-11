using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ClinicalCore.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services;

public class ClinicalCoreService : IClinicalCoreService
{
    private readonly DentalClinicDbContext _dbContext;

    public ClinicalCoreService(DentalClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<DentalMedicalRecordDto>> GetMedicalRecordAsync(long patientUserId, long currentUserId, string currentRole, CancellationToken ct = default)
    {
        if (!CanReadPatientRecord(currentUserId, patientUserId, currentRole))
        {
            return ApiResponse<DentalMedicalRecordDto>.Fail("Bạn không có quyền xem hồ sơ bệnh án của người khác.");
        }

        var record = await _dbContext.DentalMedicalRecords
            .AsNoTracking()
            .Include(r => r.PatientUser)
                .ThenInclude(p => p.User)
            .Include(r => r.ClinicalEntries)
                .ThenInclude(e => e.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(r => r.ClinicalEntries)
                .ThenInclude(e => e.OdontogramEntries)
            .Include(r => r.ClinicalEntries)
                .ThenInclude(e => e.ImagingRecords)
            .FirstOrDefaultAsync(r => r.PatientUserId == patientUserId, ct);

        if (record == null)
        {
            return ApiResponse<DentalMedicalRecordDto>.Fail("Chưa có hồ sơ bệnh án cho bệnh nhân này.");
        }

        return ApiResponse<DentalMedicalRecordDto>.Ok(MapMedicalRecord(record));
    }

    public async Task<ApiResponse<DentalMedicalRecordDto>> GetOrCreateMedicalRecordAsync(long patientUserId, long currentUserId, string currentRole, CancellationToken ct = default)
    {
        if (!CanReadPatientRecord(currentUserId, patientUserId, currentRole))
        {
            return ApiResponse<DentalMedicalRecordDto>.Fail("Bạn không có quyền truy cập hồ sơ bệnh án của người khác.");
        }

        var record = await _dbContext.DentalMedicalRecords
            .Include(r => r.PatientUser)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.PatientUserId == patientUserId, ct);

        if (record == null)
        {
            var patient = await _dbContext.PatientProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == patientUserId, ct);

            if (patient == null)
            {
                return ApiResponse<DentalMedicalRecordDto>.Fail("Không tìm thấy bệnh nhân tương ứng.");
            }

            record = new DentalMedicalRecord
            {
                PatientUserId = patientUserId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.DentalMedicalRecords.Add(record);
            await _dbContext.SaveChangesAsync(ct);
        }

        var dto = await GetMedicalRecordAsync(patientUserId, currentUserId, currentRole, ct);
        return dto.Success ? dto : ApiResponse<DentalMedicalRecordDto>.Fail(dto.Message ?? "Không thể tải hồ sơ bệnh án.");
    }

    public async Task<ApiResponse<ClinicalEntryDto>> CreateClinicalEntryAsync(CreateClinicalEntryRequest request, long currentUserId, string currentRole, CancellationToken ct = default)
    {
        if (!CanWriteClinicalEntry(currentUserId, request.PatientUserId, currentRole))
        {
            return ApiResponse<ClinicalEntryDto>.Fail("Bạn không có quyền tạo hoặc cập nhật bệnh án này.");
        }

        if (request.PatientUserId <= 0)
        {
            return ApiResponse<ClinicalEntryDto>.Fail("Bệnh nhân không hợp lệ.");
        }

        var patient = await _dbContext.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == request.PatientUserId, ct);

        if (patient == null)
        {
            return ApiResponse<ClinicalEntryDto>.Fail("Không tìm thấy bệnh nhân.");
        }

        if (!request.AppointmentId.HasValue || request.AppointmentId.Value <= 0)
        {
            return ApiResponse<ClinicalEntryDto>.Fail("Bệnh án lâm sàng phải gắn với một cuộc hẹn hợp lệ.");
        }

        if (request.AppointmentId.HasValue)
        {
            var appointment = await _dbContext.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId.Value, ct);

            if (appointment == null)
            {
                return ApiResponse<ClinicalEntryDto>.Fail("Cuộc hẹn không tồn tại.");
            }

            if (appointment.PatientUserId != request.PatientUserId)
            {
                return ApiResponse<ClinicalEntryDto>.Fail("Appointment không thuộc bệnh nhân đã chọn.");
            }

            if (appointment.AssignedDentistUserId != currentUserId)
            {
                return ApiResponse<ClinicalEntryDto>.Fail("Bạn chỉ được ghi bệnh án cho lịch hẹn được phân công cho mình.");
            }
        }

        var medicalRecord = await _dbContext.DentalMedicalRecords
            .FirstOrDefaultAsync(r => r.PatientUserId == request.PatientUserId, ct);

        if (medicalRecord == null)
        {
            medicalRecord = new DentalMedicalRecord
            {
                PatientUserId = request.PatientUserId,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.DentalMedicalRecords.Add(medicalRecord);
            await _dbContext.SaveChangesAsync(ct);
        }

        var validToothNumbers = new[] { 11, 21, 31, 41 }
            .SelectMany(quadrant => Enumerable.Range(quadrant, 8))
            .Select(number => number.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in request.OdontogramEntries)
        {
            if (string.IsNullOrWhiteSpace(item.ToothNumber))
            {
                return ApiResponse<ClinicalEntryDto>.Fail("Số răng không được để trống.");
            }

            var tooth = item.ToothNumber.Trim();
            if (!validToothNumbers.Contains(tooth))
            {
                return ApiResponse<ClinicalEntryDto>.Fail($"Số răng {tooth} nằm ngoài chuẩn FDI hợp lệ (11–18, 21–28, 31–38, 41–48).");
            }
        }

        var clinicalEntry = new ClinicalEntry
        {
            MedicalRecordId = medicalRecord.MedicalRecordId,
            AppointmentId = request.AppointmentId,
            DentistUserId = request.DentistUserId,
            Symptoms = request.Symptoms,
            ClinicalFindings = request.ClinicalFindings,
            Diagnosis = request.Diagnosis,
            DentalConditions = request.DentalConditions,
            ClinicalNotes = request.ClinicalNotes,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ClinicalEntries.Add(clinicalEntry);
        await _dbContext.SaveChangesAsync(ct);

        if (request.OdontogramEntries.Count > 0)
        {
            foreach (var item in request.OdontogramEntries)
            {
                _dbContext.OdontogramEntries.Add(new OdontogramEntry
                {
                    ClinicalEntryId = clinicalEntry.ClinicalEntryId,
                    ToothNumber = item.ToothNumber.Trim(),
                    Surface = item.Surface,
                    Condition = string.IsNullOrWhiteSpace(item.Condition) ? "Unknown" : item.Condition.Trim(),
                    Note = item.Note
                });
            }
        }

        if (request.ImagingRecords.Count > 0)
        {
            foreach (var image in request.ImagingRecords)
            {
                _dbContext.ImagingRecords.Add(new ImagingRecord
                {
                    ClinicalEntryId = clinicalEntry.ClinicalEntryId,
                    ImagingType = string.IsNullOrWhiteSpace(image.ImagingType) ? "XRay" : image.ImagingType.Trim(),
                    FileUrl = image.FileUrl,
                    ResultSummary = image.ResultSummary,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        return await GetClinicalEntryByIdAsync(clinicalEntry.ClinicalEntryId, currentUserId, currentRole, ct);
    }

    public async Task<ApiResponse<List<TreatmentPlanDto>>> GetTreatmentPlansForPatientAsync(
        long patientUserId,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        if (!CanReadPatientRecord(currentUserId, patientUserId, currentRole))
        {
            return ApiResponse<List<TreatmentPlanDto>>.Fail("Bạn không có quyền xem kế hoạch điều trị của bệnh nhân này.");
        }

        var plans = await QueryTreatmentPlans()
            .Where(plan => plan.PatientUserId == patientUserId)
            .OrderByDescending(plan => plan.CreatedAt)
            .ToListAsync(ct);

        return ApiResponse<List<TreatmentPlanDto>>.Ok(plans.Select(MapTreatmentPlan).ToList());
    }

    public Task<ApiResponse<TreatmentPlanDto>> GetTreatmentPlanByIdAsync(
        long treatmentPlanId,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default) =>
        GetTreatmentPlanAsync(treatmentPlanId, currentUserId, currentRole, ct);

    public async Task<ApiResponse<TreatmentPlanDto>> CreateTreatmentPlanAsync(CreateTreatmentPlanRequest request, long currentUserId, string currentRole, CancellationToken ct = default)
    {
        if (!IsDentist(currentRole))
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Chỉ bác sĩ mới được tạo kế hoạch điều trị.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Kế hoạch điều trị phải có ít nhất 1 hạng mục.");
        }

        var patient = await _dbContext.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == request.PatientUserId, ct);

        if (patient == null)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Bệnh nhân không tồn tại.");
        }

        var department = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, ct);

        if (department == null)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Khoa điều trị không tồn tại.");
        }

        if (!await _dbContext.DentistDepartments.AnyAsync(assignment =>
                assignment.DentistUserId == currentUserId
                && assignment.DepartmentId == request.DepartmentId
                && assignment.Status == "Active", ct))
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Bạn không có quyền lập kế hoạch tại khoa đã chọn.");
        }

        var requestedServiceIds = request.Items
            .Where(item => item.DepartmentServiceId.HasValue)
            .Select(item => item.DepartmentServiceId!.Value)
            .Distinct()
            .ToList();
        if (requestedServiceIds.Count > 0)
        {
            var services = await _dbContext.DepartmentServices
                .Where(service => requestedServiceIds.Contains(service.DepartmentServiceId))
                .Select(service => new { service.DepartmentServiceId, service.DepartmentId })
                .ToListAsync(ct);

            if (services.Count != requestedServiceIds.Count
                || services.Any(service => service.DepartmentId != request.DepartmentId))
            {
                return ApiResponse<TreatmentPlanDto>.Fail("Dịch vụ phải tồn tại và thuộc khoa đã chọn.");
            }
        }

        var sequenceNumbers = request.Items.Select(i => i.SequenceNo).ToList();
        if (sequenceNumbers.Any(sequence => sequence <= 0)
            || sequenceNumbers.Distinct().Count() != sequenceNumbers.Count)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Số thứ tự điều trị phải lớn hơn 0 và duy nhất trong cùng kế hoạch.");
        }

        var plan = new TreatmentPlan
        {
            PatientUserId = request.PatientUserId,
            DepartmentId = request.DepartmentId,
            CreatedByDentistUserId = currentUserId,
            Status = TreatmentPlanStatus.Planned,
            EstimatedTotal = request.Items.Sum(i => i.EstimatedCost ?? 0m),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TreatmentPlans.Add(plan);
        await _dbContext.SaveChangesAsync(ct);

        foreach (var item in request.Items)
        {
            var planItem = new TreatmentPlanItem
            {
                TreatmentPlanId = plan.TreatmentPlanId,
                DepartmentServiceId = item.DepartmentServiceId,
                AssignedDentistUserId = item.AssignedDentistUserId,
                ToothNumber = item.ToothNumber,
                SequenceNo = item.SequenceNo,
                ExpectedDate = item.ExpectedDate.HasValue ? DateOnly.FromDateTime(item.ExpectedDate.Value) : null,
                EstimatedCost = item.EstimatedCost,
                Status = TreatmentPlanItemStatus.Planned,
                Note = item.Note
            };

            _dbContext.TreatmentPlanItems.Add(planItem);
        }

        await _dbContext.SaveChangesAsync(ct);
        return await GetTreatmentPlanAsync(plan.TreatmentPlanId, currentUserId, currentRole, ct);
    }

    public async Task<ApiResponse<TreatmentPlanDto>> UpdateTreatmentPlanItemStatusAsync(
        long treatmentPlanItemId,
        UpdateTreatmentPlanItemStatusRequest request,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Trạng thái hạng mục không được để trống.");
        }

        var item = await _dbContext.TreatmentPlanItems
            .Include(planItem => planItem.TreatmentPlan)
            .FirstOrDefaultAsync(planItem => planItem.TreatmentPlanItemId == treatmentPlanItemId, ct);
        if (item == null)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Không tìm thấy hạng mục kế hoạch điều trị.");
        }

        if (!await CanManageTreatmentPlanAsync(item.TreatmentPlan, item, currentUserId, currentRole, ct))
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Bạn không có quyền cập nhật hạng mục kế hoạch này.");
        }

        var requestedStatus = request.Status.Trim();
        var targetStatus = requestedStatus.Equals(TreatmentPlanItemStatus.InProgress, StringComparison.OrdinalIgnoreCase)
            ? TreatmentPlanItemStatus.InProgress
            : requestedStatus.Equals(TreatmentPlanItemStatus.Completed, StringComparison.OrdinalIgnoreCase)
                ? TreatmentPlanItemStatus.Completed
                : requestedStatus.Equals(TreatmentPlanItemStatus.Cancelled, StringComparison.OrdinalIgnoreCase)
                    ? TreatmentPlanItemStatus.Cancelled
                    : string.Empty;

        if (targetStatus.Length == 0)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Trạng thái hạng mục phải là InProgress, Completed hoặc Cancelled.");
        }

        var validTransition = item.Status switch
        {
            TreatmentPlanItemStatus.Planned => targetStatus is TreatmentPlanItemStatus.InProgress
                or TreatmentPlanItemStatus.Completed or TreatmentPlanItemStatus.Cancelled,
            TreatmentPlanItemStatus.InProgress => targetStatus is TreatmentPlanItemStatus.Completed
                or TreatmentPlanItemStatus.Cancelled,
            _ => false
        };
        if (!validTransition)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Không thể chuyển hạng mục từ trạng thái hiện tại sang trạng thái yêu cầu.");
        }

        if (targetStatus == TreatmentPlanItemStatus.Completed
            && !await _dbContext.TreatmentSessions.AnyAsync(
                session => session.TreatmentPlanItemId == treatmentPlanItemId, ct))
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Hạng mục chỉ được hoàn tất sau khi đã ghi nhận ca điều trị.");
        }

        item.Status = targetStatus;
        var plan = item.TreatmentPlan;
        if (plan.Status == TreatmentPlanStatus.Planned
            && targetStatus == TreatmentPlanItemStatus.InProgress)
        {
            plan.Status = TreatmentPlanStatus.InProgress;
        }

        var planItems = await _dbContext.TreatmentPlanItems
            .Where(planItem => planItem.TreatmentPlanId == plan.TreatmentPlanId)
            .ToListAsync(ct);
        if (planItems.All(planItem => planItem.Status == TreatmentPlanItemStatus.Completed))
        {
            plan.Status = TreatmentPlanStatus.Completed;
        }

        plan.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return await GetTreatmentPlanAsync(plan.TreatmentPlanId, currentUserId, currentRole, ct);
    }

    public async Task<ApiResponse<TreatmentPlanDto>> UpdateTreatmentPlanStatusAsync(
        long treatmentPlanId,
        UpdateTreatmentPlanStatusRequest request,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Trạng thái kế hoạch không được để trống.");
        }

        if (!request.Status.Trim().Equals(TreatmentPlanStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Chỉ có thể hủy kế hoạch bằng thao tác này.");
        }

        var plan = await _dbContext.TreatmentPlans
            .FirstOrDefaultAsync(entity => entity.TreatmentPlanId == treatmentPlanId, ct);
        if (plan == null)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Không tìm thấy kế hoạch điều trị.");
        }

        if (!IsSystemAdministrator(currentRole))
        {
            var isCreator = IsDentist(currentRole) && plan.CreatedByDentistUserId == currentUserId;
            var isManager = IsDepartmentManager(currentRole)
                && await _dbContext.Departments.AnyAsync(
                    department => department.DepartmentId == plan.DepartmentId
                        && department.ManagerUserId == currentUserId, ct);
            if (!isCreator && !isManager)
            {
                return ApiResponse<TreatmentPlanDto>.Fail("Bạn không có quyền hủy kế hoạch điều trị này.");
            }
        }

        if (plan.Status is TreatmentPlanStatus.Completed or TreatmentPlanStatus.Cancelled)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Kế hoạch đã kết thúc và không thể hủy.");
        }

        plan.Status = TreatmentPlanStatus.Cancelled;
        plan.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return await GetTreatmentPlanAsync(plan.TreatmentPlanId, currentUserId, currentRole, ct);
    }

    public async Task<ApiResponse<List<TreatmentSessionDto>>> GetTreatmentSessionsForPatientAsync(
        long patientUserId,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        if (!CanReadPatientRecord(currentUserId, patientUserId, currentRole))
        {
            return ApiResponse<List<TreatmentSessionDto>>.Fail("Bạn không có quyền xem ca điều trị của bệnh nhân này.");
        }

        var sessions = await _dbContext.TreatmentSessions
            .AsNoTracking()
            .Include(session => session.DentistUser)
                .ThenInclude(dentist => dentist.User)
            .Include(session => session.PerformedServices)
                .ThenInclude(service => service.DepartmentService)
            .Include(session => session.TreatmentPlanItem)
                .ThenInclude(item => item.TreatmentPlan)
            .Where(session => session.TreatmentPlanItem.TreatmentPlan.PatientUserId == patientUserId)
            .OrderByDescending(session => session.SessionDate)
            .ToListAsync(ct);

        return ApiResponse<List<TreatmentSessionDto>>.Ok(sessions.Select(MapTreatmentSession).ToList());
    }

    public async Task<ApiResponse<TreatmentSessionDto>> CreateTreatmentSessionAsync(CreateTreatmentSessionRequest request, long currentUserId, string currentRole, CancellationToken ct = default)
    {
        if (!IsDentist(currentRole))
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Bạn không có quyền tạo ca điều trị.");
        }

        var planItem = await _dbContext.TreatmentPlanItems
            .Include(i => i.TreatmentPlan)
            .FirstOrDefaultAsync(i => i.TreatmentPlanItemId == request.TreatmentPlanItemId, ct);

        if (planItem == null)
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Hạng mục điều trị không tồn tại.");
        }

        if (planItem.TreatmentPlan.Status is TreatmentPlanStatus.Completed or TreatmentPlanStatus.Cancelled
            || planItem.Status is TreatmentPlanItemStatus.Completed or TreatmentPlanItemStatus.Cancelled)
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Không thể tạo ca điều trị cho kế hoạch hoặc hạng mục đã kết thúc.");
        }

        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Cuộc hẹn không tồn tại.");
        }

        if (appointment.PatientUserId != planItem.TreatmentPlan.PatientUserId)
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Cuộc hẹn không thuộc bệnh nhân của kế hoạch điều trị.");
        }

        if (appointment.AssignedDentistUserId != currentUserId)
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Bạn chỉ được ghi ca điều trị cho lịch hẹn được phân công cho mình.");
        }

        if (request.PerformedServices == null || request.PerformedServices.Count == 0)
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Ca điều trị phải ghi nhận ít nhất một dịch vụ đã thực hiện.");
        }

        var serviceIds = request.PerformedServices
            .Select(service => service.DepartmentServiceId)
            .Distinct()
            .ToList();
        var servicesById = await _dbContext.DepartmentServices
            .Where(service => serviceIds.Contains(service.DepartmentServiceId))
            .ToDictionaryAsync(service => service.DepartmentServiceId, ct);

        if (servicesById.Count != serviceIds.Count
            || request.PerformedServices.Any(service => service.Quantity <= 0
                || !servicesById.TryGetValue(service.DepartmentServiceId, out var departmentService)
                || departmentService.DepartmentId != planItem.TreatmentPlan.DepartmentId))
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Dịch vụ đã thực hiện không hợp lệ hoặc không thuộc khoa của kế hoạch.");
        }

        var session = new TreatmentSession
        {
            TreatmentPlanItemId = request.TreatmentPlanItemId,
            AppointmentId = request.AppointmentId,
            DentistUserId = currentUserId,
            SessionDate = DateTime.UtcNow,
            TreatmentResult = request.TreatmentResult,
            ClinicalNote = request.ClinicalNote
        };

        _dbContext.TreatmentSessions.Add(session);
        if (planItem.Status == TreatmentPlanItemStatus.Planned)
        {
            planItem.Status = TreatmentPlanItemStatus.InProgress;
        }
        if (planItem.TreatmentPlan.Status == TreatmentPlanStatus.Planned)
        {
            planItem.TreatmentPlan.Status = TreatmentPlanStatus.InProgress;
            planItem.TreatmentPlan.UpdatedAt = DateTime.UtcNow;
        }
        await _dbContext.SaveChangesAsync(ct);

        if (request.PerformedServices != null && request.PerformedServices.Count > 0)
        {
            foreach (var svc in request.PerformedServices)
            {
                var performed = new PerformedService
                {
                    TreatmentSessionId = session.TreatmentSessionId,
                    DepartmentServiceId = svc.DepartmentServiceId,
                    Quantity = svc.Quantity,
                    UnitPrice = servicesById[svc.DepartmentServiceId].Price
                };

                _dbContext.PerformedServices.Add(performed);
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        var plan = await _dbContext.TreatmentPlans
            .Include(p => p.TreatmentPlanItems)
            .FirstOrDefaultAsync(p => p.TreatmentPlanId == planItem.TreatmentPlanId, ct);

        if (plan != null)
        {
            var existingSessions = await _dbContext.TreatmentSessions
                .CountAsync(s => s.TreatmentPlanItem.TreatmentPlanId == plan.TreatmentPlanId, ct);

            if (existingSessions >= 1 && plan.Status == TreatmentPlanStatus.Planned)
            {
                plan.Status = TreatmentPlanStatus.InProgress;
                plan.UpdatedAt = DateTime.UtcNow;
            }

            var allCompleted = plan.TreatmentPlanItems.All(item => item.Status == TreatmentPlanItemStatus.Completed);
            if (allCompleted)
            {
                plan.Status = TreatmentPlanStatus.Completed;
                plan.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(ct);
        }

        return await GetTreatmentSessionAsync(session.TreatmentSessionId, currentUserId, currentRole, ct);
    }

    public async Task<ApiResponse<PrescriptionDto>> CreatePrescriptionAsync(CreatePrescriptionRequest request, long currentUserId, string currentRole, CancellationToken ct = default)
    {
        if (!IsDentist(currentRole))
        {
            return ApiResponse<PrescriptionDto>.Fail("Chỉ bác sĩ mới được kê đơn thuốc.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return ApiResponse<PrescriptionDto>.Fail("Đơn thuốc phải có ít nhất 1 dòng thuốc.");
        }

        var session = await _dbContext.TreatmentSessions
            .FirstOrDefaultAsync(s => s.TreatmentSessionId == request.TreatmentSessionId, ct);

        if (session == null)
        {
            return ApiResponse<PrescriptionDto>.Fail("Ca điều trị không tồn tại.");
        }

        if (session.DentistUserId != currentUserId)
        {
            return ApiResponse<PrescriptionDto>.Fail("Bạn chỉ được kê đơn cho ca điều trị do mình phụ trách.");
        }

        if (request.Items.Any(item => string.IsNullOrWhiteSpace(item.MedicationName)))
        {
            return ApiResponse<PrescriptionDto>.Fail("Tên thuốc không được để trống.");
        }

        if (await _dbContext.Prescriptions.AnyAsync(prescription => prescription.TreatmentSessionId == request.TreatmentSessionId, ct))
        {
            return ApiResponse<PrescriptionDto>.Fail("Ca điều trị này đã có đơn thuốc.");
        }

        var prescription = new Prescription
        {
            TreatmentSessionId = request.TreatmentSessionId,
            GeneralInstructions = request.GeneralInstructions,
            IssuedAt = DateTime.UtcNow
        };

        _dbContext.Prescriptions.Add(prescription);
        await _dbContext.SaveChangesAsync(ct);

        foreach (var item in request.Items)
        {
            _dbContext.PrescriptionItems.Add(new PrescriptionItem
            {
                PrescriptionId = prescription.PrescriptionId,
                MedicationName = item.MedicationName.Trim(),
                Dosage = item.Dosage,
                Frequency = item.Frequency,
                Duration = item.Duration,
                Instructions = item.Instructions
            });
        }

        await _dbContext.SaveChangesAsync(ct);
        return await GetPrescriptionAsync(prescription.PrescriptionId, currentUserId, currentRole, ct);
    }

    public async Task<ApiResponse<List<PrescriptionDto>>> GetPrescriptionsForPatientAsync(
        long patientUserId,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        if (!CanReadPatientRecord(currentUserId, patientUserId, currentRole))
        {
            return ApiResponse<List<PrescriptionDto>>.Fail("Bạn không có quyền xem đơn thuốc của bệnh nhân này.");
        }

        var prescriptions = await QueryPrescriptions()
            .Where(prescription => prescription.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUserId == patientUserId)
            .OrderByDescending(prescription => prescription.IssuedAt)
            .ToListAsync(ct);

        return ApiResponse<List<PrescriptionDto>>.Ok(prescriptions.Select(MapPrescription).ToList());
    }

    public async Task<ApiResponse<PrescriptionDto>> GetPrescriptionByIdAsync(
        long prescriptionId,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default) =>
        await GetPrescriptionAsync(prescriptionId, currentUserId, currentRole, ct);

    public async Task<ApiResponse<PrescriptionDto>> GetPrescriptionForTreatmentSessionAsync(
        long treatmentSessionId,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        var prescriptionId = await _dbContext.Prescriptions
            .AsNoTracking()
            .Where(prescription => prescription.TreatmentSessionId == treatmentSessionId)
            .Select(prescription => (long?)prescription.PrescriptionId)
            .FirstOrDefaultAsync(ct);

        if (!prescriptionId.HasValue)
        {
            return ApiResponse<PrescriptionDto>.Fail("Không tìm thấy đơn thuốc cho ca điều trị này.");
        }

        return await GetPrescriptionAsync(prescriptionId.Value, currentUserId, currentRole, ct);
    }

    public async Task<ApiResponse<List<DepartmentReferralDto>>> GetDepartmentReferralsForPatientAsync(
        long patientUserId,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        if (!CanReadPatientRecord(currentUserId, patientUserId, currentRole))
        {
            return ApiResponse<List<DepartmentReferralDto>>.Fail("Bạn không có quyền xem chuyển khoa của bệnh nhân này.");
        }

        var referrals = await QueryReferrals()
            .Where(referral => referral.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUserId == patientUserId)
            .OrderByDescending(referral => referral.CreatedAt)
            .ToListAsync(ct);

        return ApiResponse<List<DepartmentReferralDto>>.Ok(referrals.Select(MapReferral).ToList());
    }

    public async Task<ApiResponse<List<DepartmentReferralDto>>> GetDepartmentReferralsForManagerAsync(
        long currentUserId,
        string currentRole,
        string? status,
        CancellationToken ct = default)
    {
        if (!IsDepartmentManager(currentRole) && !IsSystemAdministrator(currentRole))
        {
            return ApiResponse<List<DepartmentReferralDto>>.Fail("Bạn không có quyền xem danh sách chuyển khoa.");
        }

        IQueryable<DepartmentReferral> query = QueryReferrals();
        if (IsDepartmentManager(currentRole))
        {
            query = query.Where(referral => referral.ToDepartment.ManagerUserId == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(referral => referral.Status == status.Trim());
        }

        var referrals = await query
            .OrderBy(referral => referral.Status == DepartmentReferralStatus.Pending ? 0 : 1)
            .ThenByDescending(referral => referral.CreatedAt)
            .ToListAsync(ct);

        return ApiResponse<List<DepartmentReferralDto>>.Ok(referrals.Select(MapReferral).ToList());
    }

    public async Task<ApiResponse<DepartmentReferralDto>> CreateDepartmentReferralAsync(CreateDepartmentReferralRequest request, long currentUserId, string currentRole, CancellationToken ct = default)
    {
        if (!IsDentist(currentRole) && !IsDepartmentManager(currentRole))
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Bạn không có quyền gửi chuyển khoa.");
        }

        if (request.FromDepartmentId == request.ToDepartmentId)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Khoa gửi và khoa nhận không được trùng nhau.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Lý do chuyển khoa là bắt buộc và không vượt quá 1000 ký tự.");
        }

        var session = await _dbContext.TreatmentSessions
            .Include(treatmentSession => treatmentSession.TreatmentPlanItem)
                .ThenInclude(item => item.TreatmentPlan)
            .FirstOrDefaultAsync(s => s.TreatmentSessionId == request.TreatmentSessionId, ct);

        if (session == null)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Ca điều trị không tồn tại.");
        }

        var fromDept = await _dbContext.Departments.FindAsync(request.FromDepartmentId);
        var toDept = await _dbContext.Departments.FindAsync(request.ToDepartmentId);

        if (fromDept == null || toDept == null)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Khoa gửi hoặc khoa nhận không hợp lệ.");
        }

        if (session.TreatmentPlanItem.TreatmentPlan.DepartmentId != request.FromDepartmentId)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Khoa gửi phải trùng với khoa của kế hoạch điều trị.");
        }

        if (IsDentist(currentRole) && session.DentistUserId != currentUserId)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Bạn chỉ được chuyển khoa từ ca điều trị do mình phụ trách.");
        }

        if (IsDepartmentManager(currentRole) && fromDept.ManagerUserId != currentUserId)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Bạn không có quyền quản lý khoa gửi chuyển.");
        }

        var referral = new DepartmentReferral
        {
            TreatmentSessionId = request.TreatmentSessionId,
            FromDepartmentId = request.FromDepartmentId,
            ToDepartmentId = request.ToDepartmentId,
            Reason = request.Reason,
            Status = DepartmentReferralStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DepartmentReferrals.Add(referral);
        await _dbContext.SaveChangesAsync(ct);

        var created = await QueryReferrals()
            .FirstAsync(entity => entity.ReferralId == referral.ReferralId, ct);
        return ApiResponse<DepartmentReferralDto>.Ok(MapReferral(created));
    }

    public async Task<ApiResponse<DepartmentReferralDto>> ReviewDepartmentReferralAsync(
        long referralId,
        ReviewDepartmentReferralRequest request,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        if (!IsDepartmentManager(currentRole) && !IsSystemAdministrator(currentRole))
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Chỉ quản lý khoa nhận mới được duyệt chuyển khoa.");
        }

        var referral = await _dbContext.DepartmentReferrals
            .Include(entity => entity.ToDepartment)
            .FirstOrDefaultAsync(entity => entity.ReferralId == referralId, ct);
        if (referral == null)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Không tìm thấy yêu cầu chuyển khoa.");
        }

        if (!CanManageReferralDepartment(referral.ToDepartment, currentUserId, currentRole))
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Bạn không có quyền quản lý khoa nhận chuyển.");
        }

        if (referral.Status != DepartmentReferralStatus.Pending)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Chỉ yêu cầu đang Pending mới được duyệt.");
        }

        if (!await _dbContext.StaffProfiles.AnyAsync(staff => staff.UserId == currentUserId, ct))
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Tài khoản duyệt không có hồ sơ nhân viên hợp lệ.");
        }

        referral.Status = request.Accept ? DepartmentReferralStatus.Accepted : DepartmentReferralStatus.Rejected;
        referral.ReviewedByUserId = currentUserId;
        referral.ReviewedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        var updated = await QueryReferrals()
            .FirstAsync(entity => entity.ReferralId == referralId, ct);
        return ApiResponse<DepartmentReferralDto>.Ok(MapReferral(updated));
    }

    public async Task<ApiResponse<DepartmentReferralDto>> AssignDepartmentReferralAsync(
        long referralId,
        AssignDepartmentReferralRequest request,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        if (!IsDepartmentManager(currentRole) && !IsSystemAdministrator(currentRole))
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Chỉ quản lý khoa nhận mới được phân công bác sĩ.");
        }

        var referral = await _dbContext.DepartmentReferrals
            .Include(entity => entity.ToDepartment)
            .FirstOrDefaultAsync(entity => entity.ReferralId == referralId, ct);
        if (referral == null)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Không tìm thấy yêu cầu chuyển khoa.");
        }

        if (!CanManageReferralDepartment(referral.ToDepartment, currentUserId, currentRole))
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Bạn không có quyền quản lý khoa nhận chuyển.");
        }

        if (referral.Status != DepartmentReferralStatus.Accepted)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Chỉ yêu cầu Accepted mới được phân công bác sĩ.");
        }

        var dentistIsInReceivingDepartment = await _dbContext.DentistDepartments
            .AnyAsync(assignment => assignment.DentistUserId == request.DentistUserId
                && assignment.DepartmentId == referral.ToDepartmentId
                && assignment.Status == "Active", ct);
        if (!dentistIsInReceivingDepartment)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Bác sĩ phải đang được phân công Active tại khoa nhận.");
        }

        referral.AssignedDentistUserId = request.DentistUserId;
        referral.Status = DepartmentReferralStatus.Assigned;
        await _dbContext.SaveChangesAsync(ct);

        var updated = await QueryReferrals()
            .FirstAsync(entity => entity.ReferralId == referralId, ct);
        return ApiResponse<DepartmentReferralDto>.Ok(MapReferral(updated));
    }

    public async Task<ApiResponse<DepartmentReferralDto>> CompleteDepartmentReferralAsync(
        long referralId,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        if (!IsDentist(currentRole))
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Chỉ bác sĩ được phân công mới có thể hoàn tất chuyển khoa.");
        }

        var referral = await _dbContext.DepartmentReferrals
            .FirstOrDefaultAsync(entity => entity.ReferralId == referralId, ct);
        if (referral == null)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Không tìm thấy yêu cầu chuyển khoa.");
        }

        if (referral.Status != DepartmentReferralStatus.Assigned || referral.AssignedDentistUserId != currentUserId)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Yêu cầu chưa được phân công cho bạn hoặc đã hoàn tất.");
        }

        referral.Status = DepartmentReferralStatus.Completed;
        await _dbContext.SaveChangesAsync(ct);

        var updated = await QueryReferrals()
            .FirstAsync(entity => entity.ReferralId == referralId, ct);
        return ApiResponse<DepartmentReferralDto>.Ok(MapReferral(updated));
    }

    public async Task<ApiResponse<List<FollowUpScheduleDto>>> GetFollowUpsForPatientAsync(
        long patientUserId,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        if (!CanReadPatientRecord(currentUserId, patientUserId, currentRole))
        {
            return ApiResponse<List<FollowUpScheduleDto>>.Fail("Bạn không có quyền xem lịch tái khám của bệnh nhân này.");
        }

        var followUps = await QueryFollowUps()
            .Where(followUp => followUp.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUserId == patientUserId)
            .OrderByDescending(followUp => followUp.RecommendedDate)
            .ToListAsync(ct);

        return ApiResponse<List<FollowUpScheduleDto>>.Ok(followUps.Select(MapFollowUp).ToList());
    }

    public async Task<ApiResponse<FollowUpScheduleDto>> CreateFollowUpAsync(CreateFollowUpRequest request, long currentUserId, string currentRole, CancellationToken ct = default)
    {
        if (!IsDentist(currentRole) && !IsDepartmentManager(currentRole))
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Bạn không có quyền đề xuất tái khám.");
        }

        if (request.RecommendedDate <= DateTime.UtcNow)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Ngày tái khám đề xuất phải lớn hơn thời điểm hiện tại.");
        }

        var session = await _dbContext.TreatmentSessions
            .FirstOrDefaultAsync(s => s.TreatmentSessionId == request.TreatmentSessionId, ct);

        if (session == null)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Ca điều trị không tồn tại.");
        }

        if (session.DentistUserId != currentUserId)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Bạn chỉ được đề xuất tái khám cho ca điều trị do mình phụ trách.");
        }

        var followUp = new FollowUpSchedule
        {
            TreatmentSessionId = request.TreatmentSessionId,
            RecommendedDate = request.RecommendedDate,
            Reason = request.Reason,
            Status = FollowUpScheduleStatus.Pending,
            AppointmentId = null
        };

        _dbContext.FollowUpSchedules.Add(followUp);
        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<FollowUpScheduleDto>.Ok(MapFollowUp(followUp));
    }

    public async Task<ApiResponse<FollowUpScheduleDto>> LinkFollowUpAppointmentAsync(
        long followUpId,
        LinkFollowUpAppointmentRequest request,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        if (!string.Equals(currentRole, UserRole.Patient, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Chỉ bệnh nhân mới được liên kết lịch tái khám của mình.");
        }

        var followUp = await _dbContext.FollowUpSchedules
            .Include(entity => entity.TreatmentSession)
                .ThenInclude(session => session.TreatmentPlanItem)
                    .ThenInclude(item => item.TreatmentPlan)
            .FirstOrDefaultAsync(entity => entity.FollowUpId == followUpId, ct);
        if (followUp == null)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Không tìm thấy lịch tái khám.");
        }

        if (followUp.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUserId != currentUserId)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Bạn không có quyền cập nhật lịch tái khám của bệnh nhân khác.");
        }

        if (followUp.Status != FollowUpScheduleStatus.Pending || followUp.AppointmentId.HasValue)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Chỉ lịch tái khám Pending chưa liên kết mới được cập nhật.");
        }

        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(entity => entity.AppointmentId == request.AppointmentId, ct);
        if (appointment == null)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Không tìm thấy lịch hẹn.");
        }

        if (appointment.PatientUserId != currentUserId)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Lịch hẹn không thuộc tài khoản bệnh nhân hiện tại.");
        }

        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Rejected
            or AppointmentStatus.Withdrawn or AppointmentStatus.Expired or AppointmentStatus.NoShow)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Không thể liên kết lịch hẹn đã hủy hoặc kết thúc không thành công.");
        }

        followUp.AppointmentId = appointment.AppointmentId;
        followUp.Status = appointment.Status == AppointmentStatus.Completed
            ? FollowUpScheduleStatus.Completed
            : FollowUpScheduleStatus.Scheduled;
        await _dbContext.SaveChangesAsync(ct);

        var updated = await QueryFollowUps()
            .FirstAsync(entity => entity.FollowUpId == followUpId, ct);
        return ApiResponse<FollowUpScheduleDto>.Ok(MapFollowUp(updated));
    }

    private async Task<ApiResponse<ClinicalEntryDto>> GetClinicalEntryByIdAsync(long clinicalEntryId, long currentUserId, string currentRole, CancellationToken ct)
    {
        var entry = await _dbContext.ClinicalEntries
            .AsNoTracking()
            .Include(e => e.DentistUser)
                .ThenInclude(d => d.User)
            .Include(e => e.OdontogramEntries)
            .Include(e => e.ImagingRecords)
            .FirstOrDefaultAsync(e => e.ClinicalEntryId == clinicalEntryId, ct);

        if (entry == null)
        {
            return ApiResponse<ClinicalEntryDto>.Fail("Không tìm thấy nội dung bệnh án.");
        }

        var medicalRecord = await _dbContext.DentalMedicalRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.MedicalRecordId == entry.MedicalRecordId, ct);

        if (medicalRecord == null)
        {
            return ApiResponse<ClinicalEntryDto>.Fail("Không tìm thấy hồ sơ bệnh án liên quan.");
        }

        if (!CanReadPatientRecord(currentUserId, medicalRecord.PatientUserId, currentRole))
        {
            return ApiResponse<ClinicalEntryDto>.Fail("Bạn không có quyền xem bệnh án này.");
        }

        return ApiResponse<ClinicalEntryDto>.Ok(MapClinicalEntry(entry));
    }

    private async Task<ApiResponse<TreatmentPlanDto>> GetTreatmentPlanAsync(long planId, long currentUserId, string currentRole, CancellationToken ct)
    {
        var plan = await QueryTreatmentPlans()
            .FirstOrDefaultAsync(p => p.TreatmentPlanId == planId, ct);

        if (plan == null)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Không tìm thấy kế hoạch điều trị.");
        }

        if (!CanReadPatientRecord(currentUserId, plan.PatientUserId, currentRole))
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Bạn không có quyền xem kế hoạch này.");
        }

        return ApiResponse<TreatmentPlanDto>.Ok(MapTreatmentPlan(plan));
    }

    private IQueryable<TreatmentPlan> QueryTreatmentPlans() =>
        _dbContext.TreatmentPlans
            .AsNoTracking()
            .Include(p => p.PatientUser)
                .ThenInclude(p => p.User)
            .Include(p => p.Department)
            .Include(p => p.CreatedByDentistUser)
                .ThenInclude(d => d.User)
            .Include(p => p.TreatmentPlanItems)
                .ThenInclude(i => i.DepartmentService)
            .Include(p => p.TreatmentPlanItems)
                .ThenInclude(i => i.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
        ;

    private async Task<ApiResponse<TreatmentSessionDto>> GetTreatmentSessionAsync(long sessionId, long currentUserId, string currentRole, CancellationToken ct)
    {
        var session = await _dbContext.TreatmentSessions
            .AsNoTracking()
            .Include(s => s.DentistUser)
                .ThenInclude(d => d.User)
            .Include(s => s.PerformedServices)
                .ThenInclude(ps => ps.DepartmentService)
            .Include(s => s.TreatmentPlanItem)
                .ThenInclude(item => item.TreatmentPlan)
            .FirstOrDefaultAsync(s => s.TreatmentSessionId == sessionId, ct);

        if (session == null)
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Không tìm thấy ca điều trị.");
        }

        if (!CanReadPatientRecord(currentUserId, session.TreatmentPlanItem.TreatmentPlan.PatientUserId, currentRole))
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Bạn không có quyền xem ca điều trị này.");
        }

        return ApiResponse<TreatmentSessionDto>.Ok(MapTreatmentSession(session));
    }

    private async Task<ApiResponse<PrescriptionDto>> GetPrescriptionAsync(long prescriptionId, long currentUserId, string currentRole, CancellationToken ct)
    {
        var prescription = await QueryPrescriptions()
            .FirstOrDefaultAsync(p => p.PrescriptionId == prescriptionId, ct);

        if (prescription == null)
        {
            return ApiResponse<PrescriptionDto>.Fail("Không tìm thấy đơn thuốc.");
        }

        if (!CanReadPatientRecord(currentUserId, prescription.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUserId, currentRole))
        {
            return ApiResponse<PrescriptionDto>.Fail("Bạn không có quyền xem đơn thuốc này.");
        }

        return ApiResponse<PrescriptionDto>.Ok(MapPrescription(prescription));
    }

    private IQueryable<Prescription> QueryPrescriptions() =>
        _dbContext.Prescriptions
            .AsNoTracking()
            .Include(p => p.PrescriptionItems)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.TreatmentPlanItem)
                    .ThenInclude(item => item.TreatmentPlan)
        ;

    private IQueryable<DepartmentReferral> QueryReferrals() =>
        _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(referral => referral.FromDepartment)
            .Include(referral => referral.ToDepartment)
            .Include(referral => referral.AssignedDentistUser)
                .ThenInclude(dentist => dentist!.User)
            .Include(referral => referral.TreatmentSession)
                .ThenInclude(session => session.TreatmentPlanItem)
                    .ThenInclude(item => item.TreatmentPlan);

    private IQueryable<FollowUpSchedule> QueryFollowUps() =>
        _dbContext.FollowUpSchedules
            .AsNoTracking()
            .Include(followUp => followUp.Appointment)
            .Include(followUp => followUp.TreatmentSession)
                .ThenInclude(session => session.TreatmentPlanItem)
                    .ThenInclude(item => item.TreatmentPlan);

    private static bool CanManageReferralDepartment(Department department, long currentUserId, string currentRole) =>
        IsSystemAdministrator(currentRole)
        || (IsDepartmentManager(currentRole) && department.ManagerUserId == currentUserId);

    private async Task<bool> CanManageTreatmentPlanAsync(
        TreatmentPlan plan,
        TreatmentPlanItem item,
        long currentUserId,
        string currentRole,
        CancellationToken ct)
    {
        if (IsSystemAdministrator(currentRole))
        {
            return true;
        }

        if (IsDepartmentManager(currentRole))
        {
            return await _dbContext.Departments.AnyAsync(
                department => department.DepartmentId == plan.DepartmentId
                    && department.ManagerUserId == currentUserId, ct);
        }

        if (!IsDentist(currentRole))
        {
            return false;
        }

        return plan.CreatedByDentistUserId == currentUserId
            || item.AssignedDentistUserId == currentUserId
            || await _dbContext.TreatmentSessions.AnyAsync(
                session => session.TreatmentPlanItemId == item.TreatmentPlanItemId
                    && session.DentistUserId == currentUserId, ct);
    }

    private static bool CanReadPatientRecord(long currentUserId, long patientUserId, string currentRole)
    {
        if (IsDentist(currentRole) || IsDepartmentManager(currentRole) || IsReceptionist(currentRole) || IsSystemAdministrator(currentRole))
        {
            return true;
        }

        return currentUserId == patientUserId;
    }

    private static bool CanWriteClinicalEntry(long currentUserId, long patientUserId, string currentRole)
    {
        if (IsDentist(currentRole) || IsDepartmentManager(currentRole))
        {
            return true;
        }

        return currentUserId == patientUserId;
    }

    private static bool IsDentist(string role) => string.Equals(role, UserRole.Dentist, StringComparison.OrdinalIgnoreCase);
    private static bool IsDepartmentManager(string role) => string.Equals(role, UserRole.DepartmentManager, StringComparison.OrdinalIgnoreCase);
    private static bool IsReceptionist(string role) => string.Equals(role, UserRole.Receptionist, StringComparison.OrdinalIgnoreCase);
    private static bool IsSystemAdministrator(string role) => string.Equals(role, UserRole.SystemAdministrator, StringComparison.OrdinalIgnoreCase);

    private static DentalMedicalRecordDto MapMedicalRecord(DentalMedicalRecord record)
    {
        var dto = new DentalMedicalRecordDto
        {
            MedicalRecordId = record.MedicalRecordId,
            PatientUserId = record.PatientUserId,
            PatientName = record.PatientUser?.FullName ?? string.Empty,
            PatientCode = record.PatientUser?.PatientCode ?? string.Empty,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
            ClinicalEntries = record.ClinicalEntries.Select(MapClinicalEntry).ToList()
        };

        return dto;
    }

    private static ClinicalEntryDto MapClinicalEntry(ClinicalEntry entry)
    {
        return new ClinicalEntryDto
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            MedicalRecordId = entry.MedicalRecordId,
            AppointmentId = entry.AppointmentId,
            DentistUserId = entry.DentistUserId,
            DentistName = entry.DentistUser?.User?.FullName ?? string.Empty,
            Symptoms = entry.Symptoms,
            ClinicalFindings = entry.ClinicalFindings,
            Diagnosis = entry.Diagnosis,
            DentalConditions = entry.DentalConditions,
            ClinicalNotes = entry.ClinicalNotes,
            CreatedAt = entry.CreatedAt,
            OdontogramEntries = entry.OdontogramEntries.Select(o => new OdontogramEntryDto
            {
                OdontogramEntryId = o.OdontogramEntryId,
                ToothNumber = o.ToothNumber,
                Surface = o.Surface,
                Condition = o.Condition,
                Note = o.Note
            }).ToList(),
            ImagingRecords = entry.ImagingRecords.Select(i => new ImagingRecordDto
            {
                ImagingRecordId = i.ImagingRecordId,
                ImagingType = i.ImagingType,
                FileUrl = i.FileUrl,
                ResultSummary = i.ResultSummary,
                CreatedAt = i.CreatedAt
            }).ToList()
        };
    }

    private static TreatmentPlanDto MapTreatmentPlan(TreatmentPlan plan)
    {
        return new TreatmentPlanDto
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            PatientUserId = plan.PatientUserId,
            PatientName = plan.PatientUser?.FullName ?? string.Empty,
            DepartmentId = plan.DepartmentId,
            DepartmentName = plan.Department?.Name ?? string.Empty,
            CreatedByDentistUserId = plan.CreatedByDentistUserId,
            CreatedByDentistName = plan.CreatedByDentistUser?.User?.FullName ?? string.Empty,
            Status = plan.Status,
            EstimatedTotal = plan.EstimatedTotal,
            CreatedAt = plan.CreatedAt,
            Items = plan.TreatmentPlanItems.Select(item => new TreatmentPlanItemDto
            {
                TreatmentPlanItemId = item.TreatmentPlanItemId,
                DepartmentServiceId = item.DepartmentServiceId,
                ServiceName = item.DepartmentService?.ServiceName ?? string.Empty,
                AssignedDentistUserId = item.AssignedDentistUserId,
                AssignedDentistName = item.AssignedDentistUser?.User?.FullName ?? string.Empty,
                ToothNumber = item.ToothNumber,
                SequenceNo = item.SequenceNo,
                ExpectedDate = item.ExpectedDate.HasValue ? item.ExpectedDate.Value.ToDateTime(TimeOnly.MinValue) : null,
                EstimatedCost = item.EstimatedCost,
                Status = item.Status,
                Note = item.Note
            }).ToList()
        };
    }

    private static TreatmentSessionDto MapTreatmentSession(TreatmentSession session)
    {
        return new TreatmentSessionDto
        {
            TreatmentSessionId = session.TreatmentSessionId,
            TreatmentPlanItemId = session.TreatmentPlanItemId,
            AppointmentId = session.AppointmentId,
            DentistUserId = session.DentistUserId,
            DentistName = session.DentistUser?.User?.FullName ?? string.Empty,
            SessionDate = session.SessionDate,
            TreatmentResult = session.TreatmentResult,
            ClinicalNote = session.ClinicalNote,
            PerformedServices = session.PerformedServices.Select(ps => new PerformedServiceDto
            {
                PerformedServiceId = ps.PerformedServiceId,
                DepartmentServiceId = ps.DepartmentServiceId,
                ServiceName = ps.DepartmentService?.ServiceName ?? string.Empty,
                Quantity = ps.Quantity,
                UnitPrice = ps.UnitPrice,
                Amount = ps.Amount ?? (ps.Quantity * ps.UnitPrice)
            }).ToList()
        };
    }

    private static PrescriptionDto MapPrescription(Prescription prescription)
    {
        return new PrescriptionDto
        {
            PrescriptionId = prescription.PrescriptionId,
            TreatmentSessionId = prescription.TreatmentSessionId,
            GeneralInstructions = prescription.GeneralInstructions,
            IssuedAt = prescription.IssuedAt,
            Items = prescription.PrescriptionItems.Select(item => new PrescriptionItemDto
            {
                PrescriptionItemId = item.PrescriptionItemId,
                MedicationName = item.MedicationName,
                Dosage = item.Dosage,
                Frequency = item.Frequency,
                Duration = item.Duration,
                Instructions = item.Instructions
            }).ToList()
        };
    }

    private static DepartmentReferralDto MapReferral(DepartmentReferral referral)
    {
        return new DepartmentReferralDto
        {
            ReferralId = referral.ReferralId,
            TreatmentSessionId = referral.TreatmentSessionId,
            FromDepartmentId = referral.FromDepartmentId,
            FromDepartmentName = referral.FromDepartment?.Name ?? string.Empty,
            ToDepartmentId = referral.ToDepartmentId,
            ToDepartmentName = referral.ToDepartment?.Name ?? string.Empty,
            Reason = referral.Reason,
            Status = referral.Status,
            AssignedDentistUserId = referral.AssignedDentistUserId,
            AssignedDentistName = referral.AssignedDentistUser?.User?.FullName,
            ReviewedByUserId = referral.ReviewedByUserId,
            ReviewedAt = referral.ReviewedAt,
            CreatedAt = referral.CreatedAt
        };
    }

    private static FollowUpScheduleDto MapFollowUp(FollowUpSchedule followUp)
    {
        return new FollowUpScheduleDto
        {
            FollowUpId = followUp.FollowUpId,
            TreatmentSessionId = followUp.TreatmentSessionId,
            RecommendedDate = followUp.RecommendedDate,
            Reason = followUp.Reason,
            Status = followUp.Status,
            AppointmentId = followUp.AppointmentId,
            AppointmentCode = followUp.Appointment?.AppointmentCode
        };
    }
}
