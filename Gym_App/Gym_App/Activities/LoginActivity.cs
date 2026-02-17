using Android.Widget;
using Android.Content;
using Android.App;
using Android.Util;
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
        private const string SessionPrefsName = "auth_session";
        private const string SessionLoggedInKey = "is_logged_in";
        private const string SessionEmailKey = "email";
        private const string TestEmail = "test.user@gym.local";
        private readonly SemaphoreSlim _oauthLock = new(1, 1);
        private Button? _googleLoginButton;
        private Button? _primaryLoginButton;
        private EditText? _usernameInput;
        private EditText? _passwordInput;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
                ThemeManager.ApplyTheme(this);
            ActionBar?.Hide();
            SetContentView(Resource.Layout.activity_login);

            _googleLoginButton = FindViewById<Button>(Resource.Id.googleLoginButton);
            _primaryLoginButton = FindViewById<Button>(Resource.Id.primaryLoginButton);
            _usernameInput = FindViewById<EditText>(Resource.Id.loginUsernameInput);
            _passwordInput = FindViewById<EditText>(Resource.Id.loginPasswordInput);

            TryHandleOAuthCallback(Intent);

            if (HasLocalSession())
            {
                StartActivity(new Intent(this, typeof(HomeActivity)));
                Finish();
                return;
            }

            if (_googleLoginButton != null)
            {
                _googleLoginButton.Click += async (s, e) =>
                {
                    await BeginOAuthSignInAsync();
                };
            }

            if (_primaryLoginButton != null)
            {
                _primaryLoginButton.Click += (s, e) =>
                {
                    BeginLocalPasswordSignIn();
                };
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

                var clientId = GetString(Resource.String.oauth_client_id);
                var authorityUrlText = GetString(Resource.String.oauth_authority_url);
                var redirectUrlText = GetString(Resource.String.oauth_redirect_url);
                var scope = GetString(Resource.String.oauth_scope);

                Log.Info(LogTag, $"Starting OIDC login. client_id='{clientId}', authority='{authorityUrlText}', redirect_uri='{redirectUrlText}', scope='{scope}'");

                if (string.IsNullOrWhiteSpace(clientId) || clientId.Contains("YOUR_"))
                {
                    Toast.MakeText(this, "OAuth2 is not configured. Set oauth_client_id in Resources/values/strings.xml.", ToastLength.Long)?.Show();
                    return;
                }

                if (!Uri.TryCreate(authorityUrlText, UriKind.Absolute, out var authorityUrl))
                {
                    Toast.MakeText(this, "OAuth2 authority URL is invalid.", ToastLength.Long)?.Show();
                    return;
                }

                if (!Uri.TryCreate(redirectUrlText, UriKind.Absolute, out var redirectUrl))
                {
                    Toast.MakeText(this, "OAuth2 redirect URL is invalid.", ToastLength.Long)?.Show();
                    return;
                }

                var oidcClient = new OidcClient(new OidcClientOptions
                {
                    Authority = authorityUrl.AbsoluteUri,
                    ClientId = clientId,
                    Scope = scope,
                    RedirectUri = redirectUrl.AbsoluteUri,
                    Browser = new AndroidOidcBrowser(this),
                    Policy = new Policy
                    {
                        Discovery = new DiscoveryPolicy
                        {
                            RequireHttps = false,
                            ValidateIssuerName = false,
                            ValidateEndpoints = false
                        },
                        RequireIdentityTokenSignature = false
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

                var email =
                    loginResult.User?.FindFirst("email")?.Value ??
                    loginResult.User?.FindFirst(ClaimTypes.Email)?.Value ??
                    loginResult.User?.Identity?.Name;

                var resolvedEmail = string.IsNullOrWhiteSpace(email) ? TestEmail : email;
                Log.Info(LogTag, $"OIDC login success. resolved_email='{resolvedEmail}'");
                SaveLocalSession(resolvedEmail);
                Toast.MakeText(this, $"Welcome, {resolvedEmail}", ToastLength.Short)?.Show();
                StartActivity(new Intent(this, typeof(HomeActivity)));
                Finish();
            }
            catch (Exception ex)
            {
                Log.Error(LogTag, ex.ToString());
                Toast.MakeText(this, "Google sign-in crashed. Check Logcat for 'OAuth2' logs.", ToastLength.Long)?.Show();
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

        private bool HasLocalSession()
        {
            var prefs = GetSharedPreferences(SessionPrefsName, FileCreationMode.Private);
            return prefs?.GetBoolean(SessionLoggedInKey, false) == true;
        }

        private void SaveLocalSession(string email)
        {
            var prefs = GetSharedPreferences(SessionPrefsName, FileCreationMode.Private);
            prefs?.Edit()
                ?.PutBoolean(SessionLoggedInKey, true)
                ?.PutString(SessionEmailKey, email)
                ?.Apply();
        }

        private void BeginLocalPasswordSignIn()
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

            var accountExists = AuthCredentialStore.AccountExists(this, username);
            if (!accountExists)
            {
                Toast.MakeText(this, "No account found. Tap 'GET STARTED' to create one.", ToastLength.Long)?.Show();
                return;
            }

            var isValid = AuthCredentialStore.ValidateCredentials(this, username, password);
            if (!isValid)
            {
                Toast.MakeText(this, "Invalid username or password", ToastLength.Short)?.Show();
                return;
            }

            EnsureBasicUserProfile(username);
            SaveLocalSession(username);
            Toast.MakeText(this, "Signed in", ToastLength.Short)?.Show();
            StartActivity(new Intent(this, typeof(HomeActivity)));
            Finish();
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
