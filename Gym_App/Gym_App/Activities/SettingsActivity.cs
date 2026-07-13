using Android.App;
using Android.Content;
using Android.Text;
using Android.Text.Style;
using Android.Widget;
using Android.Graphics;
using Gym_App.Data;
using Gym_App.Receivers;
using Gym_App.Services;
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
                var versionName = ReleaseInfo.GetDisplayVersion(this);
                var versionLine = GetString(
                    Resource.String.settings_release_text,
                    new Java.Lang.Object[]
                    {
                        new Java.Lang.String(versionName)
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

            // ── Daily Reminder ────────────────────────────────────────────────
            var reminderPrefs = GetSharedPreferences("reminder_prefs", FileCreationMode.Private);
            var reminderSwitch   = FindViewById<Switch>(Resource.Id.reminderEnabledSwitch);
            var reminderPreviewButton = FindViewById<Button>(Resource.Id.reminderPreviewButton);
            var reminderTimeText = FindViewById<TextView>(Resource.Id.reminderTimeText);
            var rdMon = FindViewById<TextView>(Resource.Id.reminderDayMon);
            var rdTue = FindViewById<TextView>(Resource.Id.reminderDayTue);
            var rdWed = FindViewById<TextView>(Resource.Id.reminderDayWed);
            var rdThu = FindViewById<TextView>(Resource.Id.reminderDayThu);
            var rdFri = FindViewById<TextView>(Resource.Id.reminderDayFri);
            var rdSat = FindViewById<TextView>(Resource.Id.reminderDaySat);
            var rdSun = FindViewById<TextView>(Resource.Id.reminderDaySun);

            var reminderEnabled  = reminderPrefs?.GetBoolean("enabled", false) ?? false;
            var reminderDaysMask = reminderPrefs?.GetInt("days_mask", ReminderScheduler.DefaultDaysMask) ?? ReminderScheduler.DefaultDaysMask;
            var reminderHour     = reminderPrefs?.GetInt("hour", 7) ?? 7;
            var reminderMinute   = reminderPrefs?.GetInt("minute", 0) ?? 0;

            if (reminderSwitch != null)
            {
                reminderSwitch.Checked = reminderEnabled;

                // Gold track when on, dim when off; white thumb always
                var gold = new Color(GetColor(Resource.Color.color_primary));
                var dimTrack = new Color(0x44, 0x44, 0x55, 0xFF);
                var trackStates = new Android.Content.Res.ColorStateList(
                    new[] { new[] { Android.Resource.Attribute.StateChecked }, Array.Empty<int>() },
                    new[] { (int)gold, (int)dimTrack });
                var thumbStates = new Android.Content.Res.ColorStateList(
                    new[] { new[] { Android.Resource.Attribute.StateChecked }, Array.Empty<int>() },
                    new[] { (int)Color.White, (int)Color.White });
                reminderSwitch.TrackTintList = trackStates;
                reminderSwitch.ThumbTintList = thumbStates;
            }

            if (reminderTimeText != null)
                reminderTimeText.Text = $"{reminderHour:D2}:{reminderMinute:D2}";

            if (reminderPreviewButton != null)
            {
                reminderPreviewButton.Click += (s, e) =>
                {
                    // Use StartService (not StartForegroundService) for preview
                    // because the app is already in the foreground.
                    var svcIntent = new Intent(this, typeof(ReminderService));
                    svcIntent.PutExtra("preview", true);
                    StartService(svcIntent);
                    Toast.MakeText(this, "Playing preview…", ToastLength.Short)?.Show();
                };
            }

            void ApplyDayStyle(TextView? btn, bool selected)
            {
                if (btn == null) return;
                btn.SetBackgroundResource(selected ? Resource.Drawable.bg_day_circle_active : Resource.Drawable.bg_day_circle_inactive);
                btn.SetTextColor(new Color(GetColor(selected ? Resource.Color.color_on_primary : Resource.Color.color_text_secondary)));
            }

            void RefreshDayButtons()
            {
                ApplyDayStyle(rdMon, ReminderScheduler.IsDaySelected(reminderDaysMask, DayOfWeek.Monday));
                ApplyDayStyle(rdTue, ReminderScheduler.IsDaySelected(reminderDaysMask, DayOfWeek.Tuesday));
                ApplyDayStyle(rdWed, ReminderScheduler.IsDaySelected(reminderDaysMask, DayOfWeek.Wednesday));
                ApplyDayStyle(rdThu, ReminderScheduler.IsDaySelected(reminderDaysMask, DayOfWeek.Thursday));
                ApplyDayStyle(rdFri, ReminderScheduler.IsDaySelected(reminderDaysMask, DayOfWeek.Friday));
                ApplyDayStyle(rdSat, ReminderScheduler.IsDaySelected(reminderDaysMask, DayOfWeek.Saturday));
                ApplyDayStyle(rdSun, ReminderScheduler.IsDaySelected(reminderDaysMask, DayOfWeek.Sunday));
            }

            RefreshDayButtons();

            void SaveAndReschedule()
            {
                reminderPrefs?.Edit()
                    ?.PutBoolean("enabled", reminderEnabled)
                    ?.PutInt("days_mask", reminderDaysMask)
                    ?.PutInt("hour", reminderHour)
                    ?.PutInt("minute", reminderMinute)
                    ?.Apply();

                ScheduleOrCancelReminders(reminderEnabled, reminderDaysMask, reminderHour, reminderMinute);
            }

            if (reminderSwitch != null)
            {
                reminderSwitch.CheckedChange += (s, e) =>
                {
                    reminderEnabled = e.IsChecked;
                    SaveAndReschedule();
                    Toast.MakeText(this, reminderEnabled ? "Reminder enabled" : "Reminder disabled", ToastLength.Short)?.Show();
                };
            }

            void ToggleDay(DayOfWeek day, int bit)
            {
                if ((reminderDaysMask & bit) != 0)
                    reminderDaysMask &= ~bit;
                else
                    reminderDaysMask |= bit;
                RefreshDayButtons();
                SaveAndReschedule();
            }

            if (rdMon != null) rdMon.Click += (s, e) => ToggleDay(DayOfWeek.Monday,    ReminderScheduler.MondayBit);
            if (rdTue != null) rdTue.Click += (s, e) => ToggleDay(DayOfWeek.Tuesday,   ReminderScheduler.TuesdayBit);
            if (rdWed != null) rdWed.Click += (s, e) => ToggleDay(DayOfWeek.Wednesday, ReminderScheduler.WednesdayBit);
            if (rdThu != null) rdThu.Click += (s, e) => ToggleDay(DayOfWeek.Thursday,  ReminderScheduler.ThursdayBit);
            if (rdFri != null) rdFri.Click += (s, e) => ToggleDay(DayOfWeek.Friday,    ReminderScheduler.FridayBit);
            if (rdSat != null) rdSat.Click += (s, e) => ToggleDay(DayOfWeek.Saturday,  ReminderScheduler.SaturdayBit);
            if (rdSun != null) rdSun.Click += (s, e) => ToggleDay(DayOfWeek.Sunday,    ReminderScheduler.SundayBit);

            if (reminderTimeText != null)
            {
                reminderTimeText.Click += (s, e) =>
                {
                    var dlg = new Android.App.TimePickerDialog(
                        this,
                        (_, args) =>
                        {
                            reminderHour   = args.HourOfDay;
                            reminderMinute = args.Minute;
                            reminderTimeText.Text = $"{reminderHour:D2}:{reminderMinute:D2}";
                            SaveAndReschedule();
                        },
                        reminderHour, reminderMinute, true);
                    dlg.Show();
                };
            }
        }

        private void ScheduleOrCancelReminders(bool enabled, int daysMask, int hour, int minute)
        {
            var alarmManager = (AlarmManager?)GetSystemService(AlarmService);
            if (alarmManager == null) return;

            // Cancel all existing per-day alarms first
            foreach (DayOfWeek day in System.Enum.GetValues<DayOfWeek>())
            {
                var cancelIntent = BuildReminderIntent((int)day);
                var cancelPi = PendingIntent.GetBroadcast(this, (int)day, cancelIntent,
                    PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
                if (cancelPi != null)
                    alarmManager.Cancel(cancelPi);
            }

            if (!enabled || daysMask == 0) return;

            var triggers = ReminderScheduler.GetScheduledTriggers(daysMask, hour, minute, DateTime.Now);
            long weekMs = 7L * 24 * 60 * 60 * 1000;
            foreach (var (day, triggerUtc) in triggers)
            {
                var pi = PendingIntent.GetBroadcast(this, (int)day, BuildReminderIntent((int)day),
                    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
                if (pi == null) continue;
                var triggerMs = new DateTimeOffset(triggerUtc).ToUnixTimeMilliseconds();
                alarmManager.SetRepeating(AlarmType.RtcWakeup, triggerMs, weekMs, pi);
            }
        }

        private Intent BuildReminderIntent(int dayCode)
        {
            var intent = new Intent("com.leiyu.GymJournal.ACTION_DAILY_REMINDER");
            intent.SetClass(this, typeof(ReminderReceiver));
            intent.PutExtra("day_code", dayCode);
            return intent;
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
