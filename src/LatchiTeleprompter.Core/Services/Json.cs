using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;

namespace LatchiTeleprompter.Core.Services;

internal static class Json
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keeps Arabic readable on disk
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>Returns null on invalid JSON (corrupt files never crash the app).</summary>
    public static T? Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, Options); }
        catch { return null; }
    }
}
