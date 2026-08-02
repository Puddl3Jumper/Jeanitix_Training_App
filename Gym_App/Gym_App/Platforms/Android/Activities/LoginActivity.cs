#pragma warning disable CS0618 // Google Sign-In types are obsolete in Play Services Auth 121.x; still required until Credential Manager migration.
using Android.Widget;
using Android.Content;
using Android.App;
using Android.Util;
using Android.Views;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Common.Apis;
using Gym_App.Data;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace Gym_App.Activities
{
    [Activity(LaunchMode = Android.Content.PM.LaunchMode.SingleTask, Exported = true)]
    public class LoginActivity : Activity
    {
        private const string LogTag = "OAuth2";
        private const int GoogleSignInRequestCode = 9101;
        private static readonly TimeSpan AuthTimeout = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan GoogleUiTimeout = TimeSpan.FromSeconds(75);
        private static readonly HttpClient Http = new();

        private readonly SemaphoreSlim _oauthLock = new(1, 1);
        private View? _googleLoginButton;
        private View? _primaryLoginButton;
        private EditText? _usernameInput;
        private EditText? _passwordInput;

        private TaskCompletionSource<GoogleSignInAccount?>? _googleSignInTcs;
        private FirebaseProjectConfig? _firebaseConfig;
        private FirebaseAuthService? _firebaseAuthService;
        private GoogleSignInClient? _googleSignInClient;

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

            InitializeAuthDependencies();

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
                    12501 => "Google sign-in was canceled.",
                    12500 => "Google sign-in failed. Check Google Play services and Google account setup on this device.",
                    _ => $"Google sign-in failed (code {statusCode})."
                };

                if (statusCode == 12501)
                {
                    tcs.TrySetResult(null);
                }
                else
                {
                    tcs.TrySetException(new InvalidOperationException(help));
                }
            }
            catch (Exception ex)
            {
                Log.Warn(LogTag, $"GoogleSignIn unexpected error: {ex}");
                tcs.TrySetException(ex);
            }
        }

        private async Task BeginOAuthSignInAsync()
        {
            if (!await _oauthLock.WaitAsync(0))
            {
                Toast.MakeText(this, "Sign in already in progress…", ToastLength.Short)?.Show();
                return;
            }

            try
            {
                SetSignInButtonsEnabled(false);

                var config = EnsureFirebaseConfig();
                var googleWebClientId = config.GoogleWebClientId;
                if (string.IsNullOrWhiteSpace(googleWebClientId))
                {
                    Toast.MakeText(this, "Google client ID not found in google-services.json.", ToastLength.Long)?.Show();
                    return;
                }

                var googleClient = EnsureGoogleSignInClient(config);
                var authService = EnsureFirebaseAuthService(config);

                // Fast path used by many apps: if a Google account is already authorized for this app,
                // skip launching account chooser UI and exchange token immediately.
                var cachedAccount = GoogleSignIn.GetLastSignedInAccount(this);
                if (cachedAccount != null && !string.IsNullOrWhiteSpace(cachedAccount.IdToken))
                {
                    await CompleteGoogleSignInAsync(cachedAccount, authService, config);
                    return;
                }

                _googleSignInTcs = new TaskCompletionSource<GoogleSignInAccount?>();
                Toast.MakeText(this, "Opening Google sign-in…", ToastLength.Short)?.Show();
                StartActivityForResult(googleClient.SignInIntent, GoogleSignInRequestCode);

                GoogleSignInAccount? account;
                try
                {
                    var signInUiTask = _googleSignInTcs.Task;
                    var completedTask = await Task.WhenAny(signInUiTask, Task.Delay(GoogleUiTimeout));
                    if (completedTask != signInUiTask)
                    {
                        _googleSignInTcs?.TrySetCanceled();
                        _googleSignInTcs = null;
                        Toast.MakeText(this, "Google sign-in took too long. Please try again.", ToastLength.Long)?.Show();
                        return;
                    }

                    account = await signInUiTask;
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
                await CompleteGoogleSignInAsync(account, authService, config);
            }
            catch (OperationCanceledException)
            {
                Toast.MakeText(this, "Sign in timed out. Please check network and try again.", ToastLength.Long)?.Show();
            }
            catch (Exception ex)
            {
                _googleSignInTcs?.TrySetCanceled();
                _googleSignInTcs = null;
                Log.Error(LogTag, ex.ToString());
                Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
            }
            finally
            {
                SetSignInButtonsEnabled(true);
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

            if (!await _oauthLock.WaitAsync(0))
            {
                Toast.MakeText(this, "Sign in already in progress…", ToastLength.Short)?.Show();
                return;
            }

            try
            {
                SetSignInButtonsEnabled(false);

                var config = EnsureFirebaseConfig();
                var auth = EnsureFirebaseAuthService(config);
                using var signInTimeoutCts = new CancellationTokenSource(AuthTimeout);
                var session = await auth.SignInWithEmailPasswordAsync(username, password, signInTimeoutCts.Token);
                AuthSessionStore.Save(this, session);
                IncrementTrainingLoginCount();

                // Do not block navigation after auth; sync in background.
                StartPostSignInSync();

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
                SetSignInButtonsEnabled(true);
                _oauthLock.Release();
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

                var config = EnsureFirebaseConfig();
                var auth = EnsureFirebaseAuthService(config);
                using var refreshTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var refreshed = await auth.RefreshIdTokenAsync(refreshToken, refreshTimeoutCts.Token);

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

        private async Task CompleteGoogleSignInAsync(GoogleSignInAccount account, FirebaseAuthService auth, FirebaseProjectConfig config)
        {
            var googleIdToken = account.IdToken;
            if (string.IsNullOrWhiteSpace(googleIdToken))
                throw new InvalidOperationException("Google sign-in did not return an ID token.");

            using var signInTimeoutCts = new CancellationTokenSource(AuthTimeout);
            var session = await auth.SignInWithGoogleIdTokenAsync(googleIdToken, requestUri: "http://localhost", signInTimeoutCts.Token);
            var email = !string.IsNullOrWhiteSpace(session.Email)
                ? session.Email
                : (account.Email ?? string.Empty);

            var stitched = string.IsNullOrWhiteSpace(session.Email) && !string.IsNullOrWhiteSpace(email)
                ? session with { Email = email }
                : session;

            AuthSessionStore.Save(this, stitched);
            IncrementTrainingLoginCount();
            StartPostSignInSync();
            EnsureBasicUserProfile(email, stitched.DisplayName);

            Log.Info(LogTag, $"Firebase sign-in success. email='{email}' package='{config.AndroidPackageName}'");
            Toast.MakeText(this, "Signed in", ToastLength.Short)?.Show();
            StartActivity(new Intent(this, typeof(HomeActivity)));
            Finish();
        }

        private void InitializeAuthDependencies()
        {
            try
            {
                var config = EnsureFirebaseConfig();
                _ = EnsureFirebaseAuthService(config);
                _ = EnsureGoogleSignInClient(config);
            }
            catch (Exception ex)
            {
                Log.Warn(LogTag, $"Auth dependency prewarm failed: {ex.Message}");
            }
        }

        private FirebaseProjectConfig EnsureFirebaseConfig()
        {
            _firebaseConfig ??= FirebaseProjectConfig.LoadFromGoogleServicesJson(this);
            return _firebaseConfig;
        }

        private FirebaseAuthService EnsureFirebaseAuthService(FirebaseProjectConfig config)
        {
            _firebaseAuthService ??= new FirebaseAuthService(config);
            return _firebaseAuthService;
        }

        private GoogleSignInClient EnsureGoogleSignInClient(FirebaseProjectConfig config)
        {
            if (_googleSignInClient != null)
                return _googleSignInClient;

            if (string.IsNullOrWhiteSpace(config.GoogleWebClientId))
                throw new InvalidOperationException("Google client ID not found in google-services.json.");

            var gso = new GoogleSignInOptions.Builder(GoogleSignInOptions.DefaultSignIn)
                .RequestIdToken(config.GoogleWebClientId)
                .RequestEmail()
                .Build();

            _googleSignInClient = GoogleSignIn.GetClient(this, gso);
            return _googleSignInClient;
        }

        private void StartPostSignInSync()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                    var database = new GymDatabase();
                    await WorkoutCloudSyncService.TryPullAndApplyAsync(database, timeoutCts.Token).ConfigureAwait(false);
                    await TryPullAndApplyOnlineProfileAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Warn(LogTag, $"Post-sign-in sync skipped: {ex.Message}");
                }
            });
        }

        private async Task TryPullAndApplyOnlineProfileAsync(CancellationToken cancellationToken)
        {
            if (!AuthSessionStore.HasSession(this))
                return;

            var localId = AuthSessionStore.ReadLocalId(this) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(localId))
                return;

            var config = FirebaseProjectConfig.LoadFromGoogleServicesJson(this);
            var (refreshToken, idToken, expiresAtUtc) = AuthSessionStore.ReadSessionTokens(this);
            var token = idToken ?? string.Empty;

            if (string.IsNullOrWhiteSpace(token) || !expiresAtUtc.HasValue || expiresAtUtc.Value <= DateTimeOffset.UtcNow.AddMinutes(1))
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                    return;

                var auth = new FirebaseAuthService(config);
                var refreshed = await auth.RefreshIdTokenAsync(refreshToken, cancellationToken).ConfigureAwait(false);
                var email = AuthSessionStore.ReadEmail(this) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(refreshed.Email) && !string.IsNullOrWhiteSpace(email))
                    refreshed = refreshed with { Email = email };

                AuthSessionStore.Save(this, refreshed);
                token = refreshed.IdToken;
            }

            if (string.IsNullOrWhiteSpace(token))
                return;

            var url = $"https://{config.ProjectId}-default-rtdb.firebaseio.com/users/{Uri.EscapeDataString(localId)}/profile.json?auth={Uri.EscapeDataString(token)}";
            using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return;

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
                return;

            OnlineProfileDto? profile;
            try
            {
                profile = JsonSerializer.Deserialize<OnlineProfileDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return;
            }

            if (profile == null)
                return;

            var prefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            var editor = prefs?.Edit();
            if (editor == null)
                return;

            var emailNormalized = (AuthSessionStore.ReadEmail(this) ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(profile.FullName))
            {
                editor.PutString("full_name", profile.FullName);
                if (!string.IsNullOrWhiteSpace(emailNormalized))
                    editor.PutString($"profile_full_name::{emailNormalized}", profile.FullName);
            }

            if (!string.IsNullOrWhiteSpace(profile.CurrentWeight))
                editor.PutString("current_weight", profile.CurrentWeight);

            if (!string.IsNullOrWhiteSpace(profile.HeightCm))
                editor.PutString("height_cm", profile.HeightCm);

            if (!string.IsNullOrWhiteSpace(profile.Age))
                editor.PutString("age", profile.Age);

            if (!string.IsNullOrWhiteSpace(profile.Sex))
                editor.PutString("sex", profile.Sex);

            if (!string.IsNullOrWhiteSpace(profile.Unit))
                editor.PutString("unit", profile.Unit);

            if (!string.IsNullOrWhiteSpace(emailNormalized))
            {
                var cache = JsonSerializer.Serialize(new OnlineProfileDto
                {
                    FullName = profile.FullName,
                    CurrentWeight = profile.CurrentWeight,
                    HeightCm = profile.HeightCm,
                    Age = profile.Age,
                    Sex = profile.Sex,
                    Unit = profile.Unit
                });
                editor.PutString($"profile_snapshot::{emailNormalized}", cache);
            }

            editor.Apply();
        }

        private void SetSignInButtonsEnabled(bool isEnabled)
        {
            RunOnUiThread(() =>
            {
                if (_googleLoginButton != null)
                    _googleLoginButton.Enabled = isEnabled;

                if (_primaryLoginButton != null)
                    _primaryLoginButton.Enabled = isEnabled;
            });
        }

        private void EnsureBasicUserProfile(string email, string? displayName)
        {
            var prefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            var editor = prefs?.Edit();
            if (editor == null)
            {
                return;
            }

            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            var emailSpecificNameKey = string.IsNullOrWhiteSpace(normalizedEmail)
                ? string.Empty
                : $"profile_full_name::{normalizedEmail}";

            var existingEmail = prefs?.GetString("email", string.Empty) ?? string.Empty;
            var existingName = prefs?.GetString("full_name", string.Empty) ?? string.Empty;

            var existingWeight = prefs?.GetString("current_weight", string.Empty) ?? string.Empty;
            var existingHeight = prefs?.GetString("height_cm", string.Empty) ?? string.Empty;
            var existingAge = prefs?.GetString("age", string.Empty) ?? string.Empty;
            var existingSex = prefs?.GetString("sex", string.Empty) ?? string.Empty;
            var existingUnit = prefs?.GetString("unit", string.Empty) ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(existingEmail) &&
                !string.Equals(existingEmail.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(existingName))
            {
                var previousEmailKey = $"profile_full_name::{existingEmail.Trim().ToLowerInvariant()}";
                editor.PutString(previousEmailKey, existingName);

                var previousSnapshotKey = $"profile_snapshot::{existingEmail.Trim().ToLowerInvariant()}";
                var previousSnapshot = JsonSerializer.Serialize(new OnlineProfileDto
                {
                    FullName = existingName,
                    CurrentWeight = existingWeight,
                    HeightCm = existingHeight,
                    Age = existingAge,
                    Sex = existingSex,
                    Unit = existingUnit
                });
                editor.PutString(previousSnapshotKey, previousSnapshot);
            }

            var savedNameForEmail = !string.IsNullOrWhiteSpace(emailSpecificNameKey)
                ? (prefs?.GetString(emailSpecificNameKey, string.Empty) ?? string.Empty)
                : string.Empty;

            var chosenProfileName = existingName;

            if (!string.IsNullOrWhiteSpace(savedNameForEmail))
            {
                editor.PutString("full_name", savedNameForEmail);
                chosenProfileName = savedNameForEmail;
            }
            else if (string.IsNullOrWhiteSpace(existingName))
            {
                var nameToUse = string.IsNullOrWhiteSpace(displayName) ? email : displayName;
                editor.PutString("full_name", nameToUse);
                chosenProfileName = nameToUse;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                editor.PutString("email", email);
            }

            if (!string.IsNullOrWhiteSpace(emailSpecificNameKey) && !string.IsNullOrWhiteSpace(chosenProfileName))
            {
                editor.PutString(emailSpecificNameKey, chosenProfileName);
            }

            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                var snapshotText = prefs?.GetString($"profile_snapshot::{normalizedEmail}", string.Empty) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(snapshotText))
                {
                    try
                    {
                        var snapshot = JsonSerializer.Deserialize<OnlineProfileDto>(snapshotText, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (snapshot != null)
                        {
                            if (!string.IsNullOrWhiteSpace(snapshot.FullName))
                                editor.PutString("full_name", snapshot.FullName);
                            if (!string.IsNullOrWhiteSpace(snapshot.CurrentWeight))
                                editor.PutString("current_weight", snapshot.CurrentWeight);
                            if (!string.IsNullOrWhiteSpace(snapshot.HeightCm))
                                editor.PutString("height_cm", snapshot.HeightCm);
                            if (!string.IsNullOrWhiteSpace(snapshot.Age))
                                editor.PutString("age", snapshot.Age);
                            if (!string.IsNullOrWhiteSpace(snapshot.Sex))
                                editor.PutString("sex", snapshot.Sex);
                            if (!string.IsNullOrWhiteSpace(snapshot.Unit))
                                editor.PutString("unit", snapshot.Unit);
                        }
                    }
                    catch
                    {
                        // Ignore malformed snapshots.
                    }
                }
                else
                {
                    var currentSnapshot = JsonSerializer.Serialize(new OnlineProfileDto
                    {
                        FullName = chosenProfileName,
                        CurrentWeight = existingWeight,
                        HeightCm = existingHeight,
                        Age = existingAge,
                        Sex = existingSex,
                        Unit = existingUnit
                    });
                    editor.PutString($"profile_snapshot::{normalizedEmail}", currentSnapshot);
                }
            }

            var unit = prefs?.GetString("unit", string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(unit))
            {
                editor.PutString("unit", "lb");
            }

            editor.Apply();
        }

        private sealed class OnlineProfileDto
        {
            public string? FullName { get; set; }
            public string? CurrentWeight { get; set; }
            public string? HeightCm { get; set; }
            public string? Age { get; set; }
            public string? Sex { get; set; }
            public string? Unit { get; set; }
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
