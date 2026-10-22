using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        var end = toDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
        var start = fromDate?.Date ?? end.Date.AddDays(-29); // Default 30 days

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
            .Where(i => i.CreatedAt >= start && i.CreatedAt <= end);

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(i => i.Appointment.DepartmentId == departmentId.Value);
        }

        var invoices = await query.OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

        var totalRevenue = invoices.Sum(i => i.TotalAmount);
        var totalPaid = invoices.SelectMany(i => i.Payments).Where(p => p.Status == "Successful" || p.Status == "Paid" || p.Status == "Completed").Sum(p => p.Amount);
        var totalRemaining = Math.Max(0, totalRevenue - totalPaid);

        // Daily trend calculation
        var dailyMap = new Dictionary<string, (decimal revenue, decimal paid, int count)>();
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
        {
            dailyMap[d.ToString("yyyy-MM-dd")] = (0m, 0m, 0);
        }

        foreach (var inv in invoices)
        {
            var key = inv.CreatedAt.ToString("yyyy-MM-dd");
            if (dailyMap.ContainsKey(key))
            {
                var cur = dailyMap[key];
                var invPaid = inv.Payments.Where(p => p.Status == "Successful" || p.Status == "Paid" || p.Status == "Completed").Sum(p => p.Amount);
                dailyMap[key] = (cur.revenue + inv.TotalAmount, cur.paid + invPaid, cur.count + 1);
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

        // Department distribution
        var allDepartments = await _dbContext.Departments.AsNoTracking().ToListAsync(ct);
        var deptDistribution = new List<DepartmentStatisticDto>();

        foreach (var dept in allDepartments)
        {
            var deptInvoices = invoices.Where(i => i.Appointment.DepartmentId == dept.DepartmentId).ToList();
            if (deptInvoices.Any() || departmentId == dept.DepartmentId)
            {
                var rev = deptInvoices.Sum(i => i.TotalAmount);
                var paid = deptInvoices.SelectMany(i => i.Payments).Where(p => p.Status == "Successful" || p.Status == "Paid" || p.Status == "Completed").Sum(p => p.Amount);
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
            FromDate = start,
            ToDate = end,
            TotalRevenue = totalRevenue,
            TotalPaid = totalPaid,
            TotalRemaining = totalRemaining,
            TotalInvoices = invoices.Count,
            PaidInvoices = invoices.Count(i => i.Status == "Paid"),
            PartiallyPaidInvoices = invoices.Count(i => i.Status == "PartiallyPaid"),
            UnpaidInvoices = invoices.Count(i => i.Status == "Unpaid"),
            DailyTrend = dailyTrend,
            DepartmentDistribution = deptDistribution.OrderByDescending(d => d.TotalRevenue).ToList(),
            Invoices = invoiceDtos
        };

        return ApiResponse<RevenueReportDto>.Ok(report);
    }

    public async Task<ApiResponse<DepartmentPerformanceReportDto>> GetDepartmentPerformanceReportAsync(
        long departmentId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var dept = await _dbContext.Departments
            .AsNoTracking()
            .Include(d => d.ManagerUser)
            .FirstOrDefaultAsync(d => d.DepartmentId == departmentId, ct);

        if (dept == null)
            return ApiResponse<DepartmentPerformanceReportDto>.Fail("Không tìm thấy thông tin chuyên khoa.");

        var dentistsPerf = await GetAllDentistsPerformanceAsync(departmentId, fromDate, toDate, ct);
        var dentistList = dentistsPerf.Data ?? new List<DentistPerformanceDto>();

        var totalAppts = dentistList.Sum(d => d.TotalAppointments);
        var completedAppts = dentistList.Sum(d => d.CompletedAppointments);
        var totalRev = dentistList.Sum(d => d.TotalRevenueGenerated);
        var ratedDentists = dentistList.Where(d => d.TotalFeedbacks > 0).ToList();
        var avgRating = ratedDentists.Any() ? Math.Round(ratedDentists.Average(d => d.AverageRating), 1) : 5.0;

        var report = new DepartmentPerformanceReportDto
        {
            DepartmentId = dept.DepartmentId,
            DepartmentName = dept.Name,
            ManagerName = dept.ManagerUser?.FullName ?? "Chưa phân bổ",
            FromDate = fromDate,
            ToDate = toDate,
            TotalDentists = dentistList.Count,
            TotalAppointments = totalAppts,
            CompletedAppointments = completedAppts,
            OverallDepartmentRating = avgRating,
            TotalDepartmentRevenue = totalRev,
            Dentists = dentistList.OrderByDescending(d => d.CompletedAppointments).ToList()
        };

        return ApiResponse<DepartmentPerformanceReportDto>.Ok(report);
    }

    public async Task<ApiResponse<List<DentistPerformanceDto>>> GetAllDentistsPerformanceAsync(
        long? departmentId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var end = toDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
        var start = fromDate?.Date ?? end.Date.AddMonths(-6);

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

        var dentists = await dentistQuery.ToListAsync(ct);

        // Query appointments in range
        var apptQuery = _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.PatientUser)
            .Include(a => a.Invoice)
            .Include(a => a.VisitFeedback)
            .Include(a => a.Department)
            .Where(a => a.AssignedDentistUserId != null &&
                        a.CreatedAt >= start && a.CreatedAt <= end);

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            apptQuery = apptQuery.Where(a => a.DepartmentId == departmentId.Value);
        }

        var appointments = await apptQuery.ToListAsync(ct);

        var result = new List<DentistPerformanceDto>();

        foreach (var d in dentists)
        {
            var dept = d.DentistDepartments.FirstOrDefault()?.Department;
            var dAppts = appointments.Where(a => a.AssignedDentistUserId == d.UserId).ToList();

            var total = dAppts.Count;
            var completed = dAppts.Count(a => a.Status == AppointmentStatus.Completed);
            var compRate = total > 0 ? Math.Round((double)completed * 100 / total, 1) : 0;

            var feedbacks = dAppts.Where(a => a.VisitFeedback != null).Select(a => a.VisitFeedback!).ToList();
            var totalFb = feedbacks.Count;
            var avgRating = totalFb > 0 ? Math.Round(feedbacks.Average(f => (double)f.DentistRating), 1) : 5.0;

            var fiveStar = feedbacks.Count(f => f.DentistRating == 5);
            var fourStar = feedbacks.Count(f => f.DentistRating == 4);
            var threeStar = feedbacks.Count(f => f.DentistRating == 3);
            var twoStar = feedbacks.Count(f => f.DentistRating == 2);
            var oneStar = feedbacks.Count(f => f.DentistRating == 1);

            var rev = dAppts.Where(a => a.Invoice != null).Sum(a => a.Invoice!.TotalAmount);

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
                CompletionRate = compRate,
                TotalFeedbacks = totalFb,
                AverageRating = avgRating,
                FiveStarCount = fiveStar,
                FourStarCount = fourStar,
                ThreeStarCount = threeStar,
                TwoStarCount = twoStar,
                OneStarCount = oneStar,
                TotalRevenueGenerated = rev,
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
        var invoices = report.Data?.Invoices ?? new List<InvoiceDto>();

        var sb = new StringBuilder();
        // UTF-8 BOM is added when encoding to bytes
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

    public async Task<byte[]> ExportDentistPerformanceCsvAsync(
        long? departmentId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var report = await GetAllDentistsPerformanceAsync(departmentId, fromDate, toDate, ct);
        var list = report.Data ?? new List<DentistPerformanceDto>();

        var sb = new StringBuilder();
        sb.AppendLine("Mã Nhân Viên,Tên Bác Sĩ,Chuyên Khoa,Tổng Ca Khám,Số Ca Hoàn Thành,Tỷ Lệ Hoàn Thành (%),Đánh Giá Trung Bình (Sao),Số Lượt Đánh Giá,Doanh Thu Đóng Góp (VNĐ)");

        foreach (var d in list)
        {
            var empCode = EscapeCsv(d.EmployeeCode ?? "--");
            var name = EscapeCsv(d.DentistName);
            var dept = EscapeCsv(d.DepartmentName);
            var total = d.TotalAppointments;
            var comp = d.CompletedAppointments;
            var rate = d.CompletionRate.ToString("F1");
            var rating = d.AverageRating.ToString("F1");
            var count = d.TotalFeedbacks;
            var rev = d.TotalRevenueGenerated.ToString("F0");

            sb.AppendLine($"{empCode},{name},{dept},{total},{comp},{rate},{rating},{count},{rev}");
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
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
        var paidAmount = invoice.Payments.Where(p => p.Status == "Successful" || p.Status == "Paid" || p.Status == "Completed").Sum(p => p.Amount);
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
