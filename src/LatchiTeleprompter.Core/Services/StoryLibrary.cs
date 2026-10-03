using System.Text.RegularExpressions;
using LatchiTeleprompter.Core.Models;

namespace LatchiTeleprompter.Core.Services;

/// <summary>
/// Local story library: one JSON file per story inside {dataDir}/scripts.
/// Stories are scanned on demand (cheap; a user has tens, not millions).
/// A single corrupted file is skipped — the rest of the library still opens.
/// </summary>
public sealed class StoryLibrary
{
    private static readonly Regex IdPattern = new("^[A-Za-z0-9-]{4,64}$", RegexOptions.Compiled);
    private readonly string _scriptsDir;

    public StoryLibrary(string dataDir)
    {
        _scriptsDir = Path.Combine(dataDir, "scripts");
        Directory.CreateDirectory(_scriptsDir);
    }

    public string NewId() => $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}";

    public IReadOnlyList<StoryMeta> ListStories()
    {
        var metas = new List<StoryMeta>();
        foreach (var file in Directory.EnumerateFiles(_scriptsDir, "*.json"))
        {
            if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
            var story = Json.Deserialize<Story>(AtomicFile.TryReadAllText(file));
            if (story is null || string.IsNullOrEmpty(story.Id) || !IdPattern.IsMatch(story.Id)) continue;
            metas.Add(new StoryMeta
            {
                Id = story.Id,
                Title = string.IsNullOrWhiteSpace(story.Title) ? Untitled : story.Title,
                CreatedAtUtc = story.CreatedAtUtc,
                UpdatedAtUtc = story.UpdatedAtUtc,
                ReadingProgress = Math.Clamp(story.ReadingProgress, 0, 1),
                WordCount = CountWords(story.Content),
            });
        }
        return metas.OrderByDescending(m => m.UpdatedAtUtc).ToList();
    }

    public Story? Load(string? id)
    {
        if (id is null || !IdPattern.IsMatch(id)) return null;
        return Json.Deserialize<Story>(AtomicFile.TryReadAllText(PathOf(id)));
    }

    public void Save(Story story)
    {
        if (string.IsNullOrEmpty(story.Id) || !IdPattern.IsMatch(story.Id)) story.Id = NewId();
        story.UpdatedAtUtc = DateTime.UtcNow;
        AtomicFile.WriteAllText(PathOf(story.Id), Json.Serialize(story));
    }

    public bool Delete(string? id)
    {
        if (id is null || !IdPattern.IsMatch(id)) return false;
        return AtomicFile.TryDelete(PathOf(id));
    }

    public Story? Duplicate(string? id)
    {
        var src = Load(id);
        if (src is null) return null;
        var copy = new Story
        {
            Id = NewId(),
            Title = TrimTitle(src.Title) + " (نسخة)",
            Content = src.Content,
            CreatedAtUtc = DateTime.UtcNow,
            ReadingProgress = 0,
        };
        Save(copy);
        return copy;
    }

    /// <summary>Renames a story. Returns false when the story or the new title is empty.</summary>
    public bool Rename(string? id, string newTitle)
    {
        var t = TrimTitle(newTitle);
        if (t.Length == 0) return false;
        var story = Load(id);
        if (story is null) return false;
        story.Title = t;
        Save(story);
        return true;
    }

    /// <summary>First meaningful line (emoji/decorations stripped) or "" when nothing usable.</summary>
    public static string SuggestTitle(string content)
    {
        if (string.IsNullOrEmpty(content)) return "";
        foreach (var rawLine in content.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            // drop surrogate pairs (emoji), control chars, variation selectors,
            // zero-width joiners and combining marks (emoji decorations)
            var sb = new System.Text.StringBuilder();
            foreach (var ch in line)
                if (!char.IsSurrogate(ch) && !char.IsControl(ch)
                    && ch != '\uFE0F' && ch != '\uFE0E' && ch != '\u200D' && ch != '\u20E3'
                    && !System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                        .Equals(System.Globalization.UnicodeCategory.NonSpacingMark))
                    sb.Append(ch);
            var cleaned = sb.ToString().Trim();
            // strip leading/trailing punctuation/symbols (decorations like 🔥 or —— )
            cleaned = Regex.Replace(cleaned, @"^[\p{P}\p{S}\s]+|[\p{P}\p{S}\s]+$", "").Trim();
            if (cleaned.Length == 0) continue;
            return cleaned.Length <= 60 ? cleaned : cleaned[..60].TrimEnd() + "…";
        }
        return "";
    }

    public static int CountWords(string text) =>
        string.IsNullOrWhiteSpace(text) ? 0
        : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string Untitled => "قصة بدون عنوان";
    private static string TrimTitle(string? t) => (t ?? "").Trim();
    private string PathOf(string id) => Path.Combine(_scriptsDir, id + ".json");
}
