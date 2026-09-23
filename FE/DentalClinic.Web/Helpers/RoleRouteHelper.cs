namespace DentalClinic.Web.Helpers;

public static class RoleRouteHelper
{
    public record RoleRoute(string Action, string Controller, string UrlPath);

    public static RoleRoute GetDashboardRoute(string? role)
    {
        return role switch
        {
            "Patient" => new RoleRoute("Dashboard", "PatientDashboard", "/Patient/Dashboard"),
            "Receptionist" => new RoleRoute("Dashboard", "ReceptionistDashboard", "/Receptionist/Dashboard"),
            "Dentist" => new RoleRoute("Dashboard", "DentistDashboard", "/Dentist/Dashboard"),
            "DepartmentManager" => new RoleRoute("Dashboard", "DepartmentDashboard", "/Department/Dashboard"),
            "SystemAdministrator" => new RoleRoute("Dashboard", "AdminDashboard", "/Admin/Dashboard"),
            _ => new RoleRoute("Index", "Home", "/")
        };
    }

    public static string GetDashboardUrl(string? role) => GetDashboardRoute(role).UrlPath;
}
