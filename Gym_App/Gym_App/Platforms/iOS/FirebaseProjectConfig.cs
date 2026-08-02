using Foundation;

namespace Gym_App.Data;

/// <summary>
/// Reads Firebase project configuration from <c>GoogleService-Info.plist</c> embedded in the iOS app bundle.
/// Mirrors the Android <c>FirebaseProjectConfig</c> in Platforms/Android/FirebaseProjectConfig.cs.
/// </summary>
public sealed record FirebaseProjectConfig(
    string ProjectId,
    string BundleId,
    string WebApiKey,
    string? GoogleClientId)
{
    private static readonly object CacheLock = new();
    private static FirebaseProjectConfig? _cached;

    /// <summary>
    /// Loads and caches the Firebase configuration from <c>GoogleService-Info.plist</c>.
    /// Throws <see cref="InvalidOperationException"/> if the file is missing or a required field is absent.
    /// </summary>
    public static FirebaseProjectConfig LoadFromGoogleServiceInfoPlist()
    {
        var cached = _cached;
        if (cached != null)
            return cached;

        var path = NSBundle.MainBundle.PathForResource("GoogleService-Info", "plist")
            ?? throw new InvalidOperationException(
                "GoogleService-Info.plist was not found in the app bundle. " +
                "Add the file to the iOS project and set its Build Action to BundleResource.");

        var dict = NSDictionary.FromFile(path)
            ?? throw new InvalidOperationException("Failed to parse GoogleService-Info.plist.");

        string GetRequired(string key)
        {
            if (dict.TryGetValue(new NSString(key), out var raw) &&
                raw is NSString str &&
                !string.IsNullOrWhiteSpace(str))
            {
                return str!;
            }

            throw new InvalidOperationException(
                $"GoogleService-Info.plist is missing required key '{key}'.");
        }

        string? GetOptional(string key)
        {
            if (dict.TryGetValue(new NSString(key), out var raw) &&
                raw is NSString str &&
                !string.IsNullOrWhiteSpace(str))
            {
                return str!;
            }

            return null;
        }

        var resolved = new FirebaseProjectConfig(
            ProjectId: GetRequired("PROJECT_ID"),
            BundleId: GetRequired("BUNDLE_ID"),
            WebApiKey: GetRequired("API_KEY"),
            GoogleClientId: GetOptional("CLIENT_ID"));

        lock (CacheLock)
        {
            _cached = resolved;
        }

        return resolved;
    }
}
