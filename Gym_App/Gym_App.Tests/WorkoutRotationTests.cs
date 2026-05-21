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

    [Fact]
    public void AdvanceRotationOffset_AdvancesAfter24Hours()
    {
        var last = new DateTime(2026, 5, 1, 10, 0, 0);
        var after25h = last.AddHours(25);

        Assert.Equal(1, GymDatabase.AdvanceRotationOffset(0, last, after25h));
        Assert.Equal(0, GymDatabase.AdvanceRotationOffset(0, last, last.AddHours(23)));
    }

    [Fact]
    public void AdvanceRotationOffset_AdvancesMultipleDays()
    {
        var last = new DateTime(2026, 5, 1, 10, 0, 0);
        var after49h = last.AddHours(49);

        Assert.Equal(2, GymDatabase.AdvanceRotationOffset(0, last, after49h));
    }

    [Fact]
    public void LowerBodySlot_CyclesLegsAbsCardio_WithOffset()
    {
        Assert.Equal("Legs", GymDatabase.BuildDailyWorkoutGroups(0, 0)[2]);
        Assert.Equal("Abs", GymDatabase.BuildDailyWorkoutGroups(0, 1)[2]);
        Assert.Equal("Cardio", GymDatabase.BuildDailyWorkoutGroups(0, 2)[2]);
        Assert.Equal("Legs", GymDatabase.BuildDailyWorkoutGroups(0, 3)[2]);
    }

    [Fact]
    public void SelectUpperPair_AvoidsYesterdayWhenPossible()
    {
        var random = new Random(42);
        for (var previous = 0; previous < 3; previous++)
        {
            for (var i = 0; i < 20; i++)
            {
                var selected = GymDatabase.SelectUpperPairIndexDifferentFromPrevious(previous, random);
                Assert.NotEqual(previous, selected);
            }
        }
    }

    [Fact]
    public void SelectUpperPair_FirstVisit_CanBeAnyPair()
    {
        var random = new Random(7);
        var seen = new HashSet<int>();
        for (var i = 0; i < 30; i++)
        {
            seen.Add(GymDatabase.SelectUpperPairIndexDifferentFromPrevious(-1, random));
        }

        Assert.True(seen.Count >= 2);
    }
}
