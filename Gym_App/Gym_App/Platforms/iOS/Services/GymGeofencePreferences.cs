using Foundation;

namespace Gym_App.Services
{
    /// <summary>
    /// Persists gym geofence feature flags and notification cooldown on iOS via NSUserDefaults.
    /// Mirrors the Android GymGeofencePreferences in Platforms/Android/Services/GymGeofencePreferences.cs.
    /// </summary>
    internal static class GymGeofencePreferences
    {
        // Keys are prefixed to mirror Android SharedPreferences namespacing.
        private const string EnabledKey         = "gym_geofence.enabled";
        private const string LastNotifyUtcMsKey = "gym_geofence.last_notify_utc_ms";

        public static bool IsEnabled()
        {
            var defaults = NSUserDefaults.StandardUserDefaults;
            // Default to enabled when the key hasn't been explicitly set yet.
            if (defaults.ValueForKey(new NSString(EnabledKey)) == null)
                return true;

            return defaults.BoolForKey(EnabledKey);
        }

        public static void SetEnabled(bool enabled)
        {
            NSUserDefaults.StandardUserDefaults.SetBool(enabled, EnabledKey);
        }

        public static bool ShouldNotify(TimeSpan cooldown)
        {
            var lastMs = NSUserDefaults.StandardUserDefaults.DoubleForKey(LastNotifyUtcMsKey);
            if (lastMs <= 0)
                return true;

            var last = DateTimeOffset.FromUnixTimeMilliseconds((long)lastMs);
            return DateTimeOffset.UtcNow - last >= cooldown;
        }

        public static void MarkNotified()
        {
            NSUserDefaults.StandardUserDefaults.SetDouble(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                LastNotifyUtcMsKey);
        }
    }
}
