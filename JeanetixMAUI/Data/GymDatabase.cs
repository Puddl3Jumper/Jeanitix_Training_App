using System.Text.Json;
using JeanetixMAUI.Models;

namespace JeanetixMAUI.Data;

public class GymDatabase
{
    private readonly string _dataPath;
    private List<Exercise> _exercises;
    private List<WorkoutSession> _workoutSessions;
    private int _nextExerciseId;
    private int _nextWorkoutSessionId;
    private int _nextWorkoutExerciseId;
    private int _nextWorkoutSetId;
    private readonly string _currentUserKey;
    private const string GuestUserKey = "__guest__";

    private const string TrainingRoutineOffsetKey = "training_routine_offset";
    private const string TrainingRoutineLowerMuscleIndexKey = "training_routine_lower_muscle_index";

    private static readonly string[] UpperBodyMuscles = { "Biceps", "Chest", "Back", "Shoulder", "Triceps", "Delts" };
    private static readonly string[] LowerBodyMuscles = { "Legs", "Abs", "Cardio" };
    private static readonly string[][] UpperBodyPairs =
    {
        new[] { "Biceps", "Triceps" },
        new[] { "Chest", "Delts" },
        new[] { "Back", "Shoulder" }
    };

    internal bool IsGuestUser => _currentUserKey == GuestUserKey;

    public GymDatabase()
    {
        _dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JeanetixApp");
        Directory.CreateDirectory(_dataPath);

        _exercises = new List<Exercise>();
        _workoutSessions = new List<WorkoutSession>();
        _nextExerciseId = 1;
        _nextWorkoutSessionId = 1;
        _nextWorkoutExerciseId = 1;
        _nextWorkoutSetId = 1;
        _currentUserKey = ResolveCurrentUserKey();

        LoadData();
        MigrateGuestSessionsToCurrentUser();
        MigrateLegacySessionsToCurrentUser();
        InitializeDefaultExercises();
    }

    private string ResolveCurrentUserKey()
    {
        var email = AuthSessionStore.ReadEmail();
        return NormalizeUserKey(email);
    }

    private static string NormalizeUserKey(string? key) =>
        string.IsNullOrWhiteSpace(key) ? GuestUserKey : key.Trim().ToLowerInvariant();

    private bool IsCurrentUserSession(WorkoutSession s) =>
        NormalizeUserKey(s.UserKey) == _currentUserKey;

    private IEnumerable<WorkoutSession> CurrentUserSessions() =>
        _workoutSessions.Where(IsCurrentUserSession);

    private void MigrateLegacySessionsToCurrentUser()
    {
        bool changed = false;
        foreach (var s in _workoutSessions)
        {
            if (string.IsNullOrWhiteSpace(s.UserKey)) { s.UserKey = _currentUserKey; changed = true; }
        }
        if (changed) SaveData();
    }

    private void MigrateGuestSessionsToCurrentUser()
    {
        if (_currentUserKey == GuestUserKey) return;
        bool changed = false;
        foreach (var s in _workoutSessions)
        {
            if (NormalizeUserKey(s.UserKey) == GuestUserKey) { s.UserKey = _currentUserKey; changed = true; }
        }
        if (changed) SaveData();
    }

    private void InitializeDefaultExercises()
    {
        if (_exercises.Count > 0) return;
        _exercises.AddRange(new[]
        {
            new Exercise { Id = _nextExerciseId++, Name = "Bench Press", MuscleGroup = "Chest", Description = "Flat barbell bench press" },
            new Exercise { Id = _nextExerciseId++, Name = "Incline Bench Press", MuscleGroup = "Chest", Description = "Incline barbell bench press" },
            new Exercise { Id = _nextExerciseId++, Name = "Push-ups", MuscleGroup = "Chest", Description = "Bodyweight push-ups" },
            new Exercise { Id = _nextExerciseId++, Name = "Squat", MuscleGroup = "Legs", Description = "Barbell back squat" },
            new Exercise { Id = _nextExerciseId++, Name = "Leg Press", MuscleGroup = "Legs", Description = "Machine leg press" },
            new Exercise { Id = _nextExerciseId++, Name = "Lunges", MuscleGroup = "Legs", Description = "Walking or stationary lunges" },
            new Exercise { Id = _nextExerciseId++, Name = "Deadlift", MuscleGroup = "Back", Description = "Conventional deadlift" },
            new Exercise { Id = _nextExerciseId++, Name = "Pull-ups", MuscleGroup = "Back", Description = "Bodyweight pull-ups" },
            new Exercise { Id = _nextExerciseId++, Name = "Bent Over Row", MuscleGroup = "Back", Description = "Barbell bent over row" },
            new Exercise { Id = _nextExerciseId++, Name = "Shoulder Press", MuscleGroup = "Shoulders", Description = "Military press or overhead press" },
            new Exercise { Id = _nextExerciseId++, Name = "Lateral Raises", MuscleGroup = "Shoulders", Description = "Dumbbell lateral raises" },
            new Exercise { Id = _nextExerciseId++, Name = "Bicep Curls", MuscleGroup = "Arms", Description = "Dumbbell or barbell curls" },
            new Exercise { Id = _nextExerciseId++, Name = "Triceps Dips", MuscleGroup = "Arms", Description = "Bodyweight or weighted dips" },
            new Exercise { Id = _nextExerciseId++, Name = "Plank", MuscleGroup = "Core", Description = "Front plank hold" },
            new Exercise { Id = _nextExerciseId++, Name = "Crunches", MuscleGroup = "Core", Description = "Standard abdominal crunches" },
        });
        SaveData();
    }

    // ── Exercises ──────────────────────────────────────────────────────────────

    public List<Exercise> GetAllExercises() => _exercises.ToList();
    public List<Exercise> GetExercisesByMuscleGroup(string muscleGroup) =>
        _exercises.Where(e => e.MuscleGroup == muscleGroup).ToList();
    public Exercise? GetExercise(int id) => _exercises.FirstOrDefault(e => e.Id == id);

    public void AddExercise(Exercise exercise)
    {
        exercise.Id = _nextExerciseId++;
        exercise.IsCustom = true;
        _exercises.Add(exercise);
        SaveData();
    }

    // ── Workout Sessions ───────────────────────────────────────────────────────

    public WorkoutSession CreateWorkoutSession(string name)
    {
        var session = new WorkoutSession { Id = _nextWorkoutSessionId++, Name = name, StartTime = DateTime.Now, UserKey = _currentUserKey };
        _workoutSessions.Add(session);
        SaveData();
        return session;
    }

    public WorkoutSession StartTimedWorkout(string name, int? workoutSessionId = null, bool resetStartTime = true)
    {
        WorkoutSession? session = workoutSessionId is > 0 ? GetWorkoutSession(workoutSessionId.Value) : null;
        session ??= GetCurrentWorkout();
        if (session == null || session.IsCompleted) return CreateWorkoutSession(name);
        if (resetStartTime) session.StartTime = DateTime.Now;
        session.EndTime = null;
        session.IsCompleted = false;
        if (!string.IsNullOrWhiteSpace(name)) session.Name = name.Trim();
        SaveData();
        return session;
    }

    public void AddExerciseToWorkout(int workoutSessionId, int exerciseId, string? circuitName = null)
    {
        var session = CurrentUserSessions().FirstOrDefault(s => s.Id == workoutSessionId);
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId);
        if (session == null || exercise == null) return;

        var normalizedCircuit = string.IsNullOrWhiteSpace(circuitName) ? null : circuitName.Trim();
        int circuitOrder = string.IsNullOrEmpty(normalizedCircuit) ? 0 :
            session.Exercises.Count(e => string.Equals(e.CircuitName, normalizedCircuit, StringComparison.OrdinalIgnoreCase)) + 1;

        session.Exercises.Add(new WorkoutExercise
        {
            Id = _nextWorkoutExerciseId++,
            WorkoutSessionId = workoutSessionId,
            ExerciseId = exerciseId,
            Exercise = exercise,
            OrderIndex = session.Exercises.Count,
            CircuitName = normalizedCircuit,
            CircuitOrder = circuitOrder
        });
        SaveData();
    }

    public void AddSetToExercise(int workoutExerciseId, int reps, double weight, int loopNumber = 1, string weightUnit = "kg") =>
        AddSetToExercise(workoutExerciseId, reps, weight, null, loopNumber, weightUnit);

    public void AddSetToExercise(int workoutExerciseId, int reps, double weight, string? notes, int loopNumber = 1, string weightUnit = "kg")
    {
        foreach (var session in CurrentUserSessions())
        {
            var we = session.Exercises.FirstOrDefault(e => e.Id == workoutExerciseId);
            if (we == null) continue;
            we.Sets.Add(new WorkoutSet
            {
                Id = _nextWorkoutSetId++,
                WorkoutExerciseId = workoutExerciseId,
                SetNumber = we.Sets.Count + 1,
                LoopNumber = loopNumber < 1 ? 1 : loopNumber,
                Reps = reps,
                Weight = weight,
                WeightUnit = weightUnit,
                Completed = true,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            });
            SaveData();
            return;
        }
    }

    public void UpdateWorkoutSession(int workoutSessionId, string? name, DateTime startDate, string? notes)
    {
        var session = CurrentUserSessions().FirstOrDefault(s => s.Id == workoutSessionId);
        if (session == null) return;
        if (!string.IsNullOrWhiteSpace(name)) session.Name = name.Trim();
        session.StartTime = new DateTime(startDate.Year, startDate.Month, startDate.Day,
            session.StartTime.Hour, session.StartTime.Minute, session.StartTime.Second, session.StartTime.Kind);
        session.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        SaveData();
    }

    public void DeleteWorkoutSession(int workoutSessionId)
    {
        var session = CurrentUserSessions().FirstOrDefault(s => s.Id == workoutSessionId);
        if (session == null) return;
        _workoutSessions.Remove(session);
        SaveData();
    }

    public void UpdateWorkoutSet(int setId, int reps, double weight, string? notes)
    {
        foreach (var session in CurrentUserSessions())
            foreach (var exercise in session.Exercises)
            {
                var set = exercise.Sets.FirstOrDefault(s => s.Id == setId);
                if (set == null) continue;
                set.Reps = reps; set.Weight = weight;
                set.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
                SaveData(); return;
            }
    }

    public void DeleteWorkoutSet(int setId)
    {
        foreach (var session in CurrentUserSessions())
            foreach (var exercise in session.Exercises)
            {
                var idx = exercise.Sets.FindIndex(s => s.Id == setId);
                if (idx < 0) continue;
                exercise.Sets.RemoveAt(idx);
                for (int i = 0; i < exercise.Sets.Count; i++) exercise.Sets[i].SetNumber = i + 1;
                SaveData(); return;
            }
    }

    public void CompleteWorkout(int workoutSessionId)
    {
        var session = CurrentUserSessions().FirstOrDefault(s => s.Id == workoutSessionId);
        if (session == null) return;
        session.EndTime = DateTime.Now;
        session.IsCompleted = true;
        SaveData();
        AdvanceVisitPlanForNextSession();
    }

    public List<WorkoutSession> GetWorkoutHistory(int limit = 50) =>
        CurrentUserSessions().Where(s => s.IsCompleted).OrderByDescending(s => s.StartTime).Take(limit).ToList();

    public WorkoutSession? GetCurrentWorkout() =>
        CurrentUserSessions().FirstOrDefault(s => !s.IsCompleted);

    public WorkoutSession? GetWorkoutSession(int id) =>
        CurrentUserSessions().FirstOrDefault(s => s.Id == id);

    public int GetCompletedWorkoutCount() =>
        CurrentUserSessions().Count(s => s.IsCompleted);

    public List<(DateTime date, double maxWeight)> GetExerciseProgress(string exerciseName, int limit = 30) =>
        CurrentUserSessions()
            .Where(s => s.IsCompleted)
            .SelectMany(s => s.Exercises.Select(e => new { Session = s, Exercise = e }))
            .Where(x => string.Equals(x.Exercise.Exercise?.Name, exerciseName, StringComparison.OrdinalIgnoreCase) && x.Exercise.Sets.Any())
            .Select(x => (date: x.Session.StartTime.Date, maxWeight: x.Exercise.Sets.Max(s => s.Weight)))
            .OrderBy(x => x.date).TakeLast(limit).ToList();

    public (double pr, double lastWeight) GetExercisePrAndLastWeight(string exerciseName)
    {
        var entries = CurrentUserSessions().Where(s => s.IsCompleted)
            .SelectMany(s => s.Exercises.Select(e => new { Session = s, Exercise = e }))
            .Where(x => string.Equals(x.Exercise.Exercise?.Name, exerciseName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (entries.Count == 0) return (0, 0);
        double pr = entries.Where(x => x.Exercise.Sets.Any()).Select(x => x.Exercise.Sets.Max(s => s.Weight)).DefaultIfEmpty(0).Max();
        double last = entries.OrderByDescending(x => x.Session.StartTime)
            .Select(x => x.Exercise.Sets.OrderByDescending(s => s.SetNumber).FirstOrDefault()?.Weight ?? 0).FirstOrDefault();
        return (pr, last);
    }

    // ── Training Rotation ──────────────────────────────────────────────────────

    public string[] GetDailyWorkoutGroups()
    {
        var dayIndex = Preferences.Get(TrainingRoutineOffsetKey, 0);
        if (GetUpperPairIndexForDay(dayIndex) != 0 && ShouldResetRotationForInactivity(GetLastCompletedWorkoutTimestamp(), DateTime.Now))
        {
            dayIndex = 0;
            Preferences.Set(TrainingRoutineOffsetKey, 0);
        }
        var upperIndex = GetUpperPairIndexForDay(dayIndex);
        var lowerIndex = ResolveLockedLowerIndex();
        return BuildDailyWorkoutGroups(upperIndex, lowerIndex);
    }

    private DateTime? GetLastCompletedWorkoutTimestamp()
    {
        DateTime? latest = null;
        foreach (var s in CurrentUserSessions().Where(s => s.IsCompleted))
        {
            var t = s.EndTime ?? s.StartTime;
            if (latest == null || t > latest) latest = t;
        }
        return latest;
    }

    public void AdvanceVisitPlanForNextSession()
    {
        var previousLower = Preferences.Get(TrainingRoutineLowerMuscleIndexKey, -1);
        var selectedLower = SelectLowerIndexDifferentFromPrevious(previousLower, Random.Shared);
        var offset = Preferences.Get(TrainingRoutineOffsetKey, 0);
        Preferences.Set(TrainingRoutineLowerMuscleIndexKey, selectedLower);
        Preferences.Set(TrainingRoutineOffsetKey, offset + 1);
    }

    public void SkipUpperBodyRotation()
    {
        var offset = Preferences.Get(TrainingRoutineOffsetKey, 0);
        Preferences.Set(TrainingRoutineOffsetKey, offset + 1);
    }

    public void SkipLowerBodyRotation()
    {
        var prev = Preferences.Get(TrainingRoutineLowerMuscleIndexKey, -1);
        Preferences.Set(TrainingRoutineLowerMuscleIndexKey, SelectLowerIndexDifferentFromPrevious(prev, Random.Shared));
    }

    private static int ResolveLockedLowerIndex()
    {
        var stored = Preferences.Get(TrainingRoutineLowerMuscleIndexKey, -1);
        if (stored >= 0 && stored < LowerBodyMuscles.Length) return stored;
        var selected = Random.Shared.Next(LowerBodyMuscles.Length);
        Preferences.Set(TrainingRoutineLowerMuscleIndexKey, selected);
        return selected;
    }

    internal static int GetUpperPairIndexForDay(int dayIndex) => NormalizeIndex(dayIndex, UpperBodyPairs.Length);

    internal static bool ShouldResetRotationForInactivity(DateTime? lastAt, DateTime now, int maxIdleDays = 3) =>
        lastAt.HasValue && (now - lastAt.Value).TotalDays > maxIdleDays;

    internal static int SelectLowerIndexDifferentFromPrevious(int prev, Random random)
    {
        if (LowerBodyMuscles.Length <= 1) return 0;
        if (prev < 0 || prev >= LowerBodyMuscles.Length) return random.Next(LowerBodyMuscles.Length);
        int sel; do { sel = random.Next(LowerBodyMuscles.Length); } while (sel == prev);
        return sel;
    }

    internal static int NormalizeIndex(int index, int length) => length <= 0 ? 0 : ((index % length) + length) % length;

    internal static string[] BuildDailyWorkoutGroups(int upperPairIndex, int lowerBodyIndex)
    {
        var pair = UpperBodyPairs[NormalizeIndex(upperPairIndex, UpperBodyPairs.Length)];
        var lower = LowerBodyMuscles[NormalizeIndex(lowerBodyIndex, LowerBodyMuscles.Length)];
        return new[] { pair[0], pair[1], lower };
    }

    internal static string BuildRoutineSessionName(IReadOnlyList<string> groups) =>
        groups.Count >= 3 ? $"{groups[0]}/{groups[1]} + {groups[2]}" :
        groups.Count > 0 ? string.Join(" + ", groups) : "Today's Workout";

    // ── Export ─────────────────────────────────────────────────────────────────

    public string ExportWorkoutsCsv()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("WorkoutDate,WorkoutName,Exercise,SetNumber,Reps,Weight,Unit,Notes");
        foreach (var session in CurrentUserSessions().Where(s => s.IsCompleted).OrderBy(s => s.StartTime))
            foreach (var ex in session.Exercises)
                foreach (var set in ex.Sets)
                    sb.AppendLine(string.Join(",",
                        Csv(session.StartTime.ToString("yyyy-MM-dd")),
                        Csv(session.Name), Csv(ex.Exercise?.Name ?? ""),
                        set.SetNumber, set.Reps,
                        set.Weight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Csv(set.WeightUnit), Csv(set.Notes ?? "")));
        return sb.ToString();
    }

    private static string Csv(string s) =>
        (s.Contains(',') || s.Contains('"') || s.Contains('\n')) ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

    // ── Cloud Sync ─────────────────────────────────────────────────────────────

    internal WorkoutCloudSyncService.WorkoutSyncPayload ExportWorkoutSyncPayload() =>
        new()
        {
            UpdatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            WorkoutSessions = CurrentUserSessions().Select(CloneForSync).ToList(),
            NextWorkoutSessionId = _nextWorkoutSessionId,
            NextWorkoutExerciseId = _nextWorkoutExerciseId,
            NextWorkoutSetId = _nextWorkoutSetId
        };

    internal bool TryApplyRemoteWorkoutSync(WorkoutCloudSyncService.WorkoutSyncPayload payload)
    {
        if (payload == null) return false;
        var incoming = payload.WorkoutSessions ?? new List<WorkoutSession>();
        if (incoming.Count == 0 && CurrentUserSessions().Any()) return false;
        foreach (var s in incoming)
        {
            s.UserKey = _currentUserKey;
            foreach (var we in s.Exercises ?? new List<WorkoutExercise>())
                if (we.Exercise == null)
                    we.Exercise = _exercises.FirstOrDefault(e => e.Id == we.ExerciseId);
        }
        _workoutSessions = _workoutSessions.Where(s => !IsCurrentUserSession(s)).Concat(incoming).ToList();
        _nextWorkoutSessionId = Math.Max(payload.NextWorkoutSessionId, (_workoutSessions.Count == 0 ? 1 : _workoutSessions.Max(s => s.Id) + 1));
        var maxWeId = _workoutSessions.SelectMany(s => s.Exercises ?? new()).Select(e => e.Id).DefaultIfEmpty(0).Max();
        _nextWorkoutExerciseId = Math.Max(payload.NextWorkoutExerciseId, maxWeId + 1);
        var maxSetId = _workoutSessions.SelectMany(s => s.Exercises ?? new()).SelectMany(e => e.Sets ?? new()).Select(s => s.Id).DefaultIfEmpty(0).Max();
        _nextWorkoutSetId = Math.Max(payload.NextWorkoutSetId, maxSetId + 1);
        SaveData(triggerCloudSync: false);
        return true;
    }

    private static WorkoutSession CloneForSync(WorkoutSession s) => new()
    {
        Id = s.Id, UserKey = s.UserKey, Name = s.Name, StartTime = s.StartTime,
        EndTime = s.EndTime, Notes = s.Notes, IsCompleted = s.IsCompleted,
        Exercises = (s.Exercises ?? new()).Select(e => new WorkoutExercise
        {
            Id = e.Id, WorkoutSessionId = e.WorkoutSessionId, ExerciseId = e.ExerciseId,
            Exercise = e.Exercise, OrderIndex = e.OrderIndex, CircuitName = e.CircuitName, CircuitOrder = e.CircuitOrder,
            Sets = (e.Sets ?? new()).Select(set => new WorkoutSet
            {
                Id = set.Id, WorkoutExerciseId = set.WorkoutExerciseId, SetNumber = set.SetNumber,
                LoopNumber = set.LoopNumber, Reps = set.Reps, Weight = set.Weight,
                WeightUnit = set.WeightUnit, Completed = set.Completed, Notes = set.Notes
            }).ToList()
        }).ToList()
    };

    // ── Persistence ────────────────────────────────────────────────────────────

    private void SaveData(bool triggerCloudSync = true)
    {
        try
        {
            var data = new
            {
                Exercises = _exercises,
                WorkoutSessions = _workoutSessions,
                NextExerciseId = _nextExerciseId,
                NextWorkoutSessionId = _nextWorkoutSessionId,
                NextWorkoutExerciseId = _nextWorkoutExerciseId,
                NextWorkoutSetId = _nextWorkoutSetId
            };
            File.WriteAllText(Path.Combine(_dataPath, "gym_data.json"),
                JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GymDatabase save error: {ex.Message}");
        }
    }

    private void LoadData()
    {
        try
        {
            var path = Path.Combine(_dataPath, "gym_data.json");
            if (!File.Exists(path)) return;
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path));
            if (data == null) return;
            if (data.TryGetValue("Exercises", out var exEl))
                _exercises = JsonSerializer.Deserialize<List<Exercise>>(exEl.GetRawText()) ?? new();
            if (data.TryGetValue("WorkoutSessions", out var wsEl))
                _workoutSessions = JsonSerializer.Deserialize<List<WorkoutSession>>(wsEl.GetRawText()) ?? new();
            if (data.TryGetValue("NextExerciseId", out var neId)) _nextExerciseId = neId.GetInt32();
            if (data.TryGetValue("NextWorkoutSessionId", out var nwsId)) _nextWorkoutSessionId = nwsId.GetInt32();
            if (data.TryGetValue("NextWorkoutExerciseId", out var nweId)) _nextWorkoutExerciseId = nweId.GetInt32();
            if (data.TryGetValue("NextWorkoutSetId", out var nsetId)) _nextWorkoutSetId = nsetId.GetInt32();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GymDatabase load error: {ex.Message}");
        }
    }
}
