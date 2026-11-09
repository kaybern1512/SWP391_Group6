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
            .Where(r => r.WorkDate == opDate)
            .ToListAsync(ct);

        var presentDentistIds = todayRecords
            .Where(r => r.Role == UserRole.Dentist && r.ClockInTimeUtc != null)
            .Select(r => r.UserId)
            .Distinct()
            .ToList();

        var presentReceptionistIds = todayRecords
            .Where(r => r.Role == UserRole.Receptionist && r.ClockInTimeUtc != null)
            .Select(r => r.UserId)
            .Distinct()
            .ToList();

        var onLeaveCount = todayRecords.Count(r => r.IsApprovedLeave) +
                           allShifts.Count(s => s.IsApprovedLeave && !todayRecords.Any(r => r.ShiftIdentifier == s.ShiftIdentifier));
        var missingPunchCount = todayRecords.Count(r => r.Status == AttendanceStatus.MissingClockOut);

        var unresolvedCoverageIncidents = await _dbContext.ReceptionistCoverageIncidents
            .AsNoTracking()
            .CountAsync(i => i.WorkDate == opDate && i.Status == "Open", ct);

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
                var shiftIdList = group.Select(s => s.ShiftIdentifier).ToList();
                var schedCount = group.Select(s => s.UserId).Distinct().Count();
                var presentUsersInShift = todayRecords
                    .Where(r => r.Role == UserRole.Receptionist && shiftIdList.Contains(r.ShiftIdentifier) && r.ClockInTimeUtc != null)
                    .Select(r => r.UserId)
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
            // Default placeholder if no receptionist schedules exist today
            receptionistCoverage.Add(new ReceptionistCoverageDto
            {
                ShiftIndex = 1,
                ShiftName = "Ca Sáng (07:00 - 13:00)",
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(13, 0),
                ScheduledCount = 0,
                PresentCount = 0
            });
            receptionistCoverage.Add(new ReceptionistCoverageDto
            {
                ShiftIndex = 2,
                ShiftName = "Ca Chiều (13:00 - 19:00)",
                StartTime = new TimeOnly(13, 0),
                EndTime = new TimeOnly(19, 0),
                ScheduledCount = 0,
                PresentCount = 0
            });
        }

        // 3. Today's Appointments
        var todayAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.ScheduledStart != null && a.ScheduledStart.Value >= opDateDateTime && a.ScheduledStart.Value < opDateNextDateTime)
            .ToListAsync(ct);

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
            var record = todayRecords.FirstOrDefault(r => r.UserId == shift.UserId && r.ShiftIdentifier == shift.ShiftIdentifier);
            var isPresent = record?.ClockInTimeUtc != null;

            var dentistApptCount = todayAppointments.Count(a =>
                activeStatuses.Contains(a.Status) &&
                (a.AssignedDentistUserId == shift.UserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == shift.UserId)));

            dentistStatuses.Add(new DentistDutyStatusDto
            {
                DentistUserId = shift.UserId,
                DentistName = shift.StaffName,
                DentistCode = shift.StaffCode,
                DepartmentName = shift.DepartmentName ?? string.Empty,
                ShiftStart = shift.StartTime,
                ShiftEnd = shift.EndTime,
                IsPresent = isPresent,
                ClockInTimeUtc = record?.ClockInTimeUtc,
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

        // 6. Doctor Leave & Disruptions
        var pendingLeavesCount = await _dbContext.DentistLeaveRequests.AsNoTracking().CountAsync(lr => lr.Status == "Pending", ct);
        var approvedLeavesTodayCount = await _dbContext.DentistLeaveRequests.AsNoTracking().CountAsync(lr => lr.Status == "Approved" && lr.StartDate <= opDate && lr.EndDate >= opDate, ct);
        var activeDisruptionsCount = await _dbContext.AppointmentDisruptions.AsNoTracking().CountAsync(d => d.Status != "Resolved" && d.Status != "Cancelled", ct);
        var pendingPatientCount = await _dbContext.AppointmentDisruptions.AsNoTracking().CountAsync(d => d.Status == "PendingPatientAction", ct);
        var pendingReceptionistCount = await _dbContext.AppointmentDisruptions.AsNoTracking().CountAsync(d => d.Status == "PendingReceptionist", ct);

        var nowUtc = DateTime.UtcNow;
        var urgentDisruptionsCount = await _dbContext.AppointmentDisruptions
            .AsNoTracking()
            .Include(d => d.Appointment)
            .CountAsync(d => (d.Status != "Resolved" && d.Status != "Cancelled") && d.Appointment != null && d.Appointment.ScheduledStart <= nowUtc.AddHours(24), ct);

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
            .Where(p => (p.PaidAt ?? p.CreatedAt) >= opDateDateTime && (p.PaidAt ?? p.CreatedAt) < opDateNextDateTime && (p.Status == "Completed" || p.Status == "Paid" || p.Status == "Success"))
            .ToListAsync(ct);

        var monthPayments = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => (p.PaidAt ?? p.CreatedAt) >= monthStartDateTime && (p.PaidAt ?? p.CreatedAt) < monthEndDateTime && (p.Status == "Completed" || p.Status == "Paid" || p.Status == "Success"))
            .ToListAsync(ct);

        var financialOps = new FinancialOperationsDto
        {
            TotalInvoicedToday = todayInvoices.Sum(i => i.TotalAmount),
            TotalCollectedToday = todayPayments.Sum(p => p.Amount),
            TotalInvoicedMonth = monthInvoices.Sum(i => i.TotalAmount),
            TotalCollectedMonth = monthPayments.Sum(p => p.Amount),
            UnpaidInvoicesCount = todayInvoices.Count(i => i.Status == "Unpaid" || i.Status == "Pending"),
            PartiallyPaidInvoicesCount = todayInvoices.Count(i => i.Status == "PartiallyPaid")
        };

        // 8. Medicine Inventory (Batches & Low Stock)
        var todayDate = opDate;
        var in30DaysDate = todayDate.AddDays(30);

        var activeBatches = await _dbContext.MedicineBatches
            .AsNoTracking()
            .Include(b => b.Medicine)
            .Where(b => b.QuantityAvailable > 0)
            .ToListAsync(ct);

        int expiredCount = 0;
        int nearExpiryCount = 0;
        int lowStockCount = 0;
        var alertItems = new List<MedicineBatchAlertItemDto>();

        foreach (var b in activeBatches)
        {
            if (b.ExpiryDate < todayDate)
            {
                expiredCount++;
                alertItems.Add(new MedicineBatchAlertItemDto
                {
                    MedicineBatchId = b.MedicineBatchId,
                    MedicineName = b.Medicine?.MedicineName ?? $"Thuốc #{b.MedicineId}",
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate,
                    DaysRemaining = b.ExpiryDate.DayNumber - todayDate.DayNumber,
                    RemainingQuantity = b.QuantityAvailable,
                    AlertType = "Expired"
                });
            }
            else if (b.ExpiryDate <= in30DaysDate)
            {
                nearExpiryCount++;
                alertItems.Add(new MedicineBatchAlertItemDto
                {
                    MedicineBatchId = b.MedicineBatchId,
                    MedicineName = b.Medicine?.MedicineName ?? $"Thuốc #{b.MedicineId}",
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate,
                    DaysRemaining = b.ExpiryDate.DayNumber - todayDate.DayNumber,
                    RemainingQuantity = b.QuantityAvailable,
                    AlertType = "NearExpiry"
                });
            }
            else if (b.QuantityAvailable <= 10)
            {
                lowStockCount++;
                alertItems.Add(new MedicineBatchAlertItemDto
                {
                    MedicineBatchId = b.MedicineBatchId,
                    MedicineName = b.Medicine?.MedicineName ?? $"Thuốc #{b.MedicineId}",
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate,
                    DaysRemaining = b.ExpiryDate.DayNumber - todayDate.DayNumber,
                    RemainingQuantity = b.QuantityAvailable,
                    AlertType = "LowStock"
                });
            }
        }

        var medicineInventory = new MedicineInventoryAlertDto
        {
            ExpiredBatchesCount = expiredCount,
            NearExpiryBatchesCount = nearExpiryCount,
            LowStockMedicinesCount = lowStockCount,
            AlertItems = alertItems.OrderBy(a => a.ExpiryDate).Take(15).ToList()
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

        if (expiredCount > 0)
        {
            alerts.Add(new ClinicOperationalAlertDto
            {
                AlertType = "ExpiredMedicine",
                Severity = "Critical",
                Title = "Thuốc trong kho đã hết hạn sử dụng",
                Message = $"Phát hiện {expiredCount} lô thuốc đã quá hạn sử dụng trong kho. Cần cô lập và xử lý tiêu hủy ngay lập tức.",
                ActionUrl = "/Admin/Medicine",
                ActionLabel = "Quản lý kho thuốc"
            });
        }

        if (nearExpiryCount > 0)
        {
            alerts.Add(new ClinicOperationalAlertDto
            {
                AlertType = "NearExpiryMedicine",
                Severity = "Warning",
                Title = "Lô thuốc cận hạn sử dụng (< 30 ngày)",
                Message = $"Có {nearExpiryCount} lô thuốc sắp hết hạn trong 30 ngày tới. Ưu tiên cấp phát trước.",
                ActionUrl = "/Admin/Medicine",
                ActionLabel = "Xem lô cận hạn"
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
