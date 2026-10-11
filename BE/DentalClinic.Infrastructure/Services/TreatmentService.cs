using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Treatments.DTOs;
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
                .Select(dd => dd.DepartmentId)
                .FirstOrDefaultAsync(ct);
            deptId = firstDept > 0 ? firstDept : 1;
        }

        var plan = new TreatmentPlan
        {
            PatientUserId = request.PatientUserId,
            DepartmentId = deptId,
            CreatedByDentistUserId = dentistUserId,
            Status = !string.IsNullOrWhiteSpace(request.Status) ? request.Status : "Active",
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
                item.TreatmentPlan.Status = "Active";
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
                .Where(d => d.ManagerUserId == managerUserId)
                .Select(d => d.DepartmentId)
                .ToListAsync(ct);

            if (!managedDeptIds.Any())
            {
                // Fallback nếu tài khoản admin hoặc chưa gán trưởng khoa rõ ràng: lấy tất cả các khoa
                managedDeptIds = await _dbContext.Departments
                    .AsNoTracking()
                    .Select(d => d.DepartmentId)
                    .ToListAsync(ct);
            }
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
            ActivePlans = dtoList.Count(p => p.Status == "Active"),
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
            .Where(tp => tp.PatientUserId == patientUserId)
            .OrderByDescending(tp => tp.CreatedAt)
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
}
