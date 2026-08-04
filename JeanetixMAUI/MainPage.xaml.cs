using JeanetixCore.Services;

namespace JeanetixMAUI;

public partial class MainPage : ContentPage
{
	private readonly IWorkoutService _workoutService;

	public MainPage(IWorkoutService workoutService)
	{
		InitializeComponent();
		_workoutService = workoutService;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadWorkoutHistoryAsync();
	}

	private async Task LoadWorkoutHistoryAsync()
	{
		try
		{
			var workouts = await _workoutService.GetWorkoutHistoryAsync();
			WorkoutList.ItemsSource = workouts.Take(5).ToList();
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Failed to load workouts: {ex.Message}", "OK");
		}
	}
}
