using System.Collections.Generic;
using System.Security.Claims;
using DentalClinic.Web.Controllers.Dentist;
using DentalClinic.Web.Controllers.Department;
using DentalClinic.Web.Controllers.Patient;
using DentalClinic.Web.Controllers.Receptionist;
using DentalClinic.Web.ViewModels.Dashboard;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class DashboardWebControllerTests
{
    private static ControllerContext CreateContextWithUser(long userId, string role, string name, string? code = null)
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Email, $"{name.ToLowerInvariant()}@clinic.local")
        };
        if (!string.IsNullOrEmpty(code))
        {
            claims.Add(new Claim("UserCode", code));
        }
        var identity = new ClaimsIdentity(claims, "CookieAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        return new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public void PatientDashboard_ShouldReturnLocalizedModel_AndValidCardRoutes()
    {
        // Arrange
        var controller = new PatientDashboardController
        {
            ControllerContext = CreateContextWithUser(101, "Patient", "Nguyen Van An", "BN-101")
        };

        // Act
        var result = controller.Dashboard();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DashboardViewModel>().Subject;

        model.Role.Should().Be("Patient");
        model.FullName.Should().Be("Nguyen Van An");
        model.Title.Should().Be("Cổng Thông Tin Bệnh Nhân");
        model.Subtitle.Should().NotBeNullOrWhiteSpace();
        model.StatCards.Should().HaveCount(4);

        model.Cards.Should().NotBeEmpty();
        foreach (var card in model.Cards)
        {
            card.ActionUrl.Should().NotBeNullOrWhiteSpace();
            card.ActionUrl.Should().NotBe("#");
            card.ActionUrl.Should().StartWith("/");
        }

        // Verify specific key patient routes
        model.Cards.Should().Contain(c => c.ActionUrl == "/Patient/Appointment/Book");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Patient/Appointment");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Patient/MedicalRecord");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Patient/TreatmentPlans");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Patient/Prescription");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Notification");
    }

    [Fact]
    public void DentistDashboard_ShouldReturnLocalizedModel_AndValidCardRoutes()
    {
        // Arrange
        var controller = new DentistDashboardController
        {
            ControllerContext = CreateContextWithUser(201, "Dentist", "BS. Tran Minh", "CCHN-888")
        };

        // Act
        var result = controller.Dashboard();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DashboardViewModel>().Subject;

        model.Role.Should().Be("Dentist");
        model.FullName.Should().Be("BS. Tran Minh");
        model.Title.Should().Be("Không Gian Lâm Sàng Bác Sĩ");
        model.Subtitle.Should().NotBeNullOrWhiteSpace();
        model.StatCards.Should().HaveCount(4);

        model.Cards.Should().NotBeEmpty();
        foreach (var card in model.Cards)
        {
            card.ActionUrl.Should().NotBeNullOrWhiteSpace();
            card.ActionUrl.Should().NotBe("#");
            card.ActionUrl.Should().StartWith("/");
        }

        // Verify clinical routes
        model.Cards.Should().Contain(c => c.ActionUrl == "/Dentist/Appointments/Today");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Dentist/MedicalRecords");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Dentist/TreatmentPlans");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Dentist/TreatmentPlans/Sessions");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Dentist/Prescriptions");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Dentist/Schedule");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Dentist/Schedule/Availability");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Dentist/Schedule/Feedback");
    }

    [Fact]
    public void ReceptionistDashboard_ShouldReturnConsolidatedPatientRegistry_AndValidRoutes()
    {
        // Arrange
        var controller = new ReceptionistDashboardController
        {
            ControllerContext = CreateContextWithUser(301, "Receptionist", "Le Thi Huong", "LT-01")
        };

        // Act
        var result = controller.Dashboard();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DashboardViewModel>().Subject;

        model.Role.Should().Be("Receptionist");
        model.FullName.Should().Be("Le Thi Huong");
        model.Title.Should().Be("Bàn Tiếp Đón & Điều Phối Lễ Tân");
        model.StatCards.Should().HaveCount(4);

        model.Cards.Should().NotBeEmpty();
        foreach (var card in model.Cards)
        {
            card.ActionUrl.Should().NotBeNullOrWhiteSpace();
            card.ActionUrl.Should().NotBe("#");
            card.ActionUrl.Should().StartWith("/");
        }

        // Consolidated patient registry route
        model.Cards.Should().Contain(c => c.ActionUrl == "/Receptionist/Patients" && c.Title == "Sổ Quản Lý Bệnh Nhân");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Receptionist/Appointments/TodayCheckIn");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Receptionist/Appointments/Requests");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Notification");
    }

    [Fact]
    public void DepartmentDashboard_ShouldReturnLocalizedModel_AndValidRoutes()
    {
        // Arrange
        var controller = new DepartmentDashboardController
        {
            ControllerContext = CreateContextWithUser(401, "DepartmentManager", "Nguyen Van Truong", "KHOA-01")
        };

        // Act
        var result = controller.Dashboard();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DashboardViewModel>().Subject;

        model.Role.Should().Be("DepartmentManager");
        model.FullName.Should().Be("Nguyen Van Truong");
        model.Title.Should().Be("Bàn Quản Lý Chuyên Khoa");
        model.StatCards.Should().HaveCount(4);

        model.Cards.Should().NotBeEmpty();
        foreach (var card in model.Cards)
        {
            card.ActionUrl.Should().NotBeNullOrWhiteSpace();
            card.ActionUrl.Should().NotBe("#");
            card.ActionUrl.Should().StartWith("/");
        }

        model.Cards.Should().Contain(c => c.ActionUrl == "/Department/Appointments/Requests");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Department/TreatmentProgress");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Department/Schedule");
        model.Cards.Should().Contain(c => c.ActionUrl == "/Notification");
    }
}
