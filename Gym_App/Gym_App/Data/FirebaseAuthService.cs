using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Gym_App.Data;

public sealed record FirebaseAuthSession(
    string IdToken,
    string RefreshToken,
    string LocalId,
    string Email,
    DateTimeOffset ExpiresAtUtc,
    string? DisplayName);

public sealed class FirebaseAuthService
{
    private static readonly HttpClient Http = new();

    private readonly FirebaseProjectConfig _config;

    public FirebaseAuthService(FirebaseProjectConfig config)
    {
        _config = config;
    }

    public async Task<FirebaseAuthSession> SignUpWithEmailPasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={Uri.EscapeDataString(_config.WebApiKey)}";
        var payload = new
        {
            email,
            password,
            returnSecureToken = true
        };

        var json = await PostJsonAsync(url, payload, cancellationToken);
        return ParseAuthSession(json);
    }

    public async Task<FirebaseAuthSession> SignInWithEmailPasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={Uri.EscapeDataString(_config.WebApiKey)}";
        var payload = new
        {
            email,
            password,
            returnSecureToken = true
        };

        var json = await PostJsonAsync(url, payload, cancellationToken);
        return ParseAuthSession(json);
    }

    public async Task<FirebaseAuthSession> SignInWithGoogleIdTokenAsync(string googleIdToken, string requestUri, CancellationToken cancellationToken)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={Uri.EscapeDataString(_config.WebApiKey)}";

        // https://cloud.google.com/identity-platform/docs/reference/rest/v1/accounts/signInWithIdp
        var postBody = $"id_token={Uri.EscapeDataString(googleIdToken)}&providerId=google.com";
        var payload = new
        {
            postBody,
            requestUri,
            returnSecureToken = true,
            returnIdpCredential = true
        };

        var json = await PostJsonAsync(url, payload, cancellationToken);
        return ParseAuthSession(json);
    }

    public async Task<FirebaseAuthSession> UpdateProfileDisplayNameAsync(string idToken, string displayName, CancellationToken cancellationToken)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={Uri.EscapeDataString(_config.WebApiKey)}";
        var payload = new
        {
            idToken,
            displayName,
            returnSecureToken = true
        };

        var json = await PostJsonAsync(url, payload, cancellationToken);
        return ParseAuthSession(json);
    }

    public async Task<FirebaseAuthSession> RefreshIdTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var url = $"https://securetoken.googleapis.com/v1/token?key={Uri.EscapeDataString(_config.WebApiKey)}";

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await Http.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ExtractFirebaseErrorMessage(responseText) ?? "Token refresh failed.");
        }

        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;

        // securetoken uses snake_case.
        var idToken = root.GetProperty("id_token").GetString() ?? string.Empty;
        var newRefreshToken = root.GetProperty("refresh_token").GetString() ?? string.Empty;
        var localId = root.GetProperty("user_id").GetString() ?? string.Empty;
        var expiresInSecondsText = root.GetProperty("expires_in").GetString();

        _ = int.TryParse(expiresInSecondsText, out var expiresInSeconds);
        if (expiresInSeconds <= 0) expiresInSeconds = 3600;

        return new FirebaseAuthSession(
            IdToken: idToken,
            RefreshToken: newRefreshToken,
            LocalId: localId,
            Email: string.Empty,
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds - 30),
            DisplayName: null);
    }

    private static FirebaseAuthSession ParseAuthSession(JsonDocument doc)
    {
        var root = doc.RootElement;

        var idToken = root.TryGetProperty("idToken", out var idTokenEl) ? idTokenEl.GetString() : null;
        var refreshToken = root.TryGetProperty("refreshToken", out var refreshTokenEl) ? refreshTokenEl.GetString() : null;
        var localId = root.TryGetProperty("localId", out var localIdEl) ? localIdEl.GetString() : null;
        var email = root.TryGetProperty("email", out var emailEl) ? emailEl.GetString() : null;
        var displayName = root.TryGetProperty("displayName", out var displayNameEl) ? displayNameEl.GetString() : null;

        var expiresInText = root.TryGetProperty("expiresIn", out var expiresInEl) ? expiresInEl.GetString() : null;
        _ = int.TryParse(expiresInText, out var expiresInSeconds);
        if (expiresInSeconds <= 0) expiresInSeconds = 3600;

        if (string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(localId))
            throw new InvalidOperationException("Firebase auth response is missing required fields.");

        return new FirebaseAuthSession(
            IdToken: idToken!,
            RefreshToken: refreshToken!,
            LocalId: localId!,
            Email: email ?? string.Empty,
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds - 30),
            DisplayName: displayName);
    }

    private static async Task<JsonDocument> PostJsonAsync(string url, object payload, CancellationToken cancellationToken)
    {
        var requestJson = JsonSerializer.Serialize(payload);
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await Http.PostAsync(url, content, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ExtractFirebaseErrorMessage(responseText) ?? "Firebase request failed.");
        }

        return JsonDocument.Parse(responseText);
    }

    private static string? ExtractFirebaseErrorMessage(string responseText)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseText);
            if (!doc.RootElement.TryGetProperty("error", out var errorEl))
                return null;

            if (errorEl.TryGetProperty("message", out var messageEl))
            {
                var message = messageEl.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                    return NormalizeFirebaseError(message!);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeFirebaseError(string message)
    {
        return message switch
        {
            "CONFIGURATION_NOT_FOUND" => "Firebase Authentication isn't enabled for this project yet. In Firebase Console → Authentication → Get started, then enable Email/Password and Google.",
            "EMAIL_EXISTS" => "That email is already registered.",
            "INVALID_PASSWORD" => "Incorrect password.",
            "EMAIL_NOT_FOUND" => "No account found for that email.",
            "USER_DISABLED" => "This account has been disabled.",
            "OPERATION_NOT_ALLOWED" => "This sign-in method is not enabled in Firebase Auth.",
            "TOO_MANY_ATTEMPTS_TRY_LATER" => "Too many attempts. Try again later.",
            _ => message
        };
    }
}
