using Android.Locations;

namespace Gym_App.Services
{
    /// <summary>
    /// Detects whether the device is near the configured gym (home-screen greeting).
    /// </summary>
    public static class GymProximity
    {
        public const double DefaultGymLatitude = GymProximityMath.DefaultGymLatitude;
        public const double DefaultGymLongitude = GymProximityMath.DefaultGymLongitude;
        public const float DefaultGymRadiusMeters = GymProximityMath.DefaultGymRadiusMeters;

        /// <summary>Max age for a cached last-known location used for an instant greeting.</summary>
        public static readonly TimeSpan CachedLocationMaxAge = TimeSpan.FromMinutes(10);

        public static bool IsWithinGymRadius(Location location)
        {
            return GymProximityMath.IsWithinGymRadius(
                location.Latitude,
                location.Longitude,
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

        public static bool IsFreshEnough(Location location, TimeSpan maxAge)
        {
            if (location.Time <= 0)
                return false;

            var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(location.Time);
            return age >= TimeSpan.Zero && age <= maxAge;
        }
    }
}
