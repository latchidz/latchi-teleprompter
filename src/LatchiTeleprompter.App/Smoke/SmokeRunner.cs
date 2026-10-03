using System.IO;
using System.Windows;
using System.Windows.Threading;
using LatchiTeleprompter.App.Services;
using LatchiTeleprompter.App.Views;
using LatchiTeleprompter.Core.Engine;
using LatchiTeleprompter.Core.Models;
using LatchiTeleprompter.Core.Services;

namespace LatchiTeleprompter.App.Smoke;

/// <summary>
/// Production self-test harness (dev/CI only, launched with `--smoke`).
/// Boots the REAL app against an isolated data folder and exercises the full
/// user path: services → editor → library → teleprompter layout/play/pause/
/// speed/end → persistence. Writes a JSON report and exits 0 only when every
/// check passes. Runs on the CI Windows runner so the shipped exe is validated,
/// not just the debugger build.
/// </summary>
public static class SmokeRunner
{
    private sealed class Check
    {
        public string Name = "";
        public bool Pass;
        public string Detail = "";
    }

    private static readonly List<Check> Checks = new();

    public static bool ShouldRun(string[] args) => args.Any(a =>
        string.Equals(a, "--smoke", StringComparison.OrdinalIgnoreCase));

    public static int Run()
    {
        string dataDir = "";
        try
        {
            // isolated, wiped data folder → deterministic boot state
            dataDir = Environment.GetEnvironmentVariable("LATCHI_TP_DATA")
                      ?? Path.Combine(Path.GetTempPath(), "latchi-tp-smoke");
            if (Directory.Exists(dataDir)) Directory.Delete(dataDir, recursive: true);
            Directory.CreateDirectory(dataDir);
            Environment.SetEnvironmentVariable("LATCHI_TP_DATA", dataDir);

            RunCore(dataDir);
        }
        catch (Exception ex)
        {
            Checks.Add(new Check { Name = "fatal", Pass = false, Detail = ex.ToString() });
        }

        var allPass = Checks.All(c => c.Pass);
        var report = new System.Text.StringBuilder();
        report.AppendLine("{");
        report.AppendLine($"  \"passed\": {Checks.Count(c => c.Pass)}, \"failed\": {Checks.Count(c => !c.Pass)},");
        report.AppendLine("  \"checks\": [");
        report.AppendLine(string.Join(",\n", Checks.Select(c =>
            $"    {{\"name\": \"{c.Name}\", \"pass\": {(c.Pass ? "true" : "false")}, \"detail\": {Json(c.Detail)}}}")));
        report.AppendLine("  ]");
        report.AppendLine("}");
        var outPath = Environment.GetEnvironmentVariable("LATCHI_SMOKE_OUT")
                      ?? Path.Combine(dataDir, "smoke-results.json");
        try { File.WriteAllText(outPath, report.ToString()); } catch { /* report path may be unwritable */ }
        Console.WriteLine(report.ToString());
        return allPass ? 0 : 1;
    }

    private static string Json(string s) =>
        "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";

    private static void Add(string name, bool pass, string detail = "") =>
        Checks.Add(new Check { Name = name, Pass = pass, Detail = detail });

    private static void RunCore(string dataDir)
    {
        /* ── S1: settings store ─────────────────────────────────────── */
        var settings = new SettingsStore(dataDir);
        Add("S1 settings defaults", settings.Current.Speed == 45 && settings.Current.FontSize == 48
            && settings.Current.Alignment == "auto" && settings.Current.Countdown,
            $"speed={settings.Current.Speed} font={settings.Current.FontSize}");

        settings.Current.Speed = 70;
        settings.Current.Alignment = "center";
        settings.Save();
        var reloaded = new SettingsStore(dataDir);
        Add("S1 settings roundtrip", reloaded.Current.Speed == 70 && reloaded.Current.Alignment == "center",
            $"speed={reloaded.Current.Speed}");

        // corrupt settings must fall back to defaults, never crash
        File.WriteAllText(Path.Combine(dataDir, "settings.json"), "{ not json ]");
        Add("S1 corrupt settings safe", new SettingsStore(dataDir).Current.Speed == 45);

        /* ── S2: story library ──────────────────────────────────────── */
        var lib = new StoryLibrary(dataDir);
        var story = new Story { Id = lib.NewId(), Title = "سرقة القرن — قصة اختبار", Content = SampleScript(80) };
        lib.Save(story);
        var story2 = new Story { Id = lib.NewId(), Title = "قصة ثانية", Content = "نص قصير" };
        lib.Save(story2);
        Add("S2 library save+list", lib.ListStories().Count == 2, $"count={lib.ListStories().Count}");

        lib.Rename(story.Id, "عنوان جديد");
        Add("S2 rename", lib.Load(story.Id)!.Title == "عنوان جديد");

        var dup = lib.Duplicate(story.Id);
        Add("S2 duplicate", dup is not null && dup.Id != story.Id && dup.Content == story.Content);

        // a corrupted story file is isolated — the library still opens
        File.WriteAllText(Path.Combine(dataDir, "scripts", "zzzz-corrupt.json"), "{{{ broken");
        Add("S2 corrupt story isolated", lib.ListStories().Count == 3);

        lib.Delete(dup!.Id);
        lib.Delete(story2.Id);
        Add("S2 delete", lib.ListStories().Count == 1);

        Add("S2 title suggestion",
            StoryLibrary.SuggestTitle("🎬 سرقة القرن — قصة حقيقية\n\nالنص...") == "سرقة القرن — قصة حقيقية",
            $"got=\"{StoryLibrary.SuggestTitle("🎬 سرقة القرن — قصة حقيقية\n\nالنص...")}\"");

        /* ── S3: autosave ───────────────────────────────────────────── */
        var autosaveDir = Path.Combine(dataDir, "recovery-test");
        Directory.CreateDirectory(autosaveDir);
        var autosave = new AutosaveService(autosaveDir, TimeSpan.FromMilliseconds(120));
        var savedCount = 0;
        autosave.Saved += () => System.Threading.Interlocked.Increment(ref savedCount);
        autosave.Schedule(null, "عنوان", "نص تجريبي", 0.4);
        autosave.Schedule(null, "عنوان", "نص تجريبي محدث", 0.4); // rescheduled → one write
        Pump(1.2);
        var loaded = autosave.Load();
        Add("S3 autosave debounce", loaded is not null && loaded.Content == "نص تجريبي محدث"
            && savedCount >= 1, $"saved={savedCount} content={loaded?.Content}");
        autosave.Dispose();

        /* ── S4: engine math ────────────────────────────────────────── */
        var engine = new TeleprompterEngine();
        engine.Configure(maxOffset: 1000, speed: 50); // 154 px/s
        engine.Seek(0);
        var rem = engine.RemainingSeconds; // ≈ 6.49s
        engine.Play();
        engine.Seek(770);
        Add("S4 engine", engine is { IsPlaying: true, Progress: 0.77 }
            && Math.Abs(rem - 1000 / (4.0 + 3.0 * 50)) < 0.01
            && Math.Abs(engine.RemainingSeconds - 230 / (4.0 + 3.0 * 50)) < 0.01,
            $"rem0={rem:F2} rem1={engine.RemainingSeconds:F2}");
        engine.Seek(1000);
        engine.Play(); // at the end, Play must be a no-op
        Add("S4 engine end", engine.AtEnd && !engine.IsPlaying);
        engine.Restart();
        Add("S4 engine restart", engine is { IsPlaying: false, Offset: 0 });

        Add("S4 speed curve", SpeedCurve.PxPerSecond(1) == 7 && SpeedCurve.PxPerSecond(100) == 304
            && SpeedCurve.Presets["normal"] == 45);

        /* ── S5: script parser + direction ──────────────────────────── */
        var markers = ScriptParser.FindMarkers("سطر أول\n[توقف 1.5ث]\nسطر ثالث [pause 2s]");
        Add("S5 markers", markers.Count == 2 && Math.Abs(markers[0].PauseSeconds - 1.5) < 0.01
            && Math.Abs(markers[1].PauseSeconds - 2) < 0.01 && markers[0].LineIndex == 2,
            $"n={markers.Count}");
        Add("S5 direction", TextDirection.IsRtl("قصة بالدارجة") && !TextDirection.IsRtl("English story")
            && TextDirection.IsRtl("قصة مع Joseph James O'Keefe"));

        /* ── S6: real UI — editor + autosave + teleprompter ────────── */
        // MainWindow on the UI thread
        MainWindow? main = null;
        TeleprompterWindow? tele = null;
        RunOnUi(() =>
        {
            main = new MainWindow();
            main.Show();
            main.Editor.Text = SampleScript(1200); // long Arabic script
        });
        Pump(1.0);

        long words = 0, chars = 0;
        string statsText = "";
        RunOnUi(() =>
        {
            words = StoryLibrary.CountWords(main!.Editor.Text);
            chars = main!.Editor.Text.Length;
            statsText = main!.TxtStats.Text;
        });
        Add("S6 editor stats", words > 1000 && chars > 5000 && statsText.Contains("كلمة"),
            $"words={words} chars={chars} stats=\"{statsText}\"");

        // autosave picked the editor content
        Pump(1.5);
        var mainAutosave = new AutosaveService(dataDir).Load();
        Add("S6 editor autosave", mainAutosave is { Content.Length: > 5000 } && mainAutosave.Content.Contains("سرقة القرن"),
            $"len={mainAutosave?.Content.Length ?? 0}");

        // teleprompter window: layout + real animation + pause + end
        double maxOffset = 0, offsetAfterPlay = 0, offsetFrozen = 0;
        bool endShown = false;
        double persisted = -1;
        RunOnUi(() =>
        {
            settings.Current.Countdown = false; // deterministic run: no 3-2-1 delay
            tele = new TeleprompterWindow(SampleScript(1200), settings.Current,
                persistProgress: f => persisted = f,
                settingsChanged: _ => { })
            {
                Owner = main,
                LastProgress = 0,
            };
            tele.Show();
        });
        Pump(1.5); // Loaded → Relayout → (countdown off) → autoplay

        RunOnUi(() => maxOffset = tele!.EngineMaxOffsetForSmoke);
        Add("S6 layout measured", maxOffset > 300, $"maxOffset={maxOffset:F0}px");

        RunOnUi(() => offsetAfterPlay = tele!.CurrentOffsetForSmoke);
        Add("S6 autoplay started", tele!.EnginePlayingForSmoke && offsetAfterPlay >= 0,
            $"playing={tele!.EnginePlayingForSmoke} offset={offsetAfterPlay:F1}");

        Pump(1.5); // ~1.5s of scrolling at default speed
        var offsetMid = 0.0;
        RunOnUi(() => offsetMid = tele!.CurrentOffsetForSmoke);
        Add("S6 scroll advances", offsetMid > offsetAfterPlay, $"from={offsetAfterPlay:F1} to={offsetMid:F1}");

        // pause → freeze exactly (two reads 400ms apart are identical)
        RunOnUi(() => tele!.TogglePlayPauseForSmoke());
        var f1 = 0.0; var f2 = 0.0;
        RunOnUi(() => f1 = tele!.CurrentOffsetForSmoke);
        Pump(0.4);
        RunOnUi(() => f2 = tele!.CurrentOffsetForSmoke);
        Add("S6 pause freeze", !tele!.EnginePlayingForSmoke && Math.Abs(f1 - f2) < 0.01,
            $"f1={f1:F2} f2={f2:F2}");

        // live speed change keeps position monotonic
        RunOnUi(() => tele!.SetSpeedForSmoke(90));
        Pump(0.8);
        var afterSpeed = 0.0;
        RunOnUi(() => { afterSpeed = tele!.CurrentOffsetForSmoke; tele!.TogglePlayPauseForSmoke(); }); // pause again
        Add("S6 live speed", afterSpeed >= f1, $"afterSpeed={afterSpeed:F1} (was {f1:F1})");

        // jump to the end → end banner + progress persist
        RunOnUi(() => tele!.SeekEndForSmoke());
        Pump(0.3);
        RunOnUi(() => endShown = tele!.EndBannerShownForSmoke);
        Add("S6 end detection", endShown && persisted >= 0.99, $"banner={endShown} persisted={persisted:F2}");

        // close everything cleanly
        RunOnUi(() => { tele!.Close(); main!.Close(); });
        Pump(0.3);
    }

    /// <summary>Long original Arabic/Darja sample (no copyrighted content).</summary>
    private static string SampleScript(int paragraphs) =>
        string.Join("\n\n", Enumerable.Range(0, paragraphs).Select(i =>
            $"الفقرة {i + 1}: هذي قصة تجريبية بالدارجة الجزائرية، فيها أسماء إنجليزية مثل Joseph James O'Keefe وأرقام مثل 1976 وعلامات مثل [توقف 1.5ث] 🎬، باش نتحققو من العرض الصحيح للنص المختلط عربي وإنجليزي وأرقام، ومن الحفظ التلقائي ومن التمرير السلس للقراءة أثناء التسجيل بالهاتف."));

    /* ── UI-thread helpers ──────────────────────────────────────────── */

    private static void RunOnUi(Action action)
    {
        var app = Application.Current;
        if (app is null) throw new InvalidOperationException("no Application instance");
        app.Dispatcher.Invoke(action);
    }

    /// <summary>Pumps the WPF dispatcher so layouts/animations advance in real time.</summary>
    private static void Pump(double seconds)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            Application.Current?.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            System.Threading.Thread.Sleep(16);
        }
    }
}
