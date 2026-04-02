namespace AureusApp.Models;

public sealed class TrainingExercise
{
    public required string Name { get; init; }
    public required string Prescription { get; init; }
    public List<TrainingSet> Sets { get; init; } = [];
    public bool StartCollapsed { get; set; }
    public int? TargetWeightKg { get; init; }
}

public sealed class TrainingSet
{
    public required int Number { get; init; }
    public int? WeightKg { get; set; }
    public int? Reps { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsEditable { get; init; }
}
