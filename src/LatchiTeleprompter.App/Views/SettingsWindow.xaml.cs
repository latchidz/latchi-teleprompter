using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LatchiTeleprompter.App.Services;
using LatchiTeleprompter.Core.Engine;
using LatchiTeleprompter.Core.Models;
using LatchiTeleprompter.Core.Services;

namespace LatchiTeleprompter.App.Views;

/// <summary>Settings dialog — edits a working copy, saves atomically on "حفظ".</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private readonly AppSettings _draft;
    private string _alignment = "auto";

    /// <summary>Raised after the user clears all app data (editor resets itself).</summary>
    public Action? DataCleared { get; set; }

    public SettingsWindow(Window owner, SettingsStore store)
    {
        _store = store;
        _draft = new AppSettings
        {
            // clone current settings (working copy)
            Speed = store.Current.Speed,
            FontSize = store.Current.FontSize,
            TextWidthPercent = store.Current.TextWidthPercent,
            Alignment = store.Current.Alignment,
            FocusGuide = store.Current.FocusGuide,
            FocusPosition = store.Current.FocusPosition,
            Countdown = store.Current.Countdown,
            ShowProgress = store.Current.ShowProgress,
            Mirror = store.Current.Mirror,
            ReopenLastStory = store.Current.ReopenLastStory,
            LastStoryId = store.Current.LastStoryId,
        };
        _alignment = _draft.Alignment;

        InitializeComponent();

        SldFont.Value = Math.Clamp(_draft.FontSize, 24, 120);
        SldSpeed.Value = SpeedCurve.Clamp(_draft.Speed);
        SldWidth.Value = Math.Clamp(_draft.TextWidthPercent, 30, 100);
        SldFocus.Value = Math.Clamp(_draft.FocusPosition, 0.20, 0.70);
        ChkGuide.IsChecked = _draft.FocusGuide;
        ChkCountdown.IsChecked = _draft.Countdown;
        ChkProgress.IsChecked = _draft.ShowProgress;
        ChkMirror.IsChecked = _draft.Mirror;
        ChkReopen.IsChecked = _draft.ReopenLastStory;

        SldFont.ValueChanged += (_, e) => { _draft.FontSize = e.NewValue; TxtFontVal.Text = ((int)e.NewValue).ToString(); };
        SldSpeed.ValueChanged += (_, e) => { _draft.Speed = (int)e.NewValue; TxtSpeedVal.Text = ((int)e.NewValue).ToString(); };
        SldWidth.ValueChanged += (_, e) => { _draft.TextWidthPercent = e.NewValue; TxtWidthVal.Text = ((int)e.NewValue) + "%"; };
        SldFocus.ValueChanged += (_, e) => { _draft.FocusPosition = e.NewValue; TxtFocusVal.Text = (int)Math.Round(e.NewValue * 100) + "%"; };
        TxtFontVal.Text = ((int)SldFont.Value).ToString();
        TxtSpeedVal.Text = ((int)SldSpeed.Value).ToString();
        TxtWidthVal.Text = ((int)SldWidth.Value) + "%";
        TxtFocusVal.Text = (int)Math.Round(SldFocus.Value * 100) + "%";

        HighlightAlign();
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out var v))
        {
            SldSpeed.Value = v;
        }
    }

    private void Align_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag }) { _alignment = tag; _draft.Alignment = tag; HighlightAlign(); }
    }

    private void HighlightAlign()
    {
        foreach (var btn in new[] { AlAuto, AlRight, AlCenter, AlLeft })
        {
            var selected = (string)btn.Tag == _alignment;
            btn.Foreground = selected
                ? (Brush)FindResource("BrushGold")
                : (Brush)FindResource("BrushMuted");
            btn.FontWeight = selected ? FontWeights.Bold : FontWeights.Normal;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _draft.FontSize = SldFont.Value;
        _draft.Speed = (int)SldSpeed.Value;
        _draft.TextWidthPercent = SldWidth.Value;
        _draft.FocusPosition = SldFocus.Value;
        _draft.Alignment = _alignment;
        _draft.FocusGuide = ChkGuide.IsChecked == true;
        _draft.Countdown = ChkCountdown.IsChecked == true;
        _draft.ShowProgress = ChkProgress.IsChecked == true;
        _draft.Mirror = ChkMirror.IsChecked == true;
        _draft.ReopenLastStory = ChkReopen.IsChecked == true;

        var current = _store.Current;
        _draft.LastStoryId = current.LastStoryId;
        _draft.WindowLeft = current.WindowLeft;
        _draft.WindowTop = current.WindowTop;
        _draft.WindowWidth = current.WindowWidth;
        _draft.WindowHeight = current.WindowHeight;
        _draft.WindowMaximized = current.WindowMaximized;

        _store.Save(_draft);
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

    private void BtnOpenData_Click(object sender, RoutedEventArgs e) => AppPaths.OpenInExplorer();

    private void BtnClearData_Click(object sender, RoutedEventArgs e)
    {
        var ok = Dialogs.Confirm(this,
            "سيتم حذف جميع القصص والنصوص المحفوظة والإعدادات نهائياً من هذا الجهاز.",
            "مسح جميع بيانات التطبيق؟");
        if (ok != true) return;
        try
        {
            if (Directory.Exists(AppPaths.DataDir))
                Directory.Delete(AppPaths.DataDir, recursive: true);
            AppPaths.EnsureDataDir();
            DataCleared?.Invoke();
            Dialogs.Alert(this, "تم مسح جميع البيانات.", "مسح البيانات");
        }
        catch (Exception ex)
        {
            Dialogs.Alert(this, "تعذّر المسح الكامل: " + ex.Message, "مسح البيانات");
        }
    }
}
