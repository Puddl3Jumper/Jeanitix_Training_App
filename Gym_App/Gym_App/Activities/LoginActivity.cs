using Android.Widget;
using Android.Content;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Common.Apis;
using Android.Gms.Tasks;

namespace Gym_App.Activities
{
    [Activity]
    public class LoginActivity : Activity
    {
        private const int GoogleSignInRequestCode = 9001;
        private GoogleSignInClient? _googleSignInClient;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            ActionBar?.Hide();
            SetContentView(Resource.Layout.activity_login);

            var emailInput = FindViewById<EditText>(Resource.Id.emailInput);
            var passwordInput = FindViewById<EditText>(Resource.Id.passwordInput);
            var loginButton = FindViewById<Button>(Resource.Id.loginButton);
            var forgotPasswordText = FindViewById<TextView>(Resource.Id.forgotPasswordText);
            var googleLoginButton = FindViewById<Button>(Resource.Id.googleLoginButton);
            var facebookLoginButton = FindViewById<Button>(Resource.Id.facebookLoginButton);
            var instagramLoginButton = FindViewById<Button>(Resource.Id.instagramLoginButton);

            var signInOptions = new GoogleSignInOptions.Builder(GoogleSignInOptions.DefaultSignIn)
                .RequestEmail()
                .Build();

            _googleSignInClient = GoogleSignIn.GetClient(this, signInOptions);

            var currentAccount = GoogleSignIn.GetLastSignedInAccount(this);
            if (currentAccount != null)
            {
                StartActivity(new Intent(this, typeof(HomeActivity)));
                Finish();
                return;
            }

            if (loginButton != null)
            {
                loginButton.Click += (s, e) =>
                {
                    var email = emailInput?.Text?.Trim() ?? string.Empty;
                    var password = passwordInput?.Text ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                    {
                        Toast.MakeText(this, "Please enter email and password", ToastLength.Short)?.Show();
                        return;
                    }

                    StartActivity(new Intent(this, typeof(HomeActivity)));
                };
            }

            if (forgotPasswordText != null)
            {
                forgotPasswordText.Click += (s, e) =>
                {
                    Toast.MakeText(this, "Password recovery coming soon", ToastLength.Short)?.Show();
                };
            }

            if (googleLoginButton != null)
            {
                googleLoginButton.Click += (s, e) =>
                {
                    if (_googleSignInClient == null)
                    {
                        Toast.MakeText(this, "Google sign-in is not ready", ToastLength.Short)?.Show();
                        return;
                    }

                    var signInIntent = _googleSignInClient.SignInIntent;
                    StartActivityForResult(signInIntent, GoogleSignInRequestCode);
                };
            }

            if (facebookLoginButton != null)
            {
                facebookLoginButton.Click += (s, e) =>
                {
                    Toast.MakeText(this, "Facebook login coming soon", ToastLength.Short)?.Show();
                };
            }

            if (instagramLoginButton != null)
            {
                instagramLoginButton.Click += (s, e) =>
                {
                    Toast.MakeText(this, "Instagram login coming soon", ToastLength.Short)?.Show();
                };
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode != GoogleSignInRequestCode)
            {
                return;
            }

            var signInTask = GoogleSignIn.GetSignedInAccountFromIntent(data);
            HandleGoogleSignInResult(signInTask);
        }

        private void HandleGoogleSignInResult(Android.Gms.Tasks.Task signInTask)
        {
            try
            {
                var account = (GoogleSignInAccount?)signInTask.GetResult(Java.Lang.Class.FromType(typeof(ApiException)));

                if (account == null)
                {
                    Toast.MakeText(this, "Google sign-in failed", ToastLength.Long)?.Show();
                    return;
                }

                Toast.MakeText(this, $"Welcome, {account.DisplayName ?? account.Email}", ToastLength.Short)?.Show();
                StartActivity(new Intent(this, typeof(HomeActivity)));
                Finish();
            }
            catch (ApiException ex)
            {
                var message = ex.StatusCode switch
                {
                    GoogleSignInStatusCodes.NetworkError => "Network error. Check internet and try again.",
                    GoogleSignInStatusCodes.SignInCancelled => "Google sign-in cancelled.",
                    GoogleSignInStatusCodes.SignInCurrentlyInProgress => "Google sign-in is already in progress.",
                    GoogleSignInStatusCodes.DeveloperError => "Google sign-in is not configured yet for this build.",
                    _ => $"Google sign-in failed ({ex.StatusCode})."
                };

                Toast.MakeText(this, message, ToastLength.Long)?.Show();
            }
        }
    }
}
