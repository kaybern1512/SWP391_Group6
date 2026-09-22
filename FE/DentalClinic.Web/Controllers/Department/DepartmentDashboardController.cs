using System.Security.Claims;
using DentalClinic.Web.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Department;

[Authorize(Roles = "DepartmentManager")]
[Route("Department")]
public class DepartmentDashboardController : Controller
{
    [HttpGet("")]
    [HttpGet("Dashboard")]
    public IActionResult Dashboard()
    {
        var vm = new DashboardViewModel
        {
            Role = "DepartmentManager",
            FullName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Department Manager",
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            Code = User.FindFirst("UserCode")?.Value,
            Title = "Department Clinical Management",
            Subtitle = "Supervise specialty staff, allocate clinical appointments, and manage cross-department referrals",
            StatCards = new List<StatCardItem>
            {
                new()
                {
                    Title = "Pending Assignment",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-inbox",
                    ColorClass = "warning"
                },
                new()
                {
                    Title = "Available Dentists",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-person-check",
                    ColorClass = "teal"
                },
                new()
                {
                    Title = "Pending Referrals",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-arrow-left-right",
                    ColorClass = "info"
                },
                new()
                {
                    Title = "Active Treatments",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-activity",
                    ColorClass = "primary"
                }
            },
            Cards = new List<DashboardCardItem>
            {
                new()
                {
                    Title = "Assign Dentist to Department",
                    Icon = "bi-person-plus",
                    ColorClass = "primary",
                    Description = "Onboard and allocate certified dental practitioners to specialty department teams.",
                    ActionText = "Assign Dentist",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Update Department Work Schedule",
                    Icon = "bi-calendar-range",
                    ColorClass = "info",
                    Description = "Organize weekly shift schedules, clinical duties, and operating chair rotations.",
                    ActionText = "Manage Schedule",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Review and Assign Appointment",
                    Icon = "bi-check2-square",
                    ColorClass = "warning",
                    Description = "Triage incoming patient appointment bookings to specialized department dentists.",
                    ActionText = "Assignment Queue",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Process Department Referral",
                    Icon = "bi-arrow-left-right",
                    ColorClass = "teal",
                    Description = "Review and accept incoming inter-specialty referrals from other departments.",
                    ActionText = "Review Referrals",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "View Department Treatment Progress",
                    Icon = "bi-graph-up",
                    ColorClass = "primary",
                    Description = "Monitor active clinical case completion, procedure outcomes, and quality metrics.",
                    ActionText = "View Progress",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Update Department Services & Pricing",
                    Icon = "bi-tags",
                    ColorClass = "success",
                    Description = "Maintain specialty clinical procedure catalog, estimated times, and fees.",
                    ActionText = "Service Catalog",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "View Department Performance Report",
                    Icon = "bi-file-earmark-bar-graph",
                    ColorClass = "danger",
                    Description = "Generate comprehensive reports on department caseloads, visits, and clinical output.",
                    ActionText = "View Report",
                    ActionUrl = "#"
                }
            },
            EmptyState = new EmptyStateViewModel
            {
                Title = "No pending department assignments",
                Description = "All incoming appointments and specialty referral requests have been triaged.",
                Icon = "bi-diagram-3"
            }
        };

        return View("~/Views/Department/Dashboard.cshtml", vm);
    }
}
