using System.Collections.Generic;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Services;

public class PatientServicesDiscoveryViewModel
{
    public List<DepartmentDto> Departments { get; set; } = new();
    public List<DepartmentServiceDto> Services { get; set; } = new();
    public long? SelectedDepartmentId { get; set; }
    public string? SearchTerm { get; set; }
    public Dictionary<long, List<DentistOptionDto>> DentistsByDepartment { get; set; } = new();
}
