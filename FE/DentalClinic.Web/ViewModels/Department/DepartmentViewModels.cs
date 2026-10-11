using System.Collections.Generic;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Department;

public class DepartmentServicesViewModel
{
    public long? DepartmentId { get; set; }
    public string? Search { get; set; }
    public List<AdminDepartmentDto> Departments { get; set; } = new();
    public List<AdminDepartmentServiceDto> Services { get; set; } = new();
    public CreateDepartmentServiceRequest CreateForm { get; set; } = new();
}

public class DepartmentDentistsViewModel
{
    public long? DepartmentId { get; set; }
    public string? Search { get; set; }
    public List<AdminDepartmentDto> Departments { get; set; } = new();
    public List<AdminUserDto> Dentists { get; set; } = new();
    public List<DentistDepartmentAllocationDto> Allocations { get; set; } = new();
    public AssignDentistDepartmentRequest AssignForm { get; set; } = new();
}
