using Android.App;
using Android.Content;
using Android.Gms.Extensions;
using Android.Gms.Location;
using Android.Util;
using AndroidX.Core.Content;
using Gym_App.Receivers;

namespace Gym_App.Services
{
    public static class GymGeofenceRegistrar
    {
        public const string ActionGeofenceTransition = "com.leiyu.GymJournal.ACTION_GEOFENCE_TRANSITION";
        public const string GeofenceRequestId = "jeanetix_gym";
        private const string LogTag = "GymGeofenceRegistrar";
        private const int PendingIntentRequestCode = 41002;

        public static void TryRegister(Context context)
        {
            if (!GymGeofencePreferences.IsEnabled(context))
            {
                TryUnregister(context);
                return;
            }

            if (ContextCompat.CheckSelfPermission(context, Android.Manifest.Permission.AccessFineLocation)
                != Android.Content.PM.Permission.Granted)
            {
                Log.Info(LogTag, "Fine location not granted; geofence not registered.");
                return;
            }

            _ = RegisterAsync(context.ApplicationContext ?? context);
        }

        public static void TryUnregister(Context context)
        {
            try
            {
                var client = LocationServices.GetGeofencingClient(context);
                var pendingIntent = CreateGeofencePendingIntent(context);
                client.RemoveGeofences(pendingIntent);
            }
            catch (Exception ex)
            {
                Log.Warn(LogTag, $"Unregister failed: {ex.Message}");
            }
        }

        private static async Task RegisterAsync(Context context)
        {
            try
            {
                var client = LocationServices.GetGeofencingClient(context);
                var pendingIntent = CreateGeofencePendingIntent(context);

                try
                {
                    await client.RemoveGeofencesAsync(pendingIntent);
                }
                catch
                {
                    // First registration may have nothing to remove.
                }

                var geofence = new Geofence.Builder()
                    .SetRequestId(GeofenceRequestId)
                    .SetCircularRegion(
                        GymProximityMath.DefaultGymLatitude,
                        GymProximityMath.DefaultGymLongitude,
                        GymProximityMath.DefaultGymRadiusMeters)
                    .SetExpirationDuration(Geofence.NeverExpire)
                    .SetTransitionTypes((int)GeofenceTransitionType.Enter)
                    .Build();

                var request = new GeofencingRequest.Builder()
                    .SetInitialTrigger(GeofencingRequest.InitialTriggerEnter)
                    .AddGeofence(geofence)
                    .Build();

                await client.AddGeofencesAsync(request, pendingIntent);
                Log.Info(LogTag, "Gym geofence registered.");
            }
            catch (Exception ex)
            {
                Log.Warn(LogTag, $"Register failed: {ex}");
            }
        }

        private static PendingIntent CreateGeofencePendingIntent(Context context)
        {
            var intent = new Intent(context, typeof(GymGeofenceReceiver));
            intent.SetAction(ActionGeofenceTransition);
            var flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
            return PendingIntent.GetBroadcast(context, PendingIntentRequestCode, intent, flags);
        }
    }
}
