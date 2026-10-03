namespace LatchiTeleprompter.Core.Engine;

/// <summary>
/// Pure scrolling state machine (no WPF): offset 0..MaxOffset, speed, play/pause.
/// The WPF layer drives the actual on-screen animation from this state, which
/// keeps every rule testable on any platform.
/// </summary>
public sealed class TeleprompterEngine
{
    private double _offset;

    public double MaxOffset { get; private set; }
    public int Speed { get; private set; } = 45;
    public bool IsPlaying { get; private set; }

    /// <summary>Raised whenever offset/speed/play-state changes.</summary>
    public event Action? StateChanged;

    public double Offset => _offset;
    public double Progress => MaxOffset <= 0 ? 0 : Math.Clamp(_offset / MaxOffset, 0, 1);
    public bool AtEnd => MaxOffset <= 0 || _offset >= MaxOffset - 0.5;
    public bool IsEmpty => MaxOffset <= 0;
    public double RemainingSeconds => Math.Max(0, (MaxOffset - _offset) / SpeedCurve.PxPerSecond(Speed));

    public void Configure(double maxOffset, int speed)
    {
        MaxOffset = Math.Max(0, maxOffset);
        Speed = SpeedCurve.Clamp(speed);
        _offset = Math.Clamp(_offset, 0, MaxOffset);
        StateChanged?.Invoke();
    }

    public void SetSpeed(int speed)
    {
        Speed = SpeedCurve.Clamp(speed);
        StateChanged?.Invoke();
    }

    public double Seek(double offset)
    {
        _offset = Math.Clamp(offset, 0, MaxOffset);
        StateChanged?.Invoke();
        return _offset;
    }

    public void Play()
    {
        if (IsPlaying || AtEnd) return;
        IsPlaying = true;
        StateChanged?.Invoke();
    }

    public void Pause()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        StateChanged?.Invoke();
    }

    public void Toggle() { if (IsPlaying) Pause(); else Play(); }

    public void Restart()
    {
        IsPlaying = false;
        _offset = 0;
        StateChanged?.Invoke();
    }

    /// <summary>Where a relative jump (±px) would land, clamped to the script.</summary>
    public double PeekJump(double deltaPx) => Math.Clamp(_offset + deltaPx, 0, MaxOffset);
}
