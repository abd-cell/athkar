using Athkar.Shareds.Enums;

namespace Athkar.Areas.Domain.Reminders;

/// <summary>
/// Turns a campaign's recurrence into the UTC instants it fires at for one
/// device.
///
/// Pure, static and free of infrastructure so it can be reasoned about and
/// tested directly — the alternative is a scheduling bug you can only reproduce
/// by waiting until Tuesday in Auckland.
///
/// The whole difficulty is that a campaign says "21:30" and means twenty-one
/// thirty <b>where the reader is</b>. So the walk happens in the device's local
/// calendar and only the final instant is converted, which is also what makes
/// daylight saving fall out correctly: the local time is the fixed point and the
/// UTC offset is whatever the zone says it is that day.
/// </summary>
public static class ReminderSchedule
{
    /// <summary>
    /// Every instant this campaign fires at for <paramref name="timeZone"/>
    /// strictly after <paramref name="fromUtc"/> and no later than
    /// <paramref name="untilUtc"/>.
    ///
    /// Returns nothing for a campaign that is disabled, has no days, is not a
    /// server-pushed fixed-time one, or has no time of day — each of which is a
    /// campaign that legitimately produces no sends rather than an error.
    /// </summary>
    public static IEnumerable<DateTime> Occurrences(
        ReminderCampaign campaign, TimeZoneInfo timeZone, DateTime fromUtc, DateTime untilUtc)
    {
        if (!campaign.IsEnabled) yield break;
        if (campaign.Days == WeekDays.None) yield break;
        if (campaign.Kind != ReminderKind.FixedTime) yield break;
        if (campaign.LocalTime is not { } localTime) yield break;

        var fromLocal = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, timeZone);
        var untilLocal = TimeZoneInfo.ConvertTimeFromUtc(untilUtc, timeZone);

        // Start a day early and end a day late. A horizon of a few hours can
        // still straddle a date boundary in the local calendar, and an offset
        // change can move an instant across one; the emitted values are filtered
        // against the real UTC window below, so the extra days cost nothing.
        for (var day = fromLocal.Date.AddDays(-1); day <= untilLocal.Date.AddDays(1); day = day.AddDays(1))
        {
            if (!Includes(campaign.Days, day.DayOfWeek)) continue;

            var instant = ToUtc(day.Add(localTime.ToTimeSpan()), timeZone);
            if (instant > fromUtc && instant <= untilUtc) yield return instant;
        }
    }

    /// <summary>True when the mask selects <paramref name="day"/>.</summary>
    public static bool Includes(WeekDays days, DayOfWeek day) =>
        (days & ToFlag(day)) != 0;

    public static WeekDays ToFlag(DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => WeekDays.Sunday,
        DayOfWeek.Monday => WeekDays.Monday,
        DayOfWeek.Tuesday => WeekDays.Tuesday,
        DayOfWeek.Wednesday => WeekDays.Wednesday,
        DayOfWeek.Thursday => WeekDays.Thursday,
        DayOfWeek.Friday => WeekDays.Friday,
        _ => WeekDays.Saturday,
    };

    /// <summary>
    /// A local wall-clock time as a UTC instant, with the two answers the naive
    /// conversion gets wrong twice a year.
    ///
    /// In the spring gap the stated time does not exist, and
    /// <c>ConvertTimeToUtc</c> throws rather than choosing; the reminder is
    /// pulled forward to the moment the clocks jump to, which is the nearest
    /// instant a reader would recognise as "about then". In the autumn overlap
    /// it happens twice, and the earlier of the two is taken — one reminder, not
    /// two, and on the side that is not already late.
    /// </summary>
    public static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(unspecified))
        {
            // Walk forward in minutes to the first valid instant. The gap is an
            // hour at most anywhere in the world, so the loop is bounded and
            // short, and this is far clearer than reaching for the adjustment
            // rules to compute the delta.
            for (var minutes = 1; minutes <= 120; minutes++)
            {
                var shifted = unspecified.AddMinutes(minutes);
                if (!timeZone.IsInvalidTime(shifted))
                    return TimeZoneInfo.ConvertTimeToUtc(shifted, timeZone);
            }
        }

        if (timeZone.IsAmbiguousTime(unspecified))
        {
            // The larger offset is the pre-transition (summer) one, which is the
            // earlier of the two instants.
            var offsets = timeZone.GetAmbiguousTimeOffsets(unspecified);

            // Specified explicitly: subtracting an offset yields an Unspecified
            // DateTime, and every other path here returns a Utc one. A value
            // that is right but mislabelled is worse than one that is wrong,
            // because nothing downstream will notice.
            return DateTime.SpecifyKind(unspecified - offsets.Max(), DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
    }

    /// <summary>
    /// The zone, or UTC when the host has never heard of it. A device that sent
    /// a zone this machine lacks still deserves its reminders, just not at a
    /// local hour anybody can vouch for.
    /// </summary>
    public static TimeZoneInfo ZoneOrUtc(string? id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)
            ? zone
            : TimeZoneInfo.Utc;
}
