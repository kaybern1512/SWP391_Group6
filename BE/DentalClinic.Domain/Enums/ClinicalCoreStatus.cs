namespace DentalClinic.Domain.Enums;

public static class TreatmentPlanStatus
{
    public const string Planned = "Planned";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class TreatmentPlanItemStatus
{
    public const string Planned = "Planned";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class DepartmentReferralStatus
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Assigned = "Assigned";
    public const string Completed = "Completed";
}

public static class FollowUpScheduleStatus
{
    public const string Pending = "Pending";
    public const string Scheduled = "Scheduled";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}
