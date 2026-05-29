#pragma warning disable CS0618 // Legacy Camera keeps loop detection dependency-free for this Android app.

using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using AndroidGraphicsFormat = Android.Graphics.Format;
using AndroidImageFormatType = Android.Graphics.ImageFormatType;
using HardwareCamera = Android.Hardware.Camera;
using Android.OS;
using Android.Views;
using Android.Widget;
using Gym_App.Data;
using Gym_App.Models;
using Gym_App.Services;

namespace Gym_App.Activities
{
    [Activity(Label = "Camera Loop Counter")]
    public class CameraLoopActivity : Activity, ISurfaceHolderCallback, HardwareCamera.IPreviewCallback
    {
        public const string ExtraWorkoutId = "workoutId";
        public const string ExtraExerciseName = "exerciseName";

        private const int RequestCameraPermission = 2201;

        private readonly CameraLoopDetector _loopDetector = new();
        private readonly CameraFrameMotionAnalyzer _motionAnalyzer = new();
        private readonly object _frameLock = new();

        private MediaPipePoseMotionAnalyzer? _poseMotionAnalyzer;

        private GymDatabase? _database;
        private SurfaceView? _cameraPreview;
        private ISurfaceHolder? _surfaceHolder;
        private HardwareCamera? _camera;
        private TextView? _loopCountText;
        private TextView? _motionStatusText;
        private TextView? _exerciseNameText;
        private Button? _startStopButton;
        private Button? _saveButton;

        private bool _surfaceReady;
        private bool _isCounting;
        private int _workoutId;
        private string _exerciseName = "Camera-detected exercise";
        private string _motionSourceText = "camera frame";
        private DateTimeOffset _lastUiUpdate = DateTimeOffset.MinValue;
        private long _lastMediaPipeFrameMs;

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
            _poseMotionAnalyzer = MediaPipePoseMotionAnalyzer.TryCreate(this);
            _motionSourceText = _poseMotionAnalyzer == null ? "camera frame" : "MediaPipe pose";

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
            _poseMotionAnalyzer?.Dispose();
            _poseMotionAnalyzer = null;
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
            _surfaceReady = false;
            StopCounting();
            StopCameraPreview();
        }

        public void OnPreviewFrame(byte[]? data, HardwareCamera? camera)
        {
            if (!_isCounting || data == null || camera == null)
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
                var motionScore = TryAnalyzeMotionScore(data, previewSize.Width, previewSize.Height);
                result = motionScore.HasValue
                    ? _loopDetector.AddSample(motionScore.Value, capturedAt)
                    : null;
            }

            if (result == null)
                return;

            if (result.LoopCompleted || DateTimeOffset.UtcNow - _lastUiUpdate > TimeSpan.FromMilliseconds(250))
            {
                _lastUiUpdate = DateTimeOffset.UtcNow;
                RunOnUiThread(() => UpdateCounterViews(result));
            }
        }

        private double? TryAnalyzeMotionScore(byte[] data, int width, int height)
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
                    ? $"MediaPipe pose ({sample.LandmarkCount} points)"
                    : "MediaPipe pose (no body)";
                return sample.HasPose ? sample.MotionScore : 0d;
            }

            _motionSourceText = "camera frame";
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

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode != RequestCameraPermission)
                return;

            if (HasCameraPermission())
            {
                UpdateStatus(BuildReadyStatus());
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
                UpdateStatus(BuildReadyStatus());
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
                UpdateStatus("Unable to start camera: " + ex.Message);
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
                _poseMotionAnalyzer?.Reset();
                _loopDetector.Reset();
                _lastMediaPipeFrameMs = 0;
            }

            _isCounting = true;
            if (_startStopButton != null)
                _startStopButton.Text = "Stop detection";

            UpdateStatus($"Counting loops with {_motionSourceText}. Keep your full movement in frame.");
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
                    _ => $"Ready - {_motionSourceText} motion {result.SmoothedMotion:P0}"
                };
            }
        }

        private string BuildReadyStatus()
        {
            return _poseMotionAnalyzer == null
                ? "Camera ready. Tap Start to count loops."
                : "MediaPipe Pose ready. Tap Start to count loops.";
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
