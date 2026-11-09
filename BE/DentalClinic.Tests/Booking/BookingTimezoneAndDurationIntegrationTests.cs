using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Booking.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Booking;

public class BookingTimezoneAndDurationIntegrationTests
{
    private readonly Mock<INotificationService> _mockNotification = new();
    private readonly Mock<ILogger<AppointmentService>> _mockApptLogger = new();
    private readonly Mock<ILogger<BookingService>> _mockBookingLogger = new();

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
        // 1. Patient
        var patient = new UserAccount
        {
            UserId = 101,
            Email = "patient101@test.com",
            PhoneNumber = "0901234567",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = 101,
            PatientCode = "PAT-101",
            FullName = "Nguyễn Văn An",
            User = patient
        };
        db.UserAccounts.Add(patient);
        db.PatientProfiles.Add(patientProfile);

        // 2. Department
        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Hàm Mặt",
            Status = "Active"
        };
        db.Departments.Add(dept);

        // 2b. Department Work Schedules (Monday to Sunday, 08:00 - 17:00)
        for (byte d = 1; d <= 7; d++)
        {
            db.DepartmentWorkSchedules.Add(new DepartmentWorkSchedule
            {
                DepartmentScheduleId = d,
                DepartmentId = 1,
                DayOfWeek = d,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(17, 0),
                IsActive = true
            });
        }

        // 3. Services: 30 min standard, 60 min surgery (requires exclusive room, 4h lead time), 90 min implant
        var service30 = new DepartmentService
        {
            DepartmentServiceId = 1,
            DepartmentId = 1,
            ServiceName = "Khám Tổng Quát",
            DurationMinutes = 30,
            Price = 150000,
            Status = "Active",
            ServiceType = "Examination",
            RequiresExclusiveRoom = false,
            RequiresConsultationFirst = false,
            MinLeadTimeHours = 2,
            BookingInstructions = "Vui lòng đến trước 10 phút."
        };

        var service60Exclusive = new DepartmentService
        {
            DepartmentServiceId = 2,
            DepartmentId = 1,
            ServiceName = "Tiểu Phẫu Nhổ Răng Khôn",
            DurationMinutes = 60,
            Price = 1500000,
            Status = "Active",
            ServiceType = "Surgery",
            RequiresExclusiveRoom = true,
            RequiresConsultationFirst = true,
            MinLeadTimeHours = 4,
            BookingInstructions = "Không ăn uống no trước 2 tiếng."
        };

        var service90 = new DepartmentService
        {
            DepartmentServiceId = 3,
            DepartmentId = 1,
            ServiceName = "Cấy Ghép Implant",
            DurationMinutes = 90,
            Price = 15000000,
            Status = "Active",
            ServiceType = "Implant",
            RequiresExclusiveRoom = false,
            RequiresConsultationFirst = true,
            MinLeadTimeHours = 6,
            BookingInstructions = "Cần mang theo phim chụp CT."
        };

        db.DepartmentServices.AddRange(service30, service60Exclusive, service90);

        // 4. Dentist
        var dentistUser = new UserAccount
        {
            UserId = 201,
            Email = "dentist201@test.com",
            PhoneNumber = "0987654321",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var dentistStaff = new StaffProfile
        {
            UserId = 201,
            EmployeeCode = "EMP-DEN-201",
            FullName = "Bác Sĩ Trần Minh",
            EmploymentStatus = "Active",
            User = dentistUser
        };
        var dentistProfile = new DentistProfile
        {
            UserId = 201,
            LicenseNumber = "LIC-201",
            User = dentistStaff
        };
        db.UserAccounts.Add(dentistUser);
        db.StaffProfiles.Add(dentistStaff);
        db.DentistProfiles.Add(dentistProfile);
        db.DentistDepartments.Add(new DentistDepartment
        {
            DentistUserId = 201,
            DepartmentId = 1,
            Status = "Active"
        });

        // 5. Exclusive Room & Shared Room
        var roomExclusive = new Room
        {
            RoomId = 10,
            DepartmentId = 1,
            RoomCode = "ROOM-EXC-10",
            RoomName = "Phòng Phẫu Thuật",
            IsExclusive = true,
            Status = "Active"
        };
        var chairExclusive = new DentalChair
        {
            DentalChairId = 100,
            RoomId = 10,
            ChairCode = "CHAIR-10",
            Status = "Active"
        };
        var roomShared = new Room
        {
            RoomId = 20,
            DepartmentId = 1,
            RoomCode = "ROOM-SHR-20",
            RoomName = "Phòng Khám Chung",
            IsExclusive = false,
            Status = "Active"
        };
        var chairShared = new DentalChair
        {
            DentalChairId = 200,
            RoomId = 20,
            ChairCode = "CHAIR-20",
            Status = "Active"
        };
        db.Rooms.AddRange(roomExclusive, roomShared);
        db.DentalChairs.AddRange(chairExclusive, chairShared);

        // 6. Work Schedule for Today and Tomorrow (08:00 - 17:00)
        var clinicToday = ClinicTimeHelper.TodayDateOnly;
        db.DentistWorkSchedules.AddRange(
            new DentistWorkSchedule
            {
                DentistScheduleId = 1,
                DentistUserId = 201,
                DepartmentId = 1,
                WorkDate = clinicToday,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(17, 0),
                Status = "Scheduled"
            },
            new DentistWorkSchedule
            {
                DentistScheduleId = 2,
                DentistUserId = 201,
                DepartmentId = 1,
                WorkDate = clinicToday.AddDays(1),
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(17, 0),
                Status = "Scheduled"
            }
        );

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Test01_PastDate_Booking_StrictlyRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var apptService = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);

        // Attempt to book yesterday
        var pastDate = ClinicTimeHelper.Today.AddDays(-1).AddHours(9);
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = pastDate,
            ReasonForVisit = "Đau răng cần khám"
        };

        var result = await apptService.CreatePatientAppointmentAsync(101, request);

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
        Assert.Contains("quá khứ", result.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task Test02_PastHourToday_Booking_StrictlyRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var apptService = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);

        // Attempt to book 30 minutes in the past
        var pastTime = ClinicTimeHelper.Now.AddMinutes(-30);
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = pastTime,
            ReasonForVisit = "Khám khẩn cấp"
        };

        var result = await apptService.CreatePatientAppointmentAsync(101, request);

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
        Assert.Contains("quá khứ", result.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task Test03_PastDate_GetAvailableSlots_Returns400()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var bookingService = new BookingService(db, _mockBookingLogger.Object);

        // Query slots for yesterday
        var pastDate = ClinicTimeHelper.Today.AddDays(-1);
        var result = await bookingService.GetAvailableSlotsAsync(new AvailableSlotsQuery
        {
            DepartmentId = 1,
            ServiceId = 1,
            Date = pastDate
        });

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
        Assert.Contains("quá khứ", result.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task Test04_TodaySlots_ExcludePastHours_And_EnforcesMinLeadTime()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var bookingService = new BookingService(db, _mockBookingLogger.Object);
        var clinicNow = ClinicTimeHelper.Now;
        var clinicToday = ClinicTimeHelper.Today;

        // Service 1 has MinLeadTimeHours = 2
        var result = await bookingService.GetAvailableSlotsAsync(new AvailableSlotsQuery
        {
            DepartmentId = 1,
            ServiceId = 1,
            Date = clinicToday
        });

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);

        var leadThreshold = clinicNow.AddHours(2);
        foreach (var slot in result.Data)
        {
            Assert.True(slot.StartTime > clinicNow.AddMinutes(15), $"Slot {slot.StartTime} must not be in past");
            Assert.True(slot.StartTime >= leadThreshold, $"Slot {slot.StartTime} must respect MinLeadTimeHours=2 ({leadThreshold})");
        }
    }

    [Fact]
    public async Task Test05_ClosingBoundary_SlotExceedingShiftEnd_Omitted()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var bookingService = new BookingService(db, _mockBookingLogger.Object);
        var tomorrow = ClinicTimeHelper.Today.AddDays(1);

        // Service 2: Duration = 60 minutes. Dentist shift is 08:00 - 17:00.
        // A slot at 16:30 would end at 17:30 (> 17:00 closing), so it MUST NOT be emitted.
        var result = await bookingService.GetAvailableSlotsAsync(new AvailableSlotsQuery
        {
            DepartmentId = 1,
            ServiceId = 2,
            Date = tomorrow
        });

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data);

        foreach (var slot in result.Data)
        {
            var slotEnd = slot.StartTime.AddMinutes(60);
            Assert.True(slotEnd.TimeOfDay <= new TimeSpan(17, 0, 0),
                $"Slot {slot.StartTime:HH:mm} ending at {slotEnd:HH:mm} exceeds clinic closing time 17:00");
        }

        // Specifically assert that 16:30 is NOT among the slots
        var slot1630 = result.Data.FirstOrDefault(s => s.StartTime.Hour == 16 && s.StartTime.Minute == 30);
        Assert.Null(slot1630);
    }

    [Fact]
    public async Task Test06_VaryingServiceDurations_ProducesCorrectDurationAndEndTime()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var apptService = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var targetDate = ClinicTimeHelper.Today.AddDays(1);

        // 1. Service 1 (30 min)
        var start1 = targetDate.AddHours(9);
        var res1 = await apptService.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = start1,
            ReasonForVisit = "Khám 30 phút"
        });
        Assert.True(res1.Success, res1.Message);
        Assert.NotNull(res1.Data);
        Assert.True(res1.Data.ScheduledEnd.HasValue && res1.Data.ScheduledStart.HasValue);
        Assert.Equal(30, (res1.Data.ScheduledEnd.Value - res1.Data.ScheduledStart.Value).TotalMinutes);

        // 2. Service 2 (60 min)
        var start2 = targetDate.AddHours(10);
        var res2 = await apptService.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 2,
            ScheduledStart = start2,
            ReasonForVisit = "Tiểu phẫu 60 phút"
        });
        Assert.True(res2.Success, res2.Message);
        Assert.NotNull(res2.Data);
        Assert.True(res2.Data.ScheduledEnd.HasValue && res2.Data.ScheduledStart.HasValue);
        Assert.Equal(60, (res2.Data.ScheduledEnd.Value - res2.Data.ScheduledStart.Value).TotalMinutes);

        // 3. Service 3 (90 min)
        var start3 = targetDate.AddHours(14);
        var res3 = await apptService.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 3,
            ScheduledStart = start3,
            ReasonForVisit = "Cấy Implant 90 phút"
        });
        Assert.True(res3.Success, res3.Message);
        Assert.NotNull(res3.Data);
        Assert.True(res3.Data.ScheduledEnd.HasValue && res3.Data.ScheduledStart.HasValue);
        Assert.Equal(90, (res3.Data.ScheduledEnd.Value - res3.Data.ScheduledStart.Value).TotalMinutes);
    }

    [Fact]
    public async Task Test07_ConcurrencyAndCollision_ExclusiveRoom_OmittedWhenOccupied()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var tomorrow = ClinicTimeHelper.Today.AddDays(1);

        // Book exclusive Room 10 from 10:00 to 11:00
        var existingExclusiveAppt = new Appointment
        {
            AppointmentCode = "APP-TEST-EXC-01",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 2,
            AssignedDentistUserId = 201,
            RoomId = 10, // Exclusive room
            DentalChairId = 100,
            ScheduledStart = tomorrow.AddHours(10),
            ScheduledEnd = tomorrow.AddHours(11),
            ReasonForVisit = "Phẫu thuật",
            BookingSource = BookingSource.Patient,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(existingExclusiveAppt);
        await db.SaveChangesAsync();

        var bookingService = new BookingService(db, _mockBookingLogger.Object);

        // Query slots for Service 2 (RequiresExclusiveRoom = true)
        var result = await bookingService.GetAvailableSlotsAsync(new AvailableSlotsQuery
        {
            DepartmentId = 1,
            ServiceId = 2,
            Date = tomorrow
        });

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);

        // 10:00 and 10:30 slots overlap with 10:00-11:00 on the exclusive room and MUST NOT be available
        var slot1000 = result.Data.FirstOrDefault(s => s.StartTime == tomorrow.AddHours(10));
        var slot1030 = result.Data.FirstOrDefault(s => s.StartTime == tomorrow.AddHours(10).AddMinutes(30));

        Assert.Null(slot1000);
        Assert.Null(slot1030);

        // But 09:00 (ends 10:00) should be available
        var slot0900 = result.Data.FirstOrDefault(s => s.StartTime == tomorrow.AddHours(9));
        Assert.NotNull(slot0900);
    }

    [Fact]
    public async Task Test08_PatientReschedule_IntoPast_StrictlyRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var apptService = new AppointmentService(db, _mockNotification.Object, _mockApptLogger.Object);
        var tomorrow = ClinicTimeHelper.Today.AddDays(1);

        // Create an initial valid appointment
        var createRes = await apptService.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow.AddHours(10),
            ReasonForVisit = "Khám ban đầu"
        });
        Assert.True(createRes.Success);
        var apptId = createRes.Data!.AppointmentId;

        // Attempt to reschedule into past
        var pastReschedule = await apptService.PatientRescheduleAsync(apptId, 101, new PatientRescheduleRequest
        {
            NewScheduledStart = ClinicTimeHelper.Now.AddMinutes(-10),
            Reason = "Bận việc đột xuất"
        });

        Assert.False(pastReschedule.Success);
        Assert.NotNull(pastReschedule.Message);
        Assert.Contains("quá khứ", pastReschedule.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task Test09_DepartmentServiceDto_ProjectsAllPhase0Attributes()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedDataAsync(db);

        var bookingService = new BookingService(db, _mockBookingLogger.Object);

        var result = await bookingService.GetServicesByDepartmentAsync(1);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.Count);

        var surgeryService = result.Data.First(s => s.DepartmentServiceId == 2);
        Assert.Equal("Surgery", surgeryService.ServiceType);
        Assert.True(surgeryService.RequiresExclusiveRoom);
        Assert.True(surgeryService.RequiresConsultationFirst);
        Assert.Equal(4, surgeryService.MinLeadTimeHours);
        Assert.Equal("Không ăn uống no trước 2 tiếng.", surgeryService.BookingInstructions);
    }
}
