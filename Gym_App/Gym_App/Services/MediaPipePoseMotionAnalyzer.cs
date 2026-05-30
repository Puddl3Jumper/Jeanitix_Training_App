#if ANDROID
using Android.Content;
using Android.Graphics;
using MediaPipe.Framework.Image;
using MediaPipe.Tasks.Components.Containers;
using MediaPipe.Tasks.Core;
using MediaPipe.Tasks.Vision.Core;
using MediaPipe.Tasks.Vision.PoseLandmarker;

namespace Gym_App.Services
{
    public sealed class MediaPipePoseMotionSample
    {
        public bool HasPose { get; init; }
        public double MotionScore { get; init; }
        public int LandmarkCount { get; init; }
    }

    /// <summary>
    /// MediaPipe Pose Landmarker adapter for turning body landmarks into a loop-detection motion signal.
    /// </summary>
    public sealed class MediaPipePoseMotionAnalyzer : IDisposable
    {
        public const string ModelAssetName = "pose_landmarker_lite.task";

        private readonly PoseLandmarker _poseLandmarker;
        private readonly ImageProcessingOptions _imageProcessingOptions;
        private float[]? _previousPoseVector;
        private bool _disposed;

        private MediaPipePoseMotionAnalyzer(PoseLandmarker poseLandmarker, ImageProcessingOptions imageProcessingOptions)
        {
            _poseLandmarker = poseLandmarker;
            _imageProcessingOptions = imageProcessingOptions;
        }

        public static MediaPipePoseMotionAnalyzer? TryCreate(Context context, int rotationDegrees = 90, Action<string>? onError = null)
        {
            try
            {
                var baseOptions = BaseOptions.InvokeBuilder()
                    .SetModelAssetPath(ModelAssetName)
                    .Build();

                var options = PoseLandmarker.PoseLandmarkerOptions.InvokeBuilder()
                    .SetBaseOptions(baseOptions)
                    .SetRunningMode(RunningMode.Video)
                    .SetNumPoses(Java.Lang.Integer.ValueOf(1))
                    .SetMinPoseDetectionConfidence(Java.Lang.Float.ValueOf(0.5f))
                    .SetMinPosePresenceConfidence(Java.Lang.Float.ValueOf(0.5f))
                    .SetMinTrackingConfidence(Java.Lang.Float.ValueOf(0.5f))
                    .Build();

                var imageProcessingOptions = ImageProcessingOptions.InvokeBuilder()
                    .SetRotationDegrees(rotationDegrees)
                    .Build();

                return new MediaPipePoseMotionAnalyzer(
                    PoseLandmarker.CreateFromOptions(context, options),
                    imageProcessingOptions);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
                return null;
            }
        }

        public MediaPipePoseMotionSample Analyze(Bitmap bitmap, long timestampMs, string? targetExercise = null, string? targetMuscle = null)
        {
            ThrowIfDisposed();

            using var image = new BitmapImageBuilder(bitmap).Build();
            var result = _poseLandmarker.DetectForVideo(image, _imageProcessingOptions, timestampMs);
            var pose = result.Landmarks().FirstOrDefault();
            if (pose == null || pose.Count == 0)
            {
                _previousPoseVector = null;
                return new MediaPipePoseMotionSample();
            }

            var currentVector = BuildPoseVector(pose, targetExercise, targetMuscle);
            if (_previousPoseVector == null || _previousPoseVector.Length != currentVector.Length)
            {
                _previousPoseVector = currentVector;
                return new MediaPipePoseMotionSample
                {
                    HasPose = true,
                    MotionScore = 0,
                    LandmarkCount = pose.Count
                };
            }

            var distanceTotal = 0d;
            var pointCount = currentVector.Length / 2;
            for (var i = 0; i < currentVector.Length; i += 2)
            {
                var dx = currentVector[i] - _previousPoseVector[i];
                var dy = currentVector[i + 1] - _previousPoseVector[i + 1];
                distanceTotal += Math.Sqrt((dx * dx) + (dy * dy));
            }

            _previousPoseVector = currentVector;

            return new MediaPipePoseMotionSample
            {
                HasPose = true,
                MotionScore = Math.Clamp((distanceTotal / Math.Max(1, pointCount)) * 8d, 0d, 1d),
                LandmarkCount = pose.Count
            };
        }

        public void Reset()
        {
            _previousPoseVector = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _poseLandmarker.Dispose();
            _disposed = true;
        }

        private static float[] BuildPoseVector(IList<NormalizedLandmark> pose, string? targetExercise, string? targetMuscle)
        {
            var landmarkIndexes = ResolveTrackedLandmarks(targetExercise, targetMuscle)
                .Where(index => index >= 0 && index < pose.Count)
                .ToArray();
            if (landmarkIndexes.Length == 0)
            {
                landmarkIndexes = Enumerable.Range(0, pose.Count).ToArray();
            }

            var vector = new float[landmarkIndexes.Length * 2];
            var vectorIndex = 0;
            foreach (var landmarkIndex in landmarkIndexes)
            {
                var landmark = pose[landmarkIndex];
                vector[vectorIndex++] = landmark.X();
                vector[vectorIndex++] = landmark.Y();
            }

            return vector;
        }

        private static int[] ResolveTrackedLandmarks(string? targetExercise, string? targetMuscle)
        {
            var key = $"{targetExercise} {targetMuscle}".ToLowerInvariant();

            if (key.Contains("squat") || key.Contains("leg") || key.Contains("lunge"))
                return new[] { 23, 24, 25, 26, 27, 28 };

            if (key.Contains("pulldown") || key.Contains("pull") || key.Contains("row") || key.Contains("back"))
                return new[] { 11, 12, 13, 14, 15, 16 };

            if (key.Contains("curl") || key.Contains("bicep") || key.Contains("tricep") || key.Contains("pushdown"))
                return new[] { 13, 14, 15, 16 };

            if (key.Contains("press") || key.Contains("chest") || key.Contains("shoulder") || key.Contains("delt"))
                return new[] { 11, 12, 13, 14, 15, 16 };

            if (key.Contains("plank") || key.Contains("core") || key.Contains("abs"))
                return new[] { 11, 12, 23, 24 };

            return Array.Empty<int>();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MediaPipePoseMotionAnalyzer));
        }
    }
}
#endif
