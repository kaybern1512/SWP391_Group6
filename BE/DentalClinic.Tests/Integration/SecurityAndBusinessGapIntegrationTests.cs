using MedicineEntity = DentalClinic.Infrastructure.Persistence.Entities.Medicine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class SecurityAndBusinessGapIntegrationTests
{
    private readonly Mock<ILogger<AppointmentService>> _mockApptLogger = new();
    private readonly Mock<INotificationService> _mockNotification = new();

    private DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedBaseDataAsync(DentalClinicDbContext db, DateTime testDate)
    {
        // 1. Patients
        var p1User = new UserAccount { UserId = 101, Email = "patient1@dentalcare.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var p1Profile = new PatientProfile { UserId = 101, PatientCode = "PAT-001", FullName = "Bệnh nhân 1", User = p1User };

        var p2User = new UserAccount { UserId = 102, Email = "patient2@dentalcare.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var p2Profile = new PatientProfile { UserId = 102, PatientCode = "PAT-002", FullName = "Bệnh nhân 2", User = p2User };

        // 2. Staff & Receptionist
        var recUser = new UserAccount { UserId = 201, Email = "rec@dentalcare.com", Role = UserRole.Receptionist, Status = AccountStatus.Active };
        var recStaff = new StaffProfile { UserId = 201, EmployeeCode = "EMP-REC", FullName = "Lễ tân Mai", EmploymentStatus = "Active", User = recUser };

        // 3. Department
        var dept = new Department { DepartmentId = 1, Name = "Khoa Răng Tổng Quát", Status = "Active" };

        // 4. Services
        var svc30 = new DepartmentService { DepartmentServiceId = 10, DepartmentId = 1, ServiceName = "Khám răng 30p", DurationMinutes = 30, Price = 100000, Status = "Active" };
        var svc60 = new DepartmentService { DepartmentServiceId = 20, DepartmentId = 1, ServiceName = "Tiểu phẫu 60p", DurationMinutes = 60, Price = 500000, Status = "Active" };

        // 5. Dentists
        var d1User = new UserAccount { UserId = 401, Email = "dentist1@dentalcare.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var d1Staff = new StaffProfile { UserId = 401, EmployeeCode = "EMP-D1", FullName = "BS. Hùng", EmploymentStatus = "Active", User = d1User };
        var d1Dentist = new DentistProfile { UserId = 401, LicenseNumber = "LIC-D1", User = d1Staff };
        var d1Dept = new DentistDepartment { DentistUserId = 401, DepartmentId = 1, Status = "Active" };

        var d2User = new UserAccount { UserId = 402, Email = "dentist2@dentalcare.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var d2Staff = new StaffProfile { UserId = 402, EmployeeCode = "EMP-D2", FullName = "BS. Thảo", EmploymentStatus = "Active", User = d2User };
        var d2Dentist = new DentistProfile { UserId = 402, LicenseNumber = "LIC-D2", User = d2Staff };
        var d2Dept = new DentistDepartment { DentistUserId = 402, DepartmentId = 1, Status = "Active" };

        // 6. Rooms & Chairs
        var room1 = new Room { RoomId = 1, RoomCode = "ROOM-101", RoomName = "Phòng Khám 101", DepartmentId = 1, Status = "Active", IsExclusive = false };
        var chair1 = new DentalChair { DentalChairId = 11, RoomId = 1, ChairCode = "CHAIR-11", Status = "Active" };
        var chair2 = new DentalChair { DentalChairId = 12, RoomId = 1, ChairCode = "CHAIR-12", Status = "Active" };

        // 7. Work Schedules: Doctor 401 and 402 work 08:00 - 12:00 on testDate
        var dateOnly = DateOnly.FromDateTime(testDate);
        var sched1 = new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 401,
            DepartmentId = 1,
            WorkDate = dateOnly,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        };

        var sched2 = new DentistWorkSchedule
        {
            DentistScheduleId = 2,
            DentistUserId = 402,
            DepartmentId = 1,
            WorkDate = dateOnly,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        };

        db.UserAccounts.AddRange(p1User, p2User, recUser, d1User, d2User);
        db.PatientProfiles.AddRange(p1Profile, p2Profile);
        db.StaffProfiles.AddRange(recStaff, d1Staff, d2Staff);
        db.DentistProfiles.AddRange(d1Dentist, d2Dentist);
        db.DentistDepartments.AddRange(d1Dept, d2Dept);
        db.Departments.Add(dept);
        db.DepartmentServices.AddRange(svc30, svc60);
        db.Rooms.Add(room1);
        db.DentalChairs.AddRange(chair1, chair2);
        db.DentistWorkSchedules.AddRange(sched1, sched2);

        await db.SaveChangesAsync();
    }

    // =========================================================================
    // P0: WALK-IN BOOKING BYPASS FIX TESTS
    // =========================================================================

    [Fact]
    public async Task P0_WalkIn_AutoConfirm_ValidResources_ConfirmedAndAllocatesQueueNumber()
    {
        var testDate = DateTime.Today.AddDays(2).AddHours(9);
        using var db = CreateDbContext(nameof(P0_WalkIn_AutoConfirm_ValidResources_ConfirmedAndAllocatesQueueNumber));
        await SeedBaseDataAsync(db, testDate);

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var request = new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = testDate,
            ReasonForVisit = "Walk-in khám tổng quát"
        };

        var result = await service.CreateReceptionistAppointmentAsync(201, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(AppointmentStatus.Confirmed, result.Data.Status);
        Assert.Equal("Q001", result.Data.QueueNumber);
        Assert.NotNull(result.Data.ConfirmedAt);
        Assert.Equal(401, result.Data.AssignedDentistUserId);
        Assert.Equal(11, result.Data.DentalChairId);
    }

    [Fact]
    public async Task P0_WalkIn_SameDoctorAndOverlappingTime_SecondRequestRejected()
    {
        var testDate = DateTime.Today.AddDays(2).AddHours(9);
        using var db = CreateDbContext(nameof(P0_WalkIn_SameDoctorAndOverlappingTime_SecondRequestRejected));
        await SeedBaseDataAsync(db, testDate);

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);

        // First walk-in: Patient 1 with Doctor 401 on Chair 11 at 09:00 - 09:30
        var req1 = new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = testDate,
            ReasonForVisit = "Walk-in 1"
        };
        var res1 = await service.CreateReceptionistAppointmentAsync(201, req1);
        Assert.True(res1.Success);

        // Second walk-in: Patient 2 with same Doctor 401 on Chair 12 at 09:15 (overlapping)
        var req2 = new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 102,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 12,
            ScheduledStart = testDate.AddMinutes(15),
            ReasonForVisit = "Walk-in 2 trùng bác sĩ"
        };
        var res2 = await service.CreateReceptionistAppointmentAsync(201, req2);

        Assert.False(res2.Success);
        Assert.Contains("Bác sĩ đã có lịch hẹn khác", res2.Message);
    }

    [Fact]
    public async Task P0_WalkIn_SameChairAndOverlappingTime_SecondRequestRejected()
    {
        var testDate = DateTime.Today.AddDays(2).AddHours(9);
        using var db = CreateDbContext(nameof(P0_WalkIn_SameChairAndOverlappingTime_SecondRequestRejected));
        await SeedBaseDataAsync(db, testDate);

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);

        // First walk-in: Doctor 401 on Chair 11 at 09:00 - 09:30
        var req1 = new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = testDate
        };
        var res1 = await service.CreateReceptionistAppointmentAsync(201, req1);
        Assert.True(res1.Success);

        // Second walk-in: Doctor 402 on SAME Chair 11 at 09:00 - 09:30
        var req2 = new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 102,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 402,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = testDate
        };
        var res2 = await service.CreateReceptionistAppointmentAsync(201, req2);

        Assert.False(res2.Success);
        Assert.Contains("Ghế nha khoa đã bị trùng lịch", res2.Message);
    }

    [Fact]
    public async Task P0_WalkIn_DoctorHasNoShift_RejectedWithClearError()
    {
        // Date with no schedule for Doctor 401 (e.g. 5 days later)
        var noShiftDate = DateTime.Today.AddDays(5).AddHours(9);
        using var db = CreateDbContext(nameof(P0_WalkIn_DoctorHasNoShift_RejectedWithClearError));
        await SeedBaseDataAsync(db, DateTime.Today.AddDays(2).AddHours(9));

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var req = new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = noShiftDate
        };

        var res = await service.CreateReceptionistAppointmentAsync(201, req);

        Assert.False(res.Success);
        Assert.Contains("Bác sĩ không có ca làm việc", res.Message);
    }

    [Fact]
    public async Task P0_WalkIn_DoctorOnLeave_RejectedWithClearError()
    {
        var testDate = DateTime.Today.AddDays(2).AddHours(9);
        using var db = CreateDbContext(nameof(P0_WalkIn_DoctorOnLeave_RejectedWithClearError));
        await SeedBaseDataAsync(db, testDate);

        // Add Doctor Leave for 401
        db.DentistAvailabilities.Add(new DentistAvailability
        {
            DentistUserId = 401,
            StartDateTime = testDate.AddMinutes(-30),
            EndDateTime = testDate.AddHours(2),
            AvailabilityStatus = DentistAvailabilityStatus.OnLeave,
            Reason = "Nghỉ phép đột xuất"
        });
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var req = new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = testDate
        };

        var res = await service.CreateReceptionistAppointmentAsync(201, req);

        Assert.False(res.Success);
        Assert.Contains("Bác sĩ đang bận hoặc nghỉ phép", res.Message);
    }

    [Fact]
    public async Task P0_WalkIn_ServiceDurationExceedsDoctorShift_Rejected()
    {
        // Doctor shift ends at 12:00. Service 60p starting at 11:30 would end at 12:30 (exceeding shift)
        var testDate = DateTime.Today.AddDays(2).AddHours(11).AddMinutes(30);
        using var db = CreateDbContext(nameof(P0_WalkIn_ServiceDurationExceedsDoctorShift_Rejected));
        await SeedBaseDataAsync(db, testDate);

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var req = new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 20, // 60 minutes duration
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = testDate
        };

        var res = await service.CreateReceptionistAppointmentAsync(201, req);

        Assert.False(res.Success);
        Assert.Contains("khung giờ làm việc", res.Message);
    }

    [Fact]
    public async Task P0_WalkIn_MissingResourcesWhenAutoConfirm_ReturnsFailure()
    {
        var testDate = DateTime.Today.AddDays(2).AddHours(9);
        using var db = CreateDbContext(nameof(P0_WalkIn_MissingResourcesWhenAutoConfirm_ReturnsFailure));
        await SeedBaseDataAsync(db, testDate);

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var req = new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AutoConfirm = true, // AutoConfirm requested but missing Chair and Room
            AssignedDentistUserId = 401,
            RoomId = null,
            DentalChairId = null,
            ScheduledStart = testDate
        };

        var res = await service.CreateReceptionistAppointmentAsync(201, req);

        Assert.False(res.Success);
        Assert.Contains("Vui lòng phân bổ đầy đủ Bác sĩ, Phòng khám và Ghế nha khoa", res.Message);
    }

    // =========================================================================
    // P1: MEDICINE DISPENSATION CUMULATIVE LIMIT TESTS
    // =========================================================================

    private async Task<(Prescription pres, MedicineEntity med, MedicineBatch batch)> SeedMedicineFixtureAsync(DentalClinicDbContext db, int prescribedUnits = 10)
    {
        var patientUser = new UserAccount { UserId = 101, Email = "pat@care.com", Role = UserRole.Patient, Status = AccountStatus.Active };

        var patientProfile = new PatientProfile { UserId = 101, PatientCode = "PAT-GAP", FullName = "Bệnh nhân Gap", User = patientUser };

        var dentistUser = new UserAccount { UserId = 401, Email = "den@care.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var dentistStaff = new StaffProfile { UserId = 401, EmployeeCode = "EMP-GAP", FullName = "BS. Gap", EmploymentStatus = "Active", User = dentistUser };
        var dentistProfile = new DentistProfile { UserId = 401, LicenseNumber = "LIC-GAP", User = dentistStaff };

        var dept = new Department { DepartmentId = 1, Name = "RHM", Status = "Active" };

        var appt = new Appointment
        {
            AppointmentId = 501,
            AppointmentCode = "APPT-GAP-01",
            PatientUserId = 101,
            DepartmentId = 1,
            Status = AppointmentStatus.InService,
            AssignedDentistUserId = 401,
            ScheduledStart = DateTime.UtcNow,
            ScheduledEnd = DateTime.UtcNow.AddMinutes(30),
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Khám răng",
            CreatedAt = DateTime.UtcNow
        };

        var session = new TreatmentSession
        {
            TreatmentSessionId = 601,
            AppointmentId = 501,
            DentistUserId = 401,
            SessionDate = DateTime.UtcNow,
            TreatmentResult = "Thực hiện thủ thuật"
        };

        var pres = new Prescription
        {
            PrescriptionId = 701,
            TreatmentSessionId = 601,
            GeneralInstructions = "Uống sau ăn",
            IssuedAt = DateTime.UtcNow,
            TreatmentSession = session
        };

        var presItem = new PrescriptionItem
        {
            PrescriptionItemId = 801,
            PrescriptionId = 701,
            MedicationName = "Amoxicillin 500mg",
            Dosage = "1 viên x 2 lần/ngày",
            Frequency = "2 lần/ngày",
            Duration = "5 ngày",
            Instructions = $"Kê đơn: {prescribedUnits} viên",
            PrescribedQuantity = prescribedUnits
        };

        var med = new MedicineEntity
        {
            MedicineId = 901,
            MedicineCode = "MED-AMX-500",
            MedicineName = "Amoxicillin 500mg",
            UnitPrice = 5000,
            Unit = "Viên",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        var batch = new MedicineBatch
        {
            MedicineBatchId = 1001,
            MedicineId = 901,
            BatchNumber = "AMX-BATCH-01",
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            QuantityReceived = 100,
            QuantityAvailable = 100,
            CostPrice = 3000,
            Status = "Available",
            ReceivedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Medicine = med
        };

        appt.PatientUser = patientProfile;
        session.Appointment = appt;
        pres.TreatmentSession = session;
        pres.PrescriptionItems.Add(presItem);
        presItem.Prescription = pres;

        db.UserAccounts.AddRange(patientUser, dentistUser);
        db.PatientProfiles.Add(patientProfile);
        db.StaffProfiles.Add(dentistStaff);
        db.DentistProfiles.Add(dentistProfile);
        db.Departments.Add(dept);
        db.Appointments.Add(appt);
        db.TreatmentSessions.Add(session);
        db.Prescriptions.Add(pres);
        db.PrescriptionItems.Add(presItem);
        db.Medicines.Add(med);
        db.MedicineBatches.Add(batch);

        await db.SaveChangesAsync();

        return (pres, med, batch);
    }

    [Fact]
    public async Task P1_DispenseMedicine_ExactPrescribedQuantity_Succeeds()
    {
        using var db = CreateDbContext(nameof(P1_DispenseMedicine_ExactPrescribedQuantity_Succeeds));
        var (pres, med, batch) = await SeedMedicineFixtureAsync(db, prescribedUnits: 10);
        var service = new MedicineService(db);

        var req = new CreateMedicineDispensationRequest
        {
            PrescriptionId = pres.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    PrescriptionItemId = 801,
                    MedicineId = med.MedicineId,
                    MedicineBatchId = batch.MedicineBatchId,
                    Quantity = 10
                }
            }
        };

        var result = await service.DispenseMedicineAsync(req, 401);

        Assert.NotNull(result);
        Assert.Equal("Dispensed", result.Status);
        Assert.Equal(10, result.Items.Sum(i => i.QuantityDispensed));

        var updatedBatch = await db.MedicineBatches.FindAsync(batch.MedicineBatchId);
        Assert.Equal(90, updatedBatch!.QuantityAvailable);
    }

    [Fact]
    public async Task P1_DispenseMedicine_PartialThenRemainder_Succeeds()
    {
        using var db = CreateDbContext(nameof(P1_DispenseMedicine_PartialThenRemainder_Succeeds));
        var (pres, med, batch) = await SeedMedicineFixtureAsync(db, prescribedUnits: 10);
        var service = new MedicineService(db);

        // Step 1: Dispense 6 units (partial)
        var req1 = new CreateMedicineDispensationRequest
        {
            PrescriptionId = pres.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    PrescriptionItemId = 801,
                    MedicineId = med.MedicineId,
                    MedicineBatchId = batch.MedicineBatchId,
                    Quantity = 6
                }
            }
        };
        var res1 = await service.DispenseMedicineAsync(req1, 401);
        Assert.NotNull(res1);

        // Step 2: Dispense remaining 4 units
        var req2 = new CreateMedicineDispensationRequest
        {
            PrescriptionId = pres.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    PrescriptionItemId = 801,
                    MedicineId = med.MedicineId,
                    MedicineBatchId = batch.MedicineBatchId,
                    Quantity = 4
                }
            }
        };
        var res2 = await service.DispenseMedicineAsync(req2, 401);
        Assert.NotNull(res2);

        // Total stock deducted = 10
        var updatedBatch = await db.MedicineBatches.FindAsync(batch.MedicineBatchId);
        Assert.Equal(90, updatedBatch!.QuantityAvailable);
    }

    [Fact]
    public async Task P1_DispenseMedicine_ExceedingPrescribedQuantity_ThrowsInvalidOperationException()
    {
        using var db = CreateDbContext(nameof(P1_DispenseMedicine_ExceedingPrescribedQuantity_ThrowsInvalidOperationException));
        var (pres, med, batch) = await SeedMedicineFixtureAsync(db, prescribedUnits: 10);
        var service = new MedicineService(db);

        // Try dispensing 12 units when only 10 prescribed
        var req = new CreateMedicineDispensationRequest
        {
            PrescriptionId = pres.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    PrescriptionItemId = 801,
                    MedicineId = med.MedicineId,
                    MedicineBatchId = batch.MedicineBatchId,
                    Quantity = 12
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DispenseMedicineAsync(req, 401));
        Assert.Contains("vượt quá số lượng kê đơn", ex.Message);

        // Stock must not change
        var updatedBatch = await db.MedicineBatches.FindAsync(batch.MedicineBatchId);
        Assert.Equal(100, updatedBatch!.QuantityAvailable);
    }

    [Fact]
    public async Task P1_DispenseMedicine_SecondPartialExceedsLimit_ThrowsInvalidOperationException()
    {
        using var db = CreateDbContext(nameof(P1_DispenseMedicine_SecondPartialExceedsLimit_ThrowsInvalidOperationException));
        var (pres, med, batch) = await SeedMedicineFixtureAsync(db, prescribedUnits: 10);
        var service = new MedicineService(db);

        // First partial: 6 units (OK)
        var req1 = new CreateMedicineDispensationRequest
        {
            PrescriptionId = pres.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new() { PrescriptionItemId = 801, MedicineId = med.MedicineId, MedicineBatchId = batch.MedicineBatchId, Quantity = 6 }
            }
        };
        await service.DispenseMedicineAsync(req1, 401);

        // Second partial: 5 units (6 + 5 = 11 > 10, must fail)
        var req2 = new CreateMedicineDispensationRequest
        {
            PrescriptionId = pres.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new() { PrescriptionItemId = 801, MedicineId = med.MedicineId, MedicineBatchId = batch.MedicineBatchId, Quantity = 5 }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DispenseMedicineAsync(req2, 401));
        Assert.Contains("đã được cấp 6/10", ex.Message);
        Assert.Contains("vượt quá số lượng kê đơn", ex.Message);

        // Only first 6 units deducted: 100 - 6 = 94
        var updatedBatch = await db.MedicineBatches.FindAsync(batch.MedicineBatchId);
        Assert.Equal(94, updatedBatch!.QuantityAvailable);
    }

    // =========================================================================
    // P2: FOLLOW-UP STATUS SYNCHRONIZATION TESTS
    // =========================================================================

    [Fact]
    public async Task P2_FollowUp_AppointmentConfirmed_SyncsFollowUpToConfirmed()
    {
        var testDate = DateTime.Today.AddDays(3).AddHours(10);
        using var db = CreateDbContext(nameof(P2_FollowUp_AppointmentConfirmed_SyncsFollowUpToConfirmed));
        await SeedBaseDataAsync(db, testDate);

        // Create an appointment in PendingReceptionReview linked to FollowUp
        var appt = new Appointment
        {
            AppointmentId = 888,
            AppointmentCode = "APP-FU-01",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = testDate,
            ScheduledEnd = testDate.AddMinutes(30),
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Khám răng",
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 999,
            TreatmentSessionId = 1,
            AppointmentId = 888,
            RecommendedDate = testDate,
            Reason = "Tái khám sau trám răng",
            Status = "Pending"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var confirmReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = testDate
        };

        var res = await service.ManagerConfirmAsync(888, 201, confirmReq);
        Assert.True(res.Success);

        // FollowUpSchedule must be synced to Confirmed
        var updatedFollowUp = await db.FollowUpSchedules.FindAsync(999L);
        Assert.NotNull(updatedFollowUp);
        Assert.Equal("Scheduled", updatedFollowUp!.Status);
    }

    [Fact]
    public async Task P2_FollowUp_AppointmentCancelled_SyncsFollowUpToCancelled()
    {
        var testDate = DateTime.Today.AddDays(3).AddHours(10);
        using var db = CreateDbContext(nameof(P2_FollowUp_AppointmentCancelled_SyncsFollowUpToCancelled));
        await SeedBaseDataAsync(db, testDate);

        var appt = new Appointment
        {
            AppointmentId = 889,
            AppointmentCode = "APP-FU-02",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = testDate,
            ScheduledEnd = testDate.AddMinutes(30),
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Khám răng",
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 998,
            TreatmentSessionId = 1,
            AppointmentId = 889,
            RecommendedDate = testDate,
            Reason = "Tái khám chỉnh nha",
            Status = "Confirmed"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        

        var res = await service.StaffCancelAppointmentAsync(889, 201, "Bệnh nhân bận công tác đột xuất");
        Assert.True(res.Success);

        var updatedFollowUp = await db.FollowUpSchedules.FindAsync(998L);
        Assert.NotNull(updatedFollowUp);
        Assert.Equal("Cancelled", updatedFollowUp!.Status);
    }

    [Fact]
    public async Task P2_FollowUp_AppointmentCompleted_SyncsFollowUpToCompleted()
    {
        var testDate = DateTime.Today.AddDays(2).AddHours(10);
        using var db = CreateDbContext(nameof(P2_FollowUp_AppointmentCompleted_SyncsFollowUpToCompleted));
        await SeedBaseDataAsync(db, testDate);

        var appt = new Appointment
        {
            AppointmentId = 890,
            AppointmentCode = "APP-FU-03",
            PatientUserId = 101,
            DepartmentId = 1,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            RequestedServiceId = 10,
            ScheduledStart = testDate,
            ScheduledEnd = testDate.AddMinutes(30),
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Khám răng",
            Status = AppointmentStatus.InService, // Must be InService to complete
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 997,
            TreatmentSessionId = 1,
            AppointmentId = 890,
            RecommendedDate = testDate,
            Reason = "Tái khám theo dõi",
            Status = "Confirmed"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var res = await service.DentistCompleteAppointmentAsync(890, 401, "Khám hoàn tất tốt đẹp");
        Assert.True(res.Success);

        var updatedFollowUp = await db.FollowUpSchedules.FindAsync(997L);
        Assert.NotNull(updatedFollowUp);
        Assert.Equal("Completed", updatedFollowUp!.Status);
    }

    [Fact]
    public async Task P2_FollowUp_AppointmentRescheduled_UpdatesFollowUpToNewAppointment()
    {
        var oldDate = DateTime.Today.AddDays(2).AddHours(10);
        var newDate = DateTime.Today.AddDays(3).AddHours(10);
        using var db = CreateDbContext(nameof(P2_FollowUp_AppointmentRescheduled_UpdatesFollowUpToNewAppointment));
        await SeedBaseDataAsync(db, oldDate);
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 3,
            DentistUserId = 401,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(newDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var appt = new Appointment
        {
            AppointmentId = 891,
            AppointmentCode = "APP-FU-04",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = oldDate,
            ScheduledEnd = oldDate.AddMinutes(30),
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Khám răng",
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 996,
            TreatmentSessionId = 1,
            AppointmentId = 891,
            RecommendedDate = oldDate,
            Reason = "Tái khám",
            Status = "Confirmed"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var reschedReq = new PatientRescheduleRequest
        {
            NewScheduledStart = newDate,
            Reason = "Xin dời ngày khám"
        };

        var res = await service.PatientRescheduleAsync(891, 101, reschedReq);
        Assert.True(res.Success);
        Assert.NotNull(res.Data);

        var updatedFollowUp = await db.FollowUpSchedules.FindAsync(996L);
        Assert.NotNull(updatedFollowUp);
        Assert.Equal(res.Data.AppointmentId, updatedFollowUp!.AppointmentId);
        Assert.Equal("Scheduled", updatedFollowUp.Status);
    }
}
