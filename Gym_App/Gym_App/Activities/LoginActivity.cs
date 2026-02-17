using Android.Widget;
using Android.Content;
using Android.App;
using Android.Util;
using Android.Views;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;
using Gym_App.Data;
using System.Security.Claims;
using System.Threading;

namespace Gym_App.Activities
{
    [Activity(LaunchMode = Android.Content.PM.LaunchMode.SingleTask, Exported = true)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "com.leiyu.GymJournal",
        DataHost = "oauth2redirect")]
    public class LoginActivity : Activity
    {
        private const string LogTag = "OAuth2";
        private readonly SemaphoreSlim _oauthLock = new(1, 1);
        private View? _googleLoginButton;
        private View? _primaryLoginButton;
        private EditText? _usernameInput;
        private EditText? _passwordInput;

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

            TryHandleOAuthCallback(Intent);

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

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            TryHandleOAuthCallback(intent);
        }

        protected override void OnDestroy()
        {
            AndroidOidcBrowser.CancelIfPending();
            base.OnDestroy();
        }

        private async Task BeginOAuthSignInAsync()
        {
            await _oauthLock.WaitAsync();
            try
            {
                if (_googleLoginButton != null)
                {
                    _googleLoginButton.Enabled = false;
                }

                var config = FirebaseProjectConfig.LoadFromGoogleServicesJson(this);
                var googleWebClientId = config.GoogleWebClientId;
                if (string.IsNullOrWhiteSpace(googleWebClientId))
                {
                    Toast.MakeText(this, "Google client ID not found in google-services.json.", ToastLength.Long)?.Show();
                    return;
                }

                var redirectUrlText = GetString(Resource.String.oauth_redirect_url);
                var scope = "openid profile email";
                var authorityUrl = new Uri("https://accounts.google.com");

                Log.Info(LogTag, $"Starting Google OIDC login for Firebase. client_id='{googleWebClientId}', authority='{authorityUrl}', redirect_uri='{redirectUrlText}', scope='{scope}'");

                if (!Uri.TryCreate(redirectUrlText, UriKind.Absolute, out var redirectUrl))
                {
                    Toast.MakeText(this, "OAuth2 redirect URL is invalid.", ToastLength.Long)?.Show();
                    return;
                }

                var oidcClient = new OidcClient(new OidcClientOptions
                {
                    Authority = authorityUrl.AbsoluteUri,
                    ClientId = googleWebClientId,
                    Scope = scope,
                    RedirectUri = redirectUrl.AbsoluteUri,
                    Browser = new AndroidOidcBrowser(this),
                    Policy = new Policy
                    {
                        Discovery = new DiscoveryPolicy
                        {
                            RequireHttps = true
                        },
                        RequireIdentityTokenSignature = true
                    }
                });

                Toast.MakeText(this, "Opening Google sign-in…", ToastLength.Short)?.Show();
                var loginResult = await oidcClient.LoginAsync(new LoginRequest());

                if (loginResult.IsError)
                {
                    Log.Warn(LogTag, $"OIDC login error. Error='{loginResult.Error}', ErrorDescription='{loginResult.ErrorDescription}'");
                    var msg = string.IsNullOrWhiteSpace(loginResult.ErrorDescription)
                        ? loginResult.Error
                        : $"{loginResult.Error}: {loginResult.ErrorDescription}";
                    Toast.MakeText(this, $"Google sign-in failed: {msg}", ToastLength.Long)?.Show();
                    return;
                }

                var googleIdToken = loginResult.IdentityToken;
                if (string.IsNullOrWhiteSpace(googleIdToken))
                {
                    Toast.MakeText(this, "Google sign-in did not return an ID token.", ToastLength.Long)?.Show();
                    return;
                }

                var auth = new FirebaseAuthService(config);
                var session = await auth.SignInWithGoogleIdTokenAsync(googleIdToken, redirectUrl.AbsoluteUri, CancellationToken.None);
                AuthSessionStore.Save(this, session);

                var email =
                    session.Email ??
                    loginResult.User?.FindFirst("email")?.Value ??
                    loginResult.User?.FindFirst(ClaimTypes.Email)?.Value ??
                    loginResult.User?.Identity?.Name ??
                    string.Empty;

                Log.Info(LogTag, $"Firebase sign-in success. email='{email}'");
                Toast.MakeText(this, "Signed in", ToastLength.Short)?.Show();
                StartActivity(new Intent(this, typeof(HomeActivity)));
                Finish();
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
                    _googleLoginButton.Enabled = true;
                }
                _oauthLock.Release();
            }
        }

        private void TryHandleOAuthCallback(Intent? intent)
        {
            var callbackUrl = intent?.DataString;
            if (string.IsNullOrWhiteSpace(callbackUrl))
                return;

            Log.Info(LogTag, $"Received OAuth2 redirect callback: '{callbackUrl}'");
            var completed = AndroidOidcBrowser.Complete(callbackUrl);
            if (!completed)
            {
                Log.Warn(LogTag, "OAuth2 callback received but no pending login task was waiting.");
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

            if (!Patterns.EmailAddress.Matcher(username).Matches())
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
                    _primaryLoginButton.Enabled = false;

                var config = FirebaseProjectConfig.LoadFromGoogleServicesJson(this);
                var auth = new FirebaseAuthService(config);
                var session = await auth.SignInWithEmailPasswordAsync(username, password, CancellationToken.None);
                AuthSessionStore.Save(this, session);

                EnsureBasicUserProfile(username);
                Toast.MakeText(this, "Signed in", ToastLength.Short)?.Show();
                StartActivity(new Intent(this, typeof(HomeActivity)));
                Finish();
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("FirebaseAuth", ex.ToString());
                Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
            }
            finally
            {
                if (_primaryLoginButton != null)
                    _primaryLoginButton.Enabled = true;
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

                StartActivity(new Intent(this, typeof(HomeActivity)));
                Finish();
            }
            catch (Exception ex)
            {
                Log.Warn(LogTag, $"Auto sign-in failed: {ex}");
                AuthSessionStore.Clear(this);
            }
        }

        private void EnsureBasicUserProfile(string username)
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
                editor.PutString("full_name", username);
            }

            var existingEmail = prefs?.GetString("email", string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(existingEmail))
            {
                editor.PutString("email", username);
            }

            var unit = prefs?.GetString("unit", string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(unit))
            {
                editor.PutString("unit", "kg");
            }

            editor.Apply();
        }
    }

    internal sealed class AndroidOidcBrowser : IBrowser
    {
        private readonly Activity _activity;
        private static TaskCompletionSource<BrowserResult>? _taskCompletionSource;

        public AndroidOidcBrowser(Activity activity)
        {
            _activity = activity;
        }

        public Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
        {
            CancelIfPending();
            _taskCompletionSource = new TaskCompletionSource<BrowserResult>();

            Log.Info("OAuth2", $"Launching system browser: '{options.StartUrl}'");

            var browserIntent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(options.StartUrl));
            browserIntent.AddFlags(ActivityFlags.SingleTop);
            _activity.StartActivity(browserIntent);

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    _taskCompletionSource?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel,
                        Error = "User cancelled"
                    });
                });
            }

            return _taskCompletionSource.Task;
        }

        public static void CancelIfPending()
        {
            _taskCompletionSource?.TrySetResult(new BrowserResult
            {
                ResultType = BrowserResultType.UserCancel,
                Error = "Cancelled"
            });
            _taskCompletionSource = null;
        }

        public static bool Complete(string callbackUrl)
        {
            if (_taskCompletionSource == null)
                return false;

            _taskCompletionSource.TrySetResult(new BrowserResult
            {
                ResultType = BrowserResultType.Success,
                Response = callbackUrl
            });
            _taskCompletionSource = null;
            return true;
        }
    }
}
