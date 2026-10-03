using System.Text;

namespace LatchiTeleprompter.Core.Services;

/// <summary>
/// Crash-safe file writing: content is written to a .tmp file first, then swapped
/// over the target — a crash mid-write can never leave a truncated story/settings
/// file behind. Reading never throws (returns null on any failure).
/// </summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }

    public static string? TryReadAllText(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch { return null; }
    }

    public static bool TryDelete(string path)
    {
        try { if (File.Exists(path)) { File.Delete(path); return true; } return false; }
        catch { return false; }
    }
}
