using System.Text.Json;
#if ANDROID
using Android.App;
using Android.Content;
using Android.Provider;
#endif
using Gym_App.Models;

namespace Gym_App.Data
{
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

        private const string TrainingRoutinePrefsName = "training_routine_prefs";
        private const string TrainingRoutineOffsetKey = "training_routine_offset";
        private const string TrainingRoutineTimestampKey = "training_routine_timestamp";
        private const string TrainingRoutineUpperPairIndexKey = "training_routine_upper_pair_index";
        private const string TrainingRoutineLowerMuscleIndexKey = "training_routine_lower_muscle_index";
        private const string TrainingRoutineLastSelectionDateKey = "training_routine_last_selection_date";
        private const string TrainingRoutineLastSelectionTimestampKey = "training_routine_last_selection_timestamp";

        private static readonly string[] UpperBodyMuscles =
        {
            "Biceps",
            "Chest",
            "Back",
            "Shoulder",
            "Triceps",
            "Delts"
        };

        private static readonly string[] LowerBodyMuscles =
        {
            "Legs",
            "Abs",
            "Cardio"
        };

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
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "GymApp");
            
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

#if ANDROID
            WorkoutCloudSyncService.TryScheduleInitialPull(this);
#endif
        }

        internal WorkoutCloudSyncService.WorkoutSyncPayload ExportWorkoutSyncPayload()
        {
            var payload = new WorkoutCloudSyncService.WorkoutSyncPayload
            {
                UpdatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                WorkoutSessions = CurrentUserSessions().Select(CloneWorkoutSessionForSync).ToList(),
                NextWorkoutSessionId = _nextWorkoutSessionId,
                NextWorkoutExerciseId = _nextWorkoutExerciseId,
                NextWorkoutSetId = _nextWorkoutSetId
            };

            return payload;
        }

        internal bool TryApplyRemoteWorkoutSync(WorkoutCloudSyncService.WorkoutSyncPayload payload)
        {
            if (payload == null)
                return false;

            // Replace only the current user's sessions; keep other users/guest data intact.
            var incoming = payload.WorkoutSessions ?? new List<WorkoutSession>();
            var existingCurrentUserSessions = CurrentUserSessions().ToList();

            // Guard against data loss when cloud state is empty but local already has history.
            if (incoming.Count == 0 && existingCurrentUserSessions.Count > 0)
                return false;

            foreach (var session in incoming)
            {
                session.UserKey = _currentUserKey;
                foreach (var workoutExercise in session.Exercises ?? new List<WorkoutExercise>())
                {
                    if (workoutExercise.Exercise == null)
                    {
                        workoutExercise.Exercise = _exercises.FirstOrDefault(e => e.Id == workoutExercise.ExerciseId);
                    }
                }
            }

            _workoutSessions = _workoutSessions
                .Where(s => !IsCurrentUserSession(s))
                .Concat(incoming)
                .ToList();

            // Recompute next IDs to avoid collisions.
            _nextWorkoutSessionId = Math.Max(payload.NextWorkoutSessionId, (_workoutSessions.Count == 0 ? 1 : _workoutSessions.Max(s => s.Id) + 1));

            var maxWorkoutExerciseId = _workoutSessions
                .SelectMany(s => s.Exercises ?? new List<WorkoutExercise>())
                .Select(e => e.Id)
                .DefaultIfEmpty(0)
                .Max();
            _nextWorkoutExerciseId = Math.Max(payload.NextWorkoutExerciseId, maxWorkoutExerciseId + 1);

            var maxWorkoutSetId = _workoutSessions
                .SelectMany(s => s.Exercises ?? new List<WorkoutExercise>())
                .SelectMany(e => e.Sets ?? new List<WorkoutSet>())
                .Select(s => s.Id)
                .DefaultIfEmpty(0)
                .Max();
            _nextWorkoutSetId = Math.Max(payload.NextWorkoutSetId, maxWorkoutSetId + 1);

            SaveData(triggerCloudSync: false);
            return true;
        }

        private static WorkoutSession CloneWorkoutSessionForSync(WorkoutSession session)
        {
            // Keep payload small and deterministic; avoid computed properties.
            return new WorkoutSession
            {
                Id = session.Id,
                UserKey = session.UserKey,
                Name = session.Name,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                Notes = session.Notes,
                IsCompleted = session.IsCompleted,
                Exercises = (session.Exercises ?? new List<WorkoutExercise>()).Select(e => new WorkoutExercise
                {
                    Id = e.Id,
                    WorkoutSessionId = e.WorkoutSessionId,
                    ExerciseId = e.ExerciseId,
                    Exercise = e.Exercise,
                    OrderIndex = e.OrderIndex,
                    CircuitName = e.CircuitName,
                    CircuitOrder = e.CircuitOrder,
                    Sets = (e.Sets ?? new List<WorkoutSet>()).Select(s => new WorkoutSet
                    {
                        Id = s.Id,
                        WorkoutExerciseId = s.WorkoutExerciseId,
                        SetNumber = s.SetNumber,
                        LoopNumber = s.LoopNumber,
                        Reps = s.Reps,
                        Weight = s.Weight,
                        WeightUnit = s.WeightUnit,
                        Completed = s.Completed,
                        Notes = s.Notes
                    }).ToList()
                }).ToList()
            };
        }

        private string ResolveCurrentUserKey()
        {
#if ANDROID
            try
            {
                var context = Application.Context;
                var email = context == null ? null : AuthSessionStore.ReadEmail(context);
                return NormalizeUserKey(email);
            }
            catch
            {
                return GuestUserKey;
            }
#else
            return GuestUserKey;
#endif
        }

        private static string NormalizeUserKey(string? key)
        {
            return string.IsNullOrWhiteSpace(key)
                ? GuestUserKey
                : key.Trim().ToLowerInvariant();
        }

        private bool IsCurrentUserSession(WorkoutSession session)
        {
            return NormalizeUserKey(session.UserKey) == _currentUserKey;
        }

        private IEnumerable<WorkoutSession> CurrentUserSessions()
        {
            return _workoutSessions.Where(IsCurrentUserSession);
        }

        private void MigrateLegacySessionsToCurrentUser()
        {
            bool hasChanges = false;
            foreach (var session in _workoutSessions)
            {
                if (string.IsNullOrWhiteSpace(session.UserKey))
                {
                    session.UserKey = _currentUserKey;
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                SaveData();
            }
        }

        private void MigrateGuestSessionsToCurrentUser()
        {
            if (_currentUserKey == GuestUserKey)
                return;

            bool hasChanges = false;
            foreach (var session in _workoutSessions)
            {
                if (NormalizeUserKey(session.UserKey) == GuestUserKey)
                {
                    session.UserKey = _currentUserKey;
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                SaveData();
            }
        }

        private void InitializeDefaultExercises()
        {
            if (_exercises.Count == 0)
            {
                var defaultExercises = new List<Exercise>
                {
                    new Exercise { Id = _nextExerciseId++, Name = "Bench Press", MuscleGroup = "Chest", Description = "Flat barbell bench press", IsCustom = false },
                    new Exercise { Id = _nextExerciseId++, Name = "Incline Bench Press", MuscleGroup = "Chest", Description = "Incline barbell bench press", IsCustom = false },
                    new Exercise { Id = _nextExerciseId++, Name = "Push-ups", MuscleGroup = "Chest", Description = "Bodyweight push-ups", IsCustom = false },
                    
                    new Exercise { Id = _nextExerciseId++, Name = "Squat", MuscleGroup = "Legs", Description = "Barbell back squat", IsCustom = false },
                    new Exercise { Id = _nextExerciseId++, Name = "Leg Press", MuscleGroup = "Legs", Description = "Machine leg press", IsCustom = false },
                    new Exercise { Id = _nextExerciseId++, Name = "Lunges", MuscleGroup = "Legs", Description = "Walking or stationary lunges", IsCustom = false },
                    
                    new Exercise { Id = _nextExerciseId++, Name = "Deadlift", MuscleGroup = "Back", Description = "Conventional deadlift", IsCustom = false },
                    new Exercise { Id = _nextExerciseId++, Name = "Pull-ups", MuscleGroup = "Back", Description = "Bodyweight pull-ups", IsCustom = false },
                    new Exercise { Id = _nextExerciseId++, Name = "Bent Over Row", MuscleGroup = "Back", Description = "Barbell bent over row", IsCustom = false },
                    
                    new Exercise { Id = _nextExerciseId++, Name = "Shoulder Press", MuscleGroup = "Shoulders", Description = "Military press or overhead press", IsCustom = false },
                    new Exercise { Id = _nextExerciseId++, Name = "Lateral Raises", MuscleGroup = "Shoulders", Description = "Dumbbell lateral raises", IsCustom = false },
                    
                    new Exercise { Id = _nextExerciseId++, Name = "Bicep Curls", MuscleGroup = "Arms", Description = "Dumbbell or barbell curls", IsCustom = false },
                    new Exercise { Id = _nextExerciseId++, Name = "Triceps Dips", MuscleGroup = "Arms", Description = "Bodyweight or weighted dips", IsCustom = false },
                    
                    new Exercise { Id = _nextExerciseId++, Name = "Plank", MuscleGroup = "Core", Description = "Front plank hold", IsCustom = false },
                    new Exercise { Id = _nextExerciseId++, Name = "Crunches", MuscleGroup = "Core", Description = "Standard abdominal crunches", IsCustom = false },
                };
                
                _exercises.AddRange(defaultExercises);
                SaveData();
            }
        }

        public List<Exercise> GetAllExercises() => _exercises.ToList();
        
        public List<Exercise> GetExercisesByMuscleGroup(string muscleGroup) 
            => _exercises.Where(e => e.MuscleGroup == muscleGroup).ToList();

        public Exercise? GetExercise(int id) => _exercises.FirstOrDefault(e => e.Id == id);

        public void AddExercise(Exercise exercise)
        {
            exercise.Id = _nextExerciseId++;
            exercise.IsCustom = true;
            _exercises.Add(exercise);
            SaveData();
        }

        public WorkoutSession CreateWorkoutSession(string name)
        {
            var session = new WorkoutSession
            {
                Id = _nextWorkoutSessionId++,
                Name = name,
                StartTime = DateTime.Now,
                IsCompleted = false,
                UserKey = _currentUserKey
            };
            _workoutSessions.Add(session);
            SaveData();
            return session;
        }

        /// <summary>
        /// Starts or resumes a timed in-progress workout. When resetStartTime is true, the timer begins now.
        /// </summary>
        public WorkoutSession StartTimedWorkout(string name, int? workoutSessionId = null, bool resetStartTime = true)
        {
            WorkoutSession? session = null;

            if (workoutSessionId is > 0)
            {
                session = GetWorkoutSession(workoutSessionId.Value);
            }

            session ??= GetCurrentWorkout();

            if (session == null || session.IsCompleted)
            {
                return CreateWorkoutSession(name);
            }

            if (resetStartTime)
            {
                session.StartTime = DateTime.Now;
            }

            session.EndTime = null;
            session.IsCompleted = false;

            if (!string.IsNullOrWhiteSpace(name))
            {
                session.Name = name.Trim();
            }

            SaveData();
            return session;
        }

        public void AddExerciseToWorkout(int workoutSessionId, int exerciseId, string? circuitName = null)
        {
            var session = CurrentUserSessions().FirstOrDefault(s => s.Id == workoutSessionId);
            var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId);
            
            if (session != null && exercise != null)
            {
                var normalizedCircuitName = string.IsNullOrWhiteSpace(circuitName)
                    ? null
                    : circuitName.Trim();

                int circuitOrder = 0;
                if (!string.IsNullOrEmpty(normalizedCircuitName))
                {
                    circuitOrder = session.Exercises
                        .Count(e => string.Equals(e.CircuitName, normalizedCircuitName, StringComparison.OrdinalIgnoreCase)) + 1;
                }

                var workoutExercise = new WorkoutExercise
                {
                    Id = _nextWorkoutExerciseId++,
                    WorkoutSessionId = workoutSessionId,
                    ExerciseId = exerciseId,
                    Exercise = exercise,
                    OrderIndex = session.Exercises.Count,
                    CircuitName = normalizedCircuitName,
                    CircuitOrder = circuitOrder
                };
                session.Exercises.Add(workoutExercise);
                SaveData();
            }
        }

        public void AddSetToExercise(int workoutExerciseId, int reps, double weight, int loopNumber = 1, string weightUnit = "kg")
        {
            AddSetToExercise(workoutExerciseId, reps, weight, null, loopNumber, weightUnit);
        }

        public void AddSetToExercise(int workoutExerciseId, int reps, double weight, string? notes, int loopNumber = 1, string weightUnit = "kg")
        {
            foreach (var session in CurrentUserSessions())
            {
                var workoutExercise = session.Exercises.FirstOrDefault(e => e.Id == workoutExerciseId);
                if (workoutExercise != null)
                {
                    int normalizedLoopNumber = loopNumber < 1 ? 1 : loopNumber;

                    var set = new WorkoutSet
                    {
                        Id = _nextWorkoutSetId++,
                        WorkoutExerciseId = workoutExerciseId,
                        SetNumber = workoutExercise.Sets.Count + 1,
                        LoopNumber = normalizedLoopNumber,
                        Reps = reps,
                        Weight = weight,
                        WeightUnit = weightUnit,
                        Completed = true,
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
                    };
                    workoutExercise.Sets.Add(set);
                    SaveData();
                    return;
                }
            }
        }

        public void UpdateWorkoutSession(int workoutSessionId, string? name, DateTime startDate, string? notes)
        {
            var session = CurrentUserSessions().FirstOrDefault(s => s.Id == workoutSessionId);
            if (session == null)
                return;

            if (!string.IsNullOrWhiteSpace(name))
                session.Name = name.Trim();

            var normalizedDate = new DateTime(
                startDate.Year,
                startDate.Month,
                startDate.Day,
                session.StartTime.Hour,
                session.StartTime.Minute,
                session.StartTime.Second,
                session.StartTime.Kind);

            session.StartTime = normalizedDate;
            session.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            SaveData();
        }

        public void DeleteWorkoutSession(int workoutSessionId)
        {
            var session = CurrentUserSessions().FirstOrDefault(s => s.Id == workoutSessionId);
            if (session == null)
                return;

            _workoutSessions.Remove(session);
            SaveData();
        }

        public void UpdateWorkoutSet(int setId, int reps, double weight, string? notes)
        {
            foreach (var session in CurrentUserSessions())
            {
                foreach (var exercise in session.Exercises)
                {
                    var set = exercise.Sets.FirstOrDefault(s => s.Id == setId);
                    if (set == null)
                        continue;

                    set.Reps = reps;
                    set.Weight = weight;
                    set.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
                    SaveData();
                    return;
                }
            }
        }

        public void DeleteWorkoutSet(int setId)
        {
            foreach (var session in CurrentUserSessions())
            {
                foreach (var exercise in session.Exercises)
                {
                    var index = exercise.Sets.FindIndex(s => s.Id == setId);
                    if (index < 0)
                        continue;

                    exercise.Sets.RemoveAt(index);
                    for (int i = 0; i < exercise.Sets.Count; i++)
                    {
                        exercise.Sets[i].SetNumber = i + 1;
                    }

                    SaveData();
                    return;
                }
            }
        }

        public List<(DateTime date, double maxWeight)> GetExerciseProgress(string exerciseName, int limit = 30)
        {
            return CurrentUserSessions()
                .Where(s => s.IsCompleted)
                .SelectMany(s => s.Exercises.Select(e => new { Session = s, Exercise = e }))
                .Where(x => string.Equals(x.Exercise.Exercise?.Name, exerciseName, StringComparison.OrdinalIgnoreCase))
                .Where(x => x.Exercise.Sets.Any())
                .Select(x => (date: x.Session.StartTime.Date, maxWeight: x.Exercise.Sets.Max(set => set.Weight)))
                .OrderBy(x => x.date)
                .TakeLast(limit)
                .ToList();
        }

        public (double pr, double lastWeight) GetExercisePrAndLastWeight(string exerciseName)
        {
            var entries = CurrentUserSessions()
                .Where(s => s.IsCompleted)
                .SelectMany(s => s.Exercises.Select(e => new { Session = s, Exercise = e }))
                .Where(x => string.Equals(x.Exercise.Exercise?.Name, exerciseName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (entries.Count == 0)
                return (0, 0);

            double pr = entries
                .Where(x => x.Exercise.Sets.Any())
                .Select(x => x.Exercise.Sets.Max(set => set.Weight))
                .DefaultIfEmpty(0)
                .Max();

            double lastWeight = entries
                .OrderByDescending(x => x.Session.StartTime)
                .Select(x => x.Exercise.Sets.OrderByDescending(set => set.SetNumber).FirstOrDefault()?.Weight ?? 0)
                .FirstOrDefault();

            return (pr, lastWeight);
        }

        public string ExportWorkoutsCsv()
        {
            var fileName = $"gym_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            using var buffer = new StringWriter();
            WriteWorkoutsCsv(buffer);
            var csvContent = buffer.ToString();

            if (TryExportToDownloads(fileName, csvContent, out var downloadPath))
                return downloadPath;

            var fallbackPath = Path.Combine(_dataPath, fileName);
            File.WriteAllText(fallbackPath, csvContent);
            return fallbackPath;
        }

        private void WriteWorkoutsCsv(TextWriter writer)
        {
            writer.WriteLine("WorkoutDate,WorkoutName,Exercise,SetNumber,Reps,Weight,Unit,SetNotes,WorkoutNotes");

            var sessions = _workoutSessions
                .Where(IsCurrentUserSession)
                .Where(s => s.IsCompleted)
                .OrderBy(s => s.StartTime)
                .ToList();

            foreach (var session in sessions)
            {
                foreach (var exercise in session.Exercises)
                {
                    foreach (var set in exercise.Sets)
                    {
                        writer.WriteLine(string.Join(",",
                            EscapeCsv(session.StartTime.ToString("yyyy-MM-dd")),
                            EscapeCsv(session.Name),
                            EscapeCsv(exercise.Exercise?.Name ?? ""),
                            set.SetNumber,
                            set.Reps,
                            set.Weight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            EscapeCsv(set.WeightUnit),
                            EscapeCsv(set.Notes ?? ""),
                            EscapeCsv(session.Notes ?? "")));
                    }
                }
            }
        }

        private static bool TryExportToDownloads(string fileName, string csvContent, out string exportedPath)
        {
            exportedPath = string.Empty;

#if ANDROID
            try
            {
                var context = Application.Context;
                if (context == null)
                    return false;

                if (OperatingSystem.IsAndroidVersionAtLeast(29))
                {
                    var values = new ContentValues();
                    values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
                    values.Put(MediaStore.IMediaColumns.MimeType, "text/csv");
                    values.Put(MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads);

                    var resolver = context.ContentResolver;
                    var uri = resolver?.Insert(MediaStore.Downloads.ExternalContentUri, values);
                    if (uri == null)
                        return false;

                    using var stream = resolver.OpenOutputStream(uri);
                    if (stream == null)
                    {
                        resolver.Delete(uri, null, null);
                        return false;
                    }

                    using var writer = new StreamWriter(stream, leaveOpen: false);
                    writer.Write(csvContent);
                    writer.Flush();

                    exportedPath = $"Downloads/{fileName}";
                    return true;
                }

                var downloadsDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);
                if (downloadsDir == null || string.IsNullOrWhiteSpace(downloadsDir.AbsolutePath))
                    return false;

                Directory.CreateDirectory(downloadsDir.AbsolutePath);
                var filePath = Path.Combine(downloadsDir.AbsolutePath, fileName);
                File.WriteAllText(filePath, csvContent);
                exportedPath = filePath;
                return true;
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }

        private static string EscapeCsv(string input)
        {
            if (input.Contains(',') || input.Contains('"') || input.Contains('\n'))
                return $"\"{input.Replace("\"", "\"\"")}\"";

            return input;
        }

        public void CompleteWorkout(int workoutSessionId)
        {
            var session = CurrentUserSessions().FirstOrDefault(s => s.Id == workoutSessionId);
            if (session != null)
            {
                session.EndTime = DateTime.Now;
                session.IsCompleted = true;
                SaveData();
            }
        }

        /// <summary>
        /// Records today's planned routine (upper/upper/lower muscles) as a completed workout in history.
        /// </summary>
        public WorkoutSession CompleteTodayRoutine(IReadOnlyList<string>? plannedGroups = null, int? workoutSessionId = null)
        {
            var groups = plannedGroups is { Count: > 0 }
                ? plannedGroups.ToArray()
                : GetDailyWorkoutGroups();

            var sessionName = BuildRoutineSessionName(groups);
            var session = ResolveRoutineWorkoutSession(workoutSessionId, sessionName);

            foreach (var muscle in groups)
            {
                var exercise = ResolveExerciseForPlannedMuscle(muscle);
                if (exercise == null)
                    continue;

                var alreadyLogged = session.Exercises.Any(e =>
                    string.Equals(e.CircuitName, muscle, StringComparison.OrdinalIgnoreCase)
                    || e.ExerciseId == exercise.Id);

                if (!alreadyLogged)
                {
                    AddExerciseToWorkout(session.Id, exercise.Id, circuitName: muscle);
                }
            }

            session = GetWorkoutSession(session.Id)!;

            foreach (var workoutExercise in session.Exercises.Where(e => e.Sets.Count == 0))
            {
                AddSetToExercise(workoutExercise.Id, reps: 1, weight: 0, notes: "Routine completed");
            }

            if (!string.IsNullOrWhiteSpace(sessionName))
            {
                session.Name = sessionName;
            }

            CompleteWorkout(session.Id);
#if ANDROID
            AdvanceVisitPlanForNextSession();
#endif
            return GetWorkoutSession(session.Id)!;
        }

        internal static string BuildRoutineSessionName(IReadOnlyList<string> groups)
        {
            if (groups.Count >= 3)
            {
                return $"{groups[0]}/{groups[1]} + {groups[2]}";
            }

            if (groups.Count > 0)
            {
                return string.Join(" + ", groups);
            }

            return "Today's Workout";
        }

        internal static Exercise? ResolveExerciseForPlannedMuscle(string plannedMuscle, IReadOnlyList<Exercise> exercises)
        {
            if (string.IsNullOrWhiteSpace(plannedMuscle))
                return null;

            var preferredName = GetPreferredExerciseName(plannedMuscle);
            if (!string.IsNullOrWhiteSpace(preferredName))
            {
                var preferred = exercises.FirstOrDefault(e =>
                    string.Equals(e.Name, preferredName, StringComparison.OrdinalIgnoreCase));
                if (preferred != null)
                {
                    return preferred;
                }
            }

            var muscleGroup = MapPlannedMuscleToGroup(plannedMuscle);
            return exercises.FirstOrDefault(e =>
                string.Equals(e.MuscleGroup, muscleGroup, StringComparison.OrdinalIgnoreCase));
        }

        internal static string MapPlannedMuscleToGroup(string plannedMuscle)
        {
            if (string.Equals(plannedMuscle, "Cardio", StringComparison.OrdinalIgnoreCase)
                || string.Equals(plannedMuscle, "Abs", StringComparison.OrdinalIgnoreCase))
            {
                return "Core";
            }

            if (string.Equals(plannedMuscle, "Delts", StringComparison.OrdinalIgnoreCase)
                || string.Equals(plannedMuscle, "Shoulder", StringComparison.OrdinalIgnoreCase))
            {
                return "Shoulders";
            }

            if (string.Equals(plannedMuscle, "Biceps", StringComparison.OrdinalIgnoreCase)
                || string.Equals(plannedMuscle, "Triceps", StringComparison.OrdinalIgnoreCase))
            {
                return "Arms";
            }

            return plannedMuscle;
        }

        private static string? GetPreferredExerciseName(string plannedMuscle)
        {
            if (string.Equals(plannedMuscle, "Chest", StringComparison.OrdinalIgnoreCase))
                return "Bench Press";
            if (string.Equals(plannedMuscle, "Back", StringComparison.OrdinalIgnoreCase))
                return "Deadlift";
            if (string.Equals(plannedMuscle, "Legs", StringComparison.OrdinalIgnoreCase))
                return "Squat";
            if (string.Equals(plannedMuscle, "Biceps", StringComparison.OrdinalIgnoreCase))
                return "Bicep Curls";
            if (string.Equals(plannedMuscle, "Triceps", StringComparison.OrdinalIgnoreCase))
                return "Triceps Dips";
            if (string.Equals(plannedMuscle, "Delts", StringComparison.OrdinalIgnoreCase)
                || string.Equals(plannedMuscle, "Shoulder", StringComparison.OrdinalIgnoreCase))
            {
                return "Shoulder Press";
            }
            if (string.Equals(plannedMuscle, "Abs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(plannedMuscle, "Cardio", StringComparison.OrdinalIgnoreCase))
            {
                return "Plank";
            }

            return null;
        }

        private Exercise? ResolveExerciseForPlannedMuscle(string plannedMuscle) =>
            ResolveExerciseForPlannedMuscle(plannedMuscle, _exercises);

        private WorkoutSession ResolveRoutineWorkoutSession(int? workoutSessionId, string sessionName)
        {
            WorkoutSession? session = null;

            if (workoutSessionId is > 0)
            {
                session = GetWorkoutSession(workoutSessionId.Value);
            }

            session ??= GetCurrentWorkout();

            if (session == null || session.IsCompleted)
            {
                return CreateWorkoutSession(sessionName);
            }

            if (!string.IsNullOrWhiteSpace(sessionName))
            {
                session.Name = sessionName;
                SaveData();
            }

            return session;
        }

        public int GetNextTrainingDay()
        {
            var rotationIndex = GetTrainingRotationIndex();
            return (rotationIndex % 3) + 1;
        }

        public int GetTrainingRotationIndex()
        {
#if ANDROID
            // Behavior-driven: the day counter only moves when the user finishes a workout
            // (see AdvanceVisitPlanForNextSession). It never auto-advances on a wall clock,
            // so skipping a day leaves the plan exactly where the user left off.
            var prefs = Android.App.Application.Context.GetSharedPreferences(TrainingRoutinePrefsName, FileCreationMode.Private);
            return prefs.GetInt(TrainingRoutineOffsetKey, 0);
#else
            return 0;
#endif
        }

        public string[] GetDailyWorkoutGroups()
        {
#if ANDROID
            var prefs = Android.App.Application.Context.GetSharedPreferences(TrainingRoutinePrefsName, FileCreationMode.Private);
            var (upperIndex, lowerIndex) = ResolveVisitPlanIndices(prefs, DateTime.UtcNow, Random.Shared);
            return BuildDailyWorkoutGroups(upperIndex, lowerIndex);
#else
            return BuildDailyWorkoutGroups(0, 0);
#endif
        }

#if ANDROID
        /// <summary>
        /// After Finish Workout: pick the next random plan (upper and lower both change) and
        /// advance the training-day counter. This is the single, behavior-driven trigger for
        /// moving the rotation forward — completing Day 1 moves the user to Day 2, etc.,
        /// regardless of how many calendar days passed in between.
        /// </summary>
        public void AdvanceVisitPlanForNextSession()
        {
            var prefs = Android.App.Application.Context.GetSharedPreferences(TrainingRoutinePrefsName, FileCreationMode.Private);
            var previousUpper = prefs.GetInt(TrainingRoutineUpperPairIndexKey, -1);
            var previousLower = prefs.GetInt(TrainingRoutineLowerMuscleIndexKey, -1);
            var (selectedUpper, selectedLower) = SelectRandomVisitPlanDifferentFrom(
                previousUpper,
                previousLower,
                Random.Shared);
            PersistVisitPlanSelection(prefs, selectedUpper, selectedLower, DateTime.UtcNow);

            var offset = prefs.GetInt(TrainingRoutineOffsetKey, 0);
            prefs.Edit()
                .PutInt(TrainingRoutineOffsetKey, offset + 1)
                .PutString(TrainingRoutineTimestampKey, DateTime.Now.ToString("o"))
                .Apply();
        }

        private static void PersistVisitPlanSelection(
            Android.Content.ISharedPreferences prefs,
            int upperIndex,
            int lowerIndex,
            DateTime selectionUtc)
        {
            var localNow = selectionUtc.ToLocalTime();
            prefs.Edit()
                .PutString(TrainingRoutineLastSelectionDateKey, localNow.ToString("yyyyMMdd"))
                .PutString(TrainingRoutineLastSelectionTimestampKey, selectionUtc.ToString("o"))
                .PutInt(TrainingRoutineUpperPairIndexKey, upperIndex)
                .PutInt(TrainingRoutineLowerMuscleIndexKey, lowerIndex)
                .Apply();
        }

#endif

#if ANDROID
        private static (int UpperIndex, int LowerIndex) ResolveVisitPlanIndices(
            Android.Content.ISharedPreferences prefs,
            DateTime nowUtc,
            Random random)
        {
            var previousUpper = prefs.GetInt(TrainingRoutineUpperPairIndexKey, -1);
            var previousLower = prefs.GetInt(TrainingRoutineLowerMuscleIndexKey, -1);

            if (HasStoredVisitPlan(previousUpper, previousLower))
            {
                // Behavior-driven rotation: keep the current plan locked until the user taps
                // Finish Workout. Coming back a day (or several days) later must not reshuffle it.
                return (previousUpper, previousLower);
            }

            // No plan has ever been assigned for this user: pick one and lock it in.
            var (selectedUpper, selectedLower) = SelectRandomVisitPlanDifferentFrom(previousUpper, previousLower, random);
            PersistVisitPlanSelection(prefs, selectedUpper, selectedLower, nowUtc);

            return (selectedUpper, selectedLower);
        }
#endif

        /// <summary>
        /// True when a visit plan has already been assigned and stored for the user.
        /// While a plan is stored it stays locked and only changes on Finish Workout.
        /// </summary>
        internal static bool HasStoredVisitPlan(int upperIndex, int lowerIndex)
        {
            return upperIndex >= 0
                && lowerIndex >= 0
                && upperIndex < UpperBodyPairs.Length
                && lowerIndex < LowerBodyMuscles.Length;
        }

        /// <summary>
        /// Legacy time-based trigger. No longer wired into the live rotation, which is now
        /// behavior-driven (advances on Finish Workout). Retained for reference/tests.
        /// </summary>
        internal static bool ShouldSelectNewPlanAfter24Hours(DateTime? lastSelectionUtc, DateTime nowUtc)
        {
            if (!lastSelectionUtc.HasValue)
            {
                return true;
            }

            return (nowUtc - lastSelectionUtc.Value).TotalHours >= 24;
        }

        /// <summary>Legacy calendar-day helper; superseded by behavior-driven advancement.</summary>
        internal static bool ShouldSelectNewPlanForCalendarDay(string? lastSelectionDate, string today)
        {
            return !string.Equals(lastSelectionDate, today, StringComparison.Ordinal);
        }

        /// <summary>
        /// Legacy time-based day-offset advance. No longer wired into the live rotation,
        /// which now advances the day counter on Finish Workout. Retained for reference/tests.
        /// </summary>
        internal static int AdvanceRotationOffset(int offset, DateTime lastTimestamp, DateTime now)
        {
            var advanceDays = (int)((now - lastTimestamp).TotalHours / 24);
            return advanceDays > 0 ? offset + advanceDays : offset;
        }

        /// <summary>Random upper + lower for a new visit; both must differ from the previous visit when possible.</summary>
        internal static (int UpperIndex, int LowerIndex) SelectRandomVisitPlanDifferentFrom(
            int previousUpperIndex,
            int previousLowerIndex,
            Random random)
        {
            if (UpperBodyPairs.Length == 0 || LowerBodyMuscles.Length == 0)
                return (0, 0);

            if (previousUpperIndex < 0
                || previousLowerIndex < 0
                || previousUpperIndex >= UpperBodyPairs.Length
                || previousLowerIndex >= LowerBodyMuscles.Length)
            {
                return (random.Next(UpperBodyPairs.Length), random.Next(LowerBodyMuscles.Length));
            }

            int upper;
            int lower;
            do
            {
                upper = random.Next(UpperBodyPairs.Length);
                lower = random.Next(LowerBodyMuscles.Length);
            }
            while (!VisitPlanDiffersFromPrevious(previousUpperIndex, previousLowerIndex, upper, lower));

            return (upper, lower);
        }

        internal static bool VisitPlanDiffersFromPrevious(
            int previousUpperIndex,
            int previousLowerIndex,
            int upperIndex,
            int lowerIndex)
        {
            var upperChanged = UpperBodyPairs.Length <= 1 || upperIndex != previousUpperIndex;
            var lowerChanged = LowerBodyMuscles.Length <= 1 || lowerIndex != previousLowerIndex;
            return upperChanged && lowerChanged;
        }

        internal static int SelectUpperPairIndexDifferentFromPrevious(int previousUpperPairIndex, Random random)
        {
            if (UpperBodyPairs.Length <= 1)
                return 0;

            if (previousUpperPairIndex < 0 || previousUpperPairIndex >= UpperBodyPairs.Length)
                return random.Next(UpperBodyPairs.Length);

            int selected;
            do
            {
                selected = random.Next(UpperBodyPairs.Length);
            }
            while (selected == previousUpperPairIndex);

            return selected;
        }

        internal static int NormalizeIndex(int index, int length)
        {
            if (length <= 0)
                return 0;

            return ((index % length) + length) % length;
        }

        internal static string[] BuildDailyWorkoutGroups(int upperPairIndex, int lowerBodyIndex)
        {
            var upperPair = UpperBodyPairs[NormalizeIndex(upperPairIndex, UpperBodyPairs.Length)];
            var lower = LowerBodyMuscles[NormalizeIndex(lowerBodyIndex, LowerBodyMuscles.Length)];
            return new[] { upperPair[0], upperPair[1], lower };
        }

        public int GetCompletedWorkoutCount()
        {
            return CurrentUserSessions().Count(s => s.IsCompleted);
        }

        public List<WorkoutSession> GetWorkoutHistory(int limit = 20)
        {
            return CurrentUserSessions()
                .Where(s => s.IsCompleted)
                .OrderByDescending(s => s.StartTime)
                .Take(limit)
                .ToList();
        }

        public WorkoutSession? GetCurrentWorkout()
        {
            return CurrentUserSessions().FirstOrDefault(s => !s.IsCompleted);
        }

        public WorkoutSession? GetWorkoutSession(int id)
        {
            return CurrentUserSessions().FirstOrDefault(s => s.Id == id);
        }

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

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(_dataPath, "gym_data.json"), json);

#if ANDROID
                if (triggerCloudSync && !IsGuestUser)
                {
                    WorkoutCloudSyncService.NotifyLocalWorkoutsChanged(this);
                }
#endif
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving data: {ex.Message}");
            }
        }

        private void LoadData()
        {
            try
            {
                var filePath = Path.Combine(_dataPath, "gym_data.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    
                    if (data != null)
                    {
                        if (data.ContainsKey("Exercises"))
                            _exercises = JsonSerializer.Deserialize<List<Exercise>>(data["Exercises"].GetRawText()) ?? new List<Exercise>();
                        
                        if (data.ContainsKey("WorkoutSessions"))
                            _workoutSessions = JsonSerializer.Deserialize<List<WorkoutSession>>(data["WorkoutSessions"].GetRawText()) ?? new List<WorkoutSession>();
                        
                        if (data.ContainsKey("NextExerciseId"))
                            _nextExerciseId = data["NextExerciseId"].GetInt32();
                        
                        if (data.ContainsKey("NextWorkoutSessionId"))
                            _nextWorkoutSessionId = data["NextWorkoutSessionId"].GetInt32();
                        
                        if (data.ContainsKey("NextWorkoutExerciseId"))
                            _nextWorkoutExerciseId = data["NextWorkoutExerciseId"].GetInt32();
                        
                        if (data.ContainsKey("NextWorkoutSetId"))
                            _nextWorkoutSetId = data["NextWorkoutSetId"].GetInt32();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading data: {ex.Message}");
            }
        }
    }
}
