using Athkar.Areas.Domain.Reminders;
using Athkar.Shareds.Enums;

namespace Athkar.Tests;

/// <summary>
/// When a campaign actually fires.
///
/// This is the part of the system that is hardest to verify by hand — a
/// scheduling bug you can only reproduce by waiting until Tuesday in Auckland —
/// so it is also the part most worth testing directly. Every case here is about
/// the same underlying fact: a campaign says "21:30" and means twenty-one
/// thirty <b>where the reader is</b>.
/// </summary>
public class ReminderScheduleTests
{
    private static ReminderCampaign FixedAt(int hour, int minute, WeekDays days = WeekDays.All) =>
        new()
        {
            Id = 1,
            Key = "test",
            Kind = ReminderKind.FixedTime,
            Delivery = ReminderDelivery.ServerPush,
            LocalTime = new TimeOnly(hour, minute),
            Days = days,
            IsEnabled = true,
        };

    /// <summary>A zone with no daylight saving, for the cases that are not about it.</summary>
    private static TimeZoneInfo Riyadh => ReminderSchedule.ZoneOrUtc("Asia/Riyadh");

    /// <summary>A zone that does observe it, for the cases that are.</summary>
    private static TimeZoneInfo London => ReminderSchedule.ZoneOrUtc("Europe/London");

    [Fact]
    public void Fires_at_the_local_time_in_the_devices_own_zone()
    {
        // Riyadh is UTC+3 all year, so 21:30 local is 18:30 UTC.
        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var occurrences = ReminderSchedule
            .Occurrences(FixedAt(21, 30), Riyadh, from, from.AddDays(1))
            .ToList();

        Assert.Single(occurrences);
        Assert.Equal(new DateTime(2026, 6, 1, 18, 30, 0, DateTimeKind.Utc), occurrences[0]);
    }

    [Fact]
    public void The_local_time_is_the_fixed_point_across_a_daylight_saving_change()
    {
        // London is UTC+0 in January and UTC+1 in June. A reader who asked for
        // 09:00 means nine o'clock in both — the UTC instant is what moves.
        var winter = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var summer = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var inWinter = ReminderSchedule
            .Occurrences(FixedAt(9, 0), London, winter, winter.AddDays(1))
            .Single();

        var inSummer = ReminderSchedule
            .Occurrences(FixedAt(9, 0), London, summer, summer.AddDays(1))
            .Single();

        Assert.Equal(9, inWinter.Hour);
        Assert.Equal(8, inSummer.Hour);
    }

    [Fact]
    public void A_time_that_does_not_exist_is_pulled_forward_rather_than_thrown()
    {
        // The spring gap: on 29 March 2026 the London clocks jump from 01:00 to
        // 02:00, so 01:30 never happens. The naive conversion throws; this
        // should quietly produce the nearest instant that does exist.
        // A window over the transition night only — a two-day window would
        // legitimately contain the next day's 01:30 as well.
        var from = new DateTime(2026, 3, 28, 22, 0, 0, DateTimeKind.Utc);

        var occurrence = ReminderSchedule
            .Occurrences(FixedAt(1, 30), London, from, from.AddHours(6))
            .Single();

        // 02:00 BST is 01:00 UTC — the moment the clocks jumped to.
        Assert.Equal(new DateTime(2026, 3, 29, 1, 0, 0, DateTimeKind.Utc), occurrence);
    }

    [Fact]
    public void A_time_that_happens_twice_produces_one_reminder()
    {
        // The autumn overlap: on 25 October 2026 London's 01:30 occurs twice.
        // A reader should be reminded once, on the earlier of the two.
        var from = new DateTime(2026, 10, 24, 22, 0, 0, DateTimeKind.Utc);

        var occurrences = ReminderSchedule
            .Occurrences(FixedAt(1, 30), London, from, from.AddHours(6))
            .ToList();

        Assert.Single(occurrences);
        Assert.Equal(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc), occurrences[0]);
    }

    [Fact]
    public void Only_the_selected_weekdays_fire()
    {
        // 1 June 2026 is a Monday, so a Monday-and-Thursday campaign fires
        // twice in the week that follows.
        var from = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc);

        var occurrences = ReminderSchedule
            .Occurrences(FixedAt(12, 0, WeekDays.MondayAndThursday), Riyadh, from, from.AddDays(7))
            .ToList();

        Assert.Equal(2, occurrences.Count);
        Assert.All(
            occurrences,
            instant => Assert.Contains(
                TimeZoneInfo.ConvertTimeFromUtc(instant, Riyadh).DayOfWeek,
                new[] { DayOfWeek.Monday, DayOfWeek.Thursday }));
    }

    [Fact]
    public void A_window_that_straddles_local_midnight_still_finds_the_occurrence()
    {
        // 23:30 in Riyadh is 20:30 UTC, and a window opened at 20:00 UTC is
        // already "tomorrow" in some zones. The walk starts a day early for
        // exactly this.
        var from = new DateTime(2026, 6, 1, 20, 0, 0, DateTimeKind.Utc);

        var occurrence = ReminderSchedule
            .Occurrences(FixedAt(23, 30), Riyadh, from, from.AddHours(1))
            .Single();

        Assert.Equal(new DateTime(2026, 6, 1, 20, 30, 0, DateTimeKind.Utc), occurrence);
    }

    [Fact]
    public void The_window_is_exclusive_at_the_start_so_a_sweep_cannot_double_send()
    {
        // Two passes whose windows touch must not both emit the same instant,
        // or a reader is reminded twice.
        var boundary = new DateTime(2026, 6, 1, 18, 30, 0, DateTimeKind.Utc);
        var campaign = FixedAt(21, 30);

        var first = ReminderSchedule
            .Occurrences(campaign, Riyadh, boundary.AddMinutes(-30), boundary)
            .ToList();

        var second = ReminderSchedule
            .Occurrences(campaign, Riyadh, boundary, boundary.AddMinutes(30))
            .ToList();

        Assert.Single(first);
        Assert.Empty(second);
    }

    [Theory]
    [InlineData(false, ReminderKind.FixedTime, WeekDays.All)]
    [InlineData(true, ReminderKind.FixedTime, WeekDays.None)]
    [InlineData(true, ReminderKind.PrayerAnchored, WeekDays.All)]
    public void Campaigns_that_cannot_fire_produce_nothing(
        bool enabled, ReminderKind kind, WeekDays days)
    {
        var campaign = FixedAt(12, 0);
        campaign.IsEnabled = enabled;
        campaign.Kind = kind;
        campaign.Days = days;

        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Empty(ReminderSchedule.Occurrences(campaign, Riyadh, from, from.AddDays(7)));
    }

    [Fact]
    public void An_anchored_campaign_is_never_scheduled_on_the_server()
    {
        // It cannot be: the server has no coordinates. The device schedules it.
        // See docs/BUSINESS_LOGIC.md §5.
        var campaign = FixedAt(12, 0);
        campaign.Kind = ReminderKind.PrayerAnchored;
        campaign.Anchor = PrayerAnchor.Sunrise;

        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Empty(ReminderSchedule.Occurrences(campaign, Riyadh, from, from.AddDays(7)));
    }

    [Fact]
    public void An_unknown_timezone_falls_back_to_utc_rather_than_failing()
    {
        // A device may send a zone this machine has never heard of. Losing the
        // local hour for that one install is a far smaller failure than
        // refusing to schedule anything for it.
        Assert.Equal(TimeZoneInfo.Utc, ReminderSchedule.ZoneOrUtc("Mars/Olympus_Mons"));
        Assert.Equal(TimeZoneInfo.Utc, ReminderSchedule.ZoneOrUtc(null));
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday, WeekDays.Sunday)]
    [InlineData(DayOfWeek.Friday, WeekDays.Friday)]
    [InlineData(DayOfWeek.Saturday, WeekDays.Saturday)]
    public void The_weekday_mask_matches_the_calendar(DayOfWeek day, WeekDays flag)
    {
        Assert.Equal(flag, ReminderSchedule.ToFlag(day));
        Assert.True(ReminderSchedule.Includes(WeekDays.All, day));
        Assert.True(ReminderSchedule.Includes(flag, day));
        Assert.False(ReminderSchedule.Includes(WeekDays.None, day));
    }
}
