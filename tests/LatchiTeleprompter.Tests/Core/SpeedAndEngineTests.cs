using LatchiTeleprompter.Core.Engine;
using Xunit;

public class SpeedCurveTests
{
    [Fact]
    public void PxPerSecond_IsMonotonicAndBounded()
    {
        double prev = 0;
        for (int v = SpeedCurve.Min; v <= SpeedCurve.Max; v++)
        {
            var px = SpeedCurve.PxPerSecond(v);
            Assert.True(px > prev, $"not monotonic at {v}");
            prev = px;
        }
        Assert.Equal(7.0, SpeedCurve.PxPerSecond(1));
        Assert.Equal(304.0, SpeedCurve.PxPerSecond(100));
    }

    [Fact]
    public void Clamp_OutOfRange()
    {
        Assert.Equal(1, SpeedCurve.Clamp(-50));
        Assert.Equal(100, SpeedCurve.Clamp(9999));
        Assert.Equal(45, SpeedCurve.Clamp(45));
    }

    [Fact]
    public void Presets_Exist()
    {
        Assert.Equal(45, SpeedCurve.Presets["normal"]);
        Assert.Equal(10, SpeedCurve.Presets["veryslow"]);
        Assert.Equal(85, SpeedCurve.Presets["veryfast"]);
    }
}

public class TeleprompterEngineTests
{
    [Fact]
    public void Configure_ClampsOffsetAndSpeed()
    {
        var e = new TeleprompterEngine();
        e.Configure(1000, 45);
        e.Seek(500);
        e.Configure(100, 500); // max shrinks → stored offset clamps into range
        Assert.Equal(100, e.MaxOffset);
        Assert.Equal(100, e.Offset);
        Assert.Equal(100, e.Speed);
    }

    [Fact]
    public void PlayPauseSeek_ProgressAndEnd()
    {
        var e = new TeleprompterEngine();
        e.Configure(1000, 50);
        e.Seek(770);
        Assert.Equal(0.77, e.Progress, 3);
        Assert.False(e.AtEnd);
        Assert.True(Math.Abs(e.RemainingSeconds - 230 / 154.0) < 0.01);

        e.Play();
        Assert.True(e.IsPlaying);
        e.Pause();
        Assert.False(e.IsPlaying);
        Assert.Equal(770, e.Offset);

        e.Seek(1000);
        Assert.True(e.AtEnd);
        e.Play(); // no-op at end
        Assert.False(e.IsPlaying);
    }

    [Fact]
    public void Restart_And_Jump()
    {
        var e = new TeleprompterEngine();
        e.Configure(1000, 45);
        e.Seek(900);
        e.Restart();
        Assert.Equal(0, e.Offset);
        Assert.Equal(500, e.PeekJump(500));
        Assert.Equal(0, e.PeekJump(-900));
    }

    [Fact]
    public void EmptyScript_IsEnd()
    {
        var e = new TeleprompterEngine();
        e.Configure(0, 45);
        Assert.True(e.IsEmpty);
        Assert.True(e.AtEnd);
        Assert.Equal(0, e.Progress);
    }
}
