using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
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
using AppointmentAppService = DentalClinic.Infrastructure.Services.AppointmentService;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

namespace DentalClinic.Tests.Booking;

public class BookingCoreIntegrityTests
{
    private static DentalClinicDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static (AppointmentAppService Service, DentalClinicDbContext Db, Mock<INotificationService> MockNotif) CreateServiceFixture()
    {
        var db = CreateInMemoryDbContext();
        var mockNotif = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<AppointmentAppService>>();
        var service = new AppointmentAppService(db, mockNotif.Object, null, null, mockLogger.Object, null);
        return (service, db, mockNotif);
    }

    private static async Task SeedClinicFixtureAsync(DentalClinicDbContext db)
    {
        var now = DateTime.UtcNow;

        // 1. Departments
        var deptGeneral = new Department { DepartmentId = 1, Name = "Nha khoa Tổng quát", Status = "Active", CreatedAt = now };
        var deptImaging = new Department { DepartmentId = 2, Name = "Chẩn đoán hình ảnh", Status = "Active", CreatedAt = now };
        db.Departments.AddRange(deptGeneral, deptImaging);

        // 2. Services
        var sGeneralExam = new DepartmentService
        {
            DepartmentServiceId = 1,
            DepartmentId = 1,
            ServiceCode = "SRV-GEN-01",
            ServiceName = "Khám tổng quát",
            Price = 100000m,
            DurationMinutes = 30,
            Status = "Active",
            CreatedAt = now
        };
        var sScaling = new DepartmentService
        {
            DepartmentServiceId = 2,
            DepartmentId = 1,
            ServiceCode = "SRV-GEN-02",
            ServiceName = "Cạo vôi răng",
            Price = 250000m,
            DurationMinutes = 45,
            Status = "Active",
            CreatedAt = now
        };
        var sPolishing = new DepartmentService
        {
            DepartmentServiceId = 3,
            DepartmentId = 1,
            ServiceCode = "SRV-GEN-03",
            ServiceName = "Đánh bóng răng",
            Price = 150000m,
            DurationMinutes = 30,
            Status = "Active",
            CreatedAt = now
        };
        var sXray = new DepartmentService
        {
            DepartmentServiceId = 4,
            DepartmentId = 2,
            ServiceCode = "SRV-IMG-01",
            ServiceName = "Chụp X-quang răng Panorama",
            Price = 300000m,
            DurationMinutes = 20,
            Status = "Active",
            CreatedAt = now
        };
        var sInactive = new DepartmentService
        {
            DepartmentServiceId = 5,
            DepartmentId = 1,
            ServiceCode = "SRV-INACTIVE",
            ServiceName = "Dịch vụ đã ngưng",
            Price = 500000m,
            DurationMinutes = 60,
            Status = "Inactive",
            CreatedAt = now
        };
        db.DepartmentServices.AddRange(sGeneralExam, sScaling, sPolishing, sXray, sInactive);

        // 3. Combos
        var comboStandard = new ServiceCombo
        {
            ComboId = 1,
            ComboCode = "COMBO-STD",
            ComboName = "Gói Chăm Sóc Tiêu Chuẩn",
            TotalOriginalPrice = 500000m,
            DiscountedPrice = 400000m,
            IsActive = true,
            CreatedAt = now
        };
        var ci1 = new ServiceComboItem { ComboItemId = 1, ComboId = 1, DepartmentServiceId = 1, Quantity = 1, AllocatedUnitPrice = 80000m };
        var ci2 = new ServiceComboItem { ComboItemId = 2, ComboId = 1, DepartmentServiceId = 2, Quantity = 1, AllocatedUnitPrice = 200000m };
        var ci3 = new ServiceComboItem { ComboItemId = 3, ComboId = 1, DepartmentServiceId = 3, Quantity = 1, AllocatedUnitPrice = 120000m };
        comboStandard.ServiceComboItems.Add(ci1);
        comboStandard.ServiceComboItems.Add(ci2);
        comboStandard.ServiceComboItems.Add(ci3);

        var comboMultiDept = new ServiceCombo
        {
            ComboId = 2,
            ComboCode = "COMBO-CROSS",
            ComboName = "Gói Khám Toàn Diện Kèm X-quang",
            TotalOriginalPrice = 650000m,
            DiscountedPrice = 550000m,
            IsActive = true,
            CreatedAt = now
        };
        var ciCross1 = new ServiceComboItem { ComboItemId = 4, ComboId = 2, DepartmentServiceId = 1, Quantity = 1, AllocatedUnitPrice = 90000m };
        var ciCross2 = new ServiceComboItem { ComboItemId = 5, ComboId = 2, DepartmentServiceId = 4, Quantity = 1, AllocatedUnitPrice = 260000m };
        var ciCross3 = new ServiceComboItem { ComboItemId = 6, ComboId = 2, DepartmentServiceId = 2, Quantity = 1, AllocatedUnitPrice = 200000m };
        comboMultiDept.ServiceComboItems.Add(ciCross1);
        comboMultiDept.ServiceComboItems.Add(ciCross2);
        comboMultiDept.ServiceComboItems.Add(ciCross3);

        var comboMultiQty = new ServiceCombo
        {
            ComboId = 3,
            ComboCode = "COMBO-MULTI-QTY",
            ComboName = "Gói 2 Lần Cạo Vôi",
            TotalOriginalPrice = 500000m,
            DiscountedPrice = 450000m,
            IsActive = true,
            CreatedAt = now
        };
        var ciQty = new ServiceComboItem { ComboItemId = 7, ComboId = 3, DepartmentServiceId = 2, Quantity = 2, AllocatedUnitPrice = 225000m };
        comboMultiQty.ServiceComboItems.Add(ciQty);

        db.ServiceCombos.AddRange(comboStandard, comboMultiDept, comboMultiQty);

        // 4. Patient User
        var uPatient = new UserAccount { UserId = 10, Email = "patient10@dentalcare.local", Role = UserRole.Patient, Status = AccountStatus.Active, CreatedAt = now, UpdatedAt = now };
        var pProfile = new PatientProfile { UserId = 10, FullName = "Nguyễn Văn Bệnh Nhân", CreatedAt = now, UpdatedAt = now };
        db.UserAccounts.Add(uPatient);
        db.PatientProfiles.Add(pProfile);

        // 5. Dentists
        var uDentist1 = new UserAccount { UserId = 21, Email = "dentist21@dentalcare.local", Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = now, UpdatedAt = now };
        var sStaff1 = new StaffProfile { UserId = 21, FullName = "BS. Trần Tổng Quát", EmploymentStatus = "Active", CreatedAt = now, UpdatedAt = now };
        var dProfile1 = new DentistProfile { UserId = 21, DepartmentId = 1, Qualification = "Thạc sĩ RHM", YearsOfExperience = 8 };

        var uDentist2 = new UserAccount { UserId = 22, Email = "dentist22@dentalcare.local", Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = now, UpdatedAt = now };
        var sStaff2 = new StaffProfile { UserId = 22, FullName = "BS. Lê Hình Ảnh", EmploymentStatus = "Active", CreatedAt = now, UpdatedAt = now };
        var dProfile2 = new DentistProfile { UserId = 22, DepartmentId = 2, Qualification = "Chuyên khoa X-quang", YearsOfExperience = 5 };

        db.UserAccounts.AddRange(uDentist1, uDentist2);
        db.StaffProfiles.AddRange(sStaff1, sStaff2);
        db.DentistProfiles.AddRange(dProfile1, dProfile2);

        // 6. Receptionist
        var uRecep = new UserAccount { UserId = 31, Email = "recep31@dentalcare.local", Role = UserRole.Receptionist, Status = AccountStatus.Active, CreatedAt = now, UpdatedAt = now };
        var sRecep = new StaffProfile { UserId = 31, FullName = "Lễ Tân Mai", EmploymentStatus = "Active", CreatedAt = now, UpdatedAt = now };
        db.UserAccounts.Add(uRecep);
        db.StaffProfiles.Add(sRecep);

        // 7. Rooms & Chairs
        var room1 = new Room { RoomId = 1, DepartmentId = 1, RoomCode = "R-101", RoomName = "Phòng Khám Tổng Quát 1", RoomType = "Clinical", Status = "Active", CreatedAt = now };
        var room2 = new Room { RoomId = 2, DepartmentId = 2, RoomCode = "R-201", RoomName = "Phòng Chụp X-quang", RoomType = "Diagnostic", Status = "Active", CreatedAt = now };
        db.Rooms.AddRange(room1, room2);

        var chair1 = new DentalChair { DentalChairId = 1, RoomId = 1, ChairCode = "CHAIR-101-A", ChairName = "Ghế A1", Status = "Active", CreatedAt = now };
        var chair2 = new DentalChair { DentalChairId = 2, RoomId = 1, ChairCode = "CHAIR-101-B", ChairName = "Ghế A2", Status = "Active", CreatedAt = now };
        var chairXray = new DentalChair { DentalChairId = 3, RoomId = 2, ChairCode = "CHAIR-201-X", ChairName = "Ghế X-quang", Status = "Active", CreatedAt = now };
        db.DentalChairs.AddRange(chair1, chair2, chairXray);

        await db.SaveChangesAsync();
    }

    #region Group 1: Booking Expansion & Multi-Service (Cases A - F)

    [Fact]
    public async Task Test1_1_SingleRetailService_CaseA_ExpandsCorrectly()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1, // Khám tổng quát, 100k, 30m
            ScheduledStart = tomorrow,
            ReasonForVisit = "Khám răng định kỳ"
        };

        var response = await service.CreatePatientAppointmentAsync(10, request);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        var appt = await db.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == response.Data.AppointmentId);

        Assert.NotNull(appt);
        Assert.Single(appt.AppointmentServices);

        var sItem = appt.AppointmentServices.First();
        Assert.Equal(1, sItem.DepartmentId);
        Assert.Equal(1, sItem.DepartmentServiceId);
        Assert.Null(sItem.ComboId);
        Assert.Null(sItem.ComboItemId);
        Assert.Equal(100000m, sItem.UnitPrice);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, sItem.Status);
        Assert.Equal(tomorrow, sItem.ScheduledStart);
        Assert.Equal(tomorrow.AddMinutes(30), sItem.ScheduledEnd);
    }

    [Fact]
    public async Task Test1_2_MultiServiceRetail_CaseB_ViaAdditionalServiceIds_ExpandsSequentially()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1, // 30 mins
            AdditionalServiceIds = new List<long> { 2, 3 }, // 45 mins, 30 mins
            ScheduledStart = tomorrow,
            ReasonForVisit = "Khám tổng quát, cạo vôi và đánh bóng"
        };

        var response = await service.CreatePatientAppointmentAsync(10, request);

        Assert.True(response.Success);
        var appt = await db.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == response.Data!.AppointmentId);

        Assert.NotNull(appt);
        Assert.Equal(3, appt.AppointmentServices.Count);

        var items = appt.AppointmentServices.OrderBy(s => s.ScheduledStart).ToList();
        // Item 0: 09:00 - 09:30
        Assert.Equal(1, items[0].DepartmentServiceId);
        Assert.Equal(tomorrow, items[0].ScheduledStart);
        Assert.Equal(tomorrow.AddMinutes(30), items[0].ScheduledEnd);
        Assert.Null(items[0].ComboId);

        // Item 1: 09:30 - 10:15 (UQ_AppointmentServices_Appt_Start satisfied)
        Assert.Equal(2, items[1].DepartmentServiceId);
        Assert.Equal(tomorrow.AddMinutes(30), items[1].ScheduledStart);
        Assert.Equal(tomorrow.AddMinutes(75), items[1].ScheduledEnd);
        Assert.Null(items[1].ComboId);

        // Item 2: 10:15 - 10:45
        Assert.Equal(3, items[2].DepartmentServiceId);
        Assert.Equal(tomorrow.AddMinutes(75), items[2].ScheduledStart);
        Assert.Equal(tomorrow.AddMinutes(105), items[2].ScheduledEnd);
        Assert.Null(items[2].ComboId);

        // Distinct ScheduledStart verification
        var distinctStarts = items.Select(x => x.ScheduledStart).Distinct().Count();
        Assert.Equal(3, distinctStarts);
    }

    [Fact]
    public async Task Test1_3_ComboWithMultipleItems_CaseC_ExpandsAllItemsWithAllocatedPrices()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(14);
        var request = new CreateAppointmentRequest
        {
            ComboId = 1, // COMBO-STD: 3 items (Khám 80k, Cạo vôi 200k, Đánh bóng 120k)
            ScheduledStart = tomorrow,
            ReasonForVisit = "Đăng ký gói chăm sóc tiêu chuẩn"
        };

        var response = await service.CreatePatientAppointmentAsync(10, request);

        Assert.True(response.Success);
        var appt = await db.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == response.Data!.AppointmentId);

        Assert.NotNull(appt);
        Assert.Equal(1, appt.ComboId);
        Assert.Equal(3, appt.AppointmentServices.Count);

        var items = appt.AppointmentServices.OrderBy(s => s.ScheduledStart).ToList();
        Assert.Equal(1, items[0].ComboId);
        Assert.Equal(1, items[0].ComboItemId);
        Assert.Equal(80000m, items[0].UnitPrice);

        Assert.Equal(1, items[1].ComboId);
        Assert.Equal(2, items[1].ComboItemId);
        Assert.Equal(200000m, items[1].UnitPrice);

        Assert.Equal(1, items[2].ComboId);
        Assert.Equal(3, items[2].ComboItemId);
        Assert.Equal(120000m, items[2].UnitPrice);

        // Sequential timing
        Assert.Equal(tomorrow, items[0].ScheduledStart);
        Assert.Equal(items[0].ScheduledEnd, items[1].ScheduledStart);
        Assert.Equal(items[1].ScheduledEnd, items[2].ScheduledStart);
    }

    [Fact]
    public async Task Test1_4_ComboWithQuantityGreaterThanOne_CaseD_ExpandsMultipleInstances()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);
        var request = new CreateAppointmentRequest
        {
            ComboId = 3, // COMBO-MULTI-QTY has Quantity = 2 for Cạo vôi răng
            ScheduledStart = tomorrow,
            ReasonForVisit = "Đặt gói 2 lần cạo vôi"
        };

        var response = await service.CreatePatientAppointmentAsync(10, request);

        Assert.True(response.Success);
        var appt = await db.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == response.Data!.AppointmentId);

        Assert.NotNull(appt);
        Assert.Equal(2, appt.AppointmentServices.Count);

        var items = appt.AppointmentServices.OrderBy(s => s.ScheduledStart).ToList();
        Assert.Equal(items[0].ComboId, items[1].ComboId);
        Assert.Equal(items[0].ComboItemId, items[1].ComboItemId);
        Assert.Equal(225000m, items[0].UnitPrice);
        Assert.Equal(225000m, items[1].UnitPrice);
        Assert.True(items[1].ScheduledStart >= items[0].ScheduledEnd);
    }

    [Fact]
    public async Task Test1_5_ComboPlusRetail_CaseE_MaintainsComboPairConstraint()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);
        var request = new CreateAppointmentRequest
        {
            ComboId = 1, // 3 items (ComboId=1, ComboItemId set, AllocatedUnitPrice)
            AdditionalServiceIds = new List<long> { 4 }, // X-quang retail 300k (ComboId=null, ComboItemId=null)
            ScheduledStart = tomorrow,
            ReasonForVisit = "Combo kèm thêm chụp phim X-quang"
        };

        var response = await service.CreatePatientAppointmentAsync(10, request);

        Assert.True(response.Success);
        var appt = await db.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == response.Data!.AppointmentId);

        Assert.NotNull(appt);
        Assert.Equal(4, appt.AppointmentServices.Count);

        var items = appt.AppointmentServices.OrderBy(s => s.ScheduledStart).ToList();

        // 3 combo items satisfy ComboPair constraint
        for (int i = 0; i < 3; i++)
        {
            Assert.NotNull(items[i].ComboId);
            Assert.NotNull(items[i].ComboItemId);
        }

        // 1 retail item satisfies ComboPair constraint (both null)
        Assert.Null(items[3].ComboId);
        Assert.Null(items[3].ComboItemId);
        Assert.Equal(300000m, items[3].UnitPrice);
    }

    [Fact]
    public async Task Test1_6_MultiDepartmentCombo_CaseF_AssignsCorrectDepartmentPerItem()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(14);
        var request = new CreateAppointmentRequest
        {
            ComboId = 2, // COMBO-CROSS: Item 1 in Dept 1, Item 2 (Xray) in Dept 2, Item 3 in Dept 1
            ScheduledStart = tomorrow,
            ReasonForVisit = "Gói khám toàn diện đa chuyên khoa"
        };

        var response = await service.CreatePatientAppointmentAsync(10, request);

        Assert.True(response.Success);
        var appt = await db.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == response.Data!.AppointmentId);

        Assert.NotNull(appt);
        var items = appt.AppointmentServices.OrderBy(s => s.ScheduledStart).ToList();
        Assert.Equal(1, items[0].DepartmentId);
        Assert.Equal(2, items[1].DepartmentId); // Cross-department route to Chẩn đoán hình ảnh!
        Assert.Equal(1, items[2].DepartmentId);
    }

    [Fact]
    public async Task Test1_7_AtomicRollback_OnInvalidService_SavesNothing()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            AdditionalServiceIds = new List<long> { 99999 }, // Non-existent service ID
            ScheduledStart = tomorrow,
            ReasonForVisit = "Test atomic failure"
        };

        var response = await service.CreatePatientAppointmentAsync(10, request);

        Assert.False(response.Success);
        Assert.Contains("99999", response.Message);

        // Verify atomic rollback: no appointment or appointment services created
        Assert.Empty(db.Appointments);
        Assert.Empty(db.AppointmentServices);
    }

    #endregion

    #region Group 2: Mandatory Resource Assignment & Concurrency Conflict

    [Fact]
    public async Task Test2_1_Confirm_WithAllResources_Succeeds()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);
        var bookResp = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Khám răng"
        });

        var apptId = bookResp.Data!.AppointmentId;

        var confirmReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = 21, // Dentist in Dept 1
            RoomId = 1,                 // Room in Dept 1
            DentalChairId = 1           // Chair in Room 1
        };

        var confirmResp = await service.ManagerConfirmAsync(apptId, 1, confirmReq);

        Assert.True(confirmResp.Success);
        Assert.Equal(AppointmentStatus.Confirmed, confirmResp.Data!.Status);

        var confirmedItem = await db.AppointmentServices.FirstAsync(s => s.AppointmentId == apptId);
        Assert.Equal(21, confirmedItem.AssignedDentistUserId);
        Assert.Equal(1, confirmedItem.RoomId);
        Assert.Equal(1, confirmedItem.DentalChairId); // Mandatory chair set, CK_AppointmentServices_ResourceAllocation satisfied!
        Assert.Equal(AppointmentStatus.Confirmed, confirmedItem.Status);
    }

    [Fact]
    public async Task Test2_2_Confirm_WithMissingChair_Rejected()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);
        var bookResp = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Khám răng"
        });

        var apptId = bookResp.Data!.AppointmentId;

        var confirmReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = 21,
            RoomId = 1,
            DentalChairId = null // MISSING CHAIR!
        };

        var confirmResp = await service.ManagerConfirmAsync(apptId, 1, confirmReq);

        Assert.False(confirmResp.Success);
        Assert.Contains("Ghế nha khoa", confirmResp.Message);

        var appt = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, appt!.Status);
    }

    [Fact]
    public async Task Test2_3_Confirm_DentistWrongDepartment_Rejected()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);
        var bookResp = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Khám răng"
        });

        var apptId = bookResp.Data!.AppointmentId;

        var confirmReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = 22, // Dentist 22 is in Department 2, not Department 1!
            RoomId = 1,
            DentalChairId = 1
        };

        var confirmResp = await service.ManagerConfirmAsync(apptId, 1, confirmReq);

        Assert.False(confirmResp.Success);
        Assert.Contains("không thuộc chuyên khoa", confirmResp.Message);
    }

    [Fact]
    public async Task Test2_4_Confirm_RoomWrongDepartment_Rejected()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);
        var bookResp = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Khám răng"
        });

        var apptId = bookResp.Data!.AppointmentId;

        var confirmReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = 21,
            RoomId = 2, // Room 2 belongs to Department 2!
            DentalChairId = 1
        };

        var confirmResp = await service.ManagerConfirmAsync(apptId, 1, confirmReq);

        Assert.False(confirmResp.Success);
        Assert.Contains("Phòng khám không hợp lệ", confirmResp.Message);
    }

    [Fact]
    public async Task Test2_5_Confirm_ChairWrongRoom_Rejected()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);
        var bookResp = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Khám răng"
        });

        var apptId = bookResp.Data!.AppointmentId;

        var confirmReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = 21,
            RoomId = 1,
            DentalChairId = 3 // Chair 3 belongs to Room 2, not Room 1! (FK_AppointmentServices_Chair_Room)
        };

        var confirmResp = await service.ManagerConfirmAsync(apptId, 1, confirmReq);

        Assert.False(confirmResp.Success);
        Assert.Contains("không thuộc phòng khám đã chọn", confirmResp.Message);
    }

    [Fact]
    public async Task Test2_6_Confirm_DentistScheduleConflict_Rejected()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);

        // Appt 1: 09:00 - 09:30, Confirmed for Dentist 21
        var book1 = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Lịch 1"
        });
        await service.ManagerConfirmAsync(book1.Data!.AppointmentId, 1, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 21,
            RoomId = 1,
            DentalChairId = 1
        });

        // Appt 2: 09:15 - 09:45, attempting to confirm for Dentist 21 (overlaps with 09:00 - 09:30)
        var book2 = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow.AddHours(4), // Book afternoon to avoid patient duplicate rule
            ReasonForVisit = "Lịch 2"
        });

        // Try to confirm Appt 2 in conflicting morning slot: 09:15 - 09:45
        var confirmResp = await service.ManagerConfirmAsync(book2.Data!.AppointmentId, 1, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 21,
            RoomId = 1,
            DentalChairId = 2, // Different chair
            ScheduledStart = tomorrow.AddMinutes(15),
            ScheduledEnd = tomorrow.AddMinutes(45)
        });

        Assert.False(confirmResp.Success);
        Assert.Contains("Bác sĩ đã có lịch hẹn khác", confirmResp.Message);
    }

    [Fact]
    public async Task Test2_7_Confirm_DentistOnApprovedLeave_Rejected()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);
        var tomorrowDateOnly = DateOnly.FromDateTime(tomorrow);

        // Seed approved leave for Dentist 21 tomorrow
        var leave = new StaffLeaveRequest
        {
            StaffUserId = 21,
            StaffRole = "Dentist",
            LeaveType = "Annual",
            StartDate = tomorrowDateOnly,
            EndDate = tomorrowDateOnly,
            Reason = "Nghỉ phép thường niên",
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        var book = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Khám răng"
        });

        var confirmResp = await service.ManagerConfirmAsync(book.Data!.AppointmentId, 1, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 21,
            RoomId = 1,
            DentalChairId = 1
        });

        Assert.False(confirmResp.Success);
        Assert.Contains("nghỉ phép", confirmResp.Message);
    }

    [Fact]
    public async Task Test2_8_Confirm_ChairScheduleConflict_Rejected()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);

        // Appt 1: Chair 1 confirmed
        var book1 = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Lịch 1"
        });
        await service.ManagerConfirmAsync(book1.Data!.AppointmentId, 1, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 21,
            RoomId = 1,
            DentalChairId = 1
        });

        // Seed Dentist 23 in Department 1 for Appt 2 so dentist is not conflicting
        var now = DateTime.UtcNow;
        var uDentist3 = new UserAccount { UserId = 23, Email = "dentist23@dentalcare.local", Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = now };
        var sStaff3 = new StaffProfile { UserId = 23, FullName = "BS. Phạm Nha Khoa", EmploymentStatus = "Active", CreatedAt = now };
        var dProfile3 = new DentistProfile { UserId = 23, DepartmentId = 1, Qualification = "Bác sĩ RHM", YearsOfExperience = 4 };
        db.UserAccounts.Add(uDentist3);
        db.StaffProfiles.Add(sStaff3);
        db.DentistProfiles.Add(dProfile3);
        await db.SaveChangesAsync();

        // Appt 2: Trying to confirm with same Chair 1 at conflicting time with non-conflicting Dentist 23
        var book2 = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow.AddHours(4),
            ReasonForVisit = "Lịch 2"
        });

        var confirmResp = await service.ManagerConfirmAsync(book2.Data!.AppointmentId, 1, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 23, // Different dentist
            RoomId = 1,
            DentalChairId = 1,          // Conflicting chair!
            ScheduledStart = tomorrow.AddMinutes(15),
            ScheduledEnd = tomorrow.AddMinutes(45)
        });

        Assert.False(confirmResp.Success);
        Assert.Contains("Ghế nha khoa đã được phân bổ", confirmResp.Message);
    }

    #endregion

    #region Group 3: Check-in & Queue Number Allocation

    [Fact]
    public async Task Test3_1_CheckIn_MultipleServices_AssignsQueueNumberToAllServices()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);

        // Combo appointment with 3 services
        var book = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            ComboId = 1,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Gói khám 3 dịch vụ"
        });
        var apptId = book.Data!.AppointmentId;

        // Confirm appointment
        await service.ManagerConfirmAsync(apptId, 1, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 21,
            RoomId = 1,
            DentalChairId = 1
        });

        // Check-in
        var checkInResp = await service.ReceptionistCheckInAsync(apptId, 31, "Bệnh nhân đã đến quầy");
        Assert.True(checkInResp.Success);

        var appt = await db.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == apptId);

        Assert.NotNull(appt);
        Assert.Equal(AppointmentStatus.CheckedIn, appt.Status);

        // Every single service MUST have QueueNumber non-null and > 0 (satisfying CK_AppointmentServices_QueueRequired)
        foreach (var item in appt.AppointmentServices)
        {
            Assert.Equal(AppointmentStatus.CheckedIn, item.Status);
            Assert.NotNull(item.QueueNumber);
            Assert.True(item.QueueNumber > 0);
        }
    }

    [Fact]
    public async Task Test3_2_QueueNumber_IncrementsCorrectly_WithoutDuplicates()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);

        // Seed second patient
        var uPat2 = new UserAccount { UserId = 11, Email = "pat2@local.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var pProf2 = new PatientProfile { UserId = 11, FullName = "Bệnh nhân 2" };
        db.UserAccounts.Add(uPat2);
        db.PatientProfiles.Add(pProf2);
        await db.SaveChangesAsync();

        // Appointment 1
        var b1 = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Khám 1"
        });
        await service.ManagerConfirmAsync(b1.Data!.AppointmentId, 1, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 21,
            RoomId = 1,
            DentalChairId = 1
        });
        await service.ReceptionistCheckInAsync(b1.Data!.AppointmentId, 31);

        // Appointment 2
        var b2 = await service.CreatePatientAppointmentAsync(11, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow.AddMinutes(30),
            ReasonForVisit = "Khám 2"
        });
        await service.ManagerConfirmAsync(b2.Data!.AppointmentId, 1, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 21,
            RoomId = 1,
            DentalChairId = 1
        });
        await service.ReceptionistCheckInAsync(b2.Data!.AppointmentId, 31);

        var item1 = await db.AppointmentServices.FirstAsync(s => s.AppointmentId == b1.Data!.AppointmentId);
        var item2 = await db.AppointmentServices.FirstAsync(s => s.AppointmentId == b2.Data!.AppointmentId);

        Assert.Equal(1, item1.QueueNumber);
        Assert.Equal(2, item2.QueueNumber);
    }

    #endregion

    #region Group 4: Reschedule & Status Sync

    [Fact]
    public async Task Test4_1_PatientReschedule_PreservesAllServices_AndUnitPrices()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);

        // Combo appointment with 3 items (Prices: 80k, 200k, 120k)
        var bookResp = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            ComboId = 1,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Combo gốc"
        });
        var oldApptId = bookResp.Data!.AppointmentId;

        // Reschedule to 3 days later at 14:00
        var newDate = ClinicTimeHelper.Now.Date.AddDays(3).AddHours(14);
        var reschedResp = await service.PatientRescheduleAsync(oldApptId, 10, new PatientRescheduleRequest
        {
            NewScheduledStart = newDate,
            Reason = "Bận việc đột xuất"
        });

        Assert.True(reschedResp.Success);
        var newApptId = reschedResp.Data!.AppointmentId;

        // Verify old appointment cancelled
        var oldAppt = await db.Appointments.Include(a => a.AppointmentServices).FirstAsync(a => a.AppointmentId == oldApptId);
        Assert.Equal(AppointmentStatus.Cancelled, oldAppt.Status);
        foreach (var s in oldAppt.AppointmentServices)
        {
            Assert.Equal(AppointmentStatus.Cancelled, s.Status);
        }

        // Verify new appointment has all 3 services preserved with snapshot unit prices
        var newAppt = await db.Appointments.Include(a => a.AppointmentServices).FirstAsync(a => a.AppointmentId == newApptId);
        Assert.Equal(1, newAppt.ComboId);
        Assert.Equal(3, newAppt.AppointmentServices.Count);

        var newItems = newAppt.AppointmentServices.OrderBy(s => s.ScheduledStart).ToList();
        Assert.Equal(1, newItems[0].ComboId);
        Assert.Equal(1, newItems[0].ComboItemId);
        Assert.Equal(80000m, newItems[0].UnitPrice); // PRESERVED!

        Assert.Equal(1, newItems[1].ComboId);
        Assert.Equal(2, newItems[1].ComboItemId);
        Assert.Equal(200000m, newItems[1].UnitPrice); // PRESERVED!

        Assert.Equal(1, newItems[2].ComboId);
        Assert.Equal(3, newItems[2].ComboItemId);
        Assert.Equal(120000m, newItems[2].UnitPrice); // PRESERVED!

        Assert.Equal(newDate, newItems[0].ScheduledStart);
        Assert.Equal(newItems[0].ScheduledEnd, newItems[1].ScheduledStart);
    }

    [Fact]
    public async Task Test4_2_CancelAndWithdraw_SyncsStatusAcrossAllItems()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);

        // Book 2 services
        var bookResp = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            AdditionalServiceIds = new List<long> { 2 },
            ScheduledStart = tomorrow,
            ReasonForVisit = "Đặt 2 dịch vụ"
        });
        var apptId = bookResp.Data!.AppointmentId;

        // Withdraw
        var withdrawResp = await service.PatientWithdrawAsync(apptId, 10, "Đổi ý không khám nữa");
        Assert.True(withdrawResp.Success);

        var appt = await db.Appointments.Include(a => a.AppointmentServices).FirstAsync(a => a.AppointmentId == apptId);
        Assert.Equal(AppointmentStatus.Withdrawn, appt.Status);
        foreach (var item in appt.AppointmentServices)
        {
            Assert.Equal(AppointmentStatus.Withdrawn, item.Status);
        }
    }

    #endregion

    #region Group 5 & 6E: DTO Mapping & Proposals

    [Fact]
    public async Task Test5_1_GetAppointmentDetail_ReturnsAllServicesAndProposals()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);

        // Combo appointment with 3 items
        var bookResp = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            ComboId = 1,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Khám combo"
        });
        var apptId = bookResp.Data!.AppointmentId;

        // Propose change on appointment
        var proposeResp = await service.ManagerProposeChangeAsync(apptId, 1, new ManagerProposeChangeRequest
        {
            ProposedDentistUserId = 21,
            ProposedStart = tomorrow.AddHours(2),
            ProposedEnd = tomorrow.AddHours(3),
            Reason = "Đổi sang buổi trưa do phòng bận"
        });
        Assert.True(proposeResp.Success);

        // Fetch detail
        var detailResp = await service.GetAppointmentDetailAsync(apptId, 10, UserRole.Patient);
        Assert.True(detailResp.Success);
        Assert.NotNull(detailResp.Data);

        var dto = detailResp.Data;
        Assert.Equal(AppointmentStatus.AwaitingPatientResponse, dto.Status);

        // All 3 services returned in Services list
        Assert.Equal(3, dto.Services.Count);
        Assert.Contains(dto.Services, s => s.DepartmentServiceId == 1);
        Assert.Contains(dto.Services, s => s.DepartmentServiceId == 2);
        Assert.Contains(dto.Services, s => s.DepartmentServiceId == 3);

        // Proposals returned
        Assert.NotEmpty(dto.Proposals);
        var proposal = dto.Proposals.First();
        Assert.Equal(21, proposal.ProposedDentistUserId);
        Assert.Contains("Đổi sang buổi trưa", proposal.Reason);
    }

    #endregion

    #region Group 6A - 6D: Integration Scenarios

    [Fact]
    public async Task Test6A_EndToEnd_WalkInAppointmentCreation_AndAutoConfirm()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);
        var req = new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 10,
            DepartmentId = 1,
            RequestedServiceId = 1,
            AdditionalServiceIds = new List<long> { 2 },
            AssignedDentistUserId = 21,
            RoomId = 1,
            DentalChairId = 1,
            AutoConfirm = true,
            AutoCheckIn = true,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Tiếp nhận tại quầy và check-in ngay"
        };

        var resp = await service.CreateReceptionistAppointmentAsync(31, req);
        Assert.True(resp.Success);
        Assert.Equal(AppointmentStatus.CheckedIn, resp.Data!.Status);

        var appt = await db.Appointments.Include(a => a.AppointmentServices).FirstAsync(a => a.AppointmentId == resp.Data.AppointmentId);
        Assert.Equal(AppointmentStatus.CheckedIn, appt.Status);
        Assert.NotNull(appt.ConfirmedAt);
        Assert.NotNull(appt.CheckedInAt);

        // All items have assigned resources and queue numbers
        foreach (var item in appt.AppointmentServices)
        {
            Assert.Equal(21, item.AssignedDentistUserId);
            Assert.Equal(1, item.RoomId);
            Assert.Equal(1, item.DentalChairId);
            Assert.NotNull(item.QueueNumber);
            Assert.Equal(AppointmentStatus.CheckedIn, item.Status);
        }
    }

    [Fact]
    public async Task Test6D_ConcurrencyLock_PreventsOverlappingResourceBooking()
    {
        var (service, db, _) = CreateServiceFixture();
        await SeedClinicFixtureAsync(db);

        var tomorrow = ClinicTimeHelper.Now.Date.AddDays(1).AddHours(9);

        // Confirm Appt 1
        var b1 = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Appt 1"
        });

        var conf1 = await service.ManagerConfirmAsync(b1.Data!.AppointmentId, 1, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 21,
            RoomId = 1,
            DentalChairId = 1
        });
        Assert.True(conf1.Success);

        // Appt 2 tries to confirm with same dentist at same time -> must fail
        var b2 = await service.CreatePatientAppointmentAsync(10, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = tomorrow.AddHours(4),
            ReasonForVisit = "Appt 2"
        });

        var conf2 = await service.ManagerConfirmAsync(b2.Data!.AppointmentId, 1, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 21,
            RoomId = 1,
            DentalChairId = 2,
            ScheduledStart = tomorrow, // Exact conflict
            ScheduledEnd = tomorrow.AddMinutes(30)
        });

        Assert.False(conf2.Success);
        Assert.Contains("Bác sĩ đã có lịch hẹn khác", conf2.Message);
    }

    #endregion
}
