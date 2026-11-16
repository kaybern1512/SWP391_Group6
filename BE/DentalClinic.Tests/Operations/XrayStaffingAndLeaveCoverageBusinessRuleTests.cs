using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
using DentalClinic.Application.Features.DentistLeave.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DentalClinic.Tests.Operations;

/// <summary>
/// Regression tests verifying the corrected X-ray staffing and leave coverage business rules:
/// 1. Staffing pool contains 2 designated X-ray dentists.
/// 2. Operational minimum is 1 qualified, available on-duty dentist per active shift (single-doctor shifts are VALID).
/// 3. Both doctors on the same shift is VALID.
/// 4. Doctor A takes leave while Doctor B covers the same shift -> VALID/APPROVED.
/// 5. Doctor A is the only morning doctor and requests leave -> REJECTED (unless replaced/covered).
/// 6. Overlapping leave that reduces coverage below 1 -> REJECTED.
/// 7. Receptionist cannot allocate imaging work to an absent/on-leave dentist.
/// 8. Slots without coverage are marked unavailable.
/// </summary>
public class XrayStaffingAndLeaveCoverageBusinessRuleTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedClinicDataAsync(DentalClinicDbContext db)
    {
        // 1. Users: Admin, Doctor A, Doctor B, Doctor C (substitute), Receptionist, Patient
        var admin = new UserAccount { UserId = 1, Email = "admin@dentalcare.test", Role = UserRole.SystemAdministrator, Status = AccountStatus.Active };
        var docA = new UserAccount { UserId = 10, Email = "doctor.a@dentalcare.test", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var docB = new UserAccount { UserId = 11, Email = "doctor.b@dentalcare.test", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var docC = new UserAccount { UserId = 12, Email = "doctor.c@dentalcare.test", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var rec = new UserAccount { UserId = 5, Email = "rec@dentalcare.test", Role = UserRole.Receptionist, Status = AccountStatus.Active };
        var pat = new UserAccount { UserId = 20, Email = "pat@dentalcare.test", Role = UserRole.Patient, Status = AccountStatus.Active };
        db.UserAccounts.AddRange(admin, docA, docB, docC, rec, pat);

        // 2. Profiles
        db.StaffProfiles.AddRange(
            new StaffProfile { UserId = 10, FullName = "Bác Sĩ Nguyễn Văn A", EmploymentStatus = "Active" },
            new StaffProfile { UserId = 11, FullName = "Bác Sĩ Trần Thị B", EmploymentStatus = "Active" },
            new StaffProfile { UserId = 12, FullName = "Bác Sĩ Lê Văn C [Dự Phòng]", EmploymentStatus = "Active" }
        );

        db.DentistProfiles.AddRange(
            new DentistProfile { UserId = 10, DepartmentId = 1, LicenseNumber = "BS-XQ-010", Qualification = "Chuyên khoa CĐHA" },
            new DentistProfile { UserId = 11, DepartmentId = 1, LicenseNumber = "BS-XQ-011", Qualification = "Chuyên khoa CĐHA" },
            new DentistProfile { UserId = 12, DepartmentId = 1, LicenseNumber = "BS-XQ-012", Qualification = "Bác sĩ RHM" }
        );

        db.PatientProfiles.Add(new PatientProfile { UserId = 20, FullName = "Bệnh nhân Test" });

        // 3. Department & Room R003 (Zero DentalChairs)
        var dept = new Department { DepartmentId = 1, Name = "Khoa Răng Tổng Quát & CĐHA", Status = "Active" };
        var room = new Room { RoomId = 101, DepartmentId = 1, RoomCode = "R003", RoomName = "Phòng Chụp X-quang", RoomType = "Imaging", Status = "Active" };
        db.Departments.Add(dept);
        db.Rooms.Add(room);

        // 4. Diagnostic Service
        var svc = new DepartmentService
        {
            DepartmentServiceId = 201,
            DepartmentId = 1,
            ServiceCode = "SVC-XRAY-01",
            ServiceName = "Chụp X-quang Panorama",
            DiagnosticType = "Panorama",
            Price = 250000m,
            DurationMinutes = 15,
            Status = "Active"
        };
        db.DepartmentServices.Add(svc);

        // 5. Appointment
        var appt = new Appointment
        {
            AppointmentId = 501,
            PatientUserId = 20,
            BookingSource = "WalkIn",
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 601,
            AppointmentId = 501,
            DepartmentServiceId = 201,
            DepartmentId = 1,
            RoomId = 101,
            ScheduledStart = new DateTime(2026, 12, 1, 8, 30, 0, DateTimeKind.Utc),
            ScheduledEnd = new DateTime(2026, 12, 1, 8, 45, 0, DateTimeKind.Utc),
            ServiceDate = new DateOnly(2026, 12, 1),
            UnitPrice = 250000m,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);

        // 6. Diagnostic order
        var diag = new Diagnostic
        {
            DiagnosticId = 701,
            AppointmentServiceId = 601,
            DepartmentServiceId = 201,
            OrderingDentistUserId = 10,
            ClinicalIndication = "Khảo sát răng khôn",
            Status = "Ordered",
            CreatedAt = DateTime.UtcNow
        };
        db.Diagnostics.Add(diag);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Rule1_SingleDoctorShift_IsFullyValid_AndSlotsAreAvailable()
    {
        // Arrange: Doctor A works Morning (08:00 - 12:00), Doctor B works Afternoon (13:00 - 17:00)
        var db = CreateDbContext(nameof(Rule1_SingleDoctorShift_IsFullyValid_AndSlotsAreAvailable));
        await SeedClinicDataAsync(db);

        var date = new DateOnly(2026, 12, 1);
        db.StaffWorkSchedules.AddRange(
            new StaffWorkSchedule { WorkScheduleId = 1, StaffUserId = 10, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003]" },
            new StaffWorkSchedule { WorkScheduleId = 2, StaffUserId = 11, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(17, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003]" }
        );
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance, null!);

        // Act: Query available slots for the day
        var slots = await imagingService.GetAvailableSlotsAsync(101, new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), "Panorama");

        // Assert:
        // Morning slots (e.g. 09:00 - 09:15) have Doctor A coverage -> Available
        var morningSlot = slots.FirstOrDefault(s => s.StartUtc.Hour == 9 && s.StartUtc.Minute == 0);
        Assert.NotNull(morningSlot);
        Assert.True(morningSlot.IsAvailable, "Morning slot with 1 on-duty doctor must be available");

        // Afternoon slots (e.g. 14:00 - 14:15) have Doctor B coverage -> Available
        var afternoonSlot = slots.FirstOrDefault(s => s.StartUtc.Hour == 14 && s.StartUtc.Minute == 0);
        Assert.NotNull(afternoonSlot);
        Assert.True(afternoonSlot.IsAvailable, "Afternoon slot with 1 on-duty doctor must be available");

        // Unscheduled slots (e.g. 12:15 - 12:30 during lunch break) have no coverage -> Unavailable
        var lunchSlot = slots.FirstOrDefault(s => s.StartUtc.Hour == 12 && s.StartUtc.Minute == 15);
        Assert.NotNull(lunchSlot);
        Assert.False(lunchSlot.IsAvailable, "Slot with 0 on-duty doctors must be marked unavailable");
        Assert.Contains("không có bác sĩ trực", lunchSlot.ConflictReason);
    }

    [Fact]
    public async Task Rule2_BothDoctorsOnSameShift_IsFullyValid()
    {
        // Arrange: Both Doctor A and Doctor B work Morning (08:00 - 12:00)
        var db = CreateDbContext(nameof(Rule2_BothDoctorsOnSameShift_IsFullyValid));
        await SeedClinicDataAsync(db);

        var date = new DateOnly(2026, 12, 1);
        db.StaffWorkSchedules.AddRange(
            new StaffWorkSchedule { WorkScheduleId = 1, StaffUserId = 10, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003]" },
            new StaffWorkSchedule { WorkScheduleId = 2, StaffUserId = 11, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003]" }
        );
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance, null!);
        var slots = await imagingService.GetAvailableSlotsAsync(101, new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), "Panorama");

        var morningSlot = slots.FirstOrDefault(s => s.StartUtc.Hour == 10);
        Assert.NotNull(morningSlot);
        Assert.True(morningSlot.IsAvailable, "Morning slot with 2 doctors on duty is fully valid and available");
    }

    [Fact]
    public async Task Rule3_DoctorATakesLeave_WhileDoctorBCoversSameShift_CanApprove()
    {
        // Arrange: Both Doctor A and Doctor B work Morning (08:00 - 12:00).
        // Doctor A requests leave for the morning.
        var db = CreateDbContext(nameof(Rule3_DoctorATakesLeave_WhileDoctorBCoversSameShift_CanApprove));
        await SeedClinicDataAsync(db);

        var date = new DateOnly(2026, 12, 1);
        db.StaffWorkSchedules.AddRange(
            new StaffWorkSchedule { WorkScheduleId = 1, StaffUserId = 10, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003]" },
            new StaffWorkSchedule { WorkScheduleId = 2, StaffUserId = 11, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003]" }
        );
        await db.SaveChangesAsync();

        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);

        // Act: Validate Doctor A's leave request
        var (canApprove, reason) = await availabilityService.ValidateXrayShiftLeaveCoverageAsync(
            dentistUserId: 10,
            startDate: date,
            endDate: date,
            startTime: new TimeOnly(8, 0),
            endTime: new TimeOnly(12, 0)
        );

        // Assert: Since Doctor B covers the shift, minimum 1 coverage is preserved -> CanApprove = true
        Assert.True(canApprove, "Doctor A leave should be approved because Doctor B covers the shift");
        Assert.Null(reason);
    }

    [Fact]
    public async Task Rule4_SoleDoctorRequestsLeave_WithoutCoverage_MustBeRejected()
    {
        // Arrange: Only Doctor A is scheduled for the Morning (08:00 - 12:00). Doctor B is not scheduled.
        var db = CreateDbContext(nameof(Rule4_SoleDoctorRequestsLeave_WithoutCoverage_MustBeRejected));
        await SeedClinicDataAsync(db);

        var date = new DateOnly(2026, 12, 1);
        db.StaffWorkSchedules.Add(
            new StaffWorkSchedule { WorkScheduleId = 1, StaffUserId = 10, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003]" }
        );
        await db.SaveChangesAsync();

        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);

        // Act: Validate Doctor A's leave request
        var (canApprove, reason) = await availabilityService.ValidateXrayShiftLeaveCoverageAsync(
            dentistUserId: 10,
            startDate: date,
            endDate: date,
            startTime: new TimeOnly(8, 0),
            endTime: new TimeOnly(12, 0)
        );

        // Assert: Zero coverage would result -> Must be rejected
        Assert.False(canApprove, "Sole doctor leave must be rejected when no replacement covers the shift");
        Assert.NotNull(reason);
        Assert.Contains("tối thiểu một bác sĩ trực", reason);
    }

    [Fact]
    public async Task Rule5_SoleDoctorRequestsLeave_WithSubstituteAdded_CanApprove()
    {
        // Arrange: Doctor A is scheduled for Morning. Doctor C (substitute) is also scheduled for Morning.
        var db = CreateDbContext(nameof(Rule5_SoleDoctorRequestsLeave_WithSubstituteAdded_CanApprove));
        await SeedClinicDataAsync(db);

        var date = new DateOnly(2026, 12, 1);
        db.StaffWorkSchedules.AddRange(
            new StaffWorkSchedule { WorkScheduleId = 1, StaffUserId = 10, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003]" },
            new StaffWorkSchedule { WorkScheduleId = 3, StaffUserId = 12, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003] - Bác sĩ thay thế" }
        );
        await db.SaveChangesAsync();

        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);

        // Act: Validate Doctor A's leave request
        var (canApprove, reason) = await availabilityService.ValidateXrayShiftLeaveCoverageAsync(
            dentistUserId: 10,
            startDate: date,
            endDate: date,
            startTime: new TimeOnly(8, 0),
            endTime: new TimeOnly(12, 0)
        );

        // Assert: Substitute Doctor C maintains coverage -> Approved
        Assert.True(canApprove, "Leave must be approved once a qualified substitute is scheduled");
        Assert.Null(reason);
    }

    [Fact]
    public async Task Rule6_OverlappingLeave_ReducingCoverageToZero_MustBeRejected()
    {
        // Arrange: Both Doctor A and Doctor B work Morning. Doctor A's leave is already APPROVED.
        var db = CreateDbContext(nameof(Rule6_OverlappingLeave_ReducingCoverageToZero_MustBeRejected));
        await SeedClinicDataAsync(db);

        var date = new DateOnly(2026, 12, 1);
        db.StaffWorkSchedules.AddRange(
            new StaffWorkSchedule { WorkScheduleId = 1, StaffUserId = 10, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003]" },
            new StaffWorkSchedule { WorkScheduleId = 2, StaffUserId = 11, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003]" }
        );

        // Doctor A has approved leave for this morning shift
        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 1001,
            StaffUserId = 10,
            StaffRole = "Dentist",
            LeaveType = "Annual",
            Reason = "Nghỉ phép thường niên",
            StartDate = date,
            EndDate = date,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);

        // Act: Doctor B now also submits/requests leave for the same morning shift
        var (canApprove, reason) = await availabilityService.ValidateXrayShiftLeaveCoverageAsync(
            dentistUserId: 11,
            startDate: date,
            endDate: date,
            startTime: new TimeOnly(8, 0),
            endTime: new TimeOnly(12, 0)
        );

        // Assert: Since Doctor A is already on leave, approving Doctor B would drop coverage to zero -> REJECTED
        Assert.False(canApprove, "Second doctor's overlapping leave must be rejected to maintain minimum 1 coverage");
        Assert.NotNull(reason);
        Assert.Contains("tối thiểu một bác sĩ trực", reason);
    }

    [Fact]
    public async Task Rule7_ReceptionistCannotAllocateImagingWork_ToAbsentDoctorOnLeave()
    {
        // Arrange: Doctor A has an approved leave request for the target morning
        var db = CreateDbContext(nameof(Rule7_ReceptionistCannotAllocateImagingWork_ToAbsentDoctorOnLeave));
        await SeedClinicDataAsync(db);

        var date = new DateOnly(2026, 12, 1);
        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 1002,
            StaffUserId = 10,
            StaffRole = "Dentist",
            LeaveType = "Sick",
            Reason = "Nghỉ ốm",
            StartDate = date,
            EndDate = date,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance, null!);

        // Act & Assert: Receptionist tries to allocate an imaging session specifically to Doctor A who is on leave
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            imagingService.BookImagingSessionAsync(new BookImagingSessionRequest
            {
                DiagnosticOrderId = 701,
                EquipmentId = 101,
                ScheduledStartUtc = new DateTime(2026, 12, 1, 2, 0, 0, DateTimeKind.Utc), // 09:00 VN Time (within 08:00 - 12:00 leave)
                AssignedDentistUserId = 10
            }, 5));

        Assert.Contains("đang trong thời gian nghỉ phép", ex.Message);
    }

    [Fact]
    public async Task Rule8_ActiveRosterLookup_PrioritizesAvailableCoveringDoctor()
    {
        // Arrange: Both Doctor A and Doctor B work Morning. Doctor A is on approved leave.
        var db = CreateDbContext(nameof(Rule8_ActiveRosterLookup_PrioritizesAvailableCoveringDoctor));
        await SeedClinicDataAsync(db);

        var date = new DateOnly(2026, 12, 1);
        db.StaffWorkSchedules.AddRange(
            new StaffWorkSchedule { WorkScheduleId = 1, StaffUserId = 10, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003]" },
            new StaffWorkSchedule { WorkScheduleId = 2, StaffUserId = 11, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003]" }
        );

        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 1003,
            StaffUserId = 10,
            StaffRole = "Dentist",
            LeaveType = "Annual",
            Reason = "Nghỉ phép thường niên",
            StartDate = date,
            EndDate = date,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var rosterService = new XrayRosterService(db, NullLogger<XrayRosterService>.Instance);

        // Act: Get active roster for room at 09:30 AM
        var queryTimeUtc = new DateTime(2026, 12, 1, 2, 30, 0, DateTimeKind.Utc); // 09:30 VN Time
        var activeRoster = await rosterService.GetActiveRosterForRoomAsync(101, queryTimeUtc);

        // Assert: Must return Doctor B (UserId 11) because Doctor A is on leave
        Assert.NotNull(activeRoster);
        Assert.Equal(11, activeRoster.DentistUserId);
        Assert.False(activeRoster.IsDentistOnLeave, "Active doctor returned must not be marked on leave");
    }

    [Fact]
    public async Task Rule9_IsDentistOnXrayDuty_ReturnsFalse_WhenDentistIsOnApprovedLeave()
    {
        // Arrange: Doctor A is scheduled for Morning, but has approved leave
        var db = CreateDbContext(nameof(Rule9_IsDentistOnXrayDuty_ReturnsFalse_WhenDentistIsOnApprovedLeave));
        await SeedClinicDataAsync(db);

        var date = new DateOnly(2026, 12, 1);
        db.StaffWorkSchedules.Add(
            new StaffWorkSchedule { WorkScheduleId = 1, StaffUserId = 10, StaffRole = "Dentist", WorkDate = date, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), Status = "Scheduled", Notes = "Trực phòng X-quang [R003]" }
        );

        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 1004,
            StaffUserId = 10,
            StaffRole = "Dentist",
            LeaveType = "Casual",
            Reason = "Việc riêng",
            StartDate = date,
            EndDate = date,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var rosterService = new XrayRosterService(db, NullLogger<XrayRosterService>.Instance);

        var shiftStartUtc = new DateTime(2026, 12, 1, 1, 0, 0, DateTimeKind.Utc); // 08:00 VN Time
        var shiftEndUtc = new DateTime(2026, 12, 1, 5, 0, 0, DateTimeKind.Utc); // 12:00 VN Time

        // Act
        var isOnDuty = await rosterService.IsDentistOnXrayDutyAsync(10, shiftStartUtc, shiftEndUtc);

        // Assert: Must be false because Doctor A is on approved leave
        Assert.False(isOnDuty, "Dentist on approved leave must not be reported as on active X-ray duty");
    }

    [Fact]
    public async Task TimezoneBoundary_VietnamAfternoonShift_1400Local_Accepted_And_2100Local_Rejected()
    {
        // Doctor B works afternoon: 13:00 - 17:00 Vietnam local time (ICT / UTC+7).
        var db = CreateDbContext(nameof(TimezoneBoundary_VietnamAfternoonShift_1400Local_Accepted_And_2100Local_Rejected));
        await SeedClinicDataAsync(db);

        var date = new DateOnly(2026, 11, 30);
        db.StaffWorkSchedules.Add(
            new StaffWorkSchedule
            {
                WorkScheduleId = 50,
                StaffUserId = 11, // Doctor B
                StaffRole = "Dentist",
                WorkDate = date,
                StartTime = new TimeOnly(13, 0),
                EndTime = new TimeOnly(17, 0),
                Status = "Scheduled",
                Notes = "Trực phòng X-quang ca chiều [R003]"
            }
        );

        var appt = new Appointment
        {
            AppointmentId = 901,
            PatientUserId = 20,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 901,
            AppointmentId = 901,
            DepartmentId = 1,
            DepartmentServiceId = 201,
            RoomId = 101,
            DentalChairId = 1,
            AssignedDentistUserId = 10, // Doctor A (consulting)
            ServiceDate = date,
            ScheduledStart = new DateTime(2026, 11, 30, 8, 30, 0),
            ScheduledEnd = new DateTime(2026, 11, 30, 9, 0, 0),
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        var diag = new Diagnostic
        {
            DiagnosticId = 901,
            AppointmentServiceId = 901,
            DepartmentServiceId = 201,
            OrderingDentistUserId = 10,
            ClinicalIndication = "Chụp X-quang kiểm tra",
            ImagingType = "Panorama",
            Status = "Ordered",
            CreatedAt = DateTime.UtcNow
        };

        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance, null!);

        // Case 1: Client sends 14:00 Vietnam local time as 07:00Z UTC (2026-11-30T07:00:00Z)
        // 07:00Z = 14:00 Vietnam time, which is INSIDE Doctor B's shift (13:00 - 17:00).
        var validBookingReq = new BookImagingSessionRequest
        {
            DiagnosticOrderId = 901,
            EquipmentId = 101, // Room R003
            AssignedDentistUserId = 11, // Doctor B
            ScheduledStartUtc = new DateTime(2026, 11, 30, 7, 0, 0, DateTimeKind.Utc)
        };

        var validResult = await imagingService.BookImagingSessionAsync(validBookingReq, 5);
        Assert.NotNull(validResult);
        Assert.Equal(11, validResult.AssignedDentistUserId);
        Assert.Equal(101, validResult.ImagingRoomId);
        Assert.Equal(new DateTime(2026, 11, 30, 7, 0, 0, DateTimeKind.Utc), validResult.ScheduledStartUtc);

        // Verify parent consultation room and dentist are preserved!
        var reloadedApptSvc = await db.AppointmentServices.FindAsync(901L);
        Assert.NotNull(reloadedApptSvc);
        Assert.Equal(101, reloadedApptSvc.RoomId);
        Assert.Equal(10, reloadedApptSvc.AssignedDentistUserId); // Remains Doctor A!

        // Case 2: Client mistakenly sends 14:00Z UTC (2026-11-30T14:00:00Z).
        // 14:00Z = 21:00 Vietnam local time, which is OUTSIDE Doctor B's shift (13:00 - 17:00).
        var invalidBookingReq = new BookImagingSessionRequest
        {
            DiagnosticOrderId = 901,
            EquipmentId = 101,
            AssignedDentistUserId = 11,
            ScheduledStartUtc = new DateTime(2026, 11, 30, 14, 0, 0, DateTimeKind.Utc)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => imagingService.BookImagingSessionAsync(invalidBookingReq, 5));
        Assert.Contains("không có ca trực", ex.Message);
    }
}
