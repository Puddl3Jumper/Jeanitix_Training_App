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
    }

    public static void WriteCrash(string source, Exception? exception)
    {
        try
        {
            var logPath = GetCrashFilePath();
            var body = $"Timestamp: {DateTimeOffset.Now:O}\nSource: {source}\n\n{exception}";
            File.WriteAllText(logPath, body);
        }
        catch
        {
            // Swallow — nothing to do if we can't write the crash log.
        }
    }

    public static string? ReadCrash()
    {
        try
        {
            var logPath = GetCrashFilePath();
            if (!File.Exists(logPath))
                return null;

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
            var logPath = GetCrashFilePath();
            if (File.Exists(logPath))
                File.Delete(logPath);
        }
        catch
        {
        }
    }

    private static string GetCrashFilePath()
    {
        var docs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(docs, CrashFileName);
    }
}
