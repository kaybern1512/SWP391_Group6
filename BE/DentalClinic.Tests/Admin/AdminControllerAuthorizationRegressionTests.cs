using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Api.Controllers;
using DentalClinic.Application.Features.Admin.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Admin;

public class AdminControllerAuthorizationRegressionTests
{
    [Fact]
    public void AdminController_ClassLevel_MustNotAllowReceptionistOrOtherStaff()
    {
        // Arrange
        var controllerType = typeof(AdminController);

        // Act
        var authAttr = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);

        var allowedRoles = authAttr.Roles.Split(',', StringSplitOptions.TrimEntries);
        Assert.Contains(UserRole.SystemAdministrator, allowedRoles);
        Assert.DoesNotContain("DepartmentManager", allowedRoles);
        Assert.DoesNotContain(UserRole.Receptionist, allowedRoles);
        Assert.DoesNotContain(UserRole.Dentist, allowedRoles);
        Assert.DoesNotContain(UserRole.Patient, allowedRoles);
    }

    [Fact]
    public void AdminController_GetUsers_MustNotAllowReceptionist()
    {
        // Arrange
        var method = typeof(AdminController).GetMethod(nameof(AdminController.GetUsers));

        // Act
        Assert.NotNull(method);
        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);
        var roles = authAttr.Roles.Split(',', StringSplitOptions.TrimEntries);
        Assert.DoesNotContain(UserRole.Receptionist, roles);
    }

    [Theory]
    [InlineData(nameof(AdminController.CreateDepartment))]
    [InlineData(nameof(AdminController.UpdateDepartment))]
    [InlineData(nameof(AdminController.ToggleDepartmentStatus))]
    [InlineData(nameof(AdminController.CreateService))]
    [InlineData(nameof(AdminController.UpdateService))]
    [InlineData(nameof(AdminController.ToggleServiceStatus))]
    [InlineData(nameof(AdminController.CreateRoom))]
    [InlineData(nameof(AdminController.UpdateRoom))]
    [InlineData(nameof(AdminController.UpdateRoomStatus))]
    [InlineData(nameof(AdminController.CreateChair))]
    [InlineData(nameof(AdminController.UpdateChair))]
    [InlineData(nameof(AdminController.UpdateChairStatus))]
    [InlineData(nameof(AdminController.ToggleUserLock))]
    [InlineData(nameof(AdminController.GetAuditLogs))]
    [InlineData(nameof(AdminController.GetDashboardStats))]
    public void AdminController_SensitiveEndpoints_MustBeStrictlyAdminOnly(string methodName)
    {
        // Arrange
        var method = typeof(AdminController).GetMethod(methodName);

        // Act
        Assert.NotNull(method);
        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        Assert.NotNull(authAttr);
        Assert.Equal(UserRole.SystemAdministrator, authAttr.Roles);
    }

    [Fact]
    public void AppointmentsController_GetReceptionistPatients_MustAllowOnlyReceptionistAndAdmin()
    {
        // Arrange
        var method = typeof(AppointmentsController).GetMethod("GetReceptionistPatients");

        // Act
        Assert.NotNull(method);
        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        var httpGetAttr = method.GetCustomAttribute<HttpGetAttribute>();

        // Assert
        Assert.NotNull(httpGetAttr);
        Assert.Equal("api/receptionist/patients", httpGetAttr.Template);

        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);
        var roles = authAttr.Roles.Split(',', StringSplitOptions.TrimEntries);
        Assert.Contains(UserRole.Receptionist, roles);
        Assert.Contains(UserRole.SystemAdministrator, roles);
        Assert.DoesNotContain("DepartmentManager", roles);
        Assert.DoesNotContain(UserRole.Dentist, roles);
        Assert.DoesNotContain(UserRole.Patient, roles);
    }

    [Fact]
    public async Task AppointmentService_GetPatientsAsync_MustReturnOnlyPatientAccounts()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new DentalClinicDbContext(options);

        // Seed Users with various roles
        dbContext.UserAccounts.AddRange(
            new UserAccount { UserId = 1, Email = "admin@clinic.vn", Role = UserRole.SystemAdministrator, Status = AccountStatus.Active },
            new UserAccount { UserId = 3, Email = "dentist@clinic.vn", Role = UserRole.Dentist, Status = AccountStatus.Active },
            new UserAccount { UserId = 4, Email = "receptionist@clinic.vn", Role = UserRole.Receptionist, Status = AccountStatus.Active },
            new UserAccount
            {
                UserId = 10,
                Email = "patient1@clinic.vn",
                PhoneNumber = "0901111222",
                Role = UserRole.Patient,
                Status = AccountStatus.Active,
                PatientProfile = new PatientProfile { FullName = "Nguyen Van A", PatientCode = "PAT-202610-0001" }
            },
            new UserAccount
            {
                UserId = 11,
                Email = "patient2@clinic.vn",
                PhoneNumber = "0903333444",
                Role = UserRole.Patient,
                Status = AccountStatus.Unverified,
                PatientProfile = new PatientProfile { FullName = "Tran Thi B", PatientCode = "PAT-202610-0002" }
            }
        );
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AppointmentService>>();
        var notifMock = new Mock<DentalClinic.Application.Common.Interfaces.INotificationService>();
        var service = new AppointmentService(dbContext, notifMock.Object, loggerMock.Object);

        // Act
        var result = await service.GetPatientsAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);

        // Ensure every returned user is strictly a Patient
        Assert.All(result.Data, u => Assert.Equal(UserRole.Patient, u.Role));
        Assert.DoesNotContain(result.Data, u => u.Role == UserRole.SystemAdministrator);
        Assert.DoesNotContain(result.Data, u => u.Role == "DepartmentManager");
        Assert.DoesNotContain(result.Data, u => u.Role == UserRole.Dentist);
        Assert.DoesNotContain(result.Data, u => u.Role == UserRole.Receptionist);

        // Test search filter
        var searchResult = await service.GetPatientsAsync("Nguyen Van A");
        Assert.True(searchResult.Success);
        Assert.NotNull(searchResult.Data);
        Assert.Single(searchResult.Data);
        Assert.Equal("Nguyen Van A", searchResult.Data[0].FullName);
    }
}
