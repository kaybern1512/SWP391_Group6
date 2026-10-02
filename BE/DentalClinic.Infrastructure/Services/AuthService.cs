using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Auth.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IOtpService _otpService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailService _emailService;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IAuditLogService _auditLogService;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    private class OtpCacheEntry
    {
        public string CodeHash { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public AuthService(
        DentalClinicDbContext dbContext,
        IPasswordHasherService passwordHasher,
        IOtpService otpService,
        IJwtTokenService jwtTokenService,
        IEmailService emailService,
        IGoogleAuthService googleAuthService,
        IAuditLogService auditLogService,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
        _jwtTokenService = jwtTokenService;
        _emailService = emailService;
        _googleAuthService = googleAuthService;
        _auditLogService = auditLogService;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // 1. Check existing email
        var existingUser = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, ct);

        if (existingUser != null)
        {
            if (existingUser.Status == AccountStatus.Unverified)
            {
                return ApiResponse.Fail("Email này đã được đăng ký nhưng chưa được kích hoạt. Vui lòng kiểm tra hộp thư hoặc yêu cầu gửi lại mã xác thực.");
            }
            return ApiResponse.Fail("Email này đã được sử dụng bởi một tài khoản khác.");
        }

        // 2. Check existing phone number if provided
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var phoneTrim = request.PhoneNumber.Trim();
            var phoneExists = await _dbContext.UserAccounts
                .AnyAsync(u => u.PhoneNumber == phoneTrim, ct);
            if (phoneExists)
            {
                return ApiResponse.Fail("Số điện thoại này đã được đăng ký với một tài khoản khác.");
            }
        }

        // 3. Generate OTP
        var otpCode = _otpService.GenerateNumericOtp(6);
        var codeHash = _otpService.HashOtp(otpCode);

        // 4. Concurrency-safe PatientCode generation with retry
        string patientCode = string.Empty;
        var maxRetries = 5;
        var now = DateTime.UtcNow;
        var yearMonth = now.ToString("yyyyMM");

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var prefix = $"PAT-{yearMonth}-";
                var lastCode = await _dbContext.PatientProfiles
                    .Where(p => p.PatientCode.StartsWith(prefix))
                    .OrderByDescending(p => p.PatientCode)
                    .Select(p => p.PatientCode)
                    .FirstOrDefaultAsync(ct);

                int nextSeq = 1;
                if (!string.IsNullOrEmpty(lastCode) && lastCode.Length >= prefix.Length + 4)
                {
                    var seqStr = lastCode.Substring(prefix.Length);
                    if (int.TryParse(seqStr, out var currentSeq))
                    {
                        nextSeq = currentSeq + 1 + attempt;
                    }
                }
                else
                {
                    nextSeq += attempt;
                }

                patientCode = $"{prefix}{nextSeq:D4}";

                // Create UserAccount
                var userAccount = new UserAccount
                {
                    Email = email,
                    PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
                    PasswordHash = _passwordHasher.HashPassword(request.Password),
                    Role = UserRole.Patient,
                    Status = AccountStatus.Unverified,
                    CreatedAt = now,
                    FailedLoginCount = 0
                };

                _dbContext.UserAccounts.Add(userAccount);
                await _dbContext.SaveChangesAsync(ct);

                // Create PatientProfile (PK = UserId)
                var patientProfile = new PatientProfile
                {
                    UserId = userAccount.UserId,
                    PatientCode = patientCode,
                    FullName = request.FullName.Trim(),
                    DateOfBirth = request.DateOfBirth.HasValue ? DateOnly.FromDateTime(request.DateOfBirth.Value) : null,
                    Gender = request.Gender,
                    CreatedAt = now
                };

                _dbContext.PatientProfiles.Add(patientProfile);
                await _dbContext.SaveChangesAsync(ct);

                await tx.CommitAsync(ct);

                // Save OTP to IMemoryCache (5 minutes expiration)
                var cacheEntry = new OtpCacheEntry
                {
                    CodeHash = codeHash,
                    AttemptCount = 0,
                    CreatedAt = now
                };
                _cache.Set($"email-verify:{userAccount.UserId}", cacheEntry, TimeSpan.FromMinutes(5));

                string? phoneOtpCode = null;
                if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                {
                    phoneOtpCode = _otpService.GenerateNumericOtp(6);
                    var phoneCacheEntry = new OtpCacheEntry
                    {
                        CodeHash = _otpService.HashOtp(phoneOtpCode),
                        AttemptCount = 0,
                        CreatedAt = now
                    };
                    _cache.Set($"phone-verify:{userAccount.UserId}", phoneCacheEntry, TimeSpan.FromMinutes(5));
                }

                try
                {
                    await _emailService.SendEmailVerificationOtpAsync(email, request.FullName.Trim(), otpCode, ct);
                    if (!string.IsNullOrWhiteSpace(phoneOtpCode))
                    {
                        _logger.LogInformation("\n======================================================\n[DEV SMS OTP NOTIFICATION]\nPURPOSE: PHONE VERIFICATION\nTO PHONE: {PhoneNumber} ({FullName})\nSMS OTP CODE: {PhoneOtpCode}\nEXPIRES IN: 5 minutes\n======================================================\n", request.PhoneNumber, request.FullName.Trim(), phoneOtpCode);
                    }
                    await _auditLogService.LogAsync(userAccount.UserId, "REGISTER", "UserAccount", userAccount.UserId.ToString(), ct: ct);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to dispatch verification email to {Email}", email);
                }

                return ApiResponse.Ok(null, "Đăng ký tài khoản thành công! Vui lòng kiểm tra email để lấy mã xác thực kích hoạt tài khoản.");
            }
            catch (DbUpdateException ex) when (attempt < maxRetries - 1)
            {
                await tx.RollbackAsync(ct);
                _logger.LogWarning(ex, "Conflict generating PatientCode {PatientCode}, retrying attempt {Attempt}...", patientCode, attempt + 1);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Error registering user {Email}.", email);
                throw;
            }
        }

        return ApiResponse.Fail("Không thể tạo mã hồ sơ bệnh nhân lúc này. Vui lòng thử lại sau.");
    }

    public async Task<ApiResponse> VerifyEmailAsync(VerifyEmailRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, ct);

        if (user == null)
        {
            return ApiResponse.Fail("Tài khoản không tồn tại trong hệ thống.");
        }

        if (user.Status == AccountStatus.Active)
        {
            return ApiResponse.Ok(new VerifyEmailResponse { RequiresPhoneVerification = false, Email = user.Email ?? string.Empty }, "Tài khoản của bạn đã được xác thực trước đó. Vui lòng đăng nhập.");
        }

        if (!_cache.TryGetValue($"email-verify:{user.UserId}", out OtpCacheEntry? cacheEntry) || cacheEntry == null)
        {
            return ApiResponse.Fail("Mã xác thực đã hết hạn hoặc không tồn tại. Vui lòng yêu cầu gửi lại mã mới.");
        }

        if (cacheEntry.AttemptCount >= 5)
        {
            return ApiResponse.Fail("Bạn đã nhập sai mã xác thực quá số lần quy định (5 lần). Vui lòng yêu cầu mã xác thực mới.");
        }

        var isOtpValid = _otpService.VerifyOtp(request.Code.Trim(), cacheEntry.CodeHash);
        if (!isOtpValid)
        {
            cacheEntry.AttemptCount++;
            var remaining = Math.Max(0, 5 - cacheEntry.AttemptCount);
            return ApiResponse.Fail($"Mã xác thực không chính xác. Bạn còn {remaining} lần thử.");
        }

        // OTP verified successfully
        _cache.Remove($"email-verify:{user.UserId}");

        user.Status = AccountStatus.Active;
        user.EmailVerifiedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(user.UserId, "VERIFY_EMAIL", "UserAccount", user.UserId.ToString(), ct: ct);

        return ApiResponse.Ok(new VerifyEmailResponse { RequiresPhoneVerification = false, Email = user.Email ?? string.Empty }, "Xác thực email thành công! Tài khoản của bạn đã được kích hoạt. Hãy đăng nhập để tiếp tục.");
    }

    public async Task<ApiResponse> ResendVerificationAsync(ResendVerificationRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.UserAccounts
            .Include(u => u.PatientProfile)
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, ct);

        if (user == null || user.Status == AccountStatus.Active)
        {
            return ApiResponse.Ok(null, "Nếu tài khoản tồn tại và chưa được kích hoạt, mã xác thực mới đã được gửi đến email của bạn.");
        }

        // Rate limiting: 60s cooldown via cache
        if (_cache.TryGetValue($"resend-cooldown:email:{user.UserId}", out _))
        {
            return ApiResponse.Fail("Vui lòng đợi 60 giây trước khi yêu cầu gửi lại mã xác thực.");
        }

        var otpCode = _otpService.GenerateNumericOtp(6);
        var cacheEntry = new OtpCacheEntry
        {
            CodeHash = _otpService.HashOtp(otpCode),
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _cache.Set($"email-verify:{user.UserId}", cacheEntry, TimeSpan.FromMinutes(5));
        _cache.Set($"resend-cooldown:email:{user.UserId}", true, TimeSpan.FromSeconds(60));

        var recipientName = user.PatientProfile?.FullName ?? user.StaffProfile?.FullName ?? user.Email ?? "Quý khách";
        await _emailService.SendEmailVerificationOtpAsync(email, recipientName, otpCode, ct);

        return ApiResponse.Ok(null, "Mã xác thực mới đã được gửi đến email của bạn.");
    }

    public async Task<ApiResponse> VerifyPhoneAsync(VerifyPhoneRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, ct);

        if (user == null)
        {
            return ApiResponse.Fail("Tài khoản không tồn tại trong hệ thống.");
        }

        if (user.Status == AccountStatus.Active)
        {
            return ApiResponse.Ok(null, "Số điện thoại của bạn đã được xác thực trước đó. Vui lòng đăng nhập.");
        }

        if (!_cache.TryGetValue($"phone-verify:{user.UserId}", out OtpCacheEntry? cacheEntry) || cacheEntry == null)
        {
            return ApiResponse.Fail("Mã xác thực đã hết hạn hoặc không tồn tại. Vui lòng yêu cầu gửi lại mã mới.");
        }

        if (cacheEntry.AttemptCount >= 5)
        {
            return ApiResponse.Fail("Bạn đã nhập sai mã xác thực quá số lần quy định (5 lần). Vui lòng yêu cầu mã xác thực mới.");
        }

        var isOtpValid = _otpService.VerifyOtp(request.Code.Trim(), cacheEntry.CodeHash);
        if (!isOtpValid)
        {
            cacheEntry.AttemptCount++;
            var remaining = Math.Max(0, 5 - cacheEntry.AttemptCount);
            return ApiResponse.Fail($"Mã xác thực không chính xác. Bạn còn {remaining} lần thử.");
        }

        _cache.Remove($"phone-verify:{user.UserId}");

        user.Status = AccountStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(user.UserId, "VERIFY_PHONE", "UserAccount", user.UserId.ToString(), ct: ct);

        return ApiResponse.Ok(null, "Xác thực số điện thoại thành công! Tài khoản của bạn đã được kích hoạt hoàn toàn.");
    }

    public async Task<ApiResponse> ResendPhoneVerificationAsync(ResendPhoneVerificationRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.UserAccounts
            .Include(u => u.PatientProfile)
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, ct);

        if (user == null || user.Status == AccountStatus.Active)
        {
            return ApiResponse.Ok(null, "Nếu tài khoản tồn tại và chưa xác thực số điện thoại, mã xác thực mới đã được gửi đến số điện thoại của bạn.");
        }

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            return ApiResponse.Fail("Tài khoản này chưa đăng ký số điện thoại.");
        }

        if (_cache.TryGetValue($"resend-cooldown:phone:{user.UserId}", out _))
        {
            return ApiResponse.Fail("Vui lòng đợi 60 giây trước khi yêu cầu gửi lại mã xác thực.");
        }

        var otpCode = _otpService.GenerateNumericOtp(6);
        var cacheEntry = new OtpCacheEntry
        {
            CodeHash = _otpService.HashOtp(otpCode),
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _cache.Set($"phone-verify:{user.UserId}", cacheEntry, TimeSpan.FromMinutes(5));
        _cache.Set($"resend-cooldown:phone:{user.UserId}", true, TimeSpan.FromSeconds(60));

        var fullName = user.PatientProfile?.FullName ?? user.StaffProfile?.FullName ?? user.Email ?? "Quý khách";
        _logger.LogInformation("\n======================================================\n[DEV SMS OTP NOTIFICATION]\nPURPOSE: RESEND PHONE VERIFICATION\nTO PHONE: {PhoneNumber} ({FullName})\nSMS OTP CODE: {PhoneOtpCode}\nEXPIRES IN: 5 minutes\n======================================================\n", user.PhoneNumber, fullName, otpCode);

        return ApiResponse.Ok(null, "Mã xác thực mới đã được gửi đến số điện thoại của bạn.");
    }

    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.UserAccounts
            .Include(u => u.PatientProfile)
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, ct);

        if (user == null)
        {
            return ApiResponse<LoginResponse>.Fail("Email hoặc mật khẩu không chính xác.");
        }

        // Check Lockout
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            var remainingMinutes = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
            return ApiResponse<LoginResponse>.Fail($"Tài khoản tạm thời bị khóa do nhập sai mật khẩu quá 5 lần. Vui lòng thử lại sau {remainingMinutes} phút.");
        }

        // Check Account Status
        if (user.Status == AccountStatus.Unverified)
        {
            return ApiResponse<LoginResponse>.Fail("Tài khoản chưa được kích hoạt. Vui lòng xác thực email trước khi đăng nhập.");
        }

        if (user.Status == AccountStatus.Locked)
        {
            return ApiResponse<LoginResponse>.Fail("Tài khoản của bạn đã bị khóa. Vui lòng liên hệ bộ phận quản trị.");
        }

        if (user.Status == AccountStatus.Inactive)
        {
            return ApiResponse<LoginResponse>.Fail("Tài khoản của bạn đã bị vô hiệu hóa.");
        }

        // Verify password
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return ApiResponse<LoginResponse>.Fail("Tài khoản này được đăng ký bằng Google. Vui lòng chọn đăng nhập bằng Google.");
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(user.PasswordHash, request.Password);
        if (!isPasswordValid)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                user.FailedLoginCount = 0;
                await _dbContext.SaveChangesAsync(ct);
                return ApiResponse<LoginResponse>.Fail("Bạn đã nhập sai mật khẩu 5 lần liên tiếp. Tài khoản đã bị tạm khóa trong 15 phút.");
            }

            await _dbContext.SaveChangesAsync(ct);
            var remainingAttempts = 5 - user.FailedLoginCount;
            return ApiResponse<LoginResponse>.Fail($"Email hoặc mật khẩu không chính xác. Bạn còn {remainingAttempts} lần thử trước khi bị tạm khóa.");
        }

        // Login success: Reset failed login count and lockout
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;

        var fullName = user.PatientProfile?.FullName ?? user.StaffProfile?.FullName ?? user.Email ?? string.Empty;
        var userCode = user.PatientProfile?.PatientCode ?? user.StaffProfile?.EmployeeCode;

        // Generate tokens
        var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email ?? string.Empty, user.Role, fullName, user.AvatarUrl);
        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();

        // Store refresh token in IMemoryCache (7 days expiration)
        _cache.Set($"refresh-token:{rawRefreshToken}", user.UserId, TimeSpan.FromDays(7));

        await _dbContext.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(user.UserId, "LOGIN", "UserAccount", user.UserId.ToString(), ct: ct);

        var response = new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresAt = _jwtTokenService.GetAccessTokenExpiration(),
            User = new UserInfoDto
            {
                UserId = user.UserId,
                Email = user.Email ?? string.Empty,
                FullName = fullName,
                Role = user.Role,
                Status = user.Status,
                AvatarUrl = user.AvatarUrl,
                PhoneNumber = user.PhoneNumber,
                UserCode = userCode
            }
        };

        return ApiResponse<LoginResponse>.Ok(response, "Đăng nhập thành công.");
    }

    public async Task<ApiResponse<LoginResponse>> GoogleLoginAsync(GoogleLoginRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default)
    {
        var googleUser = await _googleAuthService.ValidateIdTokenAsync(request.IdToken, ct);
        if (googleUser == null)
        {
            return ApiResponse<LoginResponse>.Fail("Xác thực tài khoản Google không hợp lệ hoặc đã hết hạn.");
        }

        var googleEmail = googleUser.Email.Trim().ToLowerInvariant();

        // Check if user exists by email
        var user = await _dbContext.UserAccounts
            .Include(u => u.PatientProfile)
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == googleEmail, ct);

        if (user != null)
        {
            if (user.Status == AccountStatus.Unverified)
            {
                user.Status = AccountStatus.Active;
                user.EmailVerifiedAt = DateTime.UtcNow;
            }

            if (string.IsNullOrEmpty(user.AvatarUrl) && !string.IsNullOrEmpty(googleUser.Picture))
            {
                user.AvatarUrl = googleUser.Picture;
            }
        }
        else
        {
            // Auto create new Patient account without ExternalLogins
            var now = DateTime.UtcNow;
            var yearMonth = now.ToString("yyyyMM");
            var prefix = $"PAT-{yearMonth}-";
            var lastCode = await _dbContext.PatientProfiles
                .Where(p => p.PatientCode.StartsWith(prefix))
                .OrderByDescending(p => p.PatientCode)
                .Select(p => p.PatientCode)
                .FirstOrDefaultAsync(ct);

            int nextSeq = 1;
            if (!string.IsNullOrEmpty(lastCode) && lastCode.Length >= prefix.Length + 4)
            {
                var seqStr = lastCode.Substring(prefix.Length);
                if (int.TryParse(seqStr, out var currentSeq))
                {
                    nextSeq = currentSeq + 1;
                }
            }
            var patientCode = $"{prefix}{nextSeq:D4}";

            user = new UserAccount
            {
                Email = googleEmail,
                Role = UserRole.Patient,
                Status = AccountStatus.Active,
                EmailVerifiedAt = now,
                AvatarUrl = googleUser.Picture,
                CreatedAt = now,
                FailedLoginCount = 0
            };

            _dbContext.UserAccounts.Add(user);
            await _dbContext.SaveChangesAsync(ct);

            var patientProfile = new PatientProfile
            {
                UserId = user.UserId,
                PatientCode = patientCode,
                FullName = string.IsNullOrWhiteSpace(googleUser.Name) ? googleEmail : googleUser.Name,
                CreatedAt = now
            };

            _dbContext.PatientProfiles.Add(patientProfile);
            await _dbContext.SaveChangesAsync(ct);
        }

        // Check account status
        if (user.Status == AccountStatus.Locked)
        {
            return ApiResponse<LoginResponse>.Fail("Tài khoản của bạn đã bị khóa. Vui lòng liên hệ bộ phận hỗ trợ.");
        }

        if (user.Status == AccountStatus.Inactive)
        {
            return ApiResponse<LoginResponse>.Fail("Tài khoản của bạn đã bị vô hiệu hóa.");
        }

        // Reset lockout
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;

        var fullName = user.PatientProfile?.FullName ?? user.StaffProfile?.FullName ?? googleUser.Name;
        var userCode = user.PatientProfile?.PatientCode ?? user.StaffProfile?.EmployeeCode;

        var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email ?? googleEmail, user.Role, fullName, user.AvatarUrl);
        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();

        // Store refresh token in IMemoryCache (7 days expiration)
        _cache.Set($"refresh-token:{rawRefreshToken}", user.UserId, TimeSpan.FromDays(7));

        await _dbContext.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(user.UserId, "GOOGLE_LOGIN", "UserAccount", user.UserId.ToString(), ct: ct);

        var response = new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresAt = _jwtTokenService.GetAccessTokenExpiration(),
            User = new UserInfoDto
            {
                UserId = user.UserId,
                Email = user.Email ?? googleEmail,
                FullName = fullName,
                Role = user.Role,
                Status = user.Status,
                AvatarUrl = user.AvatarUrl,
                PhoneNumber = user.PhoneNumber,
                UserCode = userCode
            }
        };

        return ApiResponse<LoginResponse>.Ok(response, "Đăng nhập Google thành công.");
    }

    public async Task<ApiResponse<RefreshTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ApiResponse<RefreshTokenResponse>.Fail("Refresh token không được để trống.");
        }

        if (!_cache.TryGetValue($"refresh-token:{request.RefreshToken}", out long userId))
        {
            return ApiResponse<RefreshTokenResponse>.Fail("Phiên làm việc đã bị thu hồi hoặc đã hết hạn. Vui lòng đăng nhập lại.");
        }

        // Revoke current refresh token
        _cache.Remove($"refresh-token:{request.RefreshToken}");

        var user = await _dbContext.UserAccounts
            .Include(u => u.PatientProfile)
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);

        if (user == null || user.Status != AccountStatus.Active)
        {
            return ApiResponse<RefreshTokenResponse>.Fail("Tài khoản không hoạt động hoặc đã bị khóa.");
        }

        // Issue new tokens
        var newRawRefreshToken = _jwtTokenService.GenerateRefreshToken();
        _cache.Set($"refresh-token:{newRawRefreshToken}", user.UserId, TimeSpan.FromDays(7));

        var fullName = user.PatientProfile?.FullName ?? user.StaffProfile?.FullName ?? user.Email ?? string.Empty;
        var newAccessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email ?? string.Empty, user.Role, fullName, user.AvatarUrl);

        var response = new RefreshTokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRawRefreshToken,
            ExpiresAt = _jwtTokenService.GetAccessTokenExpiration()
        };

        return ApiResponse<RefreshTokenResponse>.Ok(response, "Làm mới token thành công.");
    }

    public async Task<ApiResponse> LogoutAsync(LogoutRequest request, string? ipAddress, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            if (_cache.TryGetValue($"refresh-token:{request.RefreshToken}", out long userId))
            {
                _cache.Remove($"refresh-token:{request.RefreshToken}");
                await _auditLogService.LogAsync(userId, "LOGOUT", "UserAccount", userId.ToString(), ct: ct);
            }
        }

        return ApiResponse.Ok(null, "Đăng xuất thành công.");
    }

    public async Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.UserAccounts
            .Include(u => u.PatientProfile)
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, ct);

        // Security best practice: Always return generic success message to prevent user enumeration
        if (user == null || user.Status != AccountStatus.Active)
        {
            return ApiResponse.Ok(null, "Nếu email của bạn tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi đến hộp thư của bạn.");
        }

        // Generate secure 6-digit numeric OTP code for password reset
        var otpCode = _otpService.GenerateNumericOtp(6);
        var cacheEntry = new OtpCacheEntry
        {
            CodeHash = _otpService.HashOtp(otpCode),
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        // Cache reset OTP for 10 minutes in IMemoryCache
        _cache.Set($"password-reset:{user.UserId}", cacheEntry, TimeSpan.FromMinutes(10));

        var fullName = user.PatientProfile?.FullName ?? user.StaffProfile?.FullName ?? user.Email ?? "Quý khách";
        try
        {
            await _emailService.SendPasswordResetOtpAsync(user.Email!, fullName, otpCode, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset OTP to {Email}", user.Email);
        }

        await _auditLogService.LogAsync(user.UserId, "FORGOT_PASSWORD", "UserAccount", user.UserId.ToString(), ct: ct);

        return ApiResponse.Ok(null, "Mã xác thực OTP đặt lại mật khẩu đã được gửi đến email của bạn. Vui lòng kiểm tra hộp thư.");
    }

    public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, ct);

        if (user == null)
        {
            return ApiResponse.Fail("Yêu cầu đặt lại mật khẩu không hợp lệ.");
        }

        if (!_cache.TryGetValue($"password-reset:{user.UserId}", out OtpCacheEntry? cacheEntry) || cacheEntry == null)
        {
            return ApiResponse.Fail("Mã xác thực OTP không chính xác hoặc đã hết hạn. Vui lòng kiểm tra lại mã hoặc gửi yêu cầu mới.");
        }

        if (cacheEntry.AttemptCount >= 5)
        {
            return ApiResponse.Fail("Bạn đã nhập sai mã xác thực quá số lần quy định (5 lần). Vui lòng yêu cầu mã xác thực mới.");
        }

        var isOtpValid = _otpService.VerifyOtp(request.Token.Trim(), cacheEntry.CodeHash);
        if (!isOtpValid)
        {
            cacheEntry.AttemptCount++;
            var remaining = Math.Max(0, 5 - cacheEntry.AttemptCount);
            return ApiResponse.Fail($"Mã xác thực không chính xác. Bạn còn {remaining} lần thử.");
        }

        // Mark OTP as used by removing from cache
        _cache.Remove($"password-reset:{user.UserId}");

        // Hash new password and update user
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;

        await _dbContext.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(user.UserId, "RESET_PASSWORD", "UserAccount", user.UserId.ToString(), ct: ct);

        return ApiResponse.Ok(null, "Đặt lại mật khẩu thành công! Bạn có thể sử dụng mật khẩu mới để đăng nhập.");
    }

    public async Task<ApiResponse> ChangePasswordAsync(long userId, ChangePasswordRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var user = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);

        if (user == null)
        {
            return ApiResponse.Fail("Không tìm thấy thông tin tài khoản.");
        }

        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            var isCurrentValid = _passwordHasher.VerifyPassword(user.PasswordHash, request.CurrentPassword);
            if (!isCurrentValid)
            {
                return ApiResponse.Fail("Mật khẩu hiện tại không chính xác.");
            }
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(user.UserId, "CHANGE_PASSWORD", "UserAccount", user.UserId.ToString(), ct: ct);

        return ApiResponse.Ok(null, "Đổi mật khẩu thành công. Vui lòng đăng nhập lại với mật khẩu mới.");
    }
}
