namespace Gym_App.Services
{
    /// <summary>Haversine distance (unit-testable, no Android APIs).</summary>
    public static class GymProximityMath
    {
        public const double DefaultGymLatitude = 32.966413;
        public const double DefaultGymLongitude = -96.713223;
        public const float DefaultGymRadiusMeters = 150f;

        public static bool IsWithinGymRadius(
            double latitude,
            double longitude,
            double gymLatitude,
            double gymLongitude,
            float radiusMeters)
        {
            return DistanceMeters(latitude, longitude, gymLatitude, gymLongitude) <= radiusMeters;
        }

        public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusMeters = 6_371_000d;
            static double ToRadians(double degrees) => degrees * Math.PI / 180d;

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusMeters * c;
        }
    }
}
