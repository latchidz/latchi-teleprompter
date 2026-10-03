namespace LatchiTeleprompter.Core.Engine;

/// <summary>
/// Maps the 1..100 speed slider to physical scroll speed and reading heuristics.
/// Pure and monotonic — fully unit-tested.
/// </summary>
public static class SpeedCurve
{
    public const int Min = 1;
    public const int Max = 100;

    public static int Clamp(int speed) => Math.Clamp(speed, Min, Max);

    /// <summary>Scroll speed in DIP/second: 7 px/s at 1 … 304 px/s at 100.</summary>
    public static double PxPerSecond(int speed) => 4.0 + 3.0 * Clamp(speed);

    /// <summary>Editor estimate: words-per-minute for a given speed (heuristic).</summary>
    public static double WordsPerMinute(int speed) => 60.0 + 1.5 * Clamp(speed);

    public static readonly IReadOnlyDictionary<string, int> Presets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["veryslow"] = 10,
        ["slow"] = 25,
        ["normal"] = 45,
        ["fast"] = 65,
        ["veryfast"] = 85,
    };
}
