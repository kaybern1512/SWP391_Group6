namespace DentalClinic.Domain.Enums;

public static class AccountStatus
{
    public const string Unverified = "Unverified";
    public const string Active = "Active";
    public const string Locked = "Locked";
    public const string Inactive = "Inactive";

    public static readonly string[] All = { Unverified, Active, Locked, Inactive };

    public static bool IsValid(string status) => All.Contains(status);
}
