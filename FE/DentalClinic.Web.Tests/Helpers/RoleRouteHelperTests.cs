using DentalClinic.Web.Helpers;
using Xunit;

namespace DentalClinic.Web.Tests.Helpers;

public class RoleRouteHelperTests
{
    [Theory]
    [InlineData("Patient", "Dashboard", "PatientDashboard", "/Patient/Dashboard")]
    [InlineData("Receptionist", "Dashboard", "ReceptionistDashboard", "/Receptionist/Dashboard")]
    [InlineData("Dentist", "Dashboard", "DentistDashboard", "/Dentist/Dashboard")]
    [InlineData("DepartmentManager", "Dashboard", "DepartmentDashboard", "/Department/Dashboard")]
    [InlineData("SystemAdministrator", "Dashboard", "AdminDashboard", "/Admin/Dashboard")]
    public void GetDashboardRoute_KnownRole_ReturnsExpectedRoute(string role, string expectedAction, string expectedController, string expectedUrl)
    {
        var route = RoleRouteHelper.GetDashboardRoute(role);

        Assert.Equal(expectedAction, route.Action);
        Assert.Equal(expectedController, route.Controller);
        Assert.Equal(expectedUrl, route.UrlPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("UnknownRole")]
    [InlineData("Guest")]
    public void GetDashboardRoute_UnknownOrNullRole_ReturnsHomeIndex(string? role)
    {
        var route = RoleRouteHelper.GetDashboardRoute(role);

        Assert.Equal("Index", route.Action);
        Assert.Equal("Home", route.Controller);
        Assert.Equal("/", route.UrlPath);
    }

    [Fact]
    public void GetDashboardUrl_MatchesRouteUrlPath()
    {
        Assert.Equal("/Patient/Dashboard", RoleRouteHelper.GetDashboardUrl("Patient"));
        Assert.Equal("/Receptionist/Dashboard", RoleRouteHelper.GetDashboardUrl("Receptionist"));
        Assert.Equal("/Dentist/Dashboard", RoleRouteHelper.GetDashboardUrl("Dentist"));
        Assert.Equal("/Department/Dashboard", RoleRouteHelper.GetDashboardUrl("DepartmentManager"));
        Assert.Equal("/Admin/Dashboard", RoleRouteHelper.GetDashboardUrl("SystemAdministrator"));
        Assert.Equal("/", RoleRouteHelper.GetDashboardUrl(null));
    }
}
