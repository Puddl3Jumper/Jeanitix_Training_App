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
    public void UpdateWorkoutSet_UpdatesRepsWeightAndNotes()
    {
        RunInIsolatedDataHome(database =>
        {
            var workout = database.CreateWorkoutSession("Edit Set Day");
            var exercise = database.GetAllExercises().First(e => e.Name == "Push-ups");
            database.AddExerciseToWorkout(workout.Id, exercise.Id);

            var workoutExercise = database.GetWorkoutSession(workout.Id)!.Exercises.Single();
            database.AddSetToExercise(workoutExercise.Id, reps: 16, weight: 0, notes: "initial");
            var set = database.GetWorkoutSession(workout.Id)!.Exercises.Single().Sets.Single();

            database.UpdateWorkoutSet(set.Id, reps: 20, weight: 0, notes: "updated");

            var updated = database.GetWorkoutSession(workout.Id)!.Exercises.Single().Sets.Single();
            Assert.Equal(20, updated.Reps);
            Assert.Equal(0, updated.Weight);
            Assert.Equal("updated", updated.Notes);
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
    public void WorkoutCloudSyncPayload_IncludesExercisesAndSets_AndRoundTrips()
    {
        var payload = RunInIsolatedDataHome(database =>
        {
            var workout = database.CreateWorkoutSession("Push Day");
            var exercise = database.GetAllExercises().First(e => e.Name == "Bench Press");

            database.AddExerciseToWorkout(workout.Id, exercise.Id);
            var workoutExercise = database.GetWorkoutSession(workout.Id)!.Exercises.Single();

            database.AddSetToExercise(workoutExercise.Id, reps: 8, weight: 80, notes: "Top set", loopNumber: 1, weightUnit: "kg");
            database.CompleteWorkout(workout.Id);

            var exported = database.ExportWorkoutSyncPayload();

            var session = Assert.Single(exported.WorkoutSessions);
            Assert.True(session.IsCompleted);
            var exportedExercise = Assert.Single(session.Exercises);
            var exportedSet = Assert.Single(exportedExercise.Sets);

            Assert.Equal(8, exportedSet.Reps);
            Assert.Equal(80, exportedSet.Weight);
            Assert.Equal("kg", exportedSet.WeightUnit);
            Assert.Equal("Top set", exportedSet.Notes);
            Assert.Equal("Bench Press", exportedExercise.Exercise?.Name);

            return exported;
        });

        RunInIsolatedDataHome(database =>
        {
            var applied = database.TryApplyRemoteWorkoutSync(payload);
            Assert.True(applied);

            var history = database.GetWorkoutHistory(limit: 10);
            var saved = Assert.Single(history);
            Assert.Single(saved.Exercises);
            Assert.Single(saved.Exercises[0].Sets);

            var set = saved.Exercises[0].Sets[0];
            Assert.Equal(8, set.Reps);
            Assert.Equal(80, set.Weight);
            Assert.Equal("kg", set.WeightUnit);
            Assert.Equal("Top set", set.Notes);
            Assert.Equal("Bench Press", saved.Exercises[0].Exercise?.Name);
        });
    }


    private static void RunInIsolatedDataHome(Action<GymDatabase> action)
    {
        RunInIsolatedDataHome(database =>
        {
            action(database);
            return 0;
        });
    }

    private static T RunInIsolatedDataHome<T>(Func<GymDatabase, T> action)
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
            return action(database);
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
