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
                if (unitKgButton != null)
                {
                    unitKgButton.Alpha = currentUnit == "kg" ? 1.0f : 0.65f;
                }

                if (unitLbButton != null)
                {
                    unitLbButton.Alpha = currentUnit == "lb" ? 1.0f : 0.65f;
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

            var currentThemeMode = ThemeManager.GetSavedThemeMode(this);
            void UpdateThemeButtonState()
            {
                if (themeLightButton != null)
                {
                    themeLightButton.Alpha = currentThemeMode == ThemeManager.ThemeModeLight ? 1.0f : 0.65f;
                }

                if (themeDarkButton != null)
                {
                    themeDarkButton.Alpha = currentThemeMode == ThemeManager.ThemeModeDark ? 1.0f : 0.65f;
                }

                if (themeSystemButton != null)
                {
                    themeSystemButton.Alpha = currentThemeMode == ThemeManager.ThemeModeSystem ? 1.0f : 0.65f;
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
