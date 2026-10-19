using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Reports;

public class ReportServiceTests
{
    private readonly Mock<ILogger<ReportService>> _mockLogger = new();

    private DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedDataAsync(DentalClinicDbContext db)
    {
        // Department 1 managed by manager 501
        var dept1 = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Hàm Mặt",
            Description = "Khoa tổng hợp",
            ManagerUserId = 501,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        // Department 2 managed by manager 502
        var dept2 = new Department
        {
            DepartmentId = 2,
            Name = "Khoa Chỉnh Nha",
            Description = "Khoa niềng răng",
            ManagerUserId = 502,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Departments.AddRange(dept1, dept2);

        // Dentists
        var dentistUser1 = new UserAccount { UserId = 301, Email = "dentist1@clinic.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var staff1 = new StaffProfile { UserId = 301, EmployeeCode = "BS01", FullName = "Bác Sĩ Nguyễn Văn A", EmploymentStatus = "Active" };
        var dentistProfile1 = new DentistProfile { UserId = 301, LicenseNumber = "BS01-LIC", User = staff1 };

        var dentistUser2 = new UserAccount { UserId = 302, Email = "dentist2@clinic.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var staff2 = new StaffProfile { UserId = 302, EmployeeCode = "BS02", FullName = "Bác Sĩ Trần Thị B", EmploymentStatus = "Active" };
        var dentistProfile2 = new DentistProfile { UserId = 302, LicenseNumber = "BS02-LIC", User = staff2 };

        db.UserAccounts.AddRange(dentistUser1, dentistUser2);
        db.StaffProfiles.AddRange(staff1, staff2);
        db.DentistProfiles.AddRange(dentistProfile1, dentistProfile2);

        // Dentist Departments
        var dd1 = new DentistDepartment { DentistDepartmentId = 1, DentistUserId = 301, DepartmentId = 1, Status = "Active", AssignedAt = DateTime.UtcNow };
        var dd2 = new DentistDepartment { DentistDepartmentId = 2, DentistUserId = 302, DepartmentId = 1, Status = "Active", AssignedAt = DateTime.UtcNow };
        db.DentistDepartments.AddRange(dd1, dd2);

        // Patients
        var patientUser = new UserAccount { UserId = 101, Email = "patient@clinic.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var patientProfile = new PatientProfile { UserId = 101, PatientCode = "BN01", FullName = "Nguyễn Bệnh Nhân" };
        db.UserAccounts.Add(patientUser);
        db.PatientProfiles.Add(patientProfile);

        // Appointments (ScheduledStart is set)
        var appt1 = new Appointment
        {
            AppointmentId = 1,
            AppointmentCode = "APT-001",
            PatientUserId = 101,
            AssignedDentistUserId = 301,
            DepartmentId = 1,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(8),
            ScheduledEnd = DateTime.UtcNow.Date.AddHours(9),
            ReasonForVisit = "Khám răng định kỳ",
            BookingSource = "Online",
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-10) // Booked 10 days ago
        };
        var appt2 = new Appointment
        {
            AppointmentId = 2,
            AppointmentCode = "APT-002",
            PatientUserId = 101, // Same patient
            AssignedDentistUserId = 301,
            DepartmentId = 1,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(9),
            ScheduledEnd = DateTime.UtcNow.Date.AddHours(10),
            ReasonForVisit = "Trám răng sâu",
            BookingSource = "Receptionist",
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        var appt3 = new Appointment
        {
            AppointmentId = 3,
            AppointmentCode = "APT-003",
            PatientUserId = 101,
            AssignedDentistUserId = 301,
            DepartmentId = 1,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(14),
            ScheduledEnd = DateTime.UtcNow.Date.AddHours(15),
            ReasonForVisit = "Kiểm tra sau trám",
            BookingSource = "Online",
            Status = AppointmentStatus.Cancelled,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.AddRange(appt1, appt2, appt3);

        // Treatment Session
        var treatmentPlan = new TreatmentPlan
        {
            TreatmentPlanId = 1,
            PatientUserId = 101,
            DepartmentId = 1,
            CreatedByDentistUserId = 301,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var planItem = new TreatmentPlanItem
        {
            TreatmentPlanItemId = 1,
            TreatmentPlanId = 1,
            ToothNumber = "16",
            EstimatedCost = 500000,
            Status = "Completed"
        };
        var session = new TreatmentSession
        {
            TreatmentSessionId = 1,
            TreatmentPlanItemId = 1,
            AppointmentId = 2,
            DentistUserId = 301,
            SessionDate = DateTime.UtcNow.Date.AddHours(9).AddMinutes(30),
            TreatmentResult = "Đã trám thành công",
            TreatmentPlanItem = planItem,
            Appointment = appt2
        };
        db.TreatmentPlans.Add(treatmentPlan);
        db.TreatmentPlanItems.Add(planItem);
        db.TreatmentSessions.Add(session);

        // Feedback
        var feedback = new VisitFeedback
        {
            FeedbackId = 1,
            AppointmentId = 1,
            DentistRating = 5,
            ClinicRating = 5,
            Comment = "Bác sĩ rất tận tình và nhẹ nhàng",
            CreatedAt = DateTime.UtcNow
        };
        db.VisitFeedbacks.Add(feedback);

        // Invoices
        var invoice1 = new Invoice
        {
            InvoiceId = 1,
            InvoiceCode = "INV-20261013-0001",
            AppointmentId = 1,
            GeneratedByUserId = 301,
            TotalAmount = 900000,
            Status = "Paid",
            CreatedAt = DateTime.UtcNow.Date.AddHours(10),
            Appointment = appt1
        };
        var invoice2 = new Invoice
        {
            InvoiceId = 2,
            InvoiceCode = "INV-20261013-0002",
            AppointmentId = 2,
            GeneratedByUserId = 301,
            TotalAmount = 500000,
            Status = "PartiallyPaid",
            CreatedAt = DateTime.UtcNow.Date.AddHours(11),
            Appointment = appt2
        };
        // Cancelled invoice (should be excluded from TotalRevenue)
        var invoice3 = new Invoice
        {
            InvoiceId = 3,
            InvoiceCode = "INV-20261013-0003",
            AppointmentId = 3,
            GeneratedByUserId = 301,
            TotalAmount = 300000,
            Status = "Cancelled",
            CreatedAt = DateTime.UtcNow.Date.AddHours(14),
            Appointment = appt3
        };
        db.Invoices.AddRange(invoice1, invoice2, invoice3);

        // Payments
        var payment1 = new Payment
        {
            PaymentId = 1,
            InvoiceId = 1,
            PaidByPatientUserId = 101,
            Amount = 900000,
            Method = "BankTransfer",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow.Date.AddHours(10).AddMinutes(15)
        };
        var payment2 = new Payment
        {
            PaymentId = 2,
            InvoiceId = 2,
            PaidByPatientUserId = 101,
            Amount = 200000,
            Method = "Cash",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow.Date.AddHours(11).AddMinutes(15)
        };
        var payment3 = new Payment
        {
            PaymentId = 3,
            InvoiceId = 3,
            PaidByPatientUserId = 101,
            Amount = 100000,
            Method = "Cash",
            Status = "Failed", // Failed payment should NOT be counted
            CreatedAt = DateTime.UtcNow.Date.AddHours(14).AddMinutes(15)
        };
        db.Payments.AddRange(payment1, payment2, payment3);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetRevenueReportAsync_ShouldCalculateTotalsAndBreakdowns_ExcludingCancelledInvoices()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var service = new ReportService(db, _mockLogger.Object);

        // Act
        var result = await service.GetRevenueReportAsync(null, null, null);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var report = result.Data;
        // TotalRevenue excludes Cancelled invoice 3 (300,000): 900,000 + 500,000 = 1,400,000
        Assert.Equal(1400000, report.TotalRevenue);
        // TotalPaid excludes Failed payment 3 (100,000): 900,000 + 200,000 = 1,100,000
        Assert.Equal(1100000, report.TotalPaid);
        Assert.Equal(300000, report.TotalRemaining);

        Assert.Equal(3, report.TotalInvoices);
        Assert.Equal(1, report.PaidInvoices);
        Assert.Equal(1, report.PartiallyPaidInvoices);
        Assert.Equal(1, report.CancelledInvoices);

        Assert.NotEmpty(report.DailyTrend);
        Assert.NotEmpty(report.WeeklyTrend);
        Assert.NotEmpty(report.MonthlyTrend);
        Assert.NotEmpty(report.DepartmentDistribution);
    }

    [Fact]
    public async Task DepartmentManager_Authorization_AllowsOwnDepartment_AndBlocksOtherDepartment()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var service = new ReportService(db, _mockLogger.Object);

        // Act 1: Manager 501 views their own department 1 -> Success
        var resOwn = await service.GetDepartmentPerformanceReportAsync(1, null, null, callerUserId: 501, callerRole: UserRole.DepartmentManager);
        Assert.True(resOwn.Success);
        Assert.NotNull(resOwn.Data);
        Assert.Equal(1, resOwn.Data.DepartmentId);

        // Act 2: Manager 501 attempts to view department 2 -> Forbidden / Fail
        var resOther = await service.GetDepartmentPerformanceReportAsync(2, null, null, callerUserId: 501, callerRole: UserRole.DepartmentManager);
        Assert.False(resOther.Success);
        Assert.Contains("quyền", resOther.Message, StringComparison.OrdinalIgnoreCase);

        // Act 3: Manager 501 exports department 2 -> Throws or fails
        var resListOther = await service.GetAllDentistsPerformanceAsync(2, null, null, callerUserId: 501, callerRole: UserRole.DepartmentManager);
        Assert.False(resListOther.Success);

        // Act 4: Admin views department 2 -> Success
        var resAdmin = await service.GetDepartmentPerformanceReportAsync(2, null, null, callerUserId: 999, callerRole: UserRole.SystemAdministrator);
        Assert.True(resAdmin.Success);
    }

    [Fact]
    public async Task GetDepartmentPerformanceReportAsync_ShouldCalculateDoctorProductivity_DistinctPatients_AndSessions()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var service = new ReportService(db, _mockLogger.Object);

        // Act
        var result = await service.GetDepartmentPerformanceReportAsync(1, null, null);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var deptReport = result.Data;
        Assert.Equal(1, deptReport.DepartmentId);
        Assert.Equal("Khoa Răng Hàm Mặt", deptReport.DepartmentName);
        Assert.Equal(2, deptReport.TotalDentists);

        var dentist1 = deptReport.Dentists.FirstOrDefault(d => d.DentistUserId == 301);
        Assert.NotNull(dentist1);
        Assert.Equal(2, dentist1.CompletedAppointments);
        Assert.Equal(1, dentist1.CancelledAppointments);
        // Distinct patients: Patient 101 had 2 completed visits, distinct count is 1
        Assert.Equal(1, dentist1.DistinctPatientsTreated);
        // Treatment sessions: 1 completed session
        Assert.Equal(1, dentist1.CompletedTreatmentSessions);
        Assert.Equal(5.0, dentist1.AverageRating);
        Assert.Equal(1, dentist1.FiveStarCount);
        // Dentist revenue excludes Cancelled invoice 3: 900,000 + 500,000 = 1,400,000
        Assert.Equal(1400000, dentist1.TotalRevenueGenerated);
        // Paid revenue: 900,000 + 200,000 = 1,100,000
        Assert.Equal(1100000, dentist1.TotalPaidRevenue);
    }

    [Fact]
    public void EscapeCsv_MitigatesFormulaInjection_CWE1236()
    {
        // Dangerous formula payloads
        Assert.Equal("\"'=cmd|' /C calc'!A0\"", ReportService.EscapeCsv("=cmd|' /C calc'!A0"));
        Assert.Equal("\"'+84901234567\"", ReportService.EscapeCsv("+84901234567"));
        Assert.Equal("\"'-payload\"", ReportService.EscapeCsv("-payload"));
        Assert.Equal("\"'@SUM(A1:A10)\"", ReportService.EscapeCsv("@SUM(A1:A10)"));
        Assert.Equal("\"'\t=tabformula\"", ReportService.EscapeCsv("\t=tabformula"));

        // Valid numbers should not be mangled
        Assert.Equal("\"-1000\"", ReportService.EscapeCsv("-1000"));
        Assert.Equal("\"-123.45\"", ReportService.EscapeCsv("-123.45"));

        // Normal strings with Vietnamese characters
        Assert.Equal("\"Nguyễn Văn A\"", ReportService.EscapeCsv("Nguyễn Văn A"));
        Assert.Equal("\"Khoa Răng Hàm Mặt\"", ReportService.EscapeCsv("Khoa Răng Hàm Mặt"));
    }

    [Fact]
    public async Task ExportInvoicesCsvAsync_ShouldReturnUtf8BomEncodedBytes()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var service = new ReportService(db, _mockLogger.Object);

        // Act
        var data = await service.ExportInvoicesCsvAsync(null, null, null);

        // Assert
        Assert.NotNull(data);
        Assert.NotEmpty(data);

        // Check UTF-8 BOM
        var bom = Encoding.UTF8.GetPreamble();
        Assert.True(data.Length >= bom.Length);
        Assert.Equal(bom[0], data[0]);
        Assert.Equal(bom[1], data[1]);
        Assert.Equal(bom[2], data[2]);

        var text = Encoding.UTF8.GetString(data);
        Assert.Contains("INV-20261013-0001", text);
        Assert.Contains("Khoa Răng Hàm Mặt", text);
    }

    [Fact]
    public async Task ExportInvoicesXlsxAsync_ShouldReturnValidClosedXmlWorkbook()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var service = new ReportService(db, _mockLogger.Object);

        // Act
        var data = await service.ExportInvoicesXlsxAsync(null, null, null);

        // Assert
        Assert.NotNull(data);
        Assert.NotEmpty(data);

        using var ms = new MemoryStream(data);
        using var workbook = new XLWorkbook(ms);

        var ws = workbook.Worksheet("BaoCaoDoanhThu");
        Assert.NotNull(ws);
        Assert.Contains("BÁO CÁO PHÂN TÍCH DOANH THU", ws.Cell(1, 1).GetString());
        Assert.Equal("Mã Hóa Đơn", ws.Cell(5, 1).GetString());
        Assert.StartsWith("INV-20261013-", ws.Cell(6, 1).GetString());
    }

    [Fact]
    public async Task ExportDentistPerformanceXlsxAsync_ShouldReturnValidClosedXmlWorkbook()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var service = new ReportService(db, _mockLogger.Object);

        // Act
        var data = await service.ExportDentistPerformanceXlsxAsync(1, null, null);

        // Assert
        Assert.NotNull(data);
        Assert.NotEmpty(data);

        using var ms = new MemoryStream(data);
        using var workbook = new XLWorkbook(ms);

        var ws = workbook.Worksheet("NangSuatBacSi");
        Assert.NotNull(ws);
        Assert.Contains("BÁO CÁO NĂNG SUẤT", ws.Cell(1, 1).GetString());
        Assert.Equal("Mã NV", ws.Cell(4, 1).GetString());
        Assert.Equal("BS01", ws.Cell(5, 1).GetString());
    }

    [Fact]
    public async Task GetRevenueReportAsync_CrossPeriodReconciliation_MandatoryTask11Scenario()
    {
        // Arrange: Invoice A created 30/09/2026 for 2,000,000 VND
        // Payment 1: PaidAt 30/09/2026 for 500,000 VND (Successful)
        // Payment 2: PaidAt 05/10/2026 for 1,500,000 VND (Successful)
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);

        var dept = new Department { DepartmentId = 1, Name = "Khoa RHM", ManagerUserId = 501, Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Departments.Add(dept);

        var dentist = new UserAccount { UserId = 301, Email = "dentist@clinic.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var staff = new StaffProfile { UserId = 301, EmployeeCode = "BS01", FullName = "Bác Sĩ A", EmploymentStatus = "Active" };
        var dentistProf = new DentistProfile { UserId = 301, LicenseNumber = "LIC01", User = staff };
        db.UserAccounts.Add(dentist);
        db.StaffProfiles.Add(staff);
        db.DentistProfiles.Add(dentistProf);

        var patientUser = new UserAccount { UserId = 101, Email = "p@clinic.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var patientProf = new PatientProfile { UserId = 101, PatientCode = "BN01", FullName = "Bệnh Nhân X" };
        db.UserAccounts.Add(patientUser);
        db.PatientProfiles.Add(patientProf);

        var sepDateUtc = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);
        var octDateUtc = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

        var appt = new Appointment
        {
            AppointmentId = 10,
            AppointmentCode = "APT-SEP-001",
            PatientUserId = 101,
            AssignedDentistUserId = 301,
            DepartmentId = 1,
            ScheduledStart = sepDateUtc,
            ScheduledEnd = sepDateUtc.AddHours(1),
            BookingSource = "Online",
            ReasonForVisit = "Khám răng định kỳ",
            Status = AppointmentStatus.Completed,
            CreatedAt = sepDateUtc
        };
        db.Appointments.Add(appt);

        var invoiceA = new Invoice
        {
            InvoiceId = 100,
            InvoiceCode = "INV-20260930-0001",
            AppointmentId = 10,
            GeneratedByUserId = 301,
            TotalAmount = 2000000m,
            Status = "PartiallyPaid",
            CreatedAt = sepDateUtc,
            Appointment = appt
        };
        db.Invoices.Add(invoiceA);

        var payment1 = new Payment
        {
            PaymentId = 1001,
            InvoiceId = 100,
            PaidByPatientUserId = 101,
            Amount = 500000m,
            Method = "Cash",
            Status = "Successful",
            PaidAt = sepDateUtc,
            CreatedAt = sepDateUtc,
            Invoice = invoiceA
        };
        var payment2 = new Payment
        {
            PaymentId = 1002,
            InvoiceId = 100,
            PaidByPatientUserId = 101,
            Amount = 1500000m,
            Method = "BankTransfer",
            Status = "Successful",
            PaidAt = octDateUtc,
            CreatedAt = octDateUtc,
            Invoice = invoiceA
        };
        db.Payments.AddRange(payment1, payment2);
        await db.SaveChangesAsync();

        var service = new ReportService(db, _mockLogger.Object);

        // Act 1: Query September 2026 (01/09/2026 - 30/09/2026)
        var sepFrom = new DateTime(2026, 9, 1);
        var sepTo = new DateTime(2026, 9, 30);
        var sepReportResult = await service.GetRevenueReportAsync(sepFrom, sepTo, null);

        // Assert 1: September Report
        Assert.NotNull(sepReportResult.Data);
        var sepReport = sepReportResult.Data;
        Assert.Equal(2000000m, sepReport.TotalRevenue); // Invoiced in September
        Assert.Equal(500000m, sepReport.TotalPaid);     // Cash collected in September
        Assert.Equal(1500000m, sepReport.OutstandingBalanceAtPeriodEnd); // Remaining at end of September

        // Daily trend on 30/09/2026
        var sepDaily = sepReport.DailyTrend.FirstOrDefault(d => d.Date == "2026-09-30");
        Assert.NotNull(sepDaily);
        Assert.Equal(2000000m, sepDaily.Revenue);
        Assert.Equal(500000m, sepDaily.PaidAmount);

        // Monthly trend for 09/2026
        var sepMonthly = sepReport.MonthlyTrend.FirstOrDefault(m => m.Year == 2026 && m.Month == 9);
        Assert.NotNull(sepMonthly);
        Assert.Equal(2000000m, sepMonthly.Revenue);
        Assert.Equal(500000m, sepMonthly.PaidAmount);

        // Act 2: Query October 2026 (01/10/2026 - 31/10/2026)
        var octFrom = new DateTime(2026, 10, 1);
        var octTo = new DateTime(2026, 10, 31);
        var octReportResult = await service.GetRevenueReportAsync(octFrom, octTo, null);

        // Assert 2: October Report
        Assert.NotNull(octReportResult.Data);
        var octReport = octReportResult.Data;
        Assert.Equal(0m, octReport.TotalRevenue);        // No new invoices issued in October
        Assert.Equal(1500000m, octReport.TotalPaid);     // Cash collected in October from Payment 2

        // Daily trend on 05/10/2026
        var octDaily = octReport.DailyTrend.FirstOrDefault(d => d.Date == "2026-10-05");
        Assert.NotNull(octDaily);
        Assert.Equal(0m, octDaily.Revenue);
        Assert.Equal(1500000m, octDaily.PaidAmount);

        // Monthly trend for 10/2026
        var octMonthly = octReport.MonthlyTrend.FirstOrDefault(m => m.Year == 2026 && m.Month == 10);
        Assert.NotNull(octMonthly);
        Assert.Equal(0m, octMonthly.Revenue);
        Assert.Equal(1500000m, octMonthly.PaidAmount);

        // Previous Period comparison in October (Period length is 31 days, covering September)
        Assert.Equal(2000000m, octReport.PreviousPeriodRevenue);
        Assert.Equal(500000m, octReport.PreviousPeriodPaid);
    }

    [Fact]
    public async Task GetAllDentistsPerformanceAsync_TreatmentSessions_AccurateCompletionAndDentistIsolation()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);

        var dept = new Department { DepartmentId = 1, Name = "Khoa RHM", ManagerUserId = 501, Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Departments.Add(dept);

        var userDentist1 = new UserAccount { UserId = 301, Email = "d1@c.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var staff1 = new StaffProfile { UserId = 301, EmployeeCode = "BS01", FullName = "Bác Sĩ A", EmploymentStatus = "Active" };
        var prof1 = new DentistProfile { UserId = 301, LicenseNumber = "L1", User = staff1 };

        var userDentist2 = new UserAccount { UserId = 302, Email = "d2@c.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var staff2 = new StaffProfile { UserId = 302, EmployeeCode = "BS02", FullName = "Bác Sĩ B", EmploymentStatus = "Active" };
        var prof2 = new DentistProfile { UserId = 302, LicenseNumber = "L2", User = staff2 };

        db.UserAccounts.AddRange(userDentist1, userDentist2);
        db.StaffProfiles.AddRange(staff1, staff2);
        db.DentistProfiles.AddRange(prof1, prof2);

        var dd1 = new DentistDepartment { DentistDepartmentId = 1, DentistUserId = 301, DepartmentId = 1, Status = "Active", AssignedAt = DateTime.UtcNow };
        var dd2 = new DentistDepartment { DentistDepartmentId = 2, DentistUserId = 302, DepartmentId = 1, Status = "Active", AssignedAt = DateTime.UtcNow };
        db.DentistDepartments.AddRange(dd1, dd2);

        var patientUser = new UserAccount { UserId = 101, Email = "p@c.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var patientProf = new PatientProfile { UserId = 101, PatientCode = "BN01", FullName = "Bệnh Nhân" };
        db.UserAccounts.Add(patientUser);
        db.PatientProfiles.Add(patientProf);

        var plan = new TreatmentPlan { TreatmentPlanId = 1, PatientUserId = 101, DepartmentId = 1, CreatedByDentistUserId = 301, Status = "Active", CreatedAt = DateTime.UtcNow };
        var planItemCompleted = new TreatmentPlanItem { TreatmentPlanItemId = 1, TreatmentPlanId = 1, Status = "Completed" };
        var planItemPending = new TreatmentPlanItem { TreatmentPlanItemId = 2, TreatmentPlanId = 1, Status = "Pending" };
        db.TreatmentPlans.Add(plan);
        db.TreatmentPlanItems.AddRange(planItemCompleted, planItemPending);

        var appt1 = new Appointment
        {
            AppointmentId = 1,
            AppointmentCode = "APT-001",
            PatientUserId = 101,
            AssignedDentistUserId = 301,
            DepartmentId = 1,
            ScheduledStart = DateTime.UtcNow,
            BookingSource = "Online",
            ReasonForVisit = "Khám răng định kỳ",
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var appt2 = new Appointment
        {
            AppointmentId = 2,
            AppointmentCode = "APT-002",
            PatientUserId = 101,
            AssignedDentistUserId = 301,
            DepartmentId = 1,
            ScheduledStart = DateTime.UtcNow,
            BookingSource = "Online",
            ReasonForVisit = "Khám răng định kỳ",
            Status = AppointmentStatus.Cancelled,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.AddRange(appt1, appt2);

        // Session 1: Valid completed session for Dentist 1
        var sess1 = new TreatmentSession
        {
            TreatmentSessionId = 1,
            TreatmentPlanItemId = 1,
            AppointmentId = 1,
            DentistUserId = 301,
            SessionDate = DateTime.UtcNow,
            TreatmentResult = "Điều trị thành công",
            TreatmentPlanItem = planItemCompleted,
            Appointment = appt1
        };

        // Session 2: Incomplete result on pending item for Dentist 1 -> Should NOT be counted
        var sess2 = new TreatmentSession
        {
            TreatmentSessionId = 2,
            TreatmentPlanItemId = 2,
            AppointmentId = 1,
            DentistUserId = 301,
            SessionDate = DateTime.UtcNow,
            TreatmentResult = "Hủy do bệnh nhân chóng mặt",
            TreatmentPlanItem = planItemPending,
            Appointment = appt1
        };

        // Session 3: On a cancelled appointment -> Should NOT be counted
        var sess3 = new TreatmentSession
        {
            TreatmentSessionId = 3,
            TreatmentPlanItemId = 1,
            AppointmentId = 2,
            DentistUserId = 301,
            SessionDate = DateTime.UtcNow,
            TreatmentResult = "Điều trị xong",
            TreatmentPlanItem = planItemCompleted,
            Appointment = appt2
        };

        // Session 4: Valid completed session for Dentist 2 -> Belongs to Dentist 2, NOT Dentist 1
        var sess4 = new TreatmentSession
        {
            TreatmentSessionId = 4,
            TreatmentPlanItemId = 1,
            AppointmentId = 1,
            DentistUserId = 302,
            SessionDate = DateTime.UtcNow,
            TreatmentResult = "Thành công",
            TreatmentPlanItem = planItemCompleted,
            Appointment = appt1
        };

        db.TreatmentSessions.AddRange(sess1, sess2, sess3, sess4);
        await db.SaveChangesAsync();

        var service = new ReportService(db, _mockLogger.Object);

        // Act
        var res = await service.GetAllDentistsPerformanceAsync(1, null, null);

        // Assert
        Assert.NotNull(res.Data);
        var dentist1 = res.Data.First(d => d.DentistUserId == 301);
        var dentist2 = res.Data.First(d => d.DentistUserId == 302);

        // Dentist 1 has only Session 1 counted (Session 2 cancelled result, Session 3 cancelled appt, Session 4 belongs to Dentist 2)
        Assert.Equal(1, dentist1.CompletedTreatmentSessions);

        // Dentist 2 has exactly Session 4 counted
        Assert.Equal(1, dentist2.CompletedTreatmentSessions);
    }

    [Fact]
    public async Task ExportDentistPerformanceCsvAsync_CrossDepartment_ThrowsUnauthorizedAccessException()
    {
        // Arrange: Manager 501 manages Dept 1, tries to export Dept 2
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);
        var service = new ReportService(db, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await service.ExportDentistPerformanceCsvAsync(2, null, null, 501, UserRole.DepartmentManager);
        });
    }

    [Fact]
    public async Task ExportDentistPerformanceXlsxAsync_CrossDepartment_ThrowsUnauthorizedAccessException()
    {
        // Arrange: Manager 501 manages Dept 1, tries to export Dept 2
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);
        var service = new ReportService(db, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await service.ExportDentistPerformanceXlsxAsync(2, null, null, 501, UserRole.DepartmentManager);
        });
    }

    [Fact]
    public async Task ExportDentistPerformanceCsvAsync_AdminRole_SucceedsWithValidBytes()
    {
        // Arrange: Admin exports all departments
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);
        var service = new ReportService(db, _mockLogger.Object);

        // Act
        var bytes = await service.ExportDentistPerformanceCsvAsync(null, null, null, 1, UserRole.SystemAdministrator);

        // Assert
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 3);
        // Verify UTF-8 BOM
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    [Fact]
    public async Task GetDepartmentPerformanceReportAsync_DepartmentManager_DefaultsToManagedDepartmentWhenZero()
    {
        // Arrange: Manager 501 calls with departmentId = 0
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);
        var service = new ReportService(db, _mockLogger.Object);

        // Act
        var res = await service.GetDepartmentPerformanceReportAsync(0, null, null, 501, UserRole.DepartmentManager);

        // Assert
        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.Equal(1, res.Data.DepartmentId);
        Assert.Equal("Khoa Răng Hàm Mặt", res.Data.DepartmentName);
    }
}
