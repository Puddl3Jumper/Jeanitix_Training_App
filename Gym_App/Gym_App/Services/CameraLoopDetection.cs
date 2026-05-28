namespace Gym_App.Services
{
    public enum CameraLoopPhase
    {
        WaitingForMotion,
        MotionActive,
        Cooldown
    }

    public sealed class CameraLoopDetectionOptions
    {
        public double StartThreshold { get; init; } = 0.085d;
        public double EndThreshold { get; init; } = 0.035d;
        public double SmoothingFactor { get; init; } = 0.35d;
        public int RequiredActiveSamples { get; init; } = 2;
        public int RequiredQuietSamples { get; init; } = 2;
        public TimeSpan MinLoopDuration { get; init; } = TimeSpan.FromMilliseconds(350);
        public TimeSpan MaxLoopDuration { get; init; } = TimeSpan.FromSeconds(5);
        public TimeSpan Cooldown { get; init; } = TimeSpan.FromMilliseconds(250);
    }

    public sealed class CameraLoopDetectionResult
    {
        public int TotalLoops { get; init; }
        public bool LoopCompleted { get; init; }
        public double SmoothedMotion { get; init; }
        public CameraLoopPhase Phase { get; init; }
    }

    /// <summary>
    /// Counts repeated exercise loops from a scalar camera-motion signal.
    /// The Android camera layer supplies frame-difference scores; this class stays unit-testable.
    /// </summary>
    public sealed class CameraLoopDetector
    {
        private readonly CameraLoopDetectionOptions _options;
        private double? _smoothedMotion;
        private DateTimeOffset? _activeStartedAt;
        private DateTimeOffset? _lastLoopAt;
        private int _activeSamples;
        private int _quietSamples;

        public CameraLoopDetector(CameraLoopDetectionOptions? options = null)
        {
            _options = options ?? new CameraLoopDetectionOptions();
            ValidateOptions(_options);
        }

        public int TotalLoops { get; private set; }
        public CameraLoopPhase Phase { get; private set; } = CameraLoopPhase.WaitingForMotion;

        public void Reset()
        {
            _smoothedMotion = null;
            _activeStartedAt = null;
            _lastLoopAt = null;
            _activeSamples = 0;
            _quietSamples = 0;
            TotalLoops = 0;
            Phase = CameraLoopPhase.WaitingForMotion;
        }

        public CameraLoopDetectionResult AddSample(double motionScore, DateTimeOffset capturedAt)
        {
            var normalizedScore = Math.Clamp(motionScore, 0d, 1d);
            _smoothedMotion = _smoothedMotion.HasValue
                ? (_smoothedMotion.Value * (1d - _options.SmoothingFactor)) + (normalizedScore * _options.SmoothingFactor)
                : normalizedScore;

            var completed = false;
            var smoothed = _smoothedMotion.Value;

            switch (Phase)
            {
                case CameraLoopPhase.WaitingForMotion:
                case CameraLoopPhase.Cooldown:
                    if (CanStartMotion(smoothed, capturedAt))
                    {
                        StartMotion(capturedAt);
                    }
                    else if (Phase == CameraLoopPhase.Cooldown && CanLeaveCooldown(capturedAt))
                    {
                        Phase = CameraLoopPhase.WaitingForMotion;
                    }
                    break;

                case CameraLoopPhase.MotionActive:
                    completed = UpdateActiveMotion(smoothed, capturedAt);
                    break;
            }

            return new CameraLoopDetectionResult
            {
                TotalLoops = TotalLoops,
                LoopCompleted = completed,
                SmoothedMotion = smoothed,
                Phase = Phase
            };
        }

        private bool CanStartMotion(double smoothedMotion, DateTimeOffset capturedAt)
        {
            if (smoothedMotion < _options.StartThreshold)
                return false;

            return !_lastLoopAt.HasValue || capturedAt - _lastLoopAt.Value >= _options.Cooldown;
        }

        private bool CanLeaveCooldown(DateTimeOffset capturedAt)
        {
            return !_lastLoopAt.HasValue || capturedAt - _lastLoopAt.Value >= _options.Cooldown;
        }

        private void StartMotion(DateTimeOffset capturedAt)
        {
            Phase = CameraLoopPhase.MotionActive;
            _activeStartedAt = capturedAt;
            _activeSamples = 1;
            _quietSamples = 0;
        }

        private bool UpdateActiveMotion(double smoothedMotion, DateTimeOffset capturedAt)
        {
            if (!_activeStartedAt.HasValue)
            {
                StartMotion(capturedAt);
                return false;
            }

            if (smoothedMotion >= _options.StartThreshold)
            {
                _activeSamples++;
                _quietSamples = 0;
            }
            else if (smoothedMotion <= _options.EndThreshold)
            {
                _quietSamples++;
            }
            else
            {
                _quietSamples = 0;
            }

            var duration = capturedAt - _activeStartedAt.Value;
            if (duration > _options.MaxLoopDuration)
            {
                ResetActiveMotion(CameraLoopPhase.WaitingForMotion);
                return false;
            }

            if (_activeSamples < _options.RequiredActiveSamples || _quietSamples < _options.RequiredQuietSamples)
                return false;

            ResetActiveMotion(CameraLoopPhase.Cooldown);
            if (duration < _options.MinLoopDuration)
                return false;

            TotalLoops++;
            _lastLoopAt = capturedAt;
            return true;
        }

        private void ResetActiveMotion(CameraLoopPhase nextPhase)
        {
            _activeStartedAt = null;
            _activeSamples = 0;
            _quietSamples = 0;
            Phase = nextPhase;
        }

        private static void ValidateOptions(CameraLoopDetectionOptions options)
        {
            if (options.StartThreshold <= 0 || options.StartThreshold > 1)
                throw new ArgumentOutOfRangeException(nameof(options), "Start threshold must be between 0 and 1.");

            if (options.EndThreshold < 0 || options.EndThreshold >= options.StartThreshold)
                throw new ArgumentOutOfRangeException(nameof(options), "End threshold must be lower than start threshold.");

            if (options.SmoothingFactor <= 0 || options.SmoothingFactor > 1)
                throw new ArgumentOutOfRangeException(nameof(options), "Smoothing factor must be between 0 and 1.");

            if (options.RequiredActiveSamples < 1 || options.RequiredQuietSamples < 1)
                throw new ArgumentOutOfRangeException(nameof(options), "Sample counts must be positive.");

            if (options.MinLoopDuration <= TimeSpan.Zero || options.MaxLoopDuration <= options.MinLoopDuration)
                throw new ArgumentOutOfRangeException(nameof(options), "Loop duration bounds are invalid.");
        }
    }

    public sealed class CameraFrameMotionAnalyzer
    {
        private byte[]? _previousFingerprint;

        public int SampleStep { get; }

        public CameraFrameMotionAnalyzer(int sampleStep = 12)
        {
            if (sampleStep < 1)
                throw new ArgumentOutOfRangeException(nameof(sampleStep));

            SampleStep = sampleStep;
        }

        public void Reset()
        {
            _previousFingerprint = null;
        }

        public double AnalyzeNv21Frame(byte[] frame, int width, int height)
        {
            if (frame.Length == 0 || width <= 0 || height <= 0)
                return 0d;

            var lumaLength = width * height;
            if (frame.Length < lumaLength)
                return 0d;

            var fingerprint = BuildLumaFingerprint(frame, width, height, SampleStep);
            if (_previousFingerprint == null || _previousFingerprint.Length != fingerprint.Length)
            {
                _previousFingerprint = fingerprint;
                return 0d;
            }

            long diffTotal = 0;
            for (var i = 0; i < fingerprint.Length; i++)
            {
                diffTotal += Math.Abs(fingerprint[i] - _previousFingerprint[i]);
            }

            _previousFingerprint = fingerprint;
            return diffTotal / (fingerprint.Length * 255d);
        }

        private static byte[] BuildLumaFingerprint(byte[] frame, int width, int height, int sampleStep)
        {
            var columns = Math.Max(1, (int)Math.Ceiling(width / (double)sampleStep));
            var rows = Math.Max(1, (int)Math.Ceiling(height / (double)sampleStep));
            var fingerprint = new byte[columns * rows];
            var index = 0;

            for (var y = 0; y < height; y += sampleStep)
            {
                var rowOffset = y * width;
                for (var x = 0; x < width; x += sampleStep)
                {
                    fingerprint[index++] = frame[rowOffset + x];
                }
            }

            return fingerprint;
        }
    }
}
