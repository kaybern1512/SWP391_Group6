using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Treatments.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

namespace DentalClinic.Infrastructure.Services;

public class TreatmentService : ITreatmentService
{
    private readonly DentalClinicDbContext _dbContext;

    public TreatmentService(DentalClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<List<TreatmentPlanDto>>> GetDentistTreatmentPlansAsync(
        long dentistId, string? status, string? search, CancellationToken ct = default)
    {
        var query = _dbContext.TreatmentPlans
            .AsNoTracking()
            .Include(tp => tp.Patient)
                .ThenInclude(p => p.User)
            .Include(tp => tp.CreatedByDentist)
                .ThenInclude(d => d.StaffProfile)
                    .ThenInclude(s => s.User)
            .Include(tp => tp.CreatedByDentist)
                .ThenInclude(d => d.Department)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.DepartmentService)
            .Include(tp => tp.TreatmentSessions)
                .ThenInclude(ts => ts.Dentist)
                    .ThenInclude(d => d.StaffProfile)
                        .ThenInclude(s => s.User)
            .Include(tp => tp.TreatmentSessions)
                .ThenInclude(ts => ts.PerformedServices)
                    .ThenInclude(ps => ps.DepartmentService)
            .AsQueryable();

        if (dentistId > 0)
        {
            query = query.Where(tp => tp.CreatedByDentistUserId == dentistId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(tp => tp.Status == status);
        }
        else
        {
            query = query.Where(tp => !tp.TreatmentPlanItems.Any(tpi => tpi.Notes != null && tpi.Notes.StartsWith("[DirectCare]")));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(tp =>
                tp.Patient.FullName.ToLower().Contains(s) ||
                (tp.Patient.User.PhoneNumber != null && tp.Patient.User.PhoneNumber.Contains(s)) ||
                (tp.Title != null && tp.Title.ToLower().Contains(s)));
        }

        var plans = await query
            .OrderByDescending(tp => tp.CreatedAt)
            .ToListAsync(ct);

        var result = plans.Select(MapToDto).ToList();
        return ApiResponse<List<TreatmentPlanDto>>.Ok(result);
    }

    public async Task<ApiResponse<TreatmentPlanDto>> GetTreatmentPlanByIdAsync(long planId, CancellationToken ct = default)
    {
        var plan = await _dbContext.TreatmentPlans
            .AsNoTracking()
            .AsSplitQuery()
            .Include(tp => tp.Patient)
                .ThenInclude(p => p.User)
            .Include(tp => tp.CreatedByDentist)
                .ThenInclude(d => d.StaffProfile)
                    .ThenInclude(s => s.User)
            .Include(tp => tp.CreatedByDentist)
                .ThenInclude(d => d.Department)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.DepartmentService)
            .Include(tp => tp.TreatmentSessions)
                .ThenInclude(ts => ts.Dentist)
                    .ThenInclude(d => d.StaffProfile)
                        .ThenInclude(s => s.User)
            .Include(tp => tp.TreatmentSessions)
                .ThenInclude(ts => ts.Appointment)
            .Include(tp => tp.TreatmentSessions)
                .ThenInclude(ts => ts.PerformedServices)
                    .ThenInclude(ps => ps.DepartmentService)
            .FirstOrDefaultAsync(tp => tp.TreatmentPlanId == planId, ct);

        if (plan == null)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Không tìm thấy kế hoạch điều trị.");
        }

        return ApiResponse<TreatmentPlanDto>.Ok(MapToDto(plan));
    }

    public async Task<ApiResponse<TreatmentPlanDto>> CreateTreatmentPlanAsync(
        long dentistUserId, CreateTreatmentPlanRequest request, CancellationToken ct = default)
    {
        if (request.PatientUserId <= 0)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Vui lòng chọn bệnh nhân.");
        }

        var patient = await _dbContext.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == request.PatientUserId, ct);
        if (patient == null)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Không tìm thấy thông tin bệnh nhân.");
        }

        var dentist = await _dbContext.DentistProfiles.FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);
        if (dentist == null)
        {
            return ApiResponse<TreatmentPlanDto>.Fail("Không tìm thấy thông tin bác sĩ điều trị.");
        }

        var now = DateTime.UtcNow;
        var medRecord = await _dbContext.DentalMedicalRecords
            .FirstOrDefaultAsync(r => r.PatientUserId == request.PatientUserId, ct);
        if (medRecord == null)
        {
            medRecord = new DentalMedicalRecord
            {
                PatientUserId = request.PatientUserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.DentalMedicalRecords.Add(medRecord);
            await _dbContext.SaveChangesAsync(ct);
        }

        var plan = new TreatmentPlan
        {
            MedicalRecordId = medRecord.RecordId,
            PatientUserId = request.PatientUserId,
            CreatedByDentistUserId = dentistUserId,
            Title = "Phác đồ điều trị nha khoa",
            Status = !string.IsNullOrWhiteSpace(request.Status) ? request.Status : "Draft",
            CreatedAt = now,
            UpdatedAt = now,
            EstimatedTotal = 0
        };

        _dbContext.TreatmentPlans.Add(plan);
        await _dbContext.SaveChangesAsync(ct);

        decimal totalEstimated = 0;
        if (request.Items != null && request.Items.Any())
        {
            int seq = 1;
            foreach (var itemInput in request.Items)
            {
                var est = itemInput.EstimatedCost ?? 0m;
                var item = new TreatmentPlanItem
                {
                    TreatmentPlanId = plan.TreatmentPlanId,
                    DepartmentServiceId = (int)(itemInput.DepartmentServiceId ?? 1),
                    ToothNumber = itemInput.ToothNumber,
                    SequenceNo = itemInput.SequenceNo > 0 ? itemInput.SequenceNo : seq++,
                    EstimatedCost = est,
                    Status = !string.IsNullOrWhiteSpace(itemInput.Status) ? itemInput.Status : "Planned",
                    Notes = itemInput.Note,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                totalEstimated += est;
                _dbContext.TreatmentPlanItems.Add(item);
            }

            plan.EstimatedTotal = totalEstimated;
            await _dbContext.SaveChangesAsync(ct);
        }

        return await GetTreatmentPlanByIdAsync(plan.TreatmentPlanId, ct);
    }

    public async Task<ApiResponse<TreatmentPlanItemDto>> AddTreatmentPlanItemAsync(
        long planId, CreateTreatmentPlanItemInput input, CancellationToken ct = default)
    {
        var plan = await _dbContext.TreatmentPlans
            .Include(p => p.TreatmentPlanItems)
            .FirstOrDefaultAsync(p => p.TreatmentPlanId == planId, ct);

        if (plan == null)
        {
            return ApiResponse<TreatmentPlanItemDto>.Fail("Không tìm thấy kế hoạch điều trị.");
        }

        int maxSeq = plan.TreatmentPlanItems.Any() ? plan.TreatmentPlanItems.Max(i => i.SequenceNo) : 0;
        int nextSeq = input.SequenceNo > 0 ? input.SequenceNo : (maxSeq + 1);
        var now = DateTime.UtcNow;
        var est = input.EstimatedCost ?? 0m;

        var item = new TreatmentPlanItem
        {
            TreatmentPlanId = planId,
            DepartmentServiceId = (int)(input.DepartmentServiceId ?? 1),
            ToothNumber = input.ToothNumber,
            SequenceNo = nextSeq,
            EstimatedCost = est,
            Status = !string.IsNullOrWhiteSpace(input.Status) ? input.Status : "Planned",
            Notes = input.Note,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.TreatmentPlanItems.Add(item);

        plan.EstimatedTotal += est;
        plan.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(ct);

        var createdItem = await _dbContext.TreatmentPlanItems
            .AsNoTracking()
            .Include(i => i.DepartmentService)
            .FirstOrDefaultAsync(i => i.PlanItemId == item.PlanItemId, ct);

        return ApiResponse<TreatmentPlanItemDto>.Ok(MapItemToDto(createdItem!, plan, new List<TreatmentSession>()));
    }

    public async Task<ApiResponse<bool>> UpdateTreatmentPlanStatusAsync(
        long planId, string status, CancellationToken ct = default)
    {
        var plan = await _dbContext.TreatmentPlans.FirstOrDefaultAsync(p => p.TreatmentPlanId == planId, ct);
        if (plan == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy kế hoạch điều trị.");
        }

        plan.Status = status;
        plan.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> UpdateTreatmentPlanItemStatusAsync(
        long itemId, string status, CancellationToken ct = default)
    {
        var item = await _dbContext.TreatmentPlanItems
            .Include(i => i.TreatmentPlan)
                .ThenInclude(p => p.TreatmentPlanItems)
            .FirstOrDefaultAsync(i => i.PlanItemId == itemId, ct);

        if (item == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy giai đoạn điều trị.");
        }

        item.Status = status;
        item.UpdatedAt = DateTime.UtcNow;
        item.TreatmentPlan.UpdatedAt = DateTime.UtcNow;

        if (item.TreatmentPlan.TreatmentPlanItems.All(i => i.Status == "Completed"))
        {
            item.TreatmentPlan.Status = "Completed";
        }

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<List<TreatmentSessionDto>>> GetDentistTreatmentSessionsAsync(
        long dentistId, string? search, CancellationToken ct = default)
    {
        var query = _dbContext.TreatmentSessions
            .AsNoTracking()
            .Include(ts => ts.Dentist)
                .ThenInclude(d => d.StaffProfile)
                    .ThenInclude(s => s.User)
            .Include(ts => ts.Appointment)
            .Include(ts => ts.Patient)
                .ThenInclude(p => p.User)
            .Include(ts => ts.PerformedServices)
                .ThenInclude(ps => ps.DepartmentService)
            .AsQueryable();

        if (dentistId > 0)
        {
            query = query.Where(ts => ts.DentistUserId == dentistId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(ts =>
                ts.Patient.FullName.ToLower().Contains(s) ||
                (ts.Notes != null && ts.Notes.ToLower().Contains(s)) ||
                (ts.ClinicalObservations != null && ts.ClinicalObservations.ToLower().Contains(s)));
        }

        var sessions = await query
            .OrderByDescending(ts => ts.SessionDate)
            .ToListAsync(ct);

        var result = sessions.Select(MapSessionToDto).ToList();
        return ApiResponse<List<TreatmentSessionDto>>.Ok(result);
    }

    public async Task<ApiResponse<TreatmentSessionDto>> CreateTreatmentSessionAsync(
        long dentistUserId, CreateTreatmentSessionRequest request, CancellationToken ct = default)
    {
        var item = await _dbContext.TreatmentPlanItems
            .Include(i => i.TreatmentPlan)
                .ThenInclude(p => p.TreatmentPlanItems)
            .FirstOrDefaultAsync(i => i.PlanItemId == request.TreatmentPlanItemId, ct);

        if (item == null)
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Không tìm thấy giai đoạn điều trị cần ghi nhận.");
        }

        long appointmentId = request.AppointmentId;
        if (appointmentId <= 0)
        {
            var latestAppt = await _dbContext.Appointments
                .Where(a => a.PatientUserId == item.TreatmentPlan.PatientUserId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => a.AppointmentId)
                .FirstOrDefaultAsync(ct);

            if (latestAppt > 0)
            {
                appointmentId = latestAppt;
            }
            else
            {
                return ApiResponse<TreatmentSessionDto>.Fail("Vui lòng chỉ định ca khám (Appointment) tương ứng.");
            }
        }

        var appt = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);
        if (appt == null)
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Không tìm thấy ca khám tương ứng.");
        }

        var primaryItem = appt.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
        if (primaryItem == null)
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Ca khám chưa có dịch vụ liên kết.");
        }

        var now = DateTime.UtcNow;
        var sessionDate = request.SessionDate != default
            ? DateOnly.FromDateTime(request.SessionDate)
            : DateOnly.FromDateTime(now);

        var session = new TreatmentSession
        {
            AppointmentId = appointmentId,
            AppointmentServiceId = primaryItem.AppointmentServiceId,
            PatientUserId = appt.PatientUserId,
            TreatmentPlanId = item.TreatmentPlanId,
            DentistUserId = dentistUserId,
            SessionDate = sessionDate,
            Notes = request.ClinicalNote,
            ClinicalObservations = request.TreatmentResult ?? "Tiến triển tốt",
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.TreatmentSessions.Add(session);
        await _dbContext.SaveChangesAsync(ct);

        if (request.Services != null && request.Services.Any())
        {
            foreach (var svc in request.Services)
            {
                if (svc.DepartmentServiceId > 0)
                {
                    var perf = new PerformedService
                    {
                        TreatmentSessionId = session.SessionId,
                        AppointmentId = appointmentId,
                        AppointmentServiceId = primaryItem.AppointmentServiceId,
                        DepartmentServiceId = (int)svc.DepartmentServiceId,
                        Quantity = svc.Quantity > 0 ? svc.Quantity : 1,
                        UnitPrice = svc.UnitPrice,
                        CreatedAt = now
                    };
                    _dbContext.PerformedServices.Add(perf);
                }
            }
            await _dbContext.SaveChangesAsync(ct);
        }

        if (!string.IsNullOrWhiteSpace(request.MarkPhaseStatus))
        {
            item.Status = request.MarkPhaseStatus;
            item.UpdatedAt = now;
            item.TreatmentPlan.UpdatedAt = now;

            if (item.TreatmentPlan.TreatmentPlanItems.All(i => i.Status == "Completed"))
            {
                item.TreatmentPlan.Status = "Completed";
            }
            else if (item.TreatmentPlan.Status == "Draft")
            {
                item.TreatmentPlan.Status = "InProgress";
            }

            await _dbContext.SaveChangesAsync(ct);
        }

        var loadedSession = await _dbContext.TreatmentSessions
            .AsNoTracking()
            .Include(ts => ts.Dentist)
                .ThenInclude(d => d.StaffProfile)
                    .ThenInclude(s => s.User)
            .Include(ts => ts.Appointment)
            .Include(ts => ts.Patient)
                .ThenInclude(p => p.User)
            .Include(ts => ts.PerformedServices)
                .ThenInclude(ps => ps.DepartmentService)
            .FirstOrDefaultAsync(ts => ts.SessionId == session.SessionId, ct);

        return ApiResponse<TreatmentSessionDto>.Ok(MapSessionToDto(loadedSession!));
    }

    public async Task<ApiResponse<TreatmentProgressSummaryDto>> GetDepartmentTreatmentProgressAsync(
        long managerUserId, long? departmentId, string? search, CancellationToken ct = default)
    {
        var query = _dbContext.TreatmentPlans
            .AsNoTracking()
            .Include(tp => tp.Patient)
                .ThenInclude(p => p.User)
            .Include(tp => tp.CreatedByDentist)
                .ThenInclude(d => d.StaffProfile)
                    .ThenInclude(s => s.User)
            .Include(tp => tp.CreatedByDentist)
                .ThenInclude(d => d.Department)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.DepartmentService)
            .AsQueryable();

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(tp => tp.CreatedByDentist.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(tp =>
                tp.Patient.FullName.ToLower().Contains(s) ||
                tp.CreatedByDentist.Department.Name.ToLower().Contains(s));
        }

        var plans = await query
            .OrderByDescending(tp => tp.CreatedAt)
            .ToListAsync(ct);

        var dtoList = plans.Select(MapToDto).ToList();

        var summary = new TreatmentProgressSummaryDto
        {
            TotalPlans = dtoList.Count,
            ActivePlans = dtoList.Count(p => p.Status == "InProgress" || p.Status == "Planned" || p.Status == "Active"),
            CompletedPlans = dtoList.Count(p => p.Status == "Completed"),
            PendingPhases = dtoList.Sum(p => p.Items.Count(i => i.Status != "Completed")),
            Plans = dtoList
        };

        return ApiResponse<TreatmentProgressSummaryDto>.Ok(summary);
    }

    public async Task<ApiResponse<bool>> AssignPhaseDentistAsync(
        long itemId, long managerUserId, long dentistUserId, CancellationToken ct = default)
    {
        var item = await _dbContext.TreatmentPlanItems
            .Include(i => i.TreatmentPlan)
            .FirstOrDefaultAsync(i => i.PlanItemId == itemId, ct);

        if (item == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy giai đoạn điều trị.");
        }

        var dentist = await _dbContext.DentistProfiles.FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);
        if (dentist == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy thông tin bác sĩ được chỉ định.");
        }

        item.TreatmentPlan.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<List<TreatmentPlanDto>>> GetPatientTreatmentPlansAsync(
        long patientUserId, CancellationToken ct = default)
    {
        var plans = await _dbContext.TreatmentPlans
            .AsNoTracking()
            .AsSplitQuery()
            .Include(tp => tp.Patient)
                .ThenInclude(p => p.User)
            .Include(tp => tp.CreatedByDentist)
                .ThenInclude(d => d.StaffProfile)
                    .ThenInclude(s => s.User)
            .Include(tp => tp.CreatedByDentist)
                .ThenInclude(d => d.Department)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.DepartmentService)
            .Include(tp => tp.TreatmentSessions)
                .ThenInclude(ts => ts.Dentist)
                    .ThenInclude(d => d.StaffProfile)
                        .ThenInclude(s => s.User)
            .Include(tp => tp.TreatmentSessions)
                .ThenInclude(ts => ts.PerformedServices)
                    .ThenInclude(ps => ps.DepartmentService)
            .Where(tp => tp.PatientUserId == patientUserId &&
                         !tp.TreatmentPlanItems.Any(tpi => tpi.Notes != null && tpi.Notes.StartsWith("[DirectCare]")))
            .OrderByDescending(tp => tp.CreatedAt)
            .ThenBy(tp => tp.TreatmentPlanId)
            .ToListAsync(ct);

        return ApiResponse<List<TreatmentPlanDto>>.Ok(plans.Select(MapToDto).ToList());
    }

    public async Task<ApiResponse<List<TreatmentPatientOptionDto>>> GetTreatmentPatientOptionsAsync(CancellationToken ct = default)
    {
        var patients = await _dbContext.PatientProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .OrderBy(p => p.FullName)
            .Select(p => new TreatmentPatientOptionDto
            {
                PatientUserId = p.UserId,
                FullName = p.FullName,
                PatientCode = $"BN-{p.UserId:D4}",
                PhoneNumber = p.User.PhoneNumber
            })
            .ToListAsync(ct);

        return ApiResponse<List<TreatmentPatientOptionDto>>.Ok(patients);
    }

    public async Task<ApiResponse<List<TreatmentServiceOptionDto>>> GetTreatmentServiceOptionsAsync(
        long? departmentId, CancellationToken ct = default)
    {
        var query = _dbContext.DepartmentServices
            .AsNoTracking()
            .Include(ds => ds.Department)
            .Where(ds => ds.Status == "Active");

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(ds => ds.DepartmentId == departmentId.Value);
        }

        var list = await query
            .OrderBy(ds => ds.Department.Name)
            .ThenBy(ds => ds.ServiceName)
            .Select(ds => new TreatmentServiceOptionDto
            {
                DepartmentServiceId = ds.DepartmentServiceId,
                DepartmentId = ds.DepartmentId,
                DepartmentName = ds.Department.Name,
                ServiceName = ds.ServiceName,
                Price = ds.Price
            })
            .ToListAsync(ct);

        return ApiResponse<List<TreatmentServiceOptionDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<TreatmentDentistOptionDto>>> GetTreatmentDentistOptionsAsync(
        long? departmentId, CancellationToken ct = default)
    {
        var query = _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.StaffProfile)
                .ThenInclude(s => s.User)
            .AsQueryable();

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(d => d.DepartmentId == departmentId.Value);
        }

        var dentists = await query
            .OrderBy(d => d.StaffProfile.FullName)
            .Select(d => new TreatmentDentistOptionDto
            {
                DentistUserId = d.UserId,
                FullName = d.StaffProfile.FullName,
                Specialization = d.Qualification,
                DepartmentId = d.DepartmentId
            })
            .ToListAsync(ct);

        return ApiResponse<List<TreatmentDentistOptionDto>>.Ok(dentists);
    }

    public async Task<ApiResponse<List<TreatmentEligiblePhaseDto>>> GetEligiblePhasesForSessionAsync(
        long dentistUserId, CancellationToken ct = default)
    {
        var items = await _dbContext.TreatmentPlanItems
            .AsNoTracking()
            .Include(tpi => tpi.DepartmentService)
            .Include(tpi => tpi.TreatmentPlan)
                .ThenInclude(tp => tp.Patient)
                    .ThenInclude(p => p.User)
            .Include(tpi => tpi.TreatmentPlan)
                .ThenInclude(tp => tp.Patient)
                    .ThenInclude(p => p.Appointments)
            .Where(tpi => tpi.TreatmentPlan.Status != "Cancelled")
            .OrderBy(tpi => tpi.TreatmentPlan.Patient.FullName)
            .ThenBy(tpi => tpi.SequenceNo)
            .ToListAsync(ct);

        var result = items.Select(i =>
        {
            var latestAppt = i.TreatmentPlan.Patient?.Appointments
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();

            return new TreatmentEligiblePhaseDto
            {
                TreatmentPlanItemId = i.PlanItemId,
                TreatmentPlanId = i.TreatmentPlanId,
                PatientUserId = i.TreatmentPlan.PatientUserId,
                PatientName = i.TreatmentPlan.Patient?.FullName ?? "N/A",
                PatientCode = $"BN-{i.TreatmentPlan.PatientUserId:D4}",
                SequenceNo = i.SequenceNo,
                ServiceName = i.DepartmentService?.ServiceName ?? "Dịch vụ nha khoa",
                ToothNumber = i.ToothNumber,
                PhaseStatus = i.Status,
                EstimatedCost = i.EstimatedCost,
                AssociatedAppointmentId = latestAppt?.AppointmentId,
                AssociatedAppointmentCode = latestAppt != null ? $"APT-{latestAppt.AppointmentId:D6}" : null
            };
        }).ToList();

        return ApiResponse<List<TreatmentEligiblePhaseDto>>.Ok(result);
    }

    public async Task<ApiResponse<AppointmentDto>> CreatePlanFollowUpAppointmentAsync(
        long dentistUserId,
        long planId,
        CreatePlanFollowUpAppointmentRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<AppointmentDto>.Fail("Dữ liệu yêu cầu tái khám không hợp lệ.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<AppointmentDto>.Fail("Vui lòng nhập lý do / chỉ định tái khám.");

        if (request.ScheduledStart <= DateTime.UtcNow)
            return ApiResponse<AppointmentDto>.Fail("Thời gian tái khám phải là thời điểm trong tương lai.");

        var plan = await _dbContext.TreatmentPlans
            .Include(tp => tp.Patient)
                .ThenInclude(p => p.User)
            .Include(tp => tp.CreatedByDentist)
                .ThenInclude(d => d.Department)
            .Include(tp => tp.TreatmentPlanItems)
            .FirstOrDefaultAsync(tp => tp.TreatmentPlanId == planId, ct);

        if (plan == null)
            return ApiResponse<AppointmentDto>.Fail("Không tìm thấy phác đồ điều trị chỉ định.");

        var proposedEnd = request.ScheduledStart.AddMinutes(request.DurationMinutes ?? 30);
        var targetDentistId = request.RequestedDentistUserId ?? dentistUserId;

        int deptId = plan.CreatedByDentist?.DepartmentId ?? 1;
        int serviceId = 1;
        decimal unitPrice = 0m;

        if (request.PlanItemId.HasValue)
        {
            var item = plan.TreatmentPlanItems.FirstOrDefault(i => i.PlanItemId == request.PlanItemId.Value);
            if (item != null)
            {
                serviceId = item.DepartmentServiceId;
                unitPrice = item.EstimatedCost;
            }
        }
        else if (plan.TreatmentPlanItems.Any())
        {
            var first = plan.TreatmentPlanItems.OrderBy(i => i.SequenceNo).First();
            serviceId = first.DepartmentServiceId;
            unitPrice = first.EstimatedCost;
        }

        var now = DateTime.UtcNow;
        var appt = new Appointment
        {
            PatientUserId = plan.PatientUserId,
            ReasonForVisit = $"[Tái khám phác đồ #{plan.TreatmentPlanId}] {request.Reason.Trim()}",
            BookingSource = BookingSource.Receptionist,
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedByUserId = dentistUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Appointments.Add(appt);
        await _dbContext.SaveChangesAsync(ct);

        var apptItem = new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = deptId,
            DepartmentServiceId = serviceId,
            TreatmentPlanItemId = request.PlanItemId,
            RequestedDentistUserId = targetDentistId,
            AssignedDentistUserId = targetDentistId,
            ServiceDate = DateOnly.FromDateTime(request.ScheduledStart),
            ScheduledStart = request.ScheduledStart,
            ScheduledEnd = proposedEnd,
            UnitPrice = unitPrice,
            Status = AppointmentStatus.PendingReceptionReview,
            Notes = request.Reason.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.AppointmentServices.Add(apptItem);
        await _dbContext.SaveChangesAsync(ct);

        var latestSession = await _dbContext.TreatmentSessions
            .Where(s => s.TreatmentPlanId == planId)
            .OrderByDescending(s => s.SessionDate)
            .FirstOrDefaultAsync(ct);

        if (latestSession != null)
        {
            var followUp = new FollowUpSchedule
            {
                TreatmentSessionId = latestSession.SessionId,
                DentistUserId = dentistUserId,
                PatientUserId = plan.PatientUserId,
                RecommendedDate = DateOnly.FromDateTime(request.ScheduledStart),
                RecommendedServiceId = serviceId,
                ClinicalReason = request.Reason.Trim(),
                Status = "Scheduled",
                ResultingAppointmentId = appt.AppointmentId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.FollowUpSchedules.Add(followUp);
            await _dbContext.SaveChangesAsync(ct);
        }

        var dto = new AppointmentDto
        {
            AppointmentId = appt.AppointmentId,
            AppointmentCode = $"APT-{appt.AppointmentId:D6}",
            PatientUserId = appt.PatientUserId,
            PatientName = plan.Patient?.FullName ?? string.Empty,
            DepartmentId = deptId,
            DepartmentName = plan.CreatedByDentist?.Department?.Name,
            RequestedServiceId = serviceId,
            RequestedDentistUserId = targetDentistId,
            AssignedDentistUserId = targetDentistId,
            ScheduledStart = request.ScheduledStart,
            ScheduledEnd = proposedEnd,
            ReasonForVisit = appt.ReasonForVisit ?? string.Empty,
            BookingSource = appt.BookingSource,
            Status = appt.Status,
            CreatedAt = appt.CreatedAt
        };

        return ApiResponse<AppointmentDto>.Ok(dto, "Lập lịch hẹn tái khám từ phác đồ thành công.");
    }

    public async Task<ApiResponse<List<PerformedServiceDto>>> GetPerformedServicesByAppointmentAsync(long appointmentId, CancellationToken ct = default)
    {
        var services = await _dbContext.PerformedServices
            .AsNoTracking()
            .Include(ps => ps.DepartmentService)
            .Where(ps => ps.AppointmentId == appointmentId)
            .OrderBy(ps => ps.PerformedServiceId)
            .Select(ps => new PerformedServiceDto
            {
                PerformedServiceId = ps.PerformedServiceId,
                TreatmentSessionId = ps.TreatmentSessionId,
                DepartmentServiceId = ps.DepartmentServiceId,
                ServiceName = ps.DepartmentService.ServiceName,
                Quantity = ps.Quantity,
                UnitPrice = ps.UnitPrice,
                Amount = ps.Quantity * ps.UnitPrice
            })
            .ToListAsync(ct);

        return ApiResponse<List<PerformedServiceDto>>.Ok(services);
    }

    public async Task<ApiResponse<PerformedServiceDto>> AddPerformedServiceToAppointmentAsync(
        long appointmentId, long dentistUserId, AddPerformedServiceToAppointmentRequest request, CancellationToken ct = default)
    {
        if (request == null || request.DepartmentServiceId <= 0)
        {
            return ApiResponse<PerformedServiceDto>.Fail("Vui lòng chọn dịch vụ điều trị thực tế đã thực hiện.");
        }

        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .Include(a => a.TreatmentSessions)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse<PerformedServiceDto>.Fail("Không tìm thấy thông tin ca khám.");
        }

        var primaryItem = appointment.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
        if (primaryItem == null)
        {
            return ApiResponse<PerformedServiceDto>.Fail("Ca khám chưa có dịch vụ liên kết.");
        }

        var service = await _dbContext.DepartmentServices.FirstOrDefaultAsync(s => s.DepartmentServiceId == request.DepartmentServiceId, ct);
        if (service == null)
        {
            return ApiResponse<PerformedServiceDto>.Fail("Dịch vụ không tồn tại trong danh mục.");
        }

        decimal unitPrice = request.UnitPrice.HasValue && request.UnitPrice.Value >= 0 ? request.UnitPrice.Value : service.Price;
        int quantity = request.Quantity > 0 ? request.Quantity : 1;
        var now = DateTime.UtcNow;

        var session = appointment.TreatmentSessions.FirstOrDefault();
        if (session == null)
        {
            session = new TreatmentSession
            {
                AppointmentId = appointment.AppointmentId,
                AppointmentServiceId = primaryItem.AppointmentServiceId,
                PatientUserId = appointment.PatientUserId,
                TreatmentPlanId = request.TreatmentPlanId,
                DentistUserId = dentistUserId,
                SessionDate = DateOnly.FromDateTime(now),
                Notes = request.Note,
                ClinicalObservations = "Thực hiện thủ thuật lâm sàng",
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.TreatmentSessions.Add(session);
            await _dbContext.SaveChangesAsync(ct);
        }

        var performed = new PerformedService
        {
            TreatmentSessionId = session.SessionId,
            AppointmentId = appointment.AppointmentId,
            AppointmentServiceId = primaryItem.AppointmentServiceId,
            DepartmentServiceId = service.DepartmentServiceId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Notes = request.Note,
            CreatedAt = now
        };

        _dbContext.PerformedServices.Add(performed);
        await _dbContext.SaveChangesAsync(ct);

        var dto = new PerformedServiceDto
        {
            PerformedServiceId = performed.PerformedServiceId,
            TreatmentSessionId = session.SessionId,
            DepartmentServiceId = service.DepartmentServiceId,
            ServiceName = service.ServiceName,
            Quantity = performed.Quantity,
            UnitPrice = performed.UnitPrice,
            Amount = quantity * unitPrice
        };

        return ApiResponse<PerformedServiceDto>.Ok(dto, "Đã ghi nhận dịch vụ thực hiện tại ghế thành công.");
    }

    public async Task<ApiResponse<bool>> DeletePerformedServiceAsync(long performedServiceId, long dentistUserId, CancellationToken ct = default)
    {
        var performed = await _dbContext.PerformedServices
            .Include(p => p.InvoiceItems)
            .Include(p => p.TreatmentSession)
            .FirstOrDefaultAsync(p => p.PerformedServiceId == performedServiceId, ct);

        if (performed == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy dịch vụ đã thực hiện.");
        }

        if (performed.InvoiceItems.Any())
        {
            return ApiResponse<bool>.Fail("Dịch vụ này đã được xuất hóa đơn thanh toán, không thể xóa.");
        }

        _dbContext.PerformedServices.Remove(performed);
        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Đã xóa dịch vụ thực hiện thành công.");
    }

    private static TreatmentPlanDto MapToDto(TreatmentPlan plan)
    {
        var total = plan.TreatmentPlanItems.Count;
        var completed = plan.TreatmentPlanItems.Count(i => i.Status == "Completed");
        var pct = total > 0 ? (int)Math.Round((double)completed * 100 / total) : 0;

        return new TreatmentPlanDto
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            PatientUserId = plan.PatientUserId,
            PatientName = plan.Patient?.FullName ?? "N/A",
            PatientCode = $"BN-{plan.PatientUserId:D4}",
            PatientPhone = plan.Patient?.User?.PhoneNumber,
            DepartmentId = plan.CreatedByDentist?.DepartmentId ?? 1,
            DepartmentName = plan.CreatedByDentist?.Department?.Name ?? "Chuyên khoa",
            CreatedByDentistUserId = plan.CreatedByDentistUserId,
            CreatedByDentistName = plan.CreatedByDentist?.StaffProfile?.FullName ?? "Bác sĩ điều trị",
            Status = plan.Status,
            EstimatedTotal = plan.EstimatedTotal,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt,
            TotalItemsCount = total,
            CompletedItemsCount = completed,
            ProgressPercentage = pct,
            Items = plan.TreatmentPlanItems
                .OrderBy(i => i.SequenceNo)
                .Select(i => MapItemToDto(i, plan, plan.TreatmentSessions))
                .ToList()
        };
    }

    private static TreatmentPlanItemDto MapItemToDto(TreatmentPlanItem item, TreatmentPlan plan, ICollection<TreatmentSession> sessions)
    {
        return new TreatmentPlanItemDto
        {
            TreatmentPlanItemId = item.PlanItemId,
            TreatmentPlanId = item.TreatmentPlanId,
            DepartmentServiceId = item.DepartmentServiceId,
            ServiceName = item.DepartmentService?.ServiceName ?? "Thủ thuật nha khoa",
            ServicePrice = item.DepartmentService?.Price,
            AssignedDentistUserId = plan.CreatedByDentistUserId,
            AssignedDentistName = plan.CreatedByDentist?.StaffProfile?.FullName,
            ToothNumber = item.ToothNumber,
            SequenceNo = item.SequenceNo,
            ExpectedDate = null,
            EstimatedCost = item.EstimatedCost,
            Status = item.Status,
            Note = item.Notes,
            Sessions = sessions
                .OrderByDescending(s => s.SessionDate)
                .Select(MapSessionToDto)
                .ToList()
        };
    }

    private static TreatmentSessionDto MapSessionToDto(TreatmentSession session)
    {
        var patientName = session.Patient?.FullName ?? "Bệnh nhân";
        var patientCode = $"BN-{session.PatientUserId:D4}";

        return new TreatmentSessionDto
        {
            TreatmentSessionId = session.SessionId,
            TreatmentPlanItemId = session.TreatmentPlanId ?? 0,
            AppointmentId = session.AppointmentId,
            AppointmentCode = $"APT-{session.AppointmentId:D6}",
            DentistUserId = session.DentistUserId,
            DentistName = session.Dentist?.StaffProfile?.FullName ?? "Bác sĩ",
            SessionDate = session.SessionDate.ToDateTime(new TimeOnly(8, 0)),
            TreatmentResult = session.ClinicalObservations,
            ClinicalNote = session.Notes,
            PhaseServiceName = session.PerformedServices.FirstOrDefault()?.DepartmentService?.ServiceName,
            PhaseToothNumber = session.PerformedServices.FirstOrDefault()?.ToothNumber,
            PhaseSequenceNo = 1,
            PatientName = patientName,
            PatientCode = patientCode,
            PerformedServices = session.PerformedServices.Select(ps => new PerformedServiceDto
            {
                PerformedServiceId = ps.PerformedServiceId,
                TreatmentSessionId = ps.TreatmentSessionId,
                DepartmentServiceId = ps.DepartmentServiceId,
                ServiceName = ps.DepartmentService?.ServiceName ?? "Dịch vụ",
                Quantity = ps.Quantity,
                UnitPrice = ps.UnitPrice,
                Amount = ps.Quantity * ps.UnitPrice
            }).ToList()
        };
    }
}
