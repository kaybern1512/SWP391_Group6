using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.EndToEnd;

[Collection("SqlServerIntegration")]
public class ReturningPatientAndFollowUpIntegrationTests
{
    private const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=DentalClinic_ReturningPatientTests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

    private static async Task<bool> IsLocalDbAvailableAsync()
    {
        try
        {
            using var conn = new SqlConnection("Server=(localdb)\\mssqllocaldb;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5;");
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static DentalClinicDbContext CreateSqlServerDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    [Fact]
    public async Task ReturningPatient_TwoConsecutiveVisits_PreservesLongitudinalHistory_AndSeparatesInvoices()
    {
        if (!await IsLocalDbAvailableAsync()) return;

        using var db = CreateSqlServerDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        // 1. Seed base users
        var patientUser = new UserAccount { Email = "returning.patient@test.com", Role = UserRole.Patient, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var dentistUser = new UserAccount { Email = "dentist.care@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var staffUser = new UserAccount { Email = "staff.frontdesk@test.com", Role = UserRole.Receptionist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        db.UserAccounts.AddRange(patientUser, dentistUser, staffUser);
        await db.SaveChangesAsync();

        var patientProfile = new PatientProfile { UserId = patientUser.UserId, PatientCode = "PAT-RETURN-01", FullName = "Nguyễn Tái Khám", CreatedAt = DateTime.UtcNow };
        var staffProfile = new StaffProfile { UserId = staffUser.UserId, EmployeeCode = "STAFF-01", FullName = "Lễ Tân", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };
        var dentistStaffProfile = new StaffProfile { UserId = dentistUser.UserId, EmployeeCode = "DOC-01", FullName = "Bác Sĩ Điều Trị", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };
        var dentistProfile = new DentistProfile { UserId = dentistUser.UserId, Qualification = "Bác Sĩ CKI", YearsOfExperience = 7, Biography = "Dentist" };
        var dept = new Department { Name = "Khoa Răng Hàm Mặt", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        db.PatientProfiles.Add(patientProfile);
        db.StaffProfiles.AddRange(staffProfile, dentistStaffProfile);
        db.DentistProfiles.Add(dentistProfile);
        await db.SaveChangesAsync();
        await db.SaveChangesAsync();

        var mockNotif = new Mock<INotificationService>();
        var mockApptLogger = new Mock<ILogger<AppointmentService>>();
        var mockMedLogger = new Mock<ILogger<MedicalRecordService>>();
        var mockBillLogger = new Mock<ILogger<BillingService>>();
        var mockPrescLogger = new Mock<ILogger<PrescriptionService>>();
        var mockPostCareLogger = new Mock<ILogger<PostCareService>>();

        // ==========================================
        // VISIT 1: INITIAL VISIT
        // ==========================================
        var appt1 = new Appointment
        {
            AppointmentCode = "APP-VISIT-001",
            PatientUserId = patientUser.UserId,
            DepartmentId = dept.DepartmentId,
            AssignedDentistUserId = dentistUser.UserId,
            Status = AppointmentStatus.InService,
            ReasonForVisit = "Đau nhức răng 36",
            BookingSource = "Patient",
            ScheduledStart = DateTime.UtcNow.AddDays(-7),
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        };
        db.Appointments.Add(appt1);
        await db.SaveChangesAsync();

        // Dentist saves clinical entry for Visit 1
        var medService = new MedicalRecordService(db, mockMedLogger.Object);
        var entry1Res = await medService.CreateClinicalEntryAsync(dentistUser.UserId, new CreateClinicalEntryRequest
        {
            AppointmentId = appt1.AppointmentId,
            PatientUserId = patientUser.UserId,
            Symptoms = "Đau dữ dội vùng hàm dưới trái",
            ClinicalFindings = "Lỗ sâu lớn mặt nhai răng 36, tủy hoại tử",
            Diagnosis = "Viêm tủy không hồi phục răng 36",
            DentalConditions = "Sâu răng độ 3",
            ClinicalNotes = "Cần điều trị tủy và đặt hẹn tái khám",
            ToothConditions = new List<ToothConditionInput>
            {
                new() { ToothNumber = "36", Surface = "O", Condition = "Decayed", Note = "Lỗ sâu tủy" }
            }
        });
        Assert.True(entry1Res.Success);

        // Dentist creates TreatmentPlan and Session for Visit 1
        var plan = new TreatmentPlan
        {
            PatientUserId = patientUser.UserId,
            DepartmentId = dept.DepartmentId,
            CreatedByDentistUserId = dentistUser.UserId,
            Status = "Active",
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        };
        db.TreatmentPlans.Add(plan);
        await db.SaveChangesAsync();

        var phase1 = new TreatmentPlanItem
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            AssignedDentistUserId = dentistUser.UserId,
            ToothNumber = "36",
            SequenceNo = 1,
            Status = "Completed",
            Note = "Buổi 1: Mở tủy và đặt thuốc diệt tủy"
        };
        var phase2 = new TreatmentPlanItem
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            AssignedDentistUserId = dentistUser.UserId,
            ToothNumber = "36",
            SequenceNo = 2,
            Status = "Pending",
            Note = "Buổi 2: Trám bít ống tủy"
        };
        db.TreatmentPlanItems.AddRange(phase1, phase2);
        await db.SaveChangesAsync();

        var session1 = new TreatmentSession
        {
            TreatmentPlanItemId = phase1.TreatmentPlanItemId,
            AppointmentId = appt1.AppointmentId,
            DentistUserId = dentistUser.UserId,
            SessionDate = DateTime.UtcNow.AddDays(-7),
            TreatmentResult = "Đã làm sạch buồng tủy",
            ClinicalNote = "Hẹn bệnh nhân 7 ngày sau quay lại trám bít"
        };
        db.TreatmentSessions.Add(session1);
        await db.SaveChangesAsync();

        // Prescribe medication for Visit 1
        var prescService = new PrescriptionService(db, mockPrescLogger.Object);
        var presc1Res = await prescService.CreatePrescriptionAsync(dentistUser.UserId, new CreatePrescriptionRequest
        {
            AppointmentId = appt1.AppointmentId,
            GeneralInstructions = "Uống sau ăn no",
            Items = new List<CreatePrescriptionItemInput>
            {
                new() { MedicationName = "Amoxicillin 500mg", Dosage = "1 viên", Frequency = "2 lần/ngày", Duration = "5 ngày" },
                new() { MedicationName = "Paracetamol 500mg", Dosage = "1 viên", Frequency = "Khi đau cách 6h", Duration = "3 ngày" }
            }
        });
        Assert.True(presc1Res.Success);

        // Complete Visit 1
        var apptService = new AppointmentService(db, mockNotif.Object, mockApptLogger.Object);
        var comp1Res = await apptService.DentistCompleteAppointmentAsync(appt1.AppointmentId, dentistUser.UserId, "Hoàn tất buổi 1");
        Assert.True(comp1Res.Success);

        // Create Invoice 1 and Pay in full
        var billService = new BillingService(db, mockNotif.Object, mockBillLogger.Object);
        var inv1Res = await billService.CreateInvoiceAsync(staffUser.UserId, new CreateInvoiceRequest
        {
            AppointmentId = appt1.AppointmentId,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Điều trị tủy buổi 1 răng 36", Quantity = 1, UnitPrice = 800000m }
            }
        });
        Assert.True(inv1Res.Success);
        var pay1Res = await billService.ProcessPaymentAsync(staffUser.UserId, new ProcessPaymentRequest
        {
            InvoiceId = inv1Res.Data!.InvoiceId,
            Amount = 800000m,
            Method = "Cash",
            TransactionReference = "TXN-VISIT1-CASH"
        });
        Assert.True(pay1Res.Success);
        Assert.Equal("Paid", pay1Res.Data!.Status);

        // Dentist schedules a Follow-Up reminder
        var postCareService = new PostCareService(db, mockNotif.Object, mockPostCareLogger.Object);
        var followUpRes = await postCareService.CreateFollowUpAsync(dentistUser.UserId, new CreateFollowUpRequest
        {
            TreatmentSessionId = session1.TreatmentSessionId,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Reason = "Tái khám trám bít ống tủy răng 36"
        });
        Assert.True(followUpRes.Success);
        Assert.Equal("Pending", followUpRes.Data!.Status);

        // ==========================================
        // VISIT 2: RETURNING PATIENT VISIT (7 DAYS LATER)
        // ==========================================
        var appt2 = new Appointment
        {
            AppointmentCode = "APP-VISIT-002",
            PatientUserId = patientUser.UserId, // EXACT SAME PATIENT!
            DepartmentId = dept.DepartmentId,
            AssignedDentistUserId = dentistUser.UserId,
            Status = AppointmentStatus.InService,
            ReasonForVisit = "Tái khám theo hẹn trám tủy răng 36",
            BookingSource = "Receptionist",
            ScheduledStart = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt2);
        await db.SaveChangesAsync();

        // VERIFICATION: Check patient profile is NOT duplicated!
        var patientProfilesCount = await db.PatientProfiles.CountAsync(p => p.UserId == patientUser.UserId);
        Assert.Equal(1, patientProfilesCount);

        // Dentist retrieves previous clinical history
        var patientRecordRes = await medService.GetMedicalRecordByPatientIdAsync(patientUser.UserId);
        Assert.True(patientRecordRes.Success);
        Assert.Single(patientRecordRes.Data!.ClinicalEntries); // Previous Visit 1 entry is present!
        Assert.Equal("Viêm tủy không hồi phục răng 36", patientRecordRes.Data.ClinicalEntries.First().Diagnosis);

        // Dentist records new ClinicalEntry for Visit 2
        var entry2Res = await medService.CreateClinicalEntryAsync(dentistUser.UserId, new CreateClinicalEntryRequest
        {
            AppointmentId = appt2.AppointmentId,
            PatientUserId = patientUser.UserId,
            Symptoms = "Hết đau, không còn ê buốt",
            ClinicalFindings = "Tủy khô sạch, không sưng nề",
            Diagnosis = "Đang điều trị tủy răng 36 - Ổn định",
            ClinicalNotes = "Đã trám bít ống tủy vĩnh viễn bằng Gutta-percha",
            ToothConditions = new List<ToothConditionInput>
            {
                new() { ToothNumber = "36", Surface = "O", Condition = "Filled", Note = "Trám bít ống tủy" }
            }
        });
        Assert.True(entry2Res.Success);

        // Record Session 2 for the ongoing TreatmentPlan
        phase2.Status = "Completed";
        plan.Status = "Completed";
        var session2 = new TreatmentSession
        {
            TreatmentPlanItemId = phase2.TreatmentPlanItemId,
            AppointmentId = appt2.AppointmentId,
            DentistUserId = dentistUser.UserId,
            SessionDate = DateTime.UtcNow,
            TreatmentResult = "Trám bít hoàn hảo, chụp phim kiểm tra đạt chuẩn",
            ClinicalNote = "Hoàn tất quy trình điều trị tủy răng 36"
        };
        db.TreatmentSessions.Add(session2);
        await db.SaveChangesAsync();

        // Complete Visit 2
        var comp2Res = await apptService.DentistCompleteAppointmentAsync(appt2.AppointmentId, dentistUser.UserId, "Hoàn thành ca điều trị tủy");
        Assert.True(comp2Res.Success);

        // Create separate Invoice 2 and Pay in full
        var inv2Res = await billService.CreateInvoiceAsync(staffUser.UserId, new CreateInvoiceRequest
        {
            AppointmentId = appt2.AppointmentId,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "Trám bít ống tủy răng 36", Quantity = 1, UnitPrice = 700000m }
            }
        });
        Assert.True(inv2Res.Success);
        Assert.NotEqual(inv1Res.Data!.InvoiceId, inv2Res.Data!.InvoiceId); // Separate invoice!

        var pay2Res = await billService.ProcessPaymentAsync(staffUser.UserId, new ProcessPaymentRequest
        {
            InvoiceId = inv2Res.Data!.InvoiceId,
            Amount = 700000m,
            Method = "BankTransfer",
            TransactionReference = "TXN-VISIT2-BANK"
        });
        Assert.True(pay2Res.Success);

        // ==========================================
        // FINAL COMPREHENSIVE INTEGRITY VERIFICATION
        // ==========================================
        using var verifyDb = CreateSqlServerDbContext();

        // 1. Same PatientUserId, single profile
        var finalProfiles = await verifyDb.PatientProfiles.Where(p => p.UserId == patientUser.UserId).ToListAsync();
        Assert.Single(finalProfiles);

        // 2. Both appointments are preserved with correct status and dates
        var appointments = await verifyDb.Appointments.Where(a => a.PatientUserId == patientUser.UserId).OrderBy(a => a.AppointmentId).ToListAsync();
        Assert.Equal(2, appointments.Count);
        Assert.Equal(AppointmentStatus.Completed, appointments[0].Status);
        Assert.Equal(AppointmentStatus.Completed, appointments[1].Status);

        // 3. Clinical entries: 2 distinct entries linked to respective visits
        var clinicalEntries = await verifyDb.ClinicalEntries.Where(c => c.MedicalRecord.PatientUserId == patientUser.UserId).OrderBy(c => c.CreatedAt).ToListAsync();
        Assert.Equal(2, clinicalEntries.Count);
        Assert.Equal(appt1.AppointmentId, clinicalEntries[0].AppointmentId);
        Assert.Equal(appt2.AppointmentId, clinicalEntries[1].AppointmentId);

        // 4. Prescriptions: Visit 1 prescription is untouched and has 2 items
        var prescList = await verifyDb.Prescriptions.Include(p => p.PrescriptionItems).Where(p => p.TreatmentSession.Appointment.PatientUserId == patientUser.UserId).ToListAsync();
        Assert.Single(prescList);
        Assert.Equal(2, prescList.First().PrescriptionItems.Count);

        // 5. Invoices: 2 distinct invoices, both fully Paid, independent amounts
        var invoiceList = await verifyDb.Invoices.Include(i => i.Payments).Where(i => i.Appointment.PatientUserId == patientUser.UserId).OrderBy(i => i.InvoiceId).ToListAsync();
        Assert.Equal(2, invoiceList.Count);
        Assert.Equal(800000m, invoiceList[0].TotalAmount);
        Assert.Equal("Paid", invoiceList[0].Status);
        Assert.Equal(700000m, invoiceList[1].TotalAmount);
        Assert.Equal("Paid", invoiceList[1].Status);

        // 6. FollowUpSchedule remains intact as a clinical reminder
        var followUps = await verifyDb.FollowUpSchedules.Where(f => f.TreatmentSession.Appointment.PatientUserId == patientUser.UserId).ToListAsync();
        Assert.Single(followUps);
        Assert.Equal("Pending", followUps.First().Status);
    }
}
