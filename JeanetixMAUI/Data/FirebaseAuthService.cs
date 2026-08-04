using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace JeanetixMAUI.Data;

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

    public FirebaseAuthService(FirebaseProjectConfig config) => _config = config;

    public async Task<FirebaseAuthSession> SignUpWithEmailPasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={Uri.EscapeDataString(_config.WebApiKey)}";
        var payload = new { email, password, returnSecureToken = true };
        var json = await PostJsonAsync(url, payload, cancellationToken);
        return ParseAuthSession(json);
    }

    public async Task<FirebaseAuthSession> SignInWithEmailPasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={Uri.EscapeDataString(_config.WebApiKey)}";
        var payload = new { email, password, returnSecureToken = true };
        var json = await PostJsonAsync(url, payload, cancellationToken);
        return ParseAuthSession(json);
    }

    public async Task<FirebaseAuthSession> UpdateProfileDisplayNameAsync(string idToken, string displayName, CancellationToken cancellationToken)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={Uri.EscapeDataString(_config.WebApiKey)}";
        var payload = new { idToken, displayName, returnSecureToken = true };
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
            throw new InvalidOperationException(ExtractFirebaseErrorMessage(responseText) ?? "Token refresh failed.");

        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;
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

    private async Task<JsonDocument> PostJsonAsync(string url, object payload, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(payload);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await Http.PostAsync(url, content, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(ExtractFirebaseErrorMessage(responseText) ?? response.ReasonPhrase ?? "Request failed.");
        return JsonDocument.Parse(responseText);
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

    private static string? ExtractFirebaseErrorMessage(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText)) return null;
        try
        {
            using var doc = JsonDocument.Parse(responseText);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var msg))
                    return HumanizeFirebaseError(msg.GetString());
            }
        }
        catch { }
        return null;
    }

    private static string HumanizeFirebaseError(string? code) => code switch
    {
        "EMAIL_EXISTS" => "An account with this email already exists.",
        "EMAIL_NOT_FOUND" => "No account found with this email.",
        "INVALID_PASSWORD" => "Incorrect password.",
        "WEAK_PASSWORD : Password should be at least 6 characters" => "Password must be at least 6 characters.",
        "USER_DISABLED" => "This account has been disabled.",
        "TOO_MANY_ATTEMPTS_TRY_LATER" => "Too many failed attempts. Please try again later.",
        "INVALID_LOGIN_CREDENTIALS" => "Invalid email or password.",
        _ => code ?? "Authentication failed."
    };
}
