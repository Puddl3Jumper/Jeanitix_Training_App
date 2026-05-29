#pragma warning disable CS0618 // Legacy Camera preview keeps Training-page capture dependency-light.

using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Gym_App.Data;
using Gym_App.Models;
using Gym_App.Services;
using AndroidGraphicsFormat = Android.Graphics.Format;
using AndroidImageFormatType = Android.Graphics.ImageFormatType;
using HardwareCamera = Android.Hardware.Camera;

namespace Gym_App.Activities
{
    [Activity(Label = "Training")]
    public class WorkoutActivity : Activity, ISurfaceHolderCallback, HardwareCamera.IPreviewCallback
    {
        public const string ExtraStartWorkoutTimer = "startWorkoutTimer";
        public const string ExtraWorkoutId = "workoutId";

        private GymDatabase? _database;
        private WorkoutSession? _currentWorkout;
        private Chronometer? _workoutDurationChronometer;
        private bool _isWorkoutTimerRunning;

        private const int RequestCameraPermission = 2201;
        private readonly CameraLoopDetector _loopDetector = new();
        private readonly CameraFrameMotionAnalyzer _motionAnalyzer = new();
        private readonly object _frameLock = new();

        private MediaPipePoseMotionAnalyzer? _poseMotionAnalyzer;
        private SurfaceView? _cameraPreview;
        private ISurfaceHolder? _surfaceHolder;
        private HardwareCamera? _camera;
        private TextView? _loopCountText;
        private TextView? _motionStatusText;
        private TextView? _cameraExerciseNameText;
        private Button? _startStopButton;
        private Button? _saveDetectedLoopsButton;

        private bool _cameraSurfaceReady;
        private bool _isCameraCounting;
        private string _motionSourceText = "Camera";
        private DateTimeOffset _lastCameraUiUpdate = DateTimeOffset.MinValue;
        private long _lastMediaPipeFrameMs;

        private TextView? _focusValueText;
        private ImageView? _upperBodyCardImage;
        private TextView? _upperBodyWorkout1Text;
        private TextView? _upperBodyWorkout2Text;

        private ImageView? _lowerBodyCardImage;
        private TextView? _lowerBodyWorkout1Text;

        private readonly Dictionary<string, int> _muscleSecondsToday = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Chest"] = 0,
            ["Back"] = 0,
            ["Legs"] = 0,
            ["Biceps"] = 0,
            ["Triceps"] = 0,
            ["Shoulders"] = 0,
            ["Core"] = 0
        };

        private const string MuscleTimePrefsName = "muscle_time";

        private readonly Dictionary<string, string> _exerciseByMuscle = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Chest"] = "Bench Press",
            ["Back"] = "Lat Pulldown",
            ["Legs"] = "Barbell Squat",
            ["Shoulders"] = "Overhead Press",
            ["Biceps"] = "Dumbbell Curl",
            ["Triceps"] = "Cable Pushdown",
            ["Core"] = "Plank"
        };

        private string _selectedFocus = "Chest";

        public static Intent CreateIntent(Context context, bool startWorkoutTimer = false, int workoutId = -1)
        {
            var intent = new Intent(context, typeof(WorkoutActivity));
            if (startWorkoutTimer)
            {
                intent.PutExtra(ExtraStartWorkoutTimer, true);
            }

            if (workoutId > 0)
            {
                intent.PutExtra(ExtraWorkoutId, workoutId);
            }

            return intent;
        }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_workout);

            _database = new GymDatabase();
            _poseMotionAnalyzer = MediaPipePoseMotionAnalyzer.TryCreate(this);
            _motionSourceText = _poseMotionAnalyzer == null ? "Camera" : "MediaPipe";
            InitializeWorkoutSession();

            BindViews();
            BindTopActions();
            BindEmbeddedCameraControls();
            BindFinishWorkoutButton();
            BindBottomNav();
            LoadTodayMuscleTimes();

            RefreshScreen();
            SyncWorkoutTimerUi();

            if (_cameraSurfaceReady && HasCameraPermission())
            {
                StartCameraPreview();
            }
        }

        protected override void OnDestroy()
        {
            StopWorkoutTimerUi();
            StopEmbeddedCameraCounting();
            StopCameraPreview();
            _poseMotionAnalyzer?.Dispose();
            _poseMotionAnalyzer = null;
            SaveTodayMuscleTimes();
            base.OnDestroy();
        }

        protected override void OnResume()
        {
            base.OnResume();

            if (_currentWorkout != null)
            {
                _currentWorkout = _database?.GetWorkoutSession(_currentWorkout.Id) ?? _currentWorkout;
            }

            RefreshScreen();
            SyncWorkoutTimerUi();

            if (_cameraSurfaceReady && HasCameraPermission())
            {
                StartCameraPreview();
            }
        }

        protected override void OnPause()
        {
            StopWorkoutTimerUi();
            StopEmbeddedCameraCounting();
            StopCameraPreview();
            base.OnPause();
        }

        private void InitializeWorkoutSession()
        {
            if (_database == null)
                return;

            var startTimer = Intent?.GetBooleanExtra(ExtraStartWorkoutTimer, false) ?? false;
            var workoutId = Intent?.GetIntExtra(ExtraWorkoutId, -1) ?? -1;
            var sessionName = GymDatabase.BuildRoutineSessionName(GetPlannedMuscleGroups());

            if (startTimer)
            {
                _currentWorkout = _database.StartTimedWorkout(
                    sessionName,
                    workoutId > 0 ? workoutId : null,
                    resetStartTime: true);
                _isWorkoutTimerRunning = true;
                return;
            }

            _currentWorkout = workoutId > 0
                ? _database.GetWorkoutSession(workoutId)
                : _database.GetCurrentWorkout();

            _isWorkoutTimerRunning = _currentWorkout is { IsCompleted: false };
        }

        private void BindViews()
        {
            _focusValueText = FindViewById<TextView>(Resource.Id.focusValueText);
            _upperBodyCardImage = FindViewById<ImageView>(Resource.Id.upperBodyCardImage);
            _upperBodyWorkout1Text = FindViewById<TextView>(Resource.Id.upperBodyWorkout1Text);
            _upperBodyWorkout2Text = FindViewById<TextView>(Resource.Id.upperBodyWorkout2Text);

            _lowerBodyCardImage = FindViewById<ImageView>(Resource.Id.lowerBodyCardImage);
            _lowerBodyWorkout1Text = FindViewById<TextView>(Resource.Id.lowerBodyWorkout1Text);
            _workoutDurationChronometer = FindViewById<Chronometer>(Resource.Id.workoutDurationChronometer);

            _cameraPreview = FindViewById<SurfaceView>(Resource.Id.trainingCameraLoopPreview);
            _loopCountText = FindViewById<TextView>(Resource.Id.trainingCameraLoopCountText);
            _motionStatusText = FindViewById<TextView>(Resource.Id.trainingCameraLoopStatusText);
            _cameraExerciseNameText = FindViewById<TextView>(Resource.Id.trainingCameraLoopExerciseText);
            _startStopButton = FindViewById<Button>(Resource.Id.trainingCameraLoopStartStopButton);
            _saveDetectedLoopsButton = FindViewById<Button>(Resource.Id.trainingCameraLoopSaveButton);

            if (_cameraPreview?.Holder != null)
            {
                _surfaceHolder = _cameraPreview.Holder;
                _surfaceHolder.AddCallback(this);
            }
        }

        private void BindTopActions()
        {
            var historyButton = FindViewById<ImageButton>(Resource.Id.trainingHistoryButton);
            if (historyButton != null)
            {
                historyButton.Click += (s, e) => StartActivity(new Intent(this, typeof(HistoryActivity)));
            }
        }

        private void BindEmbeddedCameraControls()
        {
            UpdateEmbeddedCameraExerciseText();
            UpdateCameraCounterViews(new CameraLoopDetectionResult
            {
                TotalLoops = 0,
                SmoothedMotion = 0,
                Phase = CameraLoopPhase.WaitingForMotion
            });
            UpdateCameraStatus(BuildCameraReadyStatus());

            if (_startStopButton != null)
            {
                _startStopButton.Click += (_, _) =>
                {
                    if (_isCameraCounting)
                    {
                        StopEmbeddedCameraCounting();
                    }
                    else
                    {
                        StartEmbeddedCameraCounting();
                    }
                };
            }

            if (_saveDetectedLoopsButton != null)
            {
                _saveDetectedLoopsButton.Click += (_, _) => SaveDetectedCameraLoops();
            }
        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            _cameraSurfaceReady = true;
            _surfaceHolder = holder;
            if (HasCameraPermission())
            {
                StartCameraPreview();
            }
        }

        public void SurfaceChanged(ISurfaceHolder holder, AndroidGraphicsFormat format, int width, int height)
        {
            _surfaceHolder = holder;
            if (HasCameraPermission())
            {
                StartCameraPreview();
            }
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            _cameraSurfaceReady = false;
            StopEmbeddedCameraCounting();
            StopCameraPreview();
        }

        public void OnPreviewFrame(byte[]? data, HardwareCamera? camera)
        {
            if (!_isCameraCounting || data == null || camera == null)
                return;

            HardwareCamera.Size? previewSize;
            try
            {
                previewSize = camera.GetParameters()?.PreviewSize;
            }
            catch
            {
                return;
            }

            if (previewSize == null)
                return;

            var capturedAt = DateTimeOffset.UtcNow;
            CameraLoopDetectionResult? result;
            lock (_frameLock)
            {
                var motionScore = TryAnalyzeCameraMotionScore(data, previewSize.Width, previewSize.Height);
                result = motionScore.HasValue
                    ? _loopDetector.AddSample(motionScore.Value, capturedAt)
                    : null;
            }

            if (result == null)
                return;

            if (result.LoopCompleted || DateTimeOffset.UtcNow - _lastCameraUiUpdate > TimeSpan.FromMilliseconds(250))
            {
                _lastCameraUiUpdate = DateTimeOffset.UtcNow;
                RunOnUiThread(() => UpdateCameraCounterViews(result));
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode != RequestCameraPermission)
                return;

            if (HasCameraPermission())
            {
                UpdateCameraStatus(BuildCameraReadyStatus());
                if (_cameraSurfaceReady)
                {
                    StartCameraPreview();
                }
                StartEmbeddedCameraCounting();
            }
            else
            {
                UpdateCameraStatus("Camera permission is required for automatic loop detection.");
            }
        }

        private bool HasCameraPermission()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.M)
                return true;

            return CheckSelfPermission(Android.Manifest.Permission.Camera) == Permission.Granted;
        }

        private void EnsureCameraPermission()
        {
            if (HasCameraPermission())
                return;

            RequestPermissions(new[] { Android.Manifest.Permission.Camera }, RequestCameraPermission);
        }

        private void StartCameraPreview()
        {
            if (_camera != null || _surfaceHolder == null || !_cameraSurfaceReady)
                return;

            try
            {
                var camera = HardwareCamera.Open() ?? throw new InvalidOperationException("No camera is available.");
                _camera = camera;

                var parameters = camera.GetParameters();
                var previewSize = ChoosePreviewSize(parameters?.SupportedPreviewSizes);
                if (parameters != null && previewSize != null)
                {
                    parameters.SetPreviewSize(previewSize.Width, previewSize.Height);
                    parameters.PreviewFormat = AndroidImageFormatType.Nv21;
                    camera.SetParameters(parameters);
                }

                camera.SetDisplayOrientation(90);
                camera.SetPreviewDisplay(_surfaceHolder);
                camera.SetPreviewCallback(this);
                camera.StartPreview();
            }
            catch (Exception ex)
            {
                UpdateCameraStatus("Unable to start camera: " + ex.Message);
                StopCameraPreview();
            }
        }

        private static HardwareCamera.Size? ChoosePreviewSize(IList<HardwareCamera.Size>? supportedSizes)
        {
            if (supportedSizes == null || supportedSizes.Count == 0)
                return null;

            return supportedSizes
                .OrderBy(size => Math.Abs((size.Width * size.Height) - (640 * 480)))
                .First();
        }

        private void StopCameraPreview()
        {
            try
            {
                _camera?.SetPreviewCallback(null);
                _camera?.StopPreview();
            }
            catch
            {
                // Camera teardown can race with Activity lifecycle callbacks.
            }
            finally
            {
                _camera?.Release();
                _camera = null;
            }
        }

        private void StartEmbeddedCameraCounting()
        {
            if (!HasCameraPermission())
            {
                EnsureCameraPermission();
                return;
            }

            if (_camera == null)
            {
                StartCameraPreview();
            }

            EnsureWorkoutForCameraCounting();

            lock (_frameLock)
            {
                _motionAnalyzer.Reset();
                _poseMotionAnalyzer?.Reset();
                _loopDetector.Reset();
                _lastMediaPipeFrameMs = 0;
            }

            _isCameraCounting = true;
            if (_startStopButton != null)
                _startStopButton.Text = "Stop";

            UpdateEmbeddedCameraExerciseText();
            UpdateCameraStatus($"Detecting with {_motionSourceText}");
        }

        private void StopEmbeddedCameraCounting()
        {
            if (!_isCameraCounting)
                return;

            _isCameraCounting = false;
            if (_startStopButton != null)
                _startStopButton.Text = "Start";

            UpdateCameraStatus("Paused");
        }

        private double? TryAnalyzeCameraMotionScore(byte[] data, int width, int height)
        {
            if (_poseMotionAnalyzer != null)
            {
                var timestampMs = SystemClock.ElapsedRealtime();
                if (timestampMs - _lastMediaPipeFrameMs < 150)
                    return null;

                _lastMediaPipeFrameMs = timestampMs;
                using var bitmap = DecodeNv21Frame(data, width, height);
                if (bitmap == null)
                    return 0d;

                var sample = _poseMotionAnalyzer.Analyze(bitmap, timestampMs);
                _motionSourceText = sample.HasPose
                    ? "MediaPipe"
                    : "No body";
                return sample.HasPose ? sample.MotionScore : 0d;
            }

            _motionSourceText = "Camera";
            return _motionAnalyzer.AnalyzeNv21Frame(data, width, height);
        }

        private static Bitmap? DecodeNv21Frame(byte[] data, int width, int height)
        {
            try
            {
                using var yuvImage = new YuvImage(data, AndroidImageFormatType.Nv21, width, height, null);
                using var stream = new MemoryStream();
                if (!yuvImage.CompressToJpeg(new Rect(0, 0, width, height), 60, stream))
                    return null;

                var jpeg = stream.ToArray();
                return BitmapFactory.DecodeByteArray(jpeg, 0, jpeg.Length);
            }
            catch
            {
                return null;
            }
        }

        private void UpdateCameraCounterViews(CameraLoopDetectionResult result)
        {
            if (_loopCountText != null)
            {
                _loopCountText.Text = result.TotalLoops.ToString();
            }

            if (_motionStatusText != null)
            {
                _motionStatusText.Text = result.Phase switch
                {
                    CameraLoopPhase.MotionActive => "Motion detected",
                    CameraLoopPhase.Cooldown => "Loop counted",
                    _ when string.Equals(_motionSourceText, "No body", StringComparison.Ordinal) => "No body detected",
                    _ => $"{_motionSourceText} motion {result.SmoothedMotion:P0}"
                };
            }
        }

        private string BuildCameraReadyStatus()
        {
            return _poseMotionAnalyzer == null
                ? "Ready"
                : "MediaPipe ready";
        }

        private void UpdateCameraStatus(string message)
        {
            if (_motionStatusText != null)
            {
                _motionStatusText.Text = message;
            }
        }

        private void UpdateEmbeddedCameraExerciseText()
        {
            if (_cameraExerciseNameText != null)
            {
                _cameraExerciseNameText.Text = GetExerciseForMuscle(_selectedFocus);
            }
        }

        private void SaveDetectedCameraLoops()
        {
            var loops = _loopDetector.TotalLoops;
            if (loops <= 0)
            {
                Toast.MakeText(this, "No loops detected yet", ToastLength.Short)?.Show();
                return;
            }

            var session = EnsureWorkoutForCameraCounting();
            var workoutExercise = ResolveCameraWorkoutExercise(session);
            if (workoutExercise == null)
            {
                Toast.MakeText(this, "Unable to save camera loops", ToastLength.Short)?.Show();
                return;
            }

            var nextLoopNumber = workoutExercise.Sets.Count == 0
                ? 1
                : workoutExercise.Sets.Max(set => set.LoopNumber) + 1;
            _database?.AddSetToExercise(
                workoutExercise.Id,
                reps: loops,
                weight: 0,
                notes: "Auto-detected from camera",
                loopNumber: nextLoopNumber);

            Toast.MakeText(this, $"Saved {loops} camera-detected loops", ToastLength.Short)?.Show();
            StopEmbeddedCameraCounting();
            lock (_frameLock)
            {
                _loopDetector.Reset();
                _motionAnalyzer.Reset();
                _poseMotionAnalyzer?.Reset();
            }
            UpdateCameraCounterViews(new CameraLoopDetectionResult
            {
                TotalLoops = 0,
                SmoothedMotion = 0,
                Phase = CameraLoopPhase.WaitingForMotion
            });
        }

        private WorkoutSession EnsureWorkoutForCameraCounting()
        {
            if (_database == null)
                throw new InvalidOperationException("Database is unavailable.");

            if (_currentWorkout == null || _currentWorkout.IsCompleted)
            {
                _currentWorkout = _database.StartTimedWorkout(
                    GymDatabase.BuildRoutineSessionName(GetPlannedMuscleGroups()),
                    resetStartTime: true);
                _isWorkoutTimerRunning = true;
                SyncWorkoutTimerUi();
            }

            return _currentWorkout;
        }

        private WorkoutExercise? ResolveCameraWorkoutExercise(WorkoutSession session)
        {
            if (_database == null)
                return null;

            var exerciseName = GetExerciseForMuscle(_selectedFocus);
            var exercises = _database.GetAllExercises();
            var exercise = exercises.FirstOrDefault(e => string.Equals(e.Name, exerciseName, StringComparison.OrdinalIgnoreCase))
                ?? exercises.FirstOrDefault(e => string.Equals(e.MuscleGroup, _selectedFocus, StringComparison.OrdinalIgnoreCase));

            if (exercise == null)
            {
                exercise = new Exercise
                {
                    Name = exerciseName,
                    MuscleGroup = _selectedFocus,
                    Description = "Created by embedded camera loop detection."
                };
                _database.AddExercise(exercise);
            }

            var refreshed = _database.GetWorkoutSession(session.Id) ?? session;
            var existing = refreshed.Exercises.LastOrDefault(e => e.ExerciseId == exercise.Id);
            if (existing != null)
                return existing;

            _database.AddExerciseToWorkout(refreshed.Id, exercise.Id, circuitName: _selectedFocus);
            return _database.GetWorkoutSession(refreshed.Id)?
                .Exercises
                .LastOrDefault(e => e.ExerciseId == exercise.Id);
        }

        private void BindFinishWorkoutButton()
        {
            var finishButton = FindViewById<Button>(Resource.Id.finishedWorkoutButton);
            if (finishButton == null)
                return;

            finishButton.Click += (_, _) => OnFinishedWorkoutClicked();
        }

        private void OnFinishedWorkoutClicked()
        {
            if (_database == null)
                return;

            if (_currentWorkout == null || !_isWorkoutTimerRunning)
            {
                Toast.MakeText(this, Resource.String.start_workout_before_finish, ToastLength.Short)?.Show();
                return;
            }

            StopWorkoutTimerUi();

            var plannedGroups = GetPlannedMuscleGroups();
            _database.CompleteTodayRoutine(plannedGroups, _currentWorkout.Id);
            _currentWorkout = null;
            _isWorkoutTimerRunning = false;

            Toast.MakeText(this, Resource.String.finished_workout_logged, ToastLength.Short)?.Show();
            StartActivity(new Intent(this, typeof(HistoryActivity)));
        }

        private void SyncWorkoutTimerUi()
        {
            if (_workoutDurationChronometer == null)
                return;

            if (_currentWorkout == null || !_isWorkoutTimerRunning || _currentWorkout.IsCompleted)
            {
                StopWorkoutTimerUi();
                _workoutDurationChronometer.Text = GetString(Resource.String.workout_timer_default);
                return;
            }

            var elapsedMs = Math.Max(0, (long)(DateTime.Now - _currentWorkout.StartTime).TotalMilliseconds);
            _workoutDurationChronometer.Base = SystemClock.ElapsedRealtime() - elapsedMs;
            _workoutDurationChronometer.Start();
        }

        private void StopWorkoutTimerUi()
        {
            _workoutDurationChronometer?.Stop();
        }

        private void RefreshScreen()
        {
            var plannedGroups = GetPlannedMuscleGroups();
            if (plannedGroups.Length > 0)
            {
                _selectedFocus = plannedGroups[0];
            }

            UpdateMuscleTimeViews();
            UpdateLowerBodyViews();
            UpdateTrainingHeader();
            UpdateEmbeddedCameraExerciseText();
            UpdateBottomNavSelection();
        }

        private void UpdateTrainingHeader()
        {
            if (_focusValueText != null)
                _focusValueText.Text = "Upper Body";
        }

        private void UpdateMuscleTimeViews()
        {
            var plannedGroups = GetPlannedMuscleGroups();

            if (plannedGroups.Length >= 2)
            {
                _upperBodyWorkout1Text?.SetText(plannedGroups[0], TextView.BufferType.Normal);
                _upperBodyWorkout2Text?.SetText(plannedGroups[1], TextView.BufferType.Normal);
            }
        }

        private void UpdateLowerBodyViews()
        {
            var plannedGroups = GetPlannedMuscleGroups();

            if (plannedGroups.Length >= 3)
            {
                _lowerBodyWorkout1Text?.SetText(plannedGroups[2], TextView.BufferType.Normal);
            }
        }

        private int GetMuscleSeconds(string muscle)
        {
            if (string.Equals(muscle, "Cardio", StringComparison.OrdinalIgnoreCase))
                muscle = "Core";

            return _muscleSecondsToday.TryGetValue(muscle, out var value)
                ? Math.Max(0, value)
                : 0;
        }

        private string GetExerciseForMuscle(string muscle)
        {
            if (string.Equals(muscle, "Cardio", StringComparison.OrdinalIgnoreCase))
                muscle = "Core";
            else if (string.Equals(muscle, "Abs", StringComparison.OrdinalIgnoreCase))
                muscle = "Core";
            else if (string.Equals(muscle, "Delts", StringComparison.OrdinalIgnoreCase) || string.Equals(muscle, "Shoulder", StringComparison.OrdinalIgnoreCase))
                muscle = "Shoulders";

            return _exerciseByMuscle.TryGetValue(muscle, out var exercise)
                ? exercise
                : "Workout";
        }

        private void ApplyFocusCardImage(ImageView? target, string exerciseName)
        {
            if (target == null)
                return;

            target.SetImageResource(ResolveMuscleImageResource(exerciseName));
            target.ClearColorFilter();
            target.SetScaleType(ImageView.ScaleType.CenterCrop);
        }

        private static int ResolveMuscleImageResource(string exerciseName)
        {
            var label = exerciseName?.Trim() ?? string.Empty;

            if (string.Equals(label, "Chest", StringComparison.OrdinalIgnoreCase))
                return Resource.Drawable.chest_focus;

            if (string.Equals(label, "Biceps", StringComparison.OrdinalIgnoreCase))
                return Resource.Drawable.biceps_focus;

            if (string.Equals(label, "Legs", StringComparison.OrdinalIgnoreCase))
                return Resource.Drawable.squats_focus_no_bg;

            return Resource.Drawable.ic_dumbbell;
        }

        private void LoadTodayMuscleTimes()
        {
            var prefs = GetSharedPreferences(MuscleTimePrefsName, FileCreationMode.Private);
            if (prefs == null)
                return;

            foreach (var muscle in _muscleSecondsToday.Keys.ToList())
            {
                var key = BuildTodayMuscleKey(muscle);
                _muscleSecondsToday[muscle] = Math.Max(0, prefs.GetInt(key, 0));
            }
        }

        private void SaveTodayMuscleTimes()
        {
            var prefs = GetSharedPreferences(MuscleTimePrefsName, FileCreationMode.Private);
            var editor = prefs?.Edit();
            if (editor == null)
                return;

            foreach (var entry in _muscleSecondsToday)
            {
                editor.PutInt(BuildTodayMuscleKey(entry.Key), Math.Max(0, entry.Value));
            }

            editor.Apply();
        }

        private static string BuildTodayMuscleKey(string muscle)
        {
            return $"{DateTime.Today:yyyyMMdd}_{muscle.ToLowerInvariant()}";
        }

        private string GetPlannedFocusText()
        {
            var groups = GetPlannedMuscleGroups();
            return $"{groups[0]}/{groups[1]} + {groups[2]}";
        }

        private string[] GetPlannedMuscleGroups()
        {
            return _database?.GetDailyWorkoutGroups() ?? new[] { "Chest", "Back", "Legs" };
        }

        private void BindBottomNav()
        {
            var homeTab = FindViewById<LinearLayout>(Resource.Id.homeTab);
            var diaryTab = FindViewById<LinearLayout>(Resource.Id.diaryTab);
            var workoutTab = FindViewById<LinearLayout>(Resource.Id.workoutTab);
            var profileTab = FindViewById<LinearLayout>(Resource.Id.profileTab);

            if (homeTab != null)
                homeTab.Click += (s, e) => StartActivity(new Intent(this, typeof(HomeActivity)));

            if (diaryTab != null)
                diaryTab.Click += (s, e) => StartActivity(new Intent(this, typeof(HistoryActivity)));

            if (profileTab != null)
                profileTab.Click += (s, e) => StartActivity(new Intent(this, typeof(ProfileActivity)));

            if (workoutTab != null)
                workoutTab.Click += (s, e) => { };

            var profileLabel = FindViewById<TextView>(Resource.Id.profileTabLabel);
            if (profileLabel != null)
                profileLabel.Text = "Profile";
        }

        private void UpdateBottomNavSelection()
        {
            var homeTab = FindViewById<LinearLayout>(Resource.Id.homeTab);
            var diaryTab = FindViewById<LinearLayout>(Resource.Id.diaryTab);
            var workoutTab = FindViewById<LinearLayout>(Resource.Id.workoutTab);
            var profileTab = FindViewById<LinearLayout>(Resource.Id.profileTab);

            if (homeTab != null) homeTab.Selected = false;
            if (diaryTab != null) diaryTab.Selected = false;
            if (workoutTab != null) workoutTab.Selected = true;
            if (profileTab != null) profileTab.Selected = false;

            SetTabLabelStyle(Resource.Id.homeTabLabel, false);
            SetTabLabelStyle(Resource.Id.diaryTabLabel, false);
            SetTabLabelStyle(Resource.Id.workoutTabLabel, true);
            SetTabLabelStyle(Resource.Id.profileTabLabel, false);
        }

        private void SetTabLabelStyle(int labelId, bool isSelected)
        {
            var label = FindViewById<TextView>(labelId);
            if (label == null)
                return;

            label.SetTypeface(null, isSelected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        }
    }
}
