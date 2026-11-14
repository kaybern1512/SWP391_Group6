using System;
using System.Collections.Generic;
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
        var start = fromDate?.Date ?? end.Date.AddDays(-29);

        var query = _dbContext.Invoices
            .AsNoTracking()
            .AsSplitQuery()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .Include(i => i.GeneratedByUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.Patient)
                    .ThenInclude(p => p.User)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AppointmentServices)
                    .ThenInclude(s => s.AssignedDentist)
                        .ThenInclude(d => d!.StaffProfile)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AppointmentServices)
                    .ThenInclude(s => s.DepartmentService)
                        .ThenInclude(ds => ds.Department)
            .Where(i => i.CreatedAt >= start && i.CreatedAt <= end);

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            var deptIdInt = (int)departmentId.Value;
            query = query.Where(i => i.Appointment.AppointmentServices.Any(s => s.DepartmentId == deptIdInt));
        }

        var invoices = await query.OrderByDescending(i => i.CreatedAt).ThenBy(i => i.InvoiceId).ToListAsync(ct);

        var totalRevenue = invoices.Sum(i => i.FinalAmount);
        var totalIncoming = invoices.SelectMany(i => i.Payments).Where(p => p.Status == "Successful" && p.PaymentType != "Refund").Sum(p => p.Amount);
        var totalRefunded = invoices.SelectMany(i => i.Payments).Where(p => p.Status == "Successful" && p.PaymentType == "Refund").Sum(p => p.Amount);
        var totalPaid = Math.Max(0, totalIncoming - totalRefunded);
        var totalRemaining = Math.Max(0, totalRevenue - totalPaid);

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
                var invIncoming = inv.Payments.Where(p => p.Status == "Successful" && p.PaymentType != "Refund").Sum(p => p.Amount);
                var invRefunded = inv.Payments.Where(p => p.Status == "Successful" && p.PaymentType == "Refund").Sum(p => p.Amount);
                var invPaid = Math.Max(0, invIncoming - invRefunded);
                dailyMap[key] = (cur.revenue + inv.FinalAmount, cur.paid + invPaid, cur.count + 1);
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

        var allDepartments = await _dbContext.Departments.AsNoTracking().ToListAsync(ct);
        var deptDistribution = new List<DepartmentStatisticDto>();

        foreach (var dept in allDepartments)
        {
            var deptInvoices = invoices
                .Where(i => i.Appointment.AppointmentServices.Any(s => s.DepartmentId == dept.DepartmentId))
                .ToList();
            if (deptInvoices.Any() || departmentId == dept.DepartmentId)
            {
                var rev = deptInvoices.Sum(i => i.FinalAmount);
                var deptIncoming = deptInvoices.SelectMany(i => i.Payments).Where(p => p.Status == "Successful" && p.PaymentType != "Refund").Sum(p => p.Amount);
                var deptRefunded = deptInvoices.SelectMany(i => i.Payments).Where(p => p.Status == "Successful" && p.PaymentType == "Refund").Sum(p => p.Amount);
                var paid = Math.Max(0, deptIncoming - deptRefunded);
                var pct = totalRevenue > 0 ? Math.Round((double)(rev * 100 / totalRevenue), 1) : 0;
                var dentistCount = await _dbContext.DentistProfiles.CountAsync(d => d.DepartmentId == dept.DepartmentId, ct);
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

        var clinic = await _dbContext.ClinicProfiles.AsNoTracking().OrderBy(c => c.ClinicId).FirstOrDefaultAsync(ct);
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
            .FirstOrDefaultAsync(d => d.DepartmentId == (int)departmentId, ct);

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
            ManagerName = dentistList.FirstOrDefault()?.DentistName ?? "Quản trị phòng khám",
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

        var dentistQuery = _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.StaffProfile)
            .Include(d => d.Department)
            .AsQueryable();

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            var deptIdInt = (int)departmentId.Value;
            dentistQuery = dentistQuery.Where(d => d.DepartmentId == deptIdInt);
        }

        var dentists = await dentistQuery.ToListAsync(ct);

        var serviceQuery = _dbContext.AppointmentServices
            .AsNoTracking()
            .Include(s => s.Appointment)
                .ThenInclude(a => a.Patient)
            .Include(s => s.Appointment)
                .ThenInclude(a => a.Invoice)
            .Include(s => s.VisitFeedback)
            .Where(s => s.AssignedDentistUserId != null &&
                        s.CreatedAt >= start && s.CreatedAt <= end);

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            var deptIdInt = (int)departmentId.Value;
            serviceQuery = serviceQuery.Where(s => s.DepartmentId == deptIdInt);
        }

        var apptServices = await serviceQuery.ToListAsync(ct);

        var result = new List<DentistPerformanceDto>();

        foreach (var d in dentists)
        {
            var dServices = apptServices.Where(s => s.AssignedDentistUserId == d.UserId).ToList();

            var total = dServices.Count;
            var completed = dServices.Count(s => s.Status == "Completed");
            var compRate = total > 0 ? Math.Round((double)completed * 100 / total, 1) : 0;

            var feedbacks = dServices.Where(s => s.VisitFeedback != null).Select(s => s.VisitFeedback!).ToList();
            var totalFb = feedbacks.Count;
            var avgRating = totalFb > 0 ? Math.Round(feedbacks.Average(f => (double)f.DentistRating), 1) : 5.0;

            var fiveStar = feedbacks.Count(f => f.DentistRating == 5);
            var fourStar = feedbacks.Count(f => f.DentistRating == 4);
            var threeStar = feedbacks.Count(f => f.DentistRating == 3);
            var twoStar = feedbacks.Count(f => f.DentistRating == 2);
            var oneStar = feedbacks.Count(f => f.DentistRating == 1);

            var rev = dServices.Where(s => s.Status == "Completed").Sum(s => s.UnitPrice);

            var recentFb = dServices
                .Where(s => s.VisitFeedback != null)
                .OrderByDescending(s => s.VisitFeedback!.CreatedAt)
                .Take(5)
                .Select(s => new FeedbackItemSummaryDto
                {
                    FeedbackId = s.VisitFeedback!.FeedbackId,
                    PatientName = s.Appointment?.Patient?.FullName ?? "Bệnh nhân",
                    DentistRating = (byte)s.VisitFeedback.DentistRating,
                    ClinicRating = (byte)s.VisitFeedback.ClinicRating,
                    Comment = s.VisitFeedback.Comments,
                    CreatedAt = s.VisitFeedback.CreatedAt
                })
                .ToList();

            result.Add(new DentistPerformanceDto
            {
                DentistUserId = d.UserId,
                DentistName = d.StaffProfile?.FullName ?? "Bác sĩ",
                EmployeeCode = $"BS-{d.UserId:D4}",
                DepartmentId = d.DepartmentId,
                DepartmentName = d.Department?.Name ?? "--",
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
        var incoming = invoice.Payments.Where(p => p.Status == "Successful" && p.PaymentType != "Refund").Sum(p => p.Amount);
        var refunded = invoice.Payments.Where(p => p.Status == "Successful" && p.PaymentType == "Refund").Sum(p => p.Amount);
        var paidAmount = Math.Max(0, incoming - refunded);
        var remainingAmount = Math.Max(0, invoice.FinalAmount - paidAmount);
        var primaryService = invoice.Appointment?.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();

        return new InvoiceDto
        {
            InvoiceId = invoice.InvoiceId,
            InvoiceCode = invoice.InvoiceCode,
            AppointmentId = invoice.AppointmentId,
            AppointmentCode = $"APT-{invoice.AppointmentId:D6}",
            PatientUserId = invoice.Appointment?.PatientUserId ?? 0,
            PatientCode = invoice.Appointment != null ? $"PT-{invoice.Appointment.PatientUserId:D6}" : null,
            PatientName = invoice.Appointment?.Patient?.FullName ?? "Bệnh nhân",
            PatientPhoneNumber = invoice.Appointment?.Patient?.User?.PhoneNumber ?? invoice.Appointment?.Patient?.EmergencyContact,
            PatientEmail = invoice.Appointment?.Patient?.User?.Email,
            PatientAddress = invoice.Appointment?.Patient?.Address,
            DentistName = primaryService?.AssignedDentist?.StaffProfile?.FullName ?? "Chưa phân bổ",
            DepartmentName = primaryService?.DepartmentService?.Department?.Name ?? "Nha khoa",
            GeneratedByUserId = invoice.GeneratedByUserId,
            GeneratedByName = invoice.GeneratedByUser?.StaffProfile?.FullName ?? "Nhân viên thu ngân",
            SubTotal = invoice.TotalAmount,
            DiscountAmount = invoice.DiscountAmount,
            TotalAmount = invoice.FinalAmount,
            PaidAmount = paidAmount,
            RemainingAmount = remainingAmount,
            Status = invoice.Status,
            CreatedAt = invoice.CreatedAt,
            AppointmentDate = primaryService?.ScheduledStart,
            ReasonForVisit = invoice.Appointment?.ReasonForVisit,

            ClinicName = clinic?.ClinicName ?? "NHA KHOA ĐA CHUYÊN KHOA DENTALCARE",
            ClinicAddress = clinic?.Address ?? "123 Đường Nguyễn Tri Phương, Phường 5, Quận 10, TP. Hồ Chí Minh",
            ClinicPhone = clinic?.Hotline ?? "028 3822 9999",
            ClinicEmail = clinic?.Email ?? "contact@dentalcare.vn"
        };
    }
}
