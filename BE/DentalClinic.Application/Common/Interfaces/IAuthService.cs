using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Auth.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IAuthService
{
    Task<ApiResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken ct = default);
    Task<ApiResponse> VerifyEmailAsync(VerifyEmailRequest request, string? ipAddress, CancellationToken ct = default);
    Task<ApiResponse> ResendVerificationAsync(ResendVerificationRequest request, string? ipAddress, CancellationToken ct = default);
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default);
    Task<ApiResponse<LoginResponse>> GoogleLoginAsync(GoogleLoginRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default);
    Task<ApiResponse<RefreshTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default);
    Task<ApiResponse> LogoutAsync(LogoutRequest request, string? ipAddress, CancellationToken ct = default);
    Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, CancellationToken ct = default);
    Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken ct = default);
    Task<ApiResponse> ChangePasswordAsync(long userId, ChangePasswordRequest request, string? ipAddress, CancellationToken ct = default);
}
