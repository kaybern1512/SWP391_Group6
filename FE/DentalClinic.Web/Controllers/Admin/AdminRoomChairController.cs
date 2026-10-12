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

    public AdminRoomChairController(IAdminApiService adminApi)
    {
        _adminApi = adminApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(long? roomId = null, string tab = "rooms")
    {
        var roomsResult = await _adminApi.GetRoomsAsync();
        var chairsResult = await _adminApi.GetChairsAsync(roomId);
        var deptsResult = await _adminApi.GetDepartmentsAsync();

        var rooms = roomsResult?.IsSuccess == true && roomsResult.Data != null ? roomsResult.Data : new();
        var chairs = chairsResult?.IsSuccess == true && chairsResult.Data != null ? chairsResult.Data : new();
        var depts = deptsResult?.IsSuccess == true && deptsResult.Data != null ? deptsResult.Data : new();

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
}
