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

        var onLeaveCount = todayRecords.Count(r => r.IsApprovedLeave);
        var missingPunchCount = todayRecords.Count(r => r.Status == AttendanceStatus.MissingClockOut);

        var workforceKpis = new WorkforceKpiDto
        {
            ScheduledDentistsCount = scheduledDentistIds.Count,
            PresentDentistsCount = presentDentistIds.Count,
            ScheduledReceptionistsCount = scheduledReceptionistIds.Count,
            PresentReceptionistsCount = presentReceptionistIds.Count,
            OnLeaveStaffCount = onLeaveCount,
            MissingPunchCount = missingPunchCount
        };

        // 2. Receptionist Coverage (Morning & Afternoon)
        var receptionistCoverage = new List<ReceptionistCoverageDto>();

        var morningShifts = receptionistShifts.Where(s => s.StartTime <= new TimeOnly(9, 0)).ToList();
        var morningRecords = todayRecords.Where(r => r.Role == UserRole.Receptionist && r.ScheduledStartTime <= new TimeOnly(9, 0) && r.ClockInTimeUtc != null).ToList();

        receptionistCoverage.Add(new ReceptionistCoverageDto
        {
            ShiftIndex = 1,
            ShiftName = "Ca Tiếp Tân Sáng (08:00 - 12:00)",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            ScheduledCount = morningShifts.Select(s => s.UserId).Distinct().Count(),
            PresentCount = morningRecords.Select(r => r.UserId).Distinct().Count()
        });

        var afternoonShifts = receptionistShifts.Where(s => s.StartTime >= new TimeOnly(12, 0)).ToList();
        var afternoonRecords = todayRecords.Where(r => r.Role == UserRole.Receptionist && r.ScheduledStartTime >= new TimeOnly(12, 0) && r.ClockInTimeUtc != null).ToList();

        receptionistCoverage.Add(new ReceptionistCoverageDto
        {
            ShiftIndex = 2,
            ShiftName = "Ca Tiếp Tân Chiều (13:00 - 17:00)",
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(17, 0),
            ScheduledCount = afternoonShifts.Select(s => s.UserId).Distinct().Count(),
            PresentCount = afternoonRecords.Select(r => r.UserId).Distinct().Count()
        });

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

        // 6. Financial Operations (Today & Month)
        var monthStartDateTime = new DateTime(opDate.Year, opDate.Month, 1);
        var monthEndDateTime = monthStartDateTime.AddMonths(1);

        var todayInvoices = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.CreatedAt >= opDateDateTime && i.CreatedAt < opDateNextDateTime)
            .ToListAsync(ct);

        var monthInvoices = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.CreatedAt >= monthStartDateTime && i.CreatedAt < monthEndDateTime)
            .ToListAsync(ct);

        var todayPayments = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => (p.PaidAt ?? p.CreatedAt) >= opDateDateTime && (p.PaidAt ?? p.CreatedAt) < opDateNextDateTime && p.Status == "Completed")
            .ToListAsync(ct);

        var monthPayments = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => (p.PaidAt ?? p.CreatedAt) >= monthStartDateTime && (p.PaidAt ?? p.CreatedAt) < monthEndDateTime && p.Status == "Completed")
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

        // 7. Medicine Inventory Alerts
        var todayDate = ClinicTimeHelper.TodayDateOnly;
        var in30DaysDate = todayDate.AddDays(30);

        var batches = await _dbContext.MedicineBatches
            .AsNoTracking()
            .Include(b => b.Medicine)
            .Where(b => b.QuantityAvailable > 0)
            .ToListAsync(ct);

        var alertItems = new List<MedicineBatchAlertItemDto>();
        int expiredCount = 0;
        int nearExpiryCount = 0;
        int lowStockCount = 0;

        foreach (var b in batches)
        {
            if (b.ExpiryDate <= todayDate)
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

        // 8. Actionable Alerts
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
            FinancialOperations = financialOps,
            MedicineInventory = medicineInventory,
            ActiveAlerts = alerts
        };

        return ApiResponse<ClinicOperationsDashboardDto>.Ok(result);
    }
}
