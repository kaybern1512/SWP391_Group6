using System;
using System.Security.Cryptography;
using System.Text;
using DentalClinic.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DentalClinic.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly byte[] _secretKey;

    public OtpService(IConfiguration configuration)
    {
        var secret = configuration["OtpSettings:Secret"] ?? "DentalClinicSecureOtpSecretKey_2026_SWP391_Group6!";
        _secretKey = Encoding.UTF8.GetBytes(secret);
    }

    public string GenerateNumericOtp(int length = 6)
    {
        if (length <= 0 || length > 10)
        {
            length = 6;
        }

        var minValue = (int)Math.Pow(10, length - 1);
        var maxValue = (int)Math.Pow(10, length);
        var number = RandomNumberGenerator.GetInt32(minValue, maxValue);
        return number.ToString($"D{length}");
    }

    public string HashOtp(string otp)
    {
        using var hmac = new HMACSHA256(_secretKey);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(otp.Trim()));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public bool VerifyOtp(string providedOtp, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(providedOtp) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        try
        {
            using var hmac = new HMACSHA256(_secretKey);
            var computedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(providedOtp.Trim()));
            var storedBytes = Convert.FromHexString(storedHash.Trim());

            return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
        }
        catch
        {
            return false;
        }
    }
}
