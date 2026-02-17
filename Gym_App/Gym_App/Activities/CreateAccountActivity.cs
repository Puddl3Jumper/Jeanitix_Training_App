using Android.Content;
using Android.Widget;
using Gym_App.Data;

namespace Gym_App.Activities
{
    [Activity]
    public class CreateAccountActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
               ThemeManager.ApplyTheme(this);
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

                    if (AuthCredentialStore.AccountExists(this, email))
                    {
                        Toast.MakeText(this, "Account already exists. Please log in or reset password.", ToastLength.Long)?.Show();
                        return;
                    }

                    AuthCredentialStore.UpsertAccount(this, email, password);

                    var prefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
                    var editor = prefs?.Edit();
                    editor?.Clear();
                    editor?.PutString("full_name", fullName);
                    editor?.PutString("email", email);
                    editor?.PutString("fitness_tag", "Strength");
                    editor?.PutString("training_years", "1");
                    editor?.PutString("training_stage", "Intermediate");
                    editor?.PutString("goal", "Build strength");
                    editor?.PutString("unit", "kg");
                    editor?.Apply();

                    var authPrefs = GetSharedPreferences("auth_session", FileCreationMode.Private);
                    authPrefs?.Edit()
                        ?.PutBoolean("is_logged_in", true)
                        ?.PutString("email", email)
                        ?.Apply();

                    Toast.MakeText(this, "Account created", ToastLength.Short)?.Show();
                    StartActivity(new Intent(this, typeof(HomeActivity)));
                    Finish();
                };
            }
        }
    }
}
