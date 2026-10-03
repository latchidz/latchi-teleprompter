namespace LatchiTeleprompter.Core.Engine;

/// <summary>Detects the dominant direction of a script from its first strong character.</summary>
public static class TextDirection
{
    public static bool IsRtl(string? text)
    {
        if (string.IsNullOrEmpty(text)) return true; // Arabic-first app default
        foreach (var ch in text)
        {
            var c = (int)ch;
            if (IsRtlChar(c)) return true;
            if (IsLtrChar(c)) return false;
        }
        return true; // neutral (digits/punctuation only) → RTL default
    }

    private static bool IsRtlChar(int c) =>
        (c >= 0x0590 && c <= 0x05FF)   // Hebrew
        || (c >= 0x0600 && c <= 0x06FF) // Arabic
        || (c >= 0x0700 && c <= 0x08FF) // Syriac… Arabic Extended
        || (c >= 0xFB1D && c <= 0xFDCF) // Hebrew presentation
        || (c >= 0xFDF0 && c <= 0xFDFF) // Arabic presentation A
        || (c >= 0xFE70 && c <= 0xFEFF); // Arabic presentation B

    private static bool IsLtrChar(int c) =>
        (c >= 0x41 && c <= 0x5A) || (c >= 0x61 && c <= 0x7A) // A-Z a-z
        || (c >= 0xC0 && c <= 0x24F);                        // Latin extended
}
