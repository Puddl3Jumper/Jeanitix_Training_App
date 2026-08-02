using CoreLocation;

namespace Gym_App.Services
{
    /// <summary>
    /// Requests a one-shot current GPS fix on iOS via <see cref="CLLocationManager.RequestLocation"/>.
    /// The result is delivered asynchronously to the provided callback.
    /// Mirrors the Android GymLocationRefresh (LocationManager) in
    /// Platforms/Android/Services/GymLocationRefresh.cs.
    /// </summary>
    public sealed class GymLocationRefresh : CLLocationManagerDelegate
    {
        private readonly CLLocationManager _manager;
        private readonly Action<CLLocation?> _onComplete;
        private bool _completed;

        private GymLocationRefresh(Action<CLLocation?> onComplete)
        {
            _onComplete = onComplete;
            _manager = new CLLocationManager
            {
                Delegate = this,
                DesiredAccuracy = CLLocation.AccuracyBest
            };
        }

        /// <summary>
        /// Requests a fresh location fix. The callback receives the fix or <c>null</c> on error/timeout.
        /// </summary>
        public static void Request(Action<CLLocation?> onComplete)
        {
            if (!CLLocationManager.LocationServicesEnabled)
            {
                onComplete(null);
                return;
            }

            var status = CLLocationManager.Status;
            if (status == CLAuthorizationStatus.Denied ||
                status == CLAuthorizationStatus.Restricted)
            {
                onComplete(null);
                return;
            }

            var refresh = new GymLocationRefresh(onComplete);
            refresh.Start();
        }

        private void Start()
        {
            // RequestLocation() delivers exactly one fix via LocationsUpdated or Failed, then stops.
            _manager.RequestLocation();
        }

        public override void LocationsUpdated(CLLocationManager manager, CLLocation[] locations)
        {
            if (_completed)
                return;

            _completed = true;
            var best = locations.LastOrDefault();
            Complete(best);
        }

        public override void Failed(CLLocationManager manager, NSError error)
        {
            if (_completed)
                return;

            _completed = true;
            Complete(null);
        }

        private void Complete(CLLocation? location)
        {
            Foundation.NSRunLoop.Main.BeginInvokeOnMainThread(() => _onComplete(location));
        }
    }
}
