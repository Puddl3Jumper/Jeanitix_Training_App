using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware;
using Android.OS;
using Android.Views;
using Android.Widget;
using Gym_App.Data;
using Gym_App.Models;
using Gym_App.Services;

namespace Gym_App.Activities
{
    [Activity(Label = "Camera Loop Counter")]
    public class CameraLoopActivity : Activity, ISurfaceHolderCallback, Camera.IPreviewCallback
    {
        public const string ExtraWorkoutId = "workoutId";
        public const string ExtraExerciseName = "exerciseName";

        private const int RequestCameraPermission = 2201;

        private readonly CameraLoopDetector _loopDetector = new();
        private readonly CameraFrameMotionAnalyzer _motionAnalyzer = new();
        private readonly object _frameLock = new();

        private GymDatabase? _database;
        private SurfaceView? _cameraPreview;
        private ISurfaceHolder? _surfaceHolder;
        private Camera? _camera;
        private TextView? _loopCountText;
        private TextView? _motionStatusText;
        private TextView? _exerciseNameText;
        private Button? _startStopButton;
        private Button? _saveButton;

        private bool _surfaceReady;
        private bool _isCounting;
        private int _workoutId;
        private string _exerciseName = "Camera-detected exercise";
        private DateTimeOffset _lastUiUpdate = DateTimeOffset.MinValue;

        public static Intent CreateIntent(Context context, int workoutId, string exerciseName)
        {
            var intent = new Intent(context, typeof(CameraLoopActivity));
            intent.PutExtra(ExtraWorkoutId, workoutId);
            intent.PutExtra(ExtraExerciseName, exerciseName);
            return intent;
        }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_camera_loop);

            _database = new GymDatabase();
            _workoutId = Intent?.GetIntExtra(ExtraWorkoutId, -1) ?? -1;
            _exerciseName = Intent?.GetStringExtra(ExtraExerciseName) ?? _exerciseName;

            BindViews();
            EnsureCameraPermission();
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (_surfaceReady && HasCameraPermission())
            {
                StartCameraPreview();
            }
        }

        protected override void OnPause()
        {
            StopCounting();
            StopCameraPreview();
            base.OnPause();
        }

        protected override void OnDestroy()
        {
            StopCameraPreview();
            base.OnDestroy();
        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            _surfaceReady = true;
            _surfaceHolder = holder;
            if (HasCameraPermission())
            {
                StartCameraPreview();
            }
        }

        public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
        {
            _surfaceHolder = holder;
            if (HasCameraPermission())
            {
                StartCameraPreview();
            }
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            _surfaceReady = false;
            StopCounting();
            StopCameraPreview();
        }

        public void OnPreviewFrame(byte[]? data, Camera? camera)
        {
            if (!_isCounting || data == null || camera == null)
                return;

            Camera.Size? previewSize;
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

            CameraLoopDetectionResult result;
            lock (_frameLock)
            {
                var motionScore = _motionAnalyzer.AnalyzeNv21Frame(data, previewSize.Width, previewSize.Height);
                result = _loopDetector.AddSample(motionScore, DateTimeOffset.UtcNow);
            }

            if (result.LoopCompleted || DateTimeOffset.UtcNow - _lastUiUpdate > TimeSpan.FromMilliseconds(250))
            {
                _lastUiUpdate = DateTimeOffset.UtcNow;
                RunOnUiThread(() => UpdateCounterViews(result));
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode != RequestCameraPermission)
                return;

            if (HasCameraPermission())
            {
                UpdateStatus("Camera ready. Tap Start to count loops.");
                if (_surfaceReady)
                {
                    StartCameraPreview();
                }
            }
            else
            {
                UpdateStatus("Camera permission is required for automatic loop detection.");
            }
        }

        private void BindViews()
        {
            _cameraPreview = FindViewById<SurfaceView>(Resource.Id.cameraLoopPreview);
            _loopCountText = FindViewById<TextView>(Resource.Id.cameraLoopCountText);
            _motionStatusText = FindViewById<TextView>(Resource.Id.cameraLoopStatusText);
            _exerciseNameText = FindViewById<TextView>(Resource.Id.cameraLoopExerciseText);
            _startStopButton = FindViewById<Button>(Resource.Id.cameraLoopStartStopButton);
            _saveButton = FindViewById<Button>(Resource.Id.cameraLoopSaveButton);

            if (_exerciseNameText != null)
            {
                _exerciseNameText.Text = _exerciseName;
            }

            if (_cameraPreview?.Holder != null)
            {
                _surfaceHolder = _cameraPreview.Holder;
                _surfaceHolder.AddCallback(this);
            }

            if (_startStopButton != null)
            {
                _startStopButton.Click += (_, _) =>
                {
                    if (_isCounting)
                    {
                        StopCounting();
                    }
                    else
                    {
                        StartCounting();
                    }
                };
            }

            if (_saveButton != null)
            {
                _saveButton.Click += (_, _) => SaveDetectedLoops();
            }

            UpdateCounterViews(new CameraLoopDetectionResult
            {
                TotalLoops = 0,
                SmoothedMotion = 0,
                Phase = CameraLoopPhase.WaitingForMotion
            });
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
            {
                UpdateStatus("Camera ready. Tap Start to count loops.");
                return;
            }

            RequestPermissions(new[] { Android.Manifest.Permission.Camera }, RequestCameraPermission);
        }

        private void StartCameraPreview()
        {
            if (_camera != null || _surfaceHolder == null || !_surfaceReady)
                return;

            try
            {
                _camera = Camera.Open();
                var parameters = _camera.GetParameters();
                var previewSize = ChoosePreviewSize(parameters?.SupportedPreviewSizes);
                if (parameters != null && previewSize != null)
                {
                    parameters.SetPreviewSize(previewSize.Width, previewSize.Height);
                    parameters.PreviewFormat = ImageFormatType.Nv21;
                    _camera.SetParameters(parameters);
                }

                _camera.SetDisplayOrientation(90);
                _camera.SetPreviewDisplay(_surfaceHolder);
                _camera.SetPreviewCallback(this);
                _camera.StartPreview();
            }
            catch (Exception ex)
            {
                UpdateStatus("Unable to start camera: " + ex.Message);
                StopCameraPreview();
            }
        }

        private static Camera.Size? ChoosePreviewSize(IList<Camera.Size>? supportedSizes)
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

        private void StartCounting()
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

            lock (_frameLock)
            {
                _motionAnalyzer.Reset();
                _loopDetector.Reset();
            }

            _isCounting = true;
            if (_startStopButton != null)
                _startStopButton.Text = "Stop detection";

            UpdateStatus("Counting loops. Keep your full movement in frame.");
        }

        private void StopCounting()
        {
            if (!_isCounting)
                return;

            _isCounting = false;
            if (_startStopButton != null)
                _startStopButton.Text = "Start detection";

            UpdateStatus("Detection paused. Save when the count looks right.");
        }

        private void UpdateCounterViews(CameraLoopDetectionResult result)
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
                    _ => $"Ready - motion {result.SmoothedMotion:P0}"
                };
            }
        }

        private void UpdateStatus(string message)
        {
            if (_motionStatusText != null)
            {
                _motionStatusText.Text = message;
            }
        }

        private void SaveDetectedLoops()
        {
            var loops = _loopDetector.TotalLoops;
            if (loops <= 0)
            {
                Toast.MakeText(this, "No loops detected yet", ToastLength.Short)?.Show();
                return;
            }

            var session = ResolveWorkoutSession();
            var workoutExercise = ResolveWorkoutExercise(session);
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
            Finish();
        }

        private WorkoutSession ResolveWorkoutSession()
        {
            if (_database == null)
                throw new InvalidOperationException("Database is unavailable.");

            var session = _workoutId > 0 ? _database.GetWorkoutSession(_workoutId) : null;
            session ??= _database.GetCurrentWorkout();
            session ??= _database.CreateWorkoutSession("Camera Loop Workout");
            return session;
        }

        private WorkoutExercise? ResolveWorkoutExercise(WorkoutSession session)
        {
            if (_database == null)
                return null;

            var exercises = _database.GetAllExercises();
            var exercise = exercises.FirstOrDefault(e => string.Equals(e.Name, _exerciseName, StringComparison.OrdinalIgnoreCase))
                ?? exercises.FirstOrDefault(e => string.Equals(e.MuscleGroup, _exerciseName, StringComparison.OrdinalIgnoreCase));

            if (exercise == null)
            {
                exercise = new Exercise
                {
                    Name = _exerciseName,
                    MuscleGroup = "Camera",
                    Description = "Created by camera loop detection."
                };
                _database.AddExercise(exercise);
            }

            var refreshed = _database.GetWorkoutSession(session.Id) ?? session;
            var existing = refreshed.Exercises.LastOrDefault(e => e.ExerciseId == exercise.Id);
            if (existing != null)
                return existing;

            _database.AddExerciseToWorkout(refreshed.Id, exercise.Id, circuitName: "Camera");
            return _database.GetWorkoutSession(refreshed.Id)?
                .Exercises
                .LastOrDefault(e => e.ExerciseId == exercise.Id);
        }
    }
}
