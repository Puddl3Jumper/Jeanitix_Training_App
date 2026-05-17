using Android.App;
using Android.Content;
using Android.Gms.Location;
using Android.Util;
using Gym_App.Services;

namespace Gym_App.Receivers
{
    [BroadcastReceiver(Enabled = true, Exported = false)]
    [IntentFilter(new[] { GymGeofenceRegistrar.ActionGeofenceTransition })]
    public class GymGeofenceReceiver : BroadcastReceiver
    {
        private const string LogTag = "GymGeofenceReceiver";
        private static readonly TimeSpan NotifyCooldown = TimeSpan.FromMinutes(30);

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (context == null || intent == null)
                return;

            var geofencingEvent = GeofencingEvent.FromIntent(intent);
            if (geofencingEvent == null)
                return;

            if (geofencingEvent.HasError)
            {
                Log.Warn(LogTag, $"Geofence error: {geofencingEvent.ErrorCode}");
                return;
            }

            if (geofencingEvent.GeofenceTransition != (int)GeofenceTransitionType.Enter)
                return;

            if (!GymGeofencePreferences.IsEnabled(context))
                return;

            if (!GymGeofencePreferences.ShouldNotify(context, NotifyCooldown))
                return;

            var name = context.GetSharedPreferences("user_profile", FileCreationMode.Private)
                ?.GetString("full_name", string.Empty);
            var firstName = GetDisplayFirstName(name);
            GymArrivalNotification.Show(context, firstName);
            GymGeofencePreferences.MarkNotified(context);
        }

        private static string GetDisplayFirstName(string? rawName)
        {
            var name = rawName?.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "User", StringComparison.OrdinalIgnoreCase))
                return "there";

            var firstSpace = name.IndexOf(' ');
            var firstName = firstSpace > 0 ? name[..firstSpace] : name;
            if (firstName.Length == 0)
                return "there";

            return char.ToUpperInvariant(firstName[0]) + firstName[1..].ToLowerInvariant();
        }
    }
}
