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
        private TextView? _muscleChip1Text;
        private TextView? _muscleChip2Text;
        private TextView? _muscleChip3Text;
        private ImageView? _muscleChip1Image;
        private ImageView? _muscleChip2Image;
        private ImageView? _muscleChip3Image;

        private TextView? _currentExerciseNameText;
        private TextView? _currentElapsedTimeText;
        private TextView? _set1RepsText;
        private TextView? _set2RepsText;
        private TextView? _set3RepsText;

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
            ["Biceps"] = "Dumbbell Curl",
            ["Triceps"] = "Cable Pushdown",
            ["Core"] = "Plank"
        };

        private string _selectedFocus = "Chest";
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
            _muscleChip1Text = FindViewById<TextView>(Resource.Id.muscleChip1Text);
            _muscleChip2Text = FindViewById<TextView>(Resource.Id.muscleChip2Text);
            _muscleChip3Text = FindViewById<TextView>(Resource.Id.muscleChip3Text);
            _muscleChip1Image = FindViewById<ImageView>(Resource.Id.muscleChip1Image);
            _muscleChip2Image = FindViewById<ImageView>(Resource.Id.muscleChip2Image);
            _muscleChip3Image = FindViewById<ImageView>(Resource.Id.muscleChip3Image);

            _currentExerciseNameText = FindViewById<TextView>(Resource.Id.currentExerciseNameText);
            _currentElapsedTimeText = FindViewById<TextView>(Resource.Id.currentElapsedTimeText);
            _set1RepsText = FindViewById<TextView>(Resource.Id.set1RepsText);
            _set2RepsText = FindViewById<TextView>(Resource.Id.set2RepsText);
            _set3RepsText = FindViewById<TextView>(Resource.Id.set3RepsText);

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
            if (_finishWorkoutButton != null)
            {
                _finishWorkoutButton.Click += (s, e) =>
                {
                    _elapsedTimer?.Dispose();
                    _elapsedTimer = null;

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
            var focusExercises = GetHomeAlignedFocusLabels();

            _muscleChip1Text?.SetText(focusExercises[0], TextView.BufferType.Normal);
            _muscleChip2Text?.SetText(focusExercises[1], TextView.BufferType.Normal);
            _muscleChip3Text?.SetText(focusExercises[2], TextView.BufferType.Normal);

            ApplyFocusCardImage(_muscleChip1Image, focusExercises[0]);
            ApplyFocusCardImage(_muscleChip2Image, focusExercises[1]);
            ApplyFocusCardImage(_muscleChip3Image, focusExercises[2]);
        }

        private string[] GetHomeAlignedFocusLabels()
        {
            return GetTrainingDay() switch
            {
                1 => new[] { "Biceps", "Triceps", "Abs" },
                2 => new[] { "Chest", "Delts", "Legs" },
                _ => new[] { "Back", "Shoulder", "Cardio" }
            };
        }

        private int GetMuscleSeconds(string muscle)
        {
            if (string.Equals(muscle, "Cardio", StringComparison.OrdinalIgnoreCase))
                muscle = "Core";

            return _muscleSecondsToday.TryGetValue(muscle, out var value)
                ? Math.Max(0, value)
                : 0;
        }

        private string GetExerciseForMuscle(string muscle)
        {
            if (string.Equals(muscle, "Cardio", StringComparison.OrdinalIgnoreCase))
                muscle = "Core";
            else if (string.Equals(muscle, "Abs", StringComparison.OrdinalIgnoreCase))
                muscle = "Core";
            else if (string.Equals(muscle, "Delts", StringComparison.OrdinalIgnoreCase) || string.Equals(muscle, "Shoulder", StringComparison.OrdinalIgnoreCase))
                muscle = "Shoulders";

            return _exerciseByMuscle.TryGetValue(muscle, out var exercise)
                ? exercise
                : "Workout";
        }

        private void ApplyFocusCardImage(ImageView? target, string exerciseName)
        {
            if (target == null)
                return;

            target.SetImageResource(ResolveMuscleImageResource(exerciseName));
            target.ClearColorFilter();
            target.SetScaleType(ImageView.ScaleType.CenterCrop);
        }

        private static int ResolveMuscleImageResource(string exerciseName)
        {
            var label = exerciseName?.Trim() ?? string.Empty;

            if (string.Equals(label, "Biceps", StringComparison.OrdinalIgnoreCase))
                return Resource.Drawable.biceps_focus;

            if (string.Equals(label, "Triceps", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, "Trceps", StringComparison.OrdinalIgnoreCase))
                return Resource.Drawable.triceps_focus;

            return Resource.Drawable.ic_dumbbell;
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
            return _database?.GetNextTrainingDay() ?? 1;
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
