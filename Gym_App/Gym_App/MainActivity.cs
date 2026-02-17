using Android.Content;
using Android.Widget;
using Android.Views;
using Gym_App.Activities;

namespace Gym_App
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : Activity
    {
        private View? _startWorkoutButton;
        private View? _continueWorkoutButton;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            try
            {
                ThemeManager.ApplyTheme(this);
                base.OnCreate(savedInstanceState);
                GymApplication.InstallGlobalCrashHandlers();

                var previousCrash = GymApplication.ReadCrash();
                if (!string.IsNullOrWhiteSpace(previousCrash))
                {
                    var crashText = new TextView(this)
                    {
                        Text = "Previous startup crash detected:\n\n" + previousCrash,
                        TextSize = 13f
                    };
                    crashText.SetPadding(24, 24, 24, 24);
                    SetContentView(crashText);
                    GymApplication.ClearCrash();
                    return;
                }

                SetContentView(Resource.Layout.activity_main);

                _startWorkoutButton = FindViewById(Resource.Id.startWorkoutMainButton);
                _continueWorkoutButton = FindViewById(Resource.Id.continueWorkoutMainButton);

                if (_startWorkoutButton != null)
                {
                    _startWorkoutButton.Enabled = true;
                    _startWorkoutButton.Clickable = true;
                    _startWorkoutButton.Click += StartWorkoutButton_Click;
                }

                if (_continueWorkoutButton != null)
                {
                    _continueWorkoutButton.Enabled = true;
                    _continueWorkoutButton.Clickable = true;
                    _continueWorkoutButton.Click += ContinueWorkoutButton_Click;
                }
            }
            catch (Exception ex)
            {
                GymApplication.WriteCrash("MainActivity.OnCreate", ex);

                var errorText = new TextView(this)
                {
                    Text = "App startup error:\n\n" + ex,
                    TextSize = 14f
                };
                errorText.SetPadding(24, 24, 24, 24);
                SetContentView(errorText);
            }
        }

        protected override void OnResume()
        {
            base.OnResume();
            UpdateUI();
        }

        private void UpdateUI()
        {
        }

        private void StartWorkoutButton_Click(object? sender, EventArgs e)
        {
            var intent = new Intent(this, typeof(CreateAccountActivity));
            StartActivity(intent);
        }

        private void ContinueWorkoutButton_Click(object? sender, EventArgs e)
        {
            var intent = new Intent(this, typeof(LoginActivity));
            StartActivity(intent);
        }
    }
}