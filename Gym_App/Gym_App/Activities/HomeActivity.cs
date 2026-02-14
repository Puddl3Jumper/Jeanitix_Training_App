using Android.Content;
using Android.Views;
using Android.Widget;
using Gym_App.Data;
using Gym_App.Models;

namespace Gym_App.Activities
{
    [Activity(Label = "Home")]
    public class HomeActivity : Activity
    {
        private GymDatabase? _database;
        private TextView? _statDurationText;
        private TextView? _statSetsText;
        private TextView? _statCaloriesText;
        private LinearLayout? _todayExercisesContainer;
        private TextView? _noRecordsText;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_home);

            _database = new GymDatabase();

            _statDurationText = FindViewById<TextView>(Resource.Id.statDurationText);
            _statSetsText = FindViewById<TextView>(Resource.Id.statSetsText);
            _statCaloriesText = FindViewById<TextView>(Resource.Id.statCaloriesText);
            _todayExercisesContainer = FindViewById<LinearLayout>(Resource.Id.todayExercisesContainer);
            _noRecordsText = FindViewById<TextView>(Resource.Id.noRecordsText);

            var startWorkoutButton = FindViewById<Button>(Resource.Id.startWorkoutHomeButton);
            var addExerciseButton = FindViewById<Button>(Resource.Id.addExerciseHomeButton);
            var homeTab = FindViewById<LinearLayout>(Resource.Id.homeTab);
            var diaryTab = FindViewById<LinearLayout>(Resource.Id.diaryTab);
            var workoutTab = FindViewById<LinearLayout>(Resource.Id.workoutTab);
            var profileTab = FindViewById<LinearLayout>(Resource.Id.profileTab);

            if (homeTab != null) homeTab.Selected = true;
            if (diaryTab != null) diaryTab.Selected = false;
            if (workoutTab != null) workoutTab.Selected = false;
            if (profileTab != null) profileTab.Selected = false;

            if (startWorkoutButton != null)
            {
                startWorkoutButton.Click += (s, e) =>
                {
                    var currentWorkout = _database?.GetCurrentWorkout();
                    var intent = new Intent(this, typeof(WorkoutActivity));
                    if (currentWorkout != null)
                    {
                        intent.PutExtra("workoutId", currentWorkout.Id);
                    }
                    StartActivity(intent);
                };
            }

            if (addExerciseButton != null)
            {
                addExerciseButton.Click += (s, e) =>
                {
                    ShowQuickAddDialog();
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

            if (profileTab != null)
            {
                profileTab.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(ProfileActivity)));
                };
            }

            if (homeTab != null)
            {
                homeTab.Click += (s, e) => { };
            }
        }

        protected override void OnResume()
        {
            base.OnResume();
            LoadTodayRecords();
        }

        private void LoadTodayRecords()
        {
            if (_database == null || _todayExercisesContainer == null)
                return;

            DateTime today = DateTime.Today;

            var completedToday = _database.GetWorkoutHistory(500)
                .Where(s => s.StartTime.Date == today)
                .ToList();

            var currentWorkout = _database.GetCurrentWorkout();
            if (currentWorkout != null && currentWorkout.StartTime.Date == today)
            {
                completedToday.Add(currentWorkout);
            }

            int totalSets = 0;
            double calories = 0;
            TimeSpan totalDuration = TimeSpan.Zero;

            foreach (var session in completedToday)
            {
                totalDuration += session.Duration;
                foreach (var exercise in session.Exercises)
                {
                    totalSets += exercise.Sets.Count;
                    calories += exercise.Sets.Sum(set => set.Reps * set.Weight * 0.1);
                }
            }

            if (_statDurationText != null)
                _statDurationText.Text = $"{totalDuration.Hours:D2}:{totalDuration.Minutes:D2}";

            if (_statSetsText != null)
                _statSetsText.Text = totalSets.ToString();

            if (_statCaloriesText != null)
                _statCaloriesText.Text = $"{Math.Round(calories)} kcal";

            _todayExercisesContainer.RemoveAllViews();

            var exerciseRecords = completedToday
                .SelectMany(session => session.Exercises)
                .Where(exercise => exercise.Exercise != null)
                .ToList();

            if (exerciseRecords.Count == 0)
            {
                if (_noRecordsText != null)
                {
                    _todayExercisesContainer.AddView(_noRecordsText);
                }
                else
                {
                    var noData = new TextView(this)
                    {
                        Text = "No records yet"
                    };
                    noData.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_secondary)));
                    noData.Gravity = GravityFlags.Center;
                    _todayExercisesContainer.AddView(noData);
                }
                return;
            }

            foreach (var record in exerciseRecords)
            {
                if (record.Exercise == null)
                    continue;

                var item = new TextView(this)
                {
                    Text = $"• {record.Exercise.Name}  ({record.Sets.Count} sets)",
                    TextSize = 14
                };
                item.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
                item.SetPadding(0, 4, 0, 4);
                _todayExercisesContainer.AddView(item);
            }
        }

        private void ShowQuickAddDialog()
        {
            if (_database == null)
                return;

            var exercises = _database.GetAllExercises();
            if (exercises.Count == 0)
            {
                Toast.MakeText(this, "No exercises available", ToastLength.Short)?.Show();
                return;
            }

            var names = exercises.Select(e => e.Name).ToArray();

            var picker = new AlertDialog.Builder(this);
            picker.SetTitle("Select Exercise");
            picker.SetItems(names, (s, args) =>
            {
                var selected = exercises[args.Which];
                ShowSetEntryDialog(selected);
            });
            picker.Show();
        }

        private void ShowSetEntryDialog(Exercise exercise)
        {
            if (_database == null)
                return;

            var dialog = new AlertDialog.Builder(this);
            dialog.SetTitle($"Log {exercise.Name}");

            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            layout.SetPadding(32, 16, 32, 16);

            var repsInput = new EditText(this)
            {
                Hint = "Reps",
                InputType = Android.Text.InputTypes.ClassNumber
            };
            var weightInput = new EditText(this)
            {
                Hint = "Weight (kg)",
                InputType = Android.Text.InputTypes.ClassNumber | Android.Text.InputTypes.NumberFlagDecimal
            };

            layout.AddView(repsInput);
            layout.AddView(weightInput);
            dialog.SetView(layout);

            dialog.SetPositiveButton("Save", (s, e) =>
            {
                if (!int.TryParse(repsInput.Text, out int reps) || reps <= 0)
                {
                    Toast.MakeText(this, "Enter valid reps", ToastLength.Short)?.Show();
                    return;
                }

                if (!double.TryParse(weightInput.Text, out double weight) || weight < 0)
                {
                    Toast.MakeText(this, "Enter valid weight", ToastLength.Short)?.Show();
                    return;
                }

                var session = _database.GetCurrentWorkout() ?? _database.CreateWorkoutSession("Quick Log Workout");
                _database.AddExerciseToWorkout(session.Id, exercise.Id);

                var refreshed = _database.GetWorkoutSession(session.Id);
                var workoutExercise = refreshed?.Exercises.LastOrDefault();
                if (workoutExercise != null)
                {
                    _database.AddSetToExercise(workoutExercise.Id, reps, weight, 1);
                    Toast.MakeText(this, "Exercise recorded", ToastLength.Short)?.Show();
                    LoadTodayRecords();
                }
            });

            dialog.SetNegativeButton("Cancel", (s, e) => { });
            dialog.Show();
        }
    }
}
