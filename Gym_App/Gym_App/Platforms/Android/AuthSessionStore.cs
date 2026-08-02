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

    public static string? ReadLocalId(Context context)
    {
        var prefs = context.GetSharedPreferences(SessionPrefsName, FileCreationMode.Private);
        return prefs?.GetString(KeyLocalId, null);
    }

    public static void Save(Context context, FirebaseAuthSession session)
    {
        var prefs = context.GetSharedPreferences(SessionPrefsName, FileCreationMode.Private);
        if (prefs == null)
            return;

        var editor = prefs.Edit();
        if (editor == null)
            return;

        var existingEmail = prefs.GetString(KeyEmail, null);
        var existingLocalId = prefs.GetString(KeyLocalId, null);
        var existingDisplayName = prefs.GetString(KeyDisplayName, null);

        var emailToStore = string.IsNullOrWhiteSpace(session.Email) ? existingEmail : session.Email;
        var localIdToStore = string.IsNullOrWhiteSpace(session.LocalId) ? existingLocalId : session.LocalId;
        var displayNameToStore = string.IsNullOrWhiteSpace(session.DisplayName) ? existingDisplayName : session.DisplayName;

        editor.PutString(KeyRefreshToken, session.RefreshToken);
        editor.PutString(KeyIdToken, session.IdToken);
        editor.PutString(KeyExpiresAtUtc, session.ExpiresAtUtc.ToUnixTimeSeconds().ToString());
        editor.PutString(KeyEmail, emailToStore);
        editor.PutString(KeyLocalId, localIdToStore);
        editor.PutString(KeyDisplayName, displayNameToStore);
        editor.Apply();
    }

    public static void Clear(Context context)
    {
        var prefs = context.GetSharedPreferences(SessionPrefsName, FileCreationMode.Private);
        prefs?.Edit()?.Clear()?.Apply();
    }
}
