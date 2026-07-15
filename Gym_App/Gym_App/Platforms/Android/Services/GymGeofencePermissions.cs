using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace Gym_App.Services
{
    /// <summary>
    /// Requests permissions needed for background gym geofence notifications.
    /// </summary>
    public static class GymGeofencePermissions
    {
        public const int RequestFineLocation = 1002;
        public const int RequestBackgroundLocation = 1003;
        public const int RequestPostNotifications = 1004;

        public static bool HasFineLocation(Activity activity)
        {
            return ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.AccessFineLocation)
                == Permission.Granted;
        }

        public static bool HasBackgroundLocation(Activity activity)
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(29))
                return true;

            return ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.AccessBackgroundLocation)
                == Permission.Granted;
        }

        public static bool HasPostNotifications(Activity activity)
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(33))
                return true;

            return ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.PostNotifications)
                == Permission.Granted;
        }

        /// <summary>
        /// Request missing permissions, then invoke <paramref name="onReady"/> when geofence can be registered.
        /// </summary>
        public static void EnsureReady(Activity activity, Action onReady)
        {
            if (!HasFineLocation(activity))
            {
                ActivityCompat.RequestPermissions(activity, new[]
                {
                    Android.Manifest.Permission.AccessFineLocation,
                    Android.Manifest.Permission.AccessCoarseLocation
                }, RequestFineLocation);
                return;
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(33) && !HasPostNotifications(activity))
            {
                ActivityCompat.RequestPermissions(activity, new[]
                {
                    Android.Manifest.Permission.PostNotifications
                }, RequestPostNotifications);
                return;
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(29) && !HasBackgroundLocation(activity))
            {
                ActivityCompat.RequestPermissions(activity, new[]
                {
                    Android.Manifest.Permission.AccessBackgroundLocation
                }, RequestBackgroundLocation);
                return;
            }

            onReady();
        }

        public static void OnPermissionsResult(Activity activity, int requestCode, Action onReady)
        {
            if (requestCode is not (RequestFineLocation or RequestBackgroundLocation or RequestPostNotifications))
                return;

            if (!HasFineLocation(activity))
                return;

            if (requestCode == RequestFineLocation
                && OperatingSystem.IsAndroidVersionAtLeast(33)
                && !HasPostNotifications(activity))
            {
                ActivityCompat.RequestPermissions(activity, new[]
                {
                    Android.Manifest.Permission.PostNotifications
                }, RequestPostNotifications);
                return;
            }

            if ((requestCode == RequestFineLocation || requestCode == RequestPostNotifications)
                && OperatingSystem.IsAndroidVersionAtLeast(29)
                && !HasBackgroundLocation(activity))
            {
                ActivityCompat.RequestPermissions(activity, new[]
                {
                    Android.Manifest.Permission.AccessBackgroundLocation
                }, RequestBackgroundLocation);
                return;
            }

            // Fine location granted; register even if notification/background were denied.
            onReady();
        }
    }
}
