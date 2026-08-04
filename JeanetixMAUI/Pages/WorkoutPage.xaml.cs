using JeanetixMAUI.Data;
using JeanetixMAUI.Models;

namespace JeanetixMAUI.Pages;

public partial class WorkoutPage : ContentPage
{
    private GymDatabase? _database;
    private WorkoutSession? _currentWorkout;
    private WorkoutExercise? _currentExercise;
    private DateTime _workoutStartTime;
    private IDispatcherTimer? _timer;
    private bool _isTimerRunning;

    public WorkoutPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _database ??= new GymDatabase();
        LoadWorkoutState();
        StartTimer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopTimer();
    }

    private void LoadWorkoutState()
    {
        _currentWorkout = _database?.GetCurrentWorkout();
        if (_currentWorkout != null)
        {
            _workoutStartTime = _currentWorkout.StartTime;
            WorkoutNameLabel.Text = _currentWorkout.Name;
        }
        else
        {
            WorkoutNameLabel.Text = "No active workout";
        }
        RefreshExerciseList();
    }

    private void StartTimer()
    {
        if (_isTimerRunning) return;
        _isTimerRunning = true;
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _isTimerRunning = false;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_currentWorkout == null) return;
        var elapsed = DateTime.Now - _workoutStartTime;
        TimerLabel.Text = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
    }

    private async void OnAddExerciseClicked(object sender, EventArgs e)
    {
        if (_database == null) return;

        // Ensure a workout session exists
        if (_currentWorkout == null)
        {
            var groups = _database.GetDailyWorkoutGroups();
            var name = GymDatabase.BuildRoutineSessionName(groups);
            _currentWorkout = _database.StartTimedWorkout(name, resetStartTime: true);
            _workoutStartTime = _currentWorkout.StartTime;
            WorkoutNameLabel.Text = _currentWorkout.Name;
        }

        await Shell.Current.GoToAsync(nameof(ExerciseLibraryPage) + $"?workoutId={_currentWorkout.Id}");
    }

    private void OnLogSetClicked(object sender, EventArgs e)
    {
        if (_database == null || _currentWorkout == null || _currentExercise == null)
        {
            DisplayAlert("No Exercise", "Add an exercise first", "OK");
            return;
        }

        if (!int.TryParse(RepsEntry.Text, out var reps) || reps <= 0)
        {
            DisplayAlert("Invalid", "Enter a valid rep count", "OK");
            return;
        }

        if (!double.TryParse(WeightEntry.Text, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var weight))
            weight = 0;

        var unit = Preferences.Get("profile_unit", "kg");
        _database.AddSetToExercise(_currentExercise.Id, reps, weight, weightUnit: unit);
        RefreshExerciseList();
    }

    private void RefreshExerciseList()
    {
        ExerciseSetsContainer.Children.Clear();
        if (_currentWorkout == null) { NoExercisesLabel.IsVisible = true; return; }

        _currentWorkout = _database?.GetWorkoutSession(_currentWorkout.Id) ?? _currentWorkout;
        var exercises = _currentWorkout.Exercises;
        NoExercisesLabel.IsVisible = exercises.Count == 0;

        foreach (var we in exercises)
        {
            var exName = we.Exercise?.Name ?? "Unknown";
            var header = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
            var nameLabel = new Label { Text = exName, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
                var selectBtn = new Button { Text = "Select", FontSize = 12, TextColor = Color.FromArgb("#D4AF37"), BackgroundColor = Colors.Transparent, Padding = new Thickness(0) };
            var capturedWe = we;
            selectBtn.Clicked += (_, _) =>
            {
                _currentExercise = capturedWe;
                CurrentExerciseLabel.Text = exName;
                ExerciseMuscleLabel.Text = capturedWe.Exercise?.MuscleGroup ?? "";
            };
            header.Add(nameLabel, 0, 0);
            header.Add(selectBtn, 1, 0);

            var container = new VerticalStackLayout { Spacing = 4 };
            container.Add(header);

            foreach (var set in we.Sets)
            {
                var unit = set.WeightUnit;
                var setRow = new Label
                {
                    Text = $"  Set {set.SetNumber}: {set.Reps} reps × {set.Weight:0.#} {unit}",
                    TextColor = Color.FromArgb("#9CA3AF"), FontSize = 13
                };
                container.Add(setRow);
            }

            ExerciseSetsContainer.Children.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#202026"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                StrokeThickness = 0,
                Padding = new Thickness(12),
                Content = container
            });
        }

        // Auto-select last added exercise
        if (exercises.Count > 0 && _currentExercise == null)
        {
            _currentExercise = exercises[^1];
            CurrentExerciseLabel.Text = _currentExercise.Exercise?.Name ?? "";
            ExerciseMuscleLabel.Text = _currentExercise.Exercise?.MuscleGroup ?? "";
        }
    }

    private async void OnFinishWorkoutClicked(object sender, EventArgs e)
    {
        if (_database == null || _currentWorkout == null)
        {
            await DisplayAlert("No Workout", "Start a workout first", "OK");
            return;
        }
        bool confirm = await DisplayAlert("Finish Workout", "Complete and save this workout?", "Finish", "Cancel");
        if (!confirm) return;

        _database.CompleteWorkout(_currentWorkout.Id);
        _currentWorkout = null;
        _currentExercise = null;
        StopTimer();
        TimerLabel.Text = "00:00";
        WorkoutNameLabel.Text = "No active workout";
        RefreshExerciseList();
        await DisplayAlert("Done!", "Workout saved!", "OK");
    }

    private async void OnHistoryClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//HistoryPage");
    }
}
