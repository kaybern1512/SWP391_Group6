using System;

namespace DentalClinic.Domain.Common;

/// <summary>
/// Centralized TimeZone and Clock authority for DentalCare Clinic.
/// Enforces canonical clinic timezone Asia/Ho_Chi_Minh (UTC+7, Vietnam Standard Time).
/// Eliminates timezone discrepancies between cloud runners (UTC) and local clinic operations.
/// </summary>
public static class ClinicTimeHelper
{
    private static readonly Lazy<TimeZoneInfo> _clinicTimeZone = new(() =>
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone(
                    "Asia/Ho_Chi_Minh",
                    TimeSpan.FromHours(7),
                    "Vietnam Standard Time",
                    "Vietnam Standard Time");
            }
        }
    });

    /// <summary>
    /// Canonical TimeZone for the clinic (Asia/Ho_Chi_Minh / SE Asia Standard Time, UTC+7).
    /// </summary>
    public static TimeZoneInfo ClinicTimeZone => _clinicTimeZone.Value;

    /// <summary>
    /// Current wall-clock Date & Time at the clinic (Asia/Ho_Chi_Minh).
    /// Evaluated strictly from DateTime.UtcNow converted to clinic timezone.
    /// </summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ClinicTimeZone);

    /// <summary>
    /// Current calendar Date at the clinic (midnight of current clinic day).
    /// </summary>
    public static DateTime Today => Now.Date;

    /// <summary>
    /// Current DateOnly at the clinic.
    /// </summary>
    public static DateOnly TodayDateOnly => DateOnly.FromDateTime(Now);

    /// <summary>
    /// Converts any DateTime (Utc, Local, or Unspecified) to clinic local time.
    /// Datetime from HTML inputs (e.g. datetime-local) is Unspecified and represents clinic wall-clock time.
    /// </summary>
    public static DateTime ToClinicTime(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(dt, ClinicTimeZone);
        }

        if (dt.Kind == DateTimeKind.Local)
        {
            return TimeZoneInfo.ConvertTime(dt, TimeZoneInfo.Local, ClinicTimeZone);
        }

        // DateTimeKind.Unspecified: Represents user-entered clinic local wall-clock time
        return dt;
    }
}
