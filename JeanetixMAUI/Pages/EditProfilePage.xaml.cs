namespace JeanetixMAUI.Pages;

public partial class EditProfilePage : ContentPage
{
    public EditProfilePage()
    {
        InitializeComponent();
        NameEntry.Text = Preferences.Get("profile_full_name", string.Empty);
        HeightEntry.Text = Preferences.Get("profile_height_cm", string.Empty);
        WeightEntry.Text = Preferences.Get("profile_weight", string.Empty);
        var unit = Preferences.Get("profile_unit", "kg");
        UnitPicker.SelectedItem = unit;
        GoalEntry.Text = Preferences.Get("profile_weekly_goal", 4).ToString();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        Preferences.Set("profile_full_name", NameEntry.Text?.Trim() ?? string.Empty);
        Preferences.Set("profile_height_cm", HeightEntry.Text?.Trim() ?? string.Empty);
        Preferences.Set("profile_weight", WeightEntry.Text?.Trim() ?? string.Empty);
        if (UnitPicker.SelectedItem is string unit) Preferences.Set("profile_unit", unit);
        if (int.TryParse(GoalEntry.Text, out var goal) && goal > 0) Preferences.Set("profile_weekly_goal", goal);
        await Shell.Current.GoToAsync("..");
    }
}
