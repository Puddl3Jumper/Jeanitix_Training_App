using Gym_App.Data;
using Gym_App.Models;
using System.Reflection;
using Xunit;

namespace Gym_App.Tests;

public class MvpSpecTests
{
    [Fact]
    public void WorkoutInProgress_NotInHistory_UntilCompleted()
    {
        RunInIsolatedDataHome(database =>
        {
            var workout = database.CreateWorkoutSession("Quick Start");
            var exercise = database.GetAllExercises().First(e => e.Name == "Push-ups");
            database.AddExerciseToWorkout(workout.Id, exercise.Id);

            var current = database.GetCurrentWorkout();
            Assert.NotNull(current);
            Assert.Equal(workout.Id, current!.Id);

            Assert.Empty(database.GetWorkoutHistory(limit: 50));

            database.CompleteWorkout(workout.Id);
            Assert.NotNull(database.GetWorkoutSession(workout.Id));
            Assert.Null(database.GetCurrentWorkout());
            Assert.Contains(database.GetWorkoutHistory(limit: 50), s => s.Id == workout.Id);
        });
    }

    [Fact]
    public void History_IsSortedMostRecentFirst()
    {
        RunInIsolatedDataHome(database =>
        {
            var exercise = database.GetAllExercises().First(e => e.Name == "Bench Press");

            var older = database.CreateWorkoutSession("Older Session");
            database.AddExerciseToWorkout(older.Id, exercise.Id);
            var olderWeId = database.GetWorkoutSession(older.Id)!.Exercises.Single().Id;
            database.AddSetToExercise(olderWeId, reps: 8, weight: 60);
            database.UpdateWorkoutSession(older.Id, name: null, startDate: DateTime.Now.AddDays(-2), notes: null);
            database.CompleteWorkout(older.Id);

            var newer = database.CreateWorkoutSession("Newer Session");
            database.AddExerciseToWorkout(newer.Id, exercise.Id);
            var newerWeId = database.GetWorkoutSession(newer.Id)!.Exercises.Single().Id;
            database.AddSetToExercise(newerWeId, reps: 8, weight: 65);
            database.UpdateWorkoutSession(newer.Id, name: null, startDate: DateTime.Now.AddDays(-1), notes: null);
            database.CompleteWorkout(newer.Id);

            var history = database.GetWorkoutHistory(limit: 10);
            Assert.Equal(2, history.Count);
            Assert.Equal(newer.Id, history[0].Id);
            Assert.Equal(older.Id, history[1].Id);
        });
    }

    [Fact]
    public void DeleteWorkout_RemovesFromHistory_AndFromCsvExport()
    {
        RunInIsolatedDataHome(database =>
        {
            var workout = database.CreateWorkoutSession("Delete Me");
            var exercise = database.GetAllExercises().First(e => e.Name == "Squat");
            database.AddExerciseToWorkout(workout.Id, exercise.Id);
            var weId = database.GetWorkoutSession(workout.Id)!.Exercises.Single().Id;
            database.AddSetToExercise(weId, reps: 5, weight: 100);
            database.CompleteWorkout(workout.Id);

            Assert.Contains(database.GetWorkoutHistory(limit: 50), s => s.Id == workout.Id);

            database.DeleteWorkoutSession(workout.Id);
            Assert.DoesNotContain(database.GetWorkoutHistory(limit: 50), s => s.Id == workout.Id);

            var csvPath = database.ExportWorkoutsCsv();
            Assert.True(File.Exists(csvPath));

            var lines = File.ReadAllLines(csvPath);
            Assert.NotEmpty(lines);
            Assert.Equal("WorkoutDate,WorkoutName,Exercise,SetNumber,Reps,Weight,Unit,SetNotes,WorkoutNotes", lines[0]);
            Assert.Single(lines);
        });
    }

    [Fact]
    public void Units_AndCsvEscaping_WorkCorrectly()
    {
        RunInIsolatedDataHome(database =>
        {
            var workout = database.CreateWorkoutSession("Push, Day");
            database.UpdateWorkoutSession(workout.Id, name: "Push, Day", startDate: DateTime.Now, notes: "Felt \"great\" today");

            var exercise = database.GetAllExercises().First(e => e.Name == "Bench Press");
            database.AddExerciseToWorkout(workout.Id, exercise.Id);
            var weId = database.GetWorkoutSession(workout.Id)!.Exercises.Single().Id;
            database.AddSetToExercise(weId, reps: 10, weight: 135, notes: "top set, smooth", loopNumber: 1, weightUnit: "lb");
            database.CompleteWorkout(workout.Id);

            var csvPath = database.ExportWorkoutsCsv();
            Assert.True(File.Exists(csvPath));
            var csv = File.ReadAllText(csvPath);

            Assert.Contains("\"Push, Day\"", csv);
            Assert.Contains("lb", csv);
            Assert.Contains("\"top set, smooth\"", csv);
            Assert.Contains("\"Felt \"\"great\"\" today\"", csv);
        });
    }

    [Fact]
    public void OfflineFirst_PersistsHistoryAcrossRestart()
    {
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        var originalXdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var tempHome = Path.Combine(Path.GetTempPath(), "gym-app-tests", Guid.NewGuid().ToString("N"));
        var uniqueSessionName = $"Persisted Session {Guid.NewGuid():N}";

        Directory.CreateDirectory(tempHome);

        try
        {
            Environment.SetEnvironmentVariable("HOME", tempHome);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(tempHome, ".local", "share"));

            var database1 = new GymDatabase();
            var workout = database1.CreateWorkoutSession(uniqueSessionName);
            var exercise = database1.GetAllExercises().First(e => e.Name == "Deadlift");
            database1.AddExerciseToWorkout(workout.Id, exercise.Id);
            var weId = database1.GetWorkoutSession(workout.Id)!.Exercises.Single().Id;
            database1.AddSetToExercise(weId, reps: 5, weight: 100);
            database1.CompleteWorkout(workout.Id);

            var history1 = database1.GetWorkoutHistory(limit: 10);
            Assert.Contains(history1, s => s.Id == workout.Id);

            // Simulate app restart.
            var database2 = new GymDatabase();
            var history2 = database2.GetWorkoutHistory(limit: 10);
            var saved = Assert.Single(history2, s => s.Name == uniqueSessionName);
            Assert.Equal(uniqueSessionName, saved.Name);
            Assert.Single(saved.Exercises);
            Assert.Single(saved.Exercises[0].Sets);
            Assert.Equal(100, saved.Exercises[0].Sets[0].Weight);
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
