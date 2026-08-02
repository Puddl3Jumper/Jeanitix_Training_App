using CoreLocation;
using UIKit;

namespace Gym_App.Services
{
    /// <summary>
    /// Requests the location permissions needed for background gym geofence notifications on iOS.
    /// Mirrors the Android GymGeofencePermissions in Platforms/Android/Services/GymGeofencePermissions.cs.
    /// </summary>
    public static class GymGeofencePermissions
    {
        /// <summary>Returns true when Always location authorization has been granted.</summary>
        public static bool HasAlwaysAuthorization()
        {
            return CLLocationManager.Status == CLAuthorizationStatus.AuthorizedAlways;
        }

        /// <summary>Returns true when at-least When-In-Use location authorization has been granted.</summary>
        public static bool HasWhenInUseAuthorization()
        {
            var status = CLLocationManager.Status;
            return status == CLAuthorizationStatus.AuthorizedWhenInUse ||
                   status == CLAuthorizationStatus.AuthorizedAlways;
        }

        /// <summary>
        /// Requests the permissions required for geofence monitoring and invokes
        /// <paramref name="onReady"/> when authorization is sufficient to register geofences.
        /// On iOS 14+ Always authorization is required for background delivery; on earlier
        /// versions When-In-Use is used as a fallback.
        /// </summary>
        public static void EnsureReady(CLLocationManager locationManager, Action onReady)
        {
            var status = CLLocationManager.Status;

            if (status == CLAuthorizationStatus.AuthorizedAlways)
            {
                onReady();
                return;
            }

            if (status == CLAuthorizationStatus.NotDetermined)
            {
                // Request WhenInUse first; the system will upgrade the prompt to Always
                // when the app requests background authorization on iOS 13+.
                locationManager.RequestAlwaysAuthorization();
                // onReady will be called from the delegate's DidChangeAuthorizationStatus callback.
                return;
            }

            if (status == CLAuthorizationStatus.AuthorizedWhenInUse)
            {
                // Try to upgrade to Always for background geofencing.
                locationManager.RequestAlwaysAuthorization();
                // Proceed with WhenInUse for now — the region may still be delivered in foreground.
                onReady();
            }

            // Denied or restricted — cannot proceed.
        }

        /// <summary>
        /// Call this from the delegate's <c>DidChangeAuthorizationStatus</c> method to react to
        /// authorization changes and finish geofence setup.
        /// </summary>
        public static void OnAuthorizationChanged(
            CLAuthorizationStatus status,
            Action onReady)
        {
            if (status == CLAuthorizationStatus.AuthorizedAlways ||
                status == CLAuthorizationStatus.AuthorizedWhenInUse)
            {
                onReady();
            }
        }
    }
}
