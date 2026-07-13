namespace Gym_App.Services;

/// <summary>
/// Pure scheduling logic for the daily workout reminder.
/// Kept Android-free so it can be fully unit-tested on net10.0.
/// </summary>
public static class ReminderScheduler
{
    // Bitmask constants — index matches position in the M T W T F S S day strip.
    public const int MondayBit    = 1;
    public const int TuesdayBit   = 2;
    public const int WednesdayBit = 4;
    public const int ThursdayBit  = 8;
    public const int FridayBit    = 16;
    public const int SaturdayBit  = 32;
    public const int SundayBit    = 64;

    // Default: Mon–Fri
    public const int DefaultDaysMask = MondayBit | TuesdayBit | WednesdayBit | ThursdayBit | FridayBit;

    /// <summary>
    /// Returns true when the given day-of-week is set in the bitmask.
    /// </summary>
    /// <param name="mask">The saved days bitmask.</param>
    /// <param name="dayOfWeek">DayOfWeek value (Monday … Sunday).</param>
    public static bool IsDaySelected(int mask, DayOfWeek dayOfWeek)
    {
        var bit = DayOfWeekToBit(dayOfWeek);
        return (mask & bit) != 0;
    }

    /// <summary>
    /// Returns the next UTC trigger time for a given day-of-week and local wall-clock time,
    /// evaluated from <paramref name="now"/>. If the target time is in the past for this week,
    /// it returns the same slot next week.
    /// </summary>
    public static DateTime GetNextTriggerUtc(DayOfWeek dayOfWeek, int hour, int minute, DateTime now)
    {
        // Build candidate in local time on the same week
        var candidate = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Local);

        // Advance to the correct day-of-week in the current week
        int daysUntil = ((int)dayOfWeek - (int)candidate.DayOfWeek + 7) % 7;
        candidate = candidate.AddDays(daysUntil);

        // If that slot is in the past (or right now), push to next week
        if (candidate <= now)
            candidate = candidate.AddDays(7);

        return candidate.ToUniversalTime();
    }

    /// <summary>
    /// Returns all upcoming trigger times (one per selected day) sorted ascending.
    /// </summary>
    public static IReadOnlyList<(DayOfWeek Day, DateTime TriggerUtc)> GetScheduledTriggers(
        int daysMask, int hour, int minute, DateTime now)
    {
        var results = new List<(DayOfWeek Day, DateTime TriggerUtc)>();
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            if (IsDaySelected(daysMask, day))
                results.Add((day, GetNextTriggerUtc(day, hour, minute, now)));
        }
        results.Sort((a, b) => a.TriggerUtc.CompareTo(b.TriggerUtc));
        return results;
    }

    private static int DayOfWeekToBit(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday    => MondayBit,
        DayOfWeek.Tuesday   => TuesdayBit,
        DayOfWeek.Wednesday => WednesdayBit,
        DayOfWeek.Thursday  => ThursdayBit,
        DayOfWeek.Friday    => FridayBit,
        DayOfWeek.Saturday  => SaturdayBit,
        DayOfWeek.Sunday    => SundayBit,
        _                   => 0
    };
}
