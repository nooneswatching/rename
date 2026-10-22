namespace Rename.Core.Services;

/// <summary>Checks computed names against Windows file-name rules so Apply never hands an impossible name to the OS.</summary>
public static class FileNameValidator
{
    private static readonly char[] InvalidChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static bool IsValid(string name) => Problem(name) is null;

    /// <summary>Returns a short reason the name is invalid, or null when it is fine.</summary>
    public static string? Problem(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Name is empty";
        if (name is "." or "..") return "Name is reserved";
        if (name.IndexOfAny(InvalidChars) >= 0) return "Name contains a character not allowed on Windows (< > : \" / \\ | ? *)";
        if (name.Any(c => c < 32)) return "Name contains a control character";
        if (name.EndsWith(' ') || name.EndsWith('.')) return "Name cannot end with a space or a period";
        var stem = Models.PathParts.Stem(name);
        if (ReservedNames.Contains(stem) || ReservedNames.Contains(name)) return "Name is reserved by Windows";
        if (name.Length > 255) return "Name is longer than 255 characters";
        return null;
    }
}
