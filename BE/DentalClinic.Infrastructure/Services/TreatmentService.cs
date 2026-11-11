using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Treatments.DTOs;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

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
            .Include(tp => tp.PatientUser)
                .ThenInclude(p => p.User)
            .Include(tp => tp.Department)
            .Include(tp => tp.CreatedByDentistUser)
                .ThenInclude(d => d.User)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.DepartmentService)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .AsQueryable();

        if (dentistId > 0)
        {
            query = query.Where(tp => tp.CreatedByDentistUserId == dentistId 
                || tp.TreatmentPlanItems.Any(tpi => tpi.AssignedDentistUserId == dentistId));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(tp => tp.Status == status);
        }
        else
        {
            query = query.Where(tp => !tp.TreatmentPlanItems.Any(tpi => tpi.Note != null && tpi.Note.StartsWith("[DirectCare]")));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(tp => tp.PatientUser.FullName.ToLower().Contains(s)
                || tp.PatientUser.PatientCode.ToLower().Contains(s)
                || (tp.PatientUser.User.PhoneNumber != null && tp.PatientUser.User.PhoneNumber.Contains(s))
                || tp.Department.Name.ToLower().Contains(s));
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
            .Include(tp => tp.PatientUser)
                .ThenInclude(p => p.User)
            .Include(tp => tp.Department)
            .Include(tp => tp.CreatedByDentistUser)
                .ThenInclude(d => d.User)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.DepartmentService)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.TreatmentSessions)
                    .ThenInclude(ts => ts.DentistUser)
                        .ThenInclude(d => d!.User)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.TreatmentSessions)
                    .ThenInclude(ts => ts.Appointment)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.TreatmentSessions)
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

        var deptId = request.DepartmentId;
        if (deptId <= 0)
        {
            var firstDept = await _dbContext.DentistDepartments
                .Where(dd => dd.DentistUserId == dentistUserId)
                .OrderBy(dd => dd.DepartmentId)
                .Select(dd => dd.DepartmentId)
                .FirstOrDefaultAsync(ct);
            deptId = firstDept > 0 ? firstDept : 1;
        }

        var plan = new TreatmentPlan
        {
            PatientUserId = request.PatientUserId,
            DepartmentId = deptId,
            CreatedByDentistUserId = dentistUserId,
            Status = !string.IsNullOrWhiteSpace(request.Status) ? request.Status : "Planned",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
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
                var item = new TreatmentPlanItem
                {
                    TreatmentPlanId = plan.TreatmentPlanId,
                    DepartmentServiceId = itemInput.DepartmentServiceId,
                    AssignedDentistUserId = itemInput.AssignedDentistUserId ?? dentistUserId,
                    ToothNumber = itemInput.ToothNumber,
                    SequenceNo = itemInput.SequenceNo > 0 ? itemInput.SequenceNo : seq++,
                    ExpectedDate = itemInput.ExpectedDate,
                    EstimatedCost = itemInput.EstimatedCost,
                    Status = !string.IsNullOrWhiteSpace(itemInput.Status) ? itemInput.Status : "Pending",
                    Note = itemInput.Note
                };

                if (item.EstimatedCost.HasValue)
                {
                    totalEstimated += item.EstimatedCost.Value;
                }

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

        var item = new TreatmentPlanItem
        {
            TreatmentPlanId = planId,
            DepartmentServiceId = input.DepartmentServiceId,
            AssignedDentistUserId = input.AssignedDentistUserId ?? plan.CreatedByDentistUserId,
            ToothNumber = input.ToothNumber,
            SequenceNo = nextSeq,
            ExpectedDate = input.ExpectedDate,
            EstimatedCost = input.EstimatedCost,
            Status = !string.IsNullOrWhiteSpace(input.Status) ? input.Status : "Pending",
            Note = input.Note
        };

        _dbContext.TreatmentPlanItems.Add(item);

        plan.EstimatedTotal = (plan.EstimatedTotal ?? 0) + (input.EstimatedCost ?? 0);
        plan.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var createdItem = await _dbContext.TreatmentPlanItems
            .AsNoTracking()
            .Include(i => i.DepartmentService)
            .Include(i => i.AssignedDentistUser)
                .ThenInclude(d => d!.User)
            .FirstOrDefaultAsync(i => i.TreatmentPlanItemId == item.TreatmentPlanItemId, ct);

        return ApiResponse<TreatmentPlanItemDto>.Ok(MapItemToDto(createdItem!));
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
            .FirstOrDefaultAsync(i => i.TreatmentPlanItemId == itemId, ct);

        if (item == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy giai đoạn điều trị.");
        }

        item.Status = status;
        item.TreatmentPlan.UpdatedAt = DateTime.UtcNow;

        // Nếu tất cả các giai đoạn đã hoàn thành, tự động đánh dấu kế hoạch hoàn thành
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
            .Include(ts => ts.DentistUser)
                .ThenInclude(d => d.User)
            .Include(ts => ts.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(ts => ts.TreatmentPlanItem)
                .ThenInclude(tpi => tpi.DepartmentService)
            .Include(ts => ts.TreatmentPlanItem)
                .ThenInclude(tpi => tpi.TreatmentPlan)
                    .ThenInclude(tp => tp.PatientUser)
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
                (ts.Appointment.PatientUser != null && ts.Appointment.PatientUser.FullName.ToLower().Contains(s))
                || (ts.TreatmentPlanItem.TreatmentPlan.PatientUser != null && ts.TreatmentPlanItem.TreatmentPlan.PatientUser.FullName.ToLower().Contains(s))
                || (ts.ClinicalNote != null && ts.ClinicalNote.ToLower().Contains(s))
                || (ts.TreatmentResult != null && ts.TreatmentResult.ToLower().Contains(s)));
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
            .FirstOrDefaultAsync(i => i.TreatmentPlanItemId == request.TreatmentPlanItemId, ct);

        if (item == null)
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Không tìm thấy giai đoạn điều trị cần ghi nhận.");
        }

        long appointmentId = request.AppointmentId;
        if (appointmentId <= 0)
        {
            // Tìm lịch hẹn phù hợp gần nhất của bệnh nhân
            var latestAppt = await _dbContext.Appointments
                .Where(a => a.PatientUserId == item.TreatmentPlan.PatientUserId)
                .OrderByDescending(a => a.ScheduledStart)
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

        var appt = await _dbContext.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);
        if (appt == null)
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Không tìm thấy ca khám tương ứng.");
        }

        // Section 7: Mỗi TreatmentSession gắn với Appointment thực tế. Không tự tạo Session cho các lần khám trong tương lai.
        if (appt.ScheduledStart > DateTime.UtcNow.AddHours(2) && appt.Status != AppointmentStatus.InService && appt.Status != AppointmentStatus.CheckedIn)
        {
            return ApiResponse<TreatmentSessionDto>.Fail("Không được tự tạo buổi điều trị (TreatmentSession) cho các lần khám trong tương lai.");
        }

        var session = new TreatmentSession
        {
            TreatmentPlanItemId = request.TreatmentPlanItemId,
            AppointmentId = appointmentId,
            DentistUserId = dentistUserId,
            SessionDate = request.SessionDate != default ? request.SessionDate : DateTime.UtcNow,
            TreatmentResult = request.TreatmentResult ?? "Tiến triển tốt",
            ClinicalNote = request.ClinicalNote
        };

        _dbContext.TreatmentSessions.Add(session);
        await _dbContext.SaveChangesAsync(ct);

        // Thêm các dịch vụ thực hiện (PerformedServices)
        if (request.Services != null && request.Services.Any())
        {
            foreach (var svc in request.Services)
            {
                if (svc.DepartmentServiceId > 0)
                {
                    var perf = new PerformedService
                    {
                        TreatmentSessionId = session.TreatmentSessionId,
                        DepartmentServiceId = svc.DepartmentServiceId,
                        Quantity = svc.Quantity > 0 ? svc.Quantity : 1,
                        UnitPrice = svc.UnitPrice
                    };
                    _dbContext.PerformedServices.Add(perf);
                }
            }
            await _dbContext.SaveChangesAsync(ct);
        }

        // Cập nhật trạng thái của giai đoạn (TreatmentPlanItem)
        if (!string.IsNullOrWhiteSpace(request.MarkPhaseStatus))
        {
            item.Status = request.MarkPhaseStatus;
            item.TreatmentPlan.UpdatedAt = DateTime.UtcNow;

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
            .Include(ts => ts.DentistUser)
                .ThenInclude(d => d.User)
            .Include(ts => ts.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(ts => ts.TreatmentPlanItem)
                .ThenInclude(tpi => tpi.DepartmentService)
            .Include(ts => ts.TreatmentPlanItem)
                .ThenInclude(tpi => tpi.TreatmentPlan)
                    .ThenInclude(tp => tp.PatientUser)
            .Include(ts => ts.PerformedServices)
                .ThenInclude(ps => ps.DepartmentService)
            .FirstOrDefaultAsync(ts => ts.TreatmentSessionId == session.TreatmentSessionId, ct);

        return ApiResponse<TreatmentSessionDto>.Ok(MapSessionToDto(loadedSession!));
    }

    public async Task<ApiResponse<TreatmentProgressSummaryDto>> GetDepartmentTreatmentProgressAsync(
        long managerUserId, long? departmentId, string? search, CancellationToken ct = default)
    {
        List<long> managedDeptIds;
        if (departmentId.HasValue && departmentId.Value > 0)
        {
            managedDeptIds = new List<long> { departmentId.Value };
        }
        else
        {
            managedDeptIds = await _dbContext.Departments
                .AsNoTracking()
                .Select(d => d.DepartmentId)
                .ToListAsync(ct);
        }

        var query = _dbContext.TreatmentPlans
            .AsNoTracking()
            .Include(tp => tp.PatientUser)
                .ThenInclude(p => p.User)
            .Include(tp => tp.Department)
            .Include(tp => tp.CreatedByDentistUser)
                .ThenInclude(d => d.User)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.DepartmentService)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Where(tp => managedDeptIds.Contains(tp.DepartmentId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(tp => tp.PatientUser.FullName.ToLower().Contains(s)
                || tp.PatientUser.PatientCode.ToLower().Contains(s)
                || tp.Department.Name.ToLower().Contains(s));
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
            .FirstOrDefaultAsync(i => i.TreatmentPlanItemId == itemId, ct);

        if (item == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy giai đoạn điều trị.");
        }

        var dentist = await _dbContext.DentistProfiles.FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);
        if (dentist == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy thông tin bác sĩ được chỉ định.");
        }

        item.AssignedDentistUserId = dentistUserId;
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
            .Include(tp => tp.PatientUser)
                .ThenInclude(p => p.User)
            .Include(tp => tp.Department)
            .Include(tp => tp.CreatedByDentistUser)
                .ThenInclude(d => d.User)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.DepartmentService)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.TreatmentSessions)
                    .ThenInclude(ts => ts.DentistUser)
                        .ThenInclude(d => d!.User)
            .Include(tp => tp.TreatmentPlanItems)
                .ThenInclude(tpi => tpi.TreatmentSessions)
                    .ThenInclude(ts => ts.PerformedServices)
                        .ThenInclude(ps => ps.DepartmentService)
            .Where(tp => tp.PatientUserId == patientUserId && !tp.TreatmentPlanItems.Any(tpi => tpi.Note != null && tpi.Note.StartsWith("[DirectCare]")))
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
                PatientCode = p.PatientCode,
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
            .Include(d => d.User)
            .Include(d => d.DentistDepartments)
            .AsQueryable();

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(d => d.DentistDepartments.Any(dd => dd.DepartmentId == departmentId.Value));
        }

        var dentists = await query
            .OrderBy(d => d.User.FullName)
            .Select(d => new TreatmentDentistOptionDto
            {
                DentistUserId = d.UserId,
                FullName = d.User.FullName,
                Specialization = d.Qualification,
                DepartmentId = d.DentistDepartments.Select(dd => dd.DepartmentId).FirstOrDefault()
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
                .ThenInclude(tp => tp.PatientUser)
                    .ThenInclude(p => p.Appointments)
            .Where(tpi => tpi.TreatmentPlan.Status != "Cancelled")
            .OrderBy(tpi => tpi.TreatmentPlan.PatientUser.FullName)
            .ThenBy(tpi => tpi.SequenceNo)
            .ToListAsync(ct);

        var result = items.Select(i =>
        {
            var latestAppt = i.TreatmentPlan.PatientUser?.Appointments
                .OrderByDescending(a => a.ScheduledStart)
                .FirstOrDefault();

            return new TreatmentEligiblePhaseDto
            {
                TreatmentPlanItemId = i.TreatmentPlanItemId,
                TreatmentPlanId = i.TreatmentPlanId,
                PatientUserId = i.TreatmentPlan.PatientUserId,
                PatientName = i.TreatmentPlan.PatientUser?.FullName ?? "N/A",
                PatientCode = i.TreatmentPlan.PatientUser?.PatientCode ?? "N/A",
                SequenceNo = i.SequenceNo,
                ServiceName = i.DepartmentService?.ServiceName ?? "Dịch vụ nha khoa",
                ToothNumber = i.ToothNumber,
                PhaseStatus = i.Status,
                EstimatedCost = i.EstimatedCost,
                AssociatedAppointmentId = latestAppt?.AppointmentId,
                AssociatedAppointmentCode = latestAppt?.AppointmentCode
            };
        }).ToList();

        return ApiResponse<List<TreatmentEligiblePhaseDto>>.Ok(result);
    }

    // ==========================================
    // MAPPERS
    // ==========================================

    private static TreatmentPlanDto MapToDto(TreatmentPlan plan)
    {
        var total = plan.TreatmentPlanItems.Count;
        var completed = plan.TreatmentPlanItems.Count(i => i.Status == "Completed");
        var pct = total > 0 ? (int)Math.Round((double)completed * 100 / total) : 0;

        return new TreatmentPlanDto
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            PatientUserId = plan.PatientUserId,
            PatientName = plan.PatientUser?.FullName ?? "N/A",
            PatientCode = plan.PatientUser?.PatientCode ?? "N/A",
            PatientPhone = plan.PatientUser?.User?.PhoneNumber,
            DepartmentId = plan.DepartmentId,
            DepartmentName = plan.Department?.Name ?? "Chuyên khoa",
            CreatedByDentistUserId = plan.CreatedByDentistUserId,
            CreatedByDentistName = plan.CreatedByDentistUser?.User?.FullName ?? "Bác sĩ điều trị",
            Status = plan.Status,
            EstimatedTotal = plan.EstimatedTotal,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt,
            TotalItemsCount = total,
            CompletedItemsCount = completed,
            ProgressPercentage = pct,
            Items = plan.TreatmentPlanItems
                .OrderBy(i => i.SequenceNo)
                .Select(MapItemToDto)
                .ToList()
        };
    }

    private static TreatmentPlanItemDto MapItemToDto(TreatmentPlanItem item)
    {
        return new TreatmentPlanItemDto
        {
            TreatmentPlanItemId = item.TreatmentPlanItemId,
            TreatmentPlanId = item.TreatmentPlanId,
            DepartmentServiceId = item.DepartmentServiceId,
            ServiceName = item.DepartmentService?.ServiceName ?? "Thủ thuật nha khoa",
            ServicePrice = item.DepartmentService?.Price,
            AssignedDentistUserId = item.AssignedDentistUserId,
            AssignedDentistName = item.AssignedDentistUser?.User?.FullName,
            ToothNumber = item.ToothNumber,
            SequenceNo = item.SequenceNo,
            ExpectedDate = item.ExpectedDate,
            EstimatedCost = item.EstimatedCost,
            Status = item.Status,
            Note = item.Note,
            Sessions = item.TreatmentSessions
                .OrderByDescending(s => s.SessionDate)
                .Select(MapSessionToDto)
                .ToList()
        };
    }

    private static TreatmentSessionDto MapSessionToDto(TreatmentSession session)
    {
        var patientName = session.Appointment?.PatientUser?.FullName 
            ?? session.TreatmentPlanItem?.TreatmentPlan?.PatientUser?.FullName 
            ?? "Bệnh nhân";

        var patientCode = session.Appointment?.PatientUser?.PatientCode 
            ?? session.TreatmentPlanItem?.TreatmentPlan?.PatientUser?.PatientCode 
            ?? string.Empty;

        return new TreatmentSessionDto
        {
            TreatmentSessionId = session.TreatmentSessionId,
            TreatmentPlanItemId = session.TreatmentPlanItemId,
            AppointmentId = session.AppointmentId,
            AppointmentCode = session.Appointment?.AppointmentCode,
            DentistUserId = session.DentistUserId,
            DentistName = session.DentistUser?.User?.FullName ?? "Bác sĩ",
            SessionDate = session.SessionDate,
            TreatmentResult = session.TreatmentResult,
            ClinicalNote = session.ClinicalNote,
            PhaseServiceName = session.TreatmentPlanItem?.DepartmentService?.ServiceName,
            PhaseToothNumber = session.TreatmentPlanItem?.ToothNumber,
            PhaseSequenceNo = session.TreatmentPlanItem?.SequenceNo ?? 0,
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
            .Include(tp => tp.PatientUser)
                .ThenInclude(p => p.User)
            .Include(tp => tp.Department)
            .Include(tp => tp.TreatmentPlanItems)
            .FirstOrDefaultAsync(tp => tp.TreatmentPlanId == planId, ct);

        if (plan == null)
            return ApiResponse<AppointmentDto>.Fail("Không tìm thấy phác đồ điều trị chỉ định.");

        // Kiểm tra quyền hạn của bác sĩ đối với phác đồ điều trị
        var isDoctorInDept = await _dbContext.DentistDepartments
            .AnyAsync(dd => dd.DentistUserId == dentistUserId && dd.DepartmentId == plan.DepartmentId && dd.Status == "Active", ct);

        if (plan.CreatedByDentistUserId != dentistUserId && !isDoctorInDept)
        {
            return ApiResponse<AppointmentDto>.Fail("Bác sĩ không có quyền thao tác trên phác đồ điều trị này.");
        }

        var proposedEnd = request.ScheduledStart.AddMinutes(request.DurationMinutes ?? 30);
        var targetDentistId = request.RequestedDentistUserId ?? dentistUserId;

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;
        try
        {
            var targetDate = request.ScheduledStart.Date;
            var lockKeys = new[]
            {
                SqlAppLockHelper.DentistMasterKey(targetDentistId),
                SqlAppLockHelper.DentistKey(targetDentistId, targetDate),
                SqlAppLockHelper.PatientKey(plan.PatientUserId, targetDate),
                SqlAppLockHelper.PlanKey(planId)
            };
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct);

            // Chống trùng lặp (WF-11: khoảng thời gian giao nhau hoặc +/- 1 giờ cho cùng phác đồ)
            var windowStart = request.ScheduledStart.AddHours(-1);
            var windowEnd = request.ScheduledStart.AddHours(1);
            var hasDuplicate = await _dbContext.Appointments.AnyAsync(a =>
                a.PatientUserId == plan.PatientUserId &&
                a.DepartmentId == plan.DepartmentId &&
                (a.Status == AppointmentStatus.PendingReceptionReview || a.Status == AppointmentStatus.PendingDepartmentReview || a.Status == AppointmentStatus.Confirmed) &&
                a.ScheduledStart.HasValue &&
                ((a.ScheduledStart.Value >= windowStart && a.ScheduledStart.Value <= windowEnd) ||
                 (a.ScheduledStart.Value < proposedEnd && a.ScheduledEnd > request.ScheduledStart)), ct);

            if (hasDuplicate)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<AppointmentDto>.Fail("Đã có lịch hẹn tái khám đang chờ duyệt hoặc đã xác nhận trong khung giờ gần thời điểm này.");
            }

            // Lấy thông tin giai đoạn nếu có
            long? serviceId = null;
            if (request.PlanItemId.HasValue)
            {
                var item = plan.TreatmentPlanItems.FirstOrDefault(i => i.TreatmentPlanItemId == request.PlanItemId.Value);
                if (item != null)
                {
                    serviceId = item.DepartmentServiceId;
                }
            }

            string apptCode;
            do
            {
                apptCode = "APP-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            } while (await _dbContext.Appointments.AnyAsync(a => a.AppointmentCode == apptCode, ct));

            var appt = new Appointment
            {
                AppointmentCode = apptCode,
                PatientUserId = plan.PatientUserId,
                DepartmentId = plan.DepartmentId,
                RequestedServiceId = serviceId,
                RequestedDentistUserId = targetDentistId,
                AssignedDentistUserId = targetDentistId,
                TreatmentPlanItemId = request.PlanItemId,
                ScheduledStart = request.ScheduledStart,
                ScheduledEnd = proposedEnd,
                ReasonForVisit = $"[Tái khám phác đồ #{plan.TreatmentPlanId}] {request.Reason.Trim()}",
                BookingSource = BookingSource.Receptionist,
                Status = AppointmentStatus.PendingReceptionReview, // 4-Role Model: Chuyển Lễ tân điều phối
                CreatedByUserId = dentistUserId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Appointments.Add(appt);

            const int maxRetries = 3;
            int retryCount = 0;
            while (true)
            {
                try
                {
                    await _dbContext.SaveChangesAsync(ct);
                    break;
                }
                catch (DbUpdateException ex) when (SqlAppLockHelper.IsUniqueConstraintViolation(ex))
                {
                    if (retryCount >= maxRetries)
                    {
                        if (tx != null) await tx.RollbackAsync(ct);
                        return ApiResponse<AppointmentDto>.Fail("Không thể tạo cuộc hẹn do xung đột mã cuộc hẹn liên tiếp. Vui lòng thử lại sau.");
                    }
                    retryCount++;
                    appt.AppointmentCode = "APP-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                }
            }

            // Thêm lịch sử trạng thái
            var history = new AppointmentStatusHistory
            {
                AppointmentId = appt.AppointmentId,
                OldStatus = null,
                NewStatus = AppointmentStatus.PendingReceptionReview,
                ChangedByUserId = dentistUserId,
                Note = $"Bác sĩ chỉ định tái khám từ phác đồ #{plan.TreatmentPlanId}",
                ChangedAt = DateTime.UtcNow
            };
            _dbContext.AppointmentStatusHistories.Add(history);

            // Tạo bản ghi FollowUpSchedule liên kết Appointment (WF-11 điều 03)
            var latestSession = await _dbContext.TreatmentSessions
                .Where(s => s.TreatmentPlanItem.TreatmentPlanId == planId)
                .OrderByDescending(s => s.SessionDate)
                .FirstOrDefaultAsync(ct);

            if (latestSession != null)
            {
                var followUp = new FollowUpSchedule
                {
                    TreatmentSessionId = latestSession.TreatmentSessionId,
                    RecommendedDate = request.ScheduledStart,
                    Reason = request.Reason.Trim(),
                    Status = "Scheduled",
                    AppointmentId = appt.AppointmentId
                };
                _dbContext.FollowUpSchedules.Add(followUp);
            }

            // 4-Role Model: Tạo thông báo cho Lễ tân phụ trách tiếp nhận và điều phối
            var recUserIds = await _dbContext.UserAccounts
                .Where(u => u.Role == "Receptionist")
                .Select(u => u.UserId)
                .ToListAsync(ct);

            foreach (var recUserId in recUserIds)
            {
                _dbContext.Notifications.Add(new Notification
                {
                    UserId = recUserId,
                    AppointmentId = appt.AppointmentId,
                    Type = "FollowUpReviewRequired",
                    Channel = "InApp",
                    Subject = "Lịch tái khám mới cần sắp xếp phòng/ghế",
                    Content = $"Bác sĩ vừa lập lịch tái khám mã {appt.AppointmentCode} cho bệnh nhân {plan.PatientUser?.FullName}. Vui lòng kiểm tra và hỗ trợ xác nhận.",
                    DeliveryStatus = "Sent",
                    SentAt = DateTime.UtcNow
                });
            }

            // Tạo thông báo cho Bệnh nhân
            _dbContext.Notifications.Add(new Notification
            {
                UserId = plan.PatientUserId,
                AppointmentId = appt.AppointmentId,
                Type = "FollowUpScheduled",
                Channel = "InApp",
                Subject = "Đã đề xuất lịch tái khám theo phác đồ",
                Content = $"Bác sĩ đã lên lịch tái khám cho bạn vào {request.ScheduledStart:dd/MM/yyyy HH:mm} (Mã: {appt.AppointmentCode}). Lịch đang được chuyển Lễ tân xác nhận phòng/ghế.",
                DeliveryStatus = "Sent",
                SentAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            var dto = new AppointmentDto
            {
                AppointmentId = appt.AppointmentId,
                AppointmentCode = appt.AppointmentCode,
                PatientUserId = appt.PatientUserId,
                PatientName = plan.PatientUser?.FullName ?? string.Empty,
                DepartmentId = appt.DepartmentId,
                DepartmentName = plan.Department?.Name,
                RequestedServiceId = appt.RequestedServiceId,
                RequestedDentistUserId = appt.RequestedDentistUserId,
                AssignedDentistUserId = appt.AssignedDentistUserId,
                ScheduledStart = appt.ScheduledStart,
                ScheduledEnd = appt.ScheduledEnd,
                ReasonForVisit = appt.ReasonForVisit,
                BookingSource = appt.BookingSource,
                Status = appt.Status,
                CreatedAt = appt.CreatedAt
            };

            return ApiResponse<AppointmentDto>.Ok(dto, "Lập lịch hẹn tái khám từ phác đồ thành công. Lịch đã được chuyển tới Lễ tân để điều phối phòng/ghế.");
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            lockScope?.Dispose();
            if (tx != null) await tx.DisposeAsync();
        }
    }

    public async Task<ApiResponse<List<PerformedServiceDto>>> GetPerformedServicesByAppointmentAsync(long appointmentId, CancellationToken ct = default)
    {
        var services = await _dbContext.PerformedServices
            .AsNoTracking()
            .Include(ps => ps.DepartmentService)
            .Include(ps => ps.TreatmentSession)
            .Where(ps => ps.TreatmentSession.AppointmentId == appointmentId)
            .OrderBy(ps => ps.PerformedServiceId)
            .Select(ps => new PerformedServiceDto
            {
                PerformedServiceId = ps.PerformedServiceId,
                TreatmentSessionId = ps.TreatmentSessionId,
                DepartmentServiceId = ps.DepartmentServiceId,
                ServiceName = ps.DepartmentService.ServiceName,
                Quantity = ps.Quantity,
                UnitPrice = ps.UnitPrice,
                Amount = ps.Amount ?? (ps.Quantity * ps.UnitPrice)
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
            .Include(a => a.TreatmentSessions)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse<PerformedServiceDto>.Fail("Không tìm thấy thông tin ca khám.");
        }

        if (appointment.AssignedDentistUserId.HasValue && appointment.AssignedDentistUserId.Value != dentistUserId)
        {
            return ApiResponse<PerformedServiceDto>.Fail("Bạn không phải là bác sĩ được phân công cho ca khám này.");
        }

        var nonBillableStatuses = new[] { "Cancelled", "Withdrawn", "Rejected", "NoShow" };
        if (nonBillableStatuses.Contains(appointment.Status))
        {
            return ApiResponse<PerformedServiceDto>.Fail($"Ca khám đang ở trạng thái '{appointment.Status}', không thể ghi nhận dịch vụ thực hiện.");
        }

        var service = await _dbContext.DepartmentServices.FirstOrDefaultAsync(s => s.DepartmentServiceId == request.DepartmentServiceId, ct);
        if (service == null)
        {
            return ApiResponse<PerformedServiceDto>.Fail("Dịch vụ không tồn tại trong danh mục.");
        }

        decimal unitPrice = request.UnitPrice.HasValue && request.UnitPrice.Value >= 0 ? request.UnitPrice.Value : service.Price;
        int quantity = request.Quantity > 0 ? request.Quantity : 1;

        // Tìm hoặc tạo TreatmentSession cho Appointment này
        var session = appointment.TreatmentSessions.FirstOrDefault();
        if (session == null)
        {
            var deptId = appointment.DepartmentId ?? service.DepartmentId;
            var appointmentNote = $"Đợt khám {appointment.AppointmentCode}";

            TreatmentPlanItem? item = null;

            // 1. Explicit linking: TreatmentPlanItemId provided in request
            if (request.TreatmentPlanItemId.HasValue && request.TreatmentPlanItemId.Value > 0)
            {
                var planItem = await _dbContext.TreatmentPlanItems
                    .Include(tpi => tpi.TreatmentPlan)
                    .FirstOrDefaultAsync(tpi => tpi.TreatmentPlanItemId == request.TreatmentPlanItemId.Value, ct);

                if (planItem == null)
                {
                    return ApiResponse<PerformedServiceDto>.Fail($"Không tìm thấy giai đoạn điều trị #{request.TreatmentPlanItemId.Value}.");
                }

                if (planItem.TreatmentPlan.PatientUserId != appointment.PatientUserId)
                {
                    return ApiResponse<PerformedServiceDto>.Fail("Giai đoạn phác đồ được chỉ định không thuộc về bệnh nhân của ca khám này.");
                }

                item = planItem;
            }
            // 1b. Explicit relational linking from Appointment
            else if (appointment.TreatmentPlanItemId.HasValue && appointment.TreatmentPlanItemId.Value > 0)
            {
                var planItem = await _dbContext.TreatmentPlanItems
                    .Include(tpi => tpi.TreatmentPlan)
                    .FirstOrDefaultAsync(tpi => tpi.TreatmentPlanItemId == appointment.TreatmentPlanItemId.Value, ct);

                if (planItem != null && planItem.TreatmentPlan.PatientUserId == appointment.PatientUserId)
                {
                    item = planItem;
                }
            }
            // 2. Explicit linking: TreatmentPlanId provided in request
            else if (request.TreatmentPlanId.HasValue && request.TreatmentPlanId.Value > 0)
            {
                var linkedPlan = await _dbContext.TreatmentPlans
                    .Include(tp => tp.TreatmentPlanItems)
                    .FirstOrDefaultAsync(tp => tp.TreatmentPlanId == request.TreatmentPlanId.Value && tp.PatientUserId == appointment.PatientUserId, ct);

                if (linkedPlan == null)
                {
                    return ApiResponse<PerformedServiceDto>.Fail($"Không tìm thấy phác đồ điều trị #{request.TreatmentPlanId.Value} của bệnh nhân.");
                }

                item = linkedPlan.TreatmentPlanItems
                    .FirstOrDefault(tpi => tpi.DepartmentServiceId == request.DepartmentServiceId && tpi.Status != "Completed")
                    ?? linkedPlan.TreatmentPlanItems.FirstOrDefault(tpi => tpi.Status != "Completed")
                    ?? linkedPlan.TreatmentPlanItems.FirstOrDefault();
            }
            // 3. Fallback backward-compatibility: parse ReasonForVisit if explicitly tagged
            else if (!string.IsNullOrEmpty(appointment.ReasonForVisit) && appointment.ReasonForVisit.Contains("[Tái khám phác đồ #"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(appointment.ReasonForVisit, @"\[Tái khám phác đồ #(\d+)\]");
                if (match.Success && long.TryParse(match.Groups[1].Value, out var parsedPlanId))
                {
                    var linkedPlan = await _dbContext.TreatmentPlans
                        .Include(tp => tp.TreatmentPlanItems)
                        .FirstOrDefaultAsync(tp => tp.TreatmentPlanId == parsedPlanId && tp.PatientUserId == appointment.PatientUserId, ct);

                    if (linkedPlan != null)
                    {
                        item = linkedPlan.TreatmentPlanItems
                            .FirstOrDefault(tpi => tpi.DepartmentServiceId == request.DepartmentServiceId && tpi.Status != "Completed")
                            ?? linkedPlan.TreatmentPlanItems.FirstOrDefault(tpi => tpi.Status != "Completed")
                            ?? linkedPlan.TreatmentPlanItems.FirstOrDefault();
                    }
                }
            }

            // 4. Nếu ca khám vãng lai / đơn lẻ không gắn phác đồ dài hạn, dùng bản ghi DirectCare để cách ly,
            // Sử dụng Status = 'Completed' và Note prefix [DirectCare] để tuyệt đối tuân thủ SQL Server CHECK constraint
            if (item == null)
            {
                var directCareTag = $"[DirectCare] Đợt khám {appointment.AppointmentCode}";
                var plan = await _dbContext.TreatmentPlans
                    .FirstOrDefaultAsync(tp => tp.PatientUserId == appointment.PatientUserId &&
                                               tp.DepartmentId == deptId &&
                                               tp.Status == "Completed" &&
                                               tp.TreatmentPlanItems.Any(tpi => tpi.Note == directCareTag), ct);

                if (plan == null)
                {
                    plan = new TreatmentPlan
                    {
                        PatientUserId = appointment.PatientUserId,
                        DepartmentId = deptId,
                        CreatedByDentistUserId = dentistUserId,
                        Status = "Completed",
                                                CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _dbContext.TreatmentPlans.Add(plan);
                    await _dbContext.SaveChangesAsync(ct);
                }

                item = await _dbContext.TreatmentPlanItems
                    .FirstOrDefaultAsync(tpi => tpi.TreatmentPlanId == plan.TreatmentPlanId && tpi.Note == directCareTag, ct);

                if (item == null)
                {
                    var maxSeq = await _dbContext.TreatmentPlanItems
                        .Where(tpi => tpi.TreatmentPlanId == plan.TreatmentPlanId)
                        .Select(tpi => (int?)tpi.SequenceNo)
                        .MaxAsync(ct) ?? 0;

                    item = new TreatmentPlanItem
                    {
                        TreatmentPlanId = plan.TreatmentPlanId,
                        DepartmentServiceId = request.DepartmentServiceId,
                        SequenceNo = maxSeq + 1,
                        Status = "Completed",
                        AssignedDentistUserId = dentistUserId,
                        Note = directCareTag
                    };
                    _dbContext.TreatmentPlanItems.Add(item);
                    await _dbContext.SaveChangesAsync(ct);
                }
            }

            session = new TreatmentSession
            {
                TreatmentPlanItemId = item.TreatmentPlanItemId,
                AppointmentId = appointment.AppointmentId,
                DentistUserId = dentistUserId,
                SessionDate = DateTime.UtcNow,
                TreatmentResult = "Thực hiện thủ thuật lâm sàng",
                ClinicalNote = request.Note
            };
            _dbContext.TreatmentSessions.Add(session);
            await _dbContext.SaveChangesAsync(ct);
        }

        var performed = new PerformedService
        {
            TreatmentSessionId = session.TreatmentSessionId,
            DepartmentServiceId = service.DepartmentServiceId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Amount = quantity * unitPrice
        };

        _dbContext.PerformedServices.Add(performed);
        await _dbContext.SaveChangesAsync(ct);

        var dto = new PerformedServiceDto
        {
            PerformedServiceId = performed.PerformedServiceId,
            TreatmentSessionId = session.TreatmentSessionId,
            DepartmentServiceId = service.DepartmentServiceId,
            ServiceName = service.ServiceName,
            Quantity = performed.Quantity,
            UnitPrice = performed.UnitPrice,
            Amount = performed.Amount ?? (quantity * unitPrice)
        };

        return ApiResponse<PerformedServiceDto>.Ok(dto, "Đã ghi nhận dịch vụ thực hiện tại ghế thành công.");
    }

    public async Task<ApiResponse<bool>> DeletePerformedServiceAsync(long performedServiceId, long dentistUserId, CancellationToken ct = default)
    {
        var performed = await _dbContext.PerformedServices
            .Include(p => p.InvoiceItems)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
            .FirstOrDefaultAsync(p => p.PerformedServiceId == performedServiceId, ct);

        if (performed == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy dịch vụ đã thực hiện.");
        }

        if (performed.InvoiceItems.Any())
        {
            return ApiResponse<bool>.Fail("Dịch vụ này đã được xuất hóa đơn thanh toán, không thể xóa.");
        }

        if (performed.TreatmentSession.DentistUserId != dentistUserId &&
            performed.TreatmentSession.Appointment?.AssignedDentistUserId != dentistUserId)
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền xóa dịch vụ thực hiện này.");
        }

        _dbContext.PerformedServices.Remove(performed);
        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Đã xóa dịch vụ thực hiện thành công.");
    }
}
