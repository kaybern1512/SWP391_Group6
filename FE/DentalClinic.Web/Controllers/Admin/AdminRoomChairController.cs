using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin/Rooms")]
public class AdminRoomChairController : Controller
{
    private readonly IAdminApiService _adminApi;
    private readonly IImagingApiService _imagingApi;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public AdminRoomChairController(IAdminApiService adminApi, IImagingApiService imagingApi)
    {
        _adminApi = adminApi;
        _imagingApi = imagingApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(long? roomId = null, string tab = "rooms", string? rosterDate = null)
    {
        var roomsResult = await _adminApi.GetRoomsAsync();
        var chairsResult = await _adminApi.GetChairsAsync(roomId);
        var deptsResult = await _adminApi.GetDepartmentsAsync();

        var rooms = roomsResult?.IsSuccess == true && roomsResult.Data != null ? roomsResult.Data : new();
        var chairs = chairsResult?.IsSuccess == true && chairsResult.Data != null ? chairsResult.Data : new();
        var depts = deptsResult?.IsSuccess == true && deptsResult.Data != null ? deptsResult.Data : new();

        DateOnly targetRosterDate = DateOnly.TryParse(rosterDate, out var parsedDate) ? parsedDate : DateOnly.FromDateTime(DateTime.Today);
        var xrayRostersRes = await _imagingApi.GetRostersAsync(workDate: targetRosterDate);
        var qualifiedDentistsRes = await _imagingApi.GetQualifiedDentistsAsync();
        var equipmentsRes = await _imagingApi.GetAllEquipmentsAsync();

        ViewBag.XrayRosters = xrayRostersRes?.Data ?? new();
        ViewBag.QualifiedDentists = qualifiedDentistsRes?.Data ?? new();
        ViewBag.Equipments = equipmentsRes?.Data ?? new();
        ViewBag.RosterDate = targetRosterDate.ToString("yyyy-MM-dd");

        var vm = new RoomChairManageViewModel
        {
            Rooms = rooms,
            Chairs = chairs,
            Departments = depts,
            SelectedRoomId = roomId,
            ActiveTab = tab
        };

        return View("~/Views/Admin/RoomChair/Index.cshtml", vm);
    }

    [HttpPost("CreateRoom")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRoom(RoomFormModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu phòng khám không hợp lệ.";
            return RedirectToAction(nameof(Index), new { tab = "rooms" });
        }

        var req = new CreateRoomRequest
        {
            RoomCode = model.RoomCode.Trim().ToUpper(),
            RoomName = model.RoomName?.Trim(),
            DepartmentId = model.DepartmentId,
            Status = model.Status ?? "Active"
        };

        var result = await _adminApi.CreateRoomAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã tạo phòng khám '{model.RoomCode}' thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tạo phòng khám.";
        }

        return RedirectToAction(nameof(Index), new { tab = "rooms" });
    }

    [HttpPost("EditRoom")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRoom(RoomFormModel model)
    {
        if (!model.RoomId.HasValue || !ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu cập nhật phòng khám không hợp lệ.";
            return RedirectToAction(nameof(Index), new { tab = "rooms" });
        }

        var req = new UpdateRoomRequest
        {
            RoomCode = model.RoomCode.Trim().ToUpper(),
            RoomName = model.RoomName?.Trim(),
            DepartmentId = model.DepartmentId,
            Status = model.Status ?? "Active"
        };

        var result = await _adminApi.UpdateRoomAsync(model.RoomId.Value, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã cập nhật phòng khám '{model.RoomCode}' thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật phòng khám.";
        }

        return RedirectToAction(nameof(Index), new { tab = "rooms" });
    }

    [HttpPost("UpdateRoomStatus/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRoomStatus(long id, [FromForm] string status)
    {
        var result = await _adminApi.UpdateRoomStatusAsync(id, status);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái phòng khám thành '{status}'.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật trạng thái phòng khám.";
        }

        return RedirectToAction(nameof(Index), new { tab = "rooms" });
    }

    [HttpPost("CreateChair")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateChair(ChairFormModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu ghế nha khoa không hợp lệ.";
            return RedirectToAction(nameof(Index), new { tab = "chairs", roomId = model.RoomId });
        }

        var req = new CreateDentalChairRequest
        {
            RoomId = model.RoomId,
            ChairCode = model.ChairCode.Trim().ToUpper(),
            Status = model.Status ?? "Active"
        };

        var result = await _adminApi.CreateChairAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã thêm ghế nha khoa '{model.ChairCode}' thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể thêm ghế nha khoa.";
        }

        return RedirectToAction(nameof(Index), new { tab = "chairs", roomId = model.RoomId });
    }

    [HttpPost("EditChair")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditChair(ChairFormModel model)
    {
        if (!model.DentalChairId.HasValue || !ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu cập nhật ghế không hợp lệ.";
            return RedirectToAction(nameof(Index), new { tab = "chairs" });
        }

        var req = new UpdateDentalChairRequest
        {
            RoomId = model.RoomId,
            ChairCode = model.ChairCode.Trim().ToUpper(),
            Status = model.Status ?? "Active"
        };

        var result = await _adminApi.UpdateChairAsync(model.DentalChairId.Value, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã cập nhật ghế '{model.ChairCode}' thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật ghế.";
        }

        return RedirectToAction(nameof(Index), new { tab = "chairs", roomId = model.RoomId });
    }

    [HttpPost("UpdateChairStatus/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateChairStatus(long id, [FromForm] string status, [FromForm] long? roomId)
    {
        var result = await _adminApi.UpdateChairStatusAsync(id, status);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái ghế thành '{status}'.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật trạng thái ghế.";
        }

        return RedirectToAction(nameof(Index), new { tab = "chairs", roomId });
    }

    [HttpPost("AssignXrayRoster")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignXrayRoster(
        long roomId,
        long equipmentId,
        long dentistUserId,
        long? backupDentistUserId,
        string workDate,
        string shiftType,
        string startTime,
        string endTime,
        string? notes)
    {
        if (!DateOnly.TryParse(workDate, out var parsedDate) ||
            !TimeOnly.TryParse(startTime, out var startT) ||
            !TimeOnly.TryParse(endTime, out var endT))
        {
            TempData["ErrorMessage"] = "Thông tin ngày hoặc giờ trực không hợp lệ.";
            return RedirectToAction(nameof(Index), new { tab = "xray-roster" });
        }

        var req = new AssignXrayRosterRequest
        {
            RoomId = roomId,
            EquipmentId = equipmentId,
            DentistUserId = dentistUserId,
            BackupDentistUserId = backupDentistUserId,
            WorkDate = parsedDate,
            ShiftType = shiftType,
            StartTime = startT,
            EndTime = endT,
            Notes = notes
        };

        var result = await _imagingApi.AssignRosterAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Phân công bác sĩ trực phòng X-quang thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể phân công bác sĩ trực.";
        }

        return RedirectToAction(nameof(Index), new { tab = "xray-roster", rosterDate = workDate });
    }

    [HttpPost("ReplaceXrayDentist")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplaceXrayDentist(
        long rosterId,
        long newDentistUserId,
        long? newBackupDentistUserId,
        string reason,
        string? rosterDate)
    {
        var req = new ReplaceXrayDentistRequest
        {
            NewDentistUserId = newDentistUserId,
            NewBackupDentistUserId = newBackupDentistUserId,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Điều chuyển theo yêu cầu vận hành phòng khám" : reason.Trim()
        };

        var result = await _imagingApi.ReplaceDentistAsync(rosterId, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Điều chuyển bác sĩ trực phòng X-quang thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể điều chuyển bác sĩ trực.";
        }

        return RedirectToAction(nameof(Index), new { tab = "xray-roster", rosterDate });
    }

    [HttpPost("CancelXrayRoster")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelXrayRoster(long rosterId, string? reason, string? rosterDate)
    {
        var result = await _imagingApi.CancelRosterAsync(rosterId, reason ?? "Quản trị viên hủy ca trực");
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã hủy ca trực phòng X-quang thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hủy ca trực.";
        }

        return RedirectToAction(nameof(Index), new { tab = "xray-roster", rosterDate });
    }
}
