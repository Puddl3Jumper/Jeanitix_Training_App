using Gym_App.Data;
using Xunit;

namespace Gym_App.Tests;

public class WorkoutRotationTests
{
    [Fact]
    public void AllNineUpperLowerCombinations_AreDistinct()
    {
        var plans = new List<string>();
        for (var upper = 0; upper < 3; upper++)
        {
            for (var lower = 0; lower < 3; lower++)
            {
                var groups = GymDatabase.BuildDailyWorkoutGroups(upper, lower);
                Assert.Equal(3, groups.Length);
                plans.Add(string.Join("|", groups));
            }
        }

        Assert.Equal(9, plans.Count);
        Assert.Equal(9, plans.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(0, 0, new[] { "Biceps", "Triceps", "Legs" })]
    [InlineData(1, 1, new[] { "Chest", "Delts", "Abs" })]
    [InlineData(2, 2, new[] { "Back", "Shoulder", "Cardio" })]
    public void BuildDailyWorkoutGroups_MatchesExpectedTriplets(int upper, int lower, string[] expected)
    {
        Assert.Equal(expected, GymDatabase.BuildDailyWorkoutGroups(upper, lower));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(-1, 2)]
    public void GetUpperPairIndexForDay_CyclesThroughFixedUpperSchedule(int dayIndex, int expectedUpperIndex)
    {
        Assert.Equal(expectedUpperIndex, GymDatabase.GetUpperPairIndexForDay(dayIndex));
    }

    [Fact]
    public void UpperBodySchedule_IsFixed_Day1Biceps_Day2Chest_Day3Back()
    {
        // The upper body is a fixed weekly schedule keyed by the training-day index.
        // Lower body (3rd element) is randomized separately, so only the upper pair matters here.
        var day1 = GymDatabase.BuildDailyWorkoutGroups(GymDatabase.GetUpperPairIndexForDay(0), 0);
        var day2 = GymDatabase.BuildDailyWorkoutGroups(GymDatabase.GetUpperPairIndexForDay(1), 0);
        var day3 = GymDatabase.BuildDailyWorkoutGroups(GymDatabase.GetUpperPairIndexForDay(2), 0);

        Assert.Equal(new[] { "Biceps", "Triceps" }, new[] { day1[0], day1[1] });
        Assert.Equal(new[] { "Chest", "Delts" }, new[] { day2[0], day2[1] });
        Assert.Equal(new[] { "Back", "Shoulder" }, new[] { day3[0], day3[1] });

        // Day 4 wraps back to the Day 1 upper pair.
        var day4 = GymDatabase.BuildDailyWorkoutGroups(GymDatabase.GetUpperPairIndexForDay(3), 0);
        Assert.Equal(new[] { "Biceps", "Triceps" }, new[] { day4[0], day4[1] });
    }

    [Fact]
    public void SelectLowerIndex_NeverRepeatsThePreviousDay()
    {
        var random = new Random(123);
        for (var previous = 0; previous < 3; previous++)
        {
            for (var i = 0; i < 50; i++)
            {
                var selected = GymDatabase.SelectLowerIndexDifferentFromPrevious(previous, random);
                Assert.InRange(selected, 0, 2);
                Assert.NotEqual(previous, selected);
            }
        }
    }

    [Fact]
    public void SelectLowerIndex_FirstTime_CanProduceAnyMovement()
    {
        var random = new Random(7);
        var seen = new HashSet<int>();
        for (var i = 0; i < 60; i++)
        {
            seen.Add(GymDatabase.SelectLowerIndexDifferentFromPrevious(-1, random));
        }

        Assert.Equal(3, seen.Count);
    }

    [Fact]
    public void ShouldResetRotationForInactivity_TrueOnlyAfterMoreThanThreeDays()
    {
        var now = new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Local);

        Assert.False(GymDatabase.ShouldResetRotationForInactivity(now.AddDays(-2), now));
        // Exactly 3 days is not "more than 3 days".
        Assert.False(GymDatabase.ShouldResetRotationForInactivity(now.AddDays(-3), now));
        Assert.True(GymDatabase.ShouldResetRotationForInactivity(now.AddDays(-3).AddHours(-1), now));
        Assert.True(GymDatabase.ShouldResetRotationForInactivity(now.AddDays(-4), now));
        Assert.True(GymDatabase.ShouldResetRotationForInactivity(now.AddDays(-10), now));
    }

    [Fact]
    public void ShouldResetRotationForInactivity_FalseWhenNeverTrained()
    {
        var now = new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Local);
        Assert.False(GymDatabase.ShouldResetRotationForInactivity(null, now));
    }
}
