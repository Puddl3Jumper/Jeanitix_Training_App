using Android.Views;
using Android.Widget;
using Android.Content;
using Gym_App.Data;
using Gym_App.Models;

namespace Gym_App.Activities
{
    [Activity(Label = "Workout Session")]
    public class WorkoutActivity : Activity
    {
        private GymDatabase? _database;
        private WorkoutSession? _currentWorkout;
        private TextView? _workoutNameText;
        private TextView? _workoutTimeText;
        private LinearLayout? _exercisesContainer;
        private System.Threading.Timer? _timer;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_workout);

            _database = new GymDatabase();
            
            _workoutNameText = FindViewById<TextView>(Resource.Id.workoutNameText);
            _workoutTimeText = FindViewById<TextView>(Resource.Id.workoutTimeText);
            _exercisesContainer = FindViewById<LinearLayout>(Resource.Id.exercisesContainer);
            
            var addExerciseButton = FindViewById<Button>(Resource.Id.addExerciseButton);
            var finishWorkoutButton = FindViewById<Button>(Resource.Id.finishWorkoutButton);
            var homeTab = FindViewById<LinearLayout>(Resource.Id.homeTab);
            var diaryTab = FindViewById<LinearLayout>(Resource.Id.diaryTab);
            var workoutTab = FindViewById<LinearLayout>(Resource.Id.workoutTab);
            var profileTab = FindViewById<LinearLayout>(Resource.Id.profileTab);

            if (homeTab != null) homeTab.Selected = false;
            if (diaryTab != null) diaryTab.Selected = false;
            if (workoutTab != null) workoutTab.Selected = true;
            if (profileTab != null) profileTab.Selected = false;

            if (homeTab != null)
            {
                homeTab.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(HomeActivity)));
                };
            }

            if (diaryTab != null)
            {
                diaryTab.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(HistoryActivity)));
                };
            }

            if (profileTab != null)
            {
                profileTab.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(ProfileActivity)));
                };
            }

            int workoutId = Intent?.GetIntExtra("workoutId", -1) ?? -1;
            
            if (workoutId == -1)
            {
                _currentWorkout = _database.CreateWorkoutSession("Workout Session");
            }
            else
            {
                _currentWorkout = _database.GetWorkoutSession(workoutId);
            }

            if (addExerciseButton != null)
            {
                addExerciseButton.Text = "Add Exercise / Circuit";
                addExerciseButton.Click += AddExerciseButton_Click;
            }
            
            if (finishWorkoutButton != null)
                finishWorkoutButton.Click += FinishWorkoutButton_Click;

            UpdateUI();
            StartTimer();
        }

        private void StartTimer()
        {
            _timer = new System.Threading.Timer(_ => 
            {
                RunOnUiThread(UpdateDuration);
            }, null, 0, 1000);
        }

        private void UpdateDuration()
        {
            if (_currentWorkout != null && _workoutTimeText != null)
            {
                var duration = _currentWorkout.Duration;
                _workoutTimeText.Text = $"Duration: {duration:hh\\:mm\\:ss}";
            }
        }

        private void UpdateUI()
        {
            if (_currentWorkout == null || _workoutNameText == null || _exercisesContainer == null)
                return;

            _workoutNameText.Text = _currentWorkout.Name;
            _exercisesContainer.RemoveAllViews();

            foreach (var workoutExercise in _currentWorkout.Exercises)
            {
                AddExerciseView(workoutExercise);
            }
        }

        private void AddExerciseView(WorkoutExercise workoutExercise)
        {
            if (_exercisesContainer == null || workoutExercise.Exercise == null)
                return;

            var exerciseCard = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };
            
            var layoutParams = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent);
            layoutParams.SetMargins(0, 0, 0, 24);
            exerciseCard.LayoutParameters = layoutParams;
            exerciseCard.SetBackgroundResource(Resource.Drawable.bg_card);
            exerciseCard.SetPadding(16, 16, 16, 16);

            string titleText = workoutExercise.Exercise.Name;
            if (!string.IsNullOrWhiteSpace(workoutExercise.CircuitName))
            {
                titleText = $"{workoutExercise.Exercise.Name} (Circuit: {workoutExercise.CircuitName} #{workoutExercise.CircuitOrder})";
            }

            var exerciseTitle = new TextView(this)
            {
                Text = titleText,
                TextSize = 20
            };
            exerciseTitle.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
            exerciseTitle.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
            exerciseCard.AddView(exerciseTitle);

            foreach (var set in workoutExercise.Sets)
            {
                string setDisplay = !string.IsNullOrWhiteSpace(workoutExercise.CircuitName)
                    ? $"Set {set.SetNumber} · Loop {set.LoopNumber}: {set.Reps} reps @ {set.Weight} {set.WeightUnit}"
                    : $"Set {set.SetNumber}: {set.Reps} reps @ {set.Weight} {set.WeightUnit}";

                var setText = new TextView(this)
                {
                    Text = setDisplay,
                    TextSize = 16
                };
                setText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_secondary)));
                setText.SetPadding(16, 8, 0, 0);
                exerciseCard.AddView(setText);
            }

            var addSetButton = new Button(this)
            {
                Text = "Add Set"
            };
            addSetButton.SetBackgroundResource(Resource.Drawable.bg_button_primary);
            addSetButton.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_on_primary)));
            addSetButton.TextSize = 15;
            addSetButton.Click += (s, e) => ShowAddSetDialog(workoutExercise);
            exerciseCard.AddView(addSetButton);

            _exercisesContainer.AddView(exerciseCard);
        }

        private void ShowAddSetDialog(WorkoutExercise workoutExercise)
        {
            var dialog = new AlertDialog.Builder(this);
            dialog.SetTitle("Add Set");

            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            layout.SetPadding(32, 16, 32, 16);

            var repsInput = new EditText(this) { Hint = "Reps", InputType = Android.Text.InputTypes.ClassNumber };
            var weightInput = new EditText(this) { Hint = "Weight (kg)", InputType = Android.Text.InputTypes.ClassNumber | Android.Text.InputTypes.NumberFlagDecimal };
            var loopInput = new EditText(this) { Hint = "Loop Number", InputType = Android.Text.InputTypes.ClassNumber };

            bool isCircuitExercise = !string.IsNullOrWhiteSpace(workoutExercise.CircuitName);
            int suggestedLoop = workoutExercise.Sets.Count == 0
                ? 1
                : workoutExercise.Sets.Max(s => s.LoopNumber);

            loopInput.Text = suggestedLoop.ToString();
            loopInput.Visibility = isCircuitExercise ? ViewStates.Visible : ViewStates.Gone;

            layout.AddView(repsInput);
            layout.AddView(weightInput);
            if (isCircuitExercise)
            {
                layout.AddView(loopInput);
            }
            dialog.SetView(layout);

            dialog.SetPositiveButton("Add", (s, e) =>
            {
                if (int.TryParse(repsInput.Text, out int reps) && 
                    double.TryParse(weightInput.Text, out double weight))
                {
                    int loopNumber = 1;
                    if (isCircuitExercise)
                    {
                        if (!int.TryParse(loopInput.Text, out loopNumber) || loopNumber < 1)
                        {
                            loopNumber = 1;
                        }
                    }

                    _database?.AddSetToExercise(workoutExercise.Id, reps, weight, loopNumber);
                    _currentWorkout = _database?.GetWorkoutSession(_currentWorkout?.Id ?? -1);
                    UpdateUI();
                }
            });

            dialog.SetNegativeButton("Cancel", (s, e) => { });
            dialog.Show();
        }

        private void AddExerciseButton_Click(object? sender, EventArgs e)
        {
            var exercises = _database?.GetAllExercises() ?? new List<Exercise>();
            var exerciseNames = exercises.Select(ex => ex.Name).ToArray();

            var dialog = new AlertDialog.Builder(this);
            dialog.SetTitle("Select Exercise");
            dialog.SetItems(exerciseNames, (s, args) =>
            {
                var selectedExercise = exercises[args.Which];
                ShowExerciseModeDialog(selectedExercise);
            });
            dialog.Show();
        }

        private void ShowExerciseModeDialog(Exercise selectedExercise)
        {
            var modeDialog = new AlertDialog.Builder(this);
            modeDialog.SetTitle($"Add {selectedExercise.Name}");

            var options = new[]
            {
                "Add as Regular Exercise",
                "Add to Circuit"
            };

            modeDialog.SetItems(options, (sender, args) =>
            {
                if (args.Which == 0)
                {
                    _database?.AddExerciseToWorkout(_currentWorkout?.Id ?? -1, selectedExercise.Id);
                    _currentWorkout = _database?.GetWorkoutSession(_currentWorkout?.Id ?? -1);
                    UpdateUI();
                    return;
                }

                ShowCircuitNameDialog(selectedExercise);
            });

            modeDialog.Show();
        }

        private void ShowCircuitNameDialog(Exercise selectedExercise)
        {
            var dialog = new AlertDialog.Builder(this);
            dialog.SetTitle("Circuit Name");

            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            layout.SetPadding(32, 16, 32, 16);

            var circuitNameInput = new EditText(this)
            {
                Hint = "e.g., Circuit A"
            };

            layout.AddView(circuitNameInput);
            dialog.SetView(layout);

            dialog.SetPositiveButton("Add", (sender, args) =>
            {
                var circuitName = circuitNameInput.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(circuitName))
                {
                    _database?.AddExerciseToWorkout(_currentWorkout?.Id ?? -1, selectedExercise.Id, circuitName);
                    _currentWorkout = _database?.GetWorkoutSession(_currentWorkout?.Id ?? -1);
                    UpdateUI();
                }
                else
                {
                    Toast.MakeText(this, "Circuit name is required", ToastLength.Short)?.Show();
                }
            });

            dialog.SetNegativeButton("Cancel", (sender, args) => { });
            dialog.Show();
        }

        private void FinishWorkoutButton_Click(object? sender, EventArgs e)
        {
            var dialog = new AlertDialog.Builder(this);
            dialog.SetTitle("Finish Workout");
            dialog.SetMessage("Are you sure you want to finish this workout?");
            dialog.SetPositiveButton("Yes", (s, e) =>
            {
                _database?.CompleteWorkout(_currentWorkout?.Id ?? -1);
                Toast.MakeText(this, "Workout completed!", ToastLength.Short)?.Show();
                Finish();
            });
            dialog.SetNegativeButton("No", (s, e) => { });
            dialog.Show();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _timer?.Dispose();
        }
    }
}
