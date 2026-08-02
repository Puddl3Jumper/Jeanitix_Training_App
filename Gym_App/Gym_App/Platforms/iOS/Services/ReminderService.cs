using Foundation;
using UserNotifications;
using Gym_App.Data;

namespace Gym_App.Services
{
    /// <summary>
    /// Schedules and cancels daily workout reminder local notifications on iOS via
    /// <see cref="UNUserNotificationCenter"/>.
    /// Mirrors the Android ReminderService (foreground Service + TTS) in
    /// Platforms/Android/Services/ReminderService.cs.
    ///
    /// On iOS, background audio/TTS is not available from a geofence/alarm wake-up in the same
    /// way as Android. Instead, a <see cref="UNCalendarNotificationTrigger"/> fires a rich
    /// local notification at the scheduled time. The notification body shows today's workout
    /// rotation and is generated once at schedule time.
    /// </summary>
    public static class ReminderService
    {
        private const string NotificationCategoryId = "workout_reminder";

        /// <summary>
        /// Schedules one local notification per selected day/time combination.
        /// Any existing workout-reminder notifications are first removed.
        /// </summary>
        public static void ScheduleReminders(int daysMask, int hour, int minute)
        {
            UNUserNotificationCenter.Current.RequestAuthorization(
                UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge,
                (granted, _) =>
                {
                    if (!granted)
                        return;

                    CancelAllReminders(thenSchedule: () =>
                        AddNotificationsForMask(daysMask, hour, minute));
                });
        }

        /// <summary>Cancels all pending workout-reminder notifications.</summary>
        public static void CancelAllReminders()
        {
            CancelAllReminders(thenSchedule: null);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private static void CancelAllReminders(Action? thenSchedule)
        {
            // Enumerate pending identifiers that belong to this feature.
            UNUserNotificationCenter.Current.GetPendingNotificationRequests(requests =>
            {
                var ids = requests
                    .Where(r => r.Identifier.StartsWith("workout_reminder_", StringComparison.Ordinal))
                    .Select(r => r.Identifier)
                    .ToArray();

                if (ids.Length > 0)
                    UNUserNotificationCenter.Current.RemovePendingNotificationRequests(ids);

                thenSchedule?.Invoke();
            });
        }

        private static void AddNotificationsForMask(int daysMask, int hour, int minute)
        {
            var db = new GymDatabase();
            var groups = db.GetDailyWorkoutGroups();
            var rotationText = groups.Length >= 3
                ? $"{groups[0]}, {groups[1]} and {groups[2]}"
                : string.Join(", ", groups);

            var body = $"Today: {rotationText} 💪";

            var triggers = ReminderScheduler.GetScheduledTriggers(
                daysMask, hour, minute, DateTime.Now);

            foreach (var (day, _) in triggers)
            {
                var id = $"workout_reminder_{(int)day}";

                var content = new UNMutableNotificationContent
                {
                    Title    = "Jeanetix Reminder 🏋️",
                    Body     = body,
                    Sound    = UNNotificationSound.Default,
                    ThreadIdentifier = NotificationCategoryId
                };

                // UNCalendarNotificationTrigger repeats weekly on the same day/time.
                var components = new NSDateComponents
                {
                    Weekday = ToNSWeekday(day),
                    Hour    = hour,
                    Minute  = minute,
                    Second  = 0
                };

                var trigger = UNCalendarNotificationTrigger.CreateTrigger(
                    components, repeats: true);

                var request = UNNotificationRequest.FromIdentifier(id, content, trigger);
                UNUserNotificationCenter.Current.AddNotificationRequest(request, error =>
                {
                    if (error != null)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[ReminderService] Failed to schedule {day}: {error.LocalizedDescription}");
                    }
                });
            }
        }

        /// <summary>
        /// Converts <see cref="DayOfWeek"/> to NSDateComponents.Weekday (Sunday=1 … Saturday=7).
        /// </summary>
        private static nint ToNSWeekday(DayOfWeek day) => day switch
        {
            DayOfWeek.Sunday    => 1,
            DayOfWeek.Monday    => 2,
            DayOfWeek.Tuesday   => 3,
            DayOfWeek.Wednesday => 4,
            DayOfWeek.Thursday  => 5,
            DayOfWeek.Friday    => 6,
            DayOfWeek.Saturday  => 7,
            _                   => 2
        };
    }
}
