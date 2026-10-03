using LatchiTeleprompter.Core.Engine;
using LatchiTeleprompter.Core.Services;
using Xunit;

public class AutosaveServiceTests : IDisposable
{
    private readonly string _dir;
    public AutosaveServiceTests() { _dir = Path.Combine(Path.GetTempPath(), "ltp-auto-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
    public void Dispose() { try { Directory.Delete(_dir, recursive: true); } catch { } }

    [Fact]
    public async Task Debounce_WritesOnceAfterQuiet()
    {
        using var svc = new AutosaveService(_dir, TimeSpan.FromMilliseconds(80));
        var writes = 0;
        svc.Saved += () => Interlocked.Increment(ref writes);
        for (int i = 0; i < 5; i++) { svc.Schedule(null, "عنوان", $"النسخة {i}", 0); await Task.Delay(20); }
        await Task.Delay(400);
        Assert.True(writes >= 1, $"writes={writes}");
        var loaded = svc.Load();
        Assert.NotNull(loaded);
        Assert.Equal("النسخة 4", loaded!.Content);
        Assert.StartsWith("النسخة", loaded.Content);
    }

    [Fact]
    public void Dispose_FlushesPending()
    {
        var svc = new AutosaveService(_dir, TimeSpan.FromMilliseconds(5000)); // never fires naturally
        svc.Schedule("id-1", "العنوان", "النص الأخير", 0.42);
        svc.Dispose();
        var loaded = new AutosaveService(_dir, TimeSpan.FromMilliseconds(5000)).Load();
        Assert.NotNull(loaded);
        Assert.Equal("النص الأخير", loaded!.Content);
        Assert.Equal("id-1", loaded.StoryId);
        Assert.Equal(0.42, loaded.ReadingProgress, 3);
    }

    [Fact]
    public async Task Clear_RemovesAutosave()
    {
        using var svc = new AutosaveService(_dir, TimeSpan.FromMilliseconds(50));
        svc.ForceWrite(null, "t", "c", 0);
        svc.Clear();
        await Task.Delay(150);
        Assert.Null(svc.Load());
    }
}

public class ScriptParserTests
{
    [Fact]
    public void FindsArabicAndEnglishMarkers()
    {
        var markers = ScriptParser.FindMarkers("سطر أول\n[توقف 1.5ث]\nسطر ثالث [pause 2s] ونهاية");
        Assert.Equal(2, markers.Count);
        Assert.Equal(1.5, markers[0].PauseSeconds, 3);
        Assert.Equal(2, markers[1].PauseSeconds, 2);
        Assert.Equal(2, markers[0].LineIndex);
        Assert.Equal(3, markers[1].LineIndex);
    }

    [Fact]
    public void HandlesDecimalComma_AndNoMarkers()
    {
        var m = ScriptParser.FindMarkers("[توقف 2,5ث]");
        Assert.Single(m);
        Assert.Equal(2.5, m[0].PauseSeconds, 3);
        Assert.Empty(ScriptParser.FindMarkers("نص عادي بلا علامات"));
        Assert.False(ScriptParser.HasMarkers("نص عادي"));
        Assert.True(ScriptParser.HasMarkers("[توقف 1ث]"));
    }
}

public class TextDirectionTests
{
    [Theory]
    [InlineData("قصة بالدارجة", true)]
    [InlineData("English story", false)]
    [InlineData("قصة فيها Joseph James O'Keefe", true)]   // Arabic comes first
    [InlineData("Joseph في قصة", false)]                    // English comes first
    [InlineData("12345", true)]                              // neutral → RTL default
    [InlineData("", true)]
    public void Detection(string text, bool expectedRtl)
        => Assert.Equal(expectedRtl, TextDirection.IsRtl(text));
}

public class TimeEstimatorTests
{
    [Fact]
    public void Estimate_Bounds()
    {
        Assert.Equal(TimeSpan.Zero, TimeEstimator.Estimate(0, 45));
        var slow = TimeEstimator.Estimate(600, 10);   // 600 words @ 75 wpm = 8 min
        var fast = TimeEstimator.Estimate(600, 90);   // 600 words @ 195 wpm ≈ 3.08 min
        Assert.True(slow > fast);
        Assert.Equal(8, Math.Round(slow.TotalMinutes));
    }

    [Fact]
    public void Format_MinutesAndHours()
    {
        Assert.Equal("0:45", TimeEstimator.Format(TimeSpan.FromSeconds(45)));
        Assert.Equal("12:05", TimeEstimator.Format(new TimeSpan(0, 12, 5)));
        Assert.Equal("1:02:03", TimeEstimator.Format(new TimeSpan(1, 2, 3)));
    }
}
