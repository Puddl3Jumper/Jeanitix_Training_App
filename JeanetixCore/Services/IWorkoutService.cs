using JeanetixCore.Models;

namespace JeanetixCore.Services
{
    public interface IWorkoutService
    {
        Task<List<Exercise>> GetExercisesAsync();
        Task<List<WorkoutSession>> GetWorkoutHistoryAsync();
        Task<WorkoutSession> CreateWorkoutAsync(string name);
        Task<bool> CompleteWorkoutAsync(WorkoutSession session);
        Task<bool> AddSetToExerciseAsync(int workoutExerciseId, WorkoutSet set);
    }
}
