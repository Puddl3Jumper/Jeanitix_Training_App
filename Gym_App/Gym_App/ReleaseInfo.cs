using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace Gym_App;

public static class ReleaseInfo
{
    /// <summary>
    /// User-facing version string; matches Android Settings → App info (versionName).
    /// </summary>
    public static string GetDisplayVersion(Context context) =>
        GetAppVersion(context).VersionName;

    public static (string VersionName, long VersionCode) GetAppVersion(Context context)
    {
        try
        {
            var packageName = context.PackageName;
            var packageManager = context.PackageManager;
            if (packageManager == null || string.IsNullOrWhiteSpace(packageName))
            {
                return ("unknown", 0);
            }

            PackageInfo? packageInfo;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                packageInfo = packageManager.GetPackageInfo(
                    packageName,
                    PackageManager.PackageInfoFlags.Of((PackageInfoFlagsLong)0));
            }
            else
            {
#pragma warning disable CS0618 // VersionCode is deprecated on newer Android APIs, but still needed for older devices.
                packageInfo = packageManager.GetPackageInfo(packageName, 0);
#pragma warning restore CS0618
            }

            if (packageInfo == null)
            {
                return ("unknown", 0);
            }

            var versionName = packageInfo.VersionName;

            long versionCode;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            {
                versionCode = packageInfo.LongVersionCode;
            }
            else
            {
#pragma warning disable CS0618 // VersionCode is deprecated on newer Android APIs, but still needed for older devices.
                versionCode = packageInfo.VersionCode;
#pragma warning restore CS0618
            }

            return (string.IsNullOrWhiteSpace(versionName) ? "unknown" : versionName!, versionCode);
        }
        catch
        {
            return ("unknown", 0);
        }
    }
}
