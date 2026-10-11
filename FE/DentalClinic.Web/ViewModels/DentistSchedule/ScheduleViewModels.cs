using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.DentistSchedule;

public class DepartmentScheduleManageViewModel
{
    public long? DepartmentId { get; set; }
    public List<AdminDepartmentDto> Departments { get; set; } = new();
    public List<AdminUserDto> Dentists { get; set; } = new();
    public long? SelectedDentistUserId { get; set; }
    public DateOnly FromDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly ToDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
    public List<DentistWorkScheduleDto> Schedules { get; set; } = new();
    public CreateScheduleFormModel CreateForm { get; set; } = new();
}

public class CreateScheduleFormModel
{
    [Required(ErrorMessage = "Vui lòng chọn bác sĩ")]
    public long DentistUserId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn chuyên khoa")]
    public long DepartmentId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày làm việc")]
    public DateOnly WorkDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu")]
    public TimeOnly StartTime { get; set; } = new(8, 0);

    [Required(ErrorMessage = "Vui lòng chọn giờ kết thúc")]
    public TimeOnly EndTime { get; set; } = new(12, 0);

    [StringLength(500, ErrorMessage = "Ghi chú không quá 500 ký tự")]
    public string? Note { get; set; }
}

public class DentistPersonalScheduleViewModel
{
    public DateOnly FromDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-((int)DateTime.UtcNow.DayOfWeek == 0 ? 6 : (int)DateTime.UtcNow.DayOfWeek - 1))); // Monday
    public DateOnly ToDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));
    public List<DentistWorkScheduleDto> Schedules { get; set; } = new();
}

public class DentistAvailabilityViewModel
{
    public List<DentistAvailabilityDto> Availabilities { get; set; } = new();
    public CreateAvailabilityFormModel FormModel { get; set; } = new();
}

public class CreateAvailabilityFormModel
{
    [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu")]
    public DateTime StartDateTime { get; set; } = DateTime.UtcNow.AddHours(1);

    public DateTime? EndDateTime { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn trạng thái")]
    public string AvailabilityStatus { get; set; } = "OnLeave"; // Busy, OnLeave, Unavailable

    [StringLength(500, ErrorMessage = "Lý do không quá 500 ký tự")]
    public string? Reason { get; set; }
}
