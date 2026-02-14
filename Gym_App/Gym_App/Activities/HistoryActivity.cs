using Android.Views;
using Android.Widget;
using Android.Content;
using Gym_App.Data;

namespace Gym_App.Activities
{
    [Activity(Label = "Workout History")]
    public class HistoryActivity : Activity
    {
        private GymDatabase? _database;
        private LinearLayout? _historyContainer;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
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

            foreach (var workout in history)
            {
                var workoutCard = new LinearLayout(this)
                {
                    Orientation = Orientation.Vertical
                };
                
                var layoutParams = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.WrapContent);
                layoutParams.SetMargins(0, 0, 0, 16);
                workoutCard.LayoutParameters = layoutParams;
                workoutCard.SetBackgroundResource(Resource.Drawable.bg_card);
                workoutCard.SetPadding(16, 16, 16, 16);

                var titleText = new TextView(this)
                {
                    Text = workout.Name,
                    TextSize = 18
                };
                titleText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
                titleText.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
                workoutCard.AddView(titleText);

                var dateText = new TextView(this)
                {
                    Text = workout.StartTime.ToString("dddd, MMMM dd, yyyy - hh:mm tt"),
                    TextSize = 14
                };
                dateText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_secondary)));
                dateText.SetPadding(0, 4, 0, 0);
                workoutCard.AddView(dateText);

                var durationText = new TextView(this)
                {
                    Text = $"Duration: {workout.Duration:hh\\:mm\\:ss}",
                    TextSize = 14
                };
                durationText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_secondary)));
                durationText.SetPadding(0, 4, 0, 0);
                workoutCard.AddView(durationText);

                var exerciseCountText = new TextView(this)
                {
                    Text = $"Exercises: {workout.Exercises.Count}",
                    TextSize = 14
                };
                exerciseCountText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_secondary)));
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
                        exerciseText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
                        exerciseText.SetPadding(16, 2, 0, 2);
                        workoutCard.AddView(exerciseText);
                    }
                }

                _historyContainer.AddView(workoutCard);
            }
        }
    }
}
