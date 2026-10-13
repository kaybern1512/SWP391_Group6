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
            Title = "Bàn Tiếp Đón & Điều Phối Lễ Tân",
            Subtitle = "Quản lý check-in bệnh nhân, tiếp nhận yêu cầu đặt hẹn và mở sổ quản lý hồ sơ bệnh nhân",
            StatCards = new List<StatCardItem>
            {
                new()
                {
                    Title = "Yêu Cầu Chờ Xử Lý",
                    Value = "--",
                    Note = "Đặt hẹn trực tuyến & hotline",
                    Icon = "bi-inbox",
                    ColorClass = "warning"
                },
                new()
                {
                    Title = "Ca Khám Hôm Nay",
                    Value = "--",
                    Note = "Đã lên lịch trong ngày",
                    Icon = "bi-calendar-check",
                    ColorClass = "primary"
                },
                new()
                {
                    Title = "Bệnh Nhân Chờ Check-in",
                    Value = "--",
                    Note = "Hàng chờ sảnh tiếp tân",
                    Icon = "bi-people",
                    ColorClass = "teal"
                },
                new()
                {
                    Title = "Hóa Đơn Chờ Thu Phí",
                    Value = "--",
                    Note = "Viện phí sau điều trị",
                    Icon = "bi-file-earmark-text",
                    ColorClass = "danger"
                }
            },
            Cards = new List<DashboardCardItem>
            {
                new()
                {
                    Title = "Check-in Tiếp Nhận Bệnh Nhân",
                    Icon = "bi-person-check",
                    ColorClass = "primary",
                    Description = "Xác nhận bệnh nhân có mặt tại sảnh, đối soát khung giờ hẹn và chuyển vào ghế khám.",
                    ActionText = "Check-in Ngay",
                    ActionUrl = "/Receptionist/Appointments/TodayCheckIn"
                },
                new()
                {
                    Title = "Sổ Quản Lý Bệnh Nhân",
                    Icon = "bi-people",
                    ColorClass = "teal",
                    Description = "Tra cứu nhanh hồ sơ theo CCCD/SĐT, tiếp đón và tạo mới thông tin bệnh nhân đến khám.",
                    ActionText = "Mở Sổ Bệnh Nhân",
                    ActionUrl = "/Receptionist/Patients"
                },
                new()
                {
                    Title = "Yêu Cầu Đặt Lịch Hẹn",
                    Icon = "bi-calendar-check",
                    ColorClass = "warning",
                    Description = "Tiếp nhận và xử lý các yêu cầu đăng ký lịch hẹn mới từ bệnh nhân trên hệ thống.",
                    ActionText = "Xử Lý Lịch Hẹn",
                    ActionUrl = "/Receptionist/Appointments/Requests"
                },
                new()
                {
                    Title = "Hóa Đơn & Thu Viện Phí",
                    Icon = "bi-receipt-cutoff",
                    ColorClass = "success",
                    Description = "Lập hóa đơn ca khám hoàn thành, thu tiền mặt/chuyển khoản VietQR và in phiếu thu viện phí.",
                    ActionText = "Vào Thu Phí",
                    ActionUrl = "/Receptionist/Billing"
                },
                new()
                {
                    Title = "Thông Báo Điều Hành",
                    Icon = "bi-bell",
                    ColorClass = "info",
                    Description = "Theo dõi các thông báo điều phối ca khám và tin nhắn điều hành phòng khám.",
                    ActionText = "Xem Thông Báo",
                    ActionUrl = "/Notification"
                }
            },
            EmptyState = new EmptyStateViewModel
            {
                Title = "Không có bệnh nhân chờ check-in",
                Description = "Tất cả bệnh nhân trong khung giờ hiện tại đã được tiếp nhận hoặc chưa đến giờ khám.",
                Icon = "bi-inbox",
                ActionText = "Xem Danh Sách Ca Khám",
                ActionUrl = "/Receptionist/Appointments/TodayCheckIn"
            }
        };

        return View("~/Views/Receptionist/Dashboard.cshtml", vm);
    }
}
