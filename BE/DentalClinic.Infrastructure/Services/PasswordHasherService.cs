using DentalClinic.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace DentalClinic.Infrastructure.Services;

public class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object _dummyUser = new();

    public string HashPassword(string password)
    {
        return _hasher.HashPassword(_dummyUser, password);
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrWhiteSpace(providedPassword))
        {
            return false;
        }

        var result = _hasher.VerifyHashedPassword(_dummyUser, hashedPassword, providedPassword);
        return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
