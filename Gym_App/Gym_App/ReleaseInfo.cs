using Android.Content;
using Android.Content.PM;
using Android.OS;
using System.Globalization;

namespace Gym_App;

public static class ReleaseInfo
{
    private const string VersionAssetFileName = "app_version.txt";

    public static string GetDisplayVersion(Context context)
    {
        var fromAsset = TryReadVersionAsset(context);
        if (IsValidDisplayVersion(fromAsset))
            return fromAsset!;

        var (versionName, _) = GetAppVersion(context);
        if (IsValidDisplayVersion(versionName))
            return versionName!;

        var utc = DateTime.UtcNow;
        return $"{utc.Year}.{ISOWeek.GetWeekOfYear(utc)}.1";
    }

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

    private static string? TryReadVersionAsset(Context context)
    {
        try
        {
            using var stream = context.Assets?.Open(VersionAssetFileName);
            if (stream == null)
                return null;

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Trim();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsValidDisplayVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return !value.Contains("$([", StringComparison.Ordinal)
            && !value.Contains("GetWeekOfYear", StringComparison.OrdinalIgnoreCase);
    }
}
