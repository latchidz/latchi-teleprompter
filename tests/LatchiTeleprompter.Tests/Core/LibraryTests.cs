using LatchiTeleprompter.Core.Models;
using LatchiTeleprompter.Core.Services;
using Xunit;

public class StoryLibraryTests : IDisposable
{
    private readonly string _dir;
    private readonly StoryLibrary _lib;

    public StoryLibraryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ltp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _lib = new StoryLibrary(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void SaveListLoad_Roundtrip()
    {
        var s = new Story { Id = _lib.NewId(), Title = "قصة السفينة الغامضة", Content = "نص" };
        _lib.Save(s);
        var list = _lib.ListStories();
        Assert.Single(list);
        Assert.Equal("قصة السفينة الغامضة", list[0].Title);
        Assert.Equal(1, list[0].WordCount);

        var loaded = _lib.Load(s.Id);
        Assert.NotNull(loaded);
        Assert.Equal("نص", loaded!.Content);
        Assert.Equal(s.Id, loaded.Id);
    }

    [Fact]
    public void CorruptFile_IsIsolated()
    {
        _lib.Save(new Story { Id = _lib.NewId(), Title = "سليمة", Content = "ok" });
        File.WriteAllText(Path.Combine(_dir, "scripts", "zz-broken.json"), "{ nope");
        var list = _lib.ListStories();
        Assert.Single(list);
    }

    [Fact]
    public void RenameDuplicateDelete()
    {
        var s = new Story { Id = _lib.NewId(), Title = "أ", Content = "المحتوى" };
        _lib.Save(s);
        Assert.True(_lib.Rename(s.Id, "ب"));
        Assert.Equal("ب", _lib.Load(s.Id)!.Title);
        Assert.False(_lib.Rename(s.Id, "   ")); // empty rejected

        var dup = _lib.Duplicate(s.Id);
        Assert.NotNull(dup);
        Assert.Equal("المحتوى", dup!.Content);
        Assert.NotEqual(s.Id, dup.Id);
        Assert.Contains("نسخة", dup.Title);
        Assert.Equal(2, _lib.ListStories().Count);

        Assert.True(_lib.Delete(dup.Id));
        Assert.Single(_lib.ListStories());
        Assert.Null(_lib.Load(dup.Id));
    }

    [Fact]
    public void TitleSuggestion_StripsEmojiAndDecorations()
    {
        Assert.Equal("سرقة القرن", StoryLibrary.SuggestTitle("🎬 سرقة القرن 🎬"));
        Assert.Equal("HOOK", StoryLibrary.SuggestTitle("🔥 HOOK"));
        Assert.Equal("بداية القصة", StoryLibrary.SuggestTitle("🕵️ بداية القصة\nالسطر الثاني"));
        // long line truncated
        var longTitle = new string('ك', 100);
        Assert.True(StoryLibrary.SuggestTitle(longTitle).Length <= 61);
        Assert.Equal("", StoryLibrary.SuggestTitle(""));
        Assert.Equal("", StoryLibrary.SuggestTitle("\n\n🎬\n\n"));
        // mid-title punctuation survives; skin-tone/ZWJ machinery is stripped
        Assert.Equal("الجزء 2: النهاية", StoryLibrary.SuggestTitle("🕵🏻‍♂️ الجزء 2: النهاية"));
        Assert.Equal("قصة — بالدارجة", StoryLibrary.SuggestTitle("قصة — بالدارجة"));
    }

    [Fact]
    public void Load_InvalidId_ReturnsNull()
    {
        Assert.Null(_lib.Load("../evil"));
        Assert.Null(_lib.Load(""));
        Assert.Null(_lib.Load("no-such-id-xx"));
    }

    [Fact]
    public void WordCount_Mixed()
    {
        Assert.Equal(0, StoryLibrary.CountWords(""));
        Assert.Equal(0, StoryLibrary.CountWords("   \n  "));
        Assert.Equal(5, StoryLibrary.CountWords("واحد اثنان ثلاثة four five"));
    }
}

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir;
    public SettingsStoreTests() { _dir = Path.Combine(Path.GetTempPath(), "ltp-set-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
    public void Dispose() { try { Directory.Delete(_dir, recursive: true); } catch { } }

    [Fact]
    public void MissingFile_Defaults()
    {
        var s = new SettingsStore(_dir);
        Assert.Equal(45, s.Current.Speed);
        Assert.Equal(48, s.Current.FontSize);
        Assert.Equal("auto", s.Current.Alignment);
        Assert.True(s.Current.Countdown);
    }

    [Fact]
    public void Roundtrip_Persists()
    {
        var s = new SettingsStore(_dir);
        s.Current.Speed = 88;
        s.Current.FontSize = 72;
        s.Current.Mirror = true;
        s.Save();
        var again = new SettingsStore(_dir);
        Assert.Equal(88, again.Current.Speed);
        Assert.Equal(72, again.Current.FontSize);
        Assert.True(again.Current.Mirror);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaults()
    {
        new SettingsStore(_dir).Save();
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "###broken###");
        Assert.Equal(45, new SettingsStore(_dir).Current.Speed);
    }

    [Fact]
    public void Save_IsAtomic_NoTmpLeftBehind()
    {
        var s = new SettingsStore(_dir);
        s.Save();
        Assert.False(File.Exists(Path.Combine(_dir, "settings.json.tmp")));
    }
}

public class AtomicFileTests : IDisposable
{
    private readonly string _dir;
    public AtomicFileTests() { _dir = Path.Combine(Path.GetTempPath(), "ltp-atom-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
    public void Dispose() { try { Directory.Delete(_dir, recursive: true); } catch { } }

    [Fact]
    public void Roundtrip()
    {
        var p = Path.Combine(_dir, "a.txt");
        AtomicFile.WriteAllText(p, "مرحبا LATCHI");
        Assert.Equal("مرحبا LATCHI", AtomicFile.TryReadAllText(p));
    }

    [Fact]
    public void Replace_KeepsContentIntact()
    {
        var p = Path.Combine(_dir, "b.json");
        AtomicFile.WriteAllText(p, "one");
        AtomicFile.WriteAllText(p, "two");
        Assert.Equal("two", AtomicFile.TryReadAllText(p));
        Assert.False(File.Exists(p + ".tmp"));
    }

    [Fact]
    public void MissingFile_ReadsNull()
    {
        Assert.Null(AtomicFile.TryReadAllText(Path.Combine(_dir, "missing.json")));
    }

    [Fact]
    public void Overwrite_OriginalIntactWhenTmpOnly()
    {
        // simulate a crash: only the .tmp exists → the original stays valid
        var p = Path.Combine(_dir, "c.json");
        AtomicFile.WriteAllText(p, "good");
        File.WriteAllText(p + ".tmp", "partial");
        Assert.Equal("good", AtomicFile.TryReadAllText(p));
    }
}
