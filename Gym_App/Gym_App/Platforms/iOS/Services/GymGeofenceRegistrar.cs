using CoreLocation;

namespace Gym_App.Services
{
    /// <summary>
    /// Registers and unregisters the gym circular region with CLLocationManager for iOS geofencing.
    /// Mirrors the Android GymGeofenceRegistrar (Google Play Geofencing API) in
    /// Platforms/Android/Services/GymGeofenceRegistrar.cs.
    /// </summary>
    public static class GymGeofenceRegistrar
    {
        /// <summary>Identifier used for the monitored CLCircularRegion.</summary>
        public const string GeofenceIdentifier = "jeanetix_gym";

        // The CLLocationManager and its delegate must remain alive for the duration of monitoring.
        private static CLLocationManager? _locationManager;
        private static GymGeofenceDelegate? _delegate;

        /// <summary>
        /// Registers the gym geofence if the feature is enabled and location permission has been granted.
        /// If the feature is disabled, any existing registration is removed.
        /// </summary>
        public static void TryRegister()
        {
            if (!GymGeofencePreferences.IsEnabled())
            {
                TryUnregister();
                return;
            }

            if (!CLLocationManager.LocationServicesEnabled)
                return;

            var status = CLLocationManager.Status;
            if (status != CLAuthorizationStatus.AuthorizedAlways &&
                status != CLAuthorizationStatus.AuthorizedWhenInUse)
            {
                return;
            }

            EnsureManager();
            RegisterRegion();
        }

        /// <summary>Removes the gym geofence from CLLocationManager monitoring.</summary>
        public static void TryUnregister()
        {
            if (_locationManager == null)
                return;

            foreach (var region in _locationManager.MonitoredRegions)
            {
                if (region is CLCircularRegion cr && cr.Identifier == GeofenceIdentifier)
                {
                    _locationManager.StopMonitoring(cr);
                }
            }
        }

        private static void EnsureManager()
        {
            if (_locationManager != null)
                return;

            _delegate = new GymGeofenceDelegate();
            _locationManager = new CLLocationManager
            {
                Delegate = _delegate
            };
        }

        private static void RegisterRegion()
        {
            if (_locationManager == null)
                return;

            // Remove stale registration before adding.
            TryUnregister();

            var region = new CLCircularRegion(
                new CLLocationCoordinate2D(
                    GymProximityMath.DefaultGymLatitude,
                    GymProximityMath.DefaultGymLongitude),
                GymProximityMath.DefaultGymRadiusMeters,
                GeofenceIdentifier)
            {
                NotifyOnEntry = true,
                NotifyOnExit  = false
            };

            _locationManager.StartMonitoring(region);
        }
    }
}
