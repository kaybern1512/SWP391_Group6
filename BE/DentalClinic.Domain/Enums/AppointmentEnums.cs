namespace DentalClinic.Domain.Enums;

public static class ProposalStatus
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Expired = "Expired";

    public static readonly string[] All = { Pending, Accepted, Rejected, Expired };
    public static bool IsValid(string status) => All.Contains(status);
}

public static class BookingSource
{
    public const string Patient = "Patient";
    public const string Receptionist = "Receptionist";

    public static readonly string[] All = { Patient, Receptionist };
    public static bool IsValid(string source) => All.Contains(source);
}

public static class DentistAvailabilityStatus
{
    public const string Available = "Available";
    public const string Busy = "Busy";
    public const string OnLeave = "OnLeave";
    public const string Unavailable = "Unavailable";

    public static readonly string[] All = { Available, Busy, OnLeave, Unavailable };
    public static bool IsValid(string status) => All.Contains(status);
}
