using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        // Department
        var dept1 = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Hàm Mặt",
            Description = "Khoa tổng hợp",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var dept2 = new Department
        {
            DepartmentId = 2,
            Name = "Khoa Chỉnh Nha",
            Description = "Khoa niềng răng",
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

        // Appointments
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
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        };
        var appt2 = new Appointment
        {
            AppointmentId = 2,
            AppointmentCode = "APT-002",
            PatientUserId = 101,
            AssignedDentistUserId = 301,
            DepartmentId = 1,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(9),
            ScheduledEnd = DateTime.UtcNow.Date.AddHours(10),
            ReasonForVisit = "Trám răng sâu",
            BookingSource = "Receptionist",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.AddRange(appt1, appt2);

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
            CreatedAt = DateTime.UtcNow.Date.AddHours(10)
        };
        var invoice2 = new Invoice
        {
            InvoiceId = 2,
            InvoiceCode = "INV-20261013-0002",
            AppointmentId = 2,
            GeneratedByUserId = 301,
            TotalAmount = 500000,
            Status = "PartiallyPaid",
            CreatedAt = DateTime.UtcNow.Date.AddHours(11)
        };
        db.Invoices.AddRange(invoice1, invoice2);

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
        db.Payments.AddRange(payment1, payment2);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetRevenueReportAsync_ShouldCalculateTotalsAndBreakdowns()
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
        Assert.Equal(1400000, report.TotalRevenue); // 900,000 + 500,000
        Assert.Equal(1100000, report.TotalPaid);    // 900,000 + 200,000
        Assert.Equal(300000, report.TotalRemaining); // 300,000
        Assert.Equal(2, report.TotalInvoices);
        Assert.Equal(1, report.PaidInvoices);
        Assert.Equal(1, report.PartiallyPaidInvoices);
        Assert.NotEmpty(report.DailyTrend);
        Assert.NotEmpty(report.DepartmentDistribution);
    }

    [Fact]
    public async Task GetDepartmentPerformanceReportAsync_ShouldCalculateDoctorProductivityAndRating()
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
        Assert.Equal(5.0, dentist1.AverageRating);
        Assert.Equal(1, dentist1.FiveStarCount);
        Assert.Equal(1400000, dentist1.TotalRevenueGenerated);
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
    public async Task ExportDentistPerformanceCsvAsync_ShouldReturnValidCsvWithDentistData()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var service = new ReportService(db, _mockLogger.Object);

        // Act
        var data = await service.ExportDentistPerformanceCsvAsync(1, null, null);

        // Assert
        Assert.NotNull(data);
        Assert.NotEmpty(data);

        var text = Encoding.UTF8.GetString(data);
        Assert.Contains("Bác Sĩ Nguyễn Văn A", text);
        Assert.Contains("Khoa Răng Hàm Mặt", text);
    }
}
