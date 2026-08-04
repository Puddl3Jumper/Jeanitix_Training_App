using JeanetixMAUI.Data;
using JeanetixMAUI.Models;

namespace JeanetixMAUI.Pages;

[QueryProperty(nameof(WorkoutId), "workoutId")]
public partial class ExerciseLibraryPage : ContentPage
{
    private GymDatabase? _database;
    private List<Exercise> _allExercises = new();
    private string _selectedMuscle = "All";
    private int _workoutId;

    public int WorkoutId { get => _workoutId; set { _workoutId = value; } }

    public ExerciseLibraryPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _database ??= new GymDatabase();
        _allExercises = _database.GetAllExercises();
        BuildMuscleFilters();
        RenderList();
    }

    private void BuildMuscleFilters()
    {
        FilterRow.Children.Clear();
        var muscles = new[] { "All" }.Concat(_allExercises.Select(e => e.MuscleGroup).Distinct().OrderBy(m => m)).ToList();
        foreach (var muscle in muscles)
        {
            var b = new Border
            {
                BackgroundColor = muscle == _selectedMuscle ? Color.FromArgb("#D4AF37") : Color.FromArgb("#202026"),
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(14, 6)
            };
                var l = new Label { Text = muscle, TextColor = muscle == _selectedMuscle ? Color.FromArgb("#1F1600") : Color.FromArgb("#9E9E9E"), FontSize = 13 };
            b.Content = l;
            var captured = muscle;
            b.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() => { _selectedMuscle = captured; BuildMuscleFilters(); RenderList(); })
            });
            FilterRow.Children.Add(b);
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RenderList();

    private void RenderList()
    {
        ExerciseList.Children.Clear();
        var query = SearchBar.Text?.Trim().ToLower() ?? string.Empty;
        var filtered = _allExercises
            .Where(e => (_selectedMuscle == "All" || e.MuscleGroup == _selectedMuscle))
            .Where(e => string.IsNullOrEmpty(query) || e.Name.ToLower().Contains(query) || e.MuscleGroup.ToLower().Contains(query))
            .ToList();

        foreach (var ex in filtered)
        {
            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#24242C"),
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(14, 12)
            };

            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
            var info = new VerticalStackLayout { Spacing = 2 };
            info.Add(new Label { Text = ex.Name, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Colors.White });
            info.Add(new Label { Text = ex.MuscleGroup, FontSize = 12, TextColor = Color.FromArgb("#9CA3AF") });
            if (!string.IsNullOrWhiteSpace(ex.Description))
                info.Add(new Label { Text = ex.Description, FontSize = 12, TextColor = Color.FromArgb("#6B7280") });
            row.Add(info, 0, 0);

            if (_workoutId > 0)
            {
                var addBtn = new Button { Text = "Add", FontSize = 13, TextColor = Color.FromArgb("#D4AF37"), BackgroundColor = Colors.Transparent, Padding = new Thickness(0), VerticalOptions = LayoutOptions.Center };
                var capturedEx = ex;
                addBtn.Clicked += async (_, _) =>
                {
                    _database?.AddExerciseToWorkout(_workoutId, capturedEx.Id);
                    await DisplayAlert("Added", $"{capturedEx.Name} added to workout", "OK");
                    await Shell.Current.GoToAsync("..");
                };
                row.Add(addBtn, 1, 0);
            }
            card.Content = row;
            ExerciseList.Children.Add(card);
        }

        // Add custom exercise button
        var addCustom = new Button
        {
            Text = "+ Add Custom Exercise",
            BackgroundColor = Color.FromArgb("#D4AF37"),
            TextColor = Color.FromArgb("#1F1600"),
            BorderColor = Color.FromArgb("#D4AF37"),
            BorderWidth = 1,
            CornerRadius = 12,
            HeightRequest = 48,
            Margin = new Thickness(0, 8, 0, 0)
        };
        addCustom.Clicked += OnAddCustomClicked;
        ExerciseList.Children.Add(addCustom);
    }

    private async void OnAddCustomClicked(object sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Custom Exercise", "Exercise name:", "Add", "Cancel");
        if (string.IsNullOrWhiteSpace(name)) return;
        var muscle = await DisplayActionSheet("Muscle Group", "Cancel", null, "Chest", "Back", "Legs", "Arms", "Shoulders", "Core");
        if (muscle == "Cancel" || string.IsNullOrWhiteSpace(muscle)) return;
        _database?.AddExercise(new Exercise { Name = name.Trim(), MuscleGroup = muscle, IsCustom = true });
        _allExercises = _database?.GetAllExercises() ?? new();
        BuildMuscleFilters();
        RenderList();
    }
}
