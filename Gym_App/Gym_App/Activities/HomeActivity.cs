using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using AndroidX.Core.Content;
using Gym_App.Data;
using Gym_App.Models;
using Google.Android.Material.Dialog;

namespace Gym_App.Activities
{
    [Activity(Label = "Home")]
    public class HomeActivity : Activity
    {
        private static bool _hasShownWelcomePromptThisLaunch;
        private GymDatabase? _database;
        private TextView? _statDurationText;
        private TextView? _statSetsText;
        private TextView? _statCaloriesText;
        private TextView? _contextualStatsText;
        private TextView? _weeklyProgressText;
        private ProgressBar? _weeklyProgressBar;
        private LinearLayout? _weeklyProgressChart;
        private TextView? _insightText;
        private LinearLayout? _todayExercisesContainer;
        private ImageView? _noRecordsIcon;
        private TextView? _noRecordsText;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_home);

            _database = new GymDatabase();

            _statDurationText = FindViewById<TextView>(Resource.Id.statDurationText);
            _statSetsText = FindViewById<TextView>(Resource.Id.statSetsText);
            _statCaloriesText = FindViewById<TextView>(Resource.Id.statCaloriesText);
            _contextualStatsText = FindViewById<TextView>(Resource.Id.contextualStatsText);
            _weeklyProgressText = FindViewById<TextView>(Resource.Id.weeklyProgressText);
            _weeklyProgressBar = FindViewById<ProgressBar>(Resource.Id.weeklyProgressBar);
            _weeklyProgressChart = FindViewById<LinearLayout>(Resource.Id.weeklyProgressChart);
            _insightText = FindViewById<TextView>(Resource.Id.insightText);
            _todayExercisesContainer = FindViewById<LinearLayout>(Resource.Id.todayExercisesContainer);
            _noRecordsIcon = FindViewById<ImageView>(Resource.Id.noRecordsIcon);
            _noRecordsText = FindViewById<TextView>(Resource.Id.noRecordsText);

            var startWorkoutButton = FindViewById(Resource.Id.startWorkoutHomeButton);
            var homeTab = FindViewById<LinearLayout>(Resource.Id.homeTab);
            var diaryTab = FindViewById<LinearLayout>(Resource.Id.diaryTab);
            var workoutTab = FindViewById<LinearLayout>(Resource.Id.workoutTab);
            var profileTab = FindViewById<LinearLayout>(Resource.Id.profileTab);

            var profilePrefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            var name = profilePrefs?.GetString("full_name", "User") ?? "User";
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "User";
            }
            if (!_hasShownWelcomePromptThisLaunch)
            {
                Toast.MakeText(this, $"Welcome back, {name}", ToastLength.Short)?.Show();
                _hasShownWelcomePromptThisLaunch = true;
            }
            ShowFirstTimeOnboarding();

            if (homeTab != null) homeTab.Selected = true;
            if (diaryTab != null) diaryTab.Selected = false;
            if (workoutTab != null) workoutTab.Selected = false;
            if (profileTab != null) profileTab.Selected = false;
            UpdateBottomNavLabelStyles();

            if (startWorkoutButton != null)
            {
                startWorkoutButton.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(WorkoutActivity)));
                };
            }

            BindQuickActionButtons();

            if (diaryTab != null)
            {
                diaryTab.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(HistoryActivity)));
                };
            }

            if (workoutTab != null)
            {
                workoutTab.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(WorkoutActivity)));
                };
            }

            if (profileTab != null)
            {
                profileTab.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(ProfileActivity)));
                };
            }

            if (homeTab != null)
            {
                homeTab.Click += (s, e) => { };
            }
        }

        private void UpdateBottomNavLabelStyles()
        {
            SetTabLabelStyle(Resource.Id.homeTabLabel, FindViewById<LinearLayout>(Resource.Id.homeTab)?.Selected == true);
            SetTabLabelStyle(Resource.Id.diaryTabLabel, FindViewById<LinearLayout>(Resource.Id.diaryTab)?.Selected == true);
            SetTabLabelStyle(Resource.Id.workoutTabLabel, FindViewById<LinearLayout>(Resource.Id.workoutTab)?.Selected == true);
            SetTabLabelStyle(Resource.Id.profileTabLabel, FindViewById<LinearLayout>(Resource.Id.profileTab)?.Selected == true);
        }

        private void SetTabLabelStyle(int labelId, bool isSelected)
        {
            var label = FindViewById<TextView>(labelId);
            if (label == null)
                return;

            label.SetTypeface(null, isSelected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        }

        protected override void OnResume()
        {
            base.OnResume();
            LoadTodayRecords();
        }

        private void LoadTodayRecords()
        {
            if (_database == null || _todayExercisesContainer == null)
                return;

            DateTime today = DateTime.Today;
            var history = _database.GetWorkoutHistory(500);

            var completedToday = history
                .Where(s => s.StartTime.Date == today)
                .ToList();

            var currentWorkout = _database.GetCurrentWorkout();
            if (currentWorkout != null && currentWorkout.StartTime.Date == today)
            {
                completedToday.Add(currentWorkout);
            }

            var insightSessions = history.ToList();
            if (currentWorkout != null
                && currentWorkout.Exercises.Any(exercise => exercise.Sets.Count > 0)
                && insightSessions.All(session => session.Id != currentWorkout.Id))
            {
                insightSessions.Add(currentWorkout);
            }

            int totalSets = 0;
            double calories = 0;
            TimeSpan totalDuration = TimeSpan.Zero;

            foreach (var session in completedToday)
            {
                totalDuration += session.Duration;
                foreach (var exercise in session.Exercises)
                {
                    totalSets += exercise.Sets.Count;
                    calories += exercise.Sets.Sum(set => set.Reps * set.Weight * 0.1);
                }
            }

            int weeklyGoal = GetWeeklyGoal();
            int workoutsThisWeek = CountWorkoutsThisWeek(insightSessions);
            UpdateProgressWidget(workoutsThisWeek, weeklyGoal);
            UpdateWeeklyProgressChart(insightSessions);
            UpdateTopStats(totalDuration, totalSets, calories, insightSessions);
            UpdateInsightText(insightSessions, workoutsThisWeek);

            _todayExercisesContainer.RemoveAllViews();

            var exerciseRecords = completedToday
                .SelectMany(session => session.Exercises)
                .Where(exercise => exercise.Exercise != null)
                .ToList();

            if (exerciseRecords.Count == 0)
            {
                RenderSmartEmptyState(completedToday);
                return;
            }

            foreach (var record in exerciseRecords)
            {
                if (record.Exercise == null)
                    continue;

                var item = new TextView(this)
                {
                    Text = $"• {record.Exercise.Name}  ({record.Sets.Count} sets)",
                    TextSize = 14
                };
                item.SetTextColor(new Android.Graphics.Color(ContextCompat.GetColor(this, Resource.Color.color_text_primary)));
                item.SetPadding(0, 4, 0, 4);
                _todayExercisesContainer.AddView(item);
            }
        }

        private void UpdateTopStats(TimeSpan todayDuration, int todaySets, double todayCalories, List<WorkoutSession> history)
        {
            bool hasTodayData = todayDuration > TimeSpan.Zero || todaySets > 0 || todayCalories > 0;

            if (hasTodayData)
            {
                if (_statDurationText != null)
                    _statDurationText.Text = $"{todayDuration.Hours:D2}:{todayDuration.Minutes:D2}";
                if (_statSetsText != null)
                    _statSetsText.Text = todaySets.ToString();
                if (_statCaloriesText != null)
                    _statCaloriesText.Text = $"{Math.Round(todayCalories)} kcal";

                var weekStart = GetWeekStart(DateTime.Today);
                var weekSessions = history.Where(s => s.StartTime.Date >= weekStart).ToList();
                if (weekSessions.Count > 0)
                {
                    int avgDurationMin = (int)Math.Round(weekSessions.Average(s => s.Duration.TotalMinutes));
                    int avgSets = (int)Math.Round(weekSessions.Average(GetSessionSetCount));
                    int avgCalories = (int)Math.Round(weekSessions.Average(GetSessionCalories));
                    if (_contextualStatsText != null)
                    {
                        _contextualStatsText.Text = $"Weekly avg: {avgDurationMin} mins · {avgSets} sets · {avgCalories} kcal";
                    }
                }
                else if (_contextualStatsText != null)
                {
                    _contextualStatsText.Text = "Great start today — keep it going.";
                }

                return;
            }

            var lastSession = history.OrderByDescending(s => s.StartTime).FirstOrDefault();
            if (lastSession != null)
            {
                int lastDurationMin = (int)Math.Round(lastSession.Duration.TotalMinutes);
                int lastSets = GetSessionSetCount(lastSession);
                int lastCalories = (int)Math.Round(GetSessionCalories(lastSession));

                if (_statDurationText != null)
                    _statDurationText.Text = $"{lastDurationMin / 60:D2}:{lastDurationMin % 60:D2}";
                if (_statSetsText != null)
                    _statSetsText.Text = lastSets.ToString();
                if (_statCaloriesText != null)
                    _statCaloriesText.Text = $"{lastCalories} kcal";
                if (_contextualStatsText != null)
                    _contextualStatsText.Text = $"Last session: {lastDurationMin} mins · {lastSets} sets · {lastCalories} kcal";

                return;
            }

            if (_statDurationText != null)
                _statDurationText.Text = "00:00";
            if (_statSetsText != null)
                _statSetsText.Text = "0";
            if (_statCaloriesText != null)
                _statCaloriesText.Text = "0 kcal";
            if (_contextualStatsText != null)
                _contextualStatsText.Text = "No previous workouts yet — start your first session.";
        }

        private void UpdateProgressWidget(int workoutsThisWeek, int weeklyGoal)
        {
            if (_weeklyProgressText != null)
            {
                if (workoutsThisWeek == 0)
                {
                    _weeklyProgressText.Text = "Let's start your first workout today!";
                }
                else
                {
                    _weeklyProgressText.Text = $"You've completed {workoutsThisWeek}/{weeklyGoal} workouts this week";
                }
            }

            if (_weeklyProgressBar != null)
            {
                int progress = weeklyGoal <= 0 ? 0 : (int)Math.Round((double)workoutsThisWeek / weeklyGoal * 100);
                _weeklyProgressBar.Progress = Math.Max(0, Math.Min(progress, 100));
            }
        }

        private void UpdateWeeklyProgressChart(List<WorkoutSession> sessions)
        {
            if (_weeklyProgressChart == null)
                return;

            _weeklyProgressChart.RemoveAllViews();

            var weekStart = GetWeekStart(DateTime.Today);
            var labels = new[] { "M", "T", "W", "T", "F", "S", "S" };
            var dailyCounts = new int[7];
            int maxCount = 1;

            for (int i = 0; i < 7; i++)
            {
                var day = weekStart.AddDays(i).Date;
                dailyCounts[i] = sessions
                    .Where(session => session.StartTime.Date == day)
                    .Where(session => session.Exercises.Any(exercise => exercise.Sets.Count > 0))
                    .Select(session => session.Id)
                    .Distinct()
                    .Count();

                if (dailyCounts[i] > maxCount)
                    maxCount = dailyCounts[i];
            }

            int barMaxHeight = DpToPx(56);
            int barWidth = DpToPx(12);

            for (int i = 0; i < 7; i++)
            {
                var column = new LinearLayout(this)
                {
                    Orientation = Orientation.Vertical
                };
                column.SetGravity(GravityFlags.CenterHorizontal);
                column.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);

                var track = new LinearLayout(this)
                {
                    Orientation = Orientation.Vertical
                };
                track.SetGravity(GravityFlags.Bottom);
                track.LayoutParameters = new LinearLayout.LayoutParams(barWidth, barMaxHeight);
                track.SetPadding(0, 0, 0, 0);

                var trackDrawable = new GradientDrawable();
                trackDrawable.SetColor(new Color(ContextCompat.GetColor(this, Resource.Color.md_theme_surfaceVariant)));
                trackDrawable.SetCornerRadius(DpToPx(6));
                track.Background = trackDrawable;

                int barHeight = dailyCounts[i] == 0
                    ? DpToPx(2)
                    : Math.Max(DpToPx(8), (int)Math.Round(barMaxHeight * (dailyCounts[i] / (double)maxCount)));

                var fill = new View(this);
                fill.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, barHeight);

                var fillDrawable = new GradientDrawable();
                fillDrawable.SetColor(new Color(ContextCompat.GetColor(this, Resource.Color.color_primary)));
                fillDrawable.SetCornerRadius(DpToPx(6));
                fill.Background = fillDrawable;

                track.AddView(fill);

                var label = new TextView(this)
                {
                    Text = labels[i],
                    TextSize = 11
                };
                label.SetTextColor(new Color(ContextCompat.GetColor(this, Resource.Color.color_text_secondary)));
                label.SetPadding(0, DpToPx(6), 0, 0);

                column.AddView(track);
                column.AddView(label);
                _weeklyProgressChart.AddView(column);
            }
        }

        private int DpToPx(int dp)
        {
            return (int)Math.Round(dp * Resources.DisplayMetrics.Density);
        }

        private void UpdateInsightText(List<WorkoutSession> history, int workoutsThisWeek)
        {
            if (_insightText == null)
                return;

            var today = DateTime.Today;
            var recentStart = today.AddDays(-30);
            var previousStart = today.AddDays(-60);

            var recent = history.Where(s => s.StartTime.Date >= recentStart).ToList();
            var previous = history.Where(s => s.StartTime.Date >= previousStart && s.StartTime.Date < recentStart).ToList();

            var recentMaxByExercise = GetMaxWeightByExercise(recent);
            var previousMaxByExercise = GetMaxWeightByExercise(previous);

            string? bestExercise = null;
            double bestGain = 0;

            foreach (var (exerciseName, recentMax) in recentMaxByExercise)
            {
                previousMaxByExercise.TryGetValue(exerciseName, out var previousMax);
                var gain = recentMax - previousMax;
                if (gain > bestGain)
                {
                    bestGain = gain;
                    bestExercise = exerciseName;
                }
            }

            if (bestExercise != null && bestGain > 0)
            {
                _insightText.Text = $"💪 You increased {bestExercise} by {bestGain:0.#} kg in the last month!";
                return;
            }

            if (workoutsThisWeek > 0)
            {
                _insightText.Text = $"🔥 Consistency check: {workoutsThisWeek} workout{(workoutsThisWeek == 1 ? string.Empty : "s")} completed this week.";
                return;
            }

            var lastSession = history.OrderByDescending(s => s.StartTime).FirstOrDefault();
            if (lastSession != null)
            {
                var daysAgo = Math.Max(0, (today - lastSession.StartTime.Date).Days);
                _insightText.Text = $"⏱️ Your last workout was {daysAgo} day{(daysAgo == 1 ? string.Empty : "s")} ago — a short session today keeps momentum.";
                return;
            }

            _insightText.Text = "🌟 Start your first workout to unlock personalized trends and insights.";
        }

        private Dictionary<string, double> GetMaxWeightByExercise(List<WorkoutSession> sessions)
        {
            return sessions
                .SelectMany(s => s.Exercises)
                .Where(e => e.Exercise != null && e.Sets.Count > 0)
                .GroupBy(e => e.Exercise!.Name)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(e => e.Sets).Max(set => set.Weight)
                );
        }

        private int GetWeeklyGoal()
        {
            var prefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            if (prefs == null)
                return 4;

            var weeklyGoal = prefs.GetInt("weekly_goal", 4);
            return weeklyGoal > 0 ? weeklyGoal : 4;
        }

        private int CountWorkoutsThisWeek(List<WorkoutSession> sessions)
        {
            var weekStart = GetWeekStart(DateTime.Today);
            return sessions
                .Where(session => session.StartTime.Date >= weekStart)
                .Count(session => session.Exercises.Any(exercise => exercise.Sets.Count > 0));
        }

        private static DateTime GetWeekStart(DateTime date)
        {
            int startOffset = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-startOffset);
        }

        private static int GetSessionSetCount(WorkoutSession session)
        {
            return session.Exercises.Sum(exercise => exercise.Sets.Count);
        }

        private static double GetSessionCalories(WorkoutSession session)
        {
            return session.Exercises
                .SelectMany(exercise => exercise.Sets)
                .Sum(set => set.Reps * set.Weight * 0.1);
        }

        private void RenderSmartEmptyState(List<WorkoutSession> completedToday)
        {
            if (_database == null || _todayExercisesContainer == null)
                return;

            var history = _database.GetWorkoutHistory(200);
            var historyExercises = history
                .SelectMany(session => session.Exercises)
                .Where(exercise => exercise.Exercise != null)
                .ToList();

            var mostFrequentExercise = historyExercises
                .GroupBy(exercise => exercise.Exercise!.Id)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.First().Exercise!.Name)
                .Select(group => group.First().Exercise)
                .FirstOrDefault();

            var topThree = historyExercises
                .GroupBy(exercise => exercise.Exercise!.Name)
                .OrderByDescending(group => group.Count())
                .Take(3)
                .Select(group => group.Key)
                .ToList();

            var suggestedTitle = new TextView(this)
            {
                Text = "Suggested workout"
            };
            suggestedTitle.SetTextColor(new Android.Graphics.Color(ContextCompat.GetColor(this, Resource.Color.color_text_secondary)));
            suggestedTitle.SetTypeface(null, TypefaceStyle.Bold);
            suggestedTitle.TextSize = 13;
            suggestedTitle.SetPadding(0, 0, 0, 4);
            _todayExercisesContainer.AddView(suggestedTitle);

            var suggestedText = new TextView(this)
            {
                Text = topThree.Count > 0
                    ? $"Focus today: {string.Join(" · ", topThree)}"
                    : "Focus today: Full Body Starter (Squat · Push-up · Row)"
            };
            suggestedText.SetTextColor(new Android.Graphics.Color(ContextCompat.GetColor(this, Resource.Color.color_text_primary)));
            suggestedText.TextSize = 14;
            _todayExercisesContainer.AddView(suggestedText);

            var quickStartTitle = new TextView(this)
            {
                Text = "Quick start"
            };
            quickStartTitle.SetTextColor(new Android.Graphics.Color(ContextCompat.GetColor(this, Resource.Color.color_text_secondary)));
            quickStartTitle.SetTypeface(null, TypefaceStyle.Bold);
            quickStartTitle.TextSize = 13;
            quickStartTitle.SetPadding(0, 16, 0, 6);
            _todayExercisesContainer.AddView(quickStartTitle);

            var quickStartButton = new Button(this)
            {
                Text = mostFrequentExercise != null ? $"Start {mostFrequentExercise.Name}" : "Start Most Frequent Exercise"
            };
            quickStartButton.SetTextColor(new Android.Graphics.Color(ContextCompat.GetColor(this, Resource.Color.color_on_primary)));
            quickStartButton.SetBackgroundResource(Resource.Drawable.bg_button_primary);
            quickStartButton.SetPadding(20, 14, 20, 14);
            quickStartButton.Click += (s, e) =>
            {
                if (mostFrequentExercise == null)
                {
                    StartActivity(new Intent(this, typeof(WorkoutActivity)));
                    return;
                }

                StartQuickExercise(mostFrequentExercise);
            };
            _todayExercisesContainer.AddView(quickStartButton);

            int weeklyGoal = 4;
            var profilePrefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            if (profilePrefs != null)
            {
                weeklyGoal = profilePrefs.GetInt("weekly_goal", 4);
                if (weeklyGoal <= 0)
                {
                    weeklyGoal = 4;
                }
            }

            var today = DateTime.Today;
            int startOffset = ((int)today.DayOfWeek + 6) % 7;
            var weekStart = today.AddDays(-startOffset);
            int workoutsThisWeek = _database.GetWorkoutHistory(200).Count(session => session.StartTime.Date >= weekStart);
            int workoutsLeft = Math.Max(0, weeklyGoal - workoutsThisWeek);

            var snapshotTitle = new TextView(this)
            {
                Text = "Progress snapshot"
            };
            snapshotTitle.SetTextColor(new Android.Graphics.Color(ContextCompat.GetColor(this, Resource.Color.color_text_secondary)));
            snapshotTitle.SetTypeface(null, TypefaceStyle.Bold);
            snapshotTitle.TextSize = 13;
            snapshotTitle.SetPadding(0, 16, 0, 4);
            _todayExercisesContainer.AddView(snapshotTitle);

            var snapshotText = new TextView(this)
            {
                Text = workoutsLeft > 0
                    ? $"You’re {workoutsLeft} workout{(workoutsLeft == 1 ? string.Empty : "s")} away from your weekly goal!"
                    : "Weekly goal achieved — great consistency!"
            };
            snapshotText.SetTextColor(new Android.Graphics.Color(ContextCompat.GetColor(this, Resource.Color.color_text_primary)));
            snapshotText.TextSize = 14;
            _todayExercisesContainer.AddView(snapshotText);

            if (historyExercises.Count == 0)
            {
                var firstStepText = new TextView(this)
                {
                    Text = GetString(Resource.String.home_empty_first_step)
                };
                firstStepText.SetTextColor(new Android.Graphics.Color(ContextCompat.GetColor(this, Resource.Color.color_text_secondary)));
                firstStepText.TextSize = 13;
                firstStepText.SetPadding(0, 16, 0, 8);
                _todayExercisesContainer.AddView(firstStepText);

                var browseButton = new Button(this)
                {
                    Text = GetString(Resource.String.home_browse_exercises)
                };
                browseButton.SetTextColor(new Android.Graphics.Color(ContextCompat.GetColor(this, Resource.Color.color_on_primary)));
                browseButton.SetBackgroundResource(Resource.Drawable.bg_button_primary);
                browseButton.SetPadding(20, 14, 20, 14);
                browseButton.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(ExerciseLibraryActivity)));
                };
                _todayExercisesContainer.AddView(browseButton);
            }
        }

        private void BindQuickActionButtons()
        {
            var quickLogButton = FindViewById<ImageView>(Resource.Id.quickLogButton);
            var historyButton = FindViewById<ImageView>(Resource.Id.historyButton);
            var plansButton = FindViewById<ImageView>(Resource.Id.plansButton);
            var bodyStatsButton = FindViewById<ImageView>(Resource.Id.bodyStatsButton);

            if (quickLogButton != null)
            {
                quickLogButton.Click += (s, e) => ShowQuickAddDialog();
            }

            if (historyButton != null)
            {
                historyButton.Click += (s, e) => StartActivity(new Intent(this, typeof(HistoryActivity)));
            }

            if (plansButton != null)
            {
                plansButton.Click += (s, e) => StartActivity(new Intent(this, typeof(WorkoutActivity)));
            }

            if (bodyStatsButton != null)
            {
                bodyStatsButton.Click += (s, e) => StartActivity(new Intent(this, typeof(ProfileActivity)));
            }
        }

        private void ShowFirstTimeOnboarding()
        {
            var prefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            if (prefs == null)
                return;

            var hasSeenOnboarding = prefs.GetBoolean("home_onboarding_seen", false);
            if (hasSeenOnboarding)
                return;

            var dialog = new MaterialAlertDialogBuilder(this)
                .SetTitle(GetString(Resource.String.home_onboarding_title))
                .SetMessage(GetString(Resource.String.home_onboarding_message))
                .SetPositiveButton(GetString(Resource.String.home_onboarding_cta), (s, e) => { })
                .Create();

            dialog.Show();
            prefs.Edit()?.PutBoolean("home_onboarding_seen", true)?.Apply();
        }

        private void StartQuickExercise(Exercise exercise)
        {
            if (_database == null)
                return;

            var session = _database.CreateWorkoutSession("Quick Start Workout");
            _database.AddExerciseToWorkout(session.Id, exercise.Id);

            var intent = new Intent(this, typeof(WorkoutActivity));
            intent.PutExtra("workoutId", session.Id);
            StartActivity(intent);
        }

        private void ShowQuickAddDialog()
        {
            if (_database == null)
                return;

            var exercises = _database.GetAllExercises();
            if (exercises.Count == 0)
            {
                Toast.MakeText(this, "No exercises available", ToastLength.Short)?.Show();
                return;
            }

            var names = exercises.Select(e => e.Name).ToArray();

            var picker = new MaterialAlertDialogBuilder(this);
            picker.SetTitle("Select Exercise");
            picker.SetItems(names, (s, args) =>
            {
                var selected = exercises[args.Which];
                ShowSetEntryDialog(selected);
            });
            picker.Show();
        }

        private void ShowSetEntryDialog(Exercise exercise)
        {
            if (_database == null)
                return;

            var dialog = new MaterialAlertDialogBuilder(this);
            dialog.SetTitle($"Log {exercise.Name}");

            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            layout.SetPadding(32, 16, 32, 16);

            var repsInput = new EditText(this)
            {
                Hint = "Reps",
                InputType = Android.Text.InputTypes.ClassNumber
            };
            var weightInput = new EditText(this)
            {
                Hint = "Weight (kg)",
                InputType = Android.Text.InputTypes.ClassNumber | Android.Text.InputTypes.NumberFlagDecimal
            };

            layout.AddView(repsInput);
            layout.AddView(weightInput);
            dialog.SetView(layout);

            dialog.SetPositiveButton("Save", (s, e) =>
            {
                if (!int.TryParse(repsInput.Text, out int reps) || reps <= 0)
                {
                    Toast.MakeText(this, "Enter valid reps", ToastLength.Short)?.Show();
                    return;
                }

                if (!double.TryParse(weightInput.Text, out double weight) || weight < 0)
                {
                    Toast.MakeText(this, "Enter valid weight", ToastLength.Short)?.Show();
                    return;
                }

                var session = _database.CreateWorkoutSession("Quick Log Workout");
                _database.AddExerciseToWorkout(session.Id, exercise.Id);

                var refreshed = _database.GetWorkoutSession(session.Id);
                var workoutExercise = refreshed?.Exercises.LastOrDefault();
                if (workoutExercise != null)
                {
                    _database.AddSetToExercise(workoutExercise.Id, reps, weight, 1);
                    _database.CompleteWorkout(session.Id);
                    Toast.MakeText(this, "Exercise recorded", ToastLength.Short)?.Show();
                    LoadTodayRecords();
                }
            });

            dialog.SetNegativeButton("Cancel", (s, e) => { });
            dialog.Show();
        }
    }
}
