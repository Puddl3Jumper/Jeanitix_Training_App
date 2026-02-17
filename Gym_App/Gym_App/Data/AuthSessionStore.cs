using Android.Content;

namespace Gym_App.Data;

public static class AuthSessionStore
{
    private const string SessionPrefsName = "auth_session";

    private const string KeyRefreshToken = "refresh_token";
    private const string KeyIdToken = "id_token";
    private const string KeyEmail = "email";
    private const string KeyLocalId = "local_id";
    private const string KeyExpiresAtUtc = "expires_at_utc";
    private const string KeyDisplayName = "display_name";

    public static bool HasSession(Context context)
    {
        var prefs = context.GetSharedPreferences(SessionPrefsName, FileCreationMode.Private);
        var refreshToken = prefs?.GetString(KeyRefreshToken, null);
        return !string.IsNullOrWhiteSpace(refreshToken);
    }

    public static (string RefreshToken, string? IdToken, DateTimeOffset? ExpiresAtUtc) ReadSessionTokens(Context context)
    {
        var prefs = context.GetSharedPreferences(SessionPrefsName, FileCreationMode.Private);
        var refreshToken = prefs?.GetString(KeyRefreshToken, null) ?? string.Empty;
        var idToken = prefs?.GetString(KeyIdToken, null);
        var expiresText = prefs?.GetString(KeyExpiresAtUtc, null);

        DateTimeOffset? expiresAt = null;
        if (long.TryParse(expiresText, out var unixSeconds) && unixSeconds > 0)
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        return (refreshToken, idToken, expiresAt);
    }

    public static string? ReadEmail(Context context)
    {
        var prefs = context.GetSharedPreferences(SessionPrefsName, FileCreationMode.Private);
        return prefs?.GetString(KeyEmail, null);
    }

    public static void Save(Context context, FirebaseAuthSession session)
    {
        var prefs = context.GetSharedPreferences(SessionPrefsName, FileCreationMode.Private);
        prefs?.Edit()
            ?.PutString(KeyRefreshToken, session.RefreshToken)
            ?.PutString(KeyIdToken, session.IdToken)
            ?.PutString(KeyEmail, session.Email)
            ?.PutString(KeyLocalId, session.LocalId)
            ?.PutString(KeyDisplayName, session.DisplayName)
            ?.PutString(KeyExpiresAtUtc, session.ExpiresAtUtc.ToUnixTimeSeconds().ToString())
            ?.Apply();
    }

    public static void Clear(Context context)
    {
        var prefs = context.GetSharedPreferences(SessionPrefsName, FileCreationMode.Private);
        prefs?.Edit()?.Clear()?.Apply();
    }
}
