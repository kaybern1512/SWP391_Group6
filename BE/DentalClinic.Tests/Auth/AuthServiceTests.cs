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
            .Include(u => u.AccountVerifications)
            .FirstOrDefaultAsync(u => u.Email == "tranvanb@example.com");

        Assert.NotNull(user);
        Assert.Equal(AccountStatus.Unverified, user.Status);
        Assert.NotNull(user.PatientProfile);
        Assert.StartsWith("PAT-", user.PatientProfile.PatientCode);
        Assert.Single(user.AccountVerifications);

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

        var otpCode = "123456";
        db.AccountVerifications.Add(new AccountVerification
        {
            UserId = user.UserId,
            Channel = VerificationChannel.Email,
            Purpose = VerificationPurpose.EmailVerification,
            CodeHash = _otpService.HashOtp(otpCode),
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

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

        db.AccountVerifications.Add(new AccountVerification
        {
            UserId = user.UserId,
            Channel = VerificationChannel.Email,
            Purpose = VerificationPurpose.EmailVerification,
            CodeHash = _otpService.HashOtp("123456"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var authService = CreateAuthService(db);
        var result = await authService.VerifyEmailAsync(new VerifyEmailRequest { Email = "wrongcode@example.com", Code = "999999" }, "127.0.0.1");

        Assert.False(result.Success);

        var verification = await db.AccountVerifications.FirstAsync(v => v.UserId == user.UserId);
        Assert.Equal(1, verification.AttemptCount);
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
        var tokenHash = _jwtService.HashToken(rawToken);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var authService = CreateAuthService(db);
        var result = await authService.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = rawToken }, "127.0.0.1", "Firefox");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotEqual(rawToken, result.Data.RefreshToken);

        var oldToken = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        Assert.NotNull(oldToken!.RevokedAt);
        Assert.Equal("Rotated", oldToken.RevocationReason);
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
        var tokenHash = _jwtService.HashToken(rawToken);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var authService = CreateAuthService(db);
        var result = await authService.LogoutAsync(new LogoutRequest { RefreshToken = rawToken }, "127.0.0.1");

        Assert.True(result.Success);

        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        Assert.NotNull(token!.RevokedAt);
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

        var rawToken = "ResetToken123456";
        var tokenHash = _jwtService.HashToken(rawToken);

        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.UserId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            CreatedAt = DateTime.UtcNow
        });

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = "session1",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var authService = CreateAuthService(db);
        var result = await authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = "resetuser@example.com",
            Token = rawToken,
            NewPassword = "NewSecretPassword2026!"
        }, "127.0.0.1");

        Assert.True(result.Success);

        var updatedUser = await db.UserAccounts.FindAsync(user.UserId);
        Assert.True(_passwordHasher.VerifyPassword(updatedUser!.PasswordHash!, "NewSecretPassword2026!"));

        var resetTokenRecord = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        Assert.NotNull(resetTokenRecord!.UsedAt);

        var activeSession = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == "session1");
        Assert.NotNull(activeSession!.RevokedAt);
    }
}
