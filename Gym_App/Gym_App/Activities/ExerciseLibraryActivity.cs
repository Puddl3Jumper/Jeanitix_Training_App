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
        private EditText? _searchExerciseInput;
        private int _targetWorkoutId = -1;
        private static readonly string[] CircuitOptions = { "Circuit A", "Circuit B", "Circuit C" };
        private readonly Dictionary<string, bool> _expandedGroups = new(StringComparer.OrdinalIgnoreCase);
        private string _searchQuery = string.Empty;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_exercise_library);

            _database = new GymDatabase();
            _targetWorkoutId = Intent?.GetIntExtra("workoutId", -1) ?? -1;
            _exerciseListContainer = FindViewById<LinearLayout>(Resource.Id.exerciseListContainer);
            _searchExerciseInput = FindViewById<EditText>(Resource.Id.searchExerciseInput);

            if (_searchExerciseInput != null)
            {
                _searchExerciseInput.TextChanged += (s, e) =>
                {
                    _searchQuery = _searchExerciseInput.Text?.Trim() ?? string.Empty;
                    LoadExercises();
                };
            }

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

            var allExercises = _database.GetAllExercises();
            var filtered = string.IsNullOrWhiteSpace(_searchQuery)
                ? allExercises
                : allExercises.Where(e =>
                    e.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    e.MuscleGroup.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    (e.Description ?? string.Empty).Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
                  .ToList();

            var groupedExercises = filtered
                .GroupBy(e => string.IsNullOrWhiteSpace(e.MuscleGroup) ? "Other" : e.MuscleGroup.Trim())
                .OrderBy(g => GetGroupSortOrder(g.Key))
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (groupedExercises.Count == 0)
            {
                var emptyText = new TextView(this)
                {
                    Text = "No exercises found",
                    TextSize = 15
                };
                emptyText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_secondary)));
                emptyText.SetPadding(0, 8, 0, 0);
                _exerciseListContainer.AddView(emptyText);
                return;
            }

            foreach (var group in groupedExercises)
            {
                var groupName = group.Key;
                var groupExercises = group.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();

                if (!_expandedGroups.ContainsKey(groupName))
                    _expandedGroups[groupName] = true;

                var isExpanded = _expandedGroups[groupName];

                var panel = new LinearLayout(this)
                {
                    Orientation = Orientation.Vertical
                };
                var panelParams = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.WrapContent);
                panelParams.SetMargins(0, 0, 0, 14);
                panel.LayoutParameters = panelParams;
                panel.SetBackgroundResource(Resource.Drawable.bg_card_today_outer);
                panel.SetPadding(18, 18, 18, 18);

                var header = new LinearLayout(this)
                {
                    Orientation = Orientation.Horizontal
                };
                header.SetGravity(GravityFlags.CenterVertical);
                header.SetBackgroundResource(Resource.Drawable.bg_log_tab_inactive);
                header.SetPadding(18, 14, 18, 14);
                header.Clickable = true;
                header.Focusable = true;

                var titleText = new TextView(this)
                {
                    Text = groupName,
                    TextSize = 16
                };
                titleText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
                titleText.SetTypeface(null, TypefaceStyle.Bold);
                titleText.SetPadding(2, 0, 0, 0);
                titleText.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);

                var arrowText = new TextView(this)
                {
                    Text = isExpanded ? "▼" : "▶",
                    TextSize = 16
                };
                arrowText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_secondary)));
                arrowText.SetTypeface(null, TypefaceStyle.Bold);

                header.AddView(titleText);
                header.AddView(arrowText);
                panel.AddView(header);

                var body = new LinearLayout(this)
                {
                    Orientation = Orientation.Vertical
                };
                body.Visibility = isExpanded ? ViewStates.Visible : ViewStates.Gone;
                body.SetPadding(0, 14, 0, 0);

                foreach (var exercise in groupExercises)
                {
                    var itemCard = new LinearLayout(this)
                    {
                        Orientation = Orientation.Horizontal
                    };
                    itemCard.SetGravity(GravityFlags.CenterVertical);
                    itemCard.SetBackgroundResource(Resource.Drawable.bg_card_today_outer);
                    itemCard.SetPadding(28, 28, 28, 28);
                    itemCard.Clickable = true;
                    itemCard.Focusable = true;

                    var itemParams = new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MatchParent,
                        ViewGroup.LayoutParams.WrapContent);
                    itemParams.SetMargins(0, 0, 0, 10);
                    itemCard.LayoutParameters = itemParams;

                    var thumbWrap = new FrameLayout(this);
                    var thumbWrapParams = new LinearLayout.LayoutParams(308, 172);
                    thumbWrap.LayoutParameters = thumbWrapParams;
                    thumbWrap.SetBackgroundResource(Resource.Drawable.bg_exercise_thumbnail_frame);
                    thumbWrap.ClipToOutline = true;

                    var thumbnail = new ImageView(this);
                    thumbnail.SetImageResource(ResolveExerciseThumbnailResource(exercise));
                    thumbnail.SetScaleType(ImageView.ScaleType.CenterCrop);
                    thumbnail.LayoutParameters = new FrameLayout.LayoutParams(
                        ViewGroup.LayoutParams.MatchParent,
                        ViewGroup.LayoutParams.MatchParent);
                    thumbWrap.AddView(thumbnail);

                    var textColumn = new LinearLayout(this)
                    {
                        Orientation = Orientation.Vertical
                    };
                    var textParams = new LinearLayout.LayoutParams(
                        0,
                        ViewGroup.LayoutParams.WrapContent,
                        1f);
                    textParams.SetMargins(20, 0, 20, 0);
                    textColumn.LayoutParameters = textParams;

                    var title = new TextView(this)
                    {
                        Text = exercise.Name,
                        TextSize = 16
                    };
                    title.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
                    title.SetTypeface(null, TypefaceStyle.Bold);

                    var subtitle = new TextView(this)
                    {
                        Text = string.IsNullOrWhiteSpace(exercise.Description) ? exercise.MuscleGroup : exercise.Description,
                        TextSize = 13
                    };
                    subtitle.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_secondary)));
                    subtitle.SetSingleLine(true);
                    subtitle.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
                    subtitle.SetPadding(0, 4, 0, 0);

                    textColumn.AddView(title);
                    textColumn.AddView(subtitle);

                    var actionCircle = new FrameLayout(this);
                    actionCircle.SetBackgroundResource(Resource.Drawable.bg_exercise_action_circle);
                    actionCircle.Clickable = true;
                    actionCircle.Focusable = true;
                    var actionParams = new LinearLayout.LayoutParams(156, 156);
                    actionCircle.LayoutParameters = actionParams;

                    var actionIcon = new ImageView(this);
                    actionIcon.SetImageResource(Resource.Drawable.ic_edit);
                    actionIcon.SetColorFilter(new Android.Graphics.Color(GetColor(Resource.Color.color_on_primary)));
                    actionIcon.LayoutParameters = new FrameLayout.LayoutParams(84, 84)
                    {
                        Gravity = GravityFlags.Center
                    };
                    actionCircle.AddView(actionIcon);

                    itemCard.AddView(thumbWrap);
                    itemCard.AddView(textColumn);
                    itemCard.AddView(actionCircle);

                    var clickedExercise = exercise;
                    itemCard.Click += (s, e) => ShowLogExerciseDialog(clickedExercise);
                    actionCircle.Click += (s, e) => ShowLogExerciseDialog(clickedExercise);

                    body.AddView(itemCard);
                }

                panel.AddView(body);

                header.Click += (s, e) =>
                {
                    _expandedGroups[groupName] = !_expandedGroups[groupName];
                    LoadExercises();
                };

                _exerciseListContainer.AddView(panel);
            }
        }

        private static int GetGroupSortOrder(string groupName)
        {
            return groupName.ToLowerInvariant() switch
            {
                "chest" => 1,
                "legs" => 2,
                "back" => 3,
                "shoulders" => 4,
                "arms" => 5,
                "core" => 6,
                _ => 99
            };
        }

        private static int ResolveExerciseIllustrationResource(Exercise exercise)
        {
            var text = $"{exercise.Name} {exercise.MuscleGroup} {(exercise.Description ?? string.Empty)}".ToLowerInvariant();

            if (text.Contains("squat") || text.Contains("lunge") || text.Contains("leg") || text.Contains("calf"))
                return Resource.Drawable.ic_accessibility_new;

            if (text.Contains("plank") || text.Contains("crunch") || text.Contains("core") || text.Contains("abs") || text.Contains("situp") || text.Contains("sit-up"))
                return Resource.Drawable.ic_body_stats_outline;

            if (text.Contains("run") || text.Contains("cardio") || text.Contains("treadmill") || text.Contains("bike") || text.Contains("cycle"))
                return Resource.Drawable.ic_nav_workout;

            if (text.Contains("pull") || text.Contains("row") || text.Contains("deadlift") || text.Contains("chin"))
                return Resource.Drawable.ic_dumbbell;

            if (text.Contains("shoulder") || text.Contains("press") || text.Contains("bench") || text.Contains("push"))
                return Resource.Drawable.ic_fitness_center;

            return Resource.Drawable.ic_m3_person;
        }

        private static int ResolveExerciseThumbnailResource(Exercise exercise)
        {
            _ = exercise;
            return Resource.Drawable.jeanetix_welcome_hero;
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
