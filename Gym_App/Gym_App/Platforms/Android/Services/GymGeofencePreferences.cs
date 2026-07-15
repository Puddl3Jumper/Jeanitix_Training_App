using Android.Content;

namespace Gym_App.Services
{
    internal static class GymGeofencePreferences
    {
        private const string PrefsName = "gym_geofence";
        private const string EnabledKey = "enabled";
        private const string LastNotifyUtcMsKey = "last_notify_utc_ms";

        public static bool IsEnabled(Context context)
        {
            return context.GetSharedPreferences(PrefsName, FileCreationMode.Private)
                ?.GetBoolean(EnabledKey, true) ?? true;
        }

        public static void SetEnabled(Context context, bool enabled)
        {
            context.GetSharedPreferences(PrefsName, FileCreationMode.Private)
                ?.Edit()
                ?.PutBoolean(EnabledKey, enabled)
                ?.Apply();
        }

        public static bool ShouldNotify(Context context, TimeSpan cooldown)
        {
            var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            if (prefs == null)
                return true;

            var lastMs = prefs.GetLong(LastNotifyUtcMsKey, 0L);
            if (lastMs <= 0)
                return true;

            var last = DateTimeOffset.FromUnixTimeMilliseconds(lastMs);
            return DateTimeOffset.UtcNow - last >= cooldown;
        }

        public static void MarkNotified(Context context)
        {
            context.GetSharedPreferences(PrefsName, FileCreationMode.Private)
                ?.Edit()
                ?.PutLong(LastNotifyUtcMsKey, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                ?.Apply();
        }
    }
}
