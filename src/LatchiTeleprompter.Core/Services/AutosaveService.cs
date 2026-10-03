namespace LatchiTeleprompter.Core.Services;

public sealed record AutosaveState(string? StoryId, string Title, string Content, double ReadingProgress, DateTime SavedAtUtc);

/// <summary>
/// The user must NEVER lose his script: the editor content is auto-saved to
/// autosave.json on a debounce (default 900 ms of typing silence — not every
/// keystroke, to be kind to the HDD) and flushed on app close.
/// Restored automatically on the next launch.
/// </summary>
public sealed class AutosaveService : IDisposable
{
    private readonly string _file;
    private readonly TimeSpan _debounce;
    private readonly object _gate = new();
    private System.Threading.Timer? _timer;
    private AutosaveState? _pending;
    private bool _disposed;

    /// <summary>Raised (on a timer thread!) after a real write — UI must marshal.</summary>
    public event Action? Saved;

    public AutosaveService(string dataDir, TimeSpan? debounce = null)
    {
        _file = Path.Combine(dataDir, "autosave.json");
        _debounce = debounce ?? TimeSpan.FromMilliseconds(900);
    }

    /// <summary>(Re)schedules a save; the write happens once typing stays quiet.</summary>
    public void Schedule(string? storyId, string title, string content, double readingProgress)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _pending = new AutosaveState(storyId, title, content, Math.Clamp(readingProgress, 0, 1), DateTime.UtcNow);
            if (_timer is null)
                _timer = new System.Threading.Timer(_ => Flush(), null, _debounce, Timeout.InfiniteTimeSpan);
            else
                _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Writes the pending state now (if any). Thread-safe.</summary>
    public void Flush()
    {
        AutosaveState? toWrite = null;
        lock (_gate)
        {
            if (_pending is not null) { toWrite = _pending; _pending = null; }
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        if (toWrite is null) return;
        AtomicFile.WriteAllText(_file, Json.Serialize(toWrite));
        Saved?.Invoke();
    }

    /// <summary>Forces a write even without pending changes (used before opening stories).</summary>
    public void ForceWrite(string? storyId, string title, string content, double readingProgress)
    {
        lock (_gate) _pending = new AutosaveState(storyId, title, content, Math.Clamp(readingProgress, 0, 1), DateTime.UtcNow);
        Flush();
    }

    public AutosaveState? Load() => Json.Deserialize<AutosaveState>(AtomicFile.TryReadAllText(_file));

    /// <summary>Removes the autosave (after the user explicitly deletes everything).</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _pending = null;
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        AtomicFile.TryDelete(_file);
    }

    public void Dispose()
    {
        lock (_gate) _disposed = true;
        Flush();
        _timer?.Dispose();
        _timer = null;
    }
}
