using Android.Content;
using Android.Text;
using Android.Text.Style;
using Android.Widget;
using Android.Graphics;
using Gym_App.Data;
using Java.Lang;

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

            // Weight unit selector removed from UI for now.
            var weeklyGoal3Button = FindViewById<TextView>(Resource.Id.weeklyGoal3Button);
            var weeklyGoal4Button = FindViewById<TextView>(Resource.Id.weeklyGoal4Button);
            var weeklyGoal5Button = FindViewById<TextView>(Resource.Id.weeklyGoal5Button);
            var weeklyGoal6Button = FindViewById<TextView>(Resource.Id.weeklyGoal6Button);
            var themeLightButton = FindViewById<TextView>(Resource.Id.themeLightButton);
            var themeDarkButton = FindViewById<TextView>(Resource.Id.themeDarkButton);
            var themeSystemButton = FindViewById<TextView>(Resource.Id.themeSystemButton);
            var fontSansButton = FindViewById<TextView>(Resource.Id.fontSansButton);
            var fontSerifButton = FindViewById<TextView>(Resource.Id.fontSerifButton);
            var fontMonoButton = FindViewById<TextView>(Resource.Id.fontMonoButton);
            var logoutButton = FindViewById<TextView>(Resource.Id.logoutButton);
            var exportCsvButton = FindViewById<TextView>(Resource.Id.exportCsvButton);
            var appVersionText = FindViewById<TextView>(Resource.Id.appVersionText);

            if (appVersionText != null)
            {
                var (versionName, versionCode) = ReleaseInfo.GetAppVersion(this);
                var versionLine = GetString(
                    Resource.String.settings_release_text,
                    new Java.Lang.Object[]
                    {
                        new Java.Lang.String(versionName),
                        Long.ValueOf(versionCode)
                    });

                var spannable = new SpannableString(versionLine);

                // Bold the version number section: "v1.x.x (xxxx)"
                var marker = $"v{versionName}";
                var start = versionLine.IndexOf(marker, StringComparison.Ordinal);
                if (start < 0)
                {
                    start = versionLine.IndexOf(versionName, StringComparison.Ordinal);
                }

                if (start >= 0)
                {
                    spannable.SetSpan(
                        new StyleSpan(TypefaceStyle.Bold),
                        start,
                        versionLine.Length,
                        SpanTypes.ExclusiveExclusive);
                }

                appVersionText.SetText(spannable, TextView.BufferType.Spannable);
            }

            var weeklyGoal = prefs?.GetInt("weekly_goal", 4) ?? 4;

            void UpdateWeeklyGoalButtonState()
            {
                void ApplyGoalStyle(TextView? button, bool isSelected)
                {
                    if (button == null)
                        return;

                    button.Alpha = 1.0f;
                    button.SetTypeface(null, TypefaceStyle.Bold);
                    button.SetBackgroundResource(isSelected ? Resource.Drawable.bg_log_tab_active : Resource.Drawable.bg_log_tab_inactive);
                    button.SetTextColor(new Color(GetColor(isSelected ? Resource.Color.color_on_primary : Resource.Color.color_text_secondary)));
                }

                ApplyGoalStyle(weeklyGoal3Button, weeklyGoal == 3);
                ApplyGoalStyle(weeklyGoal4Button, weeklyGoal == 4);
                ApplyGoalStyle(weeklyGoal5Button, weeklyGoal == 5);
                ApplyGoalStyle(weeklyGoal6Button, weeklyGoal == 6);
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
                void ApplyThemeStyle(TextView? button, bool isSelected)
                {
                    if (button == null)
                        return;

                    button.Alpha = 1.0f;
                    button.SetTypeface(null, TypefaceStyle.Bold);
                    button.SetBackgroundResource(isSelected ? Resource.Drawable.bg_log_tab_active : Resource.Drawable.bg_log_tab_inactive);
                    button.SetTextColor(new Color(GetColor(isSelected ? Resource.Color.color_on_primary : Resource.Color.color_text_secondary)));
                }

                ApplyThemeStyle(themeLightButton, currentThemeMode == ThemeManager.ThemeModeLight);
                ApplyThemeStyle(themeDarkButton, currentThemeMode == ThemeManager.ThemeModeDark);
                ApplyThemeStyle(themeSystemButton, currentThemeMode == ThemeManager.ThemeModeSystem);
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

            var currentFontMode = ThemeManager.GetSavedFontMode(this);
            void UpdateFontButtonState()
            {
                void ApplyFontStyle(TextView? button, bool isSelected)
                {
                    if (button == null)
                        return;

                    button.Alpha = 1.0f;
                    button.SetTypeface(null, TypefaceStyle.Bold);
                    button.SetBackgroundResource(isSelected ? Resource.Drawable.bg_log_tab_active : Resource.Drawable.bg_log_tab_inactive);
                    button.SetTextColor(new Color(GetColor(isSelected ? Resource.Color.color_on_primary : Resource.Color.color_text_secondary)));
                }

                ApplyFontStyle(fontSansButton, currentFontMode == ThemeManager.FontModeSans);
                ApplyFontStyle(fontSerifButton, currentFontMode == ThemeManager.FontModeSerif);
                ApplyFontStyle(fontMonoButton, currentFontMode == ThemeManager.FontModeMono);
            }

            void ApplyFontMode(string mode)
            {
                if (currentFontMode == mode)
                    return;

                currentFontMode = mode;
                ThemeManager.SaveFontMode(this, mode);
                Toast.MakeText(this, "Font preference saved", ToastLength.Short)?.Show();
                Recreate();
            }

            UpdateFontButtonState();

            if (fontSansButton != null)
            {
                fontSansButton.Click += (s, e) => ApplyFontMode(ThemeManager.FontModeSans);
            }

            if (fontSerifButton != null)
            {
                fontSerifButton.Click += (s, e) => ApplyFontMode(ThemeManager.FontModeSerif);
            }

            if (fontMonoButton != null)
            {
                fontMonoButton.Click += (s, e) => ApplyFontMode(ThemeManager.FontModeMono);
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

            if (logoutButton != null)
            {
                logoutButton.Click += (s, e) =>
                {
                    AuthSessionStore.Clear(this);

                    var intent = new Intent(this, typeof(LoginActivity));
                    intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
                    StartActivity(intent);
                    Finish();
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
