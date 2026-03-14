using Android.Content;
using Android.Widget;
using Gym_App.Data;
using Gym_App.Models;
using System.Globalization;
using Android.Text;
using Android.Graphics;
using Android.Net;
using Android.Content.Res;
using System;
using System.Linq;
using Android.Views;
using Gym_App;
using Google.Android.Material.Dialog;

namespace Gym_App.Activities
{
    [Activity(Label = "Profile")]
    public class ProfileActivity : Activity
    {
        private GymDatabase? _database;

        private const int PickAvatarRequestCode = 3101;
        private const int EditProfileRequestCode = 3102;
        private const string ProfilePrefsName = "user_profile";
        private const string AvatarUriKey = "avatar_uri";

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_profile);

            SetupKeyMetricsTabs();

            _database = new GymDatabase();

            var homeTab = FindViewById<LinearLayout>(Resource.Id.homeTab);
            var diaryTab = FindViewById<LinearLayout>(Resource.Id.diaryTab);
            var workoutTab = FindViewById<LinearLayout>(Resource.Id.workoutTab);
            var profileTab = FindViewById<LinearLayout>(Resource.Id.profileTab);

            if (homeTab != null) homeTab.Selected = false;
            if (diaryTab != null) diaryTab.Selected = false;
            if (workoutTab != null) workoutTab.Selected = false;
            if (profileTab != null) profileTab.Selected = true;
            UpdateBottomNavLabelStyles();

            if (homeTab != null)
            {
                homeTab.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(HomeActivity)));
                };
            }

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

            LoadProfileHeader();
            LoadWeeklyProgressCard();
            LoadStatsOverview();
            LoadPrs();
            LoadTrainingSummary();
            SetupActions();
            SetupHeaderActions();
            SetupAvatarPicker();
        }

        private void SetupKeyMetricsTabs()
        {
            var tabStats = FindViewById<TextView>(Resource.Id.keyMetricsTabStats);
            var tabPrs = FindViewById<TextView>(Resource.Id.keyMetricsTabPrs);
            var statsContainer = FindViewById<LinearLayout>(Resource.Id.keyMetricsStatsContainer);
            var prsContainer = FindViewById<LinearLayout>(Resource.Id.keyMetricsPrsContainer);
            var card = FindViewById<LinearLayout>(Resource.Id.keyMetricsCard);

            if (tabStats == null || tabPrs == null || statsContainer == null || prsContainer == null || card == null)
                return;

            void SelectStats()
            {
                statsContainer.Visibility = ViewStates.Visible;
                prsContainer.Visibility = ViewStates.Gone;

                tabStats.SetBackgroundResource(Resource.Drawable.bg_button_primary);
                tabStats.SetTextColor(new Color(GetColor(Resource.Color.color_on_primary)));
                tabStats.SetTypeface(null, TypefaceStyle.Bold);

                tabPrs.SetBackgroundResource(Resource.Drawable.bg_key_metrics_tab_inactive);
                tabPrs.SetTextColor(new Color(GetColor(Resource.Color.color_text_secondary)));
                tabPrs.SetTypeface(null, TypefaceStyle.Bold);
            }

            void SelectPrs()
            {
                statsContainer.Visibility = ViewStates.Gone;
                prsContainer.Visibility = ViewStates.Visible;

                tabPrs.SetBackgroundResource(Resource.Drawable.bg_button_primary);
                tabPrs.SetTextColor(new Color(GetColor(Resource.Color.color_on_primary)));
                tabPrs.SetTypeface(null, TypefaceStyle.Bold);

                tabStats.SetBackgroundResource(Resource.Drawable.bg_key_metrics_tab_inactive);
                tabStats.SetTextColor(new Color(GetColor(Resource.Color.color_text_secondary)));
                tabStats.SetTypeface(null, TypefaceStyle.Bold);
            }

            tabStats.Click += (_, __) => SelectStats();
            tabPrs.Click += (_, __) => SelectPrs();

            SelectStats();

            card.Post(() =>
            {
                if (card.Width <= 0)
                    return;

                var widthSpec = View.MeasureSpec.MakeMeasureSpec(card.Width - card.PaddingLeft - card.PaddingRight, MeasureSpecMode.AtMost);
                var heightSpec = View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified);

                var originalStatsVisibility = statsContainer.Visibility;
                var originalPrsVisibility = prsContainer.Visibility;

                statsContainer.Visibility = ViewStates.Visible;
                prsContainer.Visibility = ViewStates.Invisible;

                statsContainer.Measure(widthSpec, heightSpec);
                var statsHeight = statsContainer.MeasuredHeight;

                prsContainer.Measure(widthSpec, heightSpec);
                var prsHeight = prsContainer.MeasuredHeight;

                var fixedHeight = Math.Max(statsHeight, prsHeight);
                if (fixedHeight > 0)
                {
                    var statsLp = statsContainer.LayoutParameters;
                    statsLp.Height = fixedHeight;
                    statsContainer.LayoutParameters = statsLp;

                    var prsLp = prsContainer.LayoutParameters;
                    prsLp.Height = fixedHeight;
                    prsContainer.LayoutParameters = prsLp;
                }

                statsContainer.Visibility = originalStatsVisibility;
                prsContainer.Visibility = originalPrsVisibility;
            });
        }

        protected override void OnResume()
        {
            base.OnResume();
            LoadProfileHeader();
            LoadWeeklyProgressCard();
            LoadStatsOverview();
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

        private void LoadProfileHeader()
        {
            var greetingValue = FindViewById<TextView>(Resource.Id.profileGreeting);
            var nameValue = FindViewById<TextView>(Resource.Id.profileName);
            var bioValue = FindViewById<TextView>(Resource.Id.profileBio);
            var photoView = FindViewById<ImageView>(Resource.Id.profilePhoto);

            var prefs = GetSharedPreferences(ProfilePrefsName, FileCreationMode.Private);
            var fullName = prefs?.GetString("full_name", string.Empty) ?? string.Empty;
            var fitnessTag = prefs?.GetString("fitness_tag", "Strength") ?? "Strength";
            var trainingYears = prefs?.GetString("training_years", "1") ?? "1";
            var trainingStage = prefs?.GetString("training_stage", "Intermediate") ?? "Intermediate";
            var goal = prefs?.GetString("goal", "Build strength") ?? "Build strength";
            var avatarUriString = prefs?.GetString(AvatarUriKey, string.Empty) ?? string.Empty;

            if (greetingValue != null)
                greetingValue.Text = BuildGreetingLine(fullName);

            if (nameValue != null)
                nameValue.Text = string.IsNullOrWhiteSpace(fullName) ? "Nickname not set" : FormatDisplayName(fullName);

            if (bioValue != null)
                bioValue.Text = $"{goal} · {fitnessTag} · {trainingYears}y {trainingStage}";

            if (photoView != null)
                ApplyAvatarToView(photoView, avatarUriString);
        }

        private void SetupAvatarPicker()
        {
            var photoView = FindViewById<ImageView>(Resource.Id.profilePhoto);
            if (photoView == null)
                return;

            photoView.Click += (s, e) => LaunchAvatarPicker();
        }

        private void LaunchAvatarPicker()
        {
            try
            {
                var intent = new Intent(Intent.ActionOpenDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType("image/*");
                intent.AddFlags(ActivityFlags.GrantReadUriPermission);
                intent.AddFlags(ActivityFlags.GrantPersistableUriPermission);
                StartActivityForResult(intent, PickAvatarRequestCode);
            }
            catch (Exception)
            {
                Toast.MakeText(this, "Unable to open photo picker", ToastLength.Short)?.Show();
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == EditProfileRequestCode)
            {
                if (resultCode == Result.Ok)
                {
                    LoadProfileHeader();
                    LoadStatsOverview();
                    LoadWeeklyProgressCard();
                }
                return;
            }

            if (requestCode != PickAvatarRequestCode)
                return;

            if (resultCode != Result.Ok)
                return;

            var uri = data?.Data;
            if (uri == null)
                return;

            try
            {
                ContentResolver?.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission);
            }
            catch
            {
                // Some pickers/providers may not allow persistable permissions; still try to use the URI.
            }

            var prefs = GetSharedPreferences(ProfilePrefsName, FileCreationMode.Private);
            prefs?.Edit()?.PutString(AvatarUriKey, uri.ToString())?.Apply();

            var photoView = FindViewById<ImageView>(Resource.Id.profilePhoto);
            if (photoView != null)
                ApplyAvatarToView(photoView, uri.ToString());
        }

        private void ApplyAvatarToView(ImageView photoView, string avatarUriString)
        {
            if (string.IsNullOrWhiteSpace(avatarUriString))
            {
                photoView.SetImageResource(Resource.Drawable.ic_nav_profile);
                photoView.SetScaleType(ImageView.ScaleType.CenterInside);
                photoView.ImageTintList = ColorStateList.ValueOf(Color.White);
                return;
            }

            try
            {
                var uri = Android.Net.Uri.Parse(avatarUriString);
                if (uri == null)
                    throw new InvalidOperationException("Invalid avatar uri");

                photoView.ImageTintList = null;
                photoView.ClearColorFilter();
                photoView.SetScaleType(ImageView.ScaleType.CenterCrop);
                photoView.SetImageURI(uri);
            }
            catch
            {
                photoView.SetImageResource(Resource.Drawable.ic_nav_profile);
                photoView.SetScaleType(ImageView.ScaleType.CenterInside);
                photoView.ImageTintList = ColorStateList.ValueOf(Color.White);

                var prefs = GetSharedPreferences(ProfilePrefsName, FileCreationMode.Private);
                prefs?.Edit()?.Remove(AvatarUriKey)?.Apply();
            }
        }

        private void OnEditProfileClick(object? sender, EventArgs e)
        {
            StartActivityForResult(new Intent(this, typeof(EditProfileActivity)), EditProfileRequestCode);
        }

        private void SetupHeaderActions()
        {
            var headerArea = FindViewById<LinearLayout>(Resource.Id.profileHeaderArea);
            var editButton = FindViewById<ImageButton>(Resource.Id.editProfileButton);
            var setPhotoAction = FindViewById<View>(Resource.Id.setPhotoAction);
            var editInfoAction = FindViewById<View>(Resource.Id.editInfoAction);
            var openSettingsAction = FindViewById<View>(Resource.Id.openSettingsAction);

            if (headerArea != null)
            {
                headerArea.Click += OnEditProfileClick;
            }

            if (editButton != null)
            {
                editButton.Click += OnEditProfileClick;
            }

            if (setPhotoAction != null)
            {
                setPhotoAction.Click += (_, __) => LaunchAvatarPicker();
            }

            if (editInfoAction != null)
            {
                editInfoAction.Click += OnEditProfileClick;
            }

            if (openSettingsAction != null)
            {
                openSettingsAction.Click += (_, __) => StartActivity(new Intent(this, typeof(SettingsActivity)));
            }
        }

        private void LoadWeeklyProgressCard()
        {
            if (_database == null)
                return;

            var headline = FindViewById<TextView>(Resource.Id.weeklyProgressHeadline);
            var detail = FindViewById<TextView>(Resource.Id.weeklyProgressText);
            var bar = FindViewById<ProgressBar>(Resource.Id.weeklyProgressBar);

            if (headline == null || detail == null || bar == null)
                return;

            var prefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            var weeklyGoal = prefs?.GetInt("weekly_goal", 4) ?? 4;
            if (weeklyGoal <= 0)
                weeklyGoal = 4;

            var workouts = _database.GetWorkoutHistory(5000);

            DateTime today = DateTime.Today;
            DateTime weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            if (weekStart > today)
                weekStart = weekStart.AddDays(-7);

            int workoutsThisWeek = workouts.Count(w => w.StartTime.Date >= weekStart && w.StartTime.Date <= today);
            var percent = (int)Math.Round((workoutsThisWeek / (double)weeklyGoal) * 100.0);
            percent = Math.Max(0, Math.Min(percent, 100));

            headline.Text = workoutsThisWeek == 0
                ? "Let's start your first workout today!"
                : $"You've done {workoutsThisWeek} workouts this week!";
            detail.Text = $"{percent}% of your weekly goal is completed.";
            bar.Progress = percent;
        }

        private static string GetGreetingText()
        {
            var hour = DateTime.Now.Hour;
            if (hour < 12)
                return "Good morning,";
            if (hour < 18)
                return "Good afternoon,";
            return "Good evening,";
        }

        private static string BuildGreetingLine(string fullName)
        {
            var greeting = GetGreetingText();

            var name = (fullName ?? string.Empty).Trim();
            if (name.Length == 0)
                return $"{greeting} User !";

            // Prefer first name for the greeting line.
            var first = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name;
            first = first.Trim();
            if (first.Length == 0)
                first = "User";

            return $"{greeting} {FormatDisplayName(first)}";
        }

        private static string FormatDisplayName(string fullName)
        {
            var trimmed = (fullName ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return string.Empty;

            return trimmed.EndsWith("!", StringComparison.Ordinal) ? trimmed : $"{trimmed} !";
        }

        private void LoadStatsOverview()
        {
            var weightValue = FindViewById<TextView>(Resource.Id.statWeightValue);
            var heightValue = FindViewById<TextView>(Resource.Id.statHeightValue);
            var ageValue = FindViewById<TextView>(Resource.Id.statAgeValue);
            var bodyFatValue = FindViewById<TextView>(Resource.Id.statBodyFatValue);

            if (weightValue == null || heightValue == null || ageValue == null || bodyFatValue == null)
                return;

            var prefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            var unit = prefs?.GetString("unit", "lb") ?? "lb";

            var height = prefs?.GetString("height_cm", "--") ?? "--";
            var currentWeight = prefs?.GetString("current_weight", "--") ?? "--";
            var age = prefs?.GetString("age", "--") ?? "--";

            weightValue.Text = currentWeight == "--"
                ? "Tap to add your first weight log"
                : $"{currentWeight} {unit}";

            var hasHeight = TryParseNumber(height, out var heightRawNumeric) && heightRawNumeric > 0;
            var heightFeet = hasHeight
                ? (heightRawNumeric > 20 ? heightRawNumeric / 30.48 : heightRawNumeric)
                : 0;

            heightValue.Text = !hasHeight
                ? "Tap to add height"
                : $"{heightFeet:0.##} ft";

            ageValue.Text = age == "--"
                ? "Tap to add age"
                : $"{age} yrs";

            if (TryParseNumber(currentWeight, out var weightNumeric) &&
                hasHeight &&
                TryParseNumber(age, out var ageNumeric) &&
                heightFeet > 0)
            {
                var weightKg = unit.Equals("lb", StringComparison.OrdinalIgnoreCase)
                    ? weightNumeric * 0.45359237
                    : weightNumeric;

                var heightMeters = heightFeet * 0.3048;
                var bmi = weightKg / (heightMeters * heightMeters);
                var bodyFatPercent = (1.2 * bmi) + (0.23 * ageNumeric) - 5.4;
                var clampedBodyFat = Math.Clamp(bodyFatPercent, 2.0, 65.0);

                bodyFatValue.Text = $"{clampedBodyFat:F1}%";
            }
            else
            {
                bodyFatValue.Text = "--";
            }
        }

        private bool TryParseNumber(string value, out double number)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                   double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number);
        }

        private void LoadPrs()
        {
            if (_database == null)
                return;

            var squatView = FindViewById<TextView>(Resource.Id.prSquatValue);
            var benchView = FindViewById<TextView>(Resource.Id.prBenchValue);
            var deadliftView = FindViewById<TextView>(Resource.Id.prDeadliftValue);
            var overheadView = FindViewById<TextView>(Resource.Id.prOverheadValue);

            if (squatView == null || benchView == null || deadliftView == null || overheadView == null)
                return;

            var workouts = _database.GetWorkoutHistory(5000);
            var squatPr = ComputePrForCategory(workouts, "squat");
            var benchPr = ComputePrForCategory(workouts, "bench");
            var deadliftPr = ComputePrForCategory(workouts, "deadlift");
            var optionalPr = ComputePrForCategory(workouts, "overhead");

            squatView.Text = $"Squat 1RM: {FormatPr(squatPr)}";
            benchView.Text = $"Bench 1RM: {FormatPr(benchPr)}";
            deadliftView.Text = $"Deadlift 1RM: {FormatPr(deadliftPr)}";
            overheadView.Text = $"Overhead Press (optional): {FormatPr(optionalPr)}";
        }

        private void LoadTrainingSummary()
        {
            if (_database == null)
                return;

            var weekView = FindViewById<TextView>(Resource.Id.summaryWeekValue);
            var totalView = FindViewById<TextView>(Resource.Id.summaryTotalValue);
            var streakView = FindViewById<TextView>(Resource.Id.summaryStreakValue);
            var volumeView = FindViewById<TextView>(Resource.Id.summaryVolumeValue);

            if (weekView == null || totalView == null || streakView == null || volumeView == null)
                return;

            var workouts = _database.GetWorkoutHistory(5000);

            DateTime today = DateTime.Today;
            DateTime weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            if (weekStart > today)
                weekStart = weekStart.AddDays(-7);

            int weekCount = workouts.Count(w => w.StartTime.Date >= weekStart && w.StartTime.Date <= today);
            int totalCount = workouts.Count;
            int currentStreak = CalculateCurrentStreak(workouts.Select(w => w.StartTime.Date).Distinct().OrderBy(d => d).ToList());

            double totalVolume = workouts
                .SelectMany(w => w.Exercises)
                .SelectMany(e => e.Sets)
                .Sum(s => s.Weight * s.Reps);

            weekView.Text = $"Workouts this week: {weekCount}";
            totalView.Text = $"Total workouts: {totalCount}";
            streakView.Text = $"Current streak: {currentStreak} days";
            volumeView.Text = $"Volume lifted (optional): {Math.Round(totalVolume, 1)} kg";
        }

        private void SetupActions()
        {
            var startWorkoutButton = FindViewById(Resource.Id.quickStartWorkoutButton);
            var logWeightButton = FindViewById(Resource.Id.quickLogWeightButton);
            var viewHistoryButton = FindViewById(Resource.Id.quickViewHistoryButton);
            var viewProgressButton = FindViewById(Resource.Id.quickViewProgressButton);

            if (startWorkoutButton != null)
            {
                startWorkoutButton.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(WorkoutActivity)));
                };
            }

            if (viewHistoryButton != null)
            {
                viewHistoryButton.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(HistoryActivity)));
                };
            }

            if (viewProgressButton != null)
            {
                viewProgressButton.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(ProgressActivity)));
                };
            }

            if (logWeightButton != null)
            {
                logWeightButton.Click += (s, e) => ShowLogWeightDialog();
            }
        }

        private void ShowLogWeightDialog()
        {
            int DpToPx(int dp) => (int)(dp * Resources.DisplayMetrics.Density);

            var input = new EditText(this)
            {
                Hint = "Enter current weight"
            };
            input.InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;
            DialogThemeHelper.StyleInput(this, input);

            var layout = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };
            layout.SetPadding(DpToPx(24), DpToPx(18), DpToPx(24), DpToPx(18));

            var titleText = new TextView(this)
            {
                Text = "Log Weight",
                TextSize = 22f
            };
            titleText.SetTextColor(new Color(GetColor(Resource.Color.color_text_primary)));
            titleText.SetTypeface(null, TypefaceStyle.Bold);

            input.SetPadding(0, DpToPx(10), 0, DpToPx(16));

            var actionRow = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Horizontal };
            var cancelButton = new Button(this) { Text = "Cancel" };
            var saveButton = new Button(this) { Text = "Save" };

            var cancelLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
            {
                RightMargin = DpToPx(6)
            };
            var saveLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
            {
                LeftMargin = DpToPx(6)
            };

            cancelButton.LayoutParameters = cancelLp;
            saveButton.LayoutParameters = saveLp;

            cancelButton.SetAllCaps(false);
            cancelButton.SetBackgroundResource(Resource.Drawable.bg_button_primary);
            cancelButton.SetTextColor(new Color(GetColor(Android.Resource.Color.Black)));
            cancelButton.SetTypeface(null, TypefaceStyle.Bold);
            cancelButton.SetPadding(DpToPx(18), DpToPx(8), DpToPx(18), DpToPx(8));

            saveButton.SetAllCaps(false);
            saveButton.SetBackgroundResource(Resource.Drawable.bg_button_primary);
            saveButton.SetTextColor(new Color(GetColor(Android.Resource.Color.Black)));
            saveButton.SetTypeface(null, TypefaceStyle.Bold);
            saveButton.SetPadding(DpToPx(18), DpToPx(8), DpToPx(18), DpToPx(8));

            actionRow.AddView(cancelButton);
            actionRow.AddView(saveButton);

            layout.AddView(titleText);
            layout.AddView(input);
            layout.AddView(actionRow);

            var dialog = new MaterialAlertDialogBuilder(this)
                .SetView(layout)
                .Create();

            dialog.Show();
            DialogThemeHelper.StyleShownDialog(this, dialog, styleButtons: false);

            cancelButton.Click += (s, e) => dialog.Dismiss();
            saveButton.Click += (s, e) =>
            {
                var value = input.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(value))
                {
                    Toast.MakeText(this, "Please enter a valid weight", ToastLength.Short)?.Show();
                    return;
                }

                var prefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
                prefs?.Edit()?.PutString("current_weight", value)?.Apply();
                LoadStatsOverview();
                Toast.MakeText(this, "Weight updated", ToastLength.Short)?.Show();
                dialog.Dismiss();
            };
        }

        private static (double weight, DateTime? date) ComputePrForCategory(IEnumerable<WorkoutSession> workouts, string category)
        {
            double best = 0;
            DateTime? date = null;

            foreach (var workout in workouts)
            {
                foreach (var exercise in workout.Exercises)
                {
                    var exerciseName = exercise.Exercise?.Name?.ToLowerInvariant() ?? string.Empty;
                    bool isMatch = category switch
                    {
                        "squat" => exerciseName.Contains("squat"),
                        "bench" => exerciseName.Contains("bench") || exerciseName.Contains("press"),
                        "deadlift" => exerciseName.Contains("deadlift"),
                        "overhead" => exerciseName.Contains("overhead") || exerciseName.Contains("shoulder") || exerciseName.Contains("pull-up") || exerciseName.Contains("pull up"),
                        _ => false
                    };

                    if (!isMatch)
                        continue;

                    foreach (var set in exercise.Sets)
                    {
                        if (set.Reps <= 0 || set.Weight <= 0)
                            continue;

                        double estimatedOneRm = set.Weight * (1 + set.Reps / 30.0);
                        if (estimatedOneRm > best)
                        {
                            best = estimatedOneRm;
                            date = workout.StartTime.Date;
                        }
                    }
                }
            }

            return (Math.Round(best, 1), date);
        }

        private static string FormatPr((double weight, DateTime? date) pr)
        {
            if (pr.weight <= 0 || pr.date == null)
                return "-- kg (--)";

            return $"{pr.weight} kg ({pr.date.Value:yyyy-MM-dd})";
        }

        private static int CalculateLongestStreak(List<DateTime> dates)
        {
            if (dates.Count == 0)
                return 0;

            int maxStreak = 1;
            int currentStreak = 1;

            for (int i = 1; i < dates.Count; i++)
            {
                if (dates[i] == dates[i - 1].AddDays(1))
                {
                    currentStreak++;
                    if (currentStreak > maxStreak)
                        maxStreak = currentStreak;
                }
                else
                {
                    currentStreak = 1;
                }
            }

            return maxStreak;
        }

        private static int CalculateCurrentStreak(List<DateTime> dates)
        {
            if (dates.Count == 0)
                return 0;

            var dateSet = new HashSet<DateTime>(dates);
            int streak = 0;
            DateTime cursor = DateTime.Today;

            if (!dateSet.Contains(cursor) && dateSet.Contains(cursor.AddDays(-1)))
            {
                cursor = cursor.AddDays(-1);
            }

            while (dateSet.Contains(cursor))
            {
                streak++;
                cursor = cursor.AddDays(-1);
            }

            return streak;
        }

        private static double ParseDouble(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }
    }
}
