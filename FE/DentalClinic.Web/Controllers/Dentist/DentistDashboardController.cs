using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Dentist;

[Authorize(Roles = "Dentist")]
[Route("Dentist")]
public class DentistDashboardController : Controller
{
    private readonly IImagingApiService? _imagingApiService;

    public DentistDashboardController()
    {
    }

    [ActivatorUtilitiesConstructor]
    public DentistDashboardController(IImagingApiService imagingApiService)
    {
        _imagingApiService = imagingApiService;
    }

    [HttpGet("")]
    [HttpGet("Dashboard")]
    public IActionResult Dashboard([FromQuery] string? mode = null)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        var isXrayDentist = email.Equals("dentist7@dentalcare.com", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "imaging", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(mode, "clinical", StringComparison.OrdinalIgnoreCase))
        {
            isXrayDentist = false;
        }

        if (isXrayDentist)
        {
            return BuildImagingWorkspaceView(email);
        }

        return BuildClinicalWorkspaceView();
    }

    private IActionResult BuildImagingWorkspaceView(string email)
    {
        ImagingQueueSummaryDto? queueSummary = null;
        XrayDentistRosterDto? activeDuty = null;

        try
        {
            if (_imagingApiService != null)
            {
                var dutyRes = _imagingApiService.GetMyXrayDutyAsync().GetAwaiter().GetResult();
                activeDuty = dutyRes?.Data?.FirstOrDefault(r => r.Status == "Active");

                var queueRes = _imagingApiService.GetQueueStatusAsync(9).GetAwaiter().GetResult();
                if (queueRes?.IsSuccess == true)
                {
                    queueSummary = queueRes.Data;
                }
            }
        }
        catch
        {
            // Graceful fallback
        }

        var waitingCount = queueSummary?.CurrentWaitingCount ?? 0;
        var inProgressCount = queueSummary?.CurrentInProgressCount ?? 0;
        var acquiredCount = queueSummary?.ActiveSessions?.Count(s => s.Status == "ImageAcquired" || s.Status == "ResultAvailable") ?? 0;

        var vm = new DashboardViewModel
        {
            Role = "Dentist",
            FullName = User.FindFirst(ClaimTypes.Name)?.Value ?? "BS. Đặng Quốc Khánh",
            Email = email,
            Code = User.FindFirst("UserCode")?.Value ?? (activeDuty?.RadiationCertNumber ?? "ATBX-2026-007"),
            Title = "Không Gian Chẩn Đoán Hình Ảnh",
            Subtitle = "Tiếp nhận chỉ định, thực hiện chụp, đọc kết quả và quản lý thư viện hình ảnh bệnh nhân",
            StatCards = new List<StatCardItem>
            {
                new()
                {
                    Title = "Ca Chờ Chụp",
                    Value = waitingCount.ToString(),
                    Note = "Chỉ định chờ tiếp nhận tại phòng XRAY-01",
                    Icon = "bi-hourglass-split",
                    ColorClass = "warning"
                },
                new()
                {
                    Title = "Đang Chụp",
                    Value = inProgressCount.ToString(),
                    Note = "Phiên chụp đang thực hiện trên Carestream",
                    Icon = "bi-play-circle",
                    ColorClass = "primary"
                },
                new()
                {
                    Title = "Ảnh Chờ Đọc",
                    Value = acquiredCount.ToString(),
                    Note = "Ảnh đã nhận chờ đọc & kết luận",
                    Icon = "bi-eye",
                    ColorClass = "teal"
                },
                new()
                {
                    Title = "Kết Quả Chờ Xác Nhận",
                    Value = "0",
                    Note = "Bản nháp chờ ký & đồng bộ EMR",
                    Icon = "bi-check2-circle",
                    ColorClass = "info"
                }
            },
            Cards = new List<DashboardCardItem>
            {
                new()
                {
                    Title = "Hàng Đợi Chụp X-Quang",
                    Icon = "bi-camera",
                    ColorClass = "primary",
                    Description = "Theo dõi danh sách chỉ định, số thứ tự hàng đợi và tiếp nhận ca chụp tại phòng XRAY-01.",
                    ActionText = "Vào Hàng Đợi",
                    ActionUrl = "/Dentist/XrayQueue"
                },
                new()
                {
                    Title = "Ca Đang Xử Lý & Chụp Ảnh",
                    Icon = "bi-play-circle",
                    ColorClass = "warning",
                    Description = "Ghi nhận thao tác chụp từ thiết bị Carestream CS 8200 3D và tiếp nhận hình ảnh kỹ thuật số.",
                    ActionText = "Xử Lý Ca Chụp",
                    ActionUrl = "/Dentist/XrayQueue?status=InProgress"
                },
                new()
                {
                    Title = "Đọc Phim & Kết Luận Chẩn Đoán",
                    Icon = "bi-file-earmark-medical",
                    ColorClass = "teal",
                    Description = "Kiểm tra chất lượng ảnh, nhập mô tả hình ảnh (Findings), kết luận (Impression) và ký xác nhận.",
                    ActionText = "Đọc Kết Quả",
                    ActionUrl = "/Dentist/XrayQueue?status=Acquired"
                },
                new()
                {
                    Title = "Thư Viện Phim & Hồ Sơ Bệnh Án",
                    Icon = "bi-journal-medical",
                    ColorClass = "info",
                    Description = "Tra cứu hình ảnh chẩn đoán kỹ thuật số, lịch sử chụp phim và hồ sơ bệnh án điện tử (EMR).",
                    ActionText = "Mở EMR",
                    ActionUrl = "/Dentist/MedicalRecords"
                }
            },
            EmptyState = new EmptyStateViewModel
            {
                Title = "Hiện chưa có ca chụp nào trong hàng đợi X-quang",
                Description = "Phòng XRAY-01 và thiết bị Carestream CS 8200 3D đang sẵn sàng tiếp nhận. Khi bác sĩ lâm sàng tạo chỉ định, bệnh nhân sẽ xuất hiện ở đây.",
                Icon = "bi-camera",
                ActionText = "Xem Hàng Đợi Chụp",
                ActionUrl = "/Dentist/XrayQueue"
            }
        };

        ViewBag.IsImagingWorkspace = true;
        ViewBag.ActiveDuty = activeDuty;
        ViewBag.QueueSummary = queueSummary;
        ViewBag.TargetRoomId = 9;

        return View("~/Views/Dentist/Dashboard.cshtml", vm);
    }

    private IActionResult BuildClinicalWorkspaceView()
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

        ViewBag.IsImagingWorkspace = false;
        return View("~/Views/Dentist/Dashboard.cshtml", vm);
    }
}
