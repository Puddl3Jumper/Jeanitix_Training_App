using CoreLocation;

namespace Gym_App.Services
{
    /// <summary>
    /// Detects whether the device is near the configured gym on iOS using <see cref="CLLocation"/>.
    /// Mirrors the Android GymProximity (Android.Locations.Location) in
    /// Platforms/Android/Services/GymProximity.cs.
    /// </summary>
    public static class GymProximity
    {
        public const double DefaultGymLatitude  = GymProximityMath.DefaultGymLatitude;
        public const double DefaultGymLongitude = GymProximityMath.DefaultGymLongitude;
        public const float  DefaultGymRadiusMeters = GymProximityMath.DefaultGymRadiusMeters;

        /// <summary>Max age for a cached last-known location used for an instant greeting.</summary>
        public static readonly TimeSpan CachedLocationMaxAge = TimeSpan.FromMinutes(10);

        public static bool IsWithinGymRadius(CLLocation location)
        {
            return GymProximityMath.IsWithinGymRadius(
                location.Coordinate.Latitude,
                location.Coordinate.Longitude,
                DefaultGymLatitude,
                DefaultGymLongitude,
                DefaultGymRadiusMeters);
        }

        public static bool IsWithinGymRadius(
            double latitude,
            double longitude,
            double gymLatitude,
            double gymLongitude,
            float radiusMeters)
            => GymProximityMath.IsWithinGymRadius(latitude, longitude, gymLatitude, gymLongitude, radiusMeters);

        /// <summary>
        /// Returns true when the location fix is recent enough to use for an instant greeting.
        /// </summary>
        public static bool IsFreshEnough(CLLocation location, TimeSpan maxAge)
        {
            var timestamp = location.Timestamp.ToDateTimeOffset();
            var age = DateTimeOffset.UtcNow - timestamp;
            return age >= TimeSpan.Zero && age <= maxAge;
        }
    }
}
