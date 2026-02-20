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
            var createButton = FindViewById(Resource.Id.createAccountButton);

            if (createButton != null)
            {
                createButton.Click += async (s, e) =>
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

                    try
                    {
                        createButton.Enabled = false;

                        var config = FirebaseProjectConfig.LoadFromGoogleServicesJson(this);
                        var auth = new FirebaseAuthService(config);

                        var session = await auth.SignUpWithEmailPasswordAsync(email, password, CancellationToken.None);
                        if (!string.IsNullOrWhiteSpace(fullName))
                        {
                            try
                            {
                                session = await auth.UpdateProfileDisplayNameAsync(session.IdToken, fullName, CancellationToken.None);
                                if (string.IsNullOrWhiteSpace(session.Email))
                                {
                                    session = session with { Email = email };
                                }
                            }
                            catch (Exception updateEx)
                            {
                                Android.Util.Log.Warn("FirebaseAuth", $"Profile update failed after sign-up: {updateEx}");
                            }
                        }

                        if (string.IsNullOrWhiteSpace(session.Email))
                        {
                            session = session with { Email = email };
                        }

                        AuthSessionStore.Save(this, session);
                    }
                    catch (Exception ex)
                    {
                        Android.Util.Log.Error("FirebaseAuth", ex.ToString());
                        Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
                        return;
                    }
                    finally
                    {
                        createButton.Enabled = true;
                    }

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

                    Toast.MakeText(this, "Account created", ToastLength.Short)?.Show();
                    StartActivity(new Intent(this, typeof(HomeActivity)));
                    Finish();
                };
            }
        }
    }
}
