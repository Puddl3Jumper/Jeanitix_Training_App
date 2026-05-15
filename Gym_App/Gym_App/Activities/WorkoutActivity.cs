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
            BindBottomNav();
            LoadTodayMuscleTimes();

            RefreshScreen();
        }

        protected override void OnDestroy()
        {
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
        }

        private void BindViews()
        {
            _focusValueText = FindViewById<TextView>(Resource.Id.focusValueText);
            _upperBodyCardImage = FindViewById<ImageView>(Resource.Id.upperBodyCardImage);
            _upperBodyWorkout1Text = FindViewById<TextView>(Resource.Id.upperBodyWorkout1Text);
            _upperBodyWorkout2Text = FindViewById<TextView>(Resource.Id.upperBodyWorkout2Text);

            _lowerBodyCardImage = FindViewById<ImageView>(Resource.Id.lowerBodyCardImage);
            _lowerBodyWorkout1Text = FindViewById<TextView>(Resource.Id.lowerBodyWorkout1Text);
        }

        private void BindTopActions()
        {
            var historyButton = FindViewById<ImageButton>(Resource.Id.trainingHistoryButton);
            if (historyButton != null)
            {
                historyButton.Click += (s, e) => StartActivity(new Intent(this, typeof(HistoryActivity)));
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
