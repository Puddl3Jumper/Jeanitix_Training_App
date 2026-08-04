using JeanetixMAUI.Data;

namespace JeanetixMAUI.Pages;

public partial class CreateAccountPage : ContentPage
{
    public CreateAccountPage() => InitializeComponent();

    private async void OnCreateClicked(object sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim() ?? string.Empty;
        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Error", "Please complete all fields", "OK"); return;
        }
        if (password.Length < 6)
        {
            await DisplayAlert("Error", "Password must be at least 6 characters", "OK"); return;
        }

        CreateButton.IsEnabled = false;
        try
        {
            var config = await FirebaseProjectConfig.LoadAsync();
            if (string.IsNullOrWhiteSpace(config.WebApiKey))
            {
                await DisplayAlert("Error", "Firebase not configured.", "OK"); return;
            }
            var auth = new FirebaseAuthService(config);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var session = await auth.SignUpWithEmailPasswordAsync(email, password, cts.Token);
            if (!string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    using var updateCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    session = await auth.UpdateProfileDisplayNameAsync(session.IdToken, name, updateCts.Token);
                }
                catch { /* best effort */ }
            }
            if (string.IsNullOrWhiteSpace(session.Email)) session = session with { Email = email };
            AuthSessionStore.Save(session);

            Preferences.Set("profile_full_name", name);
            Preferences.Set("profile_email", email);
            Preferences.Set("profile_unit", "lb");

            _ = WorkoutCloudSyncService.InitializeProjectIdCacheAsync();
            await Shell.Current.GoToAsync("//HomePage");
        }
        catch (OperationCanceledException)
        {
            await DisplayAlert("Timeout", "Request timed out. Check your connection.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally { CreateButton.IsEnabled = true; }
    }
}
