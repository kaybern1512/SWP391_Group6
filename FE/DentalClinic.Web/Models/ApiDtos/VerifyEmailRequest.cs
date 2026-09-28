namespace DentalClinic.Web.Models.ApiDtos;

public class VerifyEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ResendVerificationRequest
{
    public string Email { get; set; } = string.Empty;
}

public class VerifyPhoneRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ResendPhoneVerificationRequest
{
    public string Email { get; set; } = string.Empty;
}

public class VerifyEmailResponse
{
    public bool RequiresPhoneVerification { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}
