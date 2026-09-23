using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Profile.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class ProfileService : IProfileService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(
        DentalClinicDbContext dbContext,
        IFileStorageService fileStorageService,
        IAuditLogService auditLogService,
        ILogger<ProfileService> logger)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<ApiResponse<UserProfileResponse>> GetProfileAsync(long userId, CancellationToken ct = default)
    {
        var user = await _dbContext.UserAccounts
            .Include(u => u.PatientProfile)
            .Include(u => u.StaffProfile)
                .ThenInclude(s => s!.DentistProfile)
                    .ThenInclude(d => d!.DentistDepartments)
                        .ThenInclude(dd => dd.Department)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);

        if (user == null)
        {
            return ApiResponse<UserProfileResponse>.Fail("Không tìm thấy thông tin người dùng.");
        }

        var response = new UserProfileResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role,
            Status = user.Status,
            AvatarUrl = user.AvatarUrl,
            EmailVerifiedAt = user.EmailVerifiedAt,
            CreatedAt = user.CreatedAt
        };

        if (user.PatientProfile != null)
        {
            var p = user.PatientProfile;
            response.FullName = p.FullName;
            response.Code = p.PatientCode;
            response.DateOfBirth = p.DateOfBirth.HasValue ? p.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue) : null;
            response.Gender = p.Gender;
            response.Address = p.Address;
            response.NationalId = p.NationalId;
            response.HealthInsuranceNumber = p.HealthInsuranceNumber;
            response.EmergencyContact = p.EmergencyContact;
        }
        else if (user.StaffProfile != null)
        {
            var s = user.StaffProfile;
            response.FullName = s.FullName;
            response.Code = s.EmployeeCode;

            if (s.DentistProfile != null)
            {
                var d = s.DentistProfile;
                response.LicenseNumber = d.LicenseNumber;
                response.Qualification = d.Qualification;
                response.YearsOfExperience = d.YearsOfExperience;
                response.Biography = d.Biography;

                var dept = d.DentistDepartments.FirstOrDefault()?.Department;
                response.DepartmentName = dept?.Name;
            }
        }
        else
        {
            response.FullName = user.Email;
        }

        return ApiResponse<UserProfileResponse>.Ok(response, "Lấy thông tin hồ sơ thành công.");
    }

    public async Task<ApiResponse> UpdateProfileAsync(long userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await _dbContext.UserAccounts
            .Include(u => u.PatientProfile)
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);

        if (user == null)
        {
            return ApiResponse.Fail("Không tìm thấy người dùng.");
        }

        // Check phone duplicate if updated
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && request.PhoneNumber.Trim() != user.PhoneNumber)
        {
            var phoneTrim = request.PhoneNumber.Trim();
            var phoneTaken = await _dbContext.UserAccounts
                .AnyAsync(u => u.PhoneNumber == phoneTrim && u.UserId != userId, ct);
            if (phoneTaken)
            {
                return ApiResponse.Fail("Số điện thoại này đã được sử dụng bởi một tài khoản khác.");
            }
            user.PhoneNumber = phoneTrim;
        }

        if (user.PatientProfile != null)
        {
            var p = user.PatientProfile;
            p.FullName = request.FullName.Trim();
            p.DateOfBirth = request.DateOfBirth.HasValue ? DateOnly.FromDateTime(request.DateOfBirth.Value) : null;
            p.Gender = request.Gender;
            p.Address = request.Address;
            p.NationalId = request.NationalId;
            p.HealthInsuranceNumber = request.HealthInsuranceNumber;
            p.EmergencyContact = request.EmergencyContact;
            p.UpdatedAt = DateTime.UtcNow;
        }
        else if (user.StaffProfile != null)
        {
            user.StaffProfile.FullName = request.FullName.Trim();
        }

        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(userId, "UPDATE_PROFILE", "UserAccount", userId.ToString(), ct: ct);

        return ApiResponse.Ok(null, "Cập nhật thông tin hồ sơ thành công.");
    }

    public async Task<ApiResponse<AvatarUploadResponse>> UploadAvatarAsync(long userId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default)
    {
        var user = await _dbContext.UserAccounts.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user == null)
        {
            return ApiResponse<AvatarUploadResponse>.Fail("Không tìm thấy người dùng.");
        }

        string avatarUrl;
        try
        {
            avatarUrl = await _fileStorageService.SaveAvatarAsync(fileStream, originalFileName, contentType, ct);
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<AvatarUploadResponse>.Fail(ex.Message);
        }

        user.AvatarUrl = avatarUrl;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(userId, "UPLOAD_AVATAR", "UserAccount", userId.ToString(), ct: ct);

        return ApiResponse<AvatarUploadResponse>.Ok(new AvatarUploadResponse { AvatarUrl = avatarUrl }, "Tải lên ảnh đại diện thành công.");
    }
}
