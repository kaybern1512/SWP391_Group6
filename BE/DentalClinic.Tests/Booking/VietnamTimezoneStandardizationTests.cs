using System;
using System.Globalization;
using DentalClinic.Domain.Common;
using Xunit;

namespace DentalClinic.Tests.Booking;

/// <summary>
/// Comprehensive verification suite for Vietnam Standard Time (Asia/Ho_Chi_Minh, UTC+7).
/// Validates wall-clock preservation, UTC conversion, day transitions, reminders, and revenue date boundaries.
/// </summary>
public class VietnamTimezoneStandardizationTests
{
    [Fact]
    public void Test01_UtcDayTransition_CorrectlyMapsToNextDayInVietnam()
    {
        // UTC: 2026-11-09 18:30:00 (Evening in London/UTC)
        var utcTime = new DateTime(2026, 11, 9, 18, 30, 0, DateTimeKind.Utc);

        // In Vietnam (UTC+7), this is 2026-11-10 01:30:00 AM (Next Calendar Day!)
        var vietnamTime = ClinicTimeHelper.ToClinicTime(utcTime);

        Assert.Equal(2026, vietnamTime.Year);
        Assert.Equal(11, vietnamTime.Month);
        Assert.Equal(10, vietnamTime.Day); // Day must roll forward to the 10th
        Assert.Equal(1, vietnamTime.Hour);
        Assert.Equal(30, vietnamTime.Minute);
    }

    [Fact]
    public void Test02_DentistWorkSchedule_WallClockTime_PreservesExactClinicHours()
    {
        // Admin creates a morning shift: 2026-11-20 from 08:00 to 12:00
        var clinicDate = new DateOnly(2026, 11, 20);
        var startTime = new TimeOnly(8, 0);
        var endTime = new TimeOnly(12, 0);

        // When scheduled locally, the wall-clock time must not shift to 01:00-05:00
        var shiftStart = clinicDate.ToDateTime(startTime);
        var shiftEnd = clinicDate.ToDateTime(endTime);

        var convertedStart = ClinicTimeHelper.ToClinicTime(shiftStart);
        var convertedEnd = ClinicTimeHelper.ToClinicTime(shiftEnd);

        Assert.Equal(8, convertedStart.Hour);
        Assert.Equal(12, convertedEnd.Hour);
        Assert.Equal(20, convertedStart.Day);
    }

    [Fact]
    public void Test03_PatientBookingTime_ConsistentlyViewedAcrossAllRoles()
    {
        // Patient books slot: 2026-11-20 09:00 (Unspecified wall-clock from browser datetime-local input)
        var bookingInput = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Unspecified);

        var clinicBooking = ClinicTimeHelper.ToClinicTime(bookingInput);

        // Both Receptionist and Dentist must see 09:00 on Nov 20
        Assert.Equal(2026, clinicBooking.Year);
        Assert.Equal(11, clinicBooking.Month);
        Assert.Equal(20, clinicBooking.Day);
        Assert.Equal(9, clinicBooking.Hour);
        Assert.Equal(0, clinicBooking.Minute);

        // When stored or sent over wire as UTC, converting back to Clinic time returns exactly 09:00
        var utcStored = ClinicTimeHelper.ToUtc(clinicBooking);
        var restoredClinicTime = ClinicTimeHelper.ToClinicTime(utcStored);

        Assert.Equal(clinicBooking, restoredClinicTime);
    }

    [Fact]
    public void Test04_CheckInAndQueue_DateDeterminedByClinicCalendar_NotServerUtc()
    {
        // Simulate server executing at UTC 2026-11-09 23:45:00
        // In UTC this is still Nov 9th, but in Vietnam clinic time it is 2026-11-10 06:45:00 AM!
        var utcNow = new DateTime(2026, 11, 9, 23, 45, 0, DateTimeKind.Utc);
        var clinicNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, ClinicTimeHelper.ClinicTimeZone);

        // Queue number sequence date must be Nov 10th
        var queueDate = clinicNow.Date;
        Assert.Equal(10, queueDate.Day);
        Assert.Equal(11, queueDate.Month);
        Assert.Equal(2026, queueDate.Year);
    }

    [Fact]
    public void Test05_FollowUpReminder_T24hAndT8h_CalculatedAccuratelyFromClinicTime()
    {
        // Doctor schedules recall appointment: 2026-11-20 08:00 Vietnam time
        var clinicApptStart = new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Unspecified);
        var utcApptStart = ClinicTimeHelper.ToUtc(clinicApptStart);

        // In UTC, 08:00 VN is 01:00 UTC (8 - 7)
        Assert.Equal(new DateTime(2026, 11, 20, 1, 0, 0, DateTimeKind.Utc), utcApptStart);

        // T-24h trigger instant in UTC: 2026-11-19 01:00 UTC
        var reminderT24hUtc = utcApptStart.AddHours(-24);
        var reminderT24hClinic = ClinicTimeHelper.ToClinicTime(reminderT24hUtc);

        Assert.Equal(new DateTime(2026, 11, 19, 1, 0, 0, DateTimeKind.Utc), reminderT24hUtc);
        Assert.Equal(new DateTime(2026, 11, 19, 8, 0, 0), reminderT24hClinic);

        // T-8h trigger instant in UTC: 2026-11-19 17:00 UTC (00:00 midnight VN)
        var reminderT8hUtc = utcApptStart.AddHours(-8);
        var reminderT8hClinic = ClinicTimeHelper.ToClinicTime(reminderT8hUtc);

        Assert.Equal(new DateTime(2026, 11, 19, 17, 0, 0, DateTimeKind.Utc), reminderT8hUtc);
        Assert.Equal(new DateTime(2026, 11, 20, 0, 0, 0), reminderT8hClinic);
    }

    [Fact]
    public void Test06_RevenueReport_PaymentAtMidnightInVietnam_AllocatedToCorrectClinicDate()
    {
        // Patient pays via VietQR at 00:30 AM Vietnam time on 2026-11-10
        // (Server UTC instant is 2026-11-09 17:30:00)
        var paymentInstantUtc = new DateTime(2026, 11, 9, 17, 30, 0, DateTimeKind.Utc);

        // Clinic local transaction time
        var paymentClinicTime = ClinicTimeHelper.ToClinicTime(paymentInstantUtc);
        var clinicAccountingDate = paymentClinicTime.Date;

        // Must be counted towards Nov 10th revenue report!
        Assert.Equal(new DateTime(2026, 11, 10), clinicAccountingDate);
    }

    [Fact]
    public void Test07_RoundTripConversions_PreserveIntegrityWithoutOffsetDrift()
    {
        var testTimes = new[]
        {
            new DateTime(2026, 1, 1, 0, 0, 0),
            new DateTime(2026, 6, 15, 12, 30, 0),
            new DateTime(2026, 11, 20, 8, 15, 0),
            new DateTime(2026, 12, 31, 23, 59, 59)
        };

        foreach (var original in testTimes)
        {
            var utc = ClinicTimeHelper.ToUtc(original);
            var roundTrip = ClinicTimeHelper.ToClinicTime(utc);

            Assert.Equal(original, roundTrip);
            Assert.Equal(TimeSpan.FromHours(7), original - utc);
        }
    }
}
