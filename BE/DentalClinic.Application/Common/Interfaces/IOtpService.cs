namespace DentalClinic.Application.Common.Interfaces;

public interface IOtpService
{
    string GenerateNumericOtp(int length = 6);
    string HashOtp(string otp);
    bool VerifyOtp(string providedOtp, string storedHash);
}
