namespace DentalClinic.Domain.Enums;

public static class AttendanceStatus
{
    public const string NotStarted = "NotStarted";
    public const string OnTime = "OnTime";
    public const string Late = "Late";
    public const string EarlyDeparture = "EarlyDeparture";
    public const string LateAndEarlyDeparture = "LateAndEarlyDeparture";
    public const string ClockedIn = "ClockedIn";
    public const string ClockedOut = "ClockedOut";
    public const string MissingClockOut = "MissingClockOut";
    public const string ApprovedLeave = "ApprovedLeave";
    public const string UnexpectedAbsence = "UnexpectedAbsence";
    public const string NeedsReview = "NeedsReview";
    public const string Verified = "Verified";
    public const string Corrected = "Corrected";

    public static string GetDisplayName(string status) => status switch
    {
        OnTime => "Đúng giờ",
        Late => "Đi trễ",
        EarlyDeparture => "Về sớm",
        LateAndEarlyDeparture => "Trễ & Về sớm",
        ClockedIn => "Đang trong ca làm việc",
        ClockedOut => "Đã kết thúc ca",
        MissingClockOut => "Quên chấm công ra",
        ApprovedLeave => "Nghỉ phép có duyệt",
        UnexpectedAbsence => "Vắng không phép",
        NeedsReview => "Chờ đối soát",
        Verified => "Đã xác nhận",
        Corrected => "Đã điều chỉnh",
        _ => status
    };
}

public static class AttendanceEventType
{
    public const string ClockIn = "ClockIn";
    public const string ClockOut = "ClockOut";
    public const string ManualCorrection = "ManualCorrection";
    public const string CorrectionRequested = "CorrectionRequested";
    public const string CorrectionRejected = "CorrectionRejected";
    public const string MissingClockOutDetected = "MissingClockOutDetected";
    public const string PeriodFinalized = "PeriodFinalized";
    public const string PeriodReopened = "PeriodReopened";
}

public static class CorrectionRequestStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static string GetDisplayName(string status) => status switch
    {
        Pending => "Đang chờ duyệt",
        Approved => "Đã chấp thuận",
        Rejected => "Đã từ chối",
        _ => status
    };
}

public static class PeriodClosureStatus
{
    public const string Open = "Open";
    public const string Locked = "Locked";
    public const string Finalized = "Finalized";
    public const string Reopened = "Reopened";

    public static string GetDisplayName(string status) => status switch
    {
        Open => "Đang mở",
        Locked => "Đã chốt sổ",
        Finalized => "Đã chốt sổ",
        Reopened => "Mở lại",
        _ => status
    };

    public static bool IsLocked(string? status) =>
        string.Equals(status, Locked, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Finalized, StringComparison.OrdinalIgnoreCase);
}
