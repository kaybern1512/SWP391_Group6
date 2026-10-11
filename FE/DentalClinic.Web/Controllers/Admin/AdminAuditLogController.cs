using System;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin/AuditLogs")]
public class AdminAuditLogController : Controller
{
    private readonly IAdminApiService _adminApi;

    public AdminAuditLogController(IAdminApiService adminApi)
    {
        _adminApi = adminApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? action = null,
        string? entityName = null,
        string? search = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 20)
    {
        var req = new AuditLogQueryRequest
        {
            Action = action,
            EntityName = entityName,
            Search = search,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            PageSize = pageSize
        };

        var logsResult = await _adminApi.GetAuditLogsAsync(req);
        var pagedLogs = logsResult.IsSuccess && logsResult.Data != null 
            ? logsResult.Data 
            : new PagedResult<AuditLogDto> { Page = page, PageSize = pageSize };

        var vm = new AuditLogsViewModel
        {
            PagedLogs = pagedLogs,
            Action = action,
            EntityName = entityName,
            Search = search,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            PageSize = pageSize
        };

        return View("~/Views/Admin/AuditLogs/Index.cshtml", vm);
    }
}
