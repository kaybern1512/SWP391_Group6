using System;
using DentalClinic.Web.Controllers.Department;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Dentist;

/// <summary>
/// WF-20: Quyền tạo và quản lý bài viết chuyên môn nha khoa đã được chuyển giao cho Trưởng khoa (DepartmentManager).
/// Bác sĩ (Dentist) không còn trong danh sách Role được phép truy cập module này.
/// Kế thừa DepartmentBlogController để duy trì tính tương thích với hệ thống và unit tests.
/// </summary>
[Authorize(Roles = "DepartmentManager,SystemAdministrator")]
[Route("Dentist/Blog")]
public class DentistBlogController : DepartmentBlogController
{
    public DentistBlogController(IBlogApiService blogApi) : base(blogApi)
    {
    }
}
