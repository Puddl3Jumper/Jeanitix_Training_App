namespace JeanetixMAUI.Models;

public class Exercise
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCustom { get; set; }
}

public class WorkoutSession
{
    public int Id { get; set; }
    public string UserKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<WorkoutExercise> Exercises { get; set; } = new();
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : DateTime.Now - StartTime;
}

public class WorkoutExercise
{
    public int Id { get; set; }
    public int WorkoutSessionId { get; set; }
    public int ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }
    public List<WorkoutSet> Sets { get; set; } = new();
    public int OrderIndex { get; set; }
    public string? CircuitName { get; set; }
    public int CircuitOrder { get; set; }
}

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
