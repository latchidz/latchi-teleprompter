using LatchiTeleprompter.Core.Models;

namespace LatchiTeleprompter.Core.Services;

/// <summary>Loads/saves settings.json; a corrupt or missing file falls back to defaults.</summary>
public sealed class SettingsStore
{
    private readonly string _file;
    public AppSettings Current { get; private set; }

    public SettingsStore(string dataDir)
    {
        _file = Path.Combine(dataDir, "settings.json");
        Current = Json.Deserialize<AppSettings>(AtomicFile.TryReadAllText(_file)) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        // NaN window bounds (before the first layout) are not valid JSON numbers —
        // 0 is treated as "unset" by the restore logic, so this is loss-free.
        if (double.IsNaN(settings.WindowLeft)) settings.WindowLeft = 0;
        if (double.IsNaN(settings.WindowTop)) settings.WindowTop = 0;
        Current = settings;
        AtomicFile.WriteAllText(_file, Json.Serialize(settings));
    }

    public void Save() => Save(Current);
}
