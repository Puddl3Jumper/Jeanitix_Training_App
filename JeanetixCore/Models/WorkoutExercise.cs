namespace JeanetixCore.Models
{
    public class WorkoutExercise
    {
        public int Id { get; set; }
        public int WorkoutSessionId { get; set; }
        public int ExerciseId { get; set; }
        public int CircuitNumber { get; set; }
        public int LoopCount { get; set; }
        public List<WorkoutSet> Sets { get; set; } = new List<WorkoutSet>();
    }
}
