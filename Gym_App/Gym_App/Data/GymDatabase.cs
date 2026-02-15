using System.Text.Json;
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

        public GymDatabase()
        {
            _dataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GymApp");
            
            Directory.CreateDirectory(_dataPath);
            
            _exercises = new List<Exercise>();
            _workoutSessions = new List<WorkoutSession>();
            _nextExerciseId = 1;
            _nextWorkoutSessionId = 1;
            _nextWorkoutExerciseId = 1;
            _nextWorkoutSetId = 1;
            
            LoadData();
            InitializeDefaultExercises();
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
                    new Exercise { Id = _nextExerciseId++, Name = "Tricep Dips", MuscleGroup = "Arms", Description = "Bodyweight or weighted dips", IsCustom = false },
                    
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
                IsCompleted = false
            };
            _workoutSessions.Add(session);
            SaveData();
            return session;
        }

        public void AddExerciseToWorkout(int workoutSessionId, int exerciseId, string? circuitName = null)
        {
            var session = _workoutSessions.FirstOrDefault(s => s.Id == workoutSessionId);
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
            foreach (var session in _workoutSessions)
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
            var session = _workoutSessions.FirstOrDefault(s => s.Id == workoutSessionId);
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
            var session = _workoutSessions.FirstOrDefault(s => s.Id == workoutSessionId);
            if (session == null)
                return;

            _workoutSessions.Remove(session);
            SaveData();
        }

        public void UpdateWorkoutSet(int setId, int reps, double weight, string? notes)
        {
            foreach (var session in _workoutSessions)
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
            foreach (var session in _workoutSessions)
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
            return _workoutSessions
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
            var entries = _workoutSessions
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
            var filePath = Path.Combine(_dataPath, fileName);

            using var writer = new StreamWriter(filePath, false);
            writer.WriteLine("WorkoutDate,WorkoutName,Exercise,SetNumber,Reps,Weight,Unit,SetNotes,WorkoutNotes");

            var sessions = _workoutSessions
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

            return filePath;
        }

        private static string EscapeCsv(string input)
        {
            if (input.Contains(',') || input.Contains('"') || input.Contains('\n'))
                return $"\"{input.Replace("\"", "\"\"")}\"";

            return input;
        }

        public void CompleteWorkout(int workoutSessionId)
        {
            var session = _workoutSessions.FirstOrDefault(s => s.Id == workoutSessionId);
            if (session != null)
            {
                session.EndTime = DateTime.Now;
                session.IsCompleted = true;
                SaveData();
            }
        }

        public List<WorkoutSession> GetWorkoutHistory(int limit = 20)
        {
            return _workoutSessions
                .Where(s => s.IsCompleted)
                .OrderByDescending(s => s.StartTime)
                .Take(limit)
                .ToList();
        }

        public WorkoutSession? GetCurrentWorkout()
        {
            return _workoutSessions.FirstOrDefault(s => !s.IsCompleted);
        }

        public WorkoutSession? GetWorkoutSession(int id)
        {
            return _workoutSessions.FirstOrDefault(s => s.Id == id);
        }

        private void SaveData()
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
