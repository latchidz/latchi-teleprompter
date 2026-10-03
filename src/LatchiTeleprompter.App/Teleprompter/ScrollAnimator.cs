using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LatchiTeleprompter.App.Teleprompter;

/// <summary>
/// Drives the reading scroll: the script sits on a Canvas whose TranslateTransform
/// is animated from 0 to -MaxOffset. WPF transform animations run on the render
/// thread (independent of the UI thread), which is exactly what a weak machine
/// needs: no per-frame UI-thread timer, no busy loop, pixel-smooth motion — and
/// ZERO CPU while paused (no clock runs at all).
/// All API works in "offset" domain (0..max, positive), the transform gets -offset.
/// </summary>
public sealed class ScrollAnimator
{
    private readonly TranslateTransform _transform;
    private AnimationClock? _clock;

    public ScrollAnimator(TranslateTransform transform) => _transform = transform;

    /// <summary>Current scroll offset (reads the live animated value while playing).</summary>
    public double CurrentOffset
    {
        get
        {
            var y = _transform.GetValue(TranslateTransform.YProperty);
            return y is double v ? -v : 0;
        }
    }

    public bool IsAnimating => _clock is not null;

    /// <summary>Freezes the scroll at an exact offset (no animation).</summary>
    public void SetOffsetInstant(double offset)
    {
        Stop();
        _transform.Y = -Math.Max(0, offset);
    }

    /// <summary>
    /// Smoothly animates from→to over the given seconds. `completed` fires when the
    /// animation reaches `to`. A duration ≤ 0 jumps instantly (and still completes).
    /// </summary>
    public void Animate(double fromOffset, double toOffset, double seconds, Action? completed)
    {
        Stop();
        fromOffset = Math.Max(0, fromOffset);
        toOffset = Math.Max(0, toOffset);
        if (toOffset < fromOffset) toOffset = fromOffset; // scrolling is forward-only
        if (seconds <= 0 || toOffset - fromOffset < 0.5)
        {
            _transform.Y = -toOffset;
            completed?.Invoke();
            return;
        }

        _transform.Y = -fromOffset; // base value under the animation
        var anim = new DoubleAnimation(-fromOffset, -toOffset, new System.Windows.Duration(TimeSpan.FromSeconds(seconds)))
        {
            FillBehavior = FillBehavior.HoldEnd,
        };
        if (completed is not null)
            anim.Completed += (_, _) => completed();
        _clock = anim.CreateClock();
        _transform.ApplyAnimationClock(TranslateTransform.YProperty, _clock);
    }

    /// <summary>Removes the animation clock and keeps the transform's base value.</summary>
    public void Stop()
    {
        _transform.ApplyAnimationClock(TranslateTransform.YProperty, null);
        _clock = null;
    }
}
