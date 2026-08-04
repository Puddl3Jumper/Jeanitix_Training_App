using JeanetixMAUI.Data;
using JeanetixMAUI.Models;

namespace JeanetixMAUI.Pages;

public partial class HomePage : ContentPage
{
    private GymDatabase? _database;

    public HomePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _database ??= new GymDatabase();
        LoadPage();
        _ = TryPullAndRefreshAsync();
    }

    private void LoadPage()
    {
        var name = Preferences.Get("profile_full_name", "there");
        if (string.IsNullOrWhiteSpace(name)) name = "there";
        var firstName = GetDisplayFirstName(name);
        WelcomeLabel.Text = $"Welcome, {firstName}!";

        RenderTodayPlan();
        LoadTodayActivity();
        UpdateWeeklyChart();
    }

    private void RenderTodayPlan()
    {
        var groups = _database?.GetDailyWorkoutGroups() ?? new[] { "Biceps", "Triceps", "Legs" };
        UpperBodyLabel.Text = $"{groups[0]} / {groups[1]}";
        LowerBodyLabel.Text = groups.Length > 2 ? groups[2] : "Legs";
    }

    private void LoadTodayActivity()
    {
        TodayExercisesContainer.Children.Clear();
        var history = _database?.GetWorkoutHistory(100) ?? new List<WorkoutSession>();
        var today = DateTime.Today;
        var completedToday = history.Where(s => s.StartTime.Date == today).ToList();
        var current = _database?.GetCurrentWorkout();
        if (current?.StartTime.Date == today) completedToday.Add(current);

        var exerciseRecords = completedToday
            .SelectMany(s => s.Exercises)
            .Where(e => e.Exercise != null)
            .ToList();

        EmptyStateLabel.IsVisible = exerciseRecords.Count == 0;

        foreach (var record in exerciseRecords)
        {
            if (record.Exercise == null) continue;
            var setCount = record.Sets.Count;
            var totalReps = record.Sets.Sum(s => s.Reps);
            var maxWeight = record.Sets.Count == 0 ? 0 : record.Sets.Max(s => s.Weight);
            var unit = record.Sets.FirstOrDefault()?.WeightUnit ?? "kg";
            var summary = setCount == 0 ? "0 sets" : $"{setCount} set{(setCount == 1 ? "" : "s")} · {totalReps} reps · {maxWeight:0.#} {unit}";

            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
            row.Add(new Label { Text = record.Exercise.Name, TextColor = Colors.White, FontSize = 14 }, 0, 0);
            row.Add(new Label { Text = summary, TextColor = Color.FromArgb("#9CA3AF"), FontSize = 13, HorizontalOptions = LayoutOptions.End }, 1, 0);
            TodayExercisesContainer.Children.Add(row);
        }

        UpdateInsight(history);
    }

    private void UpdateWeeklyChart()
    {
        WeeklyChartGrid.Children.Clear();
        WeeklyChartGrid.RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto));

        var history = _database?.GetWorkoutHistory(200) ?? new List<WorkoutSession>();
        var weekStart = GetWeekStart(DateTime.Today);
        var labels = new[] { "M", "T", "W", "T", "F", "S", "S" };
        var dailyMinutes = new double[7];

        for (int i = 0; i < 7; i++)
        {
            var day = weekStart.AddDays(i).Date;
            dailyMinutes[i] = Math.Min(120, history
                .Where(s => s.StartTime.Date == day && s.Exercises.Any(e => e.Sets.Count > 0))
                .Where(s => s.Duration > TimeSpan.Zero)
                .Sum(s => s.Duration.TotalMinutes));
        }

        const double maxMinutes = 120;
        const double goalMinutes = 60;
        int workoutsThisWeek = history.Count(s => s.StartTime.Date >= weekStart && s.Exercises.Any(e => e.Sets.Count > 0));
        double avgMinutes = dailyMinutes.Average();
        int goalDays = dailyMinutes.Count(m => m >= goalMinutes);

        WorkoutsThisWeekLabel.Text = workoutsThisWeek.ToString();
        AvgMinutesLabel.Text = ((int)Math.Round(avgMinutes)).ToString();
        GoalDaysLabel.Text = goalDays.ToString();

        var weeklyGoal = Preferences.Get("profile_weekly_goal", 4);
        WeekGoalLabel.Text = $"{workoutsThisWeek}/{weeklyGoal} goal";

        for (int i = 0; i < 7; i++)
        {
            var ratio = maxMinutes <= 0 ? 0 : dailyMinutes[i] / maxMinutes;
            var barHeight = ratio <= 0 ? 8 : Math.Max(16, (int)(72 * ratio));
            var barColor = dailyMinutes[i] >= goalMinutes ? Color.FromArgb("#D4AF37") : Color.FromArgb("#FBE9B7");

            var barCol = new VerticalStackLayout { VerticalOptions = LayoutOptions.End, Spacing = 4 };
            barCol.Add(new BoxView { Color = barColor, CornerRadius = 4, HeightRequest = barHeight, WidthRequest = 22, HorizontalOptions = LayoutOptions.Center });
            barCol.Add(new Label { Text = labels[i], FontSize = 10, TextColor = Color.FromArgb("#6B7280"), HorizontalOptions = LayoutOptions.Center });

            WeeklyChartGrid.Add(barCol, i, 0);
        }
    }

    private void UpdateInsight(List<WorkoutSession> history)
    {
        var today = DateTime.Today;
        var weekStart = GetWeekStart(today);
        var workoutsThisWeek = history.Count(s => s.StartTime.Date >= weekStart);

        // Check for strength gains
        var recentStart = today.AddDays(-30);
        var recentMax = history.Where(s => s.StartTime.Date >= recentStart)
            .SelectMany(s => s.Exercises).Where(e => e.Exercise != null && e.Sets.Any())
            .GroupBy(e => e.Exercise!.Name)
            .ToDictionary(g => g.Key, g => g.SelectMany(e => e.Sets).Max(s => s.Weight));
        var prevMax = history.Where(s => s.StartTime.Date >= today.AddDays(-60) && s.StartTime.Date < recentStart)
            .SelectMany(s => s.Exercises).Where(e => e.Exercise != null && e.Sets.Any())
            .GroupBy(e => e.Exercise!.Name)
            .ToDictionary(g => g.Key, g => g.SelectMany(e => e.Sets).Max(s => s.Weight));

        string? bestEx = null; double bestGain = 0;
        foreach (var (name, max) in recentMax)
        {
            prevMax.TryGetValue(name, out var prev);
            var gain = max - prev;
            if (gain > bestGain) { bestGain = gain; bestEx = name; }
        }

        if (bestEx != null && bestGain > 0) { InsightLabel.Text = $"💪 You increased {bestEx} by {bestGain:0.#} kg in the last month!"; return; }
        if (workoutsThisWeek > 0) { InsightLabel.Text = $"🔥 {workoutsThisWeek} workout{(workoutsThisWeek == 1 ? "" : "s")} completed this week. Keep it up!"; return; }
        var last = history.OrderByDescending(s => s.StartTime).FirstOrDefault();
        if (last != null) { var days = (today - last.StartTime.Date).Days; InsightLabel.Text = $"⏱️ Last workout was {days} day{(days == 1 ? "" : "s")} ago — a short session today keeps momentum."; return; }
        InsightLabel.Text = "🌟 Start your first workout to unlock personalized insights.";
    }

    private async void OnStartWorkoutClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//WorkoutPage");
    }

    private void OnSkipUpperClicked(object sender, EventArgs e)
    {
        _database?.SkipUpperBodyRotation();
        RenderTodayPlan();
    }

    private void OnSkipLowerClicked(object sender, EventArgs e)
    {
        _database?.SkipLowerBodyRotation();
        RenderTodayPlan();
    }

    private async Task TryPullAndRefreshAsync()
    {
        try
        {
            if (_database == null) return;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            await WorkoutCloudSyncService.TryPullAndApplyAsync(_database, cts.Token).ConfigureAwait(false);
            MainThread.BeginInvokeOnMainThread(LoadTodayActivity);
        }
        catch { }
    }

    private static string GetDisplayFirstName(string name)
    {
        var trimmed = name.Trim();
        var i = trimmed.IndexOf(' ');
        var first = i > 0 ? trimmed[..i] : trimmed;
        if (first.Length == 0) return "there";
        return char.ToUpperInvariant(first[0]) + first[1..].ToLowerInvariant();
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        int offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }
}
