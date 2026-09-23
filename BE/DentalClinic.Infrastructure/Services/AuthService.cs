using System;
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
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        DentalClinicDbContext dbContext,
        IPasswordHasherService passwordHasher,
        IOtpService otpService,
        IJwtTokenService jwtTokenService,
        IEmailService emailService,
        IGoogleAuthService googleAuthService,
        IAuditLogService auditLogService,
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
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // 1. Check existing email
        var existingUser = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

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

        // 3. Generate OTP & Hash
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
                // Find highest existing patient code for this month
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

                // Create PatientProfile
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

                // Create AccountVerification
                var verification = new AccountVerification
                {
                    UserId = userAccount.UserId,
                    Channel = VerificationChannel.Email,
                    Purpose = VerificationPurpose.EmailVerification,
                    CodeHash = codeHash,
                    ExpiresAt = now.AddMinutes(15),
                    AttemptCount = 0,
                    CreatedAt = now
                };

                _dbContext.AccountVerifications.Add(verification);
                await _dbContext.SaveChangesAsync(ct);

                // Commit DB transaction BEFORE sending email to avoid SQL locks during external network call
                await tx.CommitAsync(ct);

                // DB committed successfully, now dispatch email
                await _emailService.SendEmailVerificationOtpAsync(email, request.FullName.Trim(), otpCode, ct);

                await _auditLogService.LogAsync(userAccount.UserId, "REGISTER", "UserAccount", userAccount.UserId.ToString(), ct: ct);

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
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

        if (user == null)
        {
            return ApiResponse.Fail("Tài khoản không tồn tại trong hệ thống.");
        }

        if (user.Status == AccountStatus.Active)
        {
            return ApiResponse.Ok(null, "Tài khoản của bạn đã được xác thực trước đó. Vui lòng đăng nhập.");
        }

        var verification = await _dbContext.AccountVerifications
            .Where(v => v.UserId == user.UserId && v.Purpose == VerificationPurpose.EmailVerification && v.VerifiedAt == null)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (verification == null)
        {
            return ApiResponse.Fail("Không tìm thấy mã xác thực hợp lệ. Vui lòng yêu cầu gửi lại mã mới.");
        }

        if (verification.ExpiresAt < DateTime.UtcNow)
        {
            return ApiResponse.Fail("Mã xác thực đã hết hạn. Vui lòng yêu cầu gửi mã mới.");
        }

        if (verification.AttemptCount >= 5)
        {
            return ApiResponse.Fail("Bạn đã nhập sai mã xác thực quá số lần quy định (5 lần). Vui lòng yêu cầu mã xác thực mới.");
        }

        var isOtpValid = _otpService.VerifyOtp(request.Code, verification.CodeHash);
        if (!isOtpValid)
        {
            verification.AttemptCount++;
            await _dbContext.SaveChangesAsync(ct);
            var remaining = Math.Max(0, 5 - verification.AttemptCount);
            return ApiResponse.Fail($"Mã xác thực không chính xác. Bạn còn {remaining} lần thử.");
        }

        verification.VerifiedAt = DateTime.UtcNow;
        user.EmailVerifiedAt = DateTime.UtcNow;
        user.Status = AccountStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(user.UserId, "VERIFY_EMAIL", "UserAccount", user.UserId.ToString(), ct: ct);

        return ApiResponse.Ok(null, "Xác thực email thành công! Tài khoản của bạn đã được kích hoạt. Hãy đăng nhập để tiếp tục.");
    }

    public async Task<ApiResponse> ResendVerificationAsync(ResendVerificationRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.UserAccounts
            .Include(u => u.PatientProfile)
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

        if (user == null || user.Status == AccountStatus.Active)
        {
            // Consistent response to avoid disclosing account presence
            return ApiResponse.Ok(null, "Nếu tài khoản tồn tại và chưa được kích hoạt, mã xác thực mới đã được gửi đến email của bạn.");
        }

        // Rate limiting: 60s cooldown
        var lastVerification = await _dbContext.AccountVerifications
            .Where(v => v.UserId == user.UserId && v.Purpose == VerificationPurpose.EmailVerification)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (lastVerification != null && (DateTime.UtcNow - lastVerification.CreatedAt).TotalSeconds < 60)
        {
            var waitSec = 60 - (int)(DateTime.UtcNow - lastVerification.CreatedAt).TotalSeconds;
            return ApiResponse.Fail($"Vui lòng đợi {waitSec} giây trước khi yêu cầu gửi lại mã xác thực.");
        }

        var otpCode = _otpService.GenerateNumericOtp(6);
        var codeHash = _otpService.HashOtp(otpCode);

        var verification = new AccountVerification
        {
            UserId = user.UserId,
            Channel = VerificationChannel.Email,
            Purpose = VerificationPurpose.EmailVerification,
            CodeHash = codeHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AccountVerifications.Add(verification);
        await _dbContext.SaveChangesAsync(ct);

        var recipientName = user.PatientProfile?.FullName ?? user.StaffProfile?.FullName ?? user.Email;
        await _emailService.SendEmailVerificationOtpAsync(email, recipientName, otpCode, ct);

        return ApiResponse.Ok(null, "Mã xác thực mới đã được gửi đến email của bạn.");
    }

    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.UserAccounts
            .Include(u => u.PatientProfile)
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

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

        var fullName = user.PatientProfile?.FullName ?? user.StaffProfile?.FullName ?? user.Email;
        var userCode = user.PatientProfile?.PatientCode ?? user.StaffProfile?.EmployeeCode;

        // Generate tokens
        var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email, user.Role, fullName, user.AvatarUrl);
        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var refreshTokenHash = _jwtTokenService.HashToken(rawRefreshToken);

        var refreshToken = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = refreshTokenHash,
            ExpiresAt = _jwtTokenService.GetRefreshTokenExpiration(),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress,
            DeviceInfo = deviceInfo
        };

        _dbContext.RefreshTokens.Add(refreshToken);
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
                Email = user.Email,
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

        // Check if external login already linked
        var externalLogin = await _dbContext.ExternalLogins
            .Include(el => el.User)
                .ThenInclude(u => u.PatientProfile)
            .Include(el => el.User)
                .ThenInclude(u => u.StaffProfile)
            .FirstOrDefaultAsync(el => el.Provider == "Google" && el.ProviderKey == googleUser.Subject, ct);

        UserAccount user;

        if (externalLogin != null)
        {
            user = externalLogin.User;
        }
        else
        {
            // Check if user exists by email
            var existingUser = await _dbContext.UserAccounts
                .Include(u => u.PatientProfile)
                .Include(u => u.StaffProfile)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == googleEmail, ct);

            if (existingUser != null)
            {
                user = existingUser;

                // Link google external login
                _dbContext.ExternalLogins.Add(new ExternalLogin
                {
                    UserId = user.UserId,
                    Provider = "Google",
                    ProviderKey = googleUser.Subject,
                    ProviderEmail = googleUser.Email,
                    CreatedAt = DateTime.UtcNow
                });

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
                // Auto create new Patient account
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

                _dbContext.ExternalLogins.Add(new ExternalLogin
                {
                    UserId = user.UserId,
                    Provider = "Google",
                    ProviderKey = googleUser.Subject,
                    ProviderEmail = googleUser.Email,
                    CreatedAt = now
                });
            }
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

        var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email, user.Role, fullName, user.AvatarUrl);
        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var refreshTokenHash = _jwtTokenService.HashToken(rawRefreshToken);

        var refreshToken = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = refreshTokenHash,
            ExpiresAt = _jwtTokenService.GetRefreshTokenExpiration(),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress,
            DeviceInfo = deviceInfo
        };

        _dbContext.RefreshTokens.Add(refreshToken);
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
                Email = user.Email,
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

        var tokenHash = _jwtTokenService.HashToken(request.RefreshToken);

        var storedToken = await _dbContext.RefreshTokens
            .Include(t => t.User)
                .ThenInclude(u => u.PatientProfile)
            .Include(t => t.User)
                .ThenInclude(u => u.StaffProfile)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (storedToken == null)
        {
            return ApiResponse<RefreshTokenResponse>.Fail("Phiên làm việc không hợp lệ.");
        }

        if (storedToken.RevokedAt != null)
        {
            // Token reuse detection: Security alert
            _logger.LogWarning("Potential token reuse detected for user {UserId} with revoked token {TokenHash}", storedToken.UserId, tokenHash);
            return ApiResponse<RefreshTokenResponse>.Fail("Phiên làm việc đã bị thu hồi hoặc đã hết hạn. Vui lòng đăng nhập lại.");
        }

        if (storedToken.ExpiresAt < DateTime.UtcNow)
        {
            return ApiResponse<RefreshTokenResponse>.Fail("Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.");
        }

        var user = storedToken.User;
        if (user.Status != AccountStatus.Active)
        {
            return ApiResponse<RefreshTokenResponse>.Fail("Tài khoản không hoạt động hoặc đã bị khóa.");
        }

        // Token rotation: Revoke current token
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.RevokedByIp = ipAddress;
        storedToken.RevocationReason = "Rotated";

        // Issue new refresh token
        var newRawRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var newHash = _jwtTokenService.HashToken(newRawRefreshToken);
        storedToken.ReplacedByTokenHash = newHash;

        var newRefreshToken = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = newHash,
            ExpiresAt = _jwtTokenService.GetRefreshTokenExpiration(),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress,
            DeviceInfo = deviceInfo
        };

        _dbContext.RefreshTokens.Add(newRefreshToken);

        var fullName = user.PatientProfile?.FullName ?? user.StaffProfile?.FullName ?? user.Email;
        var newAccessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email, user.Role, fullName, user.AvatarUrl);

        await _dbContext.SaveChangesAsync(ct);

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
            var tokenHash = _jwtTokenService.HashToken(request.RefreshToken);
            var token = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.RevokedAt == null, ct);

            if (token != null)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = ipAddress;
                token.RevocationReason = "User Logout";
                await _dbContext.SaveChangesAsync(ct);

                await _auditLogService.LogAsync(token.UserId, "LOGOUT", "RefreshToken", token.RefreshTokenId.ToString(), ct: ct);
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
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

        // Security best practice: Always return generic success message to prevent user enumeration
        if (user == null || user.Status != AccountStatus.Active)
        {
            return ApiResponse.Ok(null, "Nếu email của bạn tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi đến hộp thư của bạn.");
        }

        // Invalidate any previous unused reset tokens
        var previousTokens = await _dbContext.PasswordResetTokens
            .Where(t => t.UserId == user.UserId && t.UsedAt == null)
            .ToListAsync(ct);

        foreach (var t in previousTokens)
        {
            t.UsedAt = DateTime.UtcNow;
        }

        // Generate secure high-entropy token
        var rawResetToken = $"{Guid.NewGuid():N}{Guid.NewGuid():N}";
        var tokenHash = _jwtTokenService.HashToken(rawResetToken);

        var resetTokenRecord = new PasswordResetToken
        {
            UserId = user.UserId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.PasswordResetTokens.Add(resetTokenRecord);
        await _dbContext.SaveChangesAsync(ct);

        var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "https://localhost:7129";
        var resetUrl = $"{frontendBaseUrl.TrimEnd('/')}/Account/ResetPassword?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(rawResetToken)}";

        var fullName = user.PatientProfile?.FullName ?? user.StaffProfile?.FullName ?? user.Email;
        await _emailService.SendPasswordResetAsync(user.Email, fullName, resetUrl, ct);

        await _auditLogService.LogAsync(user.UserId, "FORGOT_PASSWORD", "UserAccount", user.UserId.ToString(), ct: ct);

        return ApiResponse.Ok(null, "Nếu email của bạn tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi đến hộp thư của bạn.");
    }

    public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

        if (user == null)
        {
            return ApiResponse.Fail("Yêu cầu đặt lại mật khẩu không hợp lệ.");
        }

        var tokenHash = _jwtTokenService.HashToken(request.Token);

        var resetToken = await _dbContext.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.UserId == user.UserId && t.TokenHash == tokenHash && t.UsedAt == null, ct);

        if (resetToken == null || resetToken.ExpiresAt < DateTime.UtcNow)
        {
            return ApiResponse.Fail("Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng gửi lại yêu cầu quên mật khẩu.");
        }

        // Mark token as used
        resetToken.UsedAt = DateTime.UtcNow;

        // Hash new password and update user
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;

        // Invalidate all active refresh tokens for security
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == user.UserId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var t in activeTokens)
        {
            t.RevokedAt = DateTime.UtcNow;
            t.RevokedByIp = ipAddress;
            t.RevocationReason = "Password Reset";
        }

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

        // Revoke active refresh tokens
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == user.UserId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var t in activeTokens)
        {
            t.RevokedAt = DateTime.UtcNow;
            t.RevokedByIp = ipAddress;
            t.RevocationReason = "Password Changed";
        }

        await _dbContext.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(user.UserId, "CHANGE_PASSWORD", "UserAccount", user.UserId.ToString(), ct: ct);

        return ApiResponse.Ok(null, "Đổi mật khẩu thành công. Vui lòng đăng nhập lại với mật khẩu mới.");
    }
}
