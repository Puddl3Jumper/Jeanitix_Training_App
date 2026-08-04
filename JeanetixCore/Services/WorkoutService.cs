using JeanetixCore.Models;

namespace JeanetixCore.Services
{
    public class WorkoutService : IWorkoutService
    {
        private readonly List<Exercise> _exercises = new()
        {
            new Exercise { Id = 1, Name = "Bench Press", MuscleGroup = "Chest" },
            new Exercise { Id = 2, Name = "Incline Press", MuscleGroup = "Chest" },
            new Exercise { Id = 3, Name = "Barbell Row", MuscleGroup = "Back" },
            new Exercise { Id = 4, Name = "Lat Pulldown", MuscleGroup = "Back" },
            new Exercise { Id = 5, Name = "Shoulder Press", MuscleGroup = "Shoulder" },
            new Exercise { Id = 6, Name = "Lateral Raise", MuscleGroup = "Shoulder" },
            new Exercise { Id = 7, Name = "Biceps Curl", MuscleGroup = "Arm" },
            new Exercise { Id = 8, Name = "Triceps Rope", MuscleGroup = "Arm" },
            new Exercise { Id = 9, Name = "Squat", MuscleGroup = "Leg" },
            new Exercise { Id = 10, Name = "Leg Press", MuscleGroup = "Leg" },
        };

        private readonly List<WorkoutSession> _workoutHistory = new();

        public Task<List<Exercise>> GetExercisesAsync()
        {
            return Task.FromResult(_exercises);
        }

        public Task<List<WorkoutSession>> GetWorkoutHistoryAsync()
        {
            return Task.FromResult(_workoutHistory.OrderByDescending(w => w.StartTime).ToList());
        }

        public Task<WorkoutSession> CreateWorkoutAsync(string name)
        {
            var session = new WorkoutSession
            {
                Id = _workoutHistory.Count + 1,
                Name = name,
                StartTime = DateTime.Now,
                UserKey = "default"
            };
            _workoutHistory.Add(session);
            return Task.FromResult(session);
        }

        public Task<bool> CompleteWorkoutAsync(WorkoutSession session)
        {
            session.EndTime = DateTime.Now;
            session.IsCompleted = true;
            return Task.FromResult(true);
        }

        public Task<bool> AddSetToExerciseAsync(int workoutExerciseId, WorkoutSet set)
        {
            var exercise = _workoutHistory
                .SelectMany(s => s.Exercises)
                .FirstOrDefault(e => e.Id == workoutExerciseId);

            if (exercise != null)
            {
                exercise.Sets.Add(set);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }
}
