using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Gym_App.Data;
using Gym_App.Models;

namespace Gym_App.Activities
{
    [Activity(Label = "Training")]
    public class WorkoutActivity : Activity
    {
        private GymDatabase? _database;
        private WorkoutSession? _currentWorkout;

        private TextView? _focusValueText;
        private TextView? _chestBarTimeText;
        private TextView? _tricepsBarTimeText;
        private TextView? _shouldersBarTimeText;
        private TextView? _muscleBar1LabelText;
        private TextView? _muscleBar2LabelText;
        private TextView? _muscleBar3LabelText;
        private View? _chestBarFill;
        private View? _tricepsBarFill;
        private View? _shouldersBarFill;

        private TextView? _currentExerciseNameText;
        private TextView? _currentElapsedTimeText;
        private TextView? _set1RepsText;
        private TextView? _set2RepsText;
        private TextView? _set3RepsText;

        private Button? _startTimerButton;
        private Button? _finishWorkoutButton;

        private readonly Dictionary<string, int> _muscleSecondsToday = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Chest"] = 0,
            ["Back"] = 0,
            ["Legs"] = 0,
            ["Biceps"] = 0,
            ["Triceps"] = 0,
            ["Shoulders"] = 0,
            ["Core"] = 0
        };

        private const string MuscleTimePrefsName = "muscle_time";

        private readonly Dictionary<string, string> _exerciseByMuscle = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Chest"] = "Bench Press",
            ["Back"] = "Lat Pulldown",
            ["Legs"] = "Barbell Squat",
            ["Shoulders"] = "Overhead Press",
            ["Biceps"] = "Barbell Curl",
            ["Triceps"] = "Cable Pushdown",
            ["Core"] = "Plank"
        };

        private string _selectedFocus = "Chest";
        private bool _timerRunning;
        private TimeSpan _elapsed = TimeSpan.Zero;
        private System.Threading.Timer? _elapsedTimer;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_workout);

            _database = new GymDatabase();
            var workoutId = Intent?.GetIntExtra("workoutId", -1) ?? -1;
            _currentWorkout = workoutId > 0
                ? _database.GetWorkoutSession(workoutId) ?? _database.GetCurrentWorkout() ?? _database.CreateWorkoutSession("Training")
                : _database.GetCurrentWorkout() ?? _database.CreateWorkoutSession("Training");

            BindViews();
            BindTopActions();
            BindWorkoutActions();
            BindBottomNav();
            LoadTodayMuscleTimes();

            RefreshScreen();
        }

        protected override void OnDestroy()
        {
            SaveTodayMuscleTimes();
            _elapsedTimer?.Dispose();
            base.OnDestroy();
        }

        protected override void OnResume()
        {
            base.OnResume();

            if (_currentWorkout != null)
            {
                _currentWorkout = _database?.GetWorkoutSession(_currentWorkout.Id) ?? _currentWorkout;
            }

            RefreshScreen();
        }

        private void BindViews()
        {
            _focusValueText = FindViewById<TextView>(Resource.Id.focusValueText);
            _chestBarTimeText = FindViewById<TextView>(Resource.Id.chestBarTimeText);
            _tricepsBarTimeText = FindViewById<TextView>(Resource.Id.tricepsBarTimeText);
            _shouldersBarTimeText = FindViewById<TextView>(Resource.Id.shouldersBarTimeText);
            _muscleBar1LabelText = FindViewById<TextView>(Resource.Id.muscleBar1LabelText);
            _muscleBar2LabelText = FindViewById<TextView>(Resource.Id.muscleBar2LabelText);
            _muscleBar3LabelText = FindViewById<TextView>(Resource.Id.muscleBar3LabelText);
            _chestBarFill = FindViewById(Resource.Id.chestBarFill);
            _tricepsBarFill = FindViewById(Resource.Id.tricepsBarFill);
            _shouldersBarFill = FindViewById(Resource.Id.shouldersBarFill);

            _currentExerciseNameText = FindViewById<TextView>(Resource.Id.currentExerciseNameText);
            _currentElapsedTimeText = FindViewById<TextView>(Resource.Id.currentElapsedTimeText);
            _set1RepsText = FindViewById<TextView>(Resource.Id.set1RepsText);
            _set2RepsText = FindViewById<TextView>(Resource.Id.set2RepsText);
            _set3RepsText = FindViewById<TextView>(Resource.Id.set3RepsText);

            _startTimerButton = FindViewById<Button>(Resource.Id.startTimerButton);
            _finishWorkoutButton = FindViewById<Button>(Resource.Id.finishWorkoutButton);
        }

        private void BindTopActions()
        {
            var historyButton = FindViewById<ImageButton>(Resource.Id.trainingHistoryButton);
            if (historyButton != null)
            {
                historyButton.Click += (s, e) => StartActivity(new Intent(this, typeof(HistoryActivity)));
            }

            var logExerciseFab = FindViewById<View>(Resource.Id.logExerciseFab);
            if (logExerciseFab != null)
            {
                logExerciseFab.Click += (s, e) =>
                {
                    var intent = new Intent(this, typeof(ExerciseLibraryActivity));
                    if (_currentWorkout != null)
                    {
                        intent.PutExtra("workoutId", _currentWorkout.Id);
                    }

                    StartActivity(intent);
                };
            }
        }

        private void BindWorkoutActions()
        {
            if (_startTimerButton != null)
            {
                _startTimerButton.Click += (s, e) => ToggleTimer();
            }

            if (_finishWorkoutButton != null)
            {
                _finishWorkoutButton.Click += (s, e) =>
                {
                    _elapsedTimer?.Dispose();
                    _elapsedTimer = null;
                    _timerRunning = false;

                    if (_currentWorkout != null)
                    {
                        _database?.CompleteWorkout(_currentWorkout.Id);
                    }

                    SaveTodayMuscleTimes();

                    Toast.MakeText(this, "Workout finished", ToastLength.Short)?.Show();
                    StartActivity(new Intent(this, typeof(HomeActivity)));
                    Finish();
                };
            }
        }

        private void ToggleTimer()
        {
            if (_timerRunning)
            {
                _elapsedTimer?.Dispose();
                _elapsedTimer = null;
                _timerRunning = false;
                SaveTodayMuscleTimes();
                if (_startTimerButton != null)
                    _startTimerButton.Text = "Start Timer";
                return;
            }

            _timerRunning = true;
            if (_startTimerButton != null)
                _startTimerButton.Text = "Pause Timer";

            _elapsedTimer?.Dispose();
            _elapsedTimer = new System.Threading.Timer(_ =>
            {
                RunOnUiThread(() =>
                {
                    _elapsed = _elapsed.Add(TimeSpan.FromSeconds(1));
                    AddSecondToSelectedMuscle();
                    UpdateElapsedText();
                    UpdateMuscleTimeViews();
                });
            }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private void RefreshScreen()
        {
            var plannedGroups = GetPlannedMuscleGroups();
            if (plannedGroups.Length > 0)
            {
                _selectedFocus = plannedGroups[0];
            }

            UpdateMuscleTimeViews();

            UpdateTrainingHeader();
            UpdateCurrentExerciseCard();
            UpdateElapsedText();
            UpdateBottomNavSelection();
        }

        private void UpdateTrainingHeader()
        {
            if (_focusValueText != null)
                _focusValueText.Text = "Workout Focus";
        }

        private void UpdateMuscleTimeViews()
        {
            var plannedGroups = GetPlannedMuscleGroups();
            var group1 = plannedGroups[0];
            var group2 = plannedGroups[1];
            var group3 = plannedGroups[2];

            var seconds1 = GetMuscleSeconds(group1);
            var seconds2 = GetMuscleSeconds(group2);
            var seconds3 = GetMuscleSeconds(group3);

            var minutes1 = ToMinutes(seconds1);
            var minutes2 = ToMinutes(seconds2);
            var minutes3 = ToMinutes(seconds3);

            _muscleBar1LabelText?.SetText(group1, TextView.BufferType.Normal);
            _muscleBar2LabelText?.SetText(group2, TextView.BufferType.Normal);
            _muscleBar3LabelText?.SetText(group3, TextView.BufferType.Normal);

            _chestBarTimeText?.SetText($"{minutes1}m", TextView.BufferType.Normal);
            _tricepsBarTimeText?.SetText($"{minutes2}m", TextView.BufferType.Normal);
            _shouldersBarTimeText?.SetText($"{minutes3}m", TextView.BufferType.Normal);

            var maxSeconds = Math.Max(1, Math.Max(seconds1, Math.Max(seconds2, seconds3)));
            UpdateMuscleBarFill(_chestBarFill, seconds1, maxSeconds);
            UpdateMuscleBarFill(_tricepsBarFill, seconds2, maxSeconds);
            UpdateMuscleBarFill(_shouldersBarFill, seconds3, maxSeconds);
        }

        private int GetMuscleSeconds(string muscle)
        {
            if (string.Equals(muscle, "Cardio", StringComparison.OrdinalIgnoreCase))
                muscle = "Core";

            return _muscleSecondsToday.TryGetValue(muscle, out var value)
                ? Math.Max(0, value)
                : 0;
        }

        private static void UpdateMuscleBarFill(View? fill, int seconds, int maxSeconds)
        {
            if (fill?.LayoutParameters is not LinearLayout.LayoutParams lp)
                return;

            var value = Math.Max(0, seconds);
            var ratio = maxSeconds <= 0 ? 0f : value / (float)maxSeconds;
            var targetWeight = value <= 0 ? 0f : Math.Max(0.8f, ratio * 10f);

            lp.Width = 0;
            lp.Height = ViewGroup.LayoutParams.MatchParent;
            lp.Weight = targetWeight;
            fill.LayoutParameters = lp;
        }

        private static int ToMinutes(int seconds)
        {
            if (seconds <= 0)
                return 0;

            return (int)Math.Floor(seconds / 60d);
        }

        private void AddSecondToSelectedMuscle()
        {
            if (!_muscleSecondsToday.ContainsKey(_selectedFocus))
                _muscleSecondsToday[_selectedFocus] = 0;

            _muscleSecondsToday[_selectedFocus]++;

            if (_muscleSecondsToday[_selectedFocus] % 10 == 0)
            {
                SaveTodayMuscleTimes();
            }
        }

        private void LoadTodayMuscleTimes()
        {
            var prefs = GetSharedPreferences(MuscleTimePrefsName, FileCreationMode.Private);
            if (prefs == null)
                return;

            foreach (var muscle in _muscleSecondsToday.Keys.ToList())
            {
                var key = BuildTodayMuscleKey(muscle);
                _muscleSecondsToday[muscle] = Math.Max(0, prefs.GetInt(key, 0));
            }
        }

        private void SaveTodayMuscleTimes()
        {
            var prefs = GetSharedPreferences(MuscleTimePrefsName, FileCreationMode.Private);
            var editor = prefs?.Edit();
            if (editor == null)
                return;

            foreach (var entry in _muscleSecondsToday)
            {
                editor.PutInt(BuildTodayMuscleKey(entry.Key), Math.Max(0, entry.Value));
            }

            editor.Apply();
        }

        private static string BuildTodayMuscleKey(string muscle)
        {
            return $"{DateTime.Today:yyyyMMdd}_{muscle.ToLowerInvariant()}";
        }

        private string GetPlannedFocusText()
        {
            var trainingDay = GetTrainingDay();

            return trainingDay switch
            {
                1 => "Biceps/Triceps + Abs",
                2 => "Chest/Delts + Legs",
                _ => "Back/Shoulder + Cardio"
            };
        }

        private int GetTrainingDay()
        {
            var prefs = GetSharedPreferences("training_plan", FileCreationMode.Private);
            var loginCount = prefs?.GetInt("login_count", 0) ?? 0;
            return ((loginCount <= 0 ? 0 : loginCount - 1) % 3) + 1;
        }

        private string[] GetPlannedMuscleGroups()
        {
            return GetTrainingDay() switch
            {
                1 => new[] { "Biceps", "Triceps", "Core" },
                2 => new[] { "Chest", "Shoulders", "Legs" },
                _ => new[] { "Back", "Shoulders", "Cardio" }
            };
        }

        private void UpdateCurrentExerciseCard()
        {
            if (_currentExerciseNameText != null)
                _currentExerciseNameText.Text = "Workout Groups";

            if (_set1RepsText != null)
                _set1RepsText.Text = BuildCircuitSummary("Circuit A");

            if (_set2RepsText != null)
                _set2RepsText.Text = BuildCircuitSummary("Circuit B");

            if (_set3RepsText != null)
            {
                _set3RepsText.Visibility = ViewStates.Visible;
                _set3RepsText.Text = BuildCircuitSummary("Circuit C");
            }
        }

        private string BuildCircuitSummary(string circuitName)
        {
            var items = _currentWorkout?.Exercises
                .Where(ex => string.Equals(ex.CircuitName, circuitName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(ex => ex.CircuitOrder)
                .ToList() ?? new List<WorkoutExercise>();

            if (items.Count == 0)
                return $"{circuitName}: waiting for contents";

            var parts = items.Select(ex =>
            {
                var exerciseName = ex.Exercise?.Name ?? "Exercise";
                var doneCount = Math.Max(0, ex.Sets.Count);
                return $"{exerciseName} x{doneCount}";
            });

            return $"{circuitName}: {string.Join(" / ", parts)}";
        }

        private void UpdateElapsedText()
        {
            if (_currentElapsedTimeText != null)
                _currentElapsedTimeText.Text = $"Elapsed Time: {_elapsed:mm\\:ss}";
        }

        private void BindBottomNav()
        {
            var homeTab = FindViewById<LinearLayout>(Resource.Id.homeTab);
            var diaryTab = FindViewById<LinearLayout>(Resource.Id.diaryTab);
            var workoutTab = FindViewById<LinearLayout>(Resource.Id.workoutTab);
            var profileTab = FindViewById<LinearLayout>(Resource.Id.profileTab);

            if (homeTab != null)
                homeTab.Click += (s, e) => StartActivity(new Intent(this, typeof(HomeActivity)));

            if (diaryTab != null)
                diaryTab.Click += (s, e) => StartActivity(new Intent(this, typeof(HistoryActivity)));

            if (profileTab != null)
                profileTab.Click += (s, e) => StartActivity(new Intent(this, typeof(ProfileActivity)));

            if (workoutTab != null)
                workoutTab.Click += (s, e) => { };

            var profileLabel = FindViewById<TextView>(Resource.Id.profileTabLabel);
            if (profileLabel != null)
                profileLabel.Text = "Profile";
        }

        private void UpdateBottomNavSelection()
        {
            var homeTab = FindViewById<LinearLayout>(Resource.Id.homeTab);
            var diaryTab = FindViewById<LinearLayout>(Resource.Id.diaryTab);
            var workoutTab = FindViewById<LinearLayout>(Resource.Id.workoutTab);
            var profileTab = FindViewById<LinearLayout>(Resource.Id.profileTab);

            if (homeTab != null) homeTab.Selected = false;
            if (diaryTab != null) diaryTab.Selected = false;
            if (workoutTab != null) workoutTab.Selected = true;
            if (profileTab != null) profileTab.Selected = false;

            SetTabLabelStyle(Resource.Id.homeTabLabel, false);
            SetTabLabelStyle(Resource.Id.diaryTabLabel, false);
            SetTabLabelStyle(Resource.Id.workoutTabLabel, true);
            SetTabLabelStyle(Resource.Id.profileTabLabel, false);
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
