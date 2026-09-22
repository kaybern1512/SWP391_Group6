using System.Security.Claims;
using DentalClinic.Web.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]
[Route("Patient")]
public class PatientDashboardController : Controller
{
    [HttpGet("")]
    [HttpGet("Dashboard")]
    public IActionResult Dashboard()
    {
        var vm = new DashboardViewModel
        {
            Role = "Patient",
            FullName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Patient",
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            Code = User.FindFirst("UserCode")?.Value,
            Title = "Personal Dental Care Portal",
            Subtitle = "Track your appointments, medical records, and multi-specialty treatment progress",
            StatCards = new List<StatCardItem>
            {
                new()
                {
                    Title = "Upcoming Appointment",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-calendar-event",
                    ColorClass = "primary"
                },
                new()
                {
                    Title = "Treatment Progress",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-graph-up-arrow",
                    ColorClass = "teal"
                },
                new()
                {
                    Title = "Outstanding Invoice",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-receipt",
                    ColorClass = "warning"
                },
                new()
                {
                    Title = "Unread Notifications",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-bell",
                    ColorClass = "info"
                }
            },
            Cards = new List<DashboardCardItem>
            {
                new()
                {
                    Title = "Book Appointment",
                    Icon = "bi-calendar-plus",
                    ColorClass = "primary",
                    Description = "Schedule a dental checkup or specialty consultation with clinic dentists.",
                    ActionText = "Book Now",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Medical Record",
                    Icon = "bi-journal-medical",
                    ColorClass = "teal",
                    Description = "View your electronic dental chart, diagnostic notes, and FDI Odontogram history.",
                    ActionText = "View Records",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Treatment Plans",
                    Icon = "bi-card-checklist",
                    ColorClass = "info",
                    Description = "Review approved treatment stages, clinical procedures, and expected timelines.",
                    ActionText = "View Plans",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Invoices & Payments",
                    Icon = "bi-credit-card-2-front",
                    ColorClass = "success",
                    Description = "Review service invoices, billed procedures, and official receipts.",
                    ActionText = "View Invoices",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Notifications",
                    Icon = "bi-bell",
                    ColorClass = "warning",
                    Description = "Stay updated with appointment reminders and clinical instructions.",
                    ActionText = "View Notifications",
                    ActionUrl = "#"
                }
            },
            EmptyState = new EmptyStateViewModel
            {
                Title = "No scheduled appointments found",
                Description = "You currently have no upcoming dental visits or active treatment sessions. Schedule an appointment to get started.",
                Icon = "bi-calendar-check",
                ActionText = "Book Appointment",
                ActionUrl = "#"
            }
        };

        return View("~/Views/Patient/Dashboard.cshtml", vm);
    }
}
