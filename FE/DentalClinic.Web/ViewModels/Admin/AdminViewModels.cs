using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Admin;

public class AdminDashboardPageViewModel
{
    public string FullName { get; set; } = "System Administrator";
    public string Email { get; set; } = string.Empty;
    public string? Code { get; set; }
    public AdminDashboardStatsDto Stats { get; set; } = new();
}

public class DepartmentManageViewModel
{
    public List<AdminDepartmentDto> Departments { get; set; } = new();
    public string? StatusFilter { get; set; }
    public DepartmentFormModel FormModel { get; set; } = new();
    public List<AdminUserDto> PotentialManagers { get; set; } = new();
}

public class DepartmentFormModel
{
    public long? DepartmentId { get; set; }

    [Required(ErrorMessage = "Tên chuyên khoa là bắt buộc")]
    [StringLength(100, ErrorMessage = "Tên khoa không quá 100 ký tự")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả không quá 500 ký tự")]
    public string? Description { get; set; }

    public long? ManagerUserId { get; set; }

    public string Status { get; set; } = "Active";
}

public class ServiceManageViewModel
{
    public List<AdminDepartmentServiceDto> Services { get; set; } = new();
    public List<AdminDepartmentDto> Departments { get; set; } = new();
    public long? SelectedDepartmentId { get; set; }
    public string? SearchKeyword { get; set; }
    public ServiceFormModel FormModel { get; set; } = new();
}

public class ServiceFormModel
{
    public long? DepartmentServiceId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn chuyên khoa")]
    public long DepartmentId { get; set; }

    [Required(ErrorMessage = "Tên dịch vụ là bắt buộc")]
    [StringLength(150, ErrorMessage = "Tên dịch vụ không quá 150 ký tự")]
    public string ServiceName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả không quá 500 ký tự")]
    public string? Description { get; set; }

    [Range(5, 480, ErrorMessage = "Thời lượng thực hiện từ 5 đến 480 phút")]
    public int? DurationMinutes { get; set; } = 30;

    [Required(ErrorMessage = "Đơn giá là bắt buộc")]
    [Range(0, 1000000000, ErrorMessage = "Giá dịch vụ phải lớn hơn hoặc bằng 0")]
    public decimal Price { get; set; }

    public string Status { get; set; } = "Active";
}

public class RoomChairManageViewModel
{
    public List<AdminRoomDto> Rooms { get; set; } = new();
    public List<AdminDentalChairDto> Chairs { get; set; } = new();
    public List<AdminDepartmentDto> Departments { get; set; } = new();
    public long? SelectedRoomId { get; set; }
    public string ActiveTab { get; set; } = "rooms"; // "rooms" or "chairs"
    public RoomFormModel RoomForm { get; set; } = new();
    public ChairFormModel ChairForm { get; set; } = new();
}

public class RoomFormModel
{
    public long? RoomId { get; set; }

    [Required(ErrorMessage = "Mã phòng khám là bắt buộc")]
    [StringLength(20, ErrorMessage = "Mã phòng không quá 20 ký tự")]
    public string RoomCode { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Tên phòng không quá 100 ký tự")]
    public string? RoomName { get; set; }

    public long? DepartmentId { get; set; }

    public string Status { get; set; } = "Active";
}

public class ChairFormModel
{
    public long? DentalChairId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phòng khám")]
    public long RoomId { get; set; }

    [Required(ErrorMessage = "Mã ghế nha khoa là bắt buộc")]
    [StringLength(20, ErrorMessage = "Mã ghế không quá 20 ký tự")]
    public string ChairCode { get; set; } = string.Empty;

    public string Status { get; set; } = "Active";
}

public class AuditLogsViewModel
{
    public PagedResult<AuditLogDto> PagedLogs { get; set; } = new();
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public string? Search { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class UserManageViewModel
{
    public List<AdminUserDto> Users { get; set; } = new();
    public string? RoleFilter { get; set; }
    public string? SearchKeyword { get; set; }
}

public class AdminReportsViewModel
{
    public int SelectedYear { get; set; } = DateTime.UtcNow.Year;
    public RevenueReportDto RevenueReport { get; set; } = new();
    public AppointmentReportDto AppointmentReport { get; set; } = new();
}
