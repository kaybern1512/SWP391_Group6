using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]
[Route("Patient")]
public class PatientDashboardController : Controller
{
    private readonly IAppointmentApiService? _appointmentApi;
    private readonly IBillingApiService? _billingApi;
    private readonly ITreatmentApiService? _treatmentApi;
    private readonly IPostCareApiService? _postCareApi;
    private readonly INotificationApiService? _notificationApi;
    private readonly IProfileApiService? _profileApi;

    public PatientDashboardController()
        : this(null, null, null, null, null, null)
    {
    }

    public PatientDashboardController(
        IAppointmentApiService? appointmentApi = null,
        IBillingApiService? billingApi = null,
        ITreatmentApiService? treatmentApi = null,
        IPostCareApiService? postCareApi = null,
        INotificationApiService? notificationApi = null,
        IProfileApiService? profileApi = null)
    {
        _appointmentApi = appointmentApi;
        _billingApi = billingApi;
        _treatmentApi = treatmentApi;
        _postCareApi = postCareApi;
        _notificationApi = notificationApi;
        _profileApi = profileApi;
    }

    [HttpGet("")]
    [HttpGet("Dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var fullName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Bệnh nhân";
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        var code = User.FindFirst("UserCode")?.Value;

        // Try to fetch updated profile code/name if available
        if (_profileApi != null)
        {
            try
            {
                var profileRes = await _profileApi.GetProfileAsync();
                if (profileRes.IsSuccess && profileRes.Data != null)
                {
                    if (!string.IsNullOrWhiteSpace(profileRes.Data.FullName)) fullName = profileRes.Data.FullName;
                    if (!string.IsNullOrWhiteSpace(profileRes.Data.Code)) code = profileRes.Data.Code;
                }
            }
            catch
            {
                // Fallback to claim values
            }
        }

        // Fetch Operational Data in Parallel/Async
        List<AppointmentDto> appointments = new();
        List<InvoiceDto> invoices = new();
        List<TreatmentPlanDto> plans = new();
        List<FollowUpScheduleDto> followUps = new();
        int unreadNotifCount = 0;

        if (_appointmentApi != null)
        {
            try
            {
                var apptRes = await _appointmentApi.GetPatientAppointmentsAsync();
                if (apptRes.IsSuccess && apptRes.Data != null)
                {
                    appointments = apptRes.Data;
                }
            }
            catch { }
        }

        if (_billingApi != null)
        {
            try
            {
                var invRes = await _billingApi.GetPatientInvoicesAsync();
                if (invRes.IsSuccess && invRes.Data != null)
                {
                    invoices = invRes.Data;
                }
            }
            catch { }
        }

        if (_treatmentApi != null)
        {
            try
            {
                var planRes = await _treatmentApi.GetPatientMyPlansAsync();
                if (planRes.IsSuccess && planRes.Data != null)
                {
                    plans = planRes.Data;
                }
            }
            catch { }
        }

        if (_postCareApi != null)
        {
            try
            {
                var fuRes = await _postCareApi.GetPatientFollowUpsAsync();
                if (fuRes.IsSuccess && fuRes.Data != null)
                {
                    followUps = fuRes.Data;
                }
            }
            catch { }
        }

        if (_notificationApi != null)
        {
            try
            {
                var notifRes = await _notificationApi.GetUnreadCountAsync();
                if (notifRes.IsSuccess)
                {
                    unreadNotifCount = notifRes.Data;
                }
            }
            catch { }
        }

        var clinicNow = PatientAppointmentController.GetClinicNow();
        var clinicToday = PatientAppointmentController.GetClinicToday();

        // Compute Operational Metrics
        var upcoming = appointments
            .Where(a => a.ScheduledStart.HasValue && a.ScheduledStart.Value >= clinicNow.AddMinutes(-30) &&
                        (a.Status == "Confirmed" || a.Status == "CheckedIn" || a.Status == "InService"))
            .OrderBy(a => a.ScheduledStart)
            .FirstOrDefault();

        var confirmedCount = appointments.Count(a => a.Status == "Confirmed");
        var pendingCount = appointments.Count(a => a.Status == "PendingReceptionReview" ||
                                                   a.Status == "PendingDepartmentReview" ||
                                                   a.Status == "AwaitingPatientResponse");

        var unpaidAmount = invoices
            .Where(i => i.Status == "Unpaid" || i.Status == "PartiallyPaid")
            .Sum(i => i.RemainingAmount);

        var activePlan = plans
            .FirstOrDefault(p => p.Status == "Active" || p.Status == "InProgress");

        var nextFollowUp = followUps
            .Where(f => f.Status == "Scheduled" && f.RecommendedDate >= clinicNow.Date)
            .OrderBy(f => f.RecommendedDate)
            .FirstOrDefault();

        // 4 KPI / Metric Stat Cards
        string upcomingCardValue = upcoming?.ScheduledStart?.ToString("dd/MM HH:mm") ?? (confirmedCount > 0 ? $"{confirmedCount} lịch" : "Chưa có");
        string upcomingCardNote = upcoming != null
            ? $"{upcoming.ServiceName} - {(string.IsNullOrWhiteSpace(upcoming.AssignedDentistName) ? "Chờ phân công" : upcoming.AssignedDentistName)}"
            : (confirmedCount > 0 ? "Bạn có lịch khám đã xác nhận" : "Không có lịch khám sắp diễn ra");

        string planCardValue = activePlan != null ? $"{activePlan.ProgressPercentage}%" : (plans.Count > 0 ? $"{plans.Count} phác đồ" : "Chưa có");
        string planCardNote = activePlan != null
            ? $"{activePlan.DepartmentName} ({activePlan.CompletedItemsCount}/{activePlan.TotalItemsCount} giai đoạn)"
            : "Chưa có phác đồ đang điều trị";

        string invoiceCardValue = unpaidAmount > 0 ? $"{unpaidAmount:N0} đ" : "0 đ";
        string invoiceCardNote = unpaidAmount > 0 ? "Cần hoàn tất tại quầy hoặc VietQR" : "Không có viện phí tồn đọng";

        string notifCardValue = unreadNotifCount.ToString();
        string notifCardNote = unreadNotifCount > 0 ? $"{unreadNotifCount} tin nhắn/nhắc nhở mới" : "Tất cả thông báo đã đọc";

        var vm = new DashboardViewModel
        {
            Role = "Patient",
            FullName = fullName,
            Email = email,
            Code = code,
            Title = "Cổng Thông Tin Bệnh Nhân",
            Subtitle = "Theo dõi lịch hẹn khám, hồ sơ bệnh án, đơn thuốc và tiến trình phác đồ điều trị",

            UpcomingAppointment = upcoming,
            ConfirmedAppointmentsCount = confirmedCount,
            PendingAppointmentsCount = pendingCount,
            UnpaidInvoicesAmount = unpaidAmount,
            ActiveTreatmentPlan = activePlan,
            NextFollowUp = nextFollowUp,
            UnreadNotificationCount = unreadNotifCount,

            StatCards = new List<StatCardItem>
            {
                new()
                {
                    Title = "Lịch Khám Sắp Tới",
                    Value = upcomingCardValue,
                    Note = upcomingCardNote,
                    Icon = "bi-calendar-event",
                    ColorClass = "primary"
                },
                new()
                {
                    Title = "Tiến Độ Điều Trị",
                    Value = planCardValue,
                    Note = planCardNote,
                    Icon = "bi-graph-up-arrow",
                    ColorClass = "teal"
                },
                new()
                {
                    Title = "Hóa Đơn Chờ Thanh Toán",
                    Value = invoiceCardValue,
                    Note = invoiceCardNote,
                    Icon = "bi-receipt",
                    ColorClass = unpaidAmount > 0 ? "danger" : "warning"
                },
                new()
                {
                    Title = "Thông Báo Chưa Đọc",
                    Value = notifCardValue,
                    Note = notifCardNote,
                    Icon = "bi-bell",
                    ColorClass = unreadNotifCount > 0 ? "danger" : "info"
                }
            },
            Cards = new List<DashboardCardItem>
            {
                new()
                {
                    Title = "Đặt Lịch Khám",
                    Icon = "bi-calendar-plus",
                    ColorClass = "primary",
                    Description = "Đăng ký khám nha khoa tổng quát hoặc chuyên sâu với đội ngũ bác sĩ.",
                    ActionText = "Đặt Lịch Ngay",
                    ActionUrl = "/Patient/Appointment/Book"
                },
                new()
                {
                    Title = "Lịch Khám Của Tôi",
                    Icon = "bi-calendar3",
                    ColorClass = "teal",
                    Description = "Xem danh sách các ca khám sắp tới, theo dõi duyệt và lịch sử thăm khám.",
                    ActionText = "Xem Lịch Khám",
                    ActionUrl = "/Patient/Appointment",
                    BadgeText = pendingCount > 0 ? $"{pendingCount} chờ xử lý" : null
                },
                new()
                {
                    Title = "Khám Phá Dịch Vụ",
                    Icon = "bi-stars",
                    ColorClass = "info",
                    Description = "Tra cứu bảng giá niêm yết, thời lượng dự kiến và danh sách bác sĩ chuyên môn.",
                    ActionText = "Xem Dịch Vụ",
                    ActionUrl = "/Patient/Services"
                },
                new()
                {
                    Title = "Hồ Sơ Bệnh Án & Sơ Đồ Răng",
                    Icon = "bi-journal-medical",
                    ColorClass = "primary",
                    Description = "Tra cứu hồ sơ bệnh án điện tử, tiền sử bệnh và sơ đồ 32 răng chuẩn FDI.",
                    ActionText = "Xem Bệnh Án",
                    ActionUrl = "/Patient/MedicalRecord"
                },
                new()
                {
                    Title = "Kế Hoạch Điều Trị",
                    Icon = "bi-card-checklist",
                    ColorClass = "info",
                    Description = "Theo dõi các giai đoạn điều trị, thủ thuật lâm sàng và tiến độ điều trị.",
                    ActionText = "Xem Kế Hoạch",
                    ActionUrl = "/Patient/TreatmentPlans"
                },
                new()
                {
                    Title = "Đơn Thuốc Điện Tử",
                    Icon = "bi-capsule",
                    ColorClass = "danger",
                    Description = "Xem danh sách đơn thuốc được bác sĩ kê sau các ca khám và in đơn thuốc.",
                    ActionText = "Xem Đơn Thuốc",
                    ActionUrl = "/Patient/Prescription"
                },
                new()
                {
                    Title = "Hóa Đơn & Thanh Toán",
                    Icon = "bi-receipt-cutoff",
                    ColorClass = "warning",
                    Description = "Tra cứu viện phí thực tế, lịch sử giao dịch và thanh toán tiện lợi qua VietQR.",
                    ActionText = "Xem Hóa Đơn",
                    ActionUrl = "/Patient/Invoices",
                    BadgeText = unpaidAmount > 0 ? "Chưa thanh toán" : null
                },
                new()
                {
                    Title = "Lịch Hẹn Tái Khám",
                    Icon = "bi-calendar-heart",
                    ColorClass = "teal",
                    Description = "Xem khuyến nghị tái khám của bác sĩ và đặt lịch tái khám thuận tiện.",
                    ActionText = "Xem Lịch Tái Khám",
                    ActionUrl = "/Patient/FollowUps"
                },
                new()
                {
                    Title = "Thông Báo Hệ Thống",
                    Icon = "bi-bell",
                    ColorClass = "secondary",
                    Description = "Cập nhật các nhắc nhở lịch hẹn và hướng dẫn chăm sóc răng miệng.",
                    ActionText = "Xem Thông Báo",
                    ActionUrl = "/Notification",
                    BadgeText = unreadNotifCount > 0 ? $"{unreadNotifCount} mới" : null
                }
            },
            EmptyState = new EmptyStateViewModel
            {
                Title = "Chưa có lịch khám sắp tới",
                Description = "Bạn hiện chưa có lịch khám hoặc buổi điều trị nha khoa nào sắp diễn ra. Vui lòng bấm đặt lịch hẹn để được thăm khám.",
                Icon = "bi-calendar-check",
                ActionText = "Đặt Lịch Khám",
                ActionUrl = "/Patient/Appointment/Book"
            }
        };

        return View("~/Views/Patient/Dashboard.cshtml", vm);
    }
}
