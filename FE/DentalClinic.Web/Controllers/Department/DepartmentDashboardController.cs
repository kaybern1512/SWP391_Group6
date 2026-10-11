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
            Title = "Bàn Quản Lý Chuyên Khoa",
            Subtitle = "Giám sát nhân sự chuyên khoa, duyệt phân bổ ca khám, quản lý tiến độ phác đồ và lịch trực bác sĩ",
            StatCards = new List<StatCardItem>
            {
                new()
                {
                    Title = "Ca Khám Chờ Phân Bổ",
                    Value = "--",
                    Note = "Hàng đợi tiếp nhận chuyên khoa",
                    Icon = "bi-inbox",
                    ColorClass = "warning"
                },
                new()
                {
                    Title = "Bác Sĩ Đang Trực",
                    Value = "--",
                    Note = "Bác sĩ sẵn sàng tại ghế",
                    Icon = "bi-person-check",
                    ColorClass = "teal"
                },
                new()
                {
                    Title = "Ca Chuyển Khoa / Hội Chẩn",
                    Value = "--",
                    Note = "Yêu cầu từ khoa khác",
                    Icon = "bi-arrow-left-right",
                    ColorClass = "info"
                },
                new()
                {
                    Title = "Phác Đồ Đang Điều Trị",
                    Value = "--",
                    Note = "Kế hoạch điều trị hoạt động",
                    Icon = "bi-activity",
                    ColorClass = "primary"
                }
            },
            Cards = new List<DashboardCardItem>
            {
                new()
                {
                    Title = "Duyệt & Phân Bổ Ca Khám",
                    Icon = "bi-check2-square",
                    ColorClass = "primary",
                    Description = "Tiếp nhận các ca khám đăng ký chuyên khoa và phân bổ cho bác sĩ phụ trách.",
                    ActionText = "Phân Bổ Ca Khám",
                    ActionUrl = "/Department/Appointments/Requests"
                },
                new()
                {
                    Title = "Giám Sát Tiến Độ Điều Trị",
                    Icon = "bi-graph-up",
                    ColorClass = "teal",
                    Description = "Theo dõi toàn diện các phác đồ điều trị, tiến độ thực hiện và kết quả điều trị trong khoa.",
                    ActionText = "Xem Tiến Độ",
                    ActionUrl = "/Department/TreatmentProgress"
                },
                new()
                {
                    Title = "Lịch Trực Bác Sĩ Chuyên Khoa",
                    Icon = "bi-calendar-range",
                    ColorClass = "info",
                    Description = "Tổ chức ca trực lâm sàng theo tuần, phân công ghế nha khoa và điều động nhân sự.",
                    ActionText = "Quản Lý Lịch Trực",
                    ActionUrl = "/Department/Schedule"
                },
                new()
                {
                    Title = "Thông Báo Chuyên Khoa",
                    Icon = "bi-bell",
                    ColorClass = "warning",
                    Description = "Cập nhật các thông báo điều hành và thông tin chỉ đạo phòng khám.",
                    ActionText = "Xem Thông Báo",
                    ActionUrl = "/Notification"
                }
            },
            EmptyState = new EmptyStateViewModel
            {
                Title = "Không có ca khám chờ duyệt",
                Description = "Tất cả ca khám và yêu cầu chuyên khoa trong danh sách chờ đều đã được xử lý phân bổ.",
                Icon = "bi-diagram-3",
                ActionText = "Xem Lịch Trực Bác Sĩ",
                ActionUrl = "/Department/Schedule"
            }
        };

        return View("~/Views/Department/Dashboard.cshtml", vm);
    }
}
