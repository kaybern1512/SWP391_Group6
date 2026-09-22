using System.Security.Claims;
using DentalClinic.Web.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist")]
[Route("Receptionist")]
public class ReceptionistDashboardController : Controller
{
    [HttpGet("")]
    [HttpGet("Dashboard")]
    public IActionResult Dashboard()
    {
        var vm = new DashboardViewModel
        {
            Role = "Receptionist",
            FullName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Receptionist",
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            Code = User.FindFirst("UserCode")?.Value,
            Title = "Patient Intake & Reception Desk",
            Subtitle = "Manage front-desk check-ins, appointment triage, and pending invoice generation",
            StatCards = new List<StatCardItem>
            {
                new()
                {
                    Title = "Pending Requests",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-inbox",
                    ColorClass = "warning"
                },
                new()
                {
                    Title = "Today's Appointments",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-calendar-check",
                    ColorClass = "primary"
                },
                new()
                {
                    Title = "Waiting Patients",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-people",
                    ColorClass = "teal"
                },
                new()
                {
                    Title = "Pending Invoices",
                    Value = "--",
                    Note = "Waiting for backend data",
                    Icon = "bi-file-earmark-text",
                    ColorClass = "danger"
                }
            },
            Cards = new List<DashboardCardItem>
            {
                new()
                {
                    Title = "Check In Patient",
                    Icon = "bi-person-check",
                    ColorClass = "primary",
                    Description = "Confirm patient arrival at the front desk and verify their scheduled clinic slot.",
                    ActionText = "Check In",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Find Patient",
                    Icon = "bi-search",
                    ColorClass = "info",
                    Description = "Look up existing patients by Patient Code, National ID card, or phone number.",
                    ActionText = "Search Records",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Create Patient",
                    Icon = "bi-person-plus",
                    ColorClass = "teal",
                    Description = "Register new walk-in patient demographics into the clinic database.",
                    ActionText = "Register Patient",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Create Appointment",
                    Icon = "bi-calendar-plus",
                    ColorClass = "warning",
                    Description = "Book a direct walk-in consultation or scheduled follow-up visit.",
                    ActionText = "New Appointment",
                    ActionUrl = "#"
                },
                new()
                {
                    Title = "Generate Invoice",
                    Icon = "bi-receipt-cutoff",
                    ColorClass = "success",
                    Description = "Generate official itemized billing invoices for completed dental treatments.",
                    ActionText = "Generate Invoice",
                    ActionUrl = "#"
                }
            },
            EmptyState = new EmptyStateViewModel
            {
                Title = "No waiting patients in intake queue",
                Description = "All confirmed patients have been admitted or no incoming visits are scheduled for this time slot.",
                Icon = "bi-inbox",
                ActionText = "Check In Patient",
                ActionUrl = "#"
            }
        };

        return View("~/Views/Receptionist/Dashboard.cshtml", vm);
    }
}
