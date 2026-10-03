
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using LatchiTeleprompter.App.Services;
using LatchiTeleprompter.App.Views;
using LatchiTeleprompter.Core.Engine;
using LatchiTeleprompter.Core.Models;
using LatchiTeleprompter.Core.Services;

namespace LatchiTeleprompter.App.Views;

/// <summary>
/// Editor shell: script editing with autosave, story library, import/export,
/// settings entry and the "start teleprompter" action. The teleprompter itself
/// lives in TeleprompterWindow.
/// </summary>
public partial class MainWindow : Window
{
    // DWM dark title bar (Windows 10 1809+; silently ignored elsewhere)
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private readonly SettingsStore _settings;
    private readonly StoryLibrary _library;
    private readonly AutosaveService _autosave;

    /// <summary>Id of the story currently loaded in the editor (null = unsaved draft).</summary>
    private string? _storyId;
    private string _storyTitle = "";
    private double _lastReadingProgress;

    /// <summary>Row model for the library list (pre-formatted strings — no converters).</summary>
    public sealed class StoryRowVm
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public string Meta { get; init; } = "";
    }

    public MainWindow()
    {
        InitializeComponent();
        AppPaths.EnsureDataDir();

        _settings = new SettingsStore(AppPaths.DataDir);
        _library = new StoryLibrary(AppPaths.DataDir);
        _autosave = new AutosaveService(AppPaths.DataDir);
        _autosave.Saved += OnAutosaveWritten;

        Loaded += OnLoaded;
        SourceInitialized += (_, _) => TryDarkTitleBar();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestoreWindowBounds();
        RestoreSession();
        UpdateStats();
        Editor.Focus();
    }

    /* ── session restore ─────────────────────────────────────────────── */

    private void RestoreSession()
    {
        // 1) unsaved editor content (crash/quick-close recovery) wins
        var autosaved = _autosave.Load();
        if (autosaved is { Content.Length: > 0 })
        {
            Editor.Text = autosaved.Content;
            _storyId = autosaved.StoryId;
            _storyTitle = autosaved.Title;
            _lastReadingProgress = autosaved.ReadingProgress;
            SetAutosaveState("تم استرجاع آخر نص تلقائياً");
            return;
        }
        // 2) otherwise reopen the last story if the user wants that
        if (_settings.Current.ReopenLastStory && !string.IsNullOrEmpty(_settings.Current.LastStoryId))
        {
            var story = _library.Load(_settings.Current.LastStoryId);
            if (story is not null)
            {
                LoadStoryIntoEditor(story);
                return;
            }
        }
        // 3) fresh start — empty editor + hint
        Editor.Text = "";
    }

    private void LoadStoryIntoEditor(Story story)
    {
        _storyId = story.Id;
        _storyTitle = story.Title;
        _lastReadingProgress = story.ReadingProgress;
        Editor.Text = story.Content;
        if (_settings.Current.LastStoryId != story.Id)
        {
            _settings.Current.LastStoryId = story.Id;
            _settings.Save();
        }
        SetAutosaveState($"مفتوح: {story.Title}");
    }

    /* ── editor + autosave ───────────────────────────────────────────── */

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtHint.Visibility = Editor.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ApplyEditorDirection();
        _autosave.Schedule(_storyId, CurrentTitle(), Editor.Text, _lastReadingProgress);
    }

    /// <summary>The editor follows the script's dominant direction (Arabic→RTL).</summary>
    private void ApplyEditorDirection()
        => Editor.FlowDirection = TextDirection.IsRtl(Editor.Text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    private string CurrentTitle() =>
        _storyTitle.Length > 0 ? _storyTitle : StoryLibrary.SuggestTitle(Editor.Text);

    private void OnAutosaveWritten()
        => Dispatcher.BeginInvoke(() => SetAutosaveState($"محفوظ تلقائياً {DateTime.Now:HH:mm}"));

    private void SetAutosaveState(string text) => TxtAutosaveState.Text = text;

    private void UpdateStats()
    {
        var words = StoryLibrary.CountWords(Editor.Text);
        var chars = Editor.Text.Length;
        var eta = TimeEstimator.Format(TimeEstimator.Estimate(words, _settings.Current.Speed));
        TxtStats.Text = $"{words} كلمة · {chars} حرف · قراءة تقديرية ~{eta}";
    }

    /* ── story saving ────────────────────────────────────────────────── */

    /// <summary>Saves the editor content into the library (creating or updating).</summary>
    private Story? SaveCurrentStory()
    {
        var content = Editor.Text;
        if (content.Trim().Length == 0) return null; // nothing to save
        var story = _storyId is null ? null : _library.Load(_storyId);
        if (story is null)
        {
            story = new Story { Id = _library.NewId(), Title = StoryLibrary.SuggestTitle(content) };
            if (story.Title.Length == 0) story.Title = "قصة بدون عنوان";
            _storyId = story.Id;
            _storyTitle = story.Title;
        }
        story.Content = content;
        story.Title = _storyTitle.Length > 0 ? _storyTitle : story.Title;
        story.ReadingProgress = _lastReadingProgress;
        _library.Save(story);
        _settings.Current.LastStoryId = story.Id;
        _settings.Save();
        _autosave.ForceWrite(story.Id, story.Title, content, _lastReadingProgress);
        SetAutosaveState($"محفوظ: {story.Title}");
        return story;
    }

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardIfNeeded()) return;
        _storyId = null;
        _storyTitle = "";
        _lastReadingProgress = 0;
        Editor.Clear();
        _autosave.Clear();
        SetAutosaveState("قصة جديدة");
        Editor.Focus();
    }

    /// <summary>Asks to save when the editor holds unsaved changes (data-loss guard).</summary>
    private bool ConfirmDiscardIfNeeded()
    {
        if (Editor.Text.Trim().Length == 0) return true;
        var saved = _storyId is null ? null : _library.Load(_storyId);
        if (saved is not null && saved.Content == Editor.Text) return true; // identical → nothing to lose
        var r = Dialogs.Confirm(this,
            "لديك تعديلات غير محفوظة في المكتبة.",
            "حفظ التعديلات قبل المتابعة؟");
        if (r is null) return false; // cancelled
        if (r == true) SaveCurrentStory();
        return true;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e) => SaveCurrentStory();

    /* ── library ─────────────────────────────────────────────────────── */

    private void BtnLibrary_Click(object sender, RoutedEventArgs e) => ToggleLibrary();
    private void BtnLibraryClose_Click(object sender, RoutedEventArgs e) => ToggleLibrary();

    private void ToggleLibrary()
    {
        var open = LibraryPanel.Visibility != Visibility.Visible;
        LibraryPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (open) RefreshLibrary();
    }

    private void RefreshLibrary()
    {
        var rows = _library.ListStories()
            .Select(m => new StoryRowVm
            {
                Id = m.Id,
                Title = m.Title,
                Meta = $"{m.WordCount} كلمة · {m.UpdatedAtUtc.ToLocalTime():yyyy/MM/dd HH:mm}"
                       + (m.ReadingProgress > 0.01 ? $" · قراءة {(int)Math.Round(m.ReadingProgress * 100)}%" : ""),
            })
            .ToList();
        LibraryList.ItemsSource = rows;
        var current = rows.FirstOrDefault(r => r.Id == _storyId);
        if (current is not null) LibraryList.SelectedItem = current;
    }

    private void LibraryList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private StoryRowVm? SelectedRow => LibraryList.SelectedItem as StoryRowVm;

    private void LibraryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedRow is not null) OpenStory(SelectedRow.Id);
    }

    private void BtnStoryOpen_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not null) OpenStory(SelectedRow.Id);
    }

    private void OpenStory(string id)
    {
        if (!ConfirmDiscardIfNeeded()) return;
        var story = _library.Load(id);
        if (story is null)
        {
            Dialogs.Alert(this, "تعذّر فتح القصة — قد يكون الملف تالفاً.", "فتح قصة");
            RefreshLibrary();
            return;
        }
        LoadStoryIntoEditor(story);
        UpdateStats();
    }

    private void BtnStoryRename_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } row) return;
        var name = Dialogs.Input(this, "إعادة تسمية القصة", "العنوان الجديد:", row.Title);
        if (string.IsNullOrWhiteSpace(name)) return;
        if (!_library.Rename(row.Id, name))
        {
            Dialogs.Alert(this, "تعذّرت إعادة التسمية.", "إعادة تسمية");
            return;
        }
        if (row.Id == _storyId) _storyTitle = name.Trim();
        RefreshLibrary();
    }

    private void BtnStoryDuplicate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } row) return;
        if (_library.Duplicate(row.Id) is null)
        {
            Dialogs.Alert(this, "تعذّر تكرار القصة.", "تكرار");
            return;
        }
        RefreshLibrary();
    }

    private void BtnStoryDelete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } row) return;
        var ok = Dialogs.Confirm(this,
            $"سيتم حذف القصة «{row.Title}» نهائياً من المكتبة.",
            "هل أنت متأكد؟");
        if (ok != true) return;
        _library.Delete(row.Id);
        if (row.Id == _storyId) { _storyId = null; _storyTitle = ""; }
        if (_settings.Current.LastStoryId == row.Id) { _settings.Current.LastStoryId = null; _settings.Save(); }
        RefreshLibrary();
    }

    /* ── import / export ─────────────────────────────────────────────── */

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "استيراد ملف نصي",
            Filter = "ملفات نصية (*.txt)|*.txt|كل الملفات (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var text = File.ReadAllText(dlg.FileName, Encoding.UTF8);
            if (!ConfirmDiscardIfNeeded()) return;
            Editor.Text = text;
            _storyId = null;
            _storyTitle = "";
            SetAutosaveState("تم الاستيراد — احفظه في المكتبة");
        }
        catch (Exception ex)
        {
            Dialogs.Alert(this, "تعذّر قراءة الملف: " + ex.Message, "استيراد");
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (Editor.Text.Length == 0)
        {
            Dialogs.Alert(this, "لا يوجد نص للتصدير.", "تصدير");
            return;
        }
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "تصدير إلى ملف نصي",
            Filter = "ملف نصي (*.txt)|*.txt",
            FileName = (CurrentTitle().Length > 0 ? CurrentTitle() : "script") + ".txt",
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, Editor.Text, new UTF8Encoding(true));
            SetAutosaveState("تم التصدير");
        }
        catch (Exception ex)
        {
            Dialogs.Alert(this, "تعذّر التصدير: " + ex.Message, "تصدير");
        }
    }

    /* ── teleprompter ────────────────────────────────────────────────── */

    private void BtnStart_Click(object sender, RoutedEventArgs e) => StartTeleprompter();

    private void StartTeleprompter()
    {
        if (Editor.Text.Trim().Length == 0)
        {
            Dialogs.Alert(this, "الصق أو اكتب السيناريو أولاً.", "ابدأ العرض");
            return;
        }
        var story = SaveCurrentStory(); // never enter reading mode with an unsaved script
        _autosave.ForceWrite(_storyId, CurrentTitle(), Editor.Text, _lastReadingProgress);

        var teleprompter = new TeleprompterWindow(
            Editor.Text,
            _settings.Current,
            persistProgress: fraction =>
            {
                _lastReadingProgress = fraction;
                _autosave.ForceWrite(_storyId, CurrentTitle(), Editor.Text, fraction);
                var story2 = _storyId is null ? null : _library.Load(_storyId);
                if (story2 is not null) { story2.ReadingProgress = fraction; _library.Save(story2); }
            },
            settingsChanged: s => { _settings.Save(); UpdateStats(); });
        teleprompter.Owner = this;
        teleprompter.LastProgress = _lastReadingProgress; // resume where the user left off
        teleprompter.ShowDialog();
        UpdateStats();
        Editor.Focus();
    }

    /* ── settings ────────────────────────────────────────────────────── */

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(this, _settings)
        {
            Owner = this,
            DataCleared = () =>
            {
                // library + autosave wiped by the settings dialog — reset the editor
                _storyId = null;
                _storyTitle = "";
                _lastReadingProgress = 0;
                Editor.Clear();
                _autosave.Clear();
                SetAutosaveState("تم مسح البيانات");
                RefreshLibrary();
            },
        };
        win.ShowDialog();
        UpdateStats();
    }

    /* ── keyboard ────────────────────────────────────────────────────── */

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+S — save to library
        if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            SaveCurrentStory();
            e.Handled = true;
            return;
        }
        // Ctrl+Enter — start teleprompter (Enter alone must keep inserting lines)
        if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.Enter)
        {
            StartTeleprompter();
            e.Handled = true;
            return;
        }
        // bare Enter starts the teleprompter only when the editor is NOT focused
        if (e.Key == Key.Enter && !Editor.IsKeyboardFocused)
        {
            StartTeleprompter();
            e.Handled = true;
        }
    }

    /* ── window lifecycle ────────────────────────────────────────────── */

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // data-loss guard: flush editor content + persist bounds
        _autosave.ForceWrite(_storyId, CurrentTitle(), Editor.Text, _lastReadingProgress);
        _autosave.Dispose();
        var s = _settings.Current;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            s.WindowLeft = Left; s.WindowTop = Top; s.WindowWidth = Width; s.WindowHeight = Height;
        }
        _settings.Save();
    }

    private void RestoreWindowBounds()
    {
        var s = _settings.Current;
        if (s.WindowMaximized) { WindowState = WindowState.Maximized; return; }
        if (s.WindowWidth < MinWidth || s.WindowHeight < MinHeight) return;
        // clamp to the visible virtual desktop (multi-monitor safety)
        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var right = left + SystemParameters.VirtualScreenWidth;
        var bottom = top + SystemParameters.VirtualScreenHeight;
        var x = Math.Clamp(double.IsNaN(s.WindowLeft) ? left : s.WindowLeft, left - 40, right - 120);
        var y = Math.Clamp(double.IsNaN(s.WindowTop) ? top : s.WindowTop, top, bottom - 80);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = x; Top = y; Width = s.WindowWidth; Height = s.WindowHeight;
    }

    private void TryDarkTitleBar()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int on = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
        }
        catch { /* cosmetic only — never fatal */ }
    }
}
