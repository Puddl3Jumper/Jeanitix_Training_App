using Gym_App.Data;
using Gym_App.Models;
using System.Reflection;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Gym_App.Tests;

public class GymDatabaseTests
{
    [Fact]
    public void Initializes_WithDefaultExercises()
    {
        RunInIsolatedDataHome(database =>
        {
            var exercises = database.GetAllExercises();

            Assert.NotEmpty(exercises);
            Assert.Contains(exercises, e => e.Name == "Bench Press");
        });
    }

    [Fact]
    public void AddExercise_CreatesCustomExercise()
    {
        RunInIsolatedDataHome(database =>
        {
            database.AddExercise(new Gym_App.Models.Exercise
            {
                Name = "Cable Fly",
                MuscleGroup = "Chest",
                Description = "Isolation"
            });

            var added = database.GetAllExercises().Single(e => e.Name == "Cable Fly");
            Assert.True(added.IsCustom);
            Assert.True(added.Id > 0);
        });
    }

    [Fact]
    public void WorkoutLifecycle_CreateAddSetComplete_HistoryContainsWorkout()
    {
        RunInIsolatedDataHome(database =>
        {
            var workout = database.CreateWorkoutSession("Push Day");
            var exercise = database.GetAllExercises().First(e => e.Name == "Bench Press");

            database.AddExerciseToWorkout(workout.Id, exercise.Id);
            var workoutExerciseId = database.GetWorkoutSession(workout.Id)!.Exercises.Single().Id;
            database.AddSetToExercise(workoutExerciseId, reps: 8, weight: 80, notes: "Top set");
            database.CompleteWorkout(workout.Id);

            var history = database.GetWorkoutHistory();
            var saved = Assert.Single(history, h => h.Id == workout.Id);

            Assert.True(saved.IsCompleted);
            Assert.Single(saved.Exercises);
            Assert.Single(saved.Exercises[0].Sets);
            Assert.Equal(8, saved.Exercises[0].Sets[0].Reps);
            Assert.Equal(80, saved.Exercises[0].Sets[0].Weight);
        });
    }

    [Fact]
    public void AddExerciseToWorkout_AssignsCircuitOrderForSameCircuitName()
    {
        RunInIsolatedDataHome(database =>
        {
            var workout = database.CreateWorkoutSession("Circuit Day");
            var exercises = database.GetAllExercises().Take(2).ToList();

            database.AddExerciseToWorkout(workout.Id, exercises[0].Id, "A");
            database.AddExerciseToWorkout(workout.Id, exercises[1].Id, "A");

            var session = database.GetWorkoutSession(workout.Id)!;
            Assert.Equal(2, session.Exercises.Count);
            Assert.Equal(1, session.Exercises[0].CircuitOrder);
            Assert.Equal(2, session.Exercises[1].CircuitOrder);
        });
    }

    [Fact]
    public void DeleteWorkoutSet_RenumbersRemainingSets()
    {
        RunInIsolatedDataHome(database =>
        {
            var workout = database.CreateWorkoutSession("Leg Day");
            var exercise = database.GetAllExercises().First(e => e.Name == "Squat");
            database.AddExerciseToWorkout(workout.Id, exercise.Id);

            var workoutExercise = database.GetWorkoutSession(workout.Id)!.Exercises.Single();
            database.AddSetToExercise(workoutExercise.Id, reps: 5, weight: 100);
            database.AddSetToExercise(workoutExercise.Id, reps: 5, weight: 105);
            database.AddSetToExercise(workoutExercise.Id, reps: 5, weight: 110);

            var secondSetId = database.GetWorkoutSession(workout.Id)!.Exercises.Single().Sets[1].Id;
            database.DeleteWorkoutSet(secondSetId);

            var sets = database.GetWorkoutSession(workout.Id)!.Exercises.Single().Sets;
            Assert.Equal(2, sets.Count);
            Assert.Equal(1, sets[0].SetNumber);
            Assert.Equal(2, sets[1].SetNumber);
            Assert.Equal(110, sets[1].Weight);
        });
    }

    [Fact]
    public void ProgressAndPr_ReturnExpectedValues()
    {
        RunInIsolatedDataHome(database =>
        {
            var exercise = database.GetAllExercises().First(e => e.Name == "Deadlift");

            var workout1 = database.CreateWorkoutSession("Pull 1");
            database.AddExerciseToWorkout(workout1.Id, exercise.Id);
            var we1 = database.GetWorkoutSession(workout1.Id)!.Exercises.Single().Id;
            database.AddSetToExercise(we1, reps: 5, weight: 120);
            database.CompleteWorkout(workout1.Id);

            var workout2 = database.CreateWorkoutSession("Pull 2");
            database.AddExerciseToWorkout(workout2.Id, exercise.Id);
            var we2 = database.GetWorkoutSession(workout2.Id)!.Exercises.Single().Id;
            database.AddSetToExercise(we2, reps: 3, weight: 130);
            database.CompleteWorkout(workout2.Id);

            var progress = database.GetExerciseProgress("Deadlift");
            var (pr, lastWeight) = database.GetExercisePrAndLastWeight("Deadlift");

            Assert.Equal(2, progress.Count);
            Assert.Equal(130, pr);
            Assert.Equal(130, lastWeight);
        });
    }

    [Fact]
    public void GetCurrentWorkout_ReturnsNewestIncompleteWorkout()
    {
        RunInIsolatedDataHome(database =>
        {
            // Create an old workout and leave it incomplete
            var oldWorkout = database.CreateWorkoutSession("Old Workout");
            System.Threading.Thread.Sleep(10); // Ensure time difference

            // Create a newer workout and leave it incomplete
            var newWorkout = database.CreateWorkoutSession("New Workout");

            // GetCurrentWorkout should return the newest incomplete workout
            var current = database.GetCurrentWorkout();

            Assert.NotNull(current);
            Assert.Equal(newWorkout.Id, current.Id);
            Assert.Equal("New Workout", current.Name);
        });
    }

    [Fact]
    public void GetCurrentWorkout_ReturnsNullWhenAllCompleted()
    {
        RunInIsolatedDataHome(database =>
        {
            var workout = database.CreateWorkoutSession("Completed Workout");
            database.CompleteWorkout(workout.Id);

            var current = database.GetCurrentWorkout();

            Assert.Null(current);
        });
    }

    [Fact]
    public void GetCurrentWorkout_IgnoresCompletedWorkouts()
    {
        RunInIsolatedDataHome(database =>
        {
            // Create and complete an old workout
            var oldWorkout = database.CreateWorkoutSession("Old Completed");
            database.CompleteWorkout(oldWorkout.Id);

            System.Threading.Thread.Sleep(10);

            // Create a newer incomplete workout
            var newWorkout = database.CreateWorkoutSession("New Incomplete");

            // GetCurrentWorkout should return the incomplete one, not the completed one
            var current = database.GetCurrentWorkout();

            Assert.NotNull(current);
            Assert.Equal(newWorkout.Id, current.Id);
        });
    }

    private static void RunInIsolatedDataHome(Action<GymDatabase> action)
    {
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        var originalXdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var tempHome = Path.Combine(Path.GetTempPath(), "gym-app-tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempHome);

        try
        {
            Environment.SetEnvironmentVariable("HOME", tempHome);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(tempHome, ".local", "share"));

            var database = new GymDatabase();
            ResetDatabaseState(database);
            action(database);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", originalXdg);

            try
            {
                if (Directory.Exists(tempHome))
                {
                    Directory.Delete(tempHome, true);
                }
            }
            catch
            {
            }
        }
    }

    private static void ResetDatabaseState(GymDatabase database)
    {
        var type = typeof(GymDatabase);

        type.GetField("_exercises", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(database, new List<Exercise>());
        type.GetField("_workoutSessions", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(database, new List<WorkoutSession>());
        type.GetField("_nextExerciseId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(database, 1);
        type.GetField("_nextWorkoutSessionId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(database, 1);
        type.GetField("_nextWorkoutExerciseId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(database, 1);
        type.GetField("_nextWorkoutSetId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(database, 1);

        type.GetMethod("InitializeDefaultExercises", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(database, null);
    }
}
