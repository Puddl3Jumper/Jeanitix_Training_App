using System.Collections.Generic;
using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.Content;

namespace Gym_App.Services
{
    /// <summary>
    /// Requests a current GPS/network fix (not only stale last-known location).
    /// </summary>
    public sealed class GymLocationRefresh : Java.Lang.Object, ILocationListener
    {
        private readonly LocationManager? _locationManager;
        private readonly Action<Location?> _onComplete;
        private readonly Handler _mainHandler;
        private bool _completed;
        private readonly Runnable _timeoutRunnable;

        private GymLocationRefresh(Context context, Action<Location?> onComplete)
        {
            _locationManager = context.GetSystemService(Context.LocationService) as LocationManager;
            _onComplete = onComplete;
            _mainHandler = new Handler(Looper.MainLooper!);
            _timeoutRunnable = new Runnable(() => Complete(null));
        }

        public static void Request(Context context, Action<Location?> onComplete, int timeoutMs = 12_000)
        {
            var refresh = new GymLocationRefresh(context.ApplicationContext ?? context, onComplete);
            refresh.Start(context, timeoutMs);
        }

        private void Start(Context context, int timeoutMs)
        {
            if (_locationManager == null)
            {
                Complete(null);
                return;
            }

            if (!IsLocationEnabled(_locationManager))
            {
                Complete(null);
                return;
            }

            if (ContextCompat.CheckSelfPermission(
                    context,
                    Android.Manifest.Permission.AccessFineLocation) != Android.Content.PM.Permission.Granted)
            {
                Complete(null);
                return;
            }

            var providers = new List<string>
            {
                LocationManager.GpsProvider,
                LocationManager.NetworkProvider
            };
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                providers.Add(LocationManager.FusedProvider);
            }

            var requested = false;
            foreach (var provider in providers)
            {
                if (!_locationManager.IsProviderEnabled(provider))
                    continue;

                try
                {
#pragma warning disable CA1422
                    _locationManager.RequestLocationUpdates(
                        provider,
                        0L,
                        0f,
                        this,
                        Looper.MainLooper!);
#pragma warning restore CA1422
                    requested = true;
                }
                catch
                {
                    // Provider may reject zero-interval updates on some devices.
                }
            }

            if (!requested)
            {
                Complete(GetLastKnown(_locationManager));
                return;
            }

            _mainHandler.PostDelayed(_timeoutRunnable, timeoutMs);
        }

        private static bool IsLocationEnabled(LocationManager manager)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(28))
            {
                return manager.IsLocationEnabled;
            }

            return manager.IsProviderEnabled(LocationManager.GpsProvider)
                || manager.IsProviderEnabled(LocationManager.NetworkProvider);
        }

        private static Location? GetLastKnown(LocationManager manager)
        {
            foreach (var provider in new[] { LocationManager.GpsProvider, LocationManager.NetworkProvider, LocationManager.PassiveProvider })
            {
                try
                {
                    var loc = manager.GetLastKnownLocation(provider);
                    if (loc != null)
                        return loc;
                }
                catch
                {
                    // Ignore per-provider failures.
                }
            }

            return null;
        }

        public void OnLocationChanged(Location location)
        {
            if (_completed || location == null)
                return;

            Complete(location);
        }

        public void OnProviderDisabled(string provider)
        {
        }

        public void OnProviderEnabled(string provider)
        {
        }

        public void OnStatusChanged(string? provider, [GeneratedEnum] Availability status, Bundle? extras)
        {
        }

        private void Complete(Location? location)
        {
            if (_completed)
                return;

            _completed = true;
            _mainHandler.RemoveCallbacks(_timeoutRunnable);

            if (_locationManager != null)
            {
                try
                {
                    _locationManager.RemoveUpdates(this);
                }
                catch
                {
                    // Ignore cleanup failures.
                }
            }

            var resolved = location ?? (_locationManager == null ? null : GetLastKnown(_locationManager));
            _mainHandler.Post(() => _onComplete(resolved));
        }
    }
}
