using JeanetixMAUI.Data;
using JeanetixMAUI.Models;

namespace JeanetixMAUI.Pages;

public partial class HistoryPage : ContentPage
{
    private GymDatabase? _database;
    private enum Range { Day, Week, Month }
    private Range _range = Range.Day;

    public HistoryPage()
    {
        InitializeComponent();
        DayTabBorder.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => SetRange(Range.Day)) });
        WeekTabBorder.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => SetRange(Range.Week)) });
        MonthTabBorder.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => SetRange(Range.Month)) });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _database ??= new GymDatabase();
        LoadHistory();
        _ = TryPullAndRefreshAsync();
    }

    private void SetRange(Range r)
    {
        _range = r;
        void Style(Border b, Label l, bool active)
        {
            b.BackgroundColor = active ? Color.FromArgb("#D4AF37") : Colors.Transparent;
            l.TextColor = active ? Color.FromArgb("#1F1600") : Color.FromArgb("#9CA3AF");
            l.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
        }
        Style(DayTabBorder, DayTab, r == Range.Day);
        Style(WeekTabBorder, WeekTab, r == Range.Week);
        Style(MonthTabBorder, MonthTab, r == Range.Month);
        LoadHistory();
    }

    private void LoadHistory()
    {
        HistoryContainer.Children.Clear();
        var history = _database?.GetWorkoutHistory(200) ?? new List<WorkoutSession>();

        var now = DateTime.Today;
        var filtered = _range switch
        {
            Range.Day => history.Where(s => s.StartTime.Date == now).ToList(),
            Range.Week => history.Where(s => s.StartTime.Date >= now.AddDays(-7)).ToList(),
            Range.Month => history.Where(s => s.StartTime.Date >= now.AddDays(-30)).ToList(),
            _ => history
        };

        RangeLabel.Text = _range switch { Range.Day => "Today", Range.Week => "Last 7 days", Range.Month => "Last 30 days", _ => "" };
        EmptyLabel.IsVisible = filtered.Count == 0;

        foreach (var session in filtered)
        {
            var totalSets = session.Exercises.Sum(e => e.Sets.Count);
            var totalReps = session.Exercises.SelectMany(e => e.Sets).Sum(s => s.Reps);
            var duration = session.Duration;
            var durationStr = duration.TotalMinutes < 1 ? "< 1 min" : $"{(int)duration.TotalMinutes} min";

            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#24242C"),
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(16)
            };

            var layout = new VerticalStackLayout { Spacing = 8 };

            // Header
            var header = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
            header.Add(new Label { Text = session.Name, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Colors.White }, 0, 0);
            header.Add(new Label { Text = session.StartTime.ToString("MMM d"), FontSize = 12, TextColor = Color.FromArgb("#6B7280"), VerticalOptions = LayoutOptions.Center }, 1, 0);
            layout.Add(header);

            // Stats row
            var stats = new HorizontalStackLayout { Spacing = 16 };
            stats.Add(new Label { Text = $"⏱ {durationStr}", FontSize = 13, TextColor = Color.FromArgb("#9CA3AF") });
            stats.Add(new Label { Text = $"💪 {totalSets} sets", FontSize = 13, TextColor = Color.FromArgb("#9CA3AF") });
            stats.Add(new Label { Text = $"🔁 {totalReps} reps", FontSize = 13, TextColor = Color.FromArgb("#9CA3AF") });
            layout.Add(stats);

            // Exercises
            foreach (var ex in session.Exercises.Take(4))
            {
                var maxW = ex.Sets.Count == 0 ? 0 : ex.Sets.Max(s => s.Weight);
                var unit = ex.Sets.FirstOrDefault()?.WeightUnit ?? "kg";
                var exRow = new Label
                {
                    Text = $"• {ex.Exercise?.Name ?? "?"} — {ex.Sets.Count} sets · {maxW:0.#} {unit} max",
                    FontSize = 13, TextColor = Color.FromArgb("#D1D5DB")
                };
                layout.Add(exRow);
            }
            if (session.Exercises.Count > 4)
                layout.Add(new Label { Text = $"  + {session.Exercises.Count - 4} more...", FontSize = 12, TextColor = Color.FromArgb("#6B7280") });

            // Delete button
            var deleteBtn = new Button { Text = "Delete", FontSize = 12, TextColor = Color.FromArgb("#EF4444"), BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.End, Padding = new Thickness(0) };
            var capturedId = session.Id;
            deleteBtn.Clicked += async (_, _) =>
            {
                bool confirm = await DisplayAlert("Delete", $"Delete \"{session.Name}\"?", "Delete", "Cancel");
                if (!confirm) return;
                _database?.DeleteWorkoutSession(capturedId);
                LoadHistory();
            };
            layout.Add(deleteBtn);

            card.Content = layout;
            HistoryContainer.Children.Add(card);
        }
    }

    private async Task TryPullAndRefreshAsync()
    {
        try
        {
            if (_database == null) return;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            await WorkoutCloudSyncService.TryPullAndApplyAsync(_database, cts.Token).ConfigureAwait(false);
            MainThread.BeginInvokeOnMainThread(LoadHistory);
        }
        catch { }
    }
}
