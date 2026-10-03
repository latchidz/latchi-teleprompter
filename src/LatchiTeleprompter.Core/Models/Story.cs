namespace LatchiTeleprompter.Core.Models;

/// <summary>A saved script/story. Persisted as one JSON file per story.</summary>
public sealed class Story
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Last reading position as a 0..1 fraction of the whole script (0 = never read).</summary>
    public double ReadingProgress { get; set; }
}

/// <summary>Lightweight listing row for the library UI (content not loaded).</summary>
public sealed class StoryMeta
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public double ReadingProgress { get; set; }
    public int WordCount { get; set; }
}
