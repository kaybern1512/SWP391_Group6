using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.EndToEnd;

public class RealDatabaseEndToEndSmokeTests
{
    private const string ConnectionString = "Server=DESKTOP-1TUBGEU\\SQLEXPRESS;Database=DentalClinic_Disposable_PreDeployClone;Trusted_Connection=True;MultipleActiveResultSets=false;TrustServerCertificate=True;";

    private DentalClinicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    [Fact]
    public async Task ScenarioA_PatientBooking_EndToEndLifecycle_Succeeds()
    {
        using var db = CreateDbContext();
        var mockNotification = new Mock<INotificationService>();
        var mockApptLogger = new Mock<ILogger<AppointmentService>>();
        var mockBillingLogger = new Mock<ILogger<BillingService>>();
        var mockPrescLogger = new Mock<ILogger<PrescriptionService>>();

        var apptService = new AppointmentService(db, mockNotification.Object, mockApptLogger.Object);
        var billingService = new BillingService(db, mockNotification.Object, mockBillingLogger.Object);
        var prescService = new PrescriptionService(db, mockPrescLogger.Object);

        long patientUserId = 13; // Trần Mai Chi (patient1@dentalcare.com)
        long receptionistUserId = 2; // Lễ tân (receptionist@dentalcare.com)
        long managerUserId = 10; // Trưởng khoa 1 (manager1@dentalcare.com)
        long dentistUserId = 3; // BS. Trần Văn Hùng (dentist@dentalcare.com)
        long departmentId = 1; // General Dentistry
        long roomId = 1; // ROOM-101 (Shared)
        long chairId = 1; // First chair in ROOM-101

        var testDate = DateTime.Today.AddDays(300 + Random.Shared.Next(1, 50)).AddHours(9); // Next week 09:00

        // Step 1: Patient creates appointment
        var createReq = new CreateAppointmentRequest
        {
            DepartmentId = departmentId,
            RequestedServiceId = 1,
            RequestedDentistUserId = null,
            ScheduledStart = testDate,
            ReasonForVisit = "Đau răng cần trám răng thẩm mỹ"
        };
        var createResult = await apptService.CreatePatientAppointmentAsync(patientUserId, createReq);
        Assert.True(createResult.Success, createResult.Message);
        long apptId = createResult.Data!.AppointmentId;
        Assert.Equal(AppointmentStatus.PendingReceptionReview, createResult.Data.Status);

        // Step 2: Receptionist reviews and forwards
        var forwardResult = await apptService.ReceptionistForwardAsync(apptId, receptionistUserId, "Hồ sơ hợp lệ, chuyển trưởng khoa điều phối");
        Assert.True(forwardResult.Success, forwardResult.Message);

        var apptAfterFwd = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.PendingDepartmentReview, apptAfterFwd!.Status);

        // Step 3: DepartmentManager confirms and assigns resources
        var confirmReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = dentistUserId,
            RoomId = roomId,
            DentalChairId = chairId,
            ScheduledStart = testDate,
            ScheduledEnd = testDate.AddMinutes(45)
        };
        var confirmResult = await apptService.ManagerConfirmAsync(apptId, managerUserId, confirmReq);
        Assert.True(confirmResult.Success, confirmResult.Message);
        Assert.Equal(AppointmentStatus.Confirmed, confirmResult.Data!.Status);
        Assert.False(string.IsNullOrWhiteSpace(confirmResult.Data.QueueNumber));

        // Step 4: Receptionist checks in patient upon arrival
        var checkInResult = await apptService.ReceptionistCheckInAsync(apptId, receptionistUserId, "Bệnh nhân đã tới quầy làm thủ tục");
        Assert.True(checkInResult.Success, checkInResult.Message);

        var apptAfterCheckIn = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.CheckedIn, apptAfterCheckIn!.Status);

        // Step 5: Dentist starts examination
        var startResult = await apptService.DentistStartAppointmentAsync(apptId, dentistUserId);
        Assert.True(startResult.Success, startResult.Message);

        var apptAfterStart = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.InService, apptAfterStart!.Status);

        // Step 6: Dentist records clinical info & creates prescription
        var prescReq = new CreatePrescriptionRequest
        {
            AppointmentId = apptId,
            GeneralInstructions = "Uống thuốc đúng liều, tránh nhai đồ cứng trong 3 ngày",
            Items = new List<CreatePrescriptionItemInput>
            {
                new CreatePrescriptionItemInput
                {
                    MedicationName = "Amoxicillin 500mg",
                    Dosage = "500mg",
                    Frequency = "Ngày 2 lần sau ăn",
                    Duration = "5 ngày",
                    Instructions = "Uống vào 8h sáng và 8h tối sau ăn"
                }
            }
        };
        var prescResult = await prescService.CreatePrescriptionAsync(dentistUserId, prescReq);
        Assert.True(prescResult.Success, prescResult.Message);
        Assert.True(prescResult.Data!.PrescriptionId > 0);

        // Step 7: Dentist completes appointment
        var completeResult = await apptService.DentistCompleteAppointmentAsync(apptId, dentistUserId, "Đã trám thẩm mỹ composite răng 36, tình trạng ổn định");
        Assert.True(completeResult.Success, completeResult.Message);

        var apptAfterComplete = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.Completed, apptAfterComplete!.Status);

        // Step 8: Receptionist creates invoice & records payment
        var invReq = new CreateInvoiceRequest
        {
            AppointmentId = apptId,
            TaxPercent = 0,
            DiscountAmount = 0,
            Note = "Thanh toán viện phí dịch vụ",
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest
                {
                    Description = "Khám và Trám Răng Thẩm Mỹ",
                    Quantity = 1,
                    UnitPrice = 350000
                }
            }
        };
        var invResult = await billingService.CreateInvoiceAsync(receptionistUserId, invReq);
        Assert.True(invResult.Success, invResult.Message);
        long invoiceId = invResult.Data!.InvoiceId;

        var payReq = new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Method = "Cash",
            Amount = 350000,
            Note = "Bệnh nhân thanh toán tiền mặt đầy đủ"
        };
        var payResult = await billingService.ProcessPaymentAsync(receptionistUserId, payReq);
        Assert.True(payResult.Success, payResult.Message);
        Assert.Equal("Paid", payResult.Data!.Status);

        // Step 9: Patient views invoice
        var patientInvoices = await billingService.GetPatientInvoicesAsync(patientUserId);
        Assert.True(patientInvoices.Success);
        Assert.Contains(patientInvoices.Data!, inv => inv.InvoiceId == invoiceId && inv.Status == "Paid");
    }

    [Fact]
    public async Task ScenarioB_ReceptionistWalkInBooking_EndToEndLifecycle_Succeeds()
    {
        using var db = CreateDbContext();
        var mockNotification = new Mock<INotificationService>();
        var mockApptLogger = new Mock<ILogger<AppointmentService>>();

        var apptService = new AppointmentService(db, mockNotification.Object, mockApptLogger.Object);

        long patientUserId = 14; // Patient 2 (patient2@dentalcare.com)
        long receptionistUserId = 2; // Receptionist
        long managerUserId = 10; // Manager of Dept 1
        long dentistUserId = 3; // Dentist
        long departmentId = 1;
        long roomId = 4; // ROOM-102 (Shared)
        long chairId = 7; // Chair CHAIR-102-A in ROOM-102

        var slotTime = DateTime.Today.AddDays(400 + Random.Shared.Next(1, 50)).AddHours(14); // Next week afternoon

        // Step 1: Receptionist creates walk-in appointment
        var walkInReq = new CreateReceptionistAppointmentRequest
        {
            PatientUserId = patientUserId,
            DepartmentId = departmentId,
            RequestedServiceId = 1,
            ScheduledStart = slotTime,
            ReasonForVisit = "Bệnh nhân vãng lai đến quầy khám cấp do đau răng cấp"
        };
        var walkInResult = await apptService.CreateReceptionistAppointmentAsync(receptionistUserId, walkInReq);
        Assert.True(walkInResult.Success, walkInResult.Message);
        long apptId = walkInResult.Data!.AppointmentId;

        // Verify BookingSource is recorded
        var savedAppt = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.AppointmentId == apptId);
        Assert.NotNull(savedAppt);
        Assert.Equal("Receptionist", savedAppt.BookingSource);
        Assert.Equal(patientUserId, savedAppt.PatientUserId);
        Assert.Equal(AppointmentStatus.PendingDepartmentReview, savedAppt.Status);

        // Step 2: DepartmentManager confirms
        var confirmReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = dentistUserId,
            RoomId = roomId,
            DentalChairId = chairId,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30)
        };
        var confirmResult = await apptService.ManagerConfirmAsync(apptId, managerUserId, confirmReq);
        Assert.True(confirmResult.Success, confirmResult.Message);
        Assert.Equal(AppointmentStatus.Confirmed, confirmResult.Data!.Status);

        // Step 3: Receptionist checks in
        var checkInResult = await apptService.ReceptionistCheckInAsync(apptId, receptionistUserId, "Bệnh nhân đến quầy");
        Assert.True(checkInResult.Success, checkInResult.Message);

        // Step 4: Dentist starts examination
        var startResult = await apptService.DentistStartAppointmentAsync(apptId, dentistUserId);
        Assert.True(startResult.Success, startResult.Message);

        var apptAfterStart = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.InService, apptAfterStart!.Status);

        // Step 5: Dentist completes
        var completeResult = await apptService.DentistCompleteAppointmentAsync(apptId, dentistUserId, "Xử lý cấp cứu tủy buồng thành công");
        Assert.True(completeResult.Success, completeResult.Message);

        var apptAfterComplete = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.Completed, apptAfterComplete!.Status);
    }

    [Fact]
    public async Task ScenarioC_RoomChairAllocation_SharedVsExclusive_EnforcesCorrectRules()
    {
        using var db = CreateDbContext();
        var mockNotification = new Mock<INotificationService>();
        var mockApptLogger = new Mock<ILogger<AppointmentService>>();
        var apptService = new AppointmentService(db, mockNotification.Object, mockApptLogger.Object);

        var slotStart = DateTime.Today.AddDays(500 + Random.Shared.Next(1, 50)).AddHours(10);
        var slotEnd = slotStart.AddMinutes(45);

        // PART 1: Shared Room (ROOM-101, IsExclusive=false in Dept 1)
        var appt1 = new Appointment
        {
            AppointmentCode = "SC-SHR-" + Guid.NewGuid().ToString("N")[..8],
            PatientUserId = 13,
            DepartmentId = 1,
            BookingSource = "Patient",
            ReasonForVisit = "Khám răng định kỳ",
            Status = AppointmentStatus.PendingDepartmentReview,
            ScheduledStart = slotStart,
            ScheduledEnd = slotEnd,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var appt2 = new Appointment
        {
            AppointmentCode = "SC-SHR-" + Guid.NewGuid().ToString("N")[..8],
            PatientUserId = 14,
            DepartmentId = 1,
            BookingSource = "Patient",
            ReasonForVisit = "Khám răng định kỳ",
            Status = AppointmentStatus.PendingDepartmentReview,
            ScheduledStart = slotStart,
            ScheduledEnd = slotEnd,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.AddRange(appt1, appt2);
        await db.SaveChangesAsync();

        // Manager 10 confirms Appt1 on Chair 1
        var conf1 = await apptService.ManagerConfirmAsync(appt1.AppointmentId, 10, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 3,
            RoomId = 1,
            DentalChairId = 1,
            ScheduledStart = slotStart,
            ScheduledEnd = slotEnd
        });
        Assert.True(conf1.Success, conf1.Message);

        // Manager 10 confirms Appt2 on Chair 2 in the same room (ROOM-101) with Dentist 6 -> BOTH SUCCEED
        var conf2 = await apptService.ManagerConfirmAsync(appt2.AppointmentId, 10, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 6,
            RoomId = 1,
            DentalChairId = 2,
            ScheduledStart = slotStart,
            ScheduledEnd = slotEnd
        });
        Assert.True(conf2.Success, "Shared multi-chair room must allow different chairs concurrently");

        // PART 2: Double-booking an occupied chair is REJECTED
        var appt3 = new Appointment
        {
            AppointmentCode = "SC-SHR-" + Guid.NewGuid().ToString("N")[..8],
            PatientUserId = 15,
            DepartmentId = 1,
            BookingSource = "Patient",
            ReasonForVisit = "Khám răng định kỳ",
            Status = AppointmentStatus.PendingDepartmentReview,
            ScheduledStart = slotStart,
            ScheduledEnd = slotEnd,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt3);
        await db.SaveChangesAsync();

        var conf3 = await apptService.ManagerConfirmAsync(appt3.AppointmentId, 10, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 46,
            RoomId = 1,
            DentalChairId = 1, // Already occupied by Appt1!
            ScheduledStart = slotStart,
            ScheduledEnd = slotEnd
        });
        Assert.False(conf3.Success);
        Assert.Contains("Ghế", conf3.Message);

        // PART 3: Exclusive Room (ROOM-202, IsExclusive=true in Dept 3, Chair 13, Manager 4)
        var slotExStart = slotStart.AddHours(3);
        var slotExEnd = slotExStart.AddMinutes(45);

        var apptEx1 = new Appointment
        {
            AppointmentCode = "SC-EXC-" + Guid.NewGuid().ToString("N")[..8],
            PatientUserId = 13,
            DepartmentId = 3,
            BookingSource = "Patient",
            ReasonForVisit = "Phẫu thuật cấy implant",
            Status = AppointmentStatus.PendingDepartmentReview,
            ScheduledStart = slotExStart,
            ScheduledEnd = slotExEnd,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptEx2 = new Appointment
        {
            AppointmentCode = "SC-EXC-" + Guid.NewGuid().ToString("N")[..8],
            PatientUserId = 14,
            DepartmentId = 3,
            BookingSource = "Patient",
            ReasonForVisit = "Phẫu thuật cấy implant",
            Status = AppointmentStatus.PendingDepartmentReview,
            ScheduledStart = slotExStart,
            ScheduledEnd = slotExEnd,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.AddRange(apptEx1, apptEx2);
        await db.SaveChangesAsync();

        // Confirm ApptEx1 in exclusive room 5 (ROOM-202, Chair 13) by Manager 4
        var confEx1 = await apptService.ManagerConfirmAsync(apptEx1.AppointmentId, 4, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 3,
            RoomId = 5,
            DentalChairId = 13, // Chair in ROOM-202
            ScheduledStart = slotExStart,
            ScheduledEnd = slotExEnd
        });
        Assert.True(confEx1.Success, confEx1.Message);

        // Second confirmation on ROOM-202 must be REJECTED because ROOM-202 is exclusive!
        var confEx2 = await apptService.ManagerConfirmAsync(apptEx2.AppointmentId, 4, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 6,
            RoomId = 5,
            DentalChairId = 13,
            ScheduledStart = slotExStart,
            ScheduledEnd = slotExEnd
        });
        Assert.False(confEx2.Success, "Exclusive room must reject concurrent overlapping appointments");
        Assert.Contains("phòng", confEx2.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task ScenarioD_Concurrency_LockProtectionAndUniqueQueueNumbers_Enforced()
    {
        var slotStart = DateTime.Today.AddDays(600 + Random.Shared.Next(1, 50)).AddHours(8);
        var slotEnd = slotStart.AddMinutes(30);

        // PART 1: Parallel Confirmations for the SAME Dentist
        using (var setupDb = CreateDbContext())
        {
            var a1 = new Appointment
            {
                AppointmentCode = "SD-CONC-" + Guid.NewGuid().ToString("N")[..8],
                PatientUserId = 13,
                DepartmentId = 1,
                BookingSource = "Patient",
                ReasonForVisit = "Khám răng định kỳ",
                Status = AppointmentStatus.PendingDepartmentReview,
                ScheduledStart = slotStart,
                ScheduledEnd = slotEnd,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var a2 = new Appointment
            {
                AppointmentCode = "SD-CONC-" + Guid.NewGuid().ToString("N")[..8],
                PatientUserId = 14,
                DepartmentId = 1,
                BookingSource = "Patient",
                ReasonForVisit = "Khám răng định kỳ",
                Status = AppointmentStatus.PendingDepartmentReview,
                ScheduledStart = slotStart,
                ScheduledEnd = slotEnd,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            setupDb.Appointments.AddRange(a1, a2);
            await setupDb.SaveChangesAsync();

            var task1 = Task.Run(async () =>
            {
                using var db1 = CreateDbContext();
                var s = new AppointmentService(db1, Mock.Of<INotificationService>(), Mock.Of<ILogger<AppointmentService>>());
                return await s.ManagerConfirmAsync(a1.AppointmentId, 10, new ManagerConfirmRequest
                {
                    AssignedDentistUserId = 3,
                    RoomId = 1,
                    DentalChairId = 1,
                    ScheduledStart = slotStart,
                    ScheduledEnd = slotEnd
                });
            });

            var task2 = Task.Run(async () =>
            {
                using var db2 = CreateDbContext();
                var s = new AppointmentService(db2, Mock.Of<INotificationService>(), Mock.Of<ILogger<AppointmentService>>());
                return await s.ManagerConfirmAsync(a2.AppointmentId, 10, new ManagerConfirmRequest
                {
                    AssignedDentistUserId = 3, // SAME DENTIST
                    RoomId = 1,
                    DentalChairId = 2,
                    ScheduledStart = slotStart,
                    ScheduledEnd = slotEnd
                });
            });

            var results = await Task.WhenAll(task1, task2);
            int successes = results.Count(r => r.Success);
            int failures = results.Count(r => !r.Success);

            Assert.Equal(1, successes);
            Assert.Equal(1, failures);
        }

        // PART 2: Concurrent QueueNumber Allocation produces strictly unique queue numbers
        var testQueueDate = DateTime.Today.AddDays(700 + Random.Shared.Next(1, 50)).AddHours(9);
        var qAppts = new List<Appointment>();
        using (var setupDb = CreateDbContext())
        {
            for (int i = 0; i < 4; i++)
            {
                var q = new Appointment
                {
                    AppointmentCode = "SD-QN-" + Guid.NewGuid().ToString("N")[..8],
                    PatientUserId = 13 + (i % 3),
                    DepartmentId = 1,
                    BookingSource = "Patient",
                    ReasonForVisit = "Khám răng định kỳ",
                    Status = AppointmentStatus.PendingDepartmentReview,
                    ScheduledStart = testQueueDate.AddMinutes(i * 35),
                    ScheduledEnd = testQueueDate.AddMinutes((i + 1) * 35),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                qAppts.Add(q);
                setupDb.Appointments.Add(q);
            }
            await setupDb.SaveChangesAsync();
        }

        var qTasks = qAppts.Select((appt, idx) => Task.Run(async () =>
        {
            using var db = CreateDbContext();
            var s = new AppointmentService(db, Mock.Of<INotificationService>(), Mock.Of<ILogger<AppointmentService>>());
            long chair = (idx % 2 == 0) ? 1 : 2;
            long dentist = (idx % 2 == 0) ? 3 : 6;
            return await s.ManagerConfirmAsync(appt.AppointmentId, 10, new ManagerConfirmRequest
            {
                AssignedDentistUserId = dentist,
                RoomId = 1,
                DentalChairId = chair,
                ScheduledStart = appt.ScheduledStart,
                ScheduledEnd = appt.ScheduledEnd
            });
        })).ToList();

        var qResults = await Task.WhenAll(qTasks);
        var successfulQueueNumbers = qResults.Where(r => r.Success).Select(r => r.Data!.QueueNumber).ToList();
        Assert.NotEmpty(successfulQueueNumbers);
        // All issued QueueNumbers must be distinct
        Assert.Equal(successfulQueueNumbers.Count, successfulQueueNumbers.Distinct().Count());
    }

    [Fact]
    public async Task ScenarioE_PrescriptionIntegrity_EnforcesValidationAndTransactions()
    {
        using var db = CreateDbContext();
        var mockLogger = new Mock<ILogger<PrescriptionService>>();
        var prescService = new PrescriptionService(db, mockLogger.Object);

        // Validation 1: Creating prescription without items returns Fail
        var emptyReq = new CreatePrescriptionRequest
        {
            AppointmentId = 1,
            GeneralInstructions = "Khám răng định kỳ",
            Items = new List<CreatePrescriptionItemInput>()
        };
        var emptyResult = await prescService.CreatePrescriptionAsync(3, emptyReq);
        Assert.False(emptyResult.Success);
        Assert.Contains("ít nhất", emptyResult.Message);

        // Validation 2: Creating prescription for non-existent appointment returns Fail
        var nonExistentReq = new CreatePrescriptionRequest
        {
            AppointmentId = 999999,
            GeneralInstructions = "Khám răng",
            Items = new List<CreatePrescriptionItemInput>
            {
                new CreatePrescriptionItemInput
                {
                    MedicationName = "Paracetamol 500mg",
                    Dosage = "500mg",
                    Frequency = "Ngày 2 lần",
                    Duration = "3 ngày"
                }
            }
        };
        var nonExistentResult = await prescService.CreatePrescriptionAsync(3, nonExistentReq);
        Assert.False(nonExistentResult.Success);
        Assert.Contains("thông tin ca khám", nonExistentResult.Message);
    }

    [Fact]
    public async Task ScenarioE_RootCanal_CompleteLifecycle_WithChairAllocation_Discount_AndPaymentSecurity()
    {
        using var db = CreateDbContext();
        var mockNotification = new Mock<INotificationService>();
        var mockApptLogger = new Mock<ILogger<AppointmentService>>();
        var mockBillingLogger = new Mock<ILogger<BillingService>>();
        var mockMedLogger = new Mock<ILogger<MedicalRecordService>>();

        var apptService = new AppointmentService(db, mockNotification.Object, mockApptLogger.Object);
        var billingService = new BillingService(db, mockNotification.Object, mockBillingLogger.Object);
        var medService = new MedicalRecordService(db, mockMedLogger.Object);

        long patientUserId = 13;
        long receptionistUserId = 2;
        long managerUserId = 10; // Manager of Dept 1 (Khoa Răng Tổng Quát & Nội Nha)
        long dentistUserId = 6;  // BS. Nguyễn Thị Phương Thảo (in Dept 1)
        long departmentId = 1;   // General Dentistry & Endodontics
        long serviceId = 11;     // Điều trị tủy răng hàm nhiều chân phức tạp
        long roomId = 1;         // ROOM-101
        long chairId = 2;        // CHAIR-101-B

        var testDate = DateTime.Today.AddDays(3000 + Random.Shared.Next(1, 50000)).AddHours(Random.Shared.Next(8, 16));

        // Step 1: Patient books root canal service in Department 1
        var createReq = new CreateAppointmentRequest
        {
            DepartmentId = departmentId,
            RequestedServiceId = serviceId,
            RequestedDentistUserId = null,
            ScheduledStart = testDate,
            ReasonForVisit = "Đau tủy răng hàm cần điều trị chuyên sâu"
        };
        var createResult = await apptService.CreatePatientAppointmentAsync(patientUserId, createReq);
        Assert.True(createResult.Success, createResult.Message);
        long apptId = createResult.Data!.AppointmentId;
        Assert.Equal(AppointmentStatus.PendingReceptionReview, createResult.Data.Status);

        // Step 2: Receptionist forwards
        var fwdResult = await apptService.ReceptionistForwardAsync(apptId, receptionistUserId, "Ca đau tủy răng, chuyển Trưởng khoa xếp phòng");
        Assert.True(fwdResult.Success, fwdResult.Message);

        // Step 3: Manager allocation rules
        // Rule 3A: Missing chair MUST be rejected
        var missingChairReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = dentistUserId,
            RoomId = roomId,
            DentalChairId = null, // Missing chair!
            ScheduledStart = testDate,
            ScheduledEnd = testDate.AddMinutes(45)
        };
        var missingChairResult = await apptService.ManagerConfirmAsync(apptId, managerUserId, missingChairReq);
        Assert.False(missingChairResult.Success);
        Assert.Contains("ghế nha khoa", missingChairResult.Message.ToLowerInvariant());

        // Rule 3B: Valid Dentist + Room + Chair succeeds
        var validConfirmReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = dentistUserId,
            RoomId = roomId,
            DentalChairId = chairId,
            ScheduledStart = testDate,
            ScheduledEnd = testDate.AddMinutes(45)
        };
        var confirmResult = await apptService.ManagerConfirmAsync(apptId, managerUserId, validConfirmReq);
        Assert.True(confirmResult.Success, confirmResult.Message);
        Assert.Equal(AppointmentStatus.Confirmed, confirmResult.Data!.Status);
        Assert.Equal(chairId, confirmResult.Data.DentalChairId);

        // Step 4: Receptionist Check-in
        var checkInResult = await apptService.ReceptionistCheckInAsync(apptId, receptionistUserId, "Bệnh nhân đã có mặt tại sảnh chờ");
        Assert.True(checkInResult.Success, checkInResult.Message);

        // Step 5: Dentist starts examination
        var startResult = await apptService.DentistStartAppointmentAsync(apptId, dentistUserId);
        Assert.True(startResult.Success, startResult.Message);

        var apptInService = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.InService, apptInService!.Status);
        Assert.Equal(chairId, apptInService.DentalChairId);

        // Step 6: Dentist records ClinicalEntry
        var entryReq = new CreateClinicalEntryRequest
        {
            PatientUserId = patientUserId,
            AppointmentId = apptId,
            Diagnosis = "Viêm tủy cấp không hồi phục răng 46",
            Symptoms = "Đau buốt từng cơn dữ dội lan lên vùng thái dương",
            ClinicalFindings = "Lỗ sâu mặt nhai chạm tủy, gõ dọc (+), tủy sống",
            ClinicalNotes = "Gây tê tại chỗ, đặt đê cao su, mở tủy làm sạch 3 ống tủy",
            CompleteAppointment = false
        };
        var entryResult = await medService.CreateClinicalEntryAsync(dentistUserId, entryReq);
        Assert.True(entryResult.Success, entryResult.Message);

        // Step 7: Integrity check - Creating invoice & processing payment while InService
        var invoiceReq = new CreateInvoiceRequest
        {
            AppointmentId = apptId,
            DiscountAmount = 180000, // 10% manual discount
            TaxPercent = 0,
            Note = "Giảm giá 10% ưu đãi khách hàng",
            Items = new List<CreateInvoiceItemRequest>
            {
                new()
                {
                    Description = "Điều trị tủy răng hàm nhiều chân phức tạp",
                    Quantity = 1,
                    UnitPrice = 1800000
                }
            }
        };
        var invResult = await billingService.CreateInvoiceAsync(receptionistUserId, invoiceReq);
        Assert.True(invResult.Success, invResult.Message);
        long invoiceId = invResult.Data!.InvoiceId;
        Assert.Equal(1620000, invResult.Data.TotalAmount); // 1,800,000 - 180,000 = 1,620,000

        // Duplicate invoice creation MUST be rejected
        var dupInvResult = await billingService.CreateInvoiceAsync(receptionistUserId, invoiceReq);
        Assert.False(dupInvResult.Success);
        Assert.Contains("đã được lập hóa đơn", dupInvResult.Message.ToLowerInvariant());

        // Process payment
        var payReq = new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Method = "BankTransfer",
            Amount = 1620000,
            Note = "Chuyển khoản QR ngân hàng"
        };
        var payResult = await billingService.ProcessPaymentAsync(receptionistUserId, payReq);
        Assert.True(payResult.Success, payResult.Message);
        Assert.Equal("Paid", payResult.Data!.Status);

        // CRITICAL INTEGRITY CHECK: Payment MUST NOT silently complete the examination!
        var apptStillInService = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.InService, apptStillInService!.Status);

        // Step 8: Dentist completes examination
        var completeResult = await apptService.DentistCompleteAppointmentAsync(apptId, dentistUserId, "Hoàn tất điều trị tủy thì 1, hẹn tái khám trám bít ống tủy");
        Assert.True(completeResult.Success, completeResult.Message);

        var finalAppt = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.Completed, finalAppt!.Status);
        Assert.Equal(chairId, finalAppt.DentalChairId);

        // Duplicate payment on already paid invoice MUST be rejected
        var dupPayResult = await billingService.ProcessPaymentAsync(receptionistUserId, payReq);
        Assert.False(dupPayResult.Success);

        // Step 9: Patient views invoice & medical records
        var patientInvoices = await billingService.GetPatientInvoicesAsync(patientUserId);
        Assert.True(patientInvoices.Success);
        Assert.Contains(patientInvoices.Data!, inv => inv.InvoiceId == invoiceId && inv.Status == "Paid");

        var medRecordResult = await medService.GetMedicalRecordByPatientIdAsync(patientUserId);
        Assert.True(medRecordResult.Success);
        Assert.NotNull(medRecordResult.Data);
        Assert.Contains(medRecordResult.Data.ClinicalEntries, ce => ce.AppointmentId == apptId && ce.Diagnosis.Contains("Viêm tủy"));
    }
}
