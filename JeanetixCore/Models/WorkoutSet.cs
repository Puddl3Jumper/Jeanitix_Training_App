namespace JeanetixCore.Models
{
    public class WorkoutSet
    {
        public int Id { get; set; }
        public int WorkoutExerciseId { get; set; }
        public int SetNumber { get; set; }
        public int LoopNumber { get; set; }
        public int Reps { get; set; }
        public double Weight { get; set; }
        public string WeightUnit { get; set; } = "kg";
        public bool Completed { get; set; }
        public string? Notes { get; set; }
    }
}
