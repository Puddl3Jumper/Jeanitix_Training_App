using Android.Widget;
using Android.Content;
using Android.App;
using Android.Util;
using Android.Views;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Common.Apis;
using Gym_App.Data;
using System.Threading;

namespace Gym_App.Activities
{
    [Activity(LaunchMode = Android.Content.PM.LaunchMode.SingleTask, Exported = true)]
    public class LoginActivity : Activity
    {
        private const string LogTag = "OAuth2";
        private const int GoogleSignInRequestCode = 9101;

        private readonly SemaphoreSlim _oauthLock = new(1, 1);
        private View? _googleLoginButton;
        private View? _primaryLoginButton;
        private EditText? _usernameInput;
        private EditText? _passwordInput;

        private TaskCompletionSource<GoogleSignInAccount?>? _googleSignInTcs;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
                ThemeManager.ApplyTheme(this);
            ActionBar?.Hide();
            SetContentView(Resource.Layout.activity_login);

            _googleLoginButton = FindViewById(Resource.Id.googleLoginButton);
            _primaryLoginButton = FindViewById(Resource.Id.primaryLoginButton);
            _usernameInput = FindViewById<EditText>(Resource.Id.loginUsernameInput);
            _passwordInput = FindViewById<EditText>(Resource.Id.loginPasswordInput);

            _ = TryAutoSignInAsync();

            if (_googleLoginButton != null)
            {
                _googleLoginButton.Click += async (s, e) =>
                {
                    await BeginOAuthSignInAsync();
                };
            }

            if (_primaryLoginButton != null)
            {
                _primaryLoginButton.Click += async (s, e) => await BeginEmailPasswordSignInAsync();
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode != GoogleSignInRequestCode)
                return;

            var tcs = _googleSignInTcs;
            _googleSignInTcs = null;

            if (tcs == null)
                return;

            if (resultCode != Result.Ok)
            {
                tcs.TrySetResult(null);
                return;
            }

            try
            {
                var task = GoogleSignIn.GetSignedInAccountFromIntent(data);
                var account = (GoogleSignInAccount?)task.GetResult(Java.Lang.Class.FromType(typeof(ApiException)));
                tcs.TrySetResult(account);
            }
            catch (ApiException ex)
            {
                var statusCode = ex.StatusCode;
                Log.Warn(LogTag, $"GoogleSignIn ApiException. StatusCode={statusCode}, Message='{ex.Message}'");

                var help = statusCode switch
                {
                    10 => "Developer error (10). In Firebase Console → Authentication → Sign-in method → Google, ensure it's enabled, then add this app's SHA-1/SHA-256 under Project settings → Your apps → Android, download an updated google-services.json, and rebuild.",
                    7 => "Network error (7). Check emulator internet access and try again.",
                    _ => $"Google sign-in failed (code {statusCode})."
                };

                tcs.TrySetException(new InvalidOperationException(help));
            }
            catch (Exception ex)
            {
                Log.Warn(LogTag, $"GoogleSignIn unexpected error: {ex}");
                tcs.TrySetException(ex);
            }
        }

        private async Task BeginOAuthSignInAsync()
        {
            await _oauthLock.WaitAsync();
            try
            {
                if (_googleLoginButton != null)
                {
                    RunOnUiThread(() => _googleLoginButton.Enabled = false);
                }

                var config = FirebaseProjectConfig.LoadFromGoogleServicesJson(this);
                var googleWebClientId = config.GoogleWebClientId;
                if (string.IsNullOrWhiteSpace(googleWebClientId))
                {
                    Toast.MakeText(this, "Google client ID not found in google-services.json.", ToastLength.Long)?.Show();
                    return;
                }

                var gso = new GoogleSignInOptions.Builder(GoogleSignInOptions.DefaultSignIn)
                    .RequestIdToken(googleWebClientId)
                    .RequestEmail()
                    .Build();

                var googleClient = GoogleSignIn.GetClient(this, gso);

                _googleSignInTcs = new TaskCompletionSource<GoogleSignInAccount?>();
                Toast.MakeText(this, "Opening Google sign-in…", ToastLength.Short)?.Show();
                StartActivityForResult(googleClient.SignInIntent, GoogleSignInRequestCode);

                GoogleSignInAccount? account;
                try
                {
                    account = await _googleSignInTcs.Task;
                }
                finally
                {
                    _googleSignInTcs = null;
                }

                if (account == null)
                {
                    Toast.MakeText(this, "Google sign-in cancelled.", ToastLength.Short)?.Show();
                    return;
                }

                var googleIdToken = account.IdToken;
                if (string.IsNullOrWhiteSpace(googleIdToken))
                {
                    Toast.MakeText(this, "Google sign-in did not return an ID token. Check Firebase/Google OAuth configuration (SHA-1/SHA-256) and try again.", ToastLength.Long)?.Show();
                    return;
                }

                var auth = new FirebaseAuthService(config);
                using var signInTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var session = await auth.SignInWithGoogleIdTokenAsync(googleIdToken, requestUri: "http://localhost", signInTimeoutCts.Token);
                var email = !string.IsNullOrWhiteSpace(session.Email)
                    ? session.Email
                    : (account.Email ?? string.Empty);

                var stitched = string.IsNullOrWhiteSpace(session.Email) && !string.IsNullOrWhiteSpace(email)
                    ? session with { Email = email }
                    : session;

                AuthSessionStore.Save(this, stitched);
                IncrementTrainingLoginCount();

                // Pull workouts immediately after login so Home/Log reflect server data without restart.
                await TryPullWorkoutsAfterAuthAsync();

                EnsureBasicUserProfile(email, stitched.DisplayName);

                Log.Info(LogTag, $"Firebase sign-in success. email='{email}'");
                Toast.MakeText(this, "Signed in", ToastLength.Short)?.Show();
                StartActivity(new Intent(this, typeof(HomeActivity)));
                Finish();
            }
            catch (OperationCanceledException)
            {
                Toast.MakeText(this, "Sign in timed out. Please check network and try again.", ToastLength.Long)?.Show();
            }
            catch (Exception ex)
            {
                Log.Error(LogTag, ex.ToString());
                Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
            }
            finally
            {
                if (_googleLoginButton != null)
                {
                    RunOnUiThread(() => _googleLoginButton.Enabled = true);
                }
                _oauthLock.Release();
            }
        }

        private async Task BeginEmailPasswordSignInAsync()
        {
            var username = _usernameInput?.Text?.Trim() ?? string.Empty;
            var password = _passwordInput?.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                Toast.MakeText(this, "Enter email and password", ToastLength.Short)?.Show();
                return;
            }

            if (Android.Util.Patterns.EmailAddress?.Matcher(username)?.Matches() != true)
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
                if (_primaryLoginButton != null)
                    RunOnUiThread(() => _primaryLoginButton.Enabled = false);

                var config = FirebaseProjectConfig.LoadFromGoogleServicesJson(this);
                var auth = new FirebaseAuthService(config);
                using var signInTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var session = await auth.SignInWithEmailPasswordAsync(username, password, signInTimeoutCts.Token);
                AuthSessionStore.Save(this, session);
                IncrementTrainingLoginCount();

                // Pull workouts immediately after login so Home/Log reflect server data without restart.
                await TryPullWorkoutsAfterAuthAsync();

                EnsureBasicUserProfile(session.Email, session.DisplayName);
                Toast.MakeText(this, "Signed in", ToastLength.Short)?.Show();
                StartActivity(new Intent(this, typeof(HomeActivity)));
                Finish();
            }
            catch (OperationCanceledException)
            {
                Toast.MakeText(this, "Sign in timed out. Please check network and try again.", ToastLength.Long)?.Show();
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("FirebaseAuth", ex.ToString());
                Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
            }
            finally
            {
                if (_primaryLoginButton != null)
                    RunOnUiThread(() => _primaryLoginButton.Enabled = true);
            }
        }

        private async Task TryAutoSignInAsync()
        {
            try
            {
                if (!AuthSessionStore.HasSession(this))
                    return;

                var (refreshToken, _, expiresAtUtc) = AuthSessionStore.ReadSessionTokens(this);
                if (string.IsNullOrWhiteSpace(refreshToken))
                    return;

                var now = DateTimeOffset.UtcNow;
                if (expiresAtUtc.HasValue && expiresAtUtc.Value > now.AddSeconds(10))
                {
                    StartActivity(new Intent(this, typeof(HomeActivity)));
                    Finish();
                    return;
                }

                var config = FirebaseProjectConfig.LoadFromGoogleServicesJson(this);
                var auth = new FirebaseAuthService(config);
                var refreshed = await auth.RefreshIdTokenAsync(refreshToken, CancellationToken.None);

                var email = AuthSessionStore.ReadEmail(this) ?? string.Empty;
                var stitched = refreshed with { Email = email };
                AuthSessionStore.Save(this, stitched);

                EnsureBasicUserProfile(email, stitched.DisplayName);

                StartActivity(new Intent(this, typeof(HomeActivity)));
                Finish();
            }
            catch (Exception ex)
            {
                Log.Warn(LogTag, $"Auto sign-in failed: {ex}");
                AuthSessionStore.Clear(this);
            }
        }

        private async Task TryPullWorkoutsAfterAuthAsync()
        {
            try
            {
                // Best-effort: do not block navigation for too long.
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var database = new GymDatabase();
                await WorkoutCloudSyncService.TryPullAndApplyAsync(database, timeoutCts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Ignore: best-effort sync.
            }
        }

        private void EnsureBasicUserProfile(string email, string? displayName)
        {
            var prefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            var editor = prefs?.Edit();
            if (editor == null)
            {
                return;
            }

            var existingName = prefs?.GetString("full_name", string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(existingName))
            {
                var nameToUse = string.IsNullOrWhiteSpace(displayName) ? email : displayName;
                editor.PutString("full_name", nameToUse);
            }

            var existingEmail = prefs?.GetString("email", string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(existingEmail))
            {
                editor.PutString("email", email);
            }

            var unit = prefs?.GetString("unit", string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(unit))
            {
                editor.PutString("unit", "kg");
            }

            editor.Apply();
        }

        // Keep a simple login-based training cycle counter (1 -> 2 -> 3 -> 1 ...).
        private void IncrementTrainingLoginCount()
        {
            var prefs = GetSharedPreferences("training_plan", FileCreationMode.Private);
            var currentCount = prefs?.GetInt("login_count", 0) ?? 0;
            prefs?.Edit()?.PutInt("login_count", currentCount + 1)?.Apply();
        }
    }
}
