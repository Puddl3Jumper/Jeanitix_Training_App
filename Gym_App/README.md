# GYM Tracker App

A comprehensive Android gym tracking application built with .NET 10 for Android (MAUI/Xamarin).

## Features

### ??? Workout Tracking
- **Start New Workouts**: Begin a new workout session with automatic time tracking
- **Add Exercises**: Select from a library of pre-defined exercises
- **Circuit Support**: Add exercises to named circuits (for example, Circuit A)
- **Loop Logging**: Record loop/round numbers for circuit sets
- **Track Sets**: Record sets with reps and weight for each exercise
- **Real-time Duration**: See how long your workout has been going
- **Continue Workouts**: Resume an in-progress workout session

### ?? Exercise Library
- **15+ Pre-defined Exercises** organized by muscle group:
  - **Chest**: Bench Press, Incline Bench Press, Push-ups
  - **Legs**: Squat, Leg Press, Lunges
  - **Back**: Deadlift, Pull-ups, Bent Over Row
  - **Shoulders**: Shoulder Press, Lateral Raises
  - **Arms**: Bicep Curls, Tricep Dips
  - **Core**: Plank, Crunches
- **Add Custom Exercises**: Create your own exercises with name, muscle group, and description

### ?? Workout History
- View all completed workouts
- See workout date, duration, and exercises performed
- Track total number of sets per exercise
- See circuit labels and loop counts for circuit exercises
- Review your progress over time

### ?? Data Persistence
- All data is saved locally using JSON serialization
- Data persists between app sessions
- Stored in app's local data directory

## Project Structure

```
Gym_App/
??? Activities/
?   ??? WorkoutActivity.cs         # Active workout screen
?   ??? ExerciseLibraryActivity.cs # Exercise library browser
?   ??? HistoryActivity.cs         # Workout history viewer
??? Models/
?   ??? Exercise.cs                # Exercise model
?   ??? WorkoutSession.cs          # Workout session model
?   ??? WorkoutExercise.cs         # Exercise in a workout
?   ??? WorkoutSet.cs              # Individual set data
??? Data/
?   ??? GymDatabase.cs             # Data management and persistence
??? Resources/layout/
?   ??? activity_main.xml          # Main menu layout
?   ??? activity_workout.xml       # Workout screen layout
?   ??? activity_exercise_library.xml
?   ??? activity_history.xml
??? MainActivity.cs                # App entry point
```

## How to Use

### Starting a Workout
1. Open the app
2. Tap "Start New Workout"
3. Tap "Add Exercise / Circuit" to select exercises from the library
4. Choose to add the exercise as regular or add it to a named circuit
5. For each exercise, tap "Add Set" and enter reps and weight
6. For circuit exercises, also enter the loop number for that set
7. When finished, tap "Finish Workout"

### Viewing Exercise Library
1. From the main menu, tap "Exercise Library"
2. Browse exercises organized by muscle group
3. Tap "Add Custom Exercise" to create your own

### Checking History
1. From the main menu, tap "View Workout History"
2. See all your completed workouts with details
3. Review exercises, sets, and duration for each session

## Technical Details

- **Framework**: .NET 10 for Android
- **Minimum Android Version**: API 24 (Android 7.0)
- **Data Storage**: JSON file in local app data directory
- **Architecture**: Simple MVVM-like pattern with Activities and Models

## Future Enhancements

Potential features to add:
- Charts and graphs for progress visualization
- Export workout data to CSV
- Workout templates/routines
- Rest timer between sets
- Body measurements tracking
- Exercise photos/videos
- Cloud sync
- Dark mode support

## Building the App

1. Ensure you have .NET 10 SDK installed
2. Open the solution in Visual Studio 2022 or later
3. Build and run on an Android emulator or device

```bash
dotnet build Gym_App/Gym_App.csproj
```

## Automated Signed APK Release (GitHub Release + GitHub Pages)

This repo includes a workflow at `.github/workflows/android-release.yml`.

### What it does
- Triggers when you push a tag like `v1.2.0`
- Builds a **signed APK**
- Generates a changelog from git commits
- Uploads the APK + changelog to **GitHub Releases**
- Publishes a download page + changelog to **GitHub Pages**

### Required repository secrets
Add these in GitHub repository settings → Secrets and variables → Actions:

- `ANDROID_KEYSTORE_BASE64` (base64 content of your `.keystore` / `.jks`)
- `ANDROID_KEYSTORE_PASSWORD`
- `ANDROID_KEY_ALIAS`
- `ANDROID_KEY_PASSWORD`

### Trigger a release

```bash
git tag v1.2.0
git push origin v1.2.0
```

### GitHub Pages setting
In repository settings → Pages, ensure source is **GitHub Actions**.

## License

This is a sample project created for personal gym tracking.
