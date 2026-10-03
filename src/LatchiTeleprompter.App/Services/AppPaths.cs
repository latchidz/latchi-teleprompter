using System.IO;

namespace LatchiTeleprompter.App.Services;

/// <summary>All local data lives under %APPDATA%\LATCHI Teleprompter (overridable for CI smoke runs).</summary>
public static class AppPaths
{
    public static string DataDir
    {
        get
        {
            var custom = Environment.GetEnvironmentVariable("LATCHI_TP_DATA");
            if (!string.IsNullOrWhiteSpace(custom)) return custom;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LATCHI Teleprompter");
        }
    }

    public static void EnsureDataDir()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(ScriptsDir);
        Directory.CreateDirectory(RecoveryDir);
    }

    public static string ScriptsDir => Path.Combine(DataDir, "scripts");
    public static string RecoveryDir => Path.Combine(DataDir, "recovery");
    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string AutosaveFile => Path.Combine(DataDir, "autosave.json");

    /// <summary>Opens the data folder in Explorer (created on demand).</summary>
    public static void OpenInExplorer()
    {
        Directory.CreateDirectory(DataDir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = DataDir,
            UseShellExecute = true,
        });
    }
}
