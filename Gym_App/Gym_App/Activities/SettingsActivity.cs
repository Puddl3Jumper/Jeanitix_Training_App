using Android.Content;
using Android.Widget;
using Android.Graphics;
using Gym_App.Data;

namespace Gym_App.Activities
{
    [Activity(Label = "Settings")]
    public class SettingsActivity : Activity
    {
        private GymDatabase? _database;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_settings);

            _database = new GymDatabase();

            var prefs = GetSharedPreferences("user_profile", FileCreationMode.Private);

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
                homeTab.Click += (s, e) => StartActivity(new Intent(this, typeof(HomeActivity)));
            }

            if (diaryTab != null)
            {
                diaryTab.Click += (s, e) => StartActivity(new Intent(this, typeof(HistoryActivity)));
            }

            if (workoutTab != null)
            {
                workoutTab.Click += (s, e) => StartActivity(new Intent(this, typeof(WorkoutActivity)));
            }

            if (profileTab != null)
            {
                profileTab.Click += (s, e) => StartActivity(new Intent(this, typeof(ProfileActivity)));
            }

            var unitKgButton = FindViewById<Button>(Resource.Id.unitKgButton);
            var unitLbButton = FindViewById<Button>(Resource.Id.unitLbButton);
            var weightUnitHeader = FindViewById<TextView>(Resource.Id.weightUnitHeader);
            var weeklyGoal3Button = FindViewById<Button>(Resource.Id.weeklyGoal3Button);
            var weeklyGoal4Button = FindViewById<Button>(Resource.Id.weeklyGoal4Button);
            var weeklyGoal5Button = FindViewById<Button>(Resource.Id.weeklyGoal5Button);
            var weeklyGoal6Button = FindViewById<Button>(Resource.Id.weeklyGoal6Button);
            var themeLightButton = FindViewById<Button>(Resource.Id.themeLightButton);
            var themeDarkButton = FindViewById<Button>(Resource.Id.themeDarkButton);
            var themeSystemButton = FindViewById<Button>(Resource.Id.themeSystemButton);
            var exportCsvButton = FindViewById<Button>(Resource.Id.exportCsvButton);
            var appVersionText = FindViewById<TextView>(Resource.Id.appVersionText);

            if (appVersionText != null)
            {
                appVersionText.Text = GetString(Resource.String.settings_release_text);
            }

            var currentUnit = prefs?.GetString("unit", "kg") ?? "kg";
            if (currentUnit == "lbs")
            {
                currentUnit = "lb";
            }

            void UpdateUnitButtonState()
            {
                if (weightUnitHeader != null)
                {
                    var label = currentUnit.Equals("lb", StringComparison.OrdinalIgnoreCase) ? "LB" : "KG";
                    weightUnitHeader.Text = $"Weight Unit ({label})";
                }

                if (unitKgButton != null)
                {
                    unitKgButton.Alpha = 1.0f;
                    unitKgButton.SetTypeface(null, currentUnit == "kg" ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                }

                if (unitLbButton != null)
                {
                    unitLbButton.Alpha = 1.0f;
                    unitLbButton.SetTypeface(null, currentUnit == "lb" ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                }
            }

            UpdateUnitButtonState();

            if (unitKgButton != null)
            {
                unitKgButton.Click += (s, e) =>
                {
                    currentUnit = "kg";
                    prefs?.Edit()?.PutString("unit", currentUnit)?.Apply();
                    UpdateUnitButtonState();
                    Toast.MakeText(this, "Unit changed to kg", ToastLength.Short)?.Show();
                };
            }

            if (unitLbButton != null)
            {
                unitLbButton.Click += (s, e) =>
                {
                    currentUnit = "lb";
                    prefs?.Edit()?.PutString("unit", currentUnit)?.Apply();
                    UpdateUnitButtonState();
                    Toast.MakeText(this, "Unit changed to lb", ToastLength.Short)?.Show();
                };
            }

            var weeklyGoal = prefs?.GetInt("weekly_goal", 4) ?? 4;

            void UpdateWeeklyGoalButtonState()
            {
                if (weeklyGoal3Button != null)
                {
                    weeklyGoal3Button.Alpha = 1.0f;
                    weeklyGoal3Button.SetTypeface(null, weeklyGoal == 3 ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                }

                if (weeklyGoal4Button != null)
                {
                    weeklyGoal4Button.Alpha = 1.0f;
                    weeklyGoal4Button.SetTypeface(null, weeklyGoal == 4 ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                }

                if (weeklyGoal5Button != null)
                {
                    weeklyGoal5Button.Alpha = 1.0f;
                    weeklyGoal5Button.SetTypeface(null, weeklyGoal == 5 ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                }

                if (weeklyGoal6Button != null)
                {
                    weeklyGoal6Button.Alpha = 1.0f;
                    weeklyGoal6Button.SetTypeface(null, weeklyGoal == 6 ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                }
            }

            void ApplyWeeklyGoal(int value)
            {
                if (weeklyGoal == value)
                    return;

                weeklyGoal = value;
                prefs?.Edit()?.PutInt("weekly_goal", weeklyGoal)?.Apply();
                UpdateWeeklyGoalButtonState();
                Toast.MakeText(this, $"Weekly goal set to {weeklyGoal}", ToastLength.Short)?.Show();
            }

            UpdateWeeklyGoalButtonState();

            if (weeklyGoal3Button != null)
            {
                weeklyGoal3Button.Click += (s, e) => ApplyWeeklyGoal(3);
            }

            if (weeklyGoal4Button != null)
            {
                weeklyGoal4Button.Click += (s, e) => ApplyWeeklyGoal(4);
            }

            if (weeklyGoal5Button != null)
            {
                weeklyGoal5Button.Click += (s, e) => ApplyWeeklyGoal(5);
            }

            if (weeklyGoal6Button != null)
            {
                weeklyGoal6Button.Click += (s, e) => ApplyWeeklyGoal(6);
            }

            var currentThemeMode = ThemeManager.GetSavedThemeMode(this);
            void UpdateThemeButtonState()
            {
                if (themeLightButton != null)
                {
                    themeLightButton.Alpha = 1.0f;
                    themeLightButton.SetTypeface(null, currentThemeMode == ThemeManager.ThemeModeLight ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                }

                if (themeDarkButton != null)
                {
                    themeDarkButton.Alpha = 1.0f;
                    themeDarkButton.SetTypeface(null, currentThemeMode == ThemeManager.ThemeModeDark ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                }

                if (themeSystemButton != null)
                {
                    themeSystemButton.Alpha = 1.0f;
                    themeSystemButton.SetTypeface(null, currentThemeMode == ThemeManager.ThemeModeSystem ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                }
            }

            void ApplyThemeMode(string mode)
            {
                if (currentThemeMode == mode)
                    return;

                currentThemeMode = mode;
                ThemeManager.SaveThemeMode(this, mode);
                Toast.MakeText(this, "Theme preference saved", ToastLength.Short)?.Show();
                Recreate();
            }

            UpdateThemeButtonState();

            if (themeLightButton != null)
            {
                themeLightButton.Click += (s, e) => ApplyThemeMode(ThemeManager.ThemeModeLight);
            }

            if (themeDarkButton != null)
            {
                themeDarkButton.Click += (s, e) => ApplyThemeMode(ThemeManager.ThemeModeDark);
            }

            if (themeSystemButton != null)
            {
                themeSystemButton.Click += (s, e) => ApplyThemeMode(ThemeManager.ThemeModeSystem);
            }

            if (exportCsvButton != null)
            {
                exportCsvButton.Click += (s, e) =>
                {
                    if (_database == null)
                        return;

                    var csvPath = _database.ExportWorkoutsCsv();
                    Toast.MakeText(this, $"CSV exported: {csvPath}", ToastLength.Long)?.Show();
                };
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
    }
}
