using Android.Views;
using Android.Widget;
using Android.Content;
using Android.Graphics;
using Gym_App.Data;
using Gym_App.Models;

namespace Gym_App.Activities
{
    [Activity(Label = "Exercise Library")]
    public class ExerciseLibraryActivity : Activity
    {
        private GymDatabase? _database;
        private LinearLayout? _exerciseListContainer;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_exercise_library);

            _database = new GymDatabase();
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

            var addCustomExerciseButton = FindViewById<Button>(Resource.Id.addCustomExerciseButton);
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
                        descText.SetTextColor(new Android.Graphics.Color(Color.White));
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
                        descText.SetTextColor(new Android.Graphics.Color(Color.White));
                        exerciseView.AddView(descText);
                    }

                    _exerciseListContainer.AddView(exerciseTitle);
                    _exerciseListContainer.AddView(exerciseView);
                }
            }
        }

        private void AddCustomExerciseButton_Click(object? sender, EventArgs e)
        {
            var dialog = new AlertDialog.Builder(this);
            dialog.SetTitle("Add Custom Exercise");

            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            layout.SetPadding(32, 16, 32, 16);

            var nameInput = new EditText(this) { Hint = "Exercise Name" };
            var muscleGroupInput = new EditText(this) { Hint = "Muscle Group (e.g., Chest, Legs)" };
            var descriptionInput = new EditText(this) { Hint = "Description (optional)" };

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
            dialog.Show();
        }
    }
}
