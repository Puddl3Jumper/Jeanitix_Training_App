using Android.Util;
using Android.Runtime;

namespace Gym_App;

public static class GymApplication
{
    public const string CrashFileName = "last_crash.txt";

    public static void InstallGlobalCrashHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception;
            WriteCrash("AppDomain.CurrentDomain.UnhandledException", exception);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrash("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
        {
            WriteCrash("AndroidEnvironment.UnhandledExceptionRaiser", args.Exception);
        };
    }

    public static void WriteCrash(string source, Exception? exception)
    {
        try
        {
            var context = Application.Context;
            var logPath = Path.Combine(context.FilesDir!.AbsolutePath!, CrashFileName);
            var body = $"Timestamp: {DateTimeOffset.Now:O}\nSource: {source}\n\n{exception}";
            File.WriteAllText(logPath, body);
            Log.Error("Gym_App", body);
        }
        catch
        {
        }
    }

    public static string? ReadCrash()
    {
        try
        {
            var context = Application.Context;
            var logPath = Path.Combine(context.FilesDir!.AbsolutePath!, CrashFileName);
            if (!File.Exists(logPath))
            {
                return null;
            }

            return File.ReadAllText(logPath);
        }
        catch
        {
            return null;
        }
    }

    public static void ClearCrash()
    {
        try
        {
            var context = Application.Context;
            var logPath = Path.Combine(context.FilesDir!.AbsolutePath!, CrashFileName);
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
        catch
        {
        }
    }
}