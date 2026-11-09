using System;
using System.Collections.Generic;

namespace DentalClinic.Application.Features.Operations.DTOs;

public class ClinicOperationsDashboardDto
{
    public DateOnly OperationDate { get; set; }
    public WorkforceKpiDto WorkforceKpis { get; set; } = new();
    public List<ReceptionistCoverageDto> ReceptionistCoverage { get; set; } = new();
    public List<DentistDutyStatusDto> DentistStatuses { get; set; } = new();
    public AppointmentOperationsDto AppointmentOperations { get; set; } = new();
    public FinancialOperationsDto FinancialOperations { get; set; } = new();
    public MedicineInventoryAlertDto MedicineInventory { get; set; } = new();
    public List<ClinicOperationalAlertDto> ActiveAlerts { get; set; } = new();
    public DoctorLeaveDisruptionOperationsDto DoctorLeaveOperations { get; set; } = new();
}

public class WorkforceKpiDto
{
    public int ScheduledDentistsCount { get; set; }
    public int PresentDentistsCount { get; set; }
    public int ClockedInDentistsCount { get; set; }
    public int VerifiedPresentDentistsCount { get; set; }
    public int ApprovedLeaveDentistsCount { get; set; }
    public int ScheduledReceptionistsCount { get; set; }
    public int PresentReceptionistsCount { get; set; }
    public int ClockedInReceptionistsCount { get; set; }
    public int VerifiedPresentReceptionistsCount { get; set; }
    public int ApprovedLeaveReceptionistsCount { get; set; }
    public int OnLeaveStaffCount { get; set; }
    public int MissingPunchCount { get; set; }
    public int UnresolvedCoverageIncidentsCount { get; set; }
}

public class ReceptionistCoverageDto
{
    public int ShiftIndex { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int ScheduledCount { get; set; }
    public int PresentCount { get; set; }
    public int ClockedInCount { get; set; }
    public int VerifiedPresentCount { get; set; }
    public int ApprovedLeaveCount { get; set; }
    public bool HasShortage => PresentCount < 2;
    public string StatusText => HasShortage
        ? $"Thiếu nhân sự ({PresentCount}/2 tối thiểu)"
        : $"Đảm bảo ({PresentCount} nhân sự)";
}

public class DentistDutyStatusDto
{
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public string DentistCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public TimeOnly ShiftStart { get; set; }
    public TimeOnly ShiftEnd { get; set; }
    public bool IsPresent { get; set; }
    public DateTime? ClockInTimeUtc { get; set; }
    public int ScheduledAppointmentsCount { get; set; }
    public bool HasAlert => ScheduledAppointmentsCount > 0 && !IsPresent;
    public string? AlertMessage => HasAlert
        ? $"Có {ScheduledAppointmentsCount} lịch khám hôm nay nhưng chưa có mặt điểm danh!"
        : null;
}

public class AppointmentOperationsDto
{
    public int PendingReviewCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int CheckedInCount { get; set; }
    public int InServiceCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public int TotalToday => PendingReviewCount + ConfirmedCount + CheckedInCount + InServiceCount + CompletedCount + CancelledCount;
}

public class FinancialOperationsDto
{
    public decimal TotalInvoicedToday { get; set; }
    public decimal TotalCollectedToday { get; set; }
    public decimal TotalOutstandingToday => Math.Max(0, TotalInvoicedToday - TotalCollectedToday);

    public decimal TotalInvoicedMonth { get; set; }
    public decimal TotalCollectedMonth { get; set; }
    public decimal TotalOutstandingMonth => Math.Max(0, TotalInvoicedMonth - TotalCollectedMonth);

    public int UnpaidInvoicesCount { get; set; }
    public int PartiallyPaidInvoicesCount { get; set; }
}

public class MedicineInventoryAlertDto
{
    public int ExpiredBatchesCount { get; set; }
    public int NearExpiryBatchesCount { get; set; } // within 30 days
    public int LowStockMedicinesCount { get; set; }
    public List<MedicineBatchAlertItemDto> AlertItems { get; set; } = new();
}

public class MedicineBatchAlertItemDto
{
    public long MedicineBatchId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int DaysRemaining { get; set; }
    public int RemainingQuantity { get; set; }
    public string AlertType { get; set; } = string.Empty; // "Expired", "NearExpiry", "LowStock"
}

public class ClinicOperationalAlertDto
{
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning"; // "Warning", "Critical", "Info"
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ActionUrl { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
}

public class UserLockImpactDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int FutureShiftsCount { get; set; }
    public int FutureAppointmentsCount { get; set; }
    public bool TriggersReceptionistShortage { get; set; }
    public List<string> Warnings { get; set; } = new();
    public bool CanProceed => true; // Allows with admin confirmation
}

public class ServiceDeactivationImpactDto
{
    public long DepartmentServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int FutureAppointmentsCount { get; set; }
    public List<string> AffectedDates { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class RoomChairDeactivationImpactDto
{
    public long TargetId { get; set; }
    public string TargetType { get; set; } = "Room"; // "Room" or "Chair"
    public string Name { get; set; } = string.Empty;
    public int FutureAppointmentsCount { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class PriceChangeImpactDto
{
    public long DepartmentServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal NewPrice { get; set; }
    public int PastInvoicesCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class DoctorLeaveDisruptionOperationsDto
{
    public int PendingDentistLeavesCount { get; set; }
    public int ApprovedDentistLeavesTodayCount { get; set; }
    public int ActiveDisruptionsCount { get; set; }
    public int DisruptionsPendingPatientCount { get; set; }
    public int DisruptionsPendingReceptionistCount { get; set; }
    public int UrgentDisruptionsCount { get; set; }
}
