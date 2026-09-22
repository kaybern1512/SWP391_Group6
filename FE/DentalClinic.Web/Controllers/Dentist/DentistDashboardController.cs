using System.Security.Claims;
using DentalClinic.Web.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Dentist;

[Authorize(Roles = "Dentist")]
[Route("Dentist")]
public class DentistDashboardController : Controller
{
    [HttpGet("")]
    [HttpGet("Dashboard")]
    public IActionResult Dashboard()
    {
        var vm = new DashboardViewModel
        {
            Role = "Dentist",
            FullName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Dentist",
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            Code = User.FindFirst("UserCode")?.Value,
            Title = "Clinical Dental Workspace",
            Subtitle = "Conduct patient consultations, update dental charts, and manage clinical treatment sessions",
            StatCards = new List<StatCardItem>
            {
                new()
                {
                    Title = "Today's Appointments",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-calendar3",
                    ColorClass = "primary"
                },
                new()
                {
                    Title = "Assigned Patients",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-people",
                    ColorClass = "teal"
                },
                new()
                {
                    Title = "Active Treatment Plans",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-clipboard-pulse",
                    ColorClass = "info"
                },
                new()
                {
                    Title = "Pending Follow-ups",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-clock-history",
                    ColorClass = "warning"
                }
            },
            Cards = new List<DashboardCardItem>
            {
                new()
                {
                    Title = "View Work Schedule",
                    Icon = "bi-calendar-week",
                    ColorClass = "primary",
                    Description = "Inspect your clinical duty shifts, operating chairs, and roster calendar.",
                    ActionText = "My Schedule",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Update Availability",
                    Icon = "bi-toggle-on",
                    ColorClass = "info",
                    Description = "Set your real-time status: Available, In Procedure, or On Leave.",
                    ActionText = "Set Availability",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Open Patient Medical Record",
                    Icon = "bi-journal-medical",
                    ColorClass = "teal",
                    Description = "Access electronic dental charts, diagnostic X-rays, and FDI Odontogram.",
                    ActionText = "Open EMR",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Create Treatment Plan",
                    Icon = "bi-diagram-3",
                    ColorClass = "warning",
                    Description = "Formulate multi-stage clinical procedures, materials, and cost estimates.",
                    ActionText = "New Plan",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Record Treatment Session",
                    Icon = "bi-clipboard2-check",
                    ColorClass = "primary",
                    Description = "Document executed procedures, medications administered, and teeth treated.",
                    ActionText = "Log Session",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Create Prescription",
                    Icon = "bi-capsule",
                    ColorClass = "danger",
                    Description = "Issue digital drug prescriptions with dosage and contraindication notes.",
                    ActionText = "New Prescription",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Create Department Referral",
                    Icon = "bi-arrow-left-right",
                    ColorClass = "info",
                    Description = "Request cross-specialty clinical consultations or transfer patient care.",
                    ActionText = "Refer Patient",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Create Follow-up Schedule",
                    Icon = "bi-calendar-plus",
                    ColorClass = "success",
                    Description = "Schedule postoperative evaluations, suture removal, or routine cleanings.",
                    ActionText = "Set Follow-up",
                    ActionUrl = "#"
                }
            },
            EmptyState = new EmptyStateViewModel
            {
                Title = "No patients currently in treatment queue",
                Description = "Your dental chair queue is currently clear. Admitted patients will appear here.",
                Icon = "bi-person-badge"
            }
        };

        return View("~/Views/Dentist/Dashboard.cshtml", vm);
    }
}
