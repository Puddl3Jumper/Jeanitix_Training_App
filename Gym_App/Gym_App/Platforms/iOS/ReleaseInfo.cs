using Foundation;

namespace Gym_App;

/// <summary>
/// Reads the iOS app bundle version info from NSBundle.
/// Mirrors the Android ReleaseInfo in Platforms/Android/ReleaseInfo.cs.
/// </summary>
public static class ReleaseInfo
{
    /// <summary>User-facing version string (CFBundleShortVersionString), e.g. "2026.28.1".</summary>
    public static string GetDisplayVersion() => GetAppVersion().VersionName;

    public static (string VersionName, long VersionCode) GetAppVersion()
    {
        try
        {
            var versionName = NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleShortVersionString")?.ToString()
                ?? "unknown";
            var versionCodeStr = NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleVersion")?.ToString() ?? "0";
            long.TryParse(versionCodeStr, out var versionCode);
            return (versionName, versionCode);
        }
        catch
        {
            return ("unknown", 0);
        }
    }
}
