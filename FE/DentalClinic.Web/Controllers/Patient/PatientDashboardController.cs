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
            Title = "Cổng Thông Tin Bệnh Nhân",
            Subtitle = "Theo dõi lịch hẹn khám, hồ sơ bệnh án, đơn thuốc và tiến trình phác đồ điều trị",
            StatCards = new List<StatCardItem>
            {
                new()
                {
                    Title = "Lịch Khám Sắp Tới",
                    Value = "Chưa có",
                    Note = "Bạn chưa có lịch hẹn mới",
                    Icon = "bi-calendar-event",
                    ColorClass = "primary"
                },
                new()
                {
                    Title = "Tiến Độ Điều Trị",
                    Value = "Chưa có",
                    Note = "Chưa có phác đồ đang điều trị",
                    Icon = "bi-graph-up-arrow",
                    ColorClass = "teal"
                },
                new()
                {
                    Title = "Hóa Đơn Chờ Thanh Toán",
                    Value = "0 đ",
                    Note = "Không có viện phí tồn đọng",
                    Icon = "bi-receipt",
                    ColorClass = "warning"
                },
                new()
                {
                    Title = "Thông Báo Chưa Đọc",
                    Value = "0",
                    Note = "Tất cả thông báo đã đọc",
                    Icon = "bi-bell",
                    ColorClass = "info"
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
                    Description = "Xem danh sách các ca khám sắp tới và lịch sử những lần thăm khám.",
                    ActionText = "Xem Lịch Khám",
                    ActionUrl = "/Patient/Appointment"
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
                    Title = "Thông Báo Hệ Thống",
                    Icon = "bi-bell",
                    ColorClass = "warning",
                    Description = "Cập nhật các nhắc nhở lịch hẹn và hướng dẫn chăm sóc răng miệng.",
                    ActionText = "Xem Thông Báo",
                    ActionUrl = "/Notification"
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
