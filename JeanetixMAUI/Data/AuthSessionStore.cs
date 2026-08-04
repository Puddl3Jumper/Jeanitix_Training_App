using JeanetixMAUI.Data;

namespace JeanetixMAUI.Data;

public static class AuthSessionStore
{
    private const string KeyRefreshToken = "auth_refresh_token";
    private const string KeyIdToken = "auth_id_token";
    private const string KeyEmail = "auth_email";
    private const string KeyLocalId = "auth_local_id";
    private const string KeyExpiresAtUtc = "auth_expires_at_utc";
    private const string KeyDisplayName = "auth_display_name";

    public static bool HasSession()
    {
        var refreshToken = Preferences.Get(KeyRefreshToken, null);
        return !string.IsNullOrWhiteSpace(refreshToken);
    }

    public static (string RefreshToken, string? IdToken, DateTimeOffset? ExpiresAtUtc) ReadSessionTokens()
    {
        var refreshToken = Preferences.Get(KeyRefreshToken, null) ?? string.Empty;
        var idToken = Preferences.Get(KeyIdToken, null);
        var expiresText = Preferences.Get(KeyExpiresAtUtc, null);

        DateTimeOffset? expiresAt = null;
        if (long.TryParse(expiresText, out var unixSeconds) && unixSeconds > 0)
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        return (refreshToken, idToken, expiresAt);
    }

    public static string? ReadEmail() => Preferences.Get(KeyEmail, null);
    public static string? ReadLocalId() => Preferences.Get(KeyLocalId, null);
    public static string? ReadDisplayName() => Preferences.Get(KeyDisplayName, null);

    public static void Save(FirebaseAuthSession session)
    {
        var existingEmail = Preferences.Get(KeyEmail, null);
        var existingLocalId = Preferences.Get(KeyLocalId, null);
        var existingDisplayName = Preferences.Get(KeyDisplayName, null);

        var emailToStore = string.IsNullOrWhiteSpace(session.Email) ? existingEmail : session.Email;
        var localIdToStore = string.IsNullOrWhiteSpace(session.LocalId) ? existingLocalId : session.LocalId;
        var displayNameToStore = string.IsNullOrWhiteSpace(session.DisplayName) ? existingDisplayName : session.DisplayName;

        Preferences.Set(KeyRefreshToken, session.RefreshToken);
        Preferences.Set(KeyIdToken, session.IdToken);
        Preferences.Set(KeyExpiresAtUtc, session.ExpiresAtUtc.ToUnixTimeSeconds().ToString());
        if (emailToStore != null) Preferences.Set(KeyEmail, emailToStore);
        if (localIdToStore != null) Preferences.Set(KeyLocalId, localIdToStore);
        if (displayNameToStore != null) Preferences.Set(KeyDisplayName, displayNameToStore);
    }

    public static void Clear()
    {
        Preferences.Remove(KeyRefreshToken);
        Preferences.Remove(KeyIdToken);
        Preferences.Remove(KeyEmail);
        Preferences.Remove(KeyLocalId);
        Preferences.Remove(KeyExpiresAtUtc);
        Preferences.Remove(KeyDisplayName);
    }
}
