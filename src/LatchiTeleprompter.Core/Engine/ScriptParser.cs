using System.Text.RegularExpressions;

namespace LatchiTeleprompter.Core.Engine;

public sealed record ScriptMarker(int LineIndex, int CharIndex, string Raw, double PauseSeconds);

/// <summary>
/// Finds inline pause markers such as "[توقف 1ث]" / "[توقف 1.5ث]" / "[pause 2s]".
/// V1 keeps markers visible as plain text (owner decision); the parser exists and
/// is tested so a future version can turn them into automatic pauses.
/// </summary>
public static class ScriptParser
{
    private static readonly Regex MarkerRegex = new(
        @"\[\s*(?:توقف|pause)\s+(\d+(?:[.,]\d+)?)\s*(?:ث|ثانية|s|sec|secs|seconds?)?\s*\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<ScriptMarker> FindMarkers(string content)
    {
        var list = new List<ScriptMarker>();
        if (string.IsNullOrEmpty(content)) return list;
        foreach (Match m in MarkerRegex.Matches(content))
        {
            var seconds = double.TryParse(
                m.Groups[1].Value.Replace(',', '.'),
                System.Globalization.CultureInfo.InvariantCulture,
                out var s) ? s : 0;
            var line = 1;
            for (int i = 0; i < m.Index && i < content.Length; i++)
                if (content[i] == '\n') line++;
            list.Add(new ScriptMarker(line, m.Index, m.Value, seconds));
        }
        return list;
    }

    public static bool HasMarkers(string content) =>
        !string.IsNullOrEmpty(content) && MarkerRegex.IsMatch(content);
}
