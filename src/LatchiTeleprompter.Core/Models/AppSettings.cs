namespace LatchiTeleprompter.Core.Models;

/// <summary>User settings, persisted as JSON in the app-data folder.</summary>
public sealed class AppSettings
{
    // Teleprompter reading
    public int Speed { get; set; } = 45;                  // 1..100
    public double FontSize { get; set; } = 48;            // 24..120 DIP
    public double TextWidthPercent { get; set; } = 70;    // 30..100 % of viewport
    public string Alignment { get; set; } = "auto";       // auto | center | right | left
    public bool FocusGuide { get; set; } = true;
    public double FocusPosition { get; set; } = 0.38;     // 0.20..0.70 (fraction of viewport height)
    public bool Countdown { get; set; } = true;
    public bool ShowProgress { get; set; } = true;
    public bool Mirror { get; set; } = false;

    // Startup / session
    public bool ReopenLastStory { get; set; } = true;
    public string? LastStoryId { get; set; }
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 0;
    public double WindowHeight { get; set; } = 0;
    public bool WindowMaximized { get; set; }
}
