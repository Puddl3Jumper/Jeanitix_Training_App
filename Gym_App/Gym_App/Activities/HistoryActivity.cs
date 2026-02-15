using Android.Views;
using Android.Widget;
using Android.Content;
using Android.Graphics;
using Gym_App.Data;
using Gym_App.Models;

namespace Gym_App.Activities
{
    [Activity(Label = "Workout History")]
    public class HistoryActivity : Activity
    {
        private GymDatabase? _database;
        private LinearLayout? _historyContainer;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_history);

            _database = new GymDatabase();
            _historyContainer = FindViewById<LinearLayout>(Resource.Id.historyContainer);

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

            LoadHistory();
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

            if (history.Count == 0)
            {
                var noDataText = new TextView(this)
                {
                    Text = "No workout history yet. Start your first workout!",
                    TextSize = 16
                };
                noDataText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_secondary)));
                noDataText.SetPadding(16, 16, 16, 16);
                _historyContainer.AddView(noDataText);
                return;
            }

            if (history.Count == 1)
            {
                _historyContainer.AddView(CreateWorkoutCard(history[0], 0));
                return;
            }

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
                rowParams.SetMargins(0, 0, 0, 16);
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

                var workoutCard = CreateWorkoutCard(workout, 16);

                timelineRow.AddView(timelineColumn);
                timelineRow.AddView(workoutCard);
                _historyContainer.AddView(timelineRow);
            }
        }

        private LinearLayout CreateWorkoutCard(WorkoutSession workout, int bottomMarginDp)
        {
            var workoutCard = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };

            var layoutParams = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent);
            if (bottomMarginDp > 0)
            {
                layoutParams.SetMargins(0, 0, 0, DpToPx(bottomMarginDp));
            }
            workoutCard.LayoutParameters = layoutParams;
            workoutCard.SetBackgroundResource(Resource.Drawable.bg_card);
            workoutCard.SetPadding(16, 16, 16, 16);

            var titleText = new TextView(this)
            {
                Text = workout.Name,
                TextSize = 18
            };
            titleText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_primary)));
            titleText.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
            titleText.SetPadding(0, 0, 0, 8);
            workoutCard.AddView(titleText);

            var dateText = new TextView(this)
            {
                Text = workout.StartTime.ToString("dddd, MMMM dd, yyyy - hh:mm tt"),
                TextSize = 14
            };
            dateText.SetTextColor(new Android.Graphics.Color(Color.White));
            dateText.SetPadding(0, 4, 0, 0);
            workoutCard.AddView(dateText);

            var durationText = new TextView(this)
            {
                Text = $"Duration: {workout.Duration:hh\\:mm\\:ss}",
                TextSize = 14
            };
            durationText.SetTextColor(new Android.Graphics.Color(Color.White));
            durationText.SetPadding(0, 4, 0, 0);
            workoutCard.AddView(durationText);

            var exerciseCountText = new TextView(this)
            {
                Text = $"Exercises: {workout.Exercises.Count}",
                TextSize = 14
            };
            exerciseCountText.SetTextColor(new Android.Graphics.Color(Color.White));
            exerciseCountText.SetPadding(0, 4, 0, 8);
            workoutCard.AddView(exerciseCountText);

            foreach (var exercise in workout.Exercises)
            {
                if (exercise.Exercise != null)
                {
                    var maxLoop = exercise.Sets.Count == 0 ? 0 : exercise.Sets.Max(s => s.LoopNumber);
                    var circuitText = string.IsNullOrWhiteSpace(exercise.CircuitName)
                        ? ""
                        : $" [Circuit {exercise.CircuitName} #{exercise.CircuitOrder}]";
                    var loopText = maxLoop > 1 ? $", Loops: {maxLoop}" : "";

                    var exerciseText = new TextView(this)
                    {
                        Text = $"• {exercise.Exercise.Name}{circuitText}: {exercise.Sets.Count} sets{loopText}",
                        TextSize = 14
                    };
                    exerciseText.SetTextColor(new Android.Graphics.Color(Color.White));
                    exerciseText.SetPadding(16, 2, 0, 2);
                    workoutCard.AddView(exerciseText);
                }
            }

            var actionsRow = new LinearLayout(this)
            {
                Orientation = Orientation.Horizontal
            };
            actionsRow.SetPadding(0, 12, 0, 0);

            var editButton = new Button(this)
            {
                Text = "Edit"
            };
            editButton.SetBackgroundResource(Resource.Drawable.bg_button_primary);
            editButton.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_on_primary)));
            var editParams = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
            editParams.SetMargins(0, 0, 16, 0);
            editButton.LayoutParameters = editParams;
            editButton.Click += (s, e) => ShowEditWorkoutDialog(workout);

            var deleteButton = new Button(this)
            {
                Text = "Delete"
            };
            deleteButton.SetBackgroundResource(Resource.Drawable.bg_button_primary);
            deleteButton.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_on_primary)));
            var deleteParams = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
            deleteParams.SetMargins(16, 0, 0, 0);
            deleteButton.LayoutParameters = deleteParams;
            deleteButton.Click += (s, e) => ConfirmDeleteWorkout(workout.Id);

            actionsRow.AddView(editButton);
            actionsRow.AddView(deleteButton);
            workoutCard.AddView(actionsRow);

            return workoutCard;
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
                var picker = new DatePickerDialog(this, (sender, args) =>
                {
                    selectedDate = new DateTime(args.Year, args.Month + 1, args.DayOfMonth);
                    dateInput.Text = selectedDate.ToString("yyyy-MM-dd");
                }, selectedDate.Year, selectedDate.Month - 1, selectedDate.Day);
                picker.Show();
            };

            var notesInput = new EditText(this)
            {
                Hint = "Workout Notes (optional)",
                Text = workout.Notes ?? string.Empty
            };

            layout.AddView(nameInput);
            layout.AddView(dateInput);
            layout.AddView(notesInput);

            var dialog = new AlertDialog.Builder(this);
            dialog.SetTitle("Edit Workout");
            dialog.SetView(layout);
            dialog.SetPositiveButton("Save", (s, e) =>
            {
                _database.UpdateWorkoutSession(workout.Id, nameInput.Text, selectedDate, notesInput.Text);
                LoadHistory();
                Toast.MakeText(this, "Workout updated", ToastLength.Short)?.Show();
            });
            dialog.SetNegativeButton("Cancel", (s, e) => { });
            dialog.Show();
        }

        private void ConfirmDeleteWorkout(int workoutId)
        {
            if (_database == null)
                return;

            var dialog = new AlertDialog.Builder(this);
            dialog.SetTitle("Delete Workout");
            dialog.SetMessage("This will permanently delete this workout.");
            dialog.SetPositiveButton("Delete", (s, e) =>
            {
                _database.DeleteWorkoutSession(workoutId);
                LoadHistory();
                Toast.MakeText(this, "Workout deleted", ToastLength.Short)?.Show();
            });
            dialog.SetNegativeButton("Cancel", (s, e) => { });
            dialog.Show();
        }
    }
}
