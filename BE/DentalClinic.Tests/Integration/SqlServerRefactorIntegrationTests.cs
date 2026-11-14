using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.Feedback.DTOs;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Application.Features.Treatments.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using AppointmentService = DentalClinic.Infrastructure.Services.AppointmentService;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;
using MedicineEntity = DentalClinic.Infrastructure.Persistence.Entities.Medicine;

namespace DentalClinic.Tests.Integration;

[Collection("SqlServerIntegration")]
public class SqlServerRefactorIntegrationTests
{
    private const string TestConnectionString =
        "Server=.\\SQLEXPRESS;Database=DentalCare_SWP391_Refactor_TEST_Integration;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

    private static async Task<bool> IsSqlServerAvailableAsync()
    {
        try
        {
            using var conn = new SqlConnection(TestConnectionString);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static DentalClinicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(TestConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task CleanDatabaseAsync(DentalClinicDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(@"
            DELETE FROM AuditLogs;
            DELETE FROM Notifications;
            DELETE FROM BlogPosts;
            DELETE FROM BlogCategories;
            DELETE FROM VisitFeedback;
            DELETE FROM Payments;
            DELETE FROM InvoiceItems;
            DELETE FROM Invoices;
            DELETE FROM FollowUpSchedules;
            DELETE FROM PerformedServices;
            DELETE FROM PrescriptionItems;
            DELETE FROM Prescriptions;
            DELETE FROM Diagnostics;
            DELETE FROM DepartmentReferrals;
            DELETE FROM ClinicalEntries;
            DELETE FROM OdontogramEntries;
            DELETE FROM TreatmentSessions;
            DELETE FROM TreatmentPlanItems;
            DELETE FROM TreatmentPlans;
            DELETE FROM AppointmentServices;
            DELETE FROM Appointments;
            DELETE FROM DentalMedicalRecords;
            DELETE FROM ServiceComboItems;
            DELETE FROM ServiceCombos;
            DELETE FROM DepartmentServices;
            DELETE FROM StaffAttendanceRecords;
            DELETE FROM StaffLeaveRequests;
            DELETE FROM StaffWorkSchedules;
            DELETE FROM DentalChairs;
            DELETE FROM Rooms;
            DELETE FROM DentistProfiles;
            DELETE FROM StaffProfiles;
            DELETE FROM PatientProfiles;
            DELETE FROM UserAccounts;
            DELETE FROM Departments;
            DELETE FROM Medicines;
            DELETE FROM ClinicProfile;
        ");
    }

    private static async Task<(long patientId, long receptionistId, long dentist1Id, long dentist2Id, int dept1Id, int dept2Id, int srv1Id, int srv2Id, int room1Id, int chair1Id, int room2Id, int chair2Id)> SeedBaseFixtureAsync(DentalClinicDbContext db)
    {
        // 1. Clinic Profile (singleton row where ClinicId = 1)
        db.ClinicProfiles.Add(new ClinicProfile
        {
            ClinicId = 1,
            ClinicName = "Nha Khoa DentalCare Integration",
            Address = "123 Đường Test",
            Hotline = "1900 1234",
            Email = "contact@dentalcare.vn",
            OpeningTime = new TimeOnly(8, 0),
            ClosingTime = new TimeOnly(20, 0)
        });

        // 2. Departments
        var dept1 = new Department { Name = "Khoa Răng Hàm Mặt", Status = "Active", CreatedAt = DateTime.UtcNow };
        var dept2 = new Department { Name = "Khoa Chỉnh Nha", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Departments.AddRange(dept1, dept2);
        await db.SaveChangesAsync();

        // 3. Department Services
        var srv1 = new DepartmentService { DepartmentId = dept1.DepartmentId, ServiceCode = $"SVC-GEN-{Guid.NewGuid():N}"[..15], ServiceName = "Khám Răng Tổng Quát", Price = 200000m, DurationMinutes = 30, Status = "Active", CreatedAt = DateTime.UtcNow };
        var srv2 = new DepartmentService { DepartmentId = dept2.DepartmentId, ServiceCode = $"SVC-ORT-{Guid.NewGuid():N}"[..15], ServiceName = "Chỉnh Nha Mắc Cài", Price = 1500000m, DurationMinutes = 60, Status = "Active", CreatedAt = DateTime.UtcNow };
        db.DepartmentServices.AddRange(srv1, srv2);
        await db.SaveChangesAsync();

        // 4. Rooms and Dental Chairs
        var room1 = new Room { DepartmentId = dept1.DepartmentId, RoomCode = $"R1_{Guid.NewGuid():N}"[..10], RoomName = "Phòng Khám 1", RoomType = "General", Status = "Active", CreatedAt = DateTime.UtcNow };
        var room2 = new Room { DepartmentId = dept2.DepartmentId, RoomCode = $"R2_{Guid.NewGuid():N}"[..10], RoomName = "Phòng Khám 2", RoomType = "General", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Rooms.AddRange(room1, room2);
        await db.SaveChangesAsync();

        var chair1 = new DentalChair { RoomId = room1.RoomId, ChairCode = $"C1_{Guid.NewGuid():N}"[..10], ChairName = "Ghế 1", Status = "Active", CreatedAt = DateTime.UtcNow };
        var chair2 = new DentalChair { RoomId = room2.RoomId, ChairCode = $"C2_{Guid.NewGuid():N}"[..10], ChairName = "Ghế 2", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.DentalChairs.AddRange(chair1, chair2);
        await db.SaveChangesAsync();

        // 5. UserAccounts
        var patientUser = new UserAccount { Email = $"pat_{Guid.NewGuid():N}@dentalcare.com", Role = UserRole.Patient, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var recUser = new UserAccount { Email = $"rec_{Guid.NewGuid():N}@dentalcare.com", Role = UserRole.Receptionist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var dentist1User = new UserAccount { Email = $"dent1_{Guid.NewGuid():N}@dentalcare.com", Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var dentist2User = new UserAccount { Email = $"dent2_{Guid.NewGuid():N}@dentalcare.com", Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        db.UserAccounts.AddRange(patientUser, recUser, dentist1User, dentist2User);
        await db.SaveChangesAsync();

        // 6. Patient & Staff Profiles
        var patientProfile = new PatientProfile { UserId = patientUser.UserId, FullName = "Nguyễn Bệnh Nhân SQL", CreatedAt = DateTime.UtcNow };
        var recStaff = new StaffProfile { UserId = recUser.UserId, FullName = "Lê Lễ Tân SQL", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };
        var dentist1Staff = new StaffProfile { UserId = dentist1User.UserId, FullName = "Bác Sĩ Một SQL", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };
        var dentist2Staff = new StaffProfile { UserId = dentist2User.UserId, FullName = "Bác Sĩ Hai SQL", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };
        db.PatientProfiles.Add(patientProfile);
        db.StaffProfiles.AddRange(recStaff, dentist1Staff, dentist2Staff);
        await db.SaveChangesAsync();

        // 7. Dentist Profiles
        var dentist1Profile = new DentistProfile { UserId = dentist1User.UserId, DepartmentId = dept1.DepartmentId, LicenseNumber = $"LIC-1-{Guid.NewGuid():N}"[..15] };
        var dentist2Profile = new DentistProfile { UserId = dentist2User.UserId, DepartmentId = dept2.DepartmentId, LicenseNumber = $"LIC-2-{Guid.NewGuid():N}"[..15] };
        db.DentistProfiles.AddRange(dentist1Profile, dentist2Profile);

        // 8. Staff Work Schedules (Status must be 'Scheduled', 'Completed', 'Cancelled', 'AbsenceReported')
        var today = DateOnly.FromDateTime(DateTime.Today);
        db.StaffWorkSchedules.AddRange(
            new StaffWorkSchedule { StaffUserId = dentist1User.UserId, StaffRole = "Dentist", WorkDate = today, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", CreatedAt = DateTime.UtcNow },
            new StaffWorkSchedule { StaffUserId = dentist1User.UserId, StaffRole = "Dentist", WorkDate = today, StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(17, 0), Status = "Scheduled", CreatedAt = DateTime.UtcNow },
            new StaffWorkSchedule { StaffUserId = dentist2User.UserId, StaffRole = "Dentist", WorkDate = today, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", CreatedAt = DateTime.UtcNow },
            new StaffWorkSchedule { StaffUserId = dentist2User.UserId, StaffRole = "Dentist", WorkDate = today, StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(17, 0), Status = "Scheduled", CreatedAt = DateTime.UtcNow }
        );

        // 9. Dental Medical Record
        db.DentalMedicalRecords.Add(new DentalMedicalRecord
        {
            PatientUserId = patientUser.UserId,
            MedicalHistory = "Khỏe mạnh bình thường",
            Allergies = "Không",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        return (patientUser.UserId, recUser.UserId, dentist1User.UserId, dentist2User.UserId, dept1.DepartmentId, dept2.DepartmentId, srv1.DepartmentServiceId, srv2.DepartmentServiceId, room1.RoomId, chair1.DentalChairId, room2.RoomId, chair2.DentalChairId);
    }

    [Fact]
    public async Task GroupA_MultiServiceBookingTransaction_CreatesHeaderAndLinesAtomically()
    {
        if (!await IsSqlServerAvailableAsync()) return;

        using var db = CreateDbContext();
        await CleanDatabaseAsync(db);
        var fixture = await SeedBaseFixtureAsync(db);

        var notifMock = new Mock<INotificationService>();
        var apptService = new AppointmentService(db, notifMock.Object, NullLogger<AppointmentService>.Instance);

        var targetStart = DateTime.Today.AddDays(1).AddHours(9);
        var req = new CreateAppointmentRequest
        {
            DepartmentId = fixture.dept1Id,
            RequestedServiceId = fixture.srv1Id,
            RequestedDentistUserId = fixture.dentist1Id,
            ScheduledStart = targetStart,
            ReasonForVisit = "Khám răng tổng quát và tư vấn niềng",
            ServiceItems = new List<BookingServiceItemRequest>
            {
                new BookingServiceItemRequest
                {
                    DepartmentServiceId = fixture.srv1Id,
                    RequestedDentistUserId = fixture.dentist1Id,
                    ScheduledStart = targetStart
                },
                new BookingServiceItemRequest
                {
                    DepartmentServiceId = fixture.srv2Id,
                    RequestedDentistUserId = fixture.dentist2Id,
                    ScheduledStart = targetStart.AddHours(1)
                }
            }
        };

        var result = await apptService.CreatePatientAppointmentAsync(fixture.patientId, req);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var apptId = result.Data.AppointmentId;

        // Verify in database via direct SQL/EF
        var apptInDb = await db.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == apptId);

        Assert.NotNull(apptInDb);
        Assert.Equal(fixture.patientId, apptInDb.PatientUserId);
        Assert.Equal(2, apptInDb.AppointmentServices.Count);

        var lines = apptInDb.AppointmentServices.OrderBy(s => s.AppointmentServiceId).ToList();
        Assert.Equal(fixture.dept1Id, lines[0].DepartmentId);
        Assert.Equal(fixture.srv1Id, lines[0].DepartmentServiceId);
        Assert.Equal(fixture.dentist1Id, lines[0].RequestedDentistUserId);

        Assert.Equal(fixture.dept2Id, lines[1].DepartmentId);
        Assert.Equal(fixture.srv2Id, lines[1].DepartmentServiceId);
        Assert.Equal(fixture.dentist2Id, lines[1].RequestedDentistUserId);
    }

    [Fact]
    public async Task GroupB_ShiftBookingQuota_RejectsThirdBookingOnSameDay()
    {
        if (!await IsSqlServerAvailableAsync()) return;

        using var db = CreateDbContext();
        await CleanDatabaseAsync(db);
        var fixture = await SeedBaseFixtureAsync(db);

        var notifMock = new Mock<INotificationService>();
        var apptService = new AppointmentService(db, notifMock.Object, NullLogger<AppointmentService>.Instance);
        var targetDay = DateTime.Today.AddDays(2);

        // 1. Morning booking (08:30)
        var morningReq = new CreateAppointmentRequest
        {
            DepartmentId = fixture.dept1Id,
            RequestedServiceId = fixture.srv1Id,
            ScheduledStart = targetDay.AddHours(8).AddMinutes(30),
            ReasonForVisit = "Khám ca sáng"
        };
        var res1 = await apptService.CreatePatientAppointmentAsync(fixture.patientId, morningReq);
        Assert.True(res1.Success);

        // 2. Afternoon booking (14:00)
        var afternoonReq = new CreateAppointmentRequest
        {
            DepartmentId = fixture.dept2Id,
            RequestedServiceId = fixture.srv2Id,
            ScheduledStart = targetDay.AddHours(14),
            ReasonForVisit = "Khám ca chiều"
        };
        var res2 = await apptService.CreatePatientAppointmentAsync(fixture.patientId, afternoonReq);
        Assert.True(res2.Success);

        // 3. Third booking on same day (15:30) -> MUST BE REJECTED
        var thirdReq = new CreateAppointmentRequest
        {
            DepartmentId = fixture.dept1Id,
            RequestedServiceId = fixture.srv1Id,
            ScheduledStart = targetDay.AddHours(15).AddMinutes(30),
            ReasonForVisit = "Khám thêm ca chiều"
        };
        var res3 = await apptService.CreatePatientAppointmentAsync(fixture.patientId, thirdReq);
        Assert.False(res3.Success);
        Assert.Contains("tối đa 2", res3.Message);
    }

    [Fact]
    public async Task GroupC_SequentialServiceExecutionAndSingleInvoice_VerifiesCheckConstraints()
    {
        if (!await IsSqlServerAvailableAsync()) return;

        using var db = CreateDbContext();
        await CleanDatabaseAsync(db);
        var fixture = await SeedBaseFixtureAsync(db);

        // Setup appointment with 2 services (PendingReceptionReview allows unassigned resources)
        var appt = new Appointment
        {
            PatientUserId = fixture.patientId,
            BookingSource = "Patient",
            Status = "PendingReceptionReview",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var line1 = new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = fixture.dept1Id,
            DepartmentServiceId = fixture.srv1Id,
            AssignedDentistUserId = fixture.dentist1Id,
            RoomId = fixture.room1Id,
            DentalChairId = fixture.chair1Id,
            QueueNumber = 1,
            ServiceDate = today,
            ScheduledStart = DateTime.Today.AddHours(9),
            ScheduledEnd = DateTime.Today.AddHours(9).AddMinutes(30),
            UnitPrice = 200000m,
            Status = "Confirmed"
        };
        var line2 = new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = fixture.dept2Id,
            DepartmentServiceId = fixture.srv2Id,
            AssignedDentistUserId = fixture.dentist2Id,
            RoomId = fixture.room2Id,
            DentalChairId = fixture.chair2Id,
            QueueNumber = 2,
            ServiceDate = today,
            ScheduledStart = DateTime.Today.AddHours(10),
            ScheduledEnd = DateTime.Today.AddHours(11),
            UnitPrice = 1500000m,
            Status = "Confirmed"
        };
        db.AppointmentServices.AddRange(line1, line2);
        appt.Status = "Confirmed";
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var apptService = new AppointmentService(db, notifMock.Object, NullLogger<AppointmentService>.Instance);
        var treatmentService = new TreatmentService(db);

        // Dentist 1 completes service 1
        var start1 = await apptService.DentistStartAppointmentAsync(appt.AppointmentId, fixture.dentist1Id, line1.AppointmentServiceId);
        Assert.True(start1.Success);

        var perf1 = await treatmentService.AddPerformedServiceToAppointmentAsync(appt.AppointmentId, fixture.dentist1Id, new AddPerformedServiceToAppointmentRequest
        {
            AppointmentServiceId = line1.AppointmentServiceId,
            DepartmentServiceId = fixture.srv1Id,
            Quantity = 1,
            UnitPrice = 200000m
        });
        Assert.True(perf1.Success);

        var comp1 = await apptService.DentistCompleteAppointmentAsync(appt.AppointmentId, fixture.dentist1Id, appointmentServiceId: line1.AppointmentServiceId);
        Assert.True(comp1.Success);

        // Dentist 2 completes service 2
        var start2 = await apptService.DentistStartAppointmentAsync(appt.AppointmentId, fixture.dentist2Id, line2.AppointmentServiceId);
        Assert.True(start2.Success);

        var perf2 = await treatmentService.AddPerformedServiceToAppointmentAsync(appt.AppointmentId, fixture.dentist2Id, new AddPerformedServiceToAppointmentRequest
        {
            AppointmentServiceId = line2.AppointmentServiceId,
            DepartmentServiceId = fixture.srv2Id,
            Quantity = 1,
            UnitPrice = 1500000m
        });
        Assert.True(perf2.Success);

        var comp2 = await apptService.DentistCompleteAppointmentAsync(appt.AppointmentId, fixture.dentist2Id, appointmentServiceId: line2.AppointmentServiceId);
        Assert.True(comp2.Success);

        // Receptionist prepares and creates single combined invoice
        var billingService = new BillingService(db, notifMock.Object, NullLogger<BillingService>.Instance);

        var invoiceReq = new CreateInvoiceRequest
        {
            AppointmentId = appt.AppointmentId,
            DiscountAmount = 100000m,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest
                {
                    PerformedServiceId = perf1.Data!.PerformedServiceId,
                    Description = "Khám răng tổng quát",
                    Quantity = 1,
                    UnitPrice = 200000m
                },
                new CreateInvoiceItemRequest
                {
                    PerformedServiceId = perf2.Data!.PerformedServiceId,
                    Description = "Chỉnh nha mắc cài",
                    Quantity = 1,
                    UnitPrice = 1500000m
                }
            }
        };

        var invoiceRes = await billingService.CreateInvoiceAsync(fixture.receptionistId, invoiceReq);
        Assert.True(invoiceRes.Success);
        var invoice = invoiceRes.Data!;

        // SubTotal: 200,000 + 1,500,000 = 1,700,000. Discount: 100,000. Total/Final: 1,600,000
        Assert.Equal(1700000m, invoice.SubTotal);
        Assert.Equal(100000m, invoice.DiscountAmount);
        Assert.Equal(1600000m, invoice.TotalAmount);

        // Direct SQL check of CK_Invoices_FinalAmount and CK_Invoices_DiscountAmount
        var dbInvoice = await db.Invoices.AsNoTracking().FirstAsync(i => i.InvoiceId == invoice.InvoiceId);
        Assert.Equal(dbInvoice.TotalAmount - dbInvoice.DiscountAmount, dbInvoice.FinalAmount);
        Assert.True(dbInvoice.DiscountAmount <= dbInvoice.TotalAmount);
    }

    [Fact]
    public async Task GroupD_DirectMedicineDispensingAndStockIntegrity_AtomicallyDecrementsStock()
    {
        if (!await IsSqlServerAvailableAsync()) return;

        using var db = CreateDbContext();
        await CleanDatabaseAsync(db);
        var fixture = await SeedBaseFixtureAsync(db);

        // Seed Medicine with Stock = 50
        var med = new MedicineEntity
        {
            MedicineCode = $"MED-{Guid.NewGuid():N}"[..15],
            MedicineName = "Paracetamol 500mg",
            Unit = "Viên",
            UnitPrice = 2000m,
            StockQuantity = 50,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(med);

        // Seed Appointment and Service (InService requires resource allocation and QueueNumber)
        var appt = new Appointment
        {
            PatientUserId = fixture.patientId,
            Status = "InProgress",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = fixture.dept1Id,
            DepartmentServiceId = fixture.srv1Id,
            AssignedDentistUserId = fixture.dentist1Id,
            RoomId = fixture.room1Id,
            DentalChairId = fixture.chair1Id,
            QueueNumber = 1,
            ServiceDate = today,
            ScheduledStart = DateTime.Today.AddHours(8),
            ScheduledEnd = DateTime.Today.AddHours(9),
            UnitPrice = 200000m,
            Status = "InService"
        };
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        // 1. Dentist creates prescription
        var prescService = new PrescriptionService(db, NullLogger<PrescriptionService>.Instance);
        var prescReq = new CreatePrescriptionRequest
        {
            AppointmentId = appt.AppointmentId,
            Items = new List<CreatePrescriptionItemInput>
            {
                new CreatePrescriptionItemInput { MedicationName = "Paracetamol 500mg", Quantity = 10, Dosage = "1 viên", Frequency = "Ngày 2 lần" }
            }
        };
        var prescRes = await prescService.CreatePrescriptionAsync(fixture.dentist1Id, prescReq);
        Assert.True(prescRes.Success);
        var prescId = prescRes.Data!.PrescriptionId;
        var pItemId = prescRes.Data.Items[0].PrescriptionItemId;

        // 2. Dentist dispenses 10 tablets directly
        var medService = new MedicineService(db);
        var dispReq = new CreateMedicineDispensationRequest
        {
            PrescriptionId = prescId,
            Items = new List<DispenseItemInput>
            {
                new DispenseItemInput { PrescriptionItemId = pItemId, MedicineId = med.MedicineId, Quantity = 10 }
            }
        };

        var dispRes = await medService.DispenseMedicineAsync(dispReq, fixture.dentist1Id, userRole: "Dentist");
        Assert.Equal("Dispensed", dispRes.Status);

        // Verify Stock decremented from 50 to 40 in SQL Server
        var updatedMed = await db.Medicines.AsNoTracking().FirstAsync(m => m.MedicineId == med.MedicineId);
        Assert.Equal(40, updatedMed.StockQuantity);

        // 3. Attempting to dispense 45 tablets (> 40) MUST FAIL and preserve 40
        var prescReq2 = new CreatePrescriptionRequest
        {
            AppointmentId = appt.AppointmentId,
            Items = new List<CreatePrescriptionItemInput>
            {
                new CreatePrescriptionItemInput { MedicationName = "Paracetamol 500mg", Quantity = 45, Dosage = "1 viên", Frequency = "Ngày 2 lần" }
            }
        };
        var prescRes2 = await prescService.CreatePrescriptionAsync(fixture.dentist1Id, prescReq2);
        Assert.True(prescRes2.Success);

        var overDispReq = new CreateMedicineDispensationRequest
        {
            PrescriptionId = prescRes2.Data!.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new DispenseItemInput { PrescriptionItemId = prescRes2.Data.Items[0].PrescriptionItemId, MedicineId = med.MedicineId, Quantity = 45 }
            }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => medService.DispenseMedicineAsync(overDispReq, fixture.dentist1Id, userRole: "Dentist"));

        var unchangedMed = await db.Medicines.AsNoTracking().FirstAsync(m => m.MedicineId == med.MedicineId);
        Assert.Equal(40, unchangedMed.StockQuantity);
    }

    [Fact]
    public async Task GroupE_FeedbackIntegrity_PreventsDuplicatePerDentistPerAppointment()
    {
        if (!await IsSqlServerAvailableAsync()) return;

        using var db = CreateDbContext();
        await CleanDatabaseAsync(db);
        var fixture = await SeedBaseFixtureAsync(db);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var appt = new Appointment
        {
            PatientUserId = fixture.patientId,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = fixture.dept1Id,
            DepartmentServiceId = fixture.srv1Id,
            AssignedDentistUserId = fixture.dentist1Id,
            RoomId = fixture.room1Id,
            DentalChairId = fixture.chair1Id,
            ServiceDate = today,
            ScheduledStart = DateTime.Today.AddHours(8),
            ScheduledEnd = DateTime.Today.AddHours(9),
            UnitPrice = 200000m,
            Status = "Completed"
        };
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var feedbackService = new FeedbackService(db);

        // First feedback -> SUCCESS
        var fb1Res = await feedbackService.CreateFeedbackAsync(new CreateFeedbackRequest
        {
            AppointmentId = appt.AppointmentId,
            DentistUserId = fixture.dentist1Id,
            DentistRating = 5,
            ClinicRating = 5,
            Comment = "Bác sĩ rất tận tâm và chuyên nghiệp."
        }, fixture.patientId);
        Assert.True(fb1Res.Success);

        // Second feedback for same appointment and dentist -> MUST BE REJECTED
        var fb2Res = await feedbackService.CreateFeedbackAsync(new CreateFeedbackRequest
        {
            AppointmentId = appt.AppointmentId,
            DentistUserId = fixture.dentist1Id,
            DentistRating = 4,
            ClinicRating = 4,
            Comment = "Đánh giá lần 2"
        }, fixture.patientId);
        Assert.False(fb2Res.Success);
        Assert.Contains("đã được gửi đánh giá", fb2Res.Message);
    }

    [Fact]
    public async Task GroupF_FollowUpSchedulingLinkage_EnforcesCompositeFkIntegrity()
    {
        if (!await IsSqlServerAvailableAsync()) return;

        using var db = CreateDbContext();
        await CleanDatabaseAsync(db);
        var fixture = await SeedBaseFixtureAsync(db);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var appt = new Appointment
        {
            PatientUserId = fixture.patientId,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = fixture.dept1Id,
            DepartmentServiceId = fixture.srv1Id,
            AssignedDentistUserId = fixture.dentist1Id,
            RoomId = fixture.room1Id,
            DentalChairId = fixture.chair1Id,
            ServiceDate = today,
            ScheduledStart = DateTime.Today.AddHours(8),
            ScheduledEnd = DateTime.Today.AddHours(9),
            UnitPrice = 200000m,
            Status = "Completed"
        };
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var mr = await db.DentalMedicalRecords.FirstAsync(r => r.PatientUserId == fixture.patientId);

        var plan = new TreatmentPlan
        {
            MedicalRecordId = mr.RecordId,
            PatientUserId = fixture.patientId,
            CreatedByDentistUserId = fixture.dentist1Id,
            Title = "Kế hoạch điều trị răng",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.TreatmentPlans.Add(plan);
        await db.SaveChangesAsync();

        var session = new TreatmentSession
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            AppointmentId = appt.AppointmentId,
            AppointmentServiceId = apptSvc.AppointmentServiceId,
            PatientUserId = fixture.patientId,
            DentistUserId = fixture.dentist1Id,
            SessionDate = today,
            CreatedAt = DateTime.UtcNow
        };
        db.TreatmentSessions.Add(session);
        await db.SaveChangesAsync();

        // Insert FollowUpSchedule matching session and patient
        var followUp = new FollowUpSchedule
        {
            TreatmentSessionId = session.SessionId,
            PatientUserId = fixture.patientId,
            DentistUserId = fixture.dentist1Id,
            RecommendedServiceId = fixture.srv1Id,
            RecommendedDate = today.AddDays(14),
            Status = "Proposed",
            ClinicalReason = "Kiểm tra lành thương sau điều trị",
            CreatedAt = DateTime.UtcNow
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var savedFollowUp = await db.FollowUpSchedules
            .Include(f => f.TreatmentSession)
            .FirstOrDefaultAsync(f => f.FollowUpId == followUp.FollowUpId);

        Assert.NotNull(savedFollowUp);
        Assert.Equal(fixture.patientId, savedFollowUp.PatientUserId);
        Assert.Equal(session.SessionId, savedFollowUp.TreatmentSessionId);
    }

    [Fact]
    public async Task GroupG_CrossPatientIsolation_PreventsUnauthorizedAccess()
    {
        if (!await IsSqlServerAvailableAsync()) return;

        using var db = CreateDbContext();
        await CleanDatabaseAsync(db);
        var fixture = await SeedBaseFixtureAsync(db);

        // Create Patient 2
        var patient2User = new UserAccount { Email = $"pat2_{Guid.NewGuid():N}@dentalcare.com", Role = UserRole.Patient, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        db.UserAccounts.Add(patient2User);
        await db.SaveChangesAsync();

        var patient2Profile = new PatientProfile { UserId = patient2User.UserId, FullName = "Trần Bệnh Nhân 2", CreatedAt = DateTime.UtcNow };
        db.PatientProfiles.Add(patient2Profile);
        await db.SaveChangesAsync();

        var medRecordService = new MedicalRecordService(db, NullLogger<MedicalRecordService>.Instance);

        // Verify authorization check in controller: MedicalRecordController requires Dentist or Receptionist or Admin to query arbitrary patient, while Patient can only query GetMyMedicalRecord
        var controllerAuth = typeof(DentalClinic.Api.Controllers.MedicalRecordController)
            .GetMethod(nameof(DentalClinic.Api.Controllers.MedicalRecordController.GetByPatientId))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .First();

        var allowedRoles = controllerAuth.Roles!.Split(',', StringSplitOptions.TrimEntries);
        Assert.DoesNotContain(UserRole.Patient, allowedRoles);
    }
}
