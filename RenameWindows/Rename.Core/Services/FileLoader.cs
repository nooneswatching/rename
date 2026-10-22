using Rename.Core.Models;

namespace Rename.Core.Services;

public static class FileLoader
{
    public static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Converts paths into new RenameFile objects. Deduplicates against existing files and within the batch.
    /// Directories and paths that do not exist are silently skipped.
    /// </summary>
    public static List<RenameFile> MakeFiles(IEnumerable<string> paths, IEnumerable<RenameFile> existing)
    {
        var seen = new HashSet<string>(existing.Select(f => Normalize(f.OriginalPath)), PathComparer);
        var result = new List<RenameFile>();
        foreach (var path in paths)
        {
            var full = Normalize(path);
            if (!File.Exists(full)) continue;          // false for directories and missing files
            if (!seen.Add(full)) continue;
            result.Add(new RenameFile(full));
        }
        return result;
    }

    /// <summary>Top-level regular files in a folder (no subfolders, no hidden files), sorted by name.</summary>
    public static List<string> TopLevelFilePaths(string folder)
    {
        try
        {
            var dir = new DirectoryInfo(folder);
            if (!dir.Exists) return [];
            return dir.EnumerateFiles()
                .Where(f => !IsHidden(f))
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.Name, StringComparer.Ordinal)
                .Select(f => f.FullName)
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return [];
        }
    }

    private static bool IsHidden(FileInfo file)
        => file.Name.StartsWith('.') || (file.Attributes & FileAttributes.Hidden) != 0;

    private static string Normalize(string path)
    {
        try { return Path.GetFullPath(path); }
        catch (Exception e) when (e is ArgumentException or PathTooLongException or NotSupportedException) { return path; }
    }
}
