namespace Gym_App.Models
{
    public class WorkoutExercise
    {
        public int Id { get; set; }
        public int WorkoutSessionId { get; set; }
        public int ExerciseId { get; set; }
        public Exercise? Exercise { get; set; }
        public List<WorkoutSet> Sets { get; set; } = new List<WorkoutSet>();
        public int OrderIndex { get; set; }
        public string? CircuitName { get; set; }
        public int CircuitOrder { get; set; }
    }
}
