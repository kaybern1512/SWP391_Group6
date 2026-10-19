using System;

namespace DentalClinic.Web.Helpers;

public static class ClinicDateTimeHelper
{
    private static readonly Lazy<TimeZoneInfo> LazyClinicTimeZone = new(ResolveClinicTimeZone);

    public static TimeZoneInfo ClinicTimeZone => LazyClinicTimeZone.Value;

    private static TimeZoneInfo ResolveClinicTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("ClinicTime", TimeSpan.FromHours(7), "Clinic Time", "Clinic Time");
            }
        }
        catch
        {
            return TimeZoneInfo.CreateCustomTimeZone("ClinicTime", TimeSpan.FromHours(7), "Clinic Time", "Clinic Time");
        }
    }

    public static DateTime GetClinicNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ClinicTimeZone);
    }

    public static DateTime GetClinicToday()
    {
        return GetClinicNow().Date;
    }

    public static DateTime ToClinicTime(DateTime utcDateTime)
    {
        var utc = utcDateTime.Kind == DateTimeKind.Utc ? utcDateTime : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, ClinicTimeZone);
    }

    public static (DateTime startUtc, DateTime endUtc) ToClinicUtcRange(DateTime? fromDate, DateTime? toDate, int defaultDays = 30)
    {
        var clinicToday = GetClinicToday();
        var to = toDate?.Date ?? clinicToday;
        var from = fromDate?.Date ?? to.AddDays(-defaultDays + 1);

        if (from > to)
        {
            (from, to) = (to, from);
        }

        var startClinic = from;
        var endClinic = to.AddDays(1); // exclusive end covering whole day

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startClinic, DateTimeKind.Unspecified), ClinicTimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endClinic, DateTimeKind.Unspecified), ClinicTimeZone);

        return (startUtc, endUtc);
    }
}
