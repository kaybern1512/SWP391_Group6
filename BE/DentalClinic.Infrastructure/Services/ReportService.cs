using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using DentalClinic.Application.Common.Helpers;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.Reports.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        DentalClinicDbContext dbContext,
        ILogger<ReportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApiResponse<RevenueReportDto>> GetRevenueReportAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? departmentId = null,
        CancellationToken ct = default)
    {
        var (startUtc, endUtc) = ClinicDateTimeHelper.ToClinicUtcRange(fromDate, toDate, 30);

        var query = _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .Include(i => i.GeneratedByUser)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.Department)
            .Where(i => i.CreatedAt >= startUtc && i.CreatedAt < endUtc);

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(i => i.Appointment != null && i.Appointment.DepartmentId == departmentId.Value);
        }

        var invoices = await query.OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

        // Billed revenue excludes Cancelled invoices
        var validInvoices = invoices.Where(i => !string.Equals(i.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)).ToList();

        // Cash collected in the reporting period [startUtc, endUtc) based on actual transaction payment time
        var periodPaymentsQuery = _dbContext.Payments
            .AsNoTracking()
            .Include(p => p.Invoice)
                .ThenInclude(i => i.Appointment)
            .Where(p => (p.PaidAt ?? p.CreatedAt) >= startUtc && (p.PaidAt ?? p.CreatedAt) < endUtc)
            .Where(p => p.Status == "Successful" ||
                        p.Status == "Paid" ||
                        p.Status == "Completed")
            .Where(p => p.Invoice != null && p.Invoice.Status != "Cancelled");

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            periodPaymentsQuery = periodPaymentsQuery.Where(p => p.Invoice.Appointment != null && p.Invoice.Appointment.DepartmentId == departmentId.Value);
        }

        var periodPayments = await periodPaymentsQuery.ToListAsync(ct);

        // 1. Accrual / Invoiced Cohort Metrics
        var totalRevenue = validInvoices.Sum(i => i.TotalAmount);
        var invoicesIssuedPaid = validInvoices.SelectMany(i => i.Payments)
            .Where(p => string.Equals(p.Status, "Successful", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Amount);
        var invoicesIssuedPaidAtPeriodEnd = validInvoices.SelectMany(i => i.Payments)
            .Where(p => (p.PaidAt ?? p.CreatedAt) < endUtc)
            .Where(p => string.Equals(p.Status, "Successful", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Amount);

        var totalRemaining = Math.Max(0, totalRevenue - invoicesIssuedPaid);
        var outstandingAtPeriodEnd = Math.Max(0, totalRevenue - invoicesIssuedPaidAtPeriodEnd);

        // 2. Cash Collected in Period (Settlement Date Basis)
        var cashCollectedInPeriod = periodPayments.Sum(p => p.Amount);
        var totalPaid = cashCollectedInPeriod > 0 || !validInvoices.Any() ? cashCollectedInPeriod : invoicesIssuedPaid;

        // Invoice counts
        var totalInvoices = invoices.Count;
        var paidInvoices = invoices.Count(i => string.Equals(i.Status, "Paid", StringComparison.OrdinalIgnoreCase));
        var partiallyPaidInvoices = invoices.Count(i => string.Equals(i.Status, "PartiallyPaid", StringComparison.OrdinalIgnoreCase));
        var unpaidInvoices = invoices.Count(i => string.Equals(i.Status, "Unpaid", StringComparison.OrdinalIgnoreCase));
        var cancelledInvoices = invoices.Count(i => string.Equals(i.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));

        // Period comparison: Previous equivalent duration
        var duration = endUtc - startUtc;
        var prevEndUtc = startUtc;
        var prevStartUtc = startUtc - duration;

        var prevInvoicesQuery = _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Payments)
            .Where(i => i.CreatedAt >= prevStartUtc && i.CreatedAt < prevEndUtc && i.Status != "Cancelled");

        var prevPaymentsQuery = _dbContext.Payments
            .AsNoTracking()
            .Include(p => p.Invoice)
                .ThenInclude(i => i.Appointment)
            .Where(p => (p.PaidAt ?? p.CreatedAt) >= prevStartUtc && (p.PaidAt ?? p.CreatedAt) < prevEndUtc)
            .Where(p => p.Status == "Successful" ||
                        p.Status == "Paid" ||
                        p.Status == "Completed")
            .Where(p => p.Invoice != null && p.Invoice.Status != "Cancelled");

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            prevInvoicesQuery = prevInvoicesQuery.Where(i => i.Appointment != null && i.Appointment.DepartmentId == departmentId.Value);
            prevPaymentsQuery = prevPaymentsQuery.Where(p => p.Invoice.Appointment != null && p.Invoice.Appointment.DepartmentId == departmentId.Value);
        }

        var prevInvoices = await prevInvoicesQuery.ToListAsync(ct);
        var prevPayments = await prevPaymentsQuery.ToListAsync(ct);

        var prevRevenue = prevInvoices.Sum(i => i.TotalAmount);
        var prevPaid = prevPayments.Sum(p => p.Amount);
        if (prevPaid == 0 && prevInvoices.Any())
        {
            prevPaid = prevInvoices.SelectMany(i => i.Payments)
                .Where(p => string.Equals(p.Status, "Successful", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                .Sum(p => p.Amount);
        }

        double revGrowth = 0;
        if (prevRevenue > 0)
        {
            revGrowth = Math.Round((double)((totalRevenue - prevRevenue) * 100 / prevRevenue), 1);
        }
        double paidGrowth = 0;
        if (prevPaid > 0)
        {
            paidGrowth = Math.Round((double)((totalPaid - prevPaid) * 100 / prevPaid), 1);
        }

        // Daily trend
        var clinicStart = ClinicDateTimeHelper.ToClinicTime(startUtc).Date;
        var clinicEnd = ClinicDateTimeHelper.ToClinicTime(endUtc.AddTicks(-1)).Date;

        var dailyMap = new Dictionary<string, (decimal revenue, decimal paid, int count)>();
        for (var d = clinicStart; d <= clinicEnd; d = d.AddDays(1))
        {
            dailyMap[d.ToString("yyyy-MM-dd")] = (0m, 0m, 0);
        }

        // 1. Daily Invoiced Revenue (by Invoice CreatedAt)
        foreach (var inv in validInvoices)
        {
            var key = ClinicDateTimeHelper.ToClinicTime(inv.CreatedAt).ToString("yyyy-MM-dd");
            if (dailyMap.ContainsKey(key))
            {
                var cur = dailyMap[key];
                dailyMap[key] = (cur.revenue + inv.TotalAmount, cur.paid, cur.count + 1);
            }
        }

        // 2. Daily Cash Collected (by Payment PaidAt/CreatedAt)
        foreach (var p in periodPayments)
        {
            var payTime = p.PaidAt ?? p.CreatedAt;
            var key = ClinicDateTimeHelper.ToClinicTime(payTime).ToString("yyyy-MM-dd");
            if (dailyMap.ContainsKey(key))
            {
                var cur = dailyMap[key];
                dailyMap[key] = (cur.revenue, cur.paid + p.Amount, cur.count);
            }
        }

        var dailyTrend = dailyMap.Select(kv =>
        {
            DateTime.TryParse(kv.Key, out var parsedDate);
            return new DailyRevenuePointDto
            {
                Date = kv.Key,
                DisplayDate = parsedDate.ToString("dd/MM"),
                Revenue = kv.Value.revenue,
                PaidAmount = kv.Value.paid,
                InvoiceCount = kv.Value.count
            };
        }).OrderBy(x => x.Date).ToList();

        // Weekly trend (Grouped by ISO calendar week)
        var weeklyInvoiceGroups = validInvoices
            .GroupBy(inv =>
            {
                var dt = ClinicDateTimeHelper.ToClinicTime(inv.CreatedAt);
                return (Year: ISOWeek.GetYear(dt), Week: ISOWeek.GetWeekOfYear(dt));
            })
            .ToDictionary(g => g.Key, g => new
            {
                Revenue = g.Sum(x => x.TotalAmount),
                Count = g.Count()
            });

        var weeklyPaymentGroups = periodPayments
            .GroupBy(p =>
            {
                var dt = ClinicDateTimeHelper.ToClinicTime(p.PaidAt ?? p.CreatedAt);
                return (Year: ISOWeek.GetYear(dt), Week: ISOWeek.GetWeekOfYear(dt));
            })
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        // Ensure all weeks in range exist
        var weeklyTrend = new List<WeeklyRevenuePointDto>();
        for (var d = clinicStart; d <= clinicEnd; d = d.AddDays(7))
        {
            var yr = ISOWeek.GetYear(d);
            var wk = ISOWeek.GetWeekOfYear(d);
            if (!weeklyTrend.Any(w => w.Year == yr && w.WeekNumber == wk))
            {
                var startOfWk = ISOWeek.ToDateTime(yr, wk, DayOfWeek.Monday);
                var endOfWk = startOfWk.AddDays(6);
                decimal wRev = 0, wPaid = 0;
                int wCount = 0;
                if (weeklyInvoiceGroups.TryGetValue((yr, wk), out var wg))
                {
                    wRev = wg.Revenue;
                    wCount = wg.Count;
                }
                if (weeklyPaymentGroups.TryGetValue((yr, wk), out var wp))
                {
                    wPaid = wp;
                }

                weeklyTrend.Add(new WeeklyRevenuePointDto
                {
                    Year = yr,
                    WeekNumber = wk,
                    WeekLabel = $"Tuần {wk:D2} ({startOfWk:dd/MM}-{endOfWk:dd/MM})",
                    StartDate = startOfWk.ToString("yyyy-MM-dd"),
                    EndDate = endOfWk.ToString("yyyy-MM-dd"),
                    Revenue = wRev,
                    PaidAmount = wPaid,
                    InvoiceCount = wCount
                });
            }
        }
        weeklyTrend = weeklyTrend.OrderBy(w => w.Year).ThenBy(w => w.WeekNumber).ToList();

        // Monthly trend
        var monthlyInvoiceGroups = validInvoices
            .GroupBy(inv =>
            {
                var dt = ClinicDateTimeHelper.ToClinicTime(inv.CreatedAt);
                return (dt.Year, dt.Month);
            })
            .ToDictionary(g => g.Key, g => new
            {
                Revenue = g.Sum(x => x.TotalAmount),
                Count = g.Count()
            });

        var monthlyPaymentGroups = periodPayments
            .GroupBy(p =>
            {
                var dt = ClinicDateTimeHelper.ToClinicTime(p.PaidAt ?? p.CreatedAt);
                return (dt.Year, dt.Month);
            })
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var monthlyTrend = new List<MonthlyRevenuePointDto>();
        for (var d = new DateTime(clinicStart.Year, clinicStart.Month, 1); d <= clinicEnd; d = d.AddMonths(1))
        {
            var key = (d.Year, d.Month);
            decimal mRev = 0, mPaid = 0;
            int mCount = 0;
            if (monthlyInvoiceGroups.TryGetValue(key, out var mg))
            {
                mRev = mg.Revenue;
                mCount = mg.Count;
            }
            if (monthlyPaymentGroups.TryGetValue(key, out var mp))
            {
                mPaid = mp;
            }

            monthlyTrend.Add(new MonthlyRevenuePointDto
            {
                Year = d.Year,
                Month = d.Month,
                MonthLabel = $"Tháng {d.Month:D2}/{d.Year}",
                Revenue = mRev,
                PaidAmount = mPaid,
                InvoiceCount = mCount
            });
        }
        monthlyTrend = monthlyTrend.OrderBy(m => m.Year).ThenBy(m => m.Month).ToList();

        // Department distribution
        var allDepartments = await _dbContext.Departments.AsNoTracking().ToListAsync(ct);
        var deptDistribution = new List<DepartmentStatisticDto>();

        foreach (var dept in allDepartments)
        {
            var deptInvoices = validInvoices.Where(i => i.Appointment != null && i.Appointment.DepartmentId == dept.DepartmentId).ToList();
            if (deptInvoices.Any() || departmentId == dept.DepartmentId)
            {
                var rev = deptInvoices.Sum(i => i.TotalAmount);
                var paid = deptInvoices.SelectMany(i => i.Payments)
                    .Where(p => string.Equals(p.Status, "Successful", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(p.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Amount);
                var pct = totalRevenue > 0 ? Math.Round((double)(rev * 100 / totalRevenue), 1) : 0;
                var dentistCount = await _dbContext.DentistDepartments.CountAsync(dd => dd.DepartmentId == dept.DepartmentId, ct);
                var completedAppts = deptInvoices.Count(i => i.Appointment.Status == AppointmentStatus.Completed);

                deptDistribution.Add(new DepartmentStatisticDto
                {
                    DepartmentId = dept.DepartmentId,
                    DepartmentName = dept.Name,
                    TotalAppointments = deptInvoices.Count,
                    CompletedAppointments = completedAppts,
                    TotalRevenue = rev,
                    TotalPaid = paid,
                    RevenuePercentage = pct,
                    DentistCount = dentistCount
                });
            }
        }

        var clinic = await _dbContext.ClinicProfiles.AsNoTracking().FirstOrDefaultAsync(ct);
        var invoiceDtos = invoices.Select(i => MapToInvoiceDto(i, clinic)).ToList();

        var report = new RevenueReportDto
        {
            FromDate = clinicStart,
            ToDate = clinicEnd,
            TotalRevenue = totalRevenue,
            TotalPaid = totalPaid,
            TotalRemaining = totalRemaining,
            CashCollectedInPeriod = cashCollectedInPeriod,
            InvoicesIssuedPaidAmount = invoicesIssuedPaid,
            OutstandingBalanceAtPeriodEnd = outstandingAtPeriodEnd,
            TotalInvoices = totalInvoices,
            PaidInvoices = paidInvoices,
            PartiallyPaidInvoices = partiallyPaidInvoices,
            UnpaidInvoices = unpaidInvoices,
            CancelledInvoices = cancelledInvoices,
            PreviousPeriodRevenue = prevRevenue,
            PreviousPeriodPaid = prevPaid,
            RevenueGrowthPercentage = revGrowth,
            PaidGrowthPercentage = paidGrowth,
            DailyTrend = dailyTrend,
            WeeklyTrend = weeklyTrend,
            MonthlyTrend = monthlyTrend,
            DepartmentDistribution = deptDistribution.OrderByDescending(d => d.TotalRevenue).ToList(),
            Invoices = invoiceDtos
        };

        return ApiResponse<RevenueReportDto>.Ok(report);
    }

    public async Task<ApiResponse<DepartmentPerformanceReportDto>> GetDepartmentPerformanceReportAsync(
        long departmentId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? callerUserId = null,
        string? callerRole = null,
        CancellationToken ct = default)
    {
        // Enforce DepartmentManager scoping
        if (string.Equals(callerRole, UserRole.DepartmentManager, StringComparison.OrdinalIgnoreCase))
        {
            if (!callerUserId.HasValue)
            {
                return ApiResponse<DepartmentPerformanceReportDto>.Fail("Không xác định được danh tính người dùng.");
            }

            if (departmentId <= 0)
            {
                var defaultDept = await _dbContext.Departments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.ManagerUserId == callerUserId.Value, ct);

                if (defaultDept == null)
                {
                    return ApiResponse<DepartmentPerformanceReportDto>.Fail("Bạn chưa được phân công quản lý chuyên khoa nào.");
                }
                departmentId = defaultDept.DepartmentId;
            }
            else
            {
                var managedDepartment = await _dbContext.Departments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DepartmentId == departmentId && d.ManagerUserId == callerUserId.Value, ct);

                if (managedDepartment == null)
                {
                    return ApiResponse<DepartmentPerformanceReportDto>.Fail("Bạn không có quyền truy cập dữ liệu của chuyên khoa này.");
                }
            }
        }

        var dept = await _dbContext.Departments
            .AsNoTracking()
            .Include(d => d.ManagerUser)
            .FirstOrDefaultAsync(d => d.DepartmentId == departmentId, ct);

        if (dept == null)
            return ApiResponse<DepartmentPerformanceReportDto>.Fail("Không tìm thấy thông tin chuyên khoa.");

        var dentistsPerf = await GetAllDentistsPerformanceAsync(departmentId, fromDate, toDate, callerUserId, callerRole, ct);
        var dentistList = dentistsPerf.Data ?? new List<DentistPerformanceDto>();

        var totalAppts = dentistList.Sum(d => d.TotalAppointments);
        var completedAppts = dentistList.Sum(d => d.CompletedAppointments);
        var cancelledAppts = dentistList.Sum(d => d.CancelledAppointments);
        var distinctPatients = dentistList.Sum(d => d.DistinctPatientsTreated);
        var completedSessions = dentistList.Sum(d => d.CompletedTreatmentSessions);
        var totalRev = dentistList.Sum(d => d.TotalRevenueGenerated);
        var totalPaid = dentistList.Sum(d => d.TotalPaidRevenue);

        var ratedDentists = dentistList.Where(d => d.TotalFeedbacks > 0).ToList();
        var avgRating = ratedDentists.Any() ? Math.Round(ratedDentists.Average(d => d.AverageRating), 1) : 5.0;

        var (startUtc, endUtc) = ClinicDateTimeHelper.ToClinicUtcRange(fromDate, toDate, 180);

        var report = new DepartmentPerformanceReportDto
        {
            DepartmentId = dept.DepartmentId,
            DepartmentName = dept.Name,
            ManagerName = dept.ManagerUser?.FullName ?? "Chưa phân bổ",
            FromDate = ClinicDateTimeHelper.ToClinicTime(startUtc).Date,
            ToDate = ClinicDateTimeHelper.ToClinicTime(endUtc.AddTicks(-1)).Date,
            TotalDentists = dentistList.Count,
            TotalAppointments = totalAppts,
            CompletedAppointments = completedAppts,
            CancelledAppointments = cancelledAppts,
            DistinctPatientsTreated = distinctPatients,
            CompletedTreatmentSessions = completedSessions,
            OverallDepartmentRating = avgRating,
            TotalDepartmentRevenue = totalRev,
            TotalDepartmentPaid = totalPaid,
            Dentists = dentistList.OrderByDescending(d => d.CompletedAppointments).ToList()
        };

        return ApiResponse<DepartmentPerformanceReportDto>.Ok(report);
    }

    public async Task<ApiResponse<List<DentistPerformanceDto>>> GetAllDentistsPerformanceAsync(
        long? departmentId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? callerUserId = null,
        string? callerRole = null,
        CancellationToken ct = default)
    {
        // Enforce DepartmentManager scoping
        List<long> allowedDeptIds = new();
        if (string.Equals(callerRole, UserRole.DepartmentManager, StringComparison.OrdinalIgnoreCase))
        {
            if (!callerUserId.HasValue)
            {
                return ApiResponse<List<DentistPerformanceDto>>.Fail("Không xác định được danh tính người dùng.");
            }

            allowedDeptIds = await _dbContext.Departments
                .AsNoTracking()
                .Where(d => d.ManagerUserId == callerUserId.Value)
                .Select(d => d.DepartmentId)
                .ToListAsync(ct);

            if (allowedDeptIds.Count == 0)
            {
                return ApiResponse<List<DentistPerformanceDto>>.Fail("Bạn chưa được phân công quản lý chuyên khoa nào.");
            }

            if (departmentId.HasValue && departmentId.Value > 0)
            {
                if (!allowedDeptIds.Contains(departmentId.Value))
                {
                    return ApiResponse<List<DentistPerformanceDto>>.Fail("Bạn không có quyền truy cập dữ liệu của chuyên khoa này.");
                }
            }
            else
            {
                // Force to their managed department, never allow viewing clinic-wide
                departmentId = allowedDeptIds.First();
            }
        }

        var (startUtc, endUtc) = ClinicDateTimeHelper.ToClinicUtcRange(fromDate, toDate, 180);

        // Get dentists list
        var dentistQuery = _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.User)
            .Include(d => d.DentistDepartments)
                .ThenInclude(dd => dd.Department)
            .AsQueryable();

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            dentistQuery = dentistQuery.Where(d => d.DentistDepartments.Any(dd => dd.DepartmentId == departmentId.Value));
        }
        else if (allowedDeptIds.Count > 0)
        {
            dentistQuery = dentistQuery.Where(d => d.DentistDepartments.Any(dd => allowedDeptIds.Contains(dd.DepartmentId)));
        }

        var dentists = await dentistQuery.ToListAsync(ct);

        // Query appointments by ScheduledStart date (with fallback to CreatedAt)
        var apptQuery = _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.PatientUser)
            .Include(a => a.Invoice)
                .ThenInclude(i => i!.Payments)
            .Include(a => a.VisitFeedback)
            .Include(a => a.Department)
            .Where(a => a.AssignedDentistUserId != null &&
                        ((a.ScheduledStart != null && a.ScheduledStart >= startUtc && a.ScheduledStart < endUtc) ||
                         (a.ScheduledStart == null && a.CreatedAt >= startUtc && a.CreatedAt < endUtc)));

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            apptQuery = apptQuery.Where(a => a.DepartmentId == departmentId.Value);
        }
        else if (allowedDeptIds.Count > 0)
        {
            apptQuery = apptQuery.Where(a => a.DepartmentId != null && allowedDeptIds.Contains(a.DepartmentId.Value));
        }

        var appointments = await apptQuery.ToListAsync(ct);

        // Query completed treatment sessions
        var sessionQuery = _dbContext.TreatmentSessions
            .AsNoTracking()
            .Include(s => s.Appointment)
            .Include(s => s.TreatmentPlanItem)
            .Where(s => s.SessionDate >= startUtc && s.SessionDate < endUtc);

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            sessionQuery = sessionQuery.Where(s => s.Appointment.DepartmentId == departmentId.Value);
        }
        else if (allowedDeptIds.Count > 0)
        {
            sessionQuery = sessionQuery.Where(s => s.Appointment.DepartmentId != null && allowedDeptIds.Contains(s.Appointment.DepartmentId.Value));
        }

        var sessions = await sessionQuery.ToListAsync(ct);

        var result = new List<DentistPerformanceDto>();

        foreach (var d in dentists)
        {
            var dept = d.DentistDepartments.FirstOrDefault()?.Department;
            var dAppts = appointments.Where(a => a.AssignedDentistUserId == d.UserId).ToList();

            var total = dAppts.Count;
            var completed = dAppts.Count(a => a.Status == AppointmentStatus.Completed);
            var cancelled = dAppts.Count(a => a.Status == AppointmentStatus.Cancelled || a.Status == AppointmentStatus.Rejected);
            var noShow = dAppts.Count(a => a.Status == AppointmentStatus.NoShow);
            var compRate = total > 0 ? Math.Round((double)completed * 100 / total, 1) : 0;

            // Distinct patients actually treated
            var distinctPatients = dAppts
                .Where(a => a.Status == AppointmentStatus.Completed && a.PatientUserId > 0)
                .Select(a => a.PatientUserId)
                .Distinct()
                .Count();

            // Completed treatment sessions:
            // Must belong to this dentist, within session date range, not on a cancelled/no-show appointment,
            // and must be actually completed (TreatmentPlanItem is Completed or TreatmentResult indicates valid completion).
            var completedSessions = sessions
                .Where(s => s.DentistUserId == d.UserId)
                .Where(s => s.Appointment == null ||
                           (s.Appointment.Status != AppointmentStatus.Cancelled &&
                            s.Appointment.Status != AppointmentStatus.NoShow &&
                            s.Appointment.Status != AppointmentStatus.Rejected))
                .Where(s => (s.TreatmentPlanItem != null && string.Equals(s.TreatmentPlanItem.Status, "Completed", StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrWhiteSpace(s.TreatmentResult) &&
                             !s.TreatmentResult.Contains("hủy", StringComparison.OrdinalIgnoreCase) &&
                             !s.TreatmentResult.Contains("thất bại", StringComparison.OrdinalIgnoreCase) &&
                             !s.TreatmentResult.Contains("dở dang", StringComparison.OrdinalIgnoreCase)))
                .Select(s => s.TreatmentSessionId)
                .Distinct()
                .Count();

            var feedbacks = dAppts.Where(a => a.VisitFeedback != null).Select(a => a.VisitFeedback!).ToList();
            var totalFb = feedbacks.Count;
            var avgRating = totalFb > 0 ? Math.Round(feedbacks.Average(f => (double)f.DentistRating), 1) : 5.0;

            var fiveStar = feedbacks.Count(f => f.DentistRating == 5);
            var fourStar = feedbacks.Count(f => f.DentistRating == 4);
            var threeStar = feedbacks.Count(f => f.DentistRating == 3);
            var twoStar = feedbacks.Count(f => f.DentistRating == 2);
            var oneStar = feedbacks.Count(f => f.DentistRating == 1);

            // Invoices: exclude Cancelled invoices
            var validDentistInvoices = dAppts
                .Where(a => a.Invoice != null && !string.Equals(a.Invoice.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                .Select(a => a.Invoice!)
                .ToList();

            var billedRev = validDentistInvoices.Sum(i => i.TotalAmount);
            var paidRev = validDentistInvoices.SelectMany(i => i.Payments)
                .Where(p => string.Equals(p.Status, "Successful", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                .Sum(p => p.Amount);

            var recentFb = dAppts
                .Where(a => a.VisitFeedback != null)
                .OrderByDescending(a => a.VisitFeedback!.CreatedAt)
                .Take(5)
                .Select(a => new FeedbackItemSummaryDto
                {
                    FeedbackId = a.VisitFeedback!.FeedbackId,
                    PatientName = a.PatientUser?.FullName ?? "Bệnh nhân",
                    DentistRating = a.VisitFeedback.DentistRating,
                    ClinicRating = a.VisitFeedback.ClinicRating,
                    Comment = a.VisitFeedback.Comment,
                    CreatedAt = a.VisitFeedback.CreatedAt
                })
                .ToList();

            result.Add(new DentistPerformanceDto
            {
                DentistUserId = d.UserId,
                DentistName = d.User?.FullName ?? "Bác sĩ",
                EmployeeCode = d.User?.EmployeeCode,
                DepartmentId = dept?.DepartmentId ?? 0,
                DepartmentName = dept?.Name ?? "--",
                TotalAppointments = total,
                CompletedAppointments = completed,
                CancelledAppointments = cancelled,
                NoShowAppointments = noShow,
                DistinctPatientsTreated = distinctPatients,
                CompletedTreatmentSessions = completedSessions,
                CompletionRate = compRate,
                TotalFeedbacks = totalFb,
                AverageRating = avgRating,
                FiveStarCount = fiveStar,
                FourStarCount = fourStar,
                ThreeStarCount = threeStar,
                TwoStarCount = twoStar,
                OneStarCount = oneStar,
                TotalRevenueGenerated = billedRev,
                TotalPaidRevenue = paidRev,
                RecentFeedbacks = recentFb
            });
        }

        return ApiResponse<List<DentistPerformanceDto>>.Ok(result.OrderByDescending(x => x.CompletedAppointments).ToList());
    }

    public async Task<byte[]> ExportInvoicesCsvAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? departmentId = null,
        CancellationToken ct = default)
    {
        var report = await GetRevenueReportAsync(fromDate, toDate, departmentId, ct);
        if (!report.Success)
        {
            if (report.Message != null && report.Message.Contains("quyền", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(report.Message);
            }
            throw new InvalidOperationException(report.Message ?? "Không thể lấy dữ liệu báo cáo doanh thu.");
        }
        var invoices = report.Data?.Invoices ?? new List<InvoiceDto>();

        var sb = new StringBuilder();
        sb.AppendLine("Mã Hóa Đơn,Mã Lịch Hẹn,Bệnh Nhân,Số Điện Thoại,Bác Sĩ Phụ Trách,Chuyên Khoa,Ngày Lập,Tổng Tiền (VNĐ),Đã Thu (VNĐ),Còn Lại (VNĐ),Trạng Thái");

        foreach (var inv in invoices)
        {
            var code = EscapeCsv(inv.InvoiceCode);
            var appt = EscapeCsv(inv.AppointmentCode ?? "--");
            var patient = EscapeCsv(inv.PatientName);
            var phone = EscapeCsv(inv.PatientPhoneNumber ?? "--");
            var dentist = EscapeCsv(inv.DentistName ?? "--");
            var dept = EscapeCsv(inv.DepartmentName ?? "--");
            var date = inv.CreatedAt.ToString("dd/MM/yyyy HH:mm");
            var total = inv.TotalAmount.ToString("F0");
            var paid = inv.PaidAmount.ToString("F0");
            var rem = inv.RemainingAmount.ToString("F0");
            var status = EscapeCsv(GetStatusLabel(inv.Status));

            sb.AppendLine($"{code},{appt},{patient},{phone},{dentist},{dept},{date},{total},{paid},{rem},{status}");
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public async Task<byte[]> ExportInvoicesXlsxAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? departmentId = null,
        CancellationToken ct = default)
    {
        var report = await GetRevenueReportAsync(fromDate, toDate, departmentId, ct);
        if (!report.Success)
        {
            if (report.Message != null && report.Message.Contains("quyền", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(report.Message);
            }
            throw new InvalidOperationException(report.Message ?? "Không thể lấy dữ liệu báo cáo doanh thu.");
        }
        var invoices = report.Data?.Invoices ?? new List<InvoiceDto>();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("BaoCaoDoanhThu");

        // Title row
        ws.Cell(1, 1).Value = "BÁO CÁO PHÂN TÍCH DOANH THU & VIỆN PHÍ - NHA KHOA DENTALCARE";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        ws.Range(1, 1, 1, 11).Merge().Style.Fill.BackgroundColor = XLColor.FromHtml("#0d6efd");

        var fStr = report.Data?.FromDate?.ToString("dd/MM/yyyy") ?? "--";
        var tStr = report.Data?.ToDate?.ToString("dd/MM/yyyy") ?? "--";
        ws.Cell(2, 1).Value = $"Thời gian báo cáo: Từ {fStr} đến {tStr} | Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Range(2, 1, 2, 11).Merge();

        // Summary KPI row
        ws.Cell(3, 1).Value = $"Tổng doanh thu hợp lệ: {report.Data?.TotalRevenue:N0} đ | Thực thu: {report.Data?.TotalPaid:N0} đ | Còn chờ thu: {report.Data?.TotalRemaining:N0} đ | Tổng HĐ: {report.Data?.TotalInvoices}";
        ws.Range(3, 1, 3, 11).Merge().Style.Font.Bold = true;

        // Table Header
        var headers = new[]
        {
            "Mã Hóa Đơn", "Mã Lịch Hẹn", "Bệnh Nhân", "Số Điện Thoại",
            "Bác Sĩ Phụ Trách", "Chuyên Khoa", "Ngày Lập",
            "Tổng Tiền (VNĐ)", "Đã Thu (VNĐ)", "Còn Lại (VNĐ)", "Trạng Thái"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(5, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#198754");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int row = 6;
        if (!invoices.Any())
        {
            ws.Cell(row, 1).Value = "Không có dữ liệu hóa đơn trong khoảng thời gian đã chọn.";
            ws.Range(row, 1, row, 11).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            row++;
        }
        else
        {
            foreach (var inv in invoices)
            {
                SetTextCell(ws.Cell(row, 1), inv.InvoiceCode);
                SetTextCell(ws.Cell(row, 2), inv.AppointmentCode ?? "--");
                SetTextCell(ws.Cell(row, 3), inv.PatientName);
                SetTextCell(ws.Cell(row, 4), inv.PatientPhoneNumber ?? "--");
                SetTextCell(ws.Cell(row, 5), inv.DentistName ?? "--");
                SetTextCell(ws.Cell(row, 6), inv.DepartmentName ?? "--");
                ws.Cell(row, 7).Value = inv.CreatedAt.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var cTotal = ws.Cell(row, 8);
                cTotal.Value = inv.TotalAmount;
                cTotal.Style.NumberFormat.Format = "#,##0 \"đ\"";
                cTotal.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var cPaid = ws.Cell(row, 9);
                cPaid.Value = inv.PaidAmount;
                cPaid.Style.NumberFormat.Format = "#,##0 \"đ\"";
                cPaid.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var cRem = ws.Cell(row, 10);
                cRem.Value = inv.RemainingAmount;
                cRem.Style.NumberFormat.Format = "#,##0 \"đ\"";
                cRem.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var cStatus = ws.Cell(row, 11);
                SetTextCell(cStatus, GetStatusLabel(inv.Status));
                cStatus.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                row++;
            }

            // Summary row at the bottom
            ws.Cell(row, 1).Value = "TỔNG CỘNG";
            ws.Range(row, 1, row, 7).Merge().Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            var sumTotal = ws.Cell(row, 8);
            sumTotal.Value = report.Data?.TotalRevenue ?? 0;
            sumTotal.Style.Font.Bold = true;
            sumTotal.Style.NumberFormat.Format = "#,##0 \"đ\"";

            var sumPaid = ws.Cell(row, 9);
            sumPaid.Value = report.Data?.TotalPaid ?? 0;
            sumPaid.Style.Font.Bold = true;
            sumPaid.Style.NumberFormat.Format = "#,##0 \"đ\"";

            var sumRem = ws.Cell(row, 10);
            sumRem.Value = report.Data?.TotalRemaining ?? 0;
            sumRem.Style.Font.Bold = true;
            sumRem.Style.NumberFormat.Format = "#,##0 \"đ\"";

            ws.Cell(row, 11).Value = $"{invoices.Count} hóa đơn";
            ws.Cell(row, 11).Style.Font.Bold = true;
            ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(row, 1, row, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fa");
        }

        ws.Columns().AdjustToContents(10, 60);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> ExportDentistPerformanceCsvAsync(
        long? departmentId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? callerUserId = null,
        string? callerRole = null,
        CancellationToken ct = default)
    {
        var report = await GetAllDentistsPerformanceAsync(departmentId, fromDate, toDate, callerUserId, callerRole, ct);
        if (!report.Success)
        {
            if (report.Message != null && report.Message.Contains("quyền", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(report.Message);
            }
            throw new InvalidOperationException(report.Message ?? "Không thể lấy dữ liệu báo cáo năng suất bác sĩ.");
        }
        var list = report.Data ?? new List<DentistPerformanceDto>();

        var sb = new StringBuilder();
        sb.AppendLine("Mã Nhân Viên,Tên Bác Sĩ,Chuyên Khoa,Tổng Ca Khám,Số Ca Hoàn Thành,Ca Bị Hủy,Số Bệnh Nhân Đã Khám,Buổi Điều Trị Hoàn Thành,Tỷ Lệ Hoàn Thành (%),Đánh Giá Trung Bình (Sao),Số Lượt Đánh Giá,Doanh Thu Hóa Đơn (VNĐ),Thực Thu (VNĐ)");

        foreach (var d in list)
        {
            var empCode = EscapeCsv(d.EmployeeCode ?? "--");
            var name = EscapeCsv(d.DentistName);
            var dept = EscapeCsv(d.DepartmentName);
            var total = d.TotalAppointments;
            var comp = d.CompletedAppointments;
            var cancelled = d.CancelledAppointments;
            var patients = d.DistinctPatientsTreated;
            var sessions = d.CompletedTreatmentSessions;
            var rate = d.CompletionRate.ToString("F1");
            var rating = d.AverageRating.ToString("F1");
            var count = d.TotalFeedbacks;
            var rev = d.TotalRevenueGenerated.ToString("F0");
            var paid = d.TotalPaidRevenue.ToString("F0");

            sb.AppendLine($"{empCode},{name},{dept},{total},{comp},{cancelled},{patients},{sessions},{rate},{rating},{count},{rev},{paid}");
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public async Task<byte[]> ExportDentistPerformanceXlsxAsync(
        long? departmentId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? callerUserId = null,
        string? callerRole = null,
        CancellationToken ct = default)
    {
        var report = await GetAllDentistsPerformanceAsync(departmentId, fromDate, toDate, callerUserId, callerRole, ct);
        if (!report.Success)
        {
            if (report.Message != null && report.Message.Contains("quyền", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(report.Message);
            }
            throw new InvalidOperationException(report.Message ?? "Không thể lấy dữ liệu báo cáo năng suất bác sĩ.");
        }
        var list = report.Data ?? new List<DentistPerformanceDto>();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("NangSuatBacSi");

        // Title row
        ws.Cell(1, 1).Value = "BÁO CÁO NĂNG SUẤT & ĐÁNH GIÁ HIỆU QUẢ BÁC SĨ - DENTALCARE";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        ws.Range(1, 1, 1, 13).Merge().Style.Fill.BackgroundColor = XLColor.FromHtml("#0d6efd");

        ws.Cell(2, 1).Value = $"Ngày xuất báo cáo: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Range(2, 1, 2, 13).Merge();

        // Table Header
        var headers = new[]
        {
            "Mã NV", "Tên Bác Sĩ", "Chuyên Khoa", "Tổng Ca Khám",
            "Đã Hoàn Thành", "Ca Bị Hủy", "Bệnh Nhân Đã Khám", "Buổi Đ.Trị Xong",
            "Tỷ Lệ Đạt (%)", "Đánh Giá (Sao)", "Số Đánh Giá",
            "Doanh Thu HĐ (VNĐ)", "Thực Thu (VNĐ)"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(4, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#198754");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int row = 5;
        if (!list.Any())
        {
            ws.Cell(row, 1).Value = "Không có dữ liệu bác sĩ trong khoảng thời gian đã chọn.";
            ws.Range(row, 1, row, 13).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        else
        {
            foreach (var d in list)
            {
                SetTextCell(ws.Cell(row, 1), d.EmployeeCode ?? "--");
                SetTextCell(ws.Cell(row, 2), d.DentistName);
                SetTextCell(ws.Cell(row, 3), d.DepartmentName);

                ws.Cell(row, 4).Value = d.TotalAppointments;
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(row, 5).Value = d.CompletedAppointments;
                ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 5).Style.Font.Bold = true;

                ws.Cell(row, 6).Value = d.CancelledAppointments;
                ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(row, 7).Value = d.DistinctPatientsTreated;
                ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(row, 8).Value = d.CompletedTreatmentSessions;
                ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var cRate = ws.Cell(row, 9);
                cRate.Value = d.CompletionRate;
                cRate.Style.NumberFormat.Format = "0.0\"%\"";
                cRate.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var cRating = ws.Cell(row, 10);
                cRating.Value = d.AverageRating;
                cRating.Style.NumberFormat.Format = "0.0";
                cRating.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(row, 11).Value = d.TotalFeedbacks;
                ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var cRev = ws.Cell(row, 12);
                cRev.Value = d.TotalRevenueGenerated;
                cRev.Style.NumberFormat.Format = "#,##0 \"đ\"";
                cRev.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var cPaid = ws.Cell(row, 13);
                cPaid.Value = d.TotalPaidRevenue;
                cPaid.Style.NumberFormat.Format = "#,##0 \"đ\"";
                cPaid.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                row++;
            }
        }

        ws.Columns().AdjustToContents(8, 50);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Escapes untrusted text for CSV, mitigating CWE-1236 Spreadsheet Formula Injection.
    /// Prefixes single quote if string begins with =, +, -, @, \t, or \r and is not a valid number.
    /// </summary>
    public static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";

        var trimmed = value.TrimStart();
        var isDangerous = trimmed.Length > 0 && (
            trimmed[0] == '=' ||
            trimmed[0] == '+' ||
            trimmed[0] == '-' ||
            trimmed[0] == '@' ||
            trimmed[0] == '\t' ||
            trimmed[0] == '\r');

        var safeValue = value;
        // Negative numbers like "-1000" or "-123.45" should remain valid numbers without prepended quote
        var isNegativeNumber = trimmed.Length > 1 && trimmed[0] == '-' && decimal.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);

        if (isDangerous && !isNegativeNumber)
        {
            safeValue = "'" + value;
        }

        var escaped = safeValue.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static void SetTextCell(IXLCell cell, string text)
    {
        cell.SetValue(text ?? string.Empty);
    }

    private static string GetStatusLabel(string status)
    {
        return status switch
        {
            "Paid" => "Đã thanh toán đủ",
            "PartiallyPaid" => "Đã đặt cọc một phần",
            "Unpaid" => "Chưa thanh toán",
            "Cancelled" => "Đã hủy",
            _ => status
        };
    }

    private static InvoiceDto MapToInvoiceDto(Invoice invoice, ClinicProfile? clinic)
    {
        var subTotal = invoice.InvoiceItems.Sum(i => i.Quantity * i.UnitPrice);
        var paidAmount = invoice.Payments
            .Where(p => string.Equals(p.Status, "Successful", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Amount);
        var remainingAmount = Math.Max(0, invoice.TotalAmount - paidAmount);
        var discountAmount = subTotal > invoice.TotalAmount ? (subTotal - invoice.TotalAmount) : 0m;

        return new InvoiceDto
        {
            InvoiceId = invoice.InvoiceId,
            InvoiceCode = invoice.InvoiceCode,
            AppointmentId = invoice.AppointmentId,
            AppointmentCode = invoice.Appointment?.AppointmentCode,
            PatientUserId = invoice.Appointment?.PatientUserId ?? 0,
            PatientCode = invoice.Appointment?.PatientUser?.PatientCode,
            PatientName = invoice.Appointment?.PatientUser?.FullName ?? "Bệnh nhân",
            PatientPhoneNumber = invoice.Appointment?.PatientUser?.EmergencyContact,
            PatientEmail = invoice.Appointment?.PatientUser?.User?.Email,
            PatientAddress = invoice.Appointment?.PatientUser?.Address,
            DentistName = invoice.Appointment?.AssignedDentistUser?.User?.FullName ?? "Chưa phân bổ",
            DepartmentName = invoice.Appointment?.Department?.Name ?? "Nha khoa",
            GeneratedByUserId = invoice.GeneratedByUserId,
            GeneratedByName = invoice.GeneratedByUser?.FullName ?? "Nhân viên thu ngân",
            SubTotal = subTotal,
            DiscountAmount = discountAmount,
            TotalAmount = invoice.TotalAmount,
            PaidAmount = paidAmount,
            RemainingAmount = remainingAmount,
            Status = invoice.Status,
            CreatedAt = invoice.CreatedAt,
            AppointmentDate = invoice.Appointment?.ScheduledStart,
            ReasonForVisit = invoice.Appointment?.ReasonForVisit,

            ClinicName = clinic?.Name ?? "NHA KHOA ĐA CHUYÊN KHOA DENTALCARE",
            ClinicAddress = clinic?.Address ?? "123 Đường Nguyễn Tri Phương, Phường 5, Quận 10, TP. Hồ Chí Minh",
            ClinicPhone = clinic?.PhoneNumber ?? "028 3822 9999",
            ClinicEmail = clinic?.Email ?? "contact@dentalcare.vn"
        };
    }
}
