using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.ClinicalDiagnostics;

public class ImagingRoomAndQueueTests
{
    private readonly Mock<ILogger<ImagingService>> _mockLogger;

    public ImagingRoomAndQueueTests()
    {
        _mockLogger = new Mock<ILogger<ImagingService>>();
    }

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedBaseDataAsync(DentalClinicDbContext db)
    {
        // 1. Users
        var dentistUser = new UserAccount { UserId = 10, Email = "dentist@dentalcare.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var patientUser = new UserAccount { UserId = 20, Email = "patient@dentalcare.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var adminUser = new UserAccount { UserId = 1, Email = "admin@dentalcare.com", Role = UserRole.SystemAdministrator, Status = AccountStatus.Active };
        var receptionistUser = new UserAccount { UserId = 5, Email = "receptionist@dentalcare.com", Role = UserRole.Receptionist, Status = AccountStatus.Active };
        db.UserAccounts.AddRange(dentistUser, patientUser, adminUser, receptionistUser);

        // 2. Profiles
        var staffDentist = new StaffProfile { UserId = 10, EmployeeCode = "EMP-010", FullName = "Bác Sĩ Nguyễn Văn A", EmploymentStatus = "Active" };
        var dentistProfile = new DentistProfile { UserId = 10, LicenseNumber = "BS-100", Qualification = "Bác sĩ CKI" };
        var patientProfile = new PatientProfile { UserId = 20, PatientCode = "BN-020", FullName = "Trần Thị B" };
        db.StaffProfiles.Add(staffDentist);
        db.DentistProfiles.Add(dentistProfile);
        db.PatientProfiles.Add(patientProfile);

        // 3. Room & Equipment
        var xrayRoom = new Room
        {
            RoomId = 100,
            RoomCode = "XRAY-01",
            RoomName = "Phòng Chụp X-quang 01",
            RoomType = ImagingRoomType.DiagnosticImaging,
            Status = "Active",
            IsExclusive = true,
            MaxConcurrentPatients = 1,
            MaxWaitingPatients = 3,
            TargetMaxWaitMinutes = 20,
            EscalationWaitMinutes = 30
        };
        db.Rooms.Add(xrayRoom);

        var machine = new ImagingEquipment
        {
            EquipmentId = 200,
            RoomId = 100,
            EquipmentCode = "XRAY-MACH-01",
            EquipmentName = "Carestream CS 8200 3D",
            EquipmentType = EquipmentType.MultiPurpose,
            SupportedDiagnosticTypes = "Intraoral,Panorama,CBCT",
            IsActive = true,
            OperationalStatus = EquipmentOperationalStatus.Operational,
            AvailableFrom = new TimeSpan(7, 0, 0),
            AvailableTo = new TimeSpan(21, 0, 0)
        };
        db.ImagingEquipments.Add(machine);

        // 4. Configs
        db.ImagingServiceConfigs.AddRange(
            new ImagingServiceConfig { ConfigId = 1, DiagnosticType = ImagingDiagnosticType.Intraoral, ServiceName = "Chụp Cận Chóp", SlotDurationMinutes = 10, BufferMinutes = 0, Price = 80000m, IsActive = true },
            new ImagingServiceConfig { ConfigId = 2, DiagnosticType = ImagingDiagnosticType.Panorama, ServiceName = "Chụp Toàn Cảnh", SlotDurationMinutes = 15, BufferMinutes = 0, Price = 200000m, IsActive = true },
            new ImagingServiceConfig { ConfigId = 3, DiagnosticType = ImagingDiagnosticType.CBCT, ServiceName = "Chụp Cắt Lớp 3D", SlotDurationMinutes = 30, BufferMinutes = 0, Price = 500000m, IsActive = true }
        );

        // 5. Appointment
        var appt = new Appointment
        {
            AppointmentId = 500,
            AppointmentCode = "APT-500",
            BookingSource = "WalkIn",
            ReasonForVisit = "Khám răng định kỳ",
            PatientUserId = 20,
            RequestedDentistUserId = 10,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(9),
            ScheduledEnd = DateTime.UtcNow.Date.AddHours(9).AddMinutes(45),
            Status = AppointmentStatus.InService
        };
        db.Appointments.Add(appt);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task TC01_CreateDiagnosticOrder_WithValidData_Succeeds()
    {
        var db = CreateDbContext(nameof(TC01_CreateDiagnosticOrder_WithValidData_Succeeds));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var req = new CreateDiagnosticOrderRequest
        {
            AppointmentId = 500,
            DiagnosticType = "Intraoral",
            ClinicalIndication = "Nghi ngờ sâu răng mặt bên R16",
            IsExternal = false
        };

        var result = await service.CreateDiagnosticOrderAsync(req, 10);

        Assert.NotNull(result);
        Assert.Equal(500, result.AppointmentId);
        Assert.Equal("Intraoral", result.DiagnosticType);
        Assert.Equal(DiagnosticOrderStatus.Ordered, result.Status);
        Assert.False(result.IsExternal);
    }

    [Fact]
    public async Task TC02_CreateDiagnosticOrder_InvalidDiagnosticType_ThrowsException()
    {
        var db = CreateDbContext(nameof(TC02_CreateDiagnosticOrder_InvalidDiagnosticType_ThrowsException));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var req = new CreateDiagnosticOrderRequest
        {
            AppointmentId = 500,
            DiagnosticType = "InvalidTypeXYZ",
            ClinicalIndication = "Kiểm tra bất thường",
            IsExternal = false
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDiagnosticOrderAsync(req, 10));
    }

    [Fact]
    public async Task TC03_CreateDiagnosticOrder_EmptyIndication_ThrowsException()
    {
        var db = CreateDbContext(nameof(TC03_CreateDiagnosticOrder_EmptyIndication_ThrowsException));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var req = new CreateDiagnosticOrderRequest
        {
            AppointmentId = 500,
            DiagnosticType = "Panorama",
            ClinicalIndication = "   ",
            IsExternal = false
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDiagnosticOrderAsync(req, 10));
    }

    [Fact]
    public async Task TC04_CreateDiagnosticOrder_ExternalWithoutFacilityName_ThrowsException()
    {
        var db = CreateDbContext(nameof(TC04_CreateDiagnosticOrder_ExternalWithoutFacilityName_ThrowsException));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var req = new CreateDiagnosticOrderRequest
        {
            AppointmentId = 500,
            DiagnosticType = "CBCT",
            ClinicalIndication = "Khảo sát vị trí răng khôn ngầm sát dây thần kinh",
            IsExternal = true,
            ExternalFacilityName = null
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDiagnosticOrderAsync(req, 10));
    }

    [Fact]
    public async Task TC05_CreateDiagnosticOrder_ExternalWithFacilityName_Succeeds()
    {
        var db = CreateDbContext(nameof(TC05_CreateDiagnosticOrder_ExternalWithFacilityName_Succeeds));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var req = new CreateDiagnosticOrderRequest
        {
            AppointmentId = 500,
            DiagnosticType = "CBCT",
            ClinicalIndication = "Khảo sát CBCT 3D",
            IsExternal = true,
            ExternalFacilityName = "Bệnh Viện Răng Hàm Mặt Trung Ương"
        };

        var result = await service.CreateDiagnosticOrderAsync(req, 10);

        Assert.NotNull(result);
        Assert.True(result.IsExternal);
        Assert.Equal("Bệnh Viện Răng Hàm Mặt Trung Ương", result.ExternalFacilityName);
    }

    [Fact]
    public async Task TC06_CreateDiagnosticOrder_CancelledAppointment_ThrowsException()
    {
        var db = CreateDbContext(nameof(TC06_CreateDiagnosticOrder_CancelledAppointment_ThrowsException));
        await SeedBaseDataAsync(db);
        var appt = await db.Appointments.FirstAsync(a => a.AppointmentId == 500);
        appt.Status = AppointmentStatus.Cancelled;
        await db.SaveChangesAsync();

        var service = new ImagingService(db, _mockLogger.Object);
        var req = new CreateDiagnosticOrderRequest
        {
            AppointmentId = 500,
            DiagnosticType = "Intraoral",
            ClinicalIndication = "Chụp răng sâu"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDiagnosticOrderAsync(req, 10));
    }

    [Fact]
    public async Task TC07_GetDiagnosticOrdersByAppointment_ReturnsOrders()
    {
        var db = CreateDbContext(nameof(TC07_GetDiagnosticOrdersByAppointment_ReturnsOrders));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "Chụp R11" }, 10);
        await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Panorama", ClinicalIndication = "Chụp tổng quát" }, 10);

        var orders = await service.GetDiagnosticOrdersByAppointmentAsync(500);

        Assert.Equal(2, orders.Count);
    }

    [Fact]
    public async Task TC08_GetAvailableSlots_WhenEquipmentOperational_GeneratesSlotsBasedOnConfigDuration()
    {
        var db = CreateDbContext(nameof(TC08_GetAvailableSlots_WhenEquipmentOperational_GeneratesSlotsBasedOnConfigDuration));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var today = DateTime.UtcNow.Date;
        var slots = await service.GetAvailableSlotsAsync(200, today, "Intraoral");

        Assert.NotEmpty(slots);
        // Intraoral duration is 10 min
        Assert.Equal(10, (slots[0].EndUtc - slots[0].StartUtc).TotalMinutes);
        Assert.True(slots.All(s => s.IsAvailable));
    }

    [Fact]
    public async Task TC09_GetAvailableSlots_WhenEquipmentInMaintenance_ReturnsEmptyList()
    {
        var db = CreateDbContext(nameof(TC09_GetAvailableSlots_WhenEquipmentInMaintenance_ReturnsEmptyList));
        await SeedBaseDataAsync(db);
        var eq = await db.ImagingEquipments.FirstAsync(e => e.EquipmentId == 200);
        eq.OperationalStatus = EquipmentOperationalStatus.Maintenance;
        await db.SaveChangesAsync();

        var service = new ImagingService(db, _mockLogger.Object);
        var slots = await service.GetAvailableSlotsAsync(200, DateTime.UtcNow.Date, "Intraoral");

        Assert.Empty(slots);
    }

    [Fact]
    public async Task TC10_GetAvailableSlots_WhenEquipmentOutOfService_ReturnsEmptyList()
    {
        var db = CreateDbContext(nameof(TC10_GetAvailableSlots_WhenEquipmentOutOfService_ReturnsEmptyList));
        await SeedBaseDataAsync(db);
        var eq = await db.ImagingEquipments.FirstAsync(e => e.EquipmentId == 200);
        eq.OperationalStatus = EquipmentOperationalStatus.OutOfService;
        await db.SaveChangesAsync();

        var service = new ImagingService(db, _mockLogger.Object);
        var slots = await service.GetAvailableSlotsAsync(200, DateTime.UtcNow.Date, "Intraoral");

        Assert.Empty(slots);
    }

    [Fact]
    public async Task TC11_GetAvailableSlots_FiltersOutBookedIntervals()
    {
        var db = CreateDbContext(nameof(TC11_GetAvailableSlots_FiltersOutBookedIntervals));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var today = DateTime.UtcNow.Date;
        var startTime = today.AddHours(8);
        var endTime = startTime.AddMinutes(15);

        var order = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Panorama", ClinicalIndication = "Khảo sát hàm" }, 10);
        await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = order.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = startTime }, 5);

        var slots = await service.GetAvailableSlotsAsync(200, today, "Panorama");
        var conflictingSlot = slots.FirstOrDefault(s => s.StartUtc == startTime);

        Assert.NotNull(conflictingSlot);
        Assert.False(conflictingSlot.IsAvailable);
        Assert.NotNull(conflictingSlot.ConflictReason);
    }

    [Fact]
    public async Task TC12_BookImagingSession_WithValidSlot_CreatesSessionAndMarksOrderScheduled()
    {
        var db = CreateDbContext(nameof(TC12_BookImagingSession_WithValidSlot_CreatesSessionAndMarksOrderScheduled));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var order = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "Răng 21" }, 10);

        var startTime = DateTime.UtcNow.Date.AddHours(10);
        var session = await service.BookImagingSessionAsync(new BookImagingSessionRequest
        {
            DiagnosticOrderId = order.DiagnosticOrderId,
            EquipmentId = 200,
            ScheduledStartUtc = startTime,
            Priority = "Normal"
        }, 5);

        Assert.NotNull(session);
        Assert.Equal(ImagingSessionStatus.Scheduled, session.Status);
        Assert.Equal(startTime, session.ScheduledStartUtc);
        Assert.Equal(10, session.SlotDurationMinutes);

        var refreshedOrder = await service.GetDiagnosticOrderByIdAsync(order.DiagnosticOrderId);
        Assert.Equal(DiagnosticOrderStatus.Scheduled, refreshedOrder!.Status);
    }

    [Fact]
    public async Task TC13_BookImagingSession_WhenEquipmentMaintenance_ThrowsException()
    {
        var db = CreateDbContext(nameof(TC13_BookImagingSession_WhenEquipmentMaintenance_ThrowsException));
        await SeedBaseDataAsync(db);
        var eq = await db.ImagingEquipments.FirstAsync(e => e.EquipmentId == 200);
        eq.OperationalStatus = EquipmentOperationalStatus.Maintenance;
        await db.SaveChangesAsync();

        var service = new ImagingService(db, _mockLogger.Object);
        var order = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "Răng 21" }, 10);

        var req = new BookImagingSessionRequest
        {
            DiagnosticOrderId = order.DiagnosticOrderId,
            EquipmentId = 200,
            ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BookImagingSessionAsync(req, 5));
    }

    [Fact]
    public async Task TC14_BookImagingSession_WhenUnsupportedDiagnosticType_ThrowsException()
    {
        var db = CreateDbContext(nameof(TC14_BookImagingSession_WhenUnsupportedDiagnosticType_ThrowsException));
        await SeedBaseDataAsync(db);
        var eq = await db.ImagingEquipments.FirstAsync(e => e.EquipmentId == 200);
        eq.SupportedDiagnosticTypes = "Intraoral"; // Chỉ hỗ trợ Intraoral, không hỗ trợ CBCT
        await db.SaveChangesAsync();

        var service = new ImagingService(db, _mockLogger.Object);
        var order = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "CBCT", ClinicalIndication = "CBCT" }, 10);

        var req = new BookImagingSessionRequest
        {
            DiagnosticOrderId = order.DiagnosticOrderId,
            EquipmentId = 200,
            ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BookImagingSessionAsync(req, 5));
    }

    [Fact]
    public async Task TC15_BookImagingSession_WhenOrderIsExternal_ThrowsException()
    {
        var db = CreateDbContext(nameof(TC15_BookImagingSession_WhenOrderIsExternal_ThrowsException));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var order = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest
        {
            AppointmentId = 500,
            DiagnosticType = "CBCT",
            ClinicalIndication = "Khảo sát ngoại viện",
            IsExternal = true,
            ExternalFacilityName = "BV Bạch Mai"
        }, 10);

        var req = new BookImagingSessionRequest
        {
            DiagnosticOrderId = order.DiagnosticOrderId,
            EquipmentId = 200,
            ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BookImagingSessionAsync(req, 5));
    }

    [Fact]
    public async Task TC16_BookImagingSession_WhenOrderAlreadyHasActiveSession_ThrowsException_Idempotency()
    {
        var db = CreateDbContext(nameof(TC16_BookImagingSession_WhenOrderAlreadyHasActiveSession_ThrowsException_Idempotency));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var order = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "Răng 21" }, 10);

        var startTime = DateTime.UtcNow.Date.AddHours(10);
        await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = order.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = startTime }, 5);

        // Attempting to book second session for the same order
        var secondStart = startTime.AddHours(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = order.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = secondStart }, 5));
    }

    [Fact]
    public async Task TC17_BookImagingSession_WhenIntervalOverlaps_ThrowsException_ConcurrencyGuard()
    {
        var db = CreateDbContext(nameof(TC17_BookImagingSession_WhenIntervalOverlaps_ThrowsException_ConcurrencyGuard));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var order1 = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Panorama", ClinicalIndication = "BN 1" }, 10);
        var order2 = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "BN 2" }, 10);

        var startTime = DateTime.UtcNow.Date.AddHours(10);
        await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = order1.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = startTime }, 5);

        // Attempt to book order2 overlapping with order1 (10:00 -> 10:15)
        var overlappingStart = startTime.AddMinutes(5);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = order2.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = overlappingStart }, 5));
    }

    [Fact]
    public async Task TC18_CheckInImagingSession_GeneratesSequentialQueueNumber_XR001()
    {
        var db = CreateDbContext(nameof(TC18_CheckInImagingSession_GeneratesSequentialQueueNumber_XR001));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var order = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "Răng 21" }, 10);
        var session = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = order.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10) }, 5);

        var checkedIn = await service.CheckInImagingSessionAsync(session.ImagingSessionId);

        Assert.Equal(ImagingSessionStatus.Waiting, checkedIn.Status);
        Assert.Equal("XR-001", checkedIn.QueueNumber);
        Assert.NotNull(checkedIn.CheckInTimeUtc);
    }

    [Fact]
    public async Task TC19_CheckInImagingSession_WhenNotScheduled_ThrowsException()
    {
        var db = CreateDbContext(nameof(TC19_CheckInImagingSession_WhenNotScheduled_ThrowsException));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var order = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "Răng 21" }, 10);
        var session = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = order.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10) }, 5);

        await service.CheckInImagingSessionAsync(session.ImagingSessionId);

        // Attempt to check-in again when already Waiting
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckInImagingSessionAsync(session.ImagingSessionId));
    }

    [Fact]
    public async Task TC20_CheckInImagingSession_WhenQueueExceedsMaxWaiting_ThrowsException()
    {
        var db = CreateDbContext(nameof(TC20_CheckInImagingSession_WhenQueueExceedsMaxWaiting_ThrowsException));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        // Room has MaxWaitingPatients = 3. Let's create 3 waiting sessions and try to check in 4th.
        for (int i = 1; i <= 3; i++)
        {
            var o = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = $"BN {i}" }, 10);
            var s = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10 + i) }, 5);
            await service.CheckInImagingSessionAsync(s.ImagingSessionId);
        }

        var o4 = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "BN 4" }, 10);
        var s4 = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o4.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(14) }, 5);

        // 4th check-in must fail because capacity is 3
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckInImagingSessionAsync(s4.ImagingSessionId));
        Assert.Contains("Hàng đợi phòng chụp X-quang đã đầy", ex.Message);
    }

    [Fact]
    public async Task TC21_CheckInImagingSession_CalculatesNonLinearEstimatedWaitMinutes()
    {
        var db = CreateDbContext(nameof(TC21_CheckInImagingSession_CalculatesNonLinearEstimatedWaitMinutes));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        // First session: Panorama (15 min)
        var o1 = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Panorama", ClinicalIndication = "BN 1" }, 10);
        var s1 = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o1.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10) }, 5);
        var c1 = await service.CheckInImagingSessionAsync(s1.ImagingSessionId);

        // Second session: CBCT (30 min)
        var o2 = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "CBCT", ClinicalIndication = "BN 2" }, 10);
        var s2 = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o2.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(11) }, 5);
        var c2 = await service.CheckInImagingSessionAsync(s2.ImagingSessionId);

        // Wait time for second session accounts for preceding 15 min Panorama
        Assert.True(c2.EstimatedWaitMinutes >= 15);
    }

    [Fact]
    public async Task TC22_CheckInImagingSession_WhenWaitExceedsTarget20Minutes_SetsDelayNotification()
    {
        var db = CreateDbContext(nameof(TC22_CheckInImagingSession_WhenWaitExceedsTarget20Minutes_SetsDelayNotification));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        // Preceding session: CBCT (30 min) waiting
        var o1 = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "CBCT", ClinicalIndication = "BN 1" }, 10);
        var s1 = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o1.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10) }, 5);
        await service.CheckInImagingSessionAsync(s1.ImagingSessionId);

        // Second session: Intraoral (10 min). Preceding wait is 30m >= 20m target
        var o2 = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "BN 2" }, 10);
        var s2 = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o2.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(11) }, 5);
        var c2 = await service.CheckInImagingSessionAsync(s2.ImagingSessionId);

        Assert.True(c2.DelayNotificationSent);
    }

    [Fact]
    public async Task TC23_CheckInImagingSession_WhenWaitExceedsEscalation30Minutes_SetsEscalationAlert()
    {
        var db = CreateDbContext(nameof(TC23_CheckInImagingSession_WhenWaitExceedsEscalation30Minutes_SetsEscalationAlert));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        // Preceding session 1: CBCT (30 min)
        var o1 = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "CBCT", ClinicalIndication = "BN 1" }, 10);
        var s1 = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o1.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10) }, 5);
        await service.CheckInImagingSessionAsync(s1.ImagingSessionId);

        // Preceding session 2: Panorama (15 min) -> total 45 min
        var o2 = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Panorama", ClinicalIndication = "BN 2" }, 10);
        var s2 = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o2.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(11) }, 5);
        await service.CheckInImagingSessionAsync(s2.ImagingSessionId);

        // Third session
        var o3 = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "BN 3" }, 10);
        var s3 = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o3.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(12) }, 5);
        var c3 = await service.CheckInImagingSessionAsync(s3.ImagingSessionId);

        Assert.True(c3.EscalationAlertSent);
    }

    [Fact]
    public async Task TC24_StartImagingSession_WhenMachineAlreadyInProgress_ThrowsException()
    {
        var db = CreateDbContext(nameof(TC24_StartImagingSession_WhenMachineAlreadyInProgress_ThrowsException));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var o1 = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "BN 1" }, 10);
        var s1 = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o1.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10) }, 5);
        await service.CheckInImagingSessionAsync(s1.ImagingSessionId);
        await service.StartImagingSessionAsync(s1.ImagingSessionId, 5);

        // Second session also waiting
        var o2 = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "BN 2" }, 10);
        var s2 = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o2.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(11) }, 5);
        await service.CheckInImagingSessionAsync(s2.ImagingSessionId);

        // Attempting to start s2 while s1 is InProgress on same machine
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartImagingSessionAsync(s2.ImagingSessionId, 5));
        Assert.Contains("đang được sử dụng", ex.Message);
    }

    [Fact]
    public async Task TC25_StartImagingSession_FromWaiting_TransitionsToInProgress()
    {
        var db = CreateDbContext(nameof(TC25_StartImagingSession_FromWaiting_TransitionsToInProgress));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var o = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "BN" }, 10);
        var s = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10) }, 5);
        await service.CheckInImagingSessionAsync(s.ImagingSessionId);

        var started = await service.StartImagingSessionAsync(s.ImagingSessionId, 5);

        Assert.Equal(ImagingSessionStatus.InProgress, started.Status);
        Assert.NotNull(started.ActualStartUtc);
    }

    [Fact]
    public async Task TC26_AcquireImage_WhenInProgress_TransitionsToAcquired()
    {
        var db = CreateDbContext(nameof(TC26_AcquireImage_WhenInProgress_TransitionsToAcquired));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var o = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "BN" }, 10);
        var s = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10) }, 5);
        await service.CheckInImagingSessionAsync(s.ImagingSessionId);
        await service.StartImagingSessionAsync(s.ImagingSessionId, 5);

        var acquired = await service.AcquireImageAsync(s.ImagingSessionId);

        Assert.Equal(ImagingSessionStatus.Acquired, acquired.Status);
    }

    [Fact]
    public async Task TC27_UploadDiagnosticResult_WithValidImage_CreatesResultAndUpdatesSession()
    {
        var db = CreateDbContext(nameof(TC27_UploadDiagnosticResult_WithValidImage_CreatesResultAndUpdatesSession));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var o = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Panorama", ClinicalIndication = "BN" }, 10);
        var s = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10) }, 5);
        await service.CheckInImagingSessionAsync(s.ImagingSessionId);
        await service.StartImagingSessionAsync(s.ImagingSessionId, 5);
        await service.AcquireImageAsync(s.ImagingSessionId);

        var result = await service.UploadDiagnosticResultAsync(new UploadDiagnosticResultRequest
        {
            DiagnosticOrderId = o.DiagnosticOrderId,
            ImagingSessionId = s.ImagingSessionId,
            FileUrl = "https://cdn.dentalcare.com/xray/XR-001.jpg",
            Findings = "Hình ảnh khớp cắn rõ nét"
        }, 5);

        Assert.NotNull(result);
        Assert.Equal("https://cdn.dentalcare.com/xray/XR-001.jpg", result.FileUrl);
        Assert.False(result.IsReviewed);
    }

    [Fact]
    public async Task TC28_CompleteImagingSession_WhenAcquiredOrResultAvailable_TransitionsToCompleted()
    {
        var db = CreateDbContext(nameof(TC28_CompleteImagingSession_WhenAcquiredOrResultAvailable_TransitionsToCompleted));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var o = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "BN" }, 10);
        var s = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10) }, 5);
        await service.CheckInImagingSessionAsync(s.ImagingSessionId);
        await service.StartImagingSessionAsync(s.ImagingSessionId, 5);
        await service.AcquireImageAsync(s.ImagingSessionId);

        var completed = await service.CompleteImagingSessionAsync(s.ImagingSessionId);

        Assert.Equal(ImagingSessionStatus.Completed, completed.Status);
        Assert.NotNull(completed.ActualEndUtc);

        var order = await service.GetDiagnosticOrderByIdAsync(o.DiagnosticOrderId);
        Assert.Equal(DiagnosticOrderStatus.Completed, order!.Status);
    }

    [Fact]
    public async Task TC29_ReviewDiagnosticResult_DentistAddsFindingsAndNotes_MarksReviewed()
    {
        var db = CreateDbContext(nameof(TC29_ReviewDiagnosticResult_DentistAddsFindingsAndNotes_MarksReviewed));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var o = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Panorama", ClinicalIndication = "BN" }, 10);
        var result = await service.UploadDiagnosticResultAsync(new UploadDiagnosticResultRequest
        {
            DiagnosticOrderId = o.DiagnosticOrderId,
            FileUrl = "https://cdn.dentalcare.com/xray/XR-002.jpg"
        }, 5);

        var reviewed = await service.ReviewDiagnosticResultAsync(result.DiagnosticResultId, new ReviewDiagnosticResultRequest
        {
            Findings = "Phát hiện sâu răng ngà sâu R46, mầm răng 48 mọc lệch",
            ReviewNotes = "Chỉ định trám răng composite và theo dõi nhổ răng 48 sau 3 tháng"
        }, 10);

        Assert.True(reviewed.IsReviewed);
        Assert.Equal(10, reviewed.ReviewedByDentistUserId);
        Assert.NotNull(reviewed.ReviewedAtUtc);
        Assert.Equal("Chỉ định trám răng composite và theo dõi nhổ răng 48 sau 3 tháng", reviewed.ReviewNotes);
    }

    [Fact]
    public async Task TC30_ReviewDiagnosticResult_CompletedSessionDoesNotAutoReview_RequiresExplicitDentistReview()
    {
        var db = CreateDbContext(nameof(TC30_ReviewDiagnosticResult_CompletedSessionDoesNotAutoReview_RequiresExplicitDentistReview));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var o = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Panorama", ClinicalIndication = "BN" }, 10);
        var s = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10) }, 5);
        await service.CheckInImagingSessionAsync(s.ImagingSessionId);
        await service.StartImagingSessionAsync(s.ImagingSessionId, 5);
        await service.AcquireImageAsync(s.ImagingSessionId);

        var res = await service.UploadDiagnosticResultAsync(new UploadDiagnosticResultRequest
        {
            DiagnosticOrderId = o.DiagnosticOrderId,
            ImagingSessionId = s.ImagingSessionId,
            FileUrl = "https://cdn.dentalcare.com/xray/test.jpg"
        }, 5);

        await service.CompleteImagingSessionAsync(s.ImagingSessionId);

        // Verification: Even though session is completed, the result itself MUST NOT be reviewed yet
        var dbResult = await db.DiagnosticResults.FirstAsync(r => r.DiagnosticResultId == res.DiagnosticResultId);
        Assert.False(dbResult.IsReviewed);
        Assert.Null(dbResult.ReviewedByDentistUserId);
    }

    [Fact]
    public async Task TC31_CancelImagingSession_RevertsOrderToOrderedIfNoActiveSessions()
    {
        var db = CreateDbContext(nameof(TC31_CancelImagingSession_RevertsOrderToOrderedIfNoActiveSessions));
        await SeedBaseDataAsync(db);
        var service = new ImagingService(db, _mockLogger.Object);

        var o = await service.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest { AppointmentId = 500, DiagnosticType = "Intraoral", ClinicalIndication = "BN" }, 10);
        var s = await service.BookImagingSessionAsync(new BookImagingSessionRequest { DiagnosticOrderId = o.DiagnosticOrderId, EquipmentId = 200, ScheduledStartUtc = DateTime.UtcNow.Date.AddHours(10) }, 5);

        var cancelled = await service.CancelImagingSessionAsync(s.ImagingSessionId, "Bệnh nhân bận", 5);

        Assert.Equal(ImagingSessionStatus.Cancelled, cancelled.Status);
        var order = await service.GetDiagnosticOrderByIdAsync(o.DiagnosticOrderId);
        Assert.Equal(DiagnosticOrderStatus.Ordered, order!.Status);
    }

    [Fact]
    public async Task TC32_BillingPreparation_IncludesCompletedInternalImaging_ExcludesExternalImaging()
    {
        var db = CreateDbContext(nameof(TC32_BillingPreparation_IncludesCompletedInternalImaging_ExcludesExternalImaging));
        await SeedBaseDataAsync(db);

        // Add internal completed order
        var internalOrder = new DiagnosticOrder
        {
            DiagnosticOrderId = 1001,
            AppointmentId = 500,
            PatientUserId = 20,
            DentistUserId = 10,
            DiagnosticType = "Panorama",
            ClinicalIndication = "Internal Xray",
            IsExternal = false,
            Status = "Completed",
            CreatedAtUtc = DateTime.UtcNow
        };
        var session = new ImagingSession
        {
            ImagingSessionId = 2001,
            DiagnosticOrderId = 1001,
            SourceAppointmentId = 500,
            PatientUserId = 20,
            ImagingRoomId = 100,
            EquipmentId = 200,
            ScheduledStartUtc = DateTime.UtcNow,
            ScheduledEndUtc = DateTime.UtcNow.AddMinutes(15),
            Status = "Completed",
            CreatedAtUtc = DateTime.UtcNow
        };

        // Add external completed order
        var externalOrder = new DiagnosticOrder
        {
            DiagnosticOrderId = 1002,
            AppointmentId = 500,
            PatientUserId = 20,
            DentistUserId = 10,
            DiagnosticType = "CBCT",
            ClinicalIndication = "External Xray",
            IsExternal = true,
            ExternalFacilityName = "BV Tai Mũi Họng",
            Status = "Completed",
            CreatedAtUtc = DateTime.UtcNow
        };

        db.DiagnosticOrders.AddRange(internalOrder, externalOrder);
        db.ImagingSessions.Add(session);
        await db.SaveChangesAsync();

        var mockLogger = new Mock<ILogger<BillingService>>();
        var mockNotificationService = new Mock<INotificationService>();
        var billingService = new BillingService(db, mockNotificationService.Object, mockLogger.Object);

        var prep = await billingService.GetAppointmentBillingPreparationAsync(500);

        Assert.True(prep.Success);
        var items = prep.Data!.SuggestedItems;

        // Internal imaging (Panorama - 200,000 VND) must be present in suggested billing items
        var internalItem = items.FirstOrDefault(i => i.Description.Contains("[CĐHA]") && i.Description.Contains("Toàn Cảnh"));
        Assert.NotNull(internalItem);
        Assert.Equal(200000m, internalItem.UnitPrice);

        // External imaging must NOT be present
        var externalItem = items.FirstOrDefault(i => i.Description.Contains("CBCT") || i.Description.Contains("Tai Mũi Họng"));
        Assert.Null(externalItem);
    }
}
