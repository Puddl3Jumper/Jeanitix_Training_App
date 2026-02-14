using Android.Content;
using Android.Widget;

namespace Gym_App.Activities
{
    [Activity]
    public class CreateAccountActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            ActionBar?.Hide();
            SetContentView(Resource.Layout.activity_create_account);

            var nameInput = FindViewById<EditText>(Resource.Id.createNameInput);
            var emailInput = FindViewById<EditText>(Resource.Id.createEmailInput);
            var passwordInput = FindViewById<EditText>(Resource.Id.createPasswordInput);
            var createButton = FindViewById<Button>(Resource.Id.createAccountButton);

            if (createButton != null)
            {
                createButton.Click += (s, e) =>
                {
                    var fullName = nameInput?.Text?.Trim() ?? string.Empty;
                    var email = emailInput?.Text?.Trim() ?? string.Empty;
                    var password = passwordInput?.Text ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                    {
                        Toast.MakeText(this, "Please complete all fields", ToastLength.Short)?.Show();
                        return;
                    }

                    if (!Android.Util.Patterns.EmailAddress.Matcher(email).Matches())
                    {
                        Toast.MakeText(this, "Please enter a valid email", ToastLength.Short)?.Show();
                        return;
                    }

                    if (password.Length < 6)
                    {
                        Toast.MakeText(this, "Password must be at least 6 characters", ToastLength.Short)?.Show();
                        return;
                    }

                    var prefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
                    var editor = prefs?.Edit();
                    editor?.PutString("full_name", fullName);
                    editor?.PutString("email", email);
                    editor?.Apply();

                    Toast.MakeText(this, "Account created", ToastLength.Short)?.Show();
                    StartActivity(new Intent(this, typeof(HomeActivity)));
                    Finish();
                };
            }
        }
    }
}
