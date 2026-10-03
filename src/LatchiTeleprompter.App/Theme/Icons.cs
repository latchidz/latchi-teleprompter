using System.Windows.Media;

namespace LatchiTeleprompter.App.Theme;

/// <summary>Monochrome vector icons (24×24 viewbox, stroke-based) — no emoji, no bitmaps.</summary>
public static class Icons
{
    private static Geometry G(string data)
    {
        var g = Geometry.Parse(data);
        g.Freeze();
        return g;
    }

    public static readonly Geometry NewDoc = G("M7 3h7l5 5v13H7z M14 3v5h5 M11.5 12v7 M8 15.5h7");
    public static readonly Geometry Save = G("M12 3v10 M8 9.5l4 4 4-4 M4.5 16.5v2a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2v-2");
    public static readonly Geometry Library = G("M4.5 4h4.5v16H4.5z M10.5 4h4.5v16h-4.5z M17 5.2l4.2 15.4-4.3 1.2-4.2-15.4z");
    public static readonly Geometry Play = G("M8 5.2l11.5 6.8L8 18.8z");
    public static readonly Geometry Pause = G("M7.5 5h3.2v14H7.5z M13.3 5h3.2v14h-3.2z");
    public static readonly Geometry Restart = G("M20 12a8 8 0 1 1-2.5-5.8 M18.2 2.8v4h-4");
    public static readonly Geometry Settings = G("M4 8h9 M17 8h3 M15 5.8v4.4 M4 16h3 M11 16h9 M9 13.8v4.4");
    public static readonly Geometry Font = G("M5 19.5l5.3-15h1.9l5.3 15 M7.4 14.5h8.4");
    public static readonly Geometry Speed = G("M4.6 18.5a9 9 0 1 1 14.8 0 M12 13.5l4.2-4.2");
    public static readonly Geometry Fullscreen = G("M4 9.5V4h5.5 M20 9.5V4h-5.5 M4 14.5V20h5.5 M20 14.5V20h-5.5");
    public static readonly Geometry Close = G("M6 6l12 12 M18 6L6 18");
    public static readonly Geometry Import = G("M12 3v10 M8.5 9.5l3.5 3.5 3.5-3.5 M4.5 17v1.5a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2V17");
    public static readonly Geometry Export = G("M12 21V11 M8.5 14.5L12 18l3.5-3.5 M4.5 7V5.5a2 2 0 0 1 2-2h11a2 2 0 0 1 2 2V7");
    public static readonly Geometry Plus = G("M12 6v12 M6 12h12");
    public static readonly Geometry Minus = G("M6 12h12");
    public static readonly Geometry JumpBack = G("M11.5 18.5l-6.5-6.5 6.5-6.5 M18.5 18.5l-6.5-6.5 6.5-6.5");
    public static readonly Geometry JumpFwd = G("M12.5 5.5l6.5 6.5-6.5 6.5 M5.5 5.5l6.5 6.5-6.5 6.5");
    public static readonly Geometry Home = G("M4 11l8-7.5L20 11 M6.5 10v9.5h4v-5.5h3v5.5h4V10");
    public static readonly Geometry Guide = G("M3 12h18 M6 8.5h4.5 M13.5 8.5H18 M6 15.5h4.5 M13.5 15.5H18");
    public static readonly Geometry Mirror = G("M12 3v18 M8.5 7.5L4 12l4.5 4.5 M15.5 7.5L20 12l-4.5 4.5");
    public static readonly Geometry Percent = G("M6 18L18 6 M7.5 8.5a1.8 1.8 0 1 0 0-.01 M16.5 17.5a1.8 1.8 0 1 0 0-.01");
}
