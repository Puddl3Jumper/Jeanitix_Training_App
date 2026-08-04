using JeanetixMAUI.Data;

namespace JeanetixMAUI.Pages;

public partial class ProgressPage : ContentPage
{
    private GymDatabase? _database;
    private List<string> _exerciseNames = new();

    public ProgressPage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _database ??= new GymDatabase();
        _exerciseNames = _database.GetAllExercises().Select(e => e.Name).OrderBy(n => n).ToList();
        ExercisePicker.ItemsSource = _exerciseNames;
        if (_exerciseNames.Count > 0) ExercisePicker.SelectedIndex = 0;
    }

    private void OnExerciseSelected(object sender, EventArgs e)
    {
        if (ExercisePicker.SelectedItem is not string name) return;
        RenderProgress(name);
    }

    private void RenderProgress(string exerciseName)
    {
        ProgressChart.Children.Clear();
        var data = _database?.GetExerciseProgress(exerciseName, 20) ?? new List<(DateTime, double)>();
        var (pr, last) = _database?.GetExercisePrAndLastWeight(exerciseName) ?? (0, 0);

        var unit = Preferences.Get("profile_unit", "kg");
        PrLabel.Text = pr > 0 ? $"{pr:0.#} {unit}" : "—";
        LastLabel.Text = last > 0 ? $"{last:0.#} {unit}" : "—";

        ChartEmptyLabel.IsVisible = data.Count == 0;
        if (data.Count == 0) return;

        var maxW = data.Max(d => d.maxWeight);
        foreach (var (date, weight) in data)
        {
            var ratio = maxW <= 0 ? 0 : weight / maxW;
            var barH = Math.Max(8, (int)(96 * ratio));
            var col = new VerticalStackLayout { VerticalOptions = LayoutOptions.End, Spacing = 2, WidthRequest = 28 };
            col.Add(new BoxView { Color = Color.FromArgb("#D4AF37"), CornerRadius = 4, HeightRequest = barH, WidthRequest = 22, HorizontalOptions = LayoutOptions.Center });
            col.Add(new Label { Text = date.ToString("M/d"), FontSize = 9, TextColor = Color.FromArgb("#6B7280"), HorizontalOptions = LayoutOptions.Center });
            ProgressChart.Children.Add(col);
        }
    }
}
