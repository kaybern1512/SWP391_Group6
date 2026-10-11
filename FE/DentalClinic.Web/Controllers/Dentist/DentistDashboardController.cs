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
            Title = "Không Gian Lâm Sàng Bác Sĩ",
            Subtitle = "Khám tư vấn bệnh nhân, cập nhật hồ sơ bệnh án, sơ đồ răng và thực hiện các buổi điều trị",
            StatCards = new List<StatCardItem>
            {
                new()
                {
                    Title = "Ca Khám Hôm Nay",
                    Value = "--",
                    Note = "Theo lịch hẹn đã xác nhận",
                    Icon = "bi-calendar3",
                    ColorClass = "primary"
                },
                new()
                {
                    Title = "Bệnh Nhân Phụ Trách",
                    Value = "--",
                    Note = "Hồ sơ bệnh án điện tử",
                    Icon = "bi-people",
                    ColorClass = "teal"
                },
                new()
                {
                    Title = "Kế Hoạch Đang Điều Trị",
                    Value = "--",
                    Note = "Phác đồ điều trị hoạt động",
                    Icon = "bi-clipboard-pulse",
                    ColorClass = "info"
                },
                new()
                {
                    Title = "Lịch Tái Khám Chờ Duyệt",
                    Value = "--",
                    Note = "Hẹn lại & theo dõi hậu phẫu",
                    Icon = "bi-clock-history",
                    ColorClass = "warning"
                }
            },
            Cards = new List<DashboardCardItem>
            {
                new()
                {
                    Title = "Ca Khám Hôm Nay",
                    Icon = "bi-calendar-check",
                    ColorClass = "primary",
                    Description = "Xem danh sách bệnh nhân tiếp nhận hôm nay tại ghế khám nha khoa.",
                    ActionText = "Vào Phòng Khám",
                    ActionUrl = "/Dentist/Appointments/Today"
                },
                new()
                {
                    Title = "Hồ Sơ Bệnh Án & Sơ Đồ Răng",
                    Icon = "bi-journal-medical",
                    ColorClass = "teal",
                    Description = "Tra cứu hồ sơ bệnh nhân, tiền sử bệnh và chẩn đoán trên sơ đồ 32 răng FDI.",
                    ActionText = "Mở Bệnh Án EMR",
                    ActionUrl = "/Dentist/MedicalRecords"
                },
                new()
                {
                    Title = "Phác Đồ & Kế Hoạch Điều Trị",
                    Icon = "bi-diagram-3",
                    ColorClass = "warning",
                    Description = "Thiết lập kế hoạch điều trị đa giai đoạn, danh mục thủ thuật và chi phí dự kiến.",
                    ActionText = "Quản Lý Phác Đồ",
                    ActionUrl = "/Dentist/TreatmentPlans"
                },
                new()
                {
                    Title = "Ghi Nhận Buổi Điều Trị",
                    Icon = "bi-clipboard2-check",
                    ColorClass = "primary",
                    Description = "Nhập tiến độ buổi điều trị, răng đã xử lý, thủ thuật lâm sàng và thuốc chỉ định.",
                    ActionText = "Ghi Nhật Ký Ca",
                    ActionUrl = "/Dentist/TreatmentPlans/Sessions"
                },
                new()
                {
                    Title = "Kê Đơn Thuốc Điện Tử",
                    Icon = "bi-capsule",
                    ColorClass = "danger",
                    Description = "Kê đơn thuốc điện tử gắn liền với ca khám, liều dùng và lời dặn bác sĩ.",
                    ActionText = "Kê Đơn Thuốc",
                    ActionUrl = "/Dentist/Prescriptions"
                },
                new()
                {
                    Title = "Lịch Làm Việc Của Tôi",
                    Icon = "bi-calendar-week",
                    ColorClass = "primary",
                    Description = "Kiểm tra lịch trực, ca làm việc theo tuần/tháng và vị trí ghế điều trị.",
                    ActionText = "Xem Lịch Biểu",
                    ActionUrl = "/Dentist/Schedule"
                },
                new()
                {
                    Title = "Trạng Thái & Nghỉ Phép",
                    Icon = "bi-toggle-on",
                    ColorClass = "info",
                    Description = "Cập nhật trạng thái trực ghế nha khoa (Sẵn sàng / Đang khám / Nghỉ phép).",
                    ActionText = "Cập Nhật Trạng Thái",
                    ActionUrl = "/Dentist/Schedule/Availability"
                },
                new()
                {
                    Title = "Đánh Giá Từ Bệnh Nhân",
                    Icon = "bi-star",
                    ColorClass = "success",
                    Description = "Xem điểm số hài lòng và phản hồi nhận xét của bệnh nhân sau ca khám.",
                    ActionText = "Xem Đánh Giá",
                    ActionUrl = "/Dentist/Schedule/Feedback"
                }
            },
            EmptyState = new EmptyStateViewModel
            {
                Title = "Hôm nay chưa có bệnh nhân trong hàng chờ khám",
                Description = "Ghế nha khoa đang sẵn sàng tiếp nhận. Bệnh nhân sau khi check-in tại lễ tân sẽ hiển thị ở đây.",
                Icon = "bi-person-badge",
                ActionText = "Xem Ca Khám Hôm Nay",
                ActionUrl = "/Dentist/Appointments/Today"
            }
        };

        return View("~/Views/Dentist/Dashboard.cshtml", vm);
    }
}
