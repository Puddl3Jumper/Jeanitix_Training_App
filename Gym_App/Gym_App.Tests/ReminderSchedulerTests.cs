using Gym_App.Services;
using Xunit;

namespace Gym_App.Tests;

public class ReminderSchedulerTests
{
    // ── IsDaySelected ─────────────────────────────────────────────────────────

    [Fact]
    public void IsDaySelected_Monday_ReturnsTrueWhenMondayBitSet()
    {
        Assert.True(ReminderScheduler.IsDaySelected(ReminderScheduler.MondayBit, DayOfWeek.Monday));
    }

    [Fact]
    public void IsDaySelected_Monday_ReturnsFalseWhenMondayBitNotSet()
    {
        int maskWithoutMonday = ReminderScheduler.DefaultDaysMask & ~ReminderScheduler.MondayBit;
        Assert.False(ReminderScheduler.IsDaySelected(maskWithoutMonday, DayOfWeek.Monday));
    }

    [Fact]
    public void IsDaySelected_DefaultMask_ContainsWeekdaysOnly()
    {
        var mask = ReminderScheduler.DefaultDaysMask;
        Assert.True(ReminderScheduler.IsDaySelected(mask, DayOfWeek.Monday));
        Assert.True(ReminderScheduler.IsDaySelected(mask, DayOfWeek.Tuesday));
        Assert.True(ReminderScheduler.IsDaySelected(mask, DayOfWeek.Wednesday));
        Assert.True(ReminderScheduler.IsDaySelected(mask, DayOfWeek.Thursday));
        Assert.True(ReminderScheduler.IsDaySelected(mask, DayOfWeek.Friday));
        Assert.False(ReminderScheduler.IsDaySelected(mask, DayOfWeek.Saturday));
        Assert.False(ReminderScheduler.IsDaySelected(mask, DayOfWeek.Sunday));
    }

    // ── GetNextTriggerUtc ─────────────────────────────────────────────────────

    [Fact]
    public void GetNextTrigger_MondayAt7am_WhenNowIsMondayBefore7am_TriggersToday()
    {
        // "Now" is Monday 06:00 local
        var now = NextWeekday(DayOfWeek.Monday).Date.AddHours(6);

        var trigger = ReminderScheduler.GetNextTriggerUtc(DayOfWeek.Monday, hour: 7, minute: 0, now);

        var localTrigger = trigger.ToLocalTime();
        Assert.Equal(DayOfWeek.Monday, localTrigger.DayOfWeek);
        Assert.Equal(7, localTrigger.Hour);
        Assert.Equal(0, localTrigger.Minute);
    }

    [Fact]
    public void GetNextTrigger_MondayAt7am_WhenNowIsMondayAfter7am_TriggersNextMonday()
    {
        // "Now" is Monday 08:00 local — the 7am slot has already passed
        var now = NextWeekday(DayOfWeek.Monday).Date.AddHours(8);

        var trigger = ReminderScheduler.GetNextTriggerUtc(DayOfWeek.Monday, hour: 7, minute: 0, now);

        var localTrigger = trigger.ToLocalTime();
        Assert.Equal(DayOfWeek.Monday, localTrigger.DayOfWeek);
        Assert.Equal(7, localTrigger.Hour);
        // Must be next week (7 days later)
        Assert.True((localTrigger - now).TotalDays >= 6);
    }

    [Fact]
    public void GetNextTrigger_MondayAt7am_WhenNowIsSunday_TriggersNextDay()
    {
        // "Now" is Sunday 20:00 local
        var now = NextWeekday(DayOfWeek.Sunday).Date.AddHours(20);

        var trigger = ReminderScheduler.GetNextTriggerUtc(DayOfWeek.Monday, hour: 7, minute: 0, now);

        var localTrigger = trigger.ToLocalTime();
        Assert.Equal(DayOfWeek.Monday, localTrigger.DayOfWeek);
        Assert.Equal(7, localTrigger.Hour);
        Assert.True((localTrigger - now).TotalHours < 24);
    }

    [Fact]
    public void GetNextTrigger_TriggerIsAlwaysInFuture()
    {
        var now = DateTime.Now;
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            var trigger = ReminderScheduler.GetNextTriggerUtc(day, hour: 7, minute: 0, now);
            Assert.True(trigger > now.ToUniversalTime(),
                $"Trigger for {day} should be in the future");
        }
    }

    // ── GetScheduledTriggers ──────────────────────────────────────────────────

    [Fact]
    public void GetScheduledTriggers_MondayOnlyMask_ReturnsSingleEntry()
    {
        var now = DateTime.Now;
        var triggers = ReminderScheduler.GetScheduledTriggers(ReminderScheduler.MondayBit, 7, 0, now);

        Assert.Single(triggers);
        Assert.Equal(DayOfWeek.Monday, triggers[0].Day);
    }

    [Fact]
    public void GetScheduledTriggers_DefaultMask_ReturnsFiveWeekdayEntries()
    {
        var now = DateTime.Now;
        var triggers = ReminderScheduler.GetScheduledTriggers(ReminderScheduler.DefaultDaysMask, 7, 0, now);

        Assert.Equal(5, triggers.Count);
        var days = triggers.Select(t => t.Day).ToHashSet();
        Assert.Contains(DayOfWeek.Monday, days);
        Assert.Contains(DayOfWeek.Friday, days);
        Assert.DoesNotContain(DayOfWeek.Saturday, days);
        Assert.DoesNotContain(DayOfWeek.Sunday, days);
    }

    [Fact]
    public void GetScheduledTriggers_AllDaysMask_ReturnsSevenEntries()
    {
        const int allDays = ReminderScheduler.MondayBit | ReminderScheduler.TuesdayBit |
                            ReminderScheduler.WednesdayBit | ReminderScheduler.ThursdayBit |
                            ReminderScheduler.FridayBit | ReminderScheduler.SaturdayBit |
                            ReminderScheduler.SundayBit;
        var triggers = ReminderScheduler.GetScheduledTriggers(allDays, 7, 0, DateTime.Now);
        Assert.Equal(7, triggers.Count);
    }

    [Fact]
    public void GetScheduledTriggers_ZeroMask_ReturnsEmpty()
    {
        var triggers = ReminderScheduler.GetScheduledTriggers(0, 7, 0, DateTime.Now);
        Assert.Empty(triggers);
    }

    [Fact]
    public void GetScheduledTriggers_TriggersAreSortedAscending()
    {
        var triggers = ReminderScheduler.GetScheduledTriggers(ReminderScheduler.DefaultDaysMask, 7, 0, DateTime.Now);
        for (int i = 1; i < triggers.Count; i++)
            Assert.True(triggers[i].TriggerUtc >= triggers[i - 1].TriggerUtc);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// Returns the next occurrence of the given day starting from today (inclusive).
    private static DateTime NextWeekday(DayOfWeek target)
    {
        var d = DateTime.Today;
        while (d.DayOfWeek != target)
            d = d.AddDays(1);
        return d;
    }
}
