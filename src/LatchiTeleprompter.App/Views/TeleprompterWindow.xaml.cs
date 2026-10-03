using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LatchiTeleprompter.App.Teleprompter;
using LatchiTeleprompter.App.Theme;
using LatchiTeleprompter.Core.Engine;
using LatchiTeleprompter.Core.Models;

namespace LatchiTeleprompter.App.Views;

/// <summary>
/// Teleprompter reading mode — the heart of the app.
/// Scrolling = a WPF render-thread animation on a TranslateTransform (see
/// ScrollAnimator): pixel-smooth on integrated graphics, zero CPU when paused.
/// Everything (speed, font, width, focus position, mirror) changes live while
/// reading, preserving the current reading position.
/// </summary>
public partial class TeleprompterWindow : Window
{
    private const double FontMin = 24, FontMax = 120;
    private static readonly TimeSpan ControlsHideDelay = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan HintDuration = TimeSpan.FromSeconds(4);

    private readonly TeleprompterEngine _engine = new();
    private readonly ScrollAnimator _animator;
    private readonly AppSettings _settings;
    private readonly Action<double> _persistProgress;
    private readonly Action<AppSettings> _settingsChanged;

    private DispatcherTimer? _hideTimer;      // hides controls + cursor while reading
    private DispatcherTimer? _progressTimer;  // 250ms UI refresh WHILE PLAYING ONLY
    private DispatcherTimer? _hintTimer;
    private DispatcherTimer? _countdownTimer;
    private bool _ended;
    private bool _layoutReady;
    private bool _startPending = true;        // countdown may delay the first play

    public TeleprompterWindow(string script, AppSettings settings,
        Action<double> persistProgress, Action<AppSettings> settingsChanged)
    {
        _settings = settings;
        _persistProgress = persistProgress;
        _settingsChanged = settingsChanged;
        InitializeComponent();
        _animator = new ScrollAnimator(ScrollTransform);

        ScriptText.Text = script;
        ApplyTypography();
        ApplyMirror();
        SldSpeed.Value = SpeedCurve.Clamp(_settings.Speed);
        SldWidth.Value = Math.Clamp(_settings.TextWidthPercent, 30, 100);
        SldFocus.Value = Math.Clamp(_settings.FocusPosition, 0.20, 0.70);

        Loaded += OnLoaded;
        SizeChanged += (_, _) => Relayout(preserveProgress: true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Relayout(preserveProgress: false);
        // start where the user left off (never re-read a whole story)
        if (LastProgress > 0.01 && LastProgress < 0.99)
            SeekToFraction(LastProgress);
        _layoutReady = true;
        TxtSpeed.Text = _settings.Speed.ToString();     // saved values may differ
        TxtFont.Text = ((int)_settings.FontSize).ToString(); // from the XAML defaults
        Focus();

        // start: with countdown (default) or immediately
        if (_settings.Countdown) BeginCountdown();
        else PlayInternal();

        _hintTimer = new DispatcherTimer { Interval = HintDuration };
        _hintTimer.Tick += (_, _) => { HintBar.Visibility = Visibility.Collapsed; _hintTimer?.Stop(); };
        _hintTimer.Start();
        ShowControls();
    }

    /// <summary>Reading progress handed over by the editor (0 = always from the top).</summary>
    public double LastProgress { get; set; }

    /* ── typography / layout ─────────────────────────────────────────── */

    private void ApplyTypography()
    {
        var rtl = TextDirection.IsRtl(ScriptText.Text);
        ScriptText.FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        ScriptText.TextAlignment = _settings.Alignment switch
        {
            "center" => TextAlignment.Center,
            "right" => rtl ? TextAlignment.Left : TextAlignment.Right,  // visual right
            "left" => rtl ? TextAlignment.Right : TextAlignment.Left,   // visual left
            _ => TextAlignment.Left, // auto = start edge (right for RTL, left for LTR)
        };
        ScriptText.FontSize = Math.Clamp(_settings.FontSize, FontMin, FontMax);
        ScriptText.LineHeight = Math.Ceiling(ScriptText.FontSize * 1.55);
    }

    private void ApplyMirror()
    {
        MirrorScale.ScaleX = _settings.Mirror ? -1 : 1;
        BtnMirror.Opacity = _settings.Mirror ? 1 : 0.55;
    }

    /// <summary>
    /// Recomputes spacers/content width/max offset. When preserveProgress is true
    /// the reading position is kept (font/width/window changes never jump back to top).
    /// </summary>
    private void Relayout(bool preserveProgress)
    {
        var fraction = preserveProgress ? CurrentFraction() : 0;
        var viewportW = Viewport.ActualWidth;
        var viewportH = Viewport.ActualHeight;
        if (viewportW <= 10 || viewportH <= 10) return;

        var focusY = Math.Clamp(_settings.FocusPosition, 0.20, 0.70) * viewportH;
        TopSpacer.Height = Math.Max(1, focusY);
        BottomSpacer.Height = Math.Max(1, viewportH - focusY + 40);
        ContentPanel.Width = Math.Max(240, viewportW * Math.Clamp(_settings.TextWidthPercent, 30, 100) / 100.0);
        ScriptText.FontSize = Math.Clamp(_settings.FontSize, FontMin, FontMax);
        ScriptText.LineHeight = Math.Ceiling(ScriptText.FontSize * 1.55);

        UpdateLayout(); // measure with the new typography
        var contentHeight = ContentPanel.ActualHeight;
        var maxOffset = Math.Max(0, contentHeight - viewportH);

        var wasPlaying = _engine.IsPlaying;
        var speed = _engine.Speed;
        _animator.Stop();
        _engine.Configure(maxOffset, speed);
        SeekToFraction(wasPlaying || preserveProgress ? fraction : 0);
        PositionGuide(viewportW, focusY);
        UpdateProgressUi();
        if (wasPlaying && !_engine.IsEmpty && !_engine.AtEnd) PlayInternal();
    }

    private void PositionGuide(double viewportW, double focusY)
    {
        var band = Math.Max(48, ScriptText.FontSize * 2.4);
        var top = Math.Max(0, focusY - band / 2);
        GuideCanvas.Visibility = _settings.FocusGuide ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (el, h) in new[] { (GuideFill, band), (GuideTop, 1.0), (GuideBottom, 1.0) })
        {
            el.Width = viewportW;
            el.Height = h;
        }
        Canvas.SetTop(GuideFill, top);
        Canvas.SetTop(GuideTop, top);
        Canvas.SetTop(GuideBottom, top + band);
        BtnGuide.Opacity = _settings.FocusGuide ? 1 : 0.55;
    }

    /* ── playback ────────────────────────────────────────────────────── */

    private double CurrentOffset => _engine.IsPlaying ? _animator.CurrentOffset : _engine.Offset;

    private double CurrentFraction()
    {
        if (_engine.IsEmpty) return 0;
        var f = CurrentOffset / _engine.MaxOffset;
        return double.IsNaN(f) ? 0 : Math.Clamp(f, 0, 1);
    }

    private void SeekToFraction(double fraction)
        => _animator.SetOffsetInstant(_engine.Seek(fraction * _engine.MaxOffset));

    private void PlayInternal()
    {
        if (_engine.IsEmpty) return;
        if (_engine.AtEnd) { OnReachedEnd(); return; }
        _engine.Play();
        _animator.Animate(_engine.Offset, _engine.MaxOffset, _engine.RemainingSeconds, OnReachedEnd);
        UpdatePlayPauseIcon();
        StartProgressTimer();
    }

    private void PauseInternal()
    {
        if (!_engine.IsPlaying) return;
        var cur = _animator.CurrentOffset;      // freeze EXACTLY at what's on screen
        _engine.Pause();
        _engine.Seek(cur);
        _animator.SetOffsetInstant(cur);
        UpdatePlayPauseIcon();
        StopProgressTimer();
    }

    private void OnReachedEnd()
    {
        _engine.Pause();
        _engine.Seek(_engine.MaxOffset);
        _animator.SetOffsetInstant(_engine.MaxOffset);
        _ended = true;
        StopProgressTimer();
        EndBanner.Visibility = Visibility.Visible;
        ShowControls();
        UpdateProgressUi();
        _persistProgress(1.0);
    }

    private void RestartFromBeginning()
    {
        _ended = false;
        EndBanner.Visibility = Visibility.Collapsed;
        _engine.Restart();
        _animator.SetOffsetInstant(0);
        PlayInternal();
    }

    private void TogglePlayPause()
    {
        if (_ended) { RestartFromBeginning(); return; }
        if (_engine.IsPlaying) PauseInternal(); else PlayInternal();
    }

    private void Jump(double deltaPx)
    {
        var wasPlaying = _engine.IsPlaying;
        var target = _engine.PeekJump((wasPlaying ? _animator.CurrentOffset : _engine.Offset) + deltaPx);
        _animator.SetOffsetInstant(_engine.Seek(target));
        if (wasPlaying)
        {
            if (_engine.AtEnd) OnReachedEnd();
            else PlayInternal();
        }
        else UpdateProgressUi();
    }

    private void SeekEndInternal()
    {
        _animator.SetOffsetInstant(_engine.Seek(_engine.MaxOffset));
        OnReachedEnd();
    }

    /* ── countdown ───────────────────────────────────────────────────── */

    private void BeginCountdown()
    {
        var n = 3;
        CountdownText.Text = n.ToString();
        CountdownOverlay.Visibility = Visibility.Visible;
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) =>
        {
            n -= 1;
            if (n <= 0) { EndCountdown(); return; }
            CountdownText.Text = n.ToString();
        };
        _countdownTimer.Start();
    }

    private void EndCountdown()
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
        CountdownOverlay.Visibility = Visibility.Collapsed;
        if (_startPending) { _startPending = false; PlayInternal(); }
    }

    /* ── timers / controls auto-hide ─────────────────────────────────── */

    private void StartProgressTimer()
    {
        _progressTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _progressTimer.Tick += OnProgressTick;
        if (!_progressTimer.IsEnabled) _progressTimer.Start();
    }

    private void StopProgressTimer() => _progressTimer?.Stop();

    private void OnProgressTick(object? sender, EventArgs e)
    {
        if (!_engine.IsPlaying) return;
        if (_animator.IsAnimating)
        {
            var cur = _animator.CurrentOffset;
            _engine.Seek(cur); // keep the logical state in sync with the screen
        }
        UpdateProgressUi();
    }

    private void UpdateProgressUi()
    {
        var pct = (int)Math.Round(_engine.Progress * 100);
        TxtPercent.Text = pct + "%";
        var barWidth = Root.ActualWidth * _engine.Progress;
        ProgressFill.Width = barWidth;
        ProgressWrap.Visibility = _settings.ShowProgress ? Visibility.Visible : Visibility.Hidden;
        var rem = TimeEstimatorFormat(_engine.RemainingSeconds);
        TxtRemaining.Text = "~" + rem;
    }

    private static string TimeEstimatorFormat(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes}:{t.Seconds:D2}";
    }

    private void UpdatePlayPauseIcon()
    {
        var playing = _engine.IsPlaying;
        IcoPlay.Data = playing ? Icons.Pause : Icons.Play;
        IcoPlay.Fill = playing ? (Brush)FindResource("BrushText") : (Brush)FindResource("BrushGold");
    }

    private void ShowControls()
    {
        ControlsBar.Opacity = 1;
        ControlsBar.IsHitTestVisible = true;
        Cursor = Cursors.Arrow;
        _hideTimer ??= new DispatcherTimer { Interval = ControlsHideDelay };
        _hideTimer.Tick -= OnHideControls;
        _hideTimer.Tick += OnHideControls;
        _hideTimer.Start();
    }

    private void OnHideControls(object? sender, EventArgs e)
    {
        _hideTimer?.Stop();
        // stay visible while paused/ended so the user always sees the play button
        if (!_engine.IsPlaying && !_startPending) return;
        if (_countdownTimer is not null) return;
        ControlsBar.Opacity = 0;
        ControlsBar.IsHitTestVisible = false;
        Cursor = Cursors.None;
    }

    /* ── input: buttons ──────────────────────────────────────────────── */

    private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_countdownTimer is not null) { EndCountdown(); return; }
        TogglePlayPause();
    }

    private void BtnRestart_Click(object sender, RoutedEventArgs e) => RestartFromBeginning();
    private void BtnBack_Click(object sender, RoutedEventArgs e) => Jump(-JumpPx);
    private void BtnFwd_Click(object sender, RoutedEventArgs e) => Jump(+JumpPx);
    private void BtnEndRestart_Click(object sender, RoutedEventArgs e) => RestartFromBeginning();
    private void BtnEndExit_Click(object sender, RoutedEventArgs e) => Close();

    private double JumpPx => Math.Max(60, Viewport.ActualHeight * 0.2);

    private void BtnExit_Click(object sender, RoutedEventArgs e) => Close();

    private void BtnFontUp_Click(object sender, RoutedEventArgs e) => ChangeFontSize(+6);
    private void BtnFontDown_Click(object sender, RoutedEventArgs e) => ChangeFontSize(-6);

    private void ChangeFontSize(double delta)
    {
        _settings.FontSize = Math.Clamp(_settings.FontSize + delta, FontMin, FontMax);
        TxtFont.Text = ((int)_settings.FontSize).ToString();
        Relayout(preserveProgress: true);
        NotifySettingsChanged();
    }

    private void SldSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_layoutReady) return;
        var speed = (int)Math.Round(e.NewValue);
        TxtSpeed.Text = speed.ToString();
        if (_engine.IsPlaying)
        {
            var cur = _animator.CurrentOffset;
            _engine.Seek(cur);
            _engine.SetSpeed(speed);
            PlayInternal(); // restart the animation at the new speed — no jump
        }
        else
        {
            _engine.SetSpeed(speed);
            UpdateProgressUi();
        }
        _settings.Speed = speed;
        NotifySettingsChanged();
    }

    private void SldWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_layoutReady) return;
        _settings.TextWidthPercent = Math.Clamp(e.NewValue, 30, 100);
        Relayout(preserveProgress: true);
        NotifySettingsChanged();
    }

    private void SldFocus_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_layoutReady) return;
        _settings.FocusPosition = Math.Clamp(e.NewValue, 0.20, 0.70);
        Relayout(preserveProgress: true);
        NotifySettingsChanged();
    }

    private void BtnGuide_Click(object sender, RoutedEventArgs e)
    {
        _settings.FocusGuide = !_settings.FocusGuide;
        Relayout(preserveProgress: true);
        NotifySettingsChanged();
    }

    private void BtnMirror_Click(object sender, RoutedEventArgs e)
    {
        _settings.Mirror = !_settings.Mirror;
        ApplyMirror();
        NotifySettingsChanged();
    }

    private void NotifySettingsChanged() => _settingsChanged(_settings);

    /* ── input: keyboard / mouse ─────────────────────────────────────── */

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // countdown: any of the play keys skips straight into reading
        if (_countdownTimer is not null && (e.Key == Key.Space || e.Key == Key.Enter))
        {
            EndCountdown();
            e.Handled = true;
            return;
        }
        switch (e.Key)
        {
            case Key.Space:
                TogglePlayPause();
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Up:
                SldSpeed.Value = Math.Min(100, _engine.Speed + 3);
                e.Handled = true;
                break;
            case Key.Down:
                SldSpeed.Value = Math.Max(1, _engine.Speed - 3);
                e.Handled = true;
                break;
            case Key.Right:
                Jump(+JumpPx);
                e.Handled = true;
                break;
            case Key.Left:
                Jump(-JumpPx);
                e.Handled = true;
                break;
            case Key.Home:
                PauseInternal();
                _animator.SetOffsetInstant(_engine.Seek(0));
                UpdateProgressUi();
                e.Handled = true;
                break;
            case Key.OemPlus or Key.Add:
                if (e.KeyboardDevice.Modifiers == ModifierKeys.Control) { ChangeFontSize(+6); e.Handled = true; }
                break;
            case Key.OemMinus or Key.Subtract:
                if (e.KeyboardDevice.Modifiers == ModifierKeys.Control) { ChangeFontSize(-6); e.Handled = true; }
                break;
        }
        ShowControls();
    }

    /// <summary>
    /// Manual wheel = pause auto-scroll, nudge the text, then the user resumes
    /// with Space/Play (chosen for stability — the wheel never fights the engine).
    /// </summary>
    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_countdownTimer is not null) { EndCountdown(); e.Handled = true; return; }
        PauseInternal();
        Jump(e.Delta * 1.2);
        e.Handled = true;
        ShowControls();
    }

    private void Window_PreviewMouseMove(object sender, MouseEventArgs e) => ShowControls();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _hideTimer?.Stop();
        _hintTimer?.Stop();
        _countdownTimer?.Stop();
        StopProgressTimer();
        if (_progressTimer is not null) _progressTimer.Tick -= OnProgressTick;
        PauseInternal();
        _persistProgress(CurrentFraction());
        NotifySettingsChanged();
    }

    /* ── dev/CI smoke seams (used only by SmokeRunner on the Windows runner) ── */

    internal double EngineMaxOffsetForSmoke => _engine.MaxOffset;
    internal double CurrentOffsetForSmoke => CurrentOffset;
    internal bool EnginePlayingForSmoke => _engine.IsPlaying;
    internal bool EndBannerShownForSmoke => EndBanner.Visibility == Visibility.Visible;

    internal void TogglePlayPauseForSmoke() => TogglePlayPause();

    internal void SetSpeedForSmoke(int speed)
    {
        if (!_engine.IsPlaying) TogglePlayPause(); // resume, then change speed live
        SldSpeed.Value = SpeedCurve.Clamp(speed);
    }

    internal void SeekEndForSmoke() => SeekEndInternal();
}
