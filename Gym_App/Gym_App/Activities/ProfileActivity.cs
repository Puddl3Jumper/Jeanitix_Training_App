using Android.Content;
using Android.Widget;
using Gym_App.Data;

namespace Gym_App.Activities
{
    [Activity(Label = "Profile")]
    public class ProfileActivity : Activity
    {
        private GymDatabase? _database;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_profile);

            _database = new GymDatabase();

            var homeTab = FindViewById<LinearLayout>(Resource.Id.homeTab);
            var diaryTab = FindViewById<LinearLayout>(Resource.Id.diaryTab);
            var workoutTab = FindViewById<LinearLayout>(Resource.Id.workoutTab);
            var profileTab = FindViewById<LinearLayout>(Resource.Id.profileTab);

            if (homeTab != null) homeTab.Selected = false;
            if (diaryTab != null) diaryTab.Selected = false;
            if (workoutTab != null) workoutTab.Selected = false;
            if (profileTab != null) profileTab.Selected = true;

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

            LoadProfile();
            LoadStats();
        }

        private void LoadProfile()
        {
            var nameValue = FindViewById<TextView>(Resource.Id.profileNameValue);
            var emailValue = FindViewById<TextView>(Resource.Id.profileEmailValue);

            var prefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            var fullName = prefs?.GetString("full_name", string.Empty) ?? string.Empty;
            var email = prefs?.GetString("email", string.Empty) ?? string.Empty;

            if (nameValue != null)
                nameValue.Text = string.IsNullOrWhiteSpace(fullName) ? "Not set" : fullName;

            if (emailValue != null)
                emailValue.Text = string.IsNullOrWhiteSpace(email) ? "Not set" : email;
        }

        private void LoadStats()
        {
            if (_database == null)
                return;

            var workouts = _database.GetWorkoutHistory(1000);

            var totalWorkoutsValue = FindViewById<TextView>(Resource.Id.profileTotalWorkoutsValue);
            var totalDurationValue = FindViewById<TextView>(Resource.Id.profileTotalDurationValue);
            var lastWorkoutValue = FindViewById<TextView>(Resource.Id.profileLastWorkoutValue);

            var totalWorkouts = workouts.Count;
            var totalDuration = workouts.Aggregate(TimeSpan.Zero, (sum, session) => sum + session.Duration);
            var lastWorkout = workouts.OrderByDescending(w => w.StartTime).FirstOrDefault();

            if (totalWorkoutsValue != null)
                totalWorkoutsValue.Text = totalWorkouts.ToString();

            if (totalDurationValue != null)
                totalDurationValue.Text = $"{(int)totalDuration.TotalHours}h {totalDuration.Minutes}m";

            if (lastWorkoutValue != null)
            {
                lastWorkoutValue.Text = lastWorkout == null
                    ? "No records yet"
                    : lastWorkout.StartTime.ToString("MMM dd, yyyy");
            }
        }
    }
}
