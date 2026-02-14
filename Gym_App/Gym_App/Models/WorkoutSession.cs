namespace Gym_App.Models
{
    public class WorkoutSession
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public List<WorkoutExercise> Exercises { get; set; } = new List<WorkoutExercise>();
        public string? Notes { get; set; }
        public bool IsCompleted { get; set; }
        
        public TimeSpan Duration => EndTime.HasValue 
            ? EndTime.Value - StartTime 
            : DateTime.Now - StartTime;
    }
}
