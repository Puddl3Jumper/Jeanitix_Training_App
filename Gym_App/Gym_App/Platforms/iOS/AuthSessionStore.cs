using Foundation;

namespace Gym_App.Data;

/// <summary>
/// Persists the Firebase auth session on iOS using NSUserDefaults.
/// Mirrors the Android SharedPreferences implementation in Platforms/Android/AuthSessionStore.cs.
/// </summary>
public static class AuthSessionStore
{
    // Keys are prefixed to mirror Android SharedPreferences namespacing.
    private const string KeyPrefix = "auth_session.";
    private const string KeyRefreshToken = KeyPrefix + "refresh_token";
    private const string KeyIdToken     = KeyPrefix + "id_token";
    private const string KeyEmail       = KeyPrefix + "email";
    private const string KeyLocalId     = KeyPrefix + "local_id";
    private const string KeyExpiresAtUtc = KeyPrefix + "expires_at_utc";
    private const string KeyDisplayName = KeyPrefix + "display_name";

    public static bool HasSession()
    {
        var refreshToken = NSUserDefaults.StandardUserDefaults.StringForKey(KeyRefreshToken);
        return !string.IsNullOrWhiteSpace(refreshToken);
    }

    public static (string RefreshToken, string? IdToken, DateTimeOffset? ExpiresAtUtc) ReadSessionTokens()
    {
        var defaults = NSUserDefaults.StandardUserDefaults;
        var refreshToken = defaults.StringForKey(KeyRefreshToken) ?? string.Empty;
        var idToken = defaults.StringForKey(KeyIdToken);
        var expiresText = defaults.StringForKey(KeyExpiresAtUtc);

        DateTimeOffset? expiresAt = null;
        if (long.TryParse(expiresText, out var unixSeconds) && unixSeconds > 0)
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        return (refreshToken, idToken, expiresAt);
    }

    public static string? ReadEmail()
    {
        return NSUserDefaults.StandardUserDefaults.StringForKey(KeyEmail);
    }

    public static string? ReadLocalId()
    {
        return NSUserDefaults.StandardUserDefaults.StringForKey(KeyLocalId);
    }

    public static string? ReadDisplayName()
    {
        return NSUserDefaults.StandardUserDefaults.StringForKey(KeyDisplayName);
    }

    public static void Save(FirebaseAuthSession session)
    {
        var defaults = NSUserDefaults.StandardUserDefaults;

        var existingEmail       = defaults.StringForKey(KeyEmail);
        var existingLocalId     = defaults.StringForKey(KeyLocalId);
        var existingDisplayName = defaults.StringForKey(KeyDisplayName);

        var emailToStore       = string.IsNullOrWhiteSpace(session.Email)       ? existingEmail       : session.Email;
        var localIdToStore     = string.IsNullOrWhiteSpace(session.LocalId)     ? existingLocalId     : session.LocalId;
        var displayNameToStore = string.IsNullOrWhiteSpace(session.DisplayName) ? existingDisplayName : session.DisplayName;

        defaults.SetString(session.RefreshToken, KeyRefreshToken);
        defaults.SetString(session.IdToken, KeyIdToken);
        defaults.SetString(session.ExpiresAtUtc.ToUnixTimeSeconds().ToString(), KeyExpiresAtUtc);
        if (emailToStore != null) defaults.SetString(emailToStore, KeyEmail);
        if (localIdToStore != null) defaults.SetString(localIdToStore, KeyLocalId);
        if (displayNameToStore != null) defaults.SetString(displayNameToStore, KeyDisplayName);

        defaults.Synchronize();
    }

    public static void Clear()
    {
        var defaults = NSUserDefaults.StandardUserDefaults;
        foreach (var key in new[] { KeyRefreshToken, KeyIdToken, KeyEmail, KeyLocalId, KeyExpiresAtUtc, KeyDisplayName })
            defaults.RemoveObject(key);

        defaults.Synchronize();
    }
}
