using Gym_App.Services;
using Xunit;

namespace Gym_App.Tests;

public class GymProximityTests
{
    [Fact]
    public void IsWithinGymRadius_AtGymCoordinates_ReturnsTrue()
    {
        var atGym = GymProximityMath.IsWithinGymRadius(
            GymProximityMath.DefaultGymLatitude,
            GymProximityMath.DefaultGymLongitude,
            GymProximityMath.DefaultGymLatitude,
            GymProximityMath.DefaultGymLongitude,
            GymProximityMath.DefaultGymRadiusMeters);

        Assert.True(atGym);
    }

    [Fact]
    public void IsWithinGymRadius_FarFromGym_ReturnsFalse()
    {
        var atGym = GymProximityMath.IsWithinGymRadius(
            0,
            0,
            GymProximityMath.DefaultGymLatitude,
            GymProximityMath.DefaultGymLongitude,
            GymProximityMath.DefaultGymRadiusMeters);

        Assert.False(atGym);
    }

    [Fact]
    public void IsWithinGymRadius_JustOutside150m_ReturnsFalse()
    {
        // ~200 m north of gym
        var lat = GymProximityMath.DefaultGymLatitude + 0.0018;
        var atGym = GymProximityMath.IsWithinGymRadius(
            lat,
            GymProximityMath.DefaultGymLongitude,
            GymProximityMath.DefaultGymLatitude,
            GymProximityMath.DefaultGymLongitude,
            GymProximityMath.DefaultGymRadiusMeters);

        Assert.False(atGym);
    }
}
