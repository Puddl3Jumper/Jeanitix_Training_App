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
    public void SelectRandomVisitPlan_BothUpperAndLowerDifferFromPrevious()
    {
        var random = new Random(99);
        for (var prevUpper = 0; prevUpper < 3; prevUpper++)
        {
            for (var prevLower = 0; prevLower < 3; prevLower++)
            {
                for (var i = 0; i < 30; i++)
                {
                    var (upper, lower) = GymDatabase.SelectRandomVisitPlanDifferentFrom(prevUpper, prevLower, random);
                    Assert.True(GymDatabase.VisitPlanDiffersFromPrevious(prevUpper, prevLower, upper, lower));
                    Assert.NotEqual(prevUpper, upper);
                    Assert.NotEqual(prevLower, lower);
                }
            }
        }
    }

    [Fact]
    public void SelectRandomVisitPlan_FirstVisit_CanProduceAnyCombination()
    {
        var random = new Random(7);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 60; i++)
        {
            var (upper, lower) = GymDatabase.SelectRandomVisitPlanDifferentFrom(-1, -1, random);
            seen.Add($"{upper},{lower}");
        }

        Assert.True(seen.Count >= 6);
    }

    [Fact]
    public void VisitPlanDiffersFromPrevious_RequiresBothSlotsToChange()
    {
        Assert.False(GymDatabase.VisitPlanDiffersFromPrevious(0, 1, 0, 1));
        Assert.False(GymDatabase.VisitPlanDiffersFromPrevious(1, 2, 1, 2));
        Assert.False(GymDatabase.VisitPlanDiffersFromPrevious(0, 1, 2, 1));
        Assert.True(GymDatabase.VisitPlanDiffersFromPrevious(0, 1, 1, 2));
        Assert.True(GymDatabase.VisitPlanDiffersFromPrevious(0, 1, 2, 0));
    }

    [Fact]
    public void ShouldSelectNewPlanForCalendarDay_OnlyWhenDateChanges()
    {
        Assert.True(GymDatabase.ShouldSelectNewPlanForCalendarDay(null, "20260520"));
        Assert.True(GymDatabase.ShouldSelectNewPlanForCalendarDay("20260519", "20260520"));
        Assert.False(GymDatabase.ShouldSelectNewPlanForCalendarDay("20260520", "20260520"));
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
}
