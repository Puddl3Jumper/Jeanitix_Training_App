using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Locations;
using Android.Runtime;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Gym_App;
using Gym_App.Data;
using Gym_App.Models;
using Gym_App.Services;
using Google.Android.Material.Dialog;

namespace Gym_App.Activities
{
    [Activity(Label = "Home")]
    public class HomeActivity : Activity
    {
        private static bool _hasShownWelcomePromptThisLaunch;
        private GymDatabase? _database;
        private TextView? _todayPlanSummaryText;
        private TextView? _todayPlanMetaText;
        private TextView? _todayLastWorkoutText;
        private Button? _startWorkoutHomeButton;
        private LinearLayout? _weeklyProgressChart;

        private TextView? _weeklyAvgValueText;
        private TextView? _weeklyAvgLabelText;
        private TextView? _weeklyGoalSummaryText;
        private TextView? _insightText;
        private TextView? _homeWelcomeText;
        private TextView? _homeGreetingSubtext;
        private LinearLayout? _todayExercisesContainer;
        private ImageView? _noRecordsIcon;
        private TextView? _noRecordsText;
        private LocationManager? _locationManager;
        private readonly SemaphoreSlim _cloudPullLock = new(1, 1);
        private bool? _lastKnownAtGym;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_home);

            _database = new GymDatabase();

            _todayPlanSummaryText = FindViewById<TextView>(Resource.Id.todayPlanSummaryText);
            _todayPlanMetaText = FindViewById<TextView>(Resource.Id.todayPlanMetaText);
            _todayLastWorkoutText = FindViewById<TextView>(Resource.Id.todayLastWorkoutText);
            _weeklyProgressChart = FindViewById<LinearLayout>(Resource.Id.weeklyProgressChart);
            _weeklyAvgValueText = FindViewById<TextView>(Resource.Id.weeklyAvgValueText);
            _weeklyAvgLabelText = FindViewById<TextView>(Resource.Id.weeklyAvgLabelText);
            _weeklyGoalSummaryText = FindViewById<TextView>(Resource.Id.weeklyGoalSummaryText);
            _insightText = FindViewById<TextView>(Resource.Id.insightText);
            _homeWelcomeText = FindViewById<TextView>(Resource.Id.homeWelcomeText);
            _homeGreetingSubtext = FindViewById<TextView>(Resource.Id.homeGreetingSubtext);
            _todayExercisesContainer = FindViewById<LinearLayout>(Resource.Id.todayExercisesContainer);
            _noRecordsIcon = FindViewById<ImageView>(Resource.Id.noRecordsIcon);
            _noRecordsText = FindViewById<TextView>(Resource.Id.noRecordsText);

            _startWorkoutHomeButton = FindViewById<Button>(Resource.Id.startWorkoutHomeButton);
            var startWorkoutButton = _startWorkoutHomeButton;
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

            UpdateWelcomeHeader(name, isAtGym: null);
            RequestLocationPermissionAndRefreshGreeting(name);

            if (!_hasShownWelcomePromptThisLaunch)
            {
                Toast.MakeText(this, $"Welcome back, {name}", ToastLength.Short)?.Show();
                _hasShownWelcomePromptThisLaunch = true;
            }
            ShowFirstTimeOnboarding();
            ShowWhatsNewAfterUpgrade();
            RenderTodayTrainingPlan();

            if (homeTab != null) homeTab.Selected = true;
            if (diaryTab != null) diaryTab.Selected = false;
            if (workoutTab != null) workoutTab.Selected = false;
            if (profileTab != null) profileTab.Selected = false;
            UpdateBottomNavLabelStyles();

            if (startWorkoutButton != null)
            {
                startWorkoutButton.Click += (s, e) =>
                {
                    var currentWorkout = _database?.GetCurrentWorkout();
                    var intent = WorkoutActivity.CreateIntent(
                        this,
                        startWorkoutTimer: true,
                        workoutId: currentWorkout?.Id ?? -1);
                    StartActivity(intent);
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
            var profilePrefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            var name = profilePrefs?.GetString("full_name", "User") ?? "User";
            RequestLocationPermissionAndRefreshGreeting(name);
            RenderTodayTrainingPlan();
            LoadTodayRecords();
            _ = TryPullWorkoutsAndRefreshAsync();
        }

        private void UpdateWelcomeHeader(string? rawName, bool? isAtGym = null)
        {
            if (_homeWelcomeText == null)
                return;

            var name = rawName?.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "User", StringComparison.OrdinalIgnoreCase))
            {
                _homeWelcomeText.Text = GetString(Resource.String.home_greeting_hi_placeholder);
                if (_homeGreetingSubtext != null)
                    _homeGreetingSubtext.Text = GetString(Resource.String.home_greeting_ready);
                return;
            }

            var firstName = GetDisplayFirstName(name);
            if (isAtGym == true)
            {
                _homeWelcomeText.Text = GetString(Resource.String.home_greeting_hi, firstName);
                if (_homeGreetingSubtext != null)
                    _homeGreetingSubtext.Text = GetString(Resource.String.home_greeting_at_gym);
                return;
            }

            _homeWelcomeText.Text = GetString(Resource.String.home_greeting_hi, firstName);
            if (_homeGreetingSubtext != null)
                _homeGreetingSubtext.Text = GetString(Resource.String.home_greeting_ready);
        }

        private static string GetDisplayFirstName(string name)
        {
            var trimmed = name.Trim();
            var firstSpace = trimmed.IndexOf(' ');
            var firstName = firstSpace > 0 ? trimmed[..firstSpace] : trimmed;
            if (firstName.Length == 0)
                return "there";

            return char.ToUpperInvariant(firstName[0]) + firstName[1..].ToLowerInvariant();
        }

        private void ApplyGymProximityState(string name, bool atGym)
        {
            if (atGym && _lastKnownAtGym != true)
            {
                var firstName = GetDisplayFirstName(name);
                Toast.MakeText(this, $"Hi {firstName}, You are here", ToastLength.Long)?.Show();
            }

            _lastKnownAtGym = atGym;
            UpdateWelcomeHeader(name, atGym);
        }

        private void RequestLocationPermissionAndRefreshGreeting(string name)
        {
            _locationManager ??= (LocationManager)GetSystemService(LocationService);

            GymGeofencePermissions.EnsureReady(this, () =>
            {
                RefreshGreetingFromLocation(name);
                GymGeofenceRegistrar.TryRegister(this);
            });
        }

        private void RefreshGreetingFromLocation(string name)
        {
            var cached = GetBestLastKnownLocation();
            if (cached != null && GymProximity.IsFreshEnough(cached, GymProximity.CachedLocationMaxAge))
            {
                ApplyGymProximityState(name, GymProximity.IsWithinGymRadius(cached));
            }

            GymLocationRefresh.Request(this, location =>
            {
                if (IsDestroyed)
                    return;

                var atGym = location != null && GymProximity.IsWithinGymRadius(location);
                RunOnUiThread(() => ApplyGymProximityState(name, atGym));
            });
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            GymGeofencePermissions.OnPermissionsResult(this, requestCode, () =>
            {
                var profilePrefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
                var name = profilePrefs?.GetString("full_name", "User") ?? "User";
                RefreshGreetingFromLocation(name);
                GymGeofenceRegistrar.TryRegister(this);
            });
        }

        private Location? GetBestLastKnownLocation()
        {
            if (_locationManager == null)
                return null;

            var criteria = new Criteria { Accuracy = Accuracy.Fine };
            var bestProvider = _locationManager.GetBestProvider(criteria, true);
            var lastLocation = bestProvider == null ? null : _locationManager.GetLastKnownLocation(bestProvider);
            return lastLocation ?? _locationManager.GetLastKnownLocation(LocationManager.GpsProvider) ?? _locationManager.GetLastKnownLocation(LocationManager.NetworkProvider);
        }

        private void RenderTodayTrainingPlan()
        {
            if (_todayPlanSummaryText == null || _todayPlanMetaText == null)
                return;

            var groups = _database?.GetDailyWorkoutGroups() ?? new[] { "Chest", "Back", "Legs" };
            _todayPlanSummaryText.Text = string.Join(" · ", groups);

            var estimatedMinutes = Math.Max(30, groups.Length * 15);
            if (_todayPlanMetaText != null)
            {
                _todayPlanMetaText.Text = GetString(
                    Resource.String.home_today_meta_format,
                    groups.Length,
                    estimatedMinutes);
            }

            RenderLastWorkoutFooter();
            UpdateStartWorkoutButtonLabel();
        }

        private void RenderLastWorkoutFooter()
        {
            if (_todayLastWorkoutText == null)
                return;

            if (_database == null)
            {
                _todayLastWorkoutText.Text = GetString(Resource.String.home_last_workout_none);
                return;
            }

            var today = DateTime.Today;
            var history = _database.GetWorkoutHistory(100);
            var lastSession = history
                .Where(s => s.StartTime.Date < today && s.Exercises.Any(e => e.Sets.Count > 0))
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefault();

            if (lastSession == null)
            {
                _todayLastWorkoutText.Text = GetString(Resource.String.home_last_workout_none);
                return;
            }

            var whenLabel = FormatRelativeWorkoutDay(lastSession.StartTime.Date, today);
            var focusLabel = SummarizeSessionFocus(lastSession);
            _todayLastWorkoutText.Text = GetString(
                Resource.String.home_last_workout_format,
                whenLabel,
                focusLabel);
        }

        private void UpdateStartWorkoutButtonLabel()
        {
            if (_startWorkoutHomeButton == null)
                return;

            var hasActiveSession = _database?.GetCurrentWorkout() != null;
            _startWorkoutHomeButton.Text = GetString(
                hasActiveSession ? Resource.String.home_continue_workout : Resource.String.home_start_workout);
        }

        private static string FormatRelativeWorkoutDay(DateTime workoutDate, DateTime today)
        {
            var days = (today - workoutDate).Days;
            if (days == 1)
                return "Yesterday";

            if (days < 7)
                return $"{days} days ago";

            return workoutDate.ToString("MMM d");
        }

        private static string SummarizeSessionFocus(WorkoutSession session)
        {
            var exerciseNames = session.Exercises
                .Where(e => e.Sets.Count > 0 && e.Exercise != null)
                .Select(e => e.Exercise!.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToList();

            if (exerciseNames.Count > 0)
                return string.Join(" · ", exerciseNames);

            return string.IsNullOrWhiteSpace(session.Name) ? "Workout" : session.Name.Trim();
        }

        private async Task TryPullWorkoutsAndRefreshAsync()
        {
            if (_database == null)
                return;

            if (!await _cloudPullLock.WaitAsync(TimeSpan.FromSeconds(8)).ConfigureAwait(false))
                return;

            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                await WorkoutCloudSyncService.TryPullAndApplyAsync(_database, timeoutCts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort only.
            }
            finally
            {
                _cloudPullLock.Release();
            }

            RunOnUiThread(() =>
            {
                try
                {
                    LoadTodayRecords();
                }
                catch
                {
                    // Ignore UI refresh failures.
                }
            });
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

            int weeklyGoal = GetWeeklyGoal();
            // Weekly chart/summary should reflect what's in the Log tab (completed sessions only).
            int workoutsThisWeek = CountWorkoutsThisWeek(history);
            UpdateProgressWidget(workoutsThisWeek, weeklyGoal);
            UpdateWeeklyProgressChart(history);
            UpdateInsightText(insightSessions, workoutsThisWeek);

            var exerciseRecords = completedToday
                .SelectMany(session => session.Exercises)
                .Where(exercise => exercise.Exercise != null)
                .ToList();

            if (exerciseRecords.Count == 0)
            {
                RenderSmartEmptyState(completedToday);
                RenderLastWorkoutFooter();
                UpdateStartWorkoutButtonLabel();
                return;
            }

            foreach (var record in exerciseRecords)
            {
                if (record.Exercise == null)
                    continue;

                var row = new LinearLayout(this)
                {
                    Orientation = Orientation.Horizontal
                };
                row.SetGravity(GravityFlags.CenterVertical);
                row.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.WrapContent);
                row.SetPadding(0, DpToPx(6), 0, DpToPx(6));

                var nameView = new TextView(this)
                {
                    Text = record.Exercise.Name,
                    TextSize = 14
                };
                nameView.LayoutParameters = new LinearLayout.LayoutParams(
                    0,
                    ViewGroup.LayoutParams.WrapContent,
                    1f);

                var setCount = record.Sets.Count;
                var totalReps = record.Sets.Sum(s => s.Reps);
                var maxWeight = record.Sets.Count == 0 ? 0 : record.Sets.Max(s => s.Weight);
                var unit = record.Sets.FirstOrDefault()?.WeightUnit ?? "kg";

                var resultText = setCount == 0
                    ? "0 sets"
                    : $"{setCount} set{(setCount == 1 ? string.Empty : "s")} · {totalReps} reps · {maxWeight:0.#} {unit}";

                var resultSpannable = new SpannableString(resultText);
                resultSpannable.SetSpan(
                    new StyleSpan(TypefaceStyle.Bold),
                    0,
                    resultText.Length,
                    SpanTypes.ExclusiveExclusive);

                var countView = new TextView(this)
                {
                    TextSize = 13
                };
                countView.SetText(resultSpannable, TextView.BufferType.Spannable);
                countView.SetTextColor(Android.Graphics.Color.White);
                countView.SetSingleLine(true);
                countView.Ellipsize = TextUtils.TruncateAt.End;
                countView.Gravity = GravityFlags.End;
                countView.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WrapContent,
                    ViewGroup.LayoutParams.WrapContent);

                row.AddView(nameView);
                row.AddView(countView);
                _todayExercisesContainer.AddView(row);
            }

            RenderLastWorkoutFooter();
            UpdateStartWorkoutButtonLabel();
        }

        private void UpdateProgressWidget(int workoutsThisWeek, int weeklyGoal)
        {
            // Weekly Progress card now uses duration-based chart + summary (see UpdateWeeklyProgressChart).
        }

        private void UpdateWeeklyProgressChart(List<WorkoutSession> sessions)
        {
            if (_weeklyProgressChart == null)
                return;

            _weeklyProgressChart.RemoveAllViews();

            var weekStart = GetWeekStart(DateTime.Today);
            var labels = new[] { "M", "T", "W", "T", "F", "S", "S" };
            var dailyMinutes = new double[7];

            for (int i = 0; i < 7; i++)
            {
                var day = weekStart.AddDays(i).Date;
                dailyMinutes[i] = sessions
                    .Where(session => session.StartTime.Date == day)
                    .Where(session => session.Exercises.Any(exercise => exercise.Sets.Count > 0))
                    .Select(session => session.Duration)
                    .Where(duration => duration > TimeSpan.Zero)
                    .Sum(duration => duration.TotalMinutes);

                if (dailyMinutes[i] < 0)
                    dailyMinutes[i] = 0;

                dailyMinutes[i] = Math.Min(120d, dailyMinutes[i]);
            }

            const double axisMaxMinutes = 120d;
            const double goalMinutes = 60d;

            var totalMinutes = dailyMinutes.Sum();
            var avgMinutes = totalMinutes / 7d;
            if (_weeklyAvgValueText != null)
            {
                _weeklyAvgValueText.Text = Math.Round(avgMinutes).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            }
            if (_weeklyAvgLabelText != null)
            {
                _weeklyAvgLabelText.Text = "mins per day (avg)";
            }

            var goalDays = dailyMinutes.Count(m => m >= goalMinutes);
            if (_weeklyGoalSummaryText != null)
            {
                var totalHours = totalMinutes / 60d;
                _weeklyGoalSummaryText.Text = $"You hit your goal on {goalDays} day{(goalDays == 1 ? string.Empty : "s")}, and trained a total of {totalHours:0.#}h";
            }

            int chartHeight = DpToPx(108);
            int barWidth = DpToPx(26);
            int barMaxHeight = chartHeight;

            var outerRow = new LinearLayout(this)
            {
                Orientation = Orientation.Horizontal
            };
            outerRow.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent);

            var barsAndLabels = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };
            barsAndLabels.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);

            var chartFrame = new FrameLayout(this);
            chartFrame.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, barMaxHeight);

            var barsRow = new LinearLayout(this)
            {
                Orientation = Orientation.Horizontal
            };
            var barsRowLp = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
            barsRow.LayoutParameters = barsRowLp;
            barsRow.BaselineAligned = false;

            var labelsRow = new LinearLayout(this)
            {
                Orientation = Orientation.Horizontal
            };
            labelsRow.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            labelsRow.BaselineAligned = false;

            for (int i = 0; i < 7; i++)
            {
                var barSlot = new FrameLayout(this);
                barSlot.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1f);

                var ratio = axisMaxMinutes <= 0 ? 0 : (dailyMinutes[i] / axisMaxMinutes);
                ratio = Math.Max(0d, Math.Min(1d, ratio));
                int barHeight = ratio <= 0
                    ? DpToPx(6)
                    : Math.Max(DpToPx(14), (int)Math.Round(barMaxHeight * ratio));

                var bar = new View(this);
                var barLp = new FrameLayout.LayoutParams(barWidth, barHeight);
                barLp.Gravity = GravityFlags.Bottom | GravityFlags.CenterHorizontal;
                bar.LayoutParameters = barLp;

                var barDrawable = new GradientDrawable();
                var minutes = dailyMinutes[i];
                var barColorRes = minutes <= 0
                    ? Resource.Color.md_theme_surfaceVariant
                    : minutes < 45d
                        ? Resource.Color.color_primary
                        : minutes > 60d
                            ? Resource.Color.md_theme_onPrimaryContainer
                            : Resource.Color.color_primary;
                barDrawable.SetColor(new Color(ContextCompat.GetColor(this, barColorRes)));
                barDrawable.SetCornerRadius(barWidth / 2f);
                bar.Background = barDrawable;
                barSlot.AddView(bar);

                if (dailyMinutes[i] >= goalMinutes)
                {
                    var badgeSize = DpToPx(22);
                    var badge = new FrameLayout(this);
                    var badgeLp = new FrameLayout.LayoutParams(badgeSize, badgeSize);
                    badgeLp.Gravity = GravityFlags.Top | GravityFlags.CenterHorizontal;
                    // Place badge near the top of the rounded bar (not at the top of the chart).
                    var barTop = barMaxHeight - barHeight;
                    badgeLp.TopMargin = Math.Max(DpToPx(2), barTop + DpToPx(2));
                    badge.LayoutParameters = badgeLp;

                    var badgeDrawable = new GradientDrawable();
                    badgeDrawable.SetColor(new Color(ContextCompat.GetColor(this, Resource.Color.md_theme_surfaceVariant)));
                    badgeDrawable.SetCornerRadius(badgeSize / 2f);
                    badgeDrawable.SetStroke(DpToPx(1), new Color(ContextCompat.GetColor(this, Resource.Color.md_theme_outline)));
                    badge.Background = badgeDrawable;

                    var check = new TextView(this)
                    {
                        Text = "✓",
                        TextSize = 14
                    };
                    check.SetTextColor(new Color(ContextCompat.GetColor(this, Resource.Color.color_text_primary)));
                    check.SetTypeface(null, TypefaceStyle.Bold);
                    check.SetIncludeFontPadding(false);
                    check.Gravity = GravityFlags.Center;
                    check.LayoutParameters = new FrameLayout.LayoutParams(
                        ViewGroup.LayoutParams.MatchParent,
                        ViewGroup.LayoutParams.MatchParent);

                    badge.AddView(check);
                    barSlot.AddView(badge);
                }

                barsRow.AddView(barSlot);

                var label = new TextView(this)
                {
                    Text = labels[i],
                    TextSize = 11
                };
                label.SetTextColor(new Color(ContextCompat.GetColor(this, Resource.Color.color_text_secondary)));
                label.Gravity = GravityFlags.Center;
                label.SetIncludeFontPadding(false);
                label.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
                label.SetPadding(0, DpToPx(6), 0, 0);
                labelsRow.AddView(label);
            }

            chartFrame.AddView(barsRow);

            barsAndLabels.AddView(chartFrame);
            barsAndLabels.AddView(labelsRow);

            outerRow.AddView(barsAndLabels);

            var axisColumn = new FrameLayout(this);
            axisColumn.LayoutParameters = new LinearLayout.LayoutParams(DpToPx(34), barMaxHeight);

            void AddAxisLabelRight(int hours)
            {
                var label = new TextView(this)
                {
                    Text = $"{hours}h",
                    TextSize = 10
                };
                label.SetTextColor(new Color(ContextCompat.GetColor(this, Resource.Color.color_text_primary)));
                label.SetIncludeFontPadding(false);

                var ratioTop = hours / 2d;
                var top = (int)Math.Round(barMaxHeight * (1d - ratioTop));
                top = Math.Max(0, Math.Min(barMaxHeight - DpToPx(12), top));

                var lp = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
                lp.Gravity = GravityFlags.Top | GravityFlags.End;
                lp.TopMargin = top;
                label.LayoutParameters = lp;
                axisColumn.AddView(label);
            }

            AddAxisLabelRight(1);
            AddAxisLabelRight(0);
            AddAxisLabelRight(2);

            outerRow.AddView(axisColumn);
            _weeklyProgressChart.AddView(outerRow);
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

            var dialogView = LayoutInflater?.Inflate(Resource.Layout.dialog_home_onboarding, null);
            if (dialogView == null)
                return;

            var ctaButton = dialogView.FindViewById<Button>(Resource.Id.onboardingCtaButton);

            var dialog = new MaterialAlertDialogBuilder(this)
                .SetView(dialogView)
                .SetCancelable(false)
                .Create();

            if (ctaButton != null)
            {
                ctaButton.Click += (s, e) =>
                {
                    prefs.Edit()?.PutBoolean("home_onboarding_seen", true)?.Apply();
                    dialog.Dismiss();
                };
            }

            dialog.Show();
            DialogThemeHelper.StyleShownDialog(this, dialog, styleButtons: false);
        }

        private void ShowWhatsNewAfterUpgrade()
        {
            var versionPrefs = GetSharedPreferences("app_release_notes", FileCreationMode.Private);
            if (versionPrefs == null)
                return;

            var (_, versionCode) = ReleaseInfo.GetAppVersion(this);
            if (versionCode <= 0)
                return;

            var lastSeenVersionCode = versionPrefs.GetLong("last_seen_version_code", 0);

            if (lastSeenVersionCode <= 0)
            {
                versionPrefs.Edit()?.PutLong("last_seen_version_code", versionCode)?.Apply();
                return;
            }

            if (versionCode <= lastSeenVersionCode)
                return;

            var onboardingPrefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            var hasSeenOnboarding = onboardingPrefs?.GetBoolean("home_onboarding_seen", false) ?? false;
            if (!hasSeenOnboarding)
            {
                versionPrefs.Edit()?.PutLong("last_seen_version_code", versionCode)?.Apply();
                return;
            }

            var dialog = new MaterialAlertDialogBuilder(this)
                .SetTitle("What's New")
                .SetMessage("Your workout plan now stays put until you tap Finish Workout. Miss a day? No problem — your plan waits for you and only moves forward once you finish a session.")
                .SetPositiveButton("Got it", (s, e) =>
                {
                    versionPrefs.Edit()?.PutLong("last_seen_version_code", versionCode)?.Apply();
                })
                .SetCancelable(false)
                .Create();

            dialog.Show();
            DialogThemeHelper.StyleShownDialog(this, dialog);
        }

        private void StartQuickExercise(Exercise exercise)
        {
            if (_database == null)
                return;

            var currentWorkout = _database.GetCurrentWorkout();
            var session = currentWorkout ?? _database.CreateWorkoutSession("Quick Start Workout");
            _database.AddExerciseToWorkout(session.Id, exercise.Id);

            StartActivity(WorkoutActivity.CreateIntent(this, startWorkoutTimer: true, workoutId: session.Id));
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
            var pickerDialog = picker.Show();
            DialogThemeHelper.StyleShownDialog(this, pickerDialog, styleButtons: false);
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

            DialogThemeHelper.StyleInput(this, repsInput);
            DialogThemeHelper.StyleInput(this, weightInput);

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
            var shownDialog = dialog.Show();
            DialogThemeHelper.StyleShownDialog(this, shownDialog);
        }
    }
}
