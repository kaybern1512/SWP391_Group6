using System;

namespace DentalClinic.Domain.Common;

/// <summary>
/// Canonical Work Shift Policy for DentalCare Clinic staff (Dentists & Receptionists).
/// Enforces strictly two fixed working shifts:
/// - Ca Sáng (Morning): 08:00 - 12:00
/// - Ca Chiều (Afternoon): 13:30 - 17:30
/// Eliminates arbitrary shift hours across Frontend, Backend, and Database.
/// </summary>
public static class StandardWorkShiftPolicy
{
    public static readonly TimeOnly MorningStart = new(8, 0);
    public static readonly TimeOnly MorningEnd = new(12, 0);

    public static readonly TimeOnly AfternoonStart = new(13, 30);
    public static readonly TimeOnly AfternoonEnd = new(17, 30);

    public const string ShiftCodeMorning = "Morning";
    public const string ShiftCodeAfternoon = "Afternoon";

    public static bool IsValidStandardShift(TimeOnly start, TimeOnly end)
    {
        return (start == MorningStart && end == MorningEnd) ||
               (start == AfternoonStart && end == AfternoonEnd);
    }

    public static bool TryGetShiftTimes(string shiftIdentifier, out TimeOnly start, out TimeOnly end)
    {
        if (string.Equals(shiftIdentifier, ShiftCodeMorning, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(shiftIdentifier, "Sang", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(shiftIdentifier, "CaSang", StringComparison.OrdinalIgnoreCase))
        {
            start = MorningStart;
            end = MorningEnd;
            return true;
        }

        if (string.Equals(shiftIdentifier, ShiftCodeAfternoon, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(shiftIdentifier, "Chieu", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(shiftIdentifier, "CaChieu", StringComparison.OrdinalIgnoreCase))
        {
            start = AfternoonStart;
            end = AfternoonEnd;
            return true;
        }

        start = default;
        end = default;
        return false;
    }

    public static void ValidateNewWorkDate(DateOnly workDate)
    {
        if (workDate < ClinicTimeHelper.TodayDateOnly)
        {
            throw new InvalidOperationException($"Không thể phân công hoặc chỉnh sửa ca trực cho ngày trong quá khứ ({workDate:dd/MM/yyyy}). Ngày làm việc phải từ hôm nay ({ClinicTimeHelper.TodayDateOnly:dd/MM/yyyy}) trở đi.");
        }
    }

    public static void ValidateStandardShiftHours(TimeOnly start, TimeOnly end)
    {
        if (!IsValidStandardShift(start, end))
        {
            throw new InvalidOperationException($"Khung giờ ca trực {start:HH\\:mm} - {end:HH\\:mm} không hợp lệ theo quy định phòng khám. Hệ thống chỉ chấp nhận Ca Sáng (08:00 - 12:00) hoặc Ca Chiều (13:30 - 17:30).");
        }
    }
}
