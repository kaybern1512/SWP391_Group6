using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Operations.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class ClinicOperationsService : IClinicOperationsService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IStaffShiftResolver _shiftResolver;
    private readonly ILogger<ClinicOperationsService> _logger;

    public ClinicOperationsService(
        DentalClinicDbContext dbContext,
        IStaffShiftResolver shiftResolver,
        ILogger<ClinicOperationsService> logger)
    {
        _dbContext = dbContext;
        _shiftResolver = shiftResolver;
        _logger = logger;
    }

    public async Task<ApiResponse<ClinicOperationsDashboardDto>> GetOperationsDashboardAsync(DateOnly? targetDate = null, CancellationToken ct = default)
    {
        var opDate = targetDate ?? ClinicTimeHelper.TodayDateOnly;
        var opDateDateTime = opDate.ToDateTime(TimeOnly.MinValue);
        var opDateNextDateTime = opDateDateTime.AddDays(1);

        // 1. Shifts & Attendance
        var allShifts = await _shiftResolver.GetAllShiftsForDateAsync(opDate, ct);
        var dentistShifts = allShifts.Where(s => s.Role == UserRole.Dentist).ToList();
        var receptionistShifts = allShifts.Where(s => s.Role == UserRole.Receptionist).ToList();

        var scheduledDentistIds = dentistShifts.Select(s => s.UserId).Distinct().ToList();
        var scheduledReceptionistIds = receptionistShifts.Select(s => s.UserId).Distinct().ToList();

        var todayRecords = await _dbContext.StaffAttendanceRecords
            .AsNoTracking()
            .Include(r => r.StaffUser)
                .ThenInclude(s => s.User)
            .Where(r => r.WorkDate == opDate)
            .ToListAsync(ct);

        var presentDentistIds = todayRecords
            .Where(r => r.StaffUser?.User?.Role == UserRole.Dentist && r.ClockInTime != null)
            .Select(r => r.StaffUserId)
            .Distinct()
            .ToList();

        var presentReceptionistIds = todayRecords
            .Where(r => r.StaffUser?.User?.Role == UserRole.Receptionist && r.ClockInTime != null)
            .Select(r => r.StaffUserId)
            .Distinct()
            .ToList();

        var onLeaveCount = allShifts.Count(s => s.IsApprovedLeave);
        var missingPunchCount = todayRecords.Count(r => r.Status == AttendanceStatus.MissingClockOut);

        var unresolvedCoverageIncidents = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .CountAsync(s => s.StaffRole == UserRole.Receptionist &&
                             s.WorkDate == opDate &&
                             s.Status == "AbsenceReported" &&
                             (s.Notes == null || !s.Notes.Contains("[Đã xử lý:")), ct);

        var workforceKpis = new WorkforceKpiDto
        {
            ScheduledDentistsCount = scheduledDentistIds.Count,
            PresentDentistsCount = presentDentistIds.Count,
            ClockedInDentistsCount = presentDentistIds.Count,
            VerifiedPresentDentistsCount = presentDentistIds.Count,
            ApprovedLeaveDentistsCount = dentistShifts.Count(s => s.IsApprovedLeave),
            ScheduledReceptionistsCount = scheduledReceptionistIds.Count,
            PresentReceptionistsCount = presentReceptionistIds.Count,
            ClockedInReceptionistsCount = presentReceptionistIds.Count,
            VerifiedPresentReceptionistsCount = presentReceptionistIds.Count,
            ApprovedLeaveReceptionistsCount = receptionistShifts.Count(s => s.IsApprovedLeave),
            OnLeaveStaffCount = onLeaveCount,
            MissingPunchCount = missingPunchCount,
            UnresolvedCoverageIncidentsCount = unresolvedCoverageIncidents
        };

        // 2. Receptionist Coverage based on actual configured shifts
        var receptionistCoverage = new List<ReceptionistCoverageDto>();

        var shiftGroups = receptionistShifts
            .GroupBy(s => (s.StartTime, s.EndTime, s.ShiftName))
            .OrderBy(g => g.Key.StartTime)
            .ToList();

        int shiftIdx = 1;
        if (shiftGroups.Any())
        {
            foreach (var group in shiftGroups)
            {
                var scheduleIds = group.Where(s => s.SourceScheduleId.HasValue).Select(s => s.SourceScheduleId!.Value).ToList();
                var schedCount = group.Select(s => s.UserId).Distinct().Count();
                var presentUsersInShift = todayRecords
                    .Where(r => r.StaffUser?.User?.Role == UserRole.Receptionist &&
                                r.ClockInTime != null &&
                                (r.WorkScheduleId.HasValue && scheduleIds.Contains(r.WorkScheduleId.Value) || group.Any(g => g.UserId == r.StaffUserId)))
                    .Select(r => r.StaffUserId)
                    .Distinct()
                    .ToList();

                var approvedLeavesInGroup = group.Count(s => s.IsApprovedLeave);

                receptionistCoverage.Add(new ReceptionistCoverageDto
                {
                    ShiftIndex = shiftIdx++,
                    ShiftName = group.Key.ShiftName,
                    StartTime = group.Key.StartTime,
                    EndTime = group.Key.EndTime,
                    ScheduledCount = schedCount,
                    PresentCount = presentUsersInShift.Count,
                    ClockedInCount = presentUsersInShift.Count,
                    VerifiedPresentCount = presentUsersInShift.Count,
                    ApprovedLeaveCount = approvedLeavesInGroup
                });
            }
        }
        else
        {
            receptionistCoverage.Add(new ReceptionistCoverageDto
            {
                ShiftIndex = 1,
                ShiftName = "Ca Sáng (08:00 - 12:00)",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                ScheduledCount = 0,
                PresentCount = 0
            });
            receptionistCoverage.Add(new ReceptionistCoverageDto
            {
                ShiftIndex = 2,
                ShiftName = "Ca Chiều (13:00 - 17:00)",
                StartTime = new TimeOnly(13, 0),
                EndTime = new TimeOnly(17, 0),
                ScheduledCount = 0,
                PresentCount = 0
            });
        }

        // 3. Today's Appointments & Services
        var todayServices = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Include(s => s.Appointment)
            .Where(s => s.ServiceDate == opDate)
            .ToListAsync(ct);

        var todayAppointments = todayServices
            .Select(s => s.Appointment)
            .DistinctBy(a => a.AppointmentId)
            .ToList();

        var activeStatuses = new[]
        {
            AppointmentStatus.PendingReceptionReview,
            AppointmentStatus.PendingDepartmentReview,
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.InService,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called
        };

        // 4. Dentist Duty Status
        var dentistStatuses = new List<DentistDutyStatusDto>();
        foreach (var shift in dentistShifts)
        {
            var record = todayRecords.FirstOrDefault(r =>
                r.StaffUserId == shift.UserId &&
                (!shift.SourceScheduleId.HasValue || r.WorkScheduleId == shift.SourceScheduleId));
            var isPresent = record?.ClockInTime != null;

            var dentistApptCount = todayServices.Count(s =>
                activeStatuses.Contains(s.Status) &&
                (s.AssignedDentistUserId == shift.UserId || (s.AssignedDentistUserId == null && s.RequestedDentistUserId == shift.UserId)));

            dentistStatuses.Add(new DentistDutyStatusDto
            {
                DentistUserId = shift.UserId,
                DentistName = shift.StaffName,
                DentistCode = shift.StaffCode,
                DepartmentName = shift.DepartmentName ?? string.Empty,
                ShiftStart = shift.StartTime,
                ShiftEnd = shift.EndTime,
                IsPresent = isPresent,
                ClockInTimeUtc = record?.ClockInTime,
                ScheduledAppointmentsCount = dentistApptCount
            });
        }

        // 5. Appointment Operations Breakdown
        var appointmentOps = new AppointmentOperationsDto
        {
            PendingReviewCount = todayAppointments.Count(a => a.Status == AppointmentStatus.PendingReceptionReview || a.Status == AppointmentStatus.PendingDepartmentReview),
            ConfirmedCount = todayAppointments.Count(a => a.Status == AppointmentStatus.Confirmed),
            CheckedInCount = todayAppointments.Count(a => a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.Called),
            InServiceCount = todayAppointments.Count(a => a.Status == AppointmentStatus.InService),
            CompletedCount = todayAppointments.Count(a => a.Status == AppointmentStatus.Completed),
            CancelledCount = todayAppointments.Count(a => a.Status == AppointmentStatus.Cancelled)
        };

        // 6. Doctor Leave & Disruptions (via AppointmentServices.PendingActionType)
        var pendingLeavesCount = await _dbContext.StaffLeaveRequests.AsNoTracking().CountAsync(lr => lr.StaffRole == UserRole.Dentist && lr.Status == "Pending", ct);
        var approvedLeavesTodayCount = await _dbContext.StaffLeaveRequests.AsNoTracking().CountAsync(lr => lr.StaffRole == UserRole.Dentist && lr.Status == "Approved" && lr.StartDate <= opDate && lr.EndDate >= opDate, ct);
        var activeDisruptionsCount = await _dbContext.AppointmentServices.AsNoTracking().CountAsync(s => s.PendingActionType != null, ct);
        var pendingPatientCount = await _dbContext.AppointmentServices.AsNoTracking().CountAsync(s => s.PendingActionType != null && s.Status == AppointmentStatus.AwaitingPatientResponse, ct);
        var pendingReceptionistCount = Math.Max(0, activeDisruptionsCount - pendingPatientCount);

        var nowUtc = DateTime.UtcNow;
        var urgentDisruptionsCount = await _dbContext.AppointmentServices
            .AsNoTracking()
            .CountAsync(s => s.PendingActionType != null && s.ScheduledStart <= nowUtc.AddHours(24), ct);

        var doctorLeaveOps = new DoctorLeaveDisruptionOperationsDto
        {
            PendingDentistLeavesCount = pendingLeavesCount,
            ApprovedDentistLeavesTodayCount = approvedLeavesTodayCount,
            ActiveDisruptionsCount = activeDisruptionsCount,
            DisruptionsPendingPatientCount = pendingPatientCount,
            DisruptionsPendingReceptionistCount = pendingReceptionistCount,
            UrgentDisruptionsCount = urgentDisruptionsCount
        };

        // 7. Financial Operations (Today & Month)
        var monthStartDateTime = new DateTime(opDate.Year, opDate.Month, 1);
        var monthEndDateTime = monthStartDateTime.AddMonths(1);

        var todayInvoices = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.CreatedAt >= opDateDateTime && i.CreatedAt < opDateNextDateTime && i.Status != "Cancelled" && i.Status != "Void")
            .ToListAsync(ct);

        var monthInvoices = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.CreatedAt >= monthStartDateTime && i.CreatedAt < monthEndDateTime && i.Status != "Cancelled" && i.Status != "Void")
            .ToListAsync(ct);

        var todayPayments = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.PaymentDate >= opDateDateTime && p.PaymentDate < opDateNextDateTime && p.Status == "Successful")
            .ToListAsync(ct);

        var monthPayments = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.PaymentDate >= monthStartDateTime && p.PaymentDate < monthEndDateTime && p.Status == "Successful")
            .ToListAsync(ct);

        var todayIncoming = todayPayments.Where(p => p.PaymentType != "Refund").Sum(p => p.Amount);
        var todayRefunded = todayPayments.Where(p => p.PaymentType == "Refund").Sum(p => p.Amount);
        var monthIncoming = monthPayments.Where(p => p.PaymentType != "Refund").Sum(p => p.Amount);
        var monthRefunded = monthPayments.Where(p => p.PaymentType == "Refund").Sum(p => p.Amount);

        var financialOps = new FinancialOperationsDto
        {
            TotalInvoicedToday = todayInvoices.Sum(i => i.FinalAmount),
            TotalCollectedToday = Math.Max(0m, todayIncoming - todayRefunded),
            TotalInvoicedMonth = monthInvoices.Sum(i => i.FinalAmount),
            TotalCollectedMonth = Math.Max(0m, monthIncoming - monthRefunded),
            UnpaidInvoicesCount = todayInvoices.Count(i => i.Status == "Unpaid" || i.Status == "Pending"),
            PartiallyPaidInvoicesCount = todayInvoices.Count(i => i.Status == "PartiallyPaid")
        };

        // 8. Medicine Inventory (3-table pharmacy: Medicines.StockQuantity <= MinimumStockAlert)
        var activeMedicines = await _dbContext.Medicines
            .AsNoTracking()
            .Where(m => m.IsActive)
            .ToListAsync(ct);

        int expiredCount = 0;
        int nearExpiryCount = 0;
        int lowStockCount = 0;
        var alertItems = new List<MedicineBatchAlertItemDto>();

        foreach (var m in activeMedicines)
        {
            if (m.StockQuantity <= m.MinimumStockAlert)
            {
                lowStockCount++;
                alertItems.Add(new MedicineBatchAlertItemDto
                {
                    MedicineBatchId = m.MedicineId,
                    MedicineName = m.MedicineName,
                    BatchNumber = m.MedicineCode,
                    ExpiryDate = opDate.AddMonths(12),
                    DaysRemaining = 365,
                    RemainingQuantity = m.StockQuantity,
                    AlertType = "LowStock"
                });
            }
        }

        var medicineInventory = new MedicineInventoryAlertDto
        {
            ExpiredBatchesCount = expiredCount,
            NearExpiryBatchesCount = nearExpiryCount,
            LowStockMedicinesCount = lowStockCount,
            AlertItems = alertItems.OrderBy(a => a.RemainingQuantity).Take(15).ToList()
        };

        // 9. Actionable Alerts
        var alerts = new List<ClinicOperationalAlertDto>();

        foreach (var cov in receptionistCoverage.Where(c => c.HasShortage))
        {
            alerts.Add(new ClinicOperationalAlertDto
            {
                AlertType = "WorkforceShortage",
                Severity = "Critical",
                Title = "Thiếu định biên tiếp tân tại quầy",
                Message = $"{cov.ShiftName} hiện chỉ có {cov.PresentCount}/2 tiếp tân có mặt điểm danh. Cần bổ sung nhân sự trực quầy gấp.",
                ActionUrl = "/Admin/Workforce/Report?role=Receptionist",
                ActionLabel = "Kiểm tra bảng công Tiếp tân"
            });
        }

        foreach (var d in dentistStatuses.Where(d => d.HasAlert))
        {
            alerts.Add(new ClinicOperationalAlertDto
            {
                AlertType = "DentistDutyMissing",
                Severity = "Critical",
                Title = "Bác sĩ chưa có mặt trực khám",
                Message = $"Bác sĩ {d.DentistName} có {d.ScheduledAppointmentsCount} lịch hẹn hôm nay nhưng chưa thực hiện chấm công vào.",
                ActionUrl = "/Admin/DentistSchedules",
                ActionLabel = "Kiểm tra lịch trực Bác sĩ"
            });
        }

        if (urgentDisruptionsCount > 0)
        {
            alerts.Add(new ClinicOperationalAlertDto
            {
                AlertType = "UrgentDisruption",
                Severity = "Critical",
                Title = "Lịch khám bị gián đoạn khẩn cấp",
                Message = $"Có {urgentDisruptionsCount} ca khám bị ảnh hưởng do bác sĩ nghỉ phép trong 24 giờ tới cần điều phối ngay.",
                ActionUrl = "/Receptionist/Appointments/Disruptions",
                ActionLabel = "Xử lý gián đoạn"
            });
        }

        if (unresolvedCoverageIncidents > 0)
        {
            alerts.Add(new ClinicOperationalAlertDto
            {
                AlertType = "UnresolvedIncident",
                Severity = "Warning",
                Title = "Sự cố vắng mặt đột xuất chưa xử lý",
                Message = $"Có {unresolvedCoverageIncidents} báo cáo vắng mặt khẩn cấp/thiếu nhân sự lễ tân chưa được xử lý dứt điểm.",
                ActionUrl = "/Admin/ReceptionistSchedule",
                ActionLabel = "Xem sự cố"
            });
        }

        if (lowStockCount > 0)
        {
            alerts.Add(new ClinicOperationalAlertDto
            {
                AlertType = "LowStockMedicine",
                Severity = "Warning",
                Title = "Thuốc chạm ngưỡng tồn kho tối thiểu",
                Message = $"Có {lowStockCount} loại thuốc trong kho đang ở mức tồn kho thấp cần nhập bổ sung.",
                ActionUrl = "/Admin/Medicine",
                ActionLabel = "Quản lý kho thuốc"
            });
        }

        if (appointmentOps.PendingReviewCount > 5)
        {
            alerts.Add(new ClinicOperationalAlertDto
            {
                AlertType = "PendingAppointmentsQueue",
                Severity = "Warning",
                Title = "Ồn ứ yêu cầu đặt hẹn chờ duyệt",
                Message = $"Có {appointmentOps.PendingReviewCount} lịch hẹn đang chờ tiếp tân tiếp nhận và xử lý.",
                ActionUrl = "/Admin/Appointments",
                ActionLabel = "Xem danh sách hẹn"
            });
        }

        var result = new ClinicOperationsDashboardDto
        {
            OperationDate = opDate,
            WorkforceKpis = workforceKpis,
            ReceptionistCoverage = receptionistCoverage,
            DentistStatuses = dentistStatuses,
            AppointmentOperations = appointmentOps,
            DoctorLeaveOperations = doctorLeaveOps,
            FinancialOperations = financialOps,
            MedicineInventory = medicineInventory,
            ActiveAlerts = alerts
        };

        return ApiResponse<ClinicOperationsDashboardDto>.Ok(result);
    }
}
