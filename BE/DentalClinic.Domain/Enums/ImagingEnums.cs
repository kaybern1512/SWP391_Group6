using System;
using System.Linq;

namespace DentalClinic.Domain.Enums;

public static class ImagingRoomType
{
    public const string Treatment = "Treatment";
    public const string DiagnosticImaging = "DiagnosticImaging";

    public static readonly string[] All = { Treatment, DiagnosticImaging };

    public static bool IsValid(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return false;
        return All.Any(t => string.Equals(t, type.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

public static class EquipmentOperationalStatus
{
    public const string Operational = "Operational";
    public const string Maintenance = "Maintenance";
    public const string OutOfService = "OutOfService";

    public static readonly string[] All = { Operational, Maintenance, OutOfService };

    public static bool IsValid(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return All.Any(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

public static class EquipmentType
{
    public const string MultiPurpose = "MultiPurpose";
    public const string IntraoralSensor = "IntraoralSensor";
    public const string PanoramicUnit = "PanoramicUnit";
    public const string CBCTUnit = "CBCTUnit";

    public static readonly string[] All = { MultiPurpose, IntraoralSensor, PanoramicUnit, CBCTUnit };
}

public static class ImagingDiagnosticType
{
    public const string Intraoral = "Intraoral";
    public const string Panorama = "Panorama";
    public const string CBCT = "CBCT";

    public static readonly string[] All = { Intraoral, Panorama, CBCT };

    public static bool IsValid(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return false;
        return All.Any(t => string.Equals(t, type.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string GetDisplayName(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return "Không xác định";
        return type.Trim().ToUpperInvariant() switch
        {
            "INTRAORAL" => "Chụp cận chóp / Trong miệng (Intraoral)",
            "PANORAMA" => "Chụp toàn cảnh (Panorama)",
            "CBCT" => "Chụp cắt lớp 3D Cone Beam (CBCT)",
            _ => type
        };
    }
}

public static class DiagnosticOrderStatus
{
    public const string Ordered = "Ordered";
    public const string Scheduled = "Scheduled";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Ordered, Scheduled, InProgress, Completed, Cancelled };

    public static bool IsValid(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return All.Any(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

public static class ImagingSessionStatus
{
    public const string Scheduled = "Scheduled";
    public const string CheckedIn = "CheckedIn";
    public const string Waiting = "Waiting";
    public const string InProgress = "InProgress";
    public const string Acquired = "Acquired";
    public const string ResultAvailable = "ResultAvailable";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string EquipmentUnavailable = "EquipmentUnavailable";
    public const string NeedsReschedule = "NeedsReschedule";
    public const string UnableToPerform = "UnableToPerform";

    public static readonly string[] All =
    {
        Scheduled,
        CheckedIn,
        Waiting,
        InProgress,
        Acquired,
        ResultAvailable,
        Completed,
        Cancelled,
        EquipmentUnavailable,
        NeedsReschedule,
        UnableToPerform
    };

    public static bool IsValid(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return All.Any(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsTerminal(string? status)
    {
        return status is Completed or Cancelled or UnableToPerform;
    }
}

public static class ImagingSessionPriority
{
    public const string Normal = "Normal";
    public const string Urgent = "Urgent";

    public static readonly string[] All = { Normal, Urgent };
}

public static class DiagnosticResultSourceType
{
    public const string Internal = "Internal";
    public const string External = "External";

    public static readonly string[] All = { Internal, External };
}
