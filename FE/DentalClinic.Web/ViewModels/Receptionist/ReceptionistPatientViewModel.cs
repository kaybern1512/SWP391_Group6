using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Receptionist;

public class ReceptionistPatientListViewModel
{
    public List<AdminUserDto> Patients { get; set; } = new();
    public string? Search { get; set; }
    public WalkInPatientFormModel NewPatient { get; set; } = new();
}

public class WalkInPatientFormModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên bệnh nhân")]
    [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ email")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string PhoneNumber { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    public string? Gender { get; set; }
}
