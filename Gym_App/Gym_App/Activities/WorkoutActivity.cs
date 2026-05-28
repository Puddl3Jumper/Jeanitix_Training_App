using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Gym_App.Data;
using Gym_App.Models;

namespace Gym_App.Activities
{
    [Activity(Label = "Training")]
    public class WorkoutActivity : Activity
    {
        public const string ExtraStartWorkoutTimer = "startWorkoutTimer";
        public const string ExtraWorkoutId = "workoutId";

        private GymDatabase? _database;
        private WorkoutSession? _currentWorkout;
        private Chronometer? _workoutDurationChronometer;
        private bool _isWorkoutTimerRunning;

        private TextView? _focusValueText;
        private ImageView? _upperBodyCardImage;
        private TextView? _upperBodyWorkout1Text;
        private TextView? _upperBodyWorkout2Text;

        private ImageView? _lowerBodyCardImage;
        private TextView? _lowerBodyWorkout1Text;

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

        public static Intent CreateIntent(Context context, bool startWorkoutTimer = false, int workoutId = -1)
        {
            var intent = new Intent(context, typeof(WorkoutActivity));
            if (startWorkoutTimer)
            {
                intent.PutExtra(ExtraStartWorkoutTimer, true);
            }

            if (workoutId > 0)
            {
                intent.PutExtra(ExtraWorkoutId, workoutId);
            }

            return intent;
        }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_workout);

            _database = new GymDatabase();
            InitializeWorkoutSession();

            BindViews();
            BindTopActions();
            BindCameraLoopButton();
            BindFinishWorkoutButton();
            BindBottomNav();
            LoadTodayMuscleTimes();

            RefreshScreen();
            SyncWorkoutTimerUi();
        }

        protected override void OnDestroy()
        {
            StopWorkoutTimerUi();
            SaveTodayMuscleTimes();
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
            SyncWorkoutTimerUi();
        }

        protected override void OnPause()
        {
            StopWorkoutTimerUi();
            base.OnPause();
        }

        private void InitializeWorkoutSession()
        {
            if (_database == null)
                return;

            var startTimer = Intent?.GetBooleanExtra(ExtraStartWorkoutTimer, false) ?? false;
            var workoutId = Intent?.GetIntExtra(ExtraWorkoutId, -1) ?? -1;
            var sessionName = GymDatabase.BuildRoutineSessionName(GetPlannedMuscleGroups());

            if (startTimer)
            {
                _currentWorkout = _database.StartTimedWorkout(
                    sessionName,
                    workoutId > 0 ? workoutId : null,
                    resetStartTime: true);
                _isWorkoutTimerRunning = true;
                return;
            }

            _currentWorkout = workoutId > 0
                ? _database.GetWorkoutSession(workoutId)
                : _database.GetCurrentWorkout();

            _isWorkoutTimerRunning = _currentWorkout is { IsCompleted: false };
        }

        private void BindViews()
        {
            _focusValueText = FindViewById<TextView>(Resource.Id.focusValueText);
            _upperBodyCardImage = FindViewById<ImageView>(Resource.Id.upperBodyCardImage);
            _upperBodyWorkout1Text = FindViewById<TextView>(Resource.Id.upperBodyWorkout1Text);
            _upperBodyWorkout2Text = FindViewById<TextView>(Resource.Id.upperBodyWorkout2Text);

            _lowerBodyCardImage = FindViewById<ImageView>(Resource.Id.lowerBodyCardImage);
            _lowerBodyWorkout1Text = FindViewById<TextView>(Resource.Id.lowerBodyWorkout1Text);
            _workoutDurationChronometer = FindViewById<Chronometer>(Resource.Id.workoutDurationChronometer);
        }

        private void BindTopActions()
        {
            var historyButton = FindViewById<ImageButton>(Resource.Id.trainingHistoryButton);
            if (historyButton != null)
            {
                historyButton.Click += (s, e) => StartActivity(new Intent(this, typeof(HistoryActivity)));
            }
        }

        private void BindCameraLoopButton()
        {
            var cameraLoopButton = FindViewById<Button>(Resource.Id.cameraLoopButton);
            if (cameraLoopButton == null)
                return;

            cameraLoopButton.Click += (_, _) => StartCameraLoopCounter();
        }

        private void StartCameraLoopCounter()
        {
            if (_database == null)
                return;

            if (_currentWorkout == null || _currentWorkout.IsCompleted)
            {
                _currentWorkout = _database.StartTimedWorkout(
                    GymDatabase.BuildRoutineSessionName(GetPlannedMuscleGroups()),
                    resetStartTime: true);
                _isWorkoutTimerRunning = true;
                SyncWorkoutTimerUi();
            }

            var exerciseName = GetExerciseForMuscle(_selectedFocus);
            StartActivity(CameraLoopActivity.CreateIntent(this, _currentWorkout.Id, exerciseName));
        }

        private void BindFinishWorkoutButton()
        {
            var finishButton = FindViewById<Button>(Resource.Id.finishedWorkoutButton);
            if (finishButton == null)
                return;

            finishButton.Click += (_, _) => OnFinishedWorkoutClicked();
        }

        private void OnFinishedWorkoutClicked()
        {
            if (_database == null)
                return;

            if (_currentWorkout == null || !_isWorkoutTimerRunning)
            {
                Toast.MakeText(this, Resource.String.start_workout_before_finish, ToastLength.Short)?.Show();
                return;
            }

            StopWorkoutTimerUi();

            var plannedGroups = GetPlannedMuscleGroups();
            _database.CompleteTodayRoutine(plannedGroups, _currentWorkout.Id);
            _currentWorkout = null;
            _isWorkoutTimerRunning = false;

            Toast.MakeText(this, Resource.String.finished_workout_logged, ToastLength.Short)?.Show();
            StartActivity(new Intent(this, typeof(HistoryActivity)));
        }

        private void SyncWorkoutTimerUi()
        {
            if (_workoutDurationChronometer == null)
                return;

            if (_currentWorkout == null || !_isWorkoutTimerRunning || _currentWorkout.IsCompleted)
            {
                StopWorkoutTimerUi();
                _workoutDurationChronometer.Text = GetString(Resource.String.workout_timer_default);
                return;
            }

            var elapsedMs = Math.Max(0, (long)(DateTime.Now - _currentWorkout.StartTime).TotalMilliseconds);
            _workoutDurationChronometer.Base = SystemClock.ElapsedRealtime() - elapsedMs;
            _workoutDurationChronometer.Start();
        }

        private void StopWorkoutTimerUi()
        {
            _workoutDurationChronometer?.Stop();
        }

        private void RefreshScreen()
        {
            var plannedGroups = GetPlannedMuscleGroups();
            if (plannedGroups.Length > 0)
            {
                _selectedFocus = plannedGroups[0];
            }

            UpdateMuscleTimeViews();
            UpdateLowerBodyViews();
            UpdateTrainingHeader();
            UpdateBottomNavSelection();
        }

        private void UpdateTrainingHeader()
        {
            if (_focusValueText != null)
                _focusValueText.Text = "Upper Body";
        }

        private void UpdateMuscleTimeViews()
        {
            var plannedGroups = GetPlannedMuscleGroups();

            if (plannedGroups.Length >= 2)
            {
                _upperBodyWorkout1Text?.SetText(plannedGroups[0], TextView.BufferType.Normal);
                _upperBodyWorkout2Text?.SetText(plannedGroups[1], TextView.BufferType.Normal);
            }
        }

        private void UpdateLowerBodyViews()
        {
            var plannedGroups = GetPlannedMuscleGroups();

            if (plannedGroups.Length >= 3)
            {
                _lowerBodyWorkout1Text?.SetText(plannedGroups[2], TextView.BufferType.Normal);
            }
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

            if (string.Equals(label, "Chest", StringComparison.OrdinalIgnoreCase))
                return Resource.Drawable.chest_focus;

            if (string.Equals(label, "Biceps", StringComparison.OrdinalIgnoreCase))
                return Resource.Drawable.biceps_focus;

            if (string.Equals(label, "Legs", StringComparison.OrdinalIgnoreCase))
                return Resource.Drawable.squats_focus_no_bg;

            return Resource.Drawable.ic_dumbbell;
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
            var groups = GetPlannedMuscleGroups();
            return $"{groups[0]}/{groups[1]} + {groups[2]}";
        }

        private string[] GetPlannedMuscleGroups()
        {
            return _database?.GetDailyWorkoutGroups() ?? new[] { "Chest", "Back", "Legs" };
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
