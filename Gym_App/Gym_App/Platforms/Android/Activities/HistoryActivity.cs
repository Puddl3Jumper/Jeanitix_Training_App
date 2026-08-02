using Android.Views;
using Android.Widget;
using Android.Content;
using Android.Graphics;
using System.Globalization;
using Gym_App;
using Gym_App.Data;
using Gym_App.Models;
using Google.Android.Material.Dialog;
using System.Threading;

namespace Gym_App.Activities
{
    [Activity(Label = "Workout History")]
    public class HistoryActivity : Activity
    {
        private enum HistoryRange
        {
            Day,
            Week,
            Month
        }

        private GymDatabase? _database;
        private LinearLayout? _historyContainer;
        private TextView? _dayTab;
        private TextView? _weekTab;
        private TextView? _monthTab;
        private TextView? _historyRangeText;
        private View? _quickAddFab;
        private int _lastScrollY;
        private bool _isFabVisible = true;
        private HistoryRange _selectedRange = HistoryRange.Day;
        private readonly SemaphoreSlim _cloudPullLock = new(1, 1);

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_history);

            _database = new GymDatabase();
            _historyContainer = FindViewById<LinearLayout>(Resource.Id.historyContainer);
            _dayTab = FindViewById<TextView>(Resource.Id.dayTab);
            _weekTab = FindViewById<TextView>(Resource.Id.weekTab);
            _monthTab = FindViewById<TextView>(Resource.Id.monthTab);
            _historyRangeText = FindViewById<TextView>(Resource.Id.historyRangeText);
            _quickAddFab = FindViewById<View>(Resource.Id.quickAddFab);
            var historyScrollView = FindViewById<ScrollView>(Resource.Id.historyScrollView);

            var homeTab = FindViewById<LinearLayout>(Resource.Id.homeTab);
            var diaryTab = FindViewById<LinearLayout>(Resource.Id.diaryTab);
            var workoutTab = FindViewById<LinearLayout>(Resource.Id.workoutTab);
            var profileTab = FindViewById<LinearLayout>(Resource.Id.profileTab);

            if (homeTab != null) homeTab.Selected = false;
            if (diaryTab != null) diaryTab.Selected = true;
            if (workoutTab != null) workoutTab.Selected = false;
            if (profileTab != null) profileTab.Selected = false;
            UpdateBottomNavLabelStyles();

            if (homeTab != null)
            {
                homeTab.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(HomeActivity)));
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

            if (_quickAddFab != null)
            {
                _quickAddFab.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(WorkoutActivity)));
                };
            }

            if (historyScrollView != null)
            {
                historyScrollView.ScrollChange += (s, e) =>
                {
                    var delta = e.ScrollY - _lastScrollY;

                    if (e.ScrollY <= 20)
                    {
                        ShowQuickAddFab();
                    }
                    else if (delta > 8)
                    {
                        HideQuickAddFab();
                    }
                    else if (delta < -8)
                    {
                        ShowQuickAddFab();
                    }

                    _lastScrollY = e.ScrollY;
                };
            }

            if (_dayTab != null)
            {
                _dayTab.Click += (s, e) =>
                {
                    _selectedRange = HistoryRange.Day;
                    UpdateHistoryRangeUi();
                    LoadHistory();
                };
            }

            if (_weekTab != null)
            {
                _weekTab.Click += (s, e) =>
                {
                    _selectedRange = HistoryRange.Week;
                    UpdateHistoryRangeUi();
                    LoadHistory();
                };
            }

            if (_monthTab != null)
            {
                _monthTab.Click += (s, e) =>
                {
                    _selectedRange = HistoryRange.Month;
                    UpdateHistoryRangeUi();
                    LoadHistory();
                };
            }

            UpdateHistoryRangeUi();

            LoadHistory();
            _ = TryPullHistoryAndRefreshAsync();
        }

        protected override void OnResume()
        {
            base.OnResume();
            LoadHistory();
            _ = TryPullHistoryAndRefreshAsync();
        }

        private async Task TryPullHistoryAndRefreshAsync()
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
                    LoadHistory();
                }
                catch
                {
                    // Ignore UI refresh failures.
                }
            });
        }

        private void UpdateHistoryRangeUi()
        {
            ApplyHistoryTabStyle(_dayTab, _selectedRange == HistoryRange.Day);
            ApplyHistoryTabStyle(_weekTab, _selectedRange == HistoryRange.Week);
            ApplyHistoryTabStyle(_monthTab, _selectedRange == HistoryRange.Month);

            if (_historyRangeText != null)
            {
                _historyRangeText.Text = GetRangeDisplayText();
            }
        }

        private void ApplyHistoryTabStyle(TextView? tab, bool isSelected)
        {
            if (tab == null)
                return;

            tab.SetTypeface(null, isSelected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
            tab.SetBackgroundResource(isSelected ? Resource.Drawable.bg_button_primary : Resource.Drawable.bg_log_tab_inactive);
            tab.SetTextColor(new Android.Graphics.Color(GetColor(isSelected ? Resource.Color.color_on_primary : Resource.Color.color_text_secondary)));
        }

        private string GetRangeDisplayText()
        {
            var now = DateTime.Now;

            return _selectedRange switch
            {
                HistoryRange.Day => now.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture),
                HistoryRange.Week => $"{StartOfWeek(now):MMM dd, yyyy} - {StartOfWeek(now).AddDays(6):MMM dd, yyyy}",
                HistoryRange.Month => now.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                _ => now.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)
            };
        }

        private DateTime StartOfWeek(DateTime value)
        {
            var firstDayOfWeek = DayOfWeek.Monday;
            var diff = (7 + (value.DayOfWeek - firstDayOfWeek)) % 7;
            return value.Date.AddDays(-diff);
        }

        private void HideQuickAddFab()
        {
            if (_quickAddFab == null || !_isFabVisible)
                return;

            _quickAddFab.Animate()
                ?.Alpha(0f)
                ?.TranslationY(DpToPx(72))
                ?.SetDuration(160)
                ?.Start();
            _isFabVisible = false;
        }

        private void ShowQuickAddFab()
        {
            if (_quickAddFab == null || _isFabVisible)
                return;

            _quickAddFab.Animate()
                ?.Alpha(1f)
                ?.TranslationY(0f)
                ?.SetDuration(160)
                ?.Start();
            _isFabVisible = true;
        }

        private List<WorkoutSession> FilterHistoryByRange(List<WorkoutSession> history)
        {
            var now = DateTime.Now;

            return _selectedRange switch
            {
                HistoryRange.Day => history.Where(x => x.StartTime.Date == now.Date).ToList(),
                HistoryRange.Week => history.Where(x => x.StartTime.Date >= StartOfWeek(now) && x.StartTime.Date < StartOfWeek(now).AddDays(7)).ToList(),
                HistoryRange.Month => history.Where(x => x.StartTime.Year == now.Year && x.StartTime.Month == now.Month).ToList(),
                _ => history
            };
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

        private void LoadHistory()
        {
            if (_historyContainer == null || _database == null)
                return;

            _historyContainer.RemoveAllViews();

            var history = _database.GetWorkoutHistory();
            var currentWorkout = _database.GetCurrentWorkout();
            if (currentWorkout != null && currentWorkout.Exercises.Any(e => e.Sets.Count > 0 || !string.IsNullOrWhiteSpace(e.CircuitName)))
            {
                history.Insert(0, currentWorkout);
            }

            var allHistory = history
                .OrderByDescending(x => x.StartTime)
                .ToList();

            history = FilterHistoryByRange(allHistory)
                .OrderByDescending(x => x.StartTime)
                .ToList();

            // If the selected range has no items but history exists, show all history instead of a blank state.
            if (history.Count == 0 && allHistory.Count > 0)
            {
                history = allHistory;
            }

            if (history.Count == 0)
            {
                _historyContainer.SetGravity(GravityFlags.Center);

                var emptyStateLayout = new LinearLayout(this)
                {
                    Orientation = Orientation.Vertical
                };
                emptyStateLayout.SetGravity(GravityFlags.CenterHorizontal);
                emptyStateLayout.LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.WrapContent);

                var noDataText = new TextView(this)
                {
                    Text = "No workouts logged yet!",
                    TextSize = 18
                };
                noDataText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
                noDataText.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
                noDataText.Gravity = GravityFlags.Center;

                var subText = new TextView(this)
                {
                    Text = "Track your first workout to see progress here.",
                    TextSize = 14
                };
                subText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.md_theme_onSurfaceVariant)));
                subText.Gravity = GravityFlags.Center;
                var subTextParams = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WrapContent,
                    ViewGroup.LayoutParams.WrapContent);
                subTextParams.TopMargin = DpToPx(8);
                subText.LayoutParameters = subTextParams;

                var startFirstWorkoutButton = new Button(this)
                {
                    Text = "Start First Workout",
                    TextSize = 16
                };
                startFirstWorkoutButton.SetAllCaps(false);
                startFirstWorkoutButton.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Bold), TypefaceStyle.Bold);
                startFirstWorkoutButton.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_on_primary)));
                startFirstWorkoutButton.SetBackgroundResource(Resource.Drawable.bg_button_primary);
                startFirstWorkoutButton.BackgroundTintList = null;
                startFirstWorkoutButton.SetMinHeight((int)Resources.GetDimension(Resource.Dimension.gym_primary_button_height));
                var buttonParams = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.WrapContent);
                buttonParams.TopMargin = DpToPx(16);
                startFirstWorkoutButton.LayoutParameters = buttonParams;
                startFirstWorkoutButton.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(WorkoutActivity)));
                };

                emptyStateLayout.AddView(noDataText);
                emptyStateLayout.AddView(subText);
                emptyStateLayout.AddView(startFirstWorkoutButton);
                _historyContainer.AddView(emptyStateLayout);
                return;
            }

            _historyContainer.SetGravity(GravityFlags.Top);

            for (var index = 0; index < history.Count; index++)
            {
                var workout = history[index];

                var timelineRow = new LinearLayout(this)
                {
                    Orientation = Orientation.Horizontal
                };
                var rowParams = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.WrapContent);
                rowParams.SetMargins(0, 0, 0, 12);
                timelineRow.LayoutParameters = rowParams;

                var timelineColumn = new LinearLayout(this)
                {
                    Orientation = Orientation.Vertical
                };
                var timelineParams = new LinearLayout.LayoutParams(DpToPx(24), ViewGroup.LayoutParams.MatchParent);
                timelineParams.SetMargins(0, 0, DpToPx(12), 0);
                timelineColumn.LayoutParameters = timelineParams;
                timelineColumn.SetGravity(GravityFlags.Top | GravityFlags.CenterHorizontal);

                var node = CreateTimelineNode();
                timelineColumn.AddView(node);

                if (index < history.Count - 1)
                {
                    var line = CreateTimelineLine();
                    timelineColumn.AddView(line);
                }

                var workoutCard = CreateWorkoutCard(workout, 0, index + 1);

                timelineRow.AddView(timelineColumn);
                timelineRow.AddView(workoutCard);
                _historyContainer.AddView(timelineRow);

                if (index < history.Count - 1)
                {
                    var divider = new View(this);
                    var dividerParams = new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MatchParent,
                        DpToPx(1));
                    dividerParams.SetMargins(DpToPx(36), 0, 0, DpToPx(12));
                    divider.LayoutParameters = dividerParams;
                    divider.SetBackgroundColor(new Android.Graphics.Color(GetColor(Resource.Color.md_theme_outline)));
                    _historyContainer.AddView(divider);
                }
            }
        }

        private LinearLayout CreateWorkoutCard(WorkoutSession workout, int bottomMarginDp, int sessionNumber)
        {
            var workoutCard = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };
            workoutCard.SetClipToPadding(false);
            workoutCard.SetClipChildren(false);

            var layoutParams = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent);
            if (bottomMarginDp > 0)
            {
                layoutParams.SetMargins(0, 0, 0, DpToPx(bottomMarginDp));
            }
            workoutCard.LayoutParameters = layoutParams;
            workoutCard.SetBackgroundResource(Resource.Drawable.bg_card_today_outer);
            workoutCard.SetPadding(DpToPx(16), DpToPx(16), DpToPx(16), DpToPx(16));

            var headerRow = new LinearLayout(this)
            {
                Orientation = Orientation.Horizontal
            };
            headerRow.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent);
            headerRow.SetGravity(GravityFlags.CenterVertical);

            var leftColumn = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };
            leftColumn.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);

            var rightColumn = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };
            rightColumn.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.WrapContent);
            rightColumn.SetGravity(GravityFlags.End);

            var titleText = new TextView(this)
            {
                Text = workout.IsCompleted ? $"Session {sessionNumber}" : $"Session {sessionNumber} · In Progress",
                TextSize = 18
            };
            titleText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_primary)));
            titleText.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);

            var durationText = new TextView(this)
            {
                Text = GetDurationDisplay(workout),
                TextSize = 14
            };
            durationText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
            durationText.SetTypeface(null, TypefaceStyle.Bold);
            durationText.SetBackgroundResource(Resource.Drawable.bg_log_tab_inactive);
            durationText.SetPadding(DpToPx(14), DpToPx(6), DpToPx(14), DpToPx(6));

            var durationLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.WrapContent);
            durationLp.SetMargins(0, 0, 0, 0);
            durationText.LayoutParameters = durationLp;

            leftColumn.AddView(titleText);
            rightColumn.AddView(durationText);

            headerRow.AddView(leftColumn);
            headerRow.AddView(rightColumn);
            workoutCard.AddView(headerRow);

            foreach (var exercise in workout.Exercises)
            {
                if (exercise.Exercise == null)
                    continue;

                var exerciseName = exercise.Exercise.Name;
                var setCount = exercise.Sets.Count;
                var totalReps = exercise.Sets.Sum(s => s.Reps);
                var maxWeight = setCount == 0 ? 0 : exercise.Sets.Max(s => s.Weight);
                var unit = exercise.Sets.FirstOrDefault()?.WeightUnit ?? "kg";

                var exerciseNameText = new TextView(this)
                {
                    Text = $"{GetExerciseIcon(exerciseName)}  {exerciseName}",
                    TextSize = 16
                };
                exerciseNameText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
                exerciseNameText.SetTypeface(null, TypefaceStyle.Bold);
                exerciseNameText.SetPadding(DpToPx(4), DpToPx(8), 0, 0);
                workoutCard.AddView(exerciseNameText);

                var repInfoText = new TextView(this)
                {
                    Text = $"{setCount} set{(setCount == 1 ? string.Empty : "s")} · {totalReps} reps · {maxWeight:0.#} {unit}",
                    TextSize = 15
                };
                repInfoText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_secondary)));
                repInfoText.SetTypeface(null, TypefaceStyle.Bold);
                repInfoText.SetPadding(DpToPx(20), DpToPx(4), 0, 0);
                workoutCard.AddView(repInfoText);
            }

            var actionsRow = new LinearLayout(this)
            {
                Orientation = Orientation.Horizontal
            };
            actionsRow.SetClipToPadding(false);
            actionsRow.SetClipChildren(false);
            actionsRow.SetPadding(0, DpToPx(10), 0, DpToPx(4));

            var pillHeightPx = Resources.GetDimensionPixelSize(Resource.Dimension.gym_primary_button_height);

            var editButton = LayoutInflater
                .From(this)
                .Inflate(Resource.Layout.layout_edit_button, actionsRow, false) as Button;
            if (editButton != null)
            editButton.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(WorkoutActivity));
                intent.PutExtra("workoutId", workout.Id);
                StartActivity(intent);
            };

            var moreButton = new TextView(this)
            {
                Text = "⋮",
                TextSize = 22
            };
            moreButton.SetBackgroundResource(Resource.Drawable.bg_log_tab_inactive);
            moreButton.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_secondary)));
            moreButton.SetTypeface(null, TypefaceStyle.Bold);
            moreButton.Gravity = GravityFlags.Center;
            moreButton.Clickable = true;
            moreButton.Focusable = true;
            moreButton.SetPadding(0, 0, 0, 0);
            moreButton.SetIncludeFontPadding(false);
            var moreParams = new LinearLayout.LayoutParams(DpToPx(52), ViewGroup.LayoutParams.WrapContent);
            moreParams.Width = pillHeightPx;
            moreParams.Height = pillHeightPx;
            moreParams.SetMargins(DpToPx(10), 0, 0, 0);
            moreButton.LayoutParameters = moreParams;
            moreButton.Click += (s, e) =>
            {
                ConfirmDeleteWorkout(workout.Id);
            };

            if (editButton != null)
            {
                editButton.Enabled = workout.Id > 0;
                actionsRow.AddView(editButton);
            }
            actionsRow.AddView(moreButton);
            workoutCard.AddView(actionsRow);

            return workoutCard;
        }

        private string GetDurationDisplay(WorkoutSession workout)
        {
            if (!workout.IsCompleted)
            {
                var inProgressDuration = DateTime.Now - workout.StartTime;
                if (inProgressDuration < TimeSpan.Zero)
                    inProgressDuration = TimeSpan.Zero;

                if (inProgressDuration.TotalHours >= 1)
                    return inProgressDuration.ToString("hh\\:mm\\:ss");

                return inProgressDuration.ToString("mm\\:ss");
            }

            if (workout.Duration <= TimeSpan.Zero)
                return "No duration recorded";

            if (workout.Duration.TotalHours >= 1)
                return workout.Duration.ToString("hh\\:mm\\:ss");

            return workout.Duration.ToString("mm\\:ss");
        }

        private string GetWorkoutMetricText(WorkoutSession workout)
        {
            var totalSets = workout.Exercises.Sum(x => x.Sets.Count);
            var primaryExercise = workout.Exercises.FirstOrDefault(x => x.Exercise != null)?.Exercise?.Name ?? "No exercise";
            return $"{totalSets} {(totalSets == 1 ? "set" : "sets")} • {primaryExercise}";
        }

        private string GetExerciseIcon(string exerciseName)
        {
            var lower = (exerciseName ?? string.Empty).ToLowerInvariant();

            if (ContainsAny(lower, "pull-up", "pull up", "pullup", "chin-up", "chin up", "chinup"))
                return "🧗";

            if (ContainsAny(lower, "squat", "lunge", "leg press", "calf", "hamstring", "glute", "quad"))
                return "🦵";

            if (ContainsAny(lower, "deadlift", "rdl", "romanian deadlift"))
                return "🏋️";

            if (ContainsAny(lower, "row", "lat pulldown", "pulldown", "back"))
                return "🚣";

            if (ContainsAny(lower, "bicep", "curl", "tricep", "dip", "arm"))
                return "💪";

            if (ContainsAny(lower, "press", "bench", "push-up", "push up", "pushup", "chest", "fly"))
                return "🏋️";

            if (ContainsAny(lower, "plank", "crunch", "sit-up", "sit up", "situp", "abs", "core", "russian twist"))
                return "🧘";

            if (ContainsAny(lower, "run", "jog", "cardio", "treadmill", "bike", "cycle", "jump rope"))
                return "🏃";

            return "🏋️";
        }

        private static bool ContainsAny(string source, params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                if (source.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private View CreateTimelineNode()
        {
            var node = new View(this);
            var nodeParams = new LinearLayout.LayoutParams(DpToPx(10), DpToPx(10));
            nodeParams.SetMargins(0, DpToPx(10), 0, DpToPx(8));
            node.LayoutParameters = nodeParams;
            node.SetBackgroundColor(new Android.Graphics.Color(GetColor(Resource.Color.color_primary)));
            return node;
        }

        private View CreateTimelineLine()
        {
            var line = new View(this);
            var lineParams = new LinearLayout.LayoutParams(DpToPx(2), ViewGroup.LayoutParams.MatchParent);
            line.LayoutParameters = lineParams;
            line.SetBackgroundColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_secondary)));
            return line;
        }

        private int DpToPx(int dp)
        {
            return (int)(dp * Resources.DisplayMetrics.Density);
        }

        private void ShowEditWorkoutDialog(WorkoutSession workout)
        {
            if (_database == null)
                return;

            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            layout.SetPadding(32, 16, 32, 16);

            var nameInput = new EditText(this)
            {
                Hint = "Workout Name",
                Text = workout.Name
            };

            var dateInput = new EditText(this)
            {
                Focusable = false,
                Clickable = true,
                Text = workout.StartTime.ToString("yyyy-MM-dd")
            };

            DateTime selectedDate = workout.StartTime.Date;
            dateInput.Click += (s, e) =>
            {
                var datePicker = new DatePicker(this);
                datePicker.UpdateDate(selectedDate.Year, selectedDate.Month - 1, selectedDate.Day);

                var dateDialog = new MaterialAlertDialogBuilder(this)
                    .SetTitle("Select Date")
                    .SetView(datePicker)
                    .SetPositiveButton("OK", (sender, args) =>
                    {
                        selectedDate = new DateTime(datePicker.Year, datePicker.Month + 1, datePicker.DayOfMonth);
                        dateInput.Text = selectedDate.ToString("yyyy-MM-dd");
                    })
                    .SetNegativeButton("Cancel", (sender, args) => { })
                    .Show();

                DialogThemeHelper.StyleShownDialog(this, dateDialog);
            };

            var notesInput = new EditText(this)
            {
                Hint = "Workout Notes (optional)",
                Text = workout.Notes ?? string.Empty
            };

            DialogThemeHelper.StyleInput(this, nameInput);
            DialogThemeHelper.StyleInput(this, dateInput);
            DialogThemeHelper.StyleInput(this, notesInput);

            layout.AddView(nameInput);
            layout.AddView(dateInput);
            layout.AddView(notesInput);

            var dialog = new MaterialAlertDialogBuilder(this);
            dialog.SetTitle("Edit Workout");
            dialog.SetView(layout);
            dialog.SetPositiveButton("Save", (s, e) =>
            {
                _database.UpdateWorkoutSession(workout.Id, nameInput.Text, selectedDate, notesInput.Text);
                LoadHistory();
                Toast.MakeText(this, "Workout updated", ToastLength.Short)?.Show();
            });
            dialog.SetNegativeButton("Cancel", (s, e) => { });
            var shownDialog = dialog.Show();
            DialogThemeHelper.StyleShownDialog(this, shownDialog);
        }

        private void ConfirmDeleteWorkout(int workoutId)
        {
            if (_database == null)
                return;

            DialogThemeHelper.ShowPillConfirmationDialog(
                this,
                title: "Delete Workout",
                message: "This will permanently delete this workout.",
                positiveText: "Delete",
                onPositive: () =>
            {
                _database.DeleteWorkoutSession(workoutId);
                LoadHistory();
                Toast.MakeText(this, "Workout deleted", ToastLength.Short)?.Show();
            });
        }
    }
}
