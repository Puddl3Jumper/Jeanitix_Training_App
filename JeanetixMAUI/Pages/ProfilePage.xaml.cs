using JeanetixMAUI.Data;

namespace JeanetixMAUI.Pages;

public partial class ProfilePage : ContentPage
{
    private GymDatabase? _database;

    public ProfilePage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _database ??= new GymDatabase();
        LoadProfile();
        LoadStats();
        LoadPrs();
    }

    private void LoadProfile()
    {
        var name = Preferences.Get("profile_full_name", "User");
        var email = Preferences.Get("profile_email", string.Empty);
        if (string.IsNullOrWhiteSpace(name)) name = "User";

        NameLabel.Text = name;
        EmailLabel.Text = email;
        AvatarInitials.Text = string.IsNullOrWhiteSpace(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();

        var height = Preferences.Get("profile_height_cm", string.Empty);
        var weight = Preferences.Get("profile_weight", string.Empty);
        var unit = Preferences.Get("profile_unit", "kg");
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(height)) parts.Add($"{height} cm");
        if (!string.IsNullOrWhiteSpace(weight)) parts.Add($"{weight} {unit}");
        StatsLabel.Text = string.Join(" · ", parts);
    }

    private void LoadStats()
    {
        var total = _database?.GetCompletedWorkoutCount() ?? 0;
        var history = _database?.GetWorkoutHistory(200) ?? new List<Models.WorkoutSession>();
        var weekStart = GetWeekStart(DateTime.Today);
        var thisWeek = history.Count(s => s.StartTime.Date >= weekStart);
        var goal = Preferences.Get("profile_weekly_goal", 4);

        TotalWorkoutsLabel.Text = total.ToString();
        ThisWeekLabel.Text = thisWeek.ToString();
        WeeklyGoalLabel.Text = goal.ToString();
    }

    private void LoadPrs()
    {
        PrsContainer.Children.Clear();
        var keyExercises = new[] { "Bench Press", "Squat", "Deadlift", "Shoulder Press", "Bicep Curls" };
        foreach (var name in keyExercises)
        {
            var (pr, last) = _database?.GetExercisePrAndLastWeight(name) ?? (0, 0);
            if (pr <= 0) continue;
            var unit = Preferences.Get("profile_unit", "kg");
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
            row.Add(new Label { Text = name, TextColor = Colors.White, FontSize = 14 }, 0, 0);
            row.Add(new Label { Text = $"PR: {pr:0.#} {unit}", TextColor = Color.FromArgb("#D4AF37"), FontSize = 14, HorizontalOptions = LayoutOptions.End }, 1, 0);
            PrsContainer.Children.Add(row);
        }
        if (PrsContainer.Children.Count == 0)
            PrsContainer.Children.Add(new Label { Text = "Complete workouts to track PRs.", TextColor = Color.FromArgb("#6B7280"), FontSize = 13 });
    }

    private async void OnEditProfileClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(EditProfilePage));

    private async void OnProgressClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(ProgressPage));

    private async void OnSettingsClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(SettingsPage));

    private async void OnSignOutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Sign Out", "Are you sure you want to sign out?", "Sign Out", "Cancel");
        if (!confirm) return;
        AuthSessionStore.Clear();
        Preferences.Remove("profile_full_name");
        Preferences.Remove("profile_email");
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }

    private static DateTime GetWeekStart(DateTime date) { int off = ((int)date.DayOfWeek + 6) % 7; return date.AddDays(-off); }
}
