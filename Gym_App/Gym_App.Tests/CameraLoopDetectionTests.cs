using Gym_App.Services;
using Xunit;

namespace Gym_App.Tests;

public class CameraLoopDetectionTests
{
    [Fact]
    public void AddSample_WhenMotionRisesAndSettles_CountsOneLoop()
    {
        var detector = new CameraLoopDetector(new CameraLoopDetectionOptions
        {
            StartThreshold = 0.2,
            EndThreshold = 0.08,
            SmoothingFactor = 1,
            RequiredActiveSamples = 2,
            RequiredQuietSamples = 2,
            MinLoopDuration = TimeSpan.FromMilliseconds(300),
            MaxLoopDuration = TimeSpan.FromSeconds(3)
        });
        var start = DateTimeOffset.UtcNow;

        detector.AddSample(0.01, start);
        detector.AddSample(0.35, start.AddMilliseconds(200));
        detector.AddSample(0.34, start.AddMilliseconds(400));
        detector.AddSample(0.32, start.AddMilliseconds(600));
        detector.AddSample(0.02, start.AddMilliseconds(800));
        var result = detector.AddSample(0.01, start.AddMilliseconds(1000));

        Assert.True(result.LoopCompleted);
        Assert.Equal(1, result.TotalLoops);
        Assert.Equal(1, detector.TotalLoops);
    }

    [Fact]
    public void AddSample_WhenMotionIsTooShort_IgnoresJitter()
    {
        var detector = new CameraLoopDetector(new CameraLoopDetectionOptions
        {
            StartThreshold = 0.2,
            EndThreshold = 0.08,
            SmoothingFactor = 1,
            RequiredActiveSamples = 1,
            RequiredQuietSamples = 1,
            MinLoopDuration = TimeSpan.FromMilliseconds(300),
            MaxLoopDuration = TimeSpan.FromSeconds(3)
        });
        var start = DateTimeOffset.UtcNow;

        detector.AddSample(0.3, start);
        var result = detector.AddSample(0.01, start.AddMilliseconds(100));

        Assert.False(result.LoopCompleted);
        Assert.Equal(0, detector.TotalLoops);
    }

    [Fact]
    public void AnalyzeNv21Frame_FirstFrameReturnsZeroThenDetectsLumaChange()
    {
        var analyzer = new CameraFrameMotionAnalyzer(sampleStep: 1);
        var firstFrame = new byte[16];
        var secondFrame = new byte[16];
        Array.Fill(secondFrame, (byte)255);

        var firstMotion = analyzer.AnalyzeNv21Frame(firstFrame, width: 4, height: 4);
        var secondMotion = analyzer.AnalyzeNv21Frame(secondFrame, width: 4, height: 4);

        Assert.Equal(0, firstMotion);
        Assert.Equal(1, secondMotion, precision: 6);
    }

    [Fact]
    public void AnalyzeNv21Frame_WhenFrameIsTooSmall_ReturnsZero()
    {
        var analyzer = new CameraFrameMotionAnalyzer(sampleStep: 1);

        var motion = analyzer.AnalyzeNv21Frame(new byte[3], width: 4, height: 4);

        Assert.Equal(0, motion);
    }
}
