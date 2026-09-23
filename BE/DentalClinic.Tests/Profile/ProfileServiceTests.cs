using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Profile.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Profile;

public class ProfileServiceTests
{
    private readonly Mock<IFileStorageService> _mockFileStorage = new();
    private readonly Mock<ILogger<ProfileService>> _mockLogger = new();
    private readonly Mock<ILogger<AuditLogService>> _mockAuditLogger = new();

    private DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private ProfileService CreateProfileService(DentalClinicDbContext dbContext)
    {
        var auditService = new AuditLogService(dbContext, _mockAuditLogger.Object);
        return new ProfileService(dbContext, _mockFileStorage.Object, auditService, _mockLogger.Object);
    }

    [Fact]
    public async Task GetProfileAsync_PatientUser_ReturnsPatientData()
    {
        using var db = CreateDbContext(nameof(GetProfileAsync_PatientUser_ReturnsPatientData));
        var user = new UserAccount
        {
            Email = "patient@dentalcare.com",
            PhoneNumber = "0912345678",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        db.PatientProfiles.Add(new PatientProfile
        {
            UserId = user.UserId,
            PatientCode = "PAT-202609-0010",
            FullName = "Le Thi C",
            Gender = "Female",
            DateOfBirth = new DateOnly(1995, 5, 20),
            Address = "123 Le Loi, Da Nang"
        });
        await db.SaveChangesAsync();

        var service = CreateProfileService(db);
        var result = await service.GetProfileAsync(user.UserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("patient@dentalcare.com", result.Data.Email);
        Assert.Equal("Le Thi C", result.Data.FullName);
        Assert.Equal("PAT-202609-0010", result.Data.Code);
        Assert.Equal(UserRole.Patient, result.Data.Role);
        Assert.Equal("Female", result.Data.Gender);
    }

    [Fact]
    public async Task GetProfileAsync_DentistUser_ReturnsDentistAndStaffData()
    {
        using var db = CreateDbContext(nameof(GetProfileAsync_DentistUser_ReturnsDentistAndStaffData));
        var user = new UserAccount
        {
            Email = "dentist@dentalcare.com",
            PhoneNumber = "0988776655",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        var staff = new StaffProfile
        {
            UserId = user.UserId,
            EmployeeCode = "EMP-001",
            FullName = "Dr. Hoang Minh",
            StaffType = StaffType.Dentist,
            Status = "Active"
        };
        db.StaffProfiles.Add(staff);
        await db.SaveChangesAsync();

        var dept = new Department
        {
            Name = "Khoa Răng Trẻ Em",
            Status = "Active"
        };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        var dentist = new DentistProfile
        {
            StaffId = staff.StaffId,
            LicenseNumber = "LIC-99999",
            Qualification = "Tiến sĩ Răng Hàm Mặt",
            YearsOfExperience = 12,
            Biography = "Chuyên gia chỉnh nha hàng đầu"
        };
        db.DentistProfiles.Add(dentist);
        await db.SaveChangesAsync();

        db.DentistDepartments.Add(new DentistDepartment
        {
            DentistId = dentist.DentistId,
            DepartmentId = dept.DepartmentId,
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var service = CreateProfileService(db);
        var result = await service.GetProfileAsync(user.UserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Dr. Hoang Minh", result.Data.FullName);
        Assert.Equal("EMP-001", result.Data.Code);
        Assert.Equal("LIC-99999", result.Data.LicenseNumber);
        Assert.Equal(12, result.Data.YearsOfExperience);
        Assert.Equal("Khoa Răng Trẻ Em", result.Data.DepartmentName);
    }

    [Fact]
    public async Task UpdateProfileAsync_ValidData_UpdatesPatientProfile()
    {
        using var db = CreateDbContext(nameof(UpdateProfileAsync_ValidData_UpdatesPatientProfile));
        var user = new UserAccount
        {
            Email = "patient2@dentalcare.com",
            PhoneNumber = "0911111111",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        var profile = new PatientProfile
        {
            UserId = user.UserId,
            PatientCode = "PAT-202609-0020",
            FullName = "Old Name"
        };
        db.PatientProfiles.Add(profile);
        await db.SaveChangesAsync();

        var service = CreateProfileService(db);
        var updateRequest = new UpdateProfileRequest
        {
            FullName = "Updated Name",
            PhoneNumber = "0922222222",
            Address = "456 Nguyen Hue",
            Gender = "Male",
            DateOfBirth = new DateTime(1990, 1, 1)
        };

        var result = await service.UpdateProfileAsync(user.UserId, updateRequest);
        Assert.True(result.Success);

        var updatedUser = await db.UserAccounts.Include(u => u.PatientProfile).FirstAsync(u => u.UserId == user.UserId);
        Assert.Equal("0922222222", updatedUser.PhoneNumber);
        Assert.Equal("Updated Name", updatedUser.PatientProfile!.FullName);
        Assert.Equal("456 Nguyen Hue", updatedUser.PatientProfile.Address);
    }

    [Fact]
    public async Task UpdateProfileAsync_DuplicatePhone_ReturnsFail()
    {
        using var db = CreateDbContext(nameof(UpdateProfileAsync_DuplicatePhone_ReturnsFail));
        var user1 = new UserAccount { Email = "u1@dentalcare.com", PhoneNumber = "0911111111", Role = UserRole.Patient, Status = AccountStatus.Active };
        var user2 = new UserAccount { Email = "u2@dentalcare.com", PhoneNumber = "0922222222", Role = UserRole.Patient, Status = AccountStatus.Active };
        db.UserAccounts.AddRange(user1, user2);
        await db.SaveChangesAsync();

        db.PatientProfiles.Add(new PatientProfile { UserId = user1.UserId, PatientCode = "PAT-01", FullName = "User 1" });
        await db.SaveChangesAsync();

        var service = CreateProfileService(db);
        var result = await service.UpdateProfileAsync(user1.UserId, new UpdateProfileRequest
        {
            FullName = "User 1",
            PhoneNumber = "0922222222" // Already used by user2
        });

        Assert.False(result.Success);
        Assert.Contains("Số điện thoại này đã được sử dụng", result.Message);
    }

    [Fact]
    public async Task UploadAvatarAsync_ValidFile_ReturnsPublicUrl()
    {
        using var db = CreateDbContext(nameof(UploadAvatarAsync_ValidFile_ReturnsPublicUrl));
        var user = new UserAccount { Email = "avataruser@dentalcare.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        _mockFileStorage.Setup(f => f.SaveAvatarAsync(It.IsAny<Stream>(), "avatar.png", "image/png", default))
            .ReturnsAsync("https://localhost:7350/uploads/avatars/test-avatar-guid.png");

        var service = CreateProfileService(db);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake-image-bytes"));

        var result = await service.UploadAvatarAsync(user.UserId, stream, "avatar.png", "image/png");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("https://localhost:7350/uploads/avatars/test-avatar-guid.png", result.Data.AvatarUrl);

        var updatedUser = await db.UserAccounts.FindAsync(user.UserId);
        Assert.Equal("https://localhost:7350/uploads/avatars/test-avatar-guid.png", updatedUser!.AvatarUrl);
    }
}
