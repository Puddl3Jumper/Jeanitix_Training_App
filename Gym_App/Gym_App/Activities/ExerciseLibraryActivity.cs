using Android.Views;
using Android.Widget;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Gym_App;
using Gym_App.Data;
using Gym_App.Models;
using Google.Android.Material.Dialog;

namespace Gym_App.Activities
{
    [Activity(Label = "Exercise Library")]
    public class ExerciseLibraryActivity : Activity
    {
        private GymDatabase? _database;
        private LinearLayout? _exerciseListContainer;
        private int _targetWorkoutId = -1;
        private static readonly string[] CircuitOptions = { "Circuit A", "Circuit B", "Circuit C" };

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_exercise_library);

            _database = new GymDatabase();
            _targetWorkoutId = Intent?.GetIntExtra("workoutId", -1) ?? -1;
            _exerciseListContainer = FindViewById<LinearLayout>(Resource.Id.exerciseListContainer);

            var homeTab = FindViewById<LinearLayout>(Resource.Id.homeTab);
            var diaryTab = FindViewById<LinearLayout>(Resource.Id.diaryTab);
            var workoutTab = FindViewById<LinearLayout>(Resource.Id.workoutTab);
            var profileTab = FindViewById<LinearLayout>(Resource.Id.profileTab);

            if (homeTab != null) homeTab.Selected = false;
            if (diaryTab != null) diaryTab.Selected = false;
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

            var addCustomExerciseButton = FindViewById(Resource.Id.addCustomExerciseButton);
            if (addCustomExerciseButton != null)
                addCustomExerciseButton.Click += AddCustomExerciseButton_Click;

            LoadExercises();
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

        private void LoadExercises()
        {
            if (_exerciseListContainer == null || _database == null)
                return;

            _exerciseListContainer.RemoveAllViews();

            var exercises = _database.GetAllExercises();
            var groupedExercises = exercises.GroupBy(e => e.MuscleGroup);

            foreach (var group in groupedExercises)
            {
                var groupHeader = new TextView(this)
                {
                    Text = group.Key,
                    TextSize = 20
                };
                groupHeader.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_primary)));
                groupHeader.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
                groupHeader.SetPadding(0, 16, 0, 8);
                _exerciseListContainer.AddView(groupHeader);

                foreach (var exercise in group)
                {
                    var exerciseTitle = new TextView(this)
                    {
                        Text = exercise.Name + (exercise.IsCustom ? " (Custom)" : ""),
                        TextSize = 16
                    };
                    exerciseTitle.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_primary)));
                    exerciseTitle.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
                    exerciseTitle.SetPadding(0, 2, 0, 6);

                    var exerciseView = new LinearLayout(this)
                    {
                        Orientation = Orientation.Vertical
                    };
                    
                    var layoutParams = new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MatchParent,
                        ViewGroup.LayoutParams.WrapContent);
                    layoutParams.SetMargins(0, 0, 0, 8);
                    exerciseView.LayoutParameters = layoutParams;
                    exerciseView.SetBackgroundResource(Resource.Drawable.bg_card);
                    exerciseView.SetPadding(16, 12, 16, 12);

                    if (!string.IsNullOrEmpty(exercise.Description))
                    {
                        var descText = new TextView(this)
                        {
                            Text = exercise.Description,
                            TextSize = 14
                        };
                        descText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
                        descText.SetPadding(0, 0, 0, 0);
                        exerciseView.AddView(descText);
                    }
                    else
                    {
                        var descText = new TextView(this)
                        {
                            Text = "No description",
                            TextSize = 14
                        };
                        descText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
                        exerciseView.AddView(descText);
                    }

                    _exerciseListContainer.AddView(exerciseTitle);
                    _exerciseListContainer.AddView(exerciseView);

                    exerciseTitle.Click += (s, e) => ShowLogExerciseDialog(exercise);
                    exerciseView.Click += (s, e) => ShowLogExerciseDialog(exercise);
                }
            }
        }

        private WorkoutSession EnsureActiveWorkout()
        {
            var session = _targetWorkoutId > 0
                ? _database?.GetWorkoutSession(_targetWorkoutId)
                : _database?.GetCurrentWorkout();

            if (session == null)
            {
                session = _database!.CreateWorkoutSession("Training");
            }

            _targetWorkoutId = session.Id;
            return session;
        }

        private void ShowLogExerciseDialog(Exercise exercise)
        {
            if (_database == null)
                return;

            var session = EnsureActiveWorkout();

            var dialog = new MaterialAlertDialogBuilder(this);
            dialog.SetTitle($"Log {exercise.Name}");

            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            layout.SetPadding(32, 16, 32, 16);

            var circuitLabel = new TextView(this)
            {
                Text = "Workout Group",
                TextSize = 14
            };
            circuitLabel.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));

            var circuitSpinner = new Spinner(this);
            var circuitAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem, CircuitOptions);
            circuitSpinner.Adapter = circuitAdapter;

            var countLabel = new TextView(this)
            {
                Text = "Done Count",
                TextSize = 14
            };
            countLabel.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
            countLabel.SetPadding(0, 20, 0, 0);

            var countInput = new EditText(this)
            {
                Hint = "How many sets completed"
            };
            countInput.InputType = InputTypes.ClassNumber;
            DialogThemeHelper.StyleInput(this, countInput);

            var existingByCircuit = session.Exercises
                .Where(e => e.ExerciseId == exercise.Id && !string.IsNullOrWhiteSpace(e.CircuitName))
                .OrderBy(e => e.CircuitName)
                .ToList();

            if (existingByCircuit.Count > 0)
            {
                var existing = existingByCircuit[0];
                var circuitName = existing.CircuitName?.Trim() ?? CircuitOptions[0];
                var selectedIndex = Array.FindIndex(CircuitOptions, c => string.Equals(c, circuitName, StringComparison.OrdinalIgnoreCase));
                if (selectedIndex >= 0)
                    circuitSpinner.SetSelection(selectedIndex);

                countInput.Text = Math.Max(0, existing.Sets.Count).ToString();
            }
            else
            {
                countInput.Text = "1";
            }

            layout.AddView(circuitLabel);
            layout.AddView(circuitSpinner);
            layout.AddView(countLabel);
            layout.AddView(countInput);
            dialog.SetView(layout);

            dialog.SetPositiveButton("Save", (s, e) =>
            {
                var selectedCircuit = CircuitOptions[Math.Max(0, circuitSpinner.SelectedItemPosition)];
                var targetCount = 0;
                if (!int.TryParse(countInput.Text?.Trim(), out targetCount))
                    targetCount = 0;
                targetCount = Math.Max(0, targetCount);

                var activeSession = EnsureActiveWorkout();
                var targetExercise = activeSession.Exercises
                    .FirstOrDefault(ex => ex.ExerciseId == exercise.Id && string.Equals(ex.CircuitName, selectedCircuit, StringComparison.OrdinalIgnoreCase));

                if (targetExercise == null && targetCount > 0)
                {
                    _database.AddExerciseToWorkout(activeSession.Id, exercise.Id, selectedCircuit);
                    activeSession = _database.GetWorkoutSession(activeSession.Id) ?? activeSession;
                    targetExercise = activeSession.Exercises
                        .Where(ex => ex.ExerciseId == exercise.Id && string.Equals(ex.CircuitName, selectedCircuit, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(ex => ex.Id)
                        .FirstOrDefault();
                }

                if (targetExercise == null)
                {
                    Toast.MakeText(this, "Saved", ToastLength.Short)?.Show();
                    return;
                }

                var currentCount = targetExercise.Sets.Count;
                if (targetCount > currentCount)
                {
                    for (var i = currentCount; i < targetCount; i++)
                    {
                        _database.AddSetToExercise(targetExercise.Id, 1, 0, "Logged from library", 1, "kg");
                    }
                }
                else if (targetCount < currentCount)
                {
                    var setsToDelete = targetExercise.Sets
                        .OrderByDescending(set => set.SetNumber)
                        .Take(currentCount - targetCount)
                        .ToList();

                    foreach (var set in setsToDelete)
                    {
                        _database.DeleteWorkoutSet(set.Id);
                    }
                }

                Toast.MakeText(this, $"{exercise.Name} saved to {selectedCircuit}", ToastLength.Short)?.Show();
            });

            dialog.SetNegativeButton("Cancel", (s, e) => { });
            var shownDialog = dialog.Show();
            DialogThemeHelper.StyleShownDialog(this, shownDialog);
        }

        private void AddCustomExerciseButton_Click(object? sender, EventArgs e)
        {
            var dialog = new MaterialAlertDialogBuilder(this);
            dialog.SetTitle("Add Custom Exercise");

            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            layout.SetPadding(32, 16, 32, 16);

            var nameInput = new EditText(this) { Hint = "Exercise Name" };
            var muscleGroupInput = new EditText(this) { Hint = "Muscle Group (e.g., Chest, Legs)" };
            var descriptionInput = new EditText(this) { Hint = "Description (optional)" };

            DialogThemeHelper.StyleInput(this, nameInput);
            DialogThemeHelper.StyleInput(this, muscleGroupInput);
            DialogThemeHelper.StyleInput(this, descriptionInput);

            layout.AddView(nameInput);
            layout.AddView(muscleGroupInput);
            layout.AddView(descriptionInput);
            dialog.SetView(layout);

            dialog.SetPositiveButton("Add", (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(nameInput.Text) && 
                    !string.IsNullOrWhiteSpace(muscleGroupInput.Text))
                {
                    var exercise = new Exercise
                    {
                        Name = nameInput.Text.Trim(),
                        MuscleGroup = muscleGroupInput.Text.Trim(),
                        Description = descriptionInput.Text?.Trim()
                    };
                    _database?.AddExercise(exercise);
                    LoadExercises();
                    Toast.MakeText(this, "Exercise added!", ToastLength.Short)?.Show();
                }
            });

            dialog.SetNegativeButton("Cancel", (s, e) => { });
            var shownDialog = dialog.Show();
            DialogThemeHelper.StyleShownDialog(this, shownDialog);
        }
    }
}
