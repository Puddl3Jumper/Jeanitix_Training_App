using CoreLocation;

namespace Gym_App.Services
{
    /// <summary>
    /// Handles entry callbacks from CLLocationManager for the gym geofence.
    /// Shows an arrival notification via <see cref="GymArrivalNotification"/> when the user enters the gym region.
    /// </summary>
    internal sealed class GymGeofenceDelegate : CLLocationManagerDelegate
    {
        private static readonly TimeSpan NotifyCooldown = TimeSpan.FromMinutes(30);

        public override void RegionEntered(CLLocationManager manager, CLRegion region)
        {
            if (region.Identifier != GymGeofenceRegistrar.GeofenceIdentifier)
                return;

            if (!GymGeofencePreferences.IsEnabled())
                return;

            if (!GymGeofencePreferences.ShouldNotify(NotifyCooldown))
                return;

            GymGeofencePreferences.MarkNotified();
            GymArrivalNotification.Show(GetDisplayFirstName());
        }

        public override void MonitoringFailed(CLLocationManager manager, CLRegion region, NSError error)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[GymGeofenceDelegate] Monitoring failed for region '{region.Identifier}': {error?.LocalizedDescription}");
        }

        private static string GetDisplayFirstName()
        {
            var rawName = Foundation.NSUserDefaults.StandardUserDefaults
                .StringForKey("user_profile.full_name")?.Trim();

            if (string.IsNullOrWhiteSpace(rawName) ||
                string.Equals(rawName, "User", StringComparison.OrdinalIgnoreCase))
            {
                return "there";
            }

            var firstSpace = rawName.IndexOf(' ');
            var firstName = firstSpace > 0 ? rawName[..firstSpace] : rawName;
            return firstName.Length == 0
                ? "there"
                : char.ToUpperInvariant(firstName[0]) + firstName[1..].ToLowerInvariant();
        }
    }
}
