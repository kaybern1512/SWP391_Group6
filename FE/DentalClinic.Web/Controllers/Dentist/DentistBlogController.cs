using System;
using DentalClinic.Web.Controllers.Department;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Dentist;

/// <summary>
/// WF-20: Quyền tạo và quản lý bài viết chuyên môn nha khoa dành cho Bác sĩ (Dentist) và Lễ tân (Receptionist).
/// Quản trị viên (SystemAdministrator) kiểm duyệt và xuất bản.
/// </summary>
[Authorize(Roles = "Dentist,SystemAdministrator")]
[Route("Dentist/Blog")]
public class DentistBlogController : DepartmentBlogController
{
    public DentistBlogController(IBlogApiService blogApi) : base(blogApi)
    {
    }
}
