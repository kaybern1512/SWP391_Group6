namespace DentalClinic.Domain.Enums;

public static class VerificationPurpose
{
    public const string EmailVerification = "EmailVerification";
    public const string EmailChangeVerification = "EmailChangeVerification";
    public const string PhoneVerification = "PhoneVerification";
}

public static class VerificationChannel
{
    public const string Email = "Email";
    public const string Phone = "Phone";
}
