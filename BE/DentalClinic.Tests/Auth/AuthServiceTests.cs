using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Auth.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Auth;

public class AuthServiceTests
{
    private readonly Mock<IEmailService> _mockEmailService = new();
    private readonly Mock<IGoogleAuthService> _mockGoogleAuth = new();
    private readonly Mock<ILogger<AuthService>> _mockLogger = new();
    private readonly IPasswordHasherService _passwordHasher = new PasswordHasherService();
    private readonly IOtpService _otpService;
    private readonly IJwtTokenService _jwtService;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _memoryCache;

    public AuthServiceTests()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "TestSuperSecretKeyForAuthServiceTests_2026_SWP391!",
                ["JwtSettings:Issuer"] = "DentalClinicApi",
                ["JwtSettings:Audience"] = "DentalClinicWeb",
                ["JwtSettings:ExpirationMinutes"] = "60",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7",
                ["OtpSettings:Secret"] = "TestOtpSecretKey_2026!",
                ["Frontend:BaseUrl"] = "https://localhost:7129"
            })
            .Build();

        _otpService = new OtpService(_config);
        _jwtService = new JwtTokenService(_config);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    private DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private AuthService CreateAuthService(DentalClinicDbContext dbContext)
    {
        var mockAuditLogger = new Mock<ILogger<AuditLogService>>();
        var auditService = new AuditLogService(dbContext, mockAuditLogger.Object);

        return new AuthService(
            dbContext,
            _passwordHasher,
            _otpService,
            _jwtService,
            _mockEmailService.Object,
            _mockGoogleAuth.Object,
            auditService,
            _memoryCache,
            _config,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_CreatesUserAndSendsOtp()
    {
        using var db = CreateDbContext(nameof(RegisterAsync_ValidRequest_CreatesUserAndSendsOtp));
        var authService = CreateAuthService(db);

        var request = new RegisterRequest
        {
            FullName = "Tran Van B",
            Email = "tranvanb@example.com",
            PhoneNumber = "0901234567",
            Password = "Password123@",
            ConfirmPassword = "Password123@"
        };

        var result = await authService.RegisterAsync(request, "127.0.0.1");

        Assert.True(result.Success);

        var user = await db.UserAccounts
            .Include(u => u.PatientProfile)
            .FirstOrDefaultAsync(u => u.Email == "tranvanb@example.com");

        Assert.NotNull(user);
        Assert.Equal(AccountStatus.Unverified, user.Status);
        Assert.NotNull(user.PatientProfile);

        // Verify OTP is cached in IMemoryCache
        Assert.True(_memoryCache.TryGetValue($"email-verify:{user.UserId}", out _));
        Assert.True(_memoryCache.TryGetValue($"phone-verify:{user.UserId}", out _));

        _mockEmailService.Verify(e => e.SendEmailVerificationOtpAsync(
            "tranvanb@example.com", "Tran Van B", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ExistingActiveEmail_ReturnsFail()
    {
        using var db = CreateDbContext(nameof(RegisterAsync_ExistingActiveEmail_ReturnsFail));
        db.UserAccounts.Add(new UserAccount
        {
            Email = "existing@example.com",
            Status = AccountStatus.Active,
            Role = UserRole.Patient,
            PasswordHash = _passwordHasher.HashPassword("Password123@")
        });
        await db.SaveChangesAsync();

        var authService = CreateAuthService(db);
        var request = new RegisterRequest
        {
            FullName = "New User",
            Email = "existing@example.com",
            Password = "Password123@"
        };

        var result = await authService.RegisterAsync(request, "127.0.0.1");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task VerifyEmailAsync_CorrectCode_ActivatesAccount()
    {
        using var db = CreateDbContext(nameof(VerifyEmailAsync_CorrectCode_ActivatesAccount));
        var user = new UserAccount
        {
            Email = "verify@example.com",
            Status = AccountStatus.Unverified,
            Role = UserRole.Patient
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        // Seed OTP in cache
        var otpCode = "123456";
        var cacheEntry = Activator.CreateInstance(
            typeof(AuthService).GetNestedType("OtpCacheEntry", System.Reflection.BindingFlags.NonPublic)!);
        cacheEntry!.GetType().GetProperty("CodeHash")!.SetValue(cacheEntry, _otpService.HashOtp(otpCode));
        cacheEntry.GetType().GetProperty("AttemptCount")!.SetValue(cacheEntry, 0);

        _memoryCache.Set($"email-verify:{user.UserId}", cacheEntry, TimeSpan.FromMinutes(5));

        var authService = CreateAuthService(db);
        var result = await authService.VerifyEmailAsync(new VerifyEmailRequest { Email = "verify@example.com", Code = "123456" }, "127.0.0.1");

        Assert.True(result.Success);

        var updatedUser = await db.UserAccounts.FindAsync(user.UserId);
        Assert.Equal(AccountStatus.Active, updatedUser!.Status);
        Assert.NotNull(updatedUser.EmailVerifiedAt);
    }

    [Fact]
    public async Task VerifyEmailAsync_WrongCode_IncrementsAttemptCount()
    {
        using var db = CreateDbContext(nameof(VerifyEmailAsync_WrongCode_IncrementsAttemptCount));
        var user = new UserAccount
        {
            Email = "wrongcode@example.com",
            Status = AccountStatus.Unverified,
            Role = UserRole.Patient
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        var cacheEntry = Activator.CreateInstance(
            typeof(AuthService).GetNestedType("OtpCacheEntry", System.Reflection.BindingFlags.NonPublic)!);
        cacheEntry!.GetType().GetProperty("CodeHash")!.SetValue(cacheEntry, _otpService.HashOtp("123456"));
        cacheEntry.GetType().GetProperty("AttemptCount")!.SetValue(cacheEntry, 0);

        _memoryCache.Set($"email-verify:{user.UserId}", cacheEntry, TimeSpan.FromMinutes(5));

        var authService = CreateAuthService(db);
        var result = await authService.VerifyEmailAsync(new VerifyEmailRequest { Email = "wrongcode@example.com", Code = "999999" }, "127.0.0.1");

        Assert.False(result.Success);
        Assert.True(_memoryCache.TryGetValue($"email-verify:{user.UserId}", out var storedEntry));
        var attemptCount = (int)storedEntry!.GetType().GetProperty("AttemptCount")!.GetValue(storedEntry)!;
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokensAndResetsLockout()
    {
        using var db = CreateDbContext(nameof(LoginAsync_ValidCredentials_ReturnsTokensAndResetsLockout));
        var user = new UserAccount
        {
            Email = "loginuser@example.com",
            PasswordHash = _passwordHasher.HashPassword("SecretPassword123!"),
            Role = UserRole.Patient,
            Status = AccountStatus.Active,
            FailedLoginCount = 2
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        db.PatientProfiles.Add(new PatientProfile
        {
            UserId = user.UserId,
            PatientCode = "PAT-202609-0001",
            FullName = "Patient Test"
        });
        await db.SaveChangesAsync();

        var authService = CreateAuthService(db);
        var result = await authService.LoginAsync(new LoginRequest
        {
            Email = "loginuser@example.com",
            Password = "SecretPassword123!"
        }, "127.0.0.1", "Chrome");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.RefreshToken));
        Assert.Equal("Patient Test", result.Data.User?.FullName);

        var updatedUser = await db.UserAccounts.FindAsync(user.UserId);
        Assert.Equal(0, updatedUser!.FailedLoginCount);
        Assert.NotNull(updatedUser.LastLoginAt);

        // Check refresh token in cache
        Assert.True(_memoryCache.TryGetValue($"refresh-token:{result.Data.RefreshToken}", out long cachedUserId));
        Assert.Equal(user.UserId, cachedUserId);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_LocksAfter5Attempts()
    {
        using var db = CreateDbContext(nameof(LoginAsync_WrongPassword_LocksAfter5Attempts));
        var user = new UserAccount
        {
            Email = "lockuser@example.com",
            PasswordHash = _passwordHasher.HashPassword("Correct123!"),
            Role = UserRole.Patient,
            Status = AccountStatus.Active,
            FailedLoginCount = 4
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        var authService = CreateAuthService(db);
        var result = await authService.LoginAsync(new LoginRequest
        {
            Email = "lockuser@example.com",
            Password = "WrongPassword!"
        }, "127.0.0.1", "Chrome");

        Assert.False(result.Success);
        Assert.Contains("tạm khóa", result.Message);

        var updatedUser = await db.UserAccounts.FindAsync(user.UserId);
        Assert.NotNull(updatedUser!.LockoutEnd);
        Assert.True(updatedUser.LockoutEnd > DateTime.UtcNow);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_RotatesSuccessfully()
    {
        using var db = CreateDbContext(nameof(RefreshTokenAsync_ValidToken_RotatesSuccessfully));
        var user = new UserAccount
        {
            Email = "refreshuser@example.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        var rawToken = _jwtService.GenerateRefreshToken();
        _memoryCache.Set($"refresh-token:{rawToken}", user.UserId, TimeSpan.FromDays(7));

        var authService = CreateAuthService(db);
        var result = await authService.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = rawToken }, "127.0.0.1", "Firefox");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotEqual(rawToken, result.Data.RefreshToken);

        // Old token should be removed from cache
        Assert.False(_memoryCache.TryGetValue($"refresh-token:{rawToken}", out _));
        // New token should be present in cache
        Assert.True(_memoryCache.TryGetValue($"refresh-token:{result.Data.RefreshToken}", out long newUserId));
        Assert.Equal(user.UserId, newUserId);
    }

    [Fact]
    public async Task LogoutAsync_RevokesToken()
    {
        using var db = CreateDbContext(nameof(LogoutAsync_RevokesToken));
        var user = new UserAccount
        {
            Email = "logoutuser@example.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        var rawToken = _jwtService.GenerateRefreshToken();
        _memoryCache.Set($"refresh-token:{rawToken}", user.UserId, TimeSpan.FromDays(7));

        var authService = CreateAuthService(db);
        var result = await authService.LogoutAsync(new LogoutRequest { RefreshToken = rawToken }, "127.0.0.1");

        Assert.True(result.Success);
        Assert.False(_memoryCache.TryGetValue($"refresh-token:{rawToken}", out _));
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_UpdatesPasswordAndRevokesSessions()
    {
        using var db = CreateDbContext(nameof(ResetPasswordAsync_ValidToken_UpdatesPasswordAndRevokesSessions));
        var user = new UserAccount
        {
            Email = "resetuser@example.com",
            PasswordHash = _passwordHasher.HashPassword("OldPassword123!"),
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        var rawOtp = "123456";
        var cacheEntry = Activator.CreateInstance(
            typeof(AuthService).GetNestedType("OtpCacheEntry", System.Reflection.BindingFlags.NonPublic)!);
        cacheEntry!.GetType().GetProperty("CodeHash")!.SetValue(cacheEntry, _otpService.HashOtp(rawOtp));
        cacheEntry.GetType().GetProperty("AttemptCount")!.SetValue(cacheEntry, 0);

        _memoryCache.Set($"password-reset:{user.UserId}", cacheEntry, TimeSpan.FromMinutes(10));

        var authService = CreateAuthService(db);
        var result = await authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = "resetuser@example.com",
            Token = rawOtp,
            NewPassword = "NewSecretPassword2026!"
        }, "127.0.0.1");

        Assert.True(result.Success);

        var updatedUser = await db.UserAccounts.FindAsync(user.UserId);
        Assert.True(_passwordHasher.VerifyPassword(updatedUser!.PasswordHash!, "NewSecretPassword2026!"));

        // Cache entry should be removed
        Assert.False(_memoryCache.TryGetValue($"password-reset:{user.UserId}", out _));
    }

    [Theory]
    [InlineData("admin@dentalcare.com", UserRole.SystemAdministrator, "EMP-ADM-001", "Admin Quản Trị")]
    [InlineData("receptionist@dentalcare.com", UserRole.Receptionist, "EMP-REC-001", "Lễ Tân Mai")]
    [InlineData("dentist@dentalcare.com", UserRole.Dentist, "EMP-DEN-001", "Bác Sĩ Hùng")]
    [InlineData("manager@dentalcare.com", UserRole.DepartmentManager, "EMP-MGR-001", "Trưởng Khoa Nam")]
    public async Task LoginAsync_StaffRoles_ReturnsCorrectRoleAndEmployeeCode(string email, string role, string empCode, string fullName)
    {
        using var db = CreateDbContext("StaffLogin_" + role);
        var user = new UserAccount
        {
            Email = email,
            PasswordHash = _passwordHasher.HashPassword("Password123@"),
            Role = role,
            Status = AccountStatus.Active,
            EmailVerifiedAt = DateTime.UtcNow
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        db.StaffProfiles.Add(new StaffProfile
        {
            UserId = user.UserId,
            EmployeeCode = empCode,
            FullName = fullName,
            EmploymentStatus = "Active"
        });
        await db.SaveChangesAsync();

        var authService = CreateAuthService(db);
        var result = await authService.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = "Password123@"
        }, "127.0.0.1", "TestClient");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.User);
        Assert.Equal(role, result.Data.User.Role);
        Assert.Equal(empCode, result.Data.User.UserCode);
        Assert.Equal(fullName, result.Data.User.FullName);
        Assert.NotEmpty(result.Data.AccessToken);
    }

    [Fact]
    public async Task VerifyPhoneAsync_ValidCode_ActivatesUser()
    {
        using var db = CreateDbContext(nameof(VerifyPhoneAsync_ValidCode_ActivatesUser));
        var user = new UserAccount
        {
            Email = "phoneuser@example.com",
            PhoneNumber = "0988776655",
            Status = AccountStatus.Unverified,
            Role = UserRole.Patient,
            EmailVerifiedAt = DateTime.UtcNow
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        var cacheEntry = Activator.CreateInstance(
            typeof(AuthService).GetNestedType("OtpCacheEntry", System.Reflection.BindingFlags.NonPublic)!);
        cacheEntry!.GetType().GetProperty("CodeHash")!.SetValue(cacheEntry, _otpService.HashOtp("123456"));
        cacheEntry.GetType().GetProperty("AttemptCount")!.SetValue(cacheEntry, 0);

        _memoryCache.Set($"phone-verify:{user.UserId}", cacheEntry, TimeSpan.FromMinutes(5));

        var authService = CreateAuthService(db);
        var result = await authService.VerifyPhoneAsync(new VerifyPhoneRequest
        {
            Email = "phoneuser@example.com",
            Code = "123456"
        }, "127.0.0.1");

        Assert.True(result.Success);

        var updatedUser = await db.UserAccounts.FindAsync(user.UserId);
        Assert.NotNull(updatedUser);
        Assert.Equal(AccountStatus.Active, updatedUser.Status);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ActiveUser_SendsOtpEmail()
    {
        using var db = CreateDbContext(nameof(ForgotPasswordAsync_ActiveUser_SendsOtpEmail));
        var user = new UserAccount
        {
            Email = "forgotuser@example.com",
            Status = AccountStatus.Active,
            Role = UserRole.Patient,
            PasswordHash = _passwordHasher.HashPassword("OldPassword123@")
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        var authService = CreateAuthService(db);
        var result = await authService.ForgotPasswordAsync(new ForgotPasswordRequest
        {
            Email = "forgotuser@example.com"
        }, "127.0.0.1");

        Assert.True(result.Success);

        // Check OTP is in cache
        Assert.True(_memoryCache.TryGetValue($"password-reset:{user.UserId}", out _));

        _mockEmailService.Verify(e => e.SendPasswordResetOtpAsync(
            "forgotuser@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidOtp_UpdatesPassword()
    {
        using var db = CreateDbContext(nameof(ResetPasswordAsync_ValidOtp_UpdatesPassword));
        var user = new UserAccount
        {
            Email = "resetuser@example.com",
            Status = AccountStatus.Active,
            Role = UserRole.Patient,
            PasswordHash = _passwordHasher.HashPassword("OldPassword123@")
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        var otp = "654321";
        var cacheEntry = Activator.CreateInstance(
            typeof(AuthService).GetNestedType("OtpCacheEntry", System.Reflection.BindingFlags.NonPublic)!);
        cacheEntry!.GetType().GetProperty("CodeHash")!.SetValue(cacheEntry, _otpService.HashOtp(otp));
        cacheEntry.GetType().GetProperty("AttemptCount")!.SetValue(cacheEntry, 0);

        _memoryCache.Set($"password-reset:{user.UserId}", cacheEntry, TimeSpan.FromMinutes(10));

        var authService = CreateAuthService(db);
        var result = await authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = "resetuser@example.com",
            Token = "654321",
            NewPassword = "NewPassword123@",
            ConfirmPassword = "NewPassword123@"
        }, "127.0.0.1");

        Assert.True(result.Success);

        var updatedUser = await db.UserAccounts.FindAsync(user.UserId);
        Assert.NotNull(updatedUser);
        Assert.True(_passwordHasher.VerifyPassword(updatedUser.PasswordHash!, "NewPassword123@"));

        Assert.False(_memoryCache.TryGetValue($"password-reset:{user.UserId}", out _));
    }
}
