using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Profile.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

public class AvatarUploadForm
{
    public IFormFile? Avatar { get; set; }
}

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    private bool TryGetUserId(out long userId)
    {
        var claimValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(claimValue, out userId);
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<UserProfileResponse>.Fail("Không xác định được danh tính người dùng."));
        }

        var result = await _profileService.GetProfileAsync(userId, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse.Fail("Không xác định được danh tính người dùng."));
        }

        var result = await _profileService.UpdateProfileAsync(userId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<AvatarUploadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AvatarUploadResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AvatarUploadResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadAvatar([FromForm] AvatarUploadForm form, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<AvatarUploadResponse>.Fail("Không xác định được danh tính người dùng."));
        }

        var avatar = form.Avatar;
        if (avatar == null || avatar.Length == 0)
        {
            return BadRequest(ApiResponse<AvatarUploadResponse>.Fail("Vui lòng chọn tập tin hình ảnh."));
        }

        if (avatar.Length > 5 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<AvatarUploadResponse>.Fail("Kích thước ảnh không được vượt quá 5 MB."));
        }

        var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExts.Contains(ext))
        {
            return BadRequest(ApiResponse<AvatarUploadResponse>.Fail("Định dạng ảnh không hợp lệ. Chỉ chấp nhận JPG, JPEG, PNG, WEBP."));
        }

        using var stream = avatar.OpenReadStream();
        var result = await _profileService.UploadAvatarAsync(userId, stream, avatar.FileName, avatar.ContentType, ct);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
