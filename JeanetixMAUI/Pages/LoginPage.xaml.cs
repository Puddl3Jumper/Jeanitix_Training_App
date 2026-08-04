using JeanetixMAUI.Data;

namespace JeanetixMAUI.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnSignInClicked(object sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Error", "Enter email and password", "OK");
            return;
        }

        SignInButton.IsEnabled = false;
        try
        {
            var config = await FirebaseProjectConfig.LoadAsync();
            if (string.IsNullOrWhiteSpace(config.WebApiKey))
            {
                await DisplayAlert("Error", "Firebase not configured. Check google-services.json.", "OK");
                return;
            }
            var auth = new FirebaseAuthService(config);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var session = await auth.SignInWithEmailPasswordAsync(email, password, cts.Token);
            AuthSessionStore.Save(session);
            EnsureBasicProfile(email, session.DisplayName);
            _ = WorkoutCloudSyncService.InitializeProjectIdCacheAsync();
            await Shell.Current.GoToAsync("//HomePage");
        }
        catch (OperationCanceledException)
        {
            await DisplayAlert("Timeout", "Sign in timed out. Check your connection.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            SignInButton.IsEnabled = true;
        }
    }

    private async void OnCreateAccountClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(CreateAccountPage));
    }

    private async void OnGuestClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//HomePage");
    }

    private static void EnsureBasicProfile(string email, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(email))
            Preferences.Set("profile_email", email);
        var existingName = Preferences.Get("profile_full_name", string.Empty);
        if (string.IsNullOrWhiteSpace(existingName))
            Preferences.Set("profile_full_name", string.IsNullOrWhiteSpace(displayName) ? email : displayName);    }
}
