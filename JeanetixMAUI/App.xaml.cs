using JeanetixMAUI.Data;

namespace JeanetixMAUI;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = new AppShell();
        var window = new Window(shell);

        // Navigate to login if no session exists
        shell.Loaded += async (s, e) =>
        {
            if (!AuthSessionStore.HasSession())
            {
                await Shell.Current.GoToAsync("LoginPage");
            }
            _ = WorkoutCloudSyncService.InitializeProjectIdCacheAsync();
        };

        return window;
    }
}