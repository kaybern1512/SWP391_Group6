namespace DentalClinic.Domain.Enums;

public static class AppointmentStatus
{
    public const string PendingReceptionReview = "PendingReceptionReview";
    public const string PendingDepartmentReview = "PendingDepartmentReview";
    public const string AwaitingPatientResponse = "AwaitingPatientResponse";
    public const string Confirmed = "Confirmed";
    public const string CheckedIn = "CheckedIn";
    public const string Waiting = "Waiting";
    public const string Called = "Called";
    public const string InService = "InService";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";
    public const string NoShow = "NoShow";

    public static readonly string[] All =
    {
        PendingReceptionReview,
        PendingDepartmentReview,
        AwaitingPatientResponse,
        Confirmed,
        CheckedIn,
        Waiting,
        Called,
        InService,
        Completed,
        Rejected,
        Withdrawn,
        Expired,
        Cancelled,
        NoShow
    };

    public static bool IsValid(string status) => All.Contains(status);

    /// <summary>
    /// Checks whether an appointment status occupies a dentist or room/chair slot.
    /// </summary>
    public static bool IsSlotOccupying(string status) =>
        status is Confirmed or CheckedIn or Waiting or Called or InService;
}
