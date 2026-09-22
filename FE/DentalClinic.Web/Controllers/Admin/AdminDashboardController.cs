using System.Security.Claims;
using DentalClinic.Web.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin")]
public class AdminDashboardController : Controller
{
    [HttpGet("")]
    [HttpGet("Dashboard")]
    public IActionResult Dashboard()
    {
        var vm = new DashboardViewModel
        {
            Role = "SystemAdministrator",
            FullName = User.FindFirst(ClaimTypes.Name)?.Value ?? "System Administrator",
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            Code = User.FindFirst("UserCode")?.Value,
            Title = "System Administration Console",
            Subtitle = "Manage staff accounts, clinic information, department structures, and system audit logs",
            StatCards = new List<StatCardItem>
            {
                new()
                {
                    Title = "Total Staff",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-people",
                    ColorClass = "primary"
                },
                new()
                {
                    Title = "Active Departments",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-hospital",
                    ColorClass = "teal"
                },
                new()
                {
                    Title = "System Health Status",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-cpu",
                    ColorClass = "info"
                },
                new()
                {
                    Title = "Audit Events",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-shield-check",
                    ColorClass = "warning"
                }
            },
            Cards = new List<DashboardCardItem>
            {
                new()
                {
                    Title = "Update Account Status",
                    Icon = "bi-person-lock",
                    ColorClass = "danger",
                    Description = "Activate, deactivate, or lock user and staff credentials across the system.",
                    ActionText = "Account Status",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Create Staff Account",
                    Icon = "bi-person-badge-fill",
                    ColorClass = "primary",
                    Description = "Provision new clinical and operational credentials for dentists, receptionists, and managers.",
                    ActionText = "Create Staff",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Update Clinic Information",
                    Icon = "bi-building-gear",
                    ColorClass = "teal",
                    Description = "Maintain legal clinic profile, primary address, phone numbers, and official policies.",
                    ActionText = "Clinic Info",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Create Department",
                    Icon = "bi-folder-plus",
                    ColorClass = "info",
                    Description = "Establish a new dental specialty department in the clinic organization structure.",
                    ActionText = "New Department",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Update Department",
                    Icon = "bi-pencil-square",
                    ColorClass = "warning",
                    Description = "Modify department titles, operational descriptions, and assigned department heads.",
                    ActionText = "Edit Department",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "View Clinic Performance Report",
                    Icon = "bi-bar-chart-line",
                    ColorClass = "success",
                    Description = "Review high-level clinic operation metrics, appointment statistics, and throughput.",
                    ActionText = "View Report",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "View Audit Logs",
                    Icon = "bi-shield-check",
                    ColorClass = "danger",
                    Description = "Inspect immutable security audit trails for authentication, records, and administrative events.",
                    ActionText = "Audit Logs",
                    ActionUrl = "#"
                }
            },
            EmptyState = new EmptyStateViewModel
            {
                Title = "No administrative alerts pending",
                Description = "All user accounts and system configuration services are currently stable.",
                Icon = "bi-shield-shaded"
            }
        };

        return View("~/Views/Admin/Dashboard.cshtml", vm);
    }
}
