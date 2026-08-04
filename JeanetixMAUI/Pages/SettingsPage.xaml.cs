using JeanetixMAUI.Data;

namespace JeanetixMAUI.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        var unit = Preferences.Get("profile_unit", "kg");
        UnitPicker.SelectedItem = unit;
        GoalEntry.Text = Preferences.Get("profile_weekly_goal", 4).ToString();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (UnitPicker.SelectedItem is string unit) Preferences.Set("profile_unit", unit);
        if (int.TryParse(GoalEntry.Text, out var goal) && goal > 0) Preferences.Set("profile_weekly_goal", goal);
        await DisplayAlert("Saved", "Settings saved.", "OK");
        await Shell.Current.GoToAsync("..");
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            var db = new GymDatabase();
            var csv = db.ExportWorkoutsCsv();
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JeanetixApp", "workouts_export.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, csv);
            await DisplayAlert("Exported", $"Workouts exported to:\n{path}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
