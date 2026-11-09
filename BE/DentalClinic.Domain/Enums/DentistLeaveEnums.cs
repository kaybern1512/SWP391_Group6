using System.Linq;

namespace DentalClinic.Domain.Enums;

public static class DentistLeaveType
{
    public const string AnnualLeave = "AnnualLeave";
    public const string SickLeave = "SickLeave";
    public const string Personal = "Personal";
    public const string Emergency = "Emergency";
    public const string Other = "Other";

    public static readonly string[] All = { AnnualLeave, SickLeave, Personal, Emergency, Other };
    public static bool IsValid(string type) => All.Contains(type);

    public static string ToDisplayName(string type) => type switch
    {
        AnnualLeave => "Nghỉ phép năm",
        SickLeave => "Nghỉ ốm / Khám bệnh",
        Personal => "Việc cá nhân",
        Emergency => "Việc khẩn cấp",
        Other => "Khác",
        _ => type
    };
}

public static class DentistLeaveStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";

    public static readonly string[] All = { Pending, Approved, Rejected, Withdrawn };
    public static bool IsValid(string status) => All.Contains(status);

    public static string ToDisplayName(string status) => status switch
    {
        Pending => "Chờ duyệt",
        Approved => "Đã duyệt",
        Rejected => "Đã từ chối",
        Withdrawn => "Đã rút đơn",
        _ => status
    };
}

public static class AppointmentDisruptionStatus
{
    public const string PendingPatientChoice = "PendingPatientChoice";
    public const string DoctorChanged = "DoctorChanged";
    public const string Rescheduled = "Rescheduled";
    public const string NeedsStaffAction = "NeedsStaffAction";
    public const string ResolvedByStaff = "ResolvedByStaff";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        PendingPatientChoice,
        DoctorChanged,
        Rescheduled,
        NeedsStaffAction,
        ResolvedByStaff,
        Cancelled
    };

    public static bool IsValid(string status) => All.Contains(status);

    public static string ToDisplayName(string status) => status switch
    {
        PendingPatientChoice => "Chờ bệnh nhân phản hồi",
        DoctorChanged => "Đã đổi bác sĩ",
        Rescheduled => "Đã dời lịch khám",
        NeedsStaffAction => "Cần lễ tân xử lý",
        ResolvedByStaff => "Lễ tân đã điều phối",
        Cancelled => "Đã hủy lịch",
        _ => status
    };
}

public static class DisruptionPatientChoice
{
    public const string ChangeDoctor = "ChangeDoctor";
    public const string Reschedule = "Reschedule";
    public const string ContactStaff = "ContactStaff";

    public static readonly string[] All = { ChangeDoctor, Reschedule, ContactStaff };
    public static bool IsValid(string choice) => All.Contains(choice);
}

public static class DisruptionEmailStatus
{
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";

    public static readonly string[] All = { Pending, Sent, Failed, Skipped };
    public static bool IsValid(string status) => All.Contains(status);
}
