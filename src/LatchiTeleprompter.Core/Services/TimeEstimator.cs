using LatchiTeleprompter.Core.Engine;

namespace LatchiTeleprompter.Core.Services;

/// <summary>Lightweight reading-time estimate for the editor status bar (heuristic).</summary>
public static class TimeEstimator
{
    public static TimeSpan Estimate(int wordCount, int speed)
    {
        if (wordCount <= 0) return TimeSpan.Zero;
        return TimeSpan.FromMinutes(wordCount / SpeedCurve.WordsPerMinute(speed));
    }

    public static string Format(TimeSpan t)
    {
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}";
        return $"{t.Minutes}:{t.Seconds:D2}";
    }
}
