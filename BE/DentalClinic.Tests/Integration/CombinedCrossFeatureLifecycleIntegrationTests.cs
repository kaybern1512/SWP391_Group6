using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.DentistLeave.DTOs;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Integration;

[Collection("CombinedIntegration")]
public class CombinedCrossFeatureLifecycleIntegrationTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION")
        ?? $"Server={Environment.GetEnvironmentVariable("TEST_SQL_SERVER") ?? Environment.GetEnvironmentVariable("DENTAL_CLINIC_SQL_INSTANCE") ?? ".\\MSSQLSERVER01"};Database={Environment.GetEnvironmentVariable("DENTAL_CLINIC_TEST_DB") ?? "DentalCare_Combined_Leave_QA"};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";

    private static DentalClinicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task CleanTestDataAsync(DateOnly testDate)
    {
        using var db = CreateDbContext();
        var dtStart = testDate.ToDateTime(new TimeOnly(0, 0));
        var dtEnd = testDate.ToDateTime(new TimeOnly(23, 59, 59));

        // 0. Clean stale InService appointments for test chair or dentists
        var staleInService = await db.Appointments
            .Where(a => a.Status == AppointmentStatus.InService && (a.DentalChairId == 1 || a.AssignedDentistUserId == 3 || a.AssignedDentistUserId == 6))
            .ToListAsync();
        foreach (var s in staleInService)
        {
            s.Status = AppointmentStatus.Completed;
            s.CompletedAt = DateTime.UtcNow;
        }

        // 1. Clean disruptions
        var disruptions = await db.AppointmentDisruptions
            .Where(d => d.OriginalScheduledStart >= dtStart && d.OriginalScheduledStart <= dtEnd)
            .ToListAsync();
        db.AppointmentDisruptions.RemoveRange(disruptions);

        // 2. Clean payments & invoices for appointments on that day
        var apptIds = await db.Appointments
            .Where(a => a.ScheduledStart >= dtStart && a.ScheduledStart <= dtEnd)
            .Select(a => a.AppointmentId)
            .ToListAsync();

        if (apptIds.Count > 0)
        {
            var histories = await db.AppointmentStatusHistories.Where(h => apptIds.Contains(h.AppointmentId)).ToListAsync();
            db.AppointmentStatusHistories.RemoveRange(histories);

            var payments = await db.Payments.Where(p => p.Invoice.AppointmentId == 0 || apptIds.Contains(p.Invoice.AppointmentId)).ToListAsync();
            db.Payments.RemoveRange(payments);

            var invItems = await db.InvoiceItems.Where(i => apptIds.Contains(i.Invoice.AppointmentId)).ToListAsync();
            db.InvoiceItems.RemoveRange(invItems);

            var invoices = await db.Invoices.Where(i => apptIds.Contains(i.AppointmentId)).ToListAsync();
            db.Invoices.RemoveRange(invoices);

            var appts = await db.Appointments.Where(a => apptIds.Contains(a.AppointmentId)).ToListAsync();
            db.Appointments.RemoveRange(appts);
        }

        // 3. Clean leave requests & availability
        var leaves = await db.DentistLeaveRequests
            .Where(l => l.StartDate == testDate)
            .ToListAsync();
        db.DentistLeaveRequests.RemoveRange(leaves);

        var avails = await db.DentistAvailabilities
            .Where(a => a.StartDateTime >= dtStart && a.StartDateTime <= dtEnd)
            .ToListAsync();
        db.DentistAvailabilities.RemoveRange(avails);

        var dentistSchedules = await db.DentistWorkSchedules
            .Where(s => s.WorkDate == testDate)
            .ToListAsync();
        db.DentistWorkSchedules.RemoveRange(dentistSchedules);

        // 4. Clean receptionist schedules & incidents
        var recSchedules = await db.ReceptionistWorkSchedules
            .Where(s => s.WorkDate == testDate)
            .ToListAsync();
        db.ReceptionistWorkSchedules.RemoveRange(recSchedules);

        var incidents = await db.ReceptionistCoverageIncidents
            .Where(i => i.WorkDate == testDate)
            .ToListAsync();
        db.ReceptionistCoverageIncidents.RemoveRange(incidents);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CrossFeature_CompleteLifecycle_ReceptionistShift_DentistLeave_DisruptionRecovery_And_EmergencyAbsence_EnforcesAllInvariants()
    {
        var testDate = new DateOnly(2027, 1, 15);
        await CleanTestDataAsync(testDate);

        try
        {
            long dentist1UserId = 3;   // Dr. Trần Văn Hùng (Dept 1)
            long dentist2UserId = 6;   // Dr. Nguyễn Thị Mai (Dept 1)
            long patientUserId = 13;   // Bệnh nhân Trần Mai Chi
            long receptionistAUserId = 2; // Lễ tân A
            long receptionistBUserId = 44; // Lễ tân B
            long receptionistCUserId = 45; // Lễ tân C (Substitute)
            long adminUserId = 1;     // Quản trị viên
            long departmentId = 1;
            long roomId = 1;           // ROOM-101
            long chairId = 1;          // CHAIR-101-A

            var slotStart = testDate.ToDateTime(new TimeOnly(9, 0));
            var slotEnd = testDate.ToDateTime(new TimeOnly(9, 45));

            // =================================================================
            // STEP 1: Receptionist Active Shift & Appointment Creation
            // =================================================================
            long apptId;
            long schedAId;
            using (var db = CreateDbContext())
            {
                // Ensure 2 Receptionists are scheduled on shift (Min 2 rule)
                var schedA = new ReceptionistWorkSchedule
                {
                    ReceptionistUserId = receptionistAUserId,
                    WorkDate = testDate,
                    StartTime = new TimeOnly(7, 0),
                    EndTime = new TimeOnly(19, 0),
                    Status = "Scheduled",
                    IsPublished = true,
                    CreatedBy = adminUserId,
                    CreatedAt = DateTime.UtcNow
                };
                var schedB = new ReceptionistWorkSchedule
                {
                    ReceptionistUserId = receptionistBUserId,
                    WorkDate = testDate,
                    StartTime = new TimeOnly(7, 0),
                    EndTime = new TimeOnly(19, 0),
                    Status = "Scheduled",
                    IsPublished = true,
                    CreatedBy = adminUserId,
                    CreatedAt = DateTime.UtcNow
                };
                db.ReceptionistWorkSchedules.AddRange(schedA, schedB);
                await db.SaveChangesAsync();
                schedAId = schedA.ReceptionistScheduleId;

                // Ensure Dentist 1 & Dentist 2 have active work schedules
                db.DentistWorkSchedules.AddRange(
                    new DentistWorkSchedule
                    {
                        DentistUserId = dentist1UserId,
                        DepartmentId = departmentId,
                        WorkDate = testDate,
                        StartTime = new TimeOnly(8, 0),
                        EndTime = new TimeOnly(17, 0),
                        Status = "Scheduled"
                    },
                    new DentistWorkSchedule
                    {
                        DentistUserId = dentist2UserId,
                        DepartmentId = departmentId,
                        WorkDate = testDate,
                        StartTime = new TimeOnly(8, 0),
                        EndTime = new TimeOnly(17, 0),
                        Status = "Scheduled"
                    }
                );

                // Create confirmed appointment for patient with Dentist 1
                var appt = new Appointment
                {
                    AppointmentCode = $"APP-{testDate:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 24),
                    PatientUserId = patientUserId,
                    DepartmentId = departmentId,
                    RequestedServiceId = 1,
                    AssignedDentistUserId = dentist1UserId,
                    RoomId = roomId,
                    DentalChairId = chairId,
                    QueueNumber = "Q-001",
                    ScheduledStart = slotStart,
                    ScheduledEnd = slotEnd,
                    Status = AppointmentStatus.Confirmed,
                    ReasonForVisit = "Kiểm tra răng định kỳ",
                    BookingSource = "Receptionist",
                    CreatedAt = DateTime.UtcNow
                };
                db.Appointments.Add(appt);
                await db.SaveChangesAsync();
                apptId = appt.AppointmentId;
            }

            Assert.True(apptId > 0);

            // =================================================================
            // STEP 2: Dentist submits Leave Request & Admin Approves
            // =================================================================
            long leaveRequestId;
            var mockReminder = new Mock<IAppointmentReminderService>();
            var mockEmail = new Mock<IEmailService>();
            var mockNotification = new Mock<INotificationService>();
            var mockConfig = new Mock<IConfiguration>();
            var mockLeaveLogger = new Mock<ILogger<DentistLeaveService>>();
            var mockDisruptLogger = new Mock<ILogger<AppointmentDisruptionService>>();
            var mockApptLogger = new Mock<ILogger<AppointmentService>>();
            var mockBillingLogger = new Mock<ILogger<BillingService>>();
            var mockAudit = new Mock<IAuditLogService>();

            mockConfig.Setup(c => c["App:BaseUrl"]).Returns("https://localhost:7129");

            using (var db = CreateDbContext())
            {
                var leaveService = new DentistLeaveService(db, mockReminder.Object, mockEmail.Object, mockNotification.Object, mockConfig.Object, mockLeaveLogger.Object);

                var submitReq = new CreateDentistLeaveRequest
                {
                    LeaveType = DentistLeaveType.AnnualLeave,
                    StartDate = testDate,
                    EndDate = testDate,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(17, 0),
                    Reason = "Nghỉ phép tham dự hội nghị nha khoa quốc tế",
                    Notes = "Đã thông báo khoa"
                };

                var submitResult = await leaveService.SubmitLeaveRequestAsync(submitReq, dentist1UserId);
                Assert.True(submitResult.Success, submitResult.Message);
                leaveRequestId = submitResult.Data!.LeaveRequestId;

                // Admin approves the leave
                var approveResult = await leaveService.ApproveLeaveRequestAsync(leaveRequestId, adminUserId, new ApproveDentistLeaveRequest
                {
                    AdminNotes = "Phê duyệt nghỉ phép tham dự hội nghị"
                });
                Assert.True(approveResult.Success, approveResult.Message);
            }

            // =================================================================
            // STEP 3: Verify Disruption Record in DB
            // =================================================================
            long disruptionId;
            string actionToken;
            using (var db = CreateDbContext())
            {
                // Invariant: Leave is Approved
                var leaveInDb = await db.DentistLeaveRequests.FindAsync(leaveRequestId);
                Assert.NotNull(leaveInDb);
                Assert.Equal("Approved", leaveInDb.Status);

                // Invariant: DentistAvailability marked OnLeave
                var avail = await db.DentistAvailabilities.FirstOrDefaultAsync(a => a.DentistUserId == dentist1UserId && a.StartDateTime >= slotStart.Date);
                Assert.NotNull(avail);
                Assert.Equal(DentistAvailabilityStatus.OnLeave, avail.AvailabilityStatus);

                // Invariant: Disruption record created with ActionToken
                var disruption = await db.AppointmentDisruptions.FirstOrDefaultAsync(d => d.AppointmentId == apptId);
                Assert.NotNull(disruption);
                Assert.Equal(AppointmentDisruptionStatus.PendingPatientChoice, disruption.Status);
                Assert.Equal(dentist1UserId, disruption.OriginalDentistUserId);
                Assert.False(string.IsNullOrWhiteSpace(disruption.ActionToken));
                Assert.NotNull(disruption.TokenExpiresAt);
                Assert.True(disruption.TokenExpiresAt > DateTime.UtcNow);

                disruptionId = disruption.DisruptionId;
                actionToken = disruption.ActionToken;

                // Invariant: Appointment status is Confirmed with disruption tracking
                var apptInDb = await db.Appointments.FindAsync(apptId);
                Assert.NotNull(apptInDb);
                Assert.Equal(AppointmentStatus.Confirmed, apptInDb.Status);
            }

            // =================================================================
            // STEP 4: Patient recovers by selecting Alternative Dentist (Doctor 2)
            // =================================================================
            using (var db = CreateDbContext())
            {
                var disruptionService = new AppointmentDisruptionService(db, mockReminder.Object, mockEmail.Object, mockNotification.Object, mockConfig.Object, mockDisruptLogger.Object);

                var chooseReq = new PatientChooseNewDentistRequest
                {
                    NewDentistUserId = dentist2UserId,
                    Note = "Tôi đồng ý chuyển sang BS. Nguyễn Thị Mai",
                    ActionToken = actionToken
                };

                var recoverResult = await disruptionService.PatientChooseNewDentistAsync(disruptionId, patientUserId, chooseReq);
                Assert.True(recoverResult.Success, recoverResult.Message);

                // Invariant: Disruption marked DoctorChanged
                var disruptionInDb = await db.AppointmentDisruptions.FindAsync(disruptionId);
                Assert.NotNull(disruptionInDb);
                Assert.Equal(AppointmentDisruptionStatus.DoctorChanged, disruptionInDb.Status);
                Assert.Equal(dentist2UserId, disruptionInDb.NewDentistUserId);

                // Invariant: Appointment assigned to Dentist 2, status Confirmed
                var apptInDb = await db.Appointments.FindAsync(apptId);
                Assert.NotNull(apptInDb);
                Assert.Equal(dentist2UserId, apptInDb.AssignedDentistUserId);
                Assert.Equal(AppointmentStatus.Confirmed, apptInDb.Status);
                Assert.Null(apptInDb.QueueNumber); // QueueNumber reset upon doctor change, ready for receptionist check-in issuance
            }

            // =================================================================
            // STEP 5: Receptionist queries disruptions and sees resolution
            // =================================================================
            using (var db = CreateDbContext())
            {
                var disruptionService = new AppointmentDisruptionService(db, mockReminder.Object, mockEmail.Object, mockNotification.Object, mockConfig.Object, mockDisruptLogger.Object);
                var disruptionsResult = await disruptionService.GetReceptionistDisruptionsAsync();
                Assert.True(disruptionsResult.Success);
                var targetDisruption = disruptionsResult.Data!.FirstOrDefault(d => d.DisruptionId == disruptionId);
                Assert.NotNull(targetDisruption);
                Assert.Equal("DoctorChanged", targetDisruption.Status);
            }

            // =================================================================
            // STEP 6: Receptionist A reports Emergency Absence & System Detects Understaffing
            // =================================================================
            long incidentId;
            using (var db = CreateDbContext())
            {
                var recCoverageService = new ReceptionistCoverageService(db, mockNotification.Object, mockAudit.Object);

                // Receptionist A reports absence
                var absenceResult = await recCoverageService.ReportEmergencyAbsenceAsync(new ReportEmergencyAbsenceRequest
                {
                    ReceptionistUserId = receptionistAUserId,
                    ScheduleId = schedAId,
                    Reason = "Cấp cứu gia đình, không thể tiếp tục ca trực"
                }, receptionistAUserId);
                Assert.True(absenceResult.Success, absenceResult.Message);

                // Incident was created
                var incident = await db.ReceptionistCoverageIncidents
                    .FirstOrDefaultAsync(i => i.WorkDate == testDate && i.AffectedReceptionistUserId == receptionistAUserId);
                Assert.NotNull(incident);
                Assert.Equal("Open", incident.Status);
                incidentId = incident.IncidentId;
            }

            // =================================================================
            // STEP 7: Admin resolves Incident by assigning Substitute Receptionist C
            // =================================================================
            using (var db = CreateDbContext())
            {
                var recCoverageService = new ReceptionistCoverageService(db, mockNotification.Object, mockAudit.Object);

                var resolveReq = new ResolveCoverageIncidentRequest
                {
                    ReplacementReceptionistUserId = receptionistCUserId,
                    ResolutionNotes = "Điều động Lễ tân C thay thế ca trực của Lễ tân A"
                };

                var resolveResult = await recCoverageService.ResolveCoverageIncidentAsync(incidentId, resolveReq, adminUserId);
                Assert.True(resolveResult.Success, resolveResult.Message);

                // Invariant: New schedule created for substitute C
                var subSchedule = await db.ReceptionistWorkSchedules
                    .FirstOrDefaultAsync(s => s.ReceptionistUserId == receptionistCUserId && s.WorkDate == testDate && s.Status == "Scheduled");
                Assert.NotNull(subSchedule);

                // Invariant: Incident status is Resolved
                var incidentInDb = await db.ReceptionistCoverageIncidents.FindAsync(incidentId);
                Assert.NotNull(incidentInDb);
                Assert.Equal("Resolved", incidentInDb.Status);
            }

            // =================================================================
            // STEP 8: Substitute Receptionist C handles Check-in, Completion & Billing
            // =================================================================
            using (var db = CreateDbContext())
            {
                var apptService = new AppointmentService(db, mockNotification.Object, mockApptLogger.Object);
                var billingService = new BillingService(db, mockNotification.Object, mockBillingLogger.Object);

                // Substitute Receptionist checks in patient
                var checkInResult = await apptService.ReceptionistCheckInAsync(apptId, receptionistCUserId, "Bệnh nhân đã tới quầy tiếp đón làm thủ tục");
                Assert.True(checkInResult.Success, checkInResult.Message);

                // Dentist 2 starts examination
                var startResult = await apptService.DentistStartAppointmentAsync(apptId, dentist2UserId);
                Assert.True(startResult.Success, startResult.Message);

                // Dentist 2 completes examination
                var completeResult = await apptService.DentistCompleteAppointmentAsync(apptId, dentist2UserId, "Khám tổng quát hoàn tất, răng khỏe mạnh");
                Assert.True(completeResult.Success, completeResult.Message);

                // Substitute Receptionist creates invoice
                var invoiceReq = new CreateInvoiceRequest
                {
                    AppointmentId = apptId,
                    TaxPercent = 0,
                    DiscountAmount = 0,
                    Note = "Thu viện phí khám lâm sàng",
                    Items = new List<CreateInvoiceItemRequest>
                    {
                        new CreateInvoiceItemRequest
                        {
                            Description = "Khám Lâm Sàng và Tư Vấn",
                            Quantity = 1,
                            UnitPrice = 500000
                        }
                    }
                };
                var invoiceResult = await billingService.CreateInvoiceAsync(receptionistCUserId, invoiceReq);
                Assert.True(invoiceResult.Success, invoiceResult.Message);
                long invoiceId = invoiceResult.Data!.InvoiceId;

                // Process payment
                var payResult = await billingService.ProcessPaymentAsync(receptionistCUserId, new ProcessPaymentRequest
                {
                    InvoiceId = invoiceId,
                    Method = "Cash",
                    Amount = 500000,
                    Note = "Bệnh nhân thanh toán đủ tiền mặt"
                });
                Assert.True(payResult.Success, payResult.Message);
                Assert.Equal("Paid", payResult.Data!.Status);

                // =================================================================
                // STEP 9: Cross-Feature Invariant Checks
                // =================================================================
                var finalAppt = await db.Appointments.FindAsync(apptId);
                Assert.NotNull(finalAppt);
                Assert.Equal(AppointmentStatus.Completed, finalAppt.Status);
                Assert.Equal(dentist2UserId, finalAppt.AssignedDentistUserId);

                // Invariant: No double booking for Dentist 2
                var overlappingAppts = await db.Appointments
                    .Where(a => a.AssignedDentistUserId == dentist2UserId &&
                                a.ScheduledStart < slotEnd &&
                                (a.ScheduledEnd ?? a.ScheduledStart.Value.AddMinutes(30)) > slotStart &&
                                a.AppointmentId != apptId &&
                                a.Status != AppointmentStatus.Cancelled)
                    .CountAsync();
                Assert.Equal(0, overlappingAppts);

                // Invariant: Zero duplicate QueueNumbers on Department 1 for testDate
                var queueCollisions = await db.Appointments
                    .Where(a => a.DepartmentId == departmentId &&
                                a.ScheduledStart >= slotStart.Date &&
                                a.ScheduledStart < slotStart.Date.AddDays(1) &&
                                a.QueueNumber != null &&
                                a.QueueNumber != "")
                    .GroupBy(a => a.QueueNumber)
                    .Where(g => g.Count() > 1)
                    .CountAsync();
                Assert.Equal(0, queueCollisions);

                // Invariant: Invoice fully paid, TotalAmount == 500000
                var finalInvoice = await db.Invoices
                    .Include(i => i.Payments)
                    .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
                Assert.NotNull(finalInvoice);
                Assert.Equal("Paid", finalInvoice.Status);
                Assert.Equal(500000, finalInvoice.TotalAmount);
                Assert.Equal(500000, finalInvoice.Payments.Sum(p => p.Amount));
            }
        }
        finally
        {
            await CleanTestDataAsync(testDate);
        }
    }
}
