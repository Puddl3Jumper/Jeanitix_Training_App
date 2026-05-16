using Android.Content;
using Android.Content.PM;
using Android.OS;
using System.Globalization;

namespace Gym_App;

public static class ReleaseInfo
{
    private const string BuildPatchAssetFileName = "build_patch.txt";

    public static string GetDisplayVersion(Context context)
    {
        var utc = DateTime.UtcNow;
        var year = utc.Year;
        var week = ISOWeek.GetWeekOfYear(utc);
        var patch = ReadBuildPatch(context);
        return $"{year}.{week}.{patch}";
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

    private static int ReadBuildPatch(Context context)
    {
        try
        {
            using var stream = context.Assets?.Open(BuildPatchAssetFileName);
            if (stream == null)
                return 1;

            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd().Trim();
            return int.TryParse(text, out var patch) && patch > 0 ? patch : 1;
        }
        catch
        {
            return 1;
        }
    }
}
