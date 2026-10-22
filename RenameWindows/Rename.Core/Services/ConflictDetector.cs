namespace Rename.Core.Services;

public static class ConflictDetector
{
    /// <summary>
    /// Returns the names that appear more than once. Windows file systems are case-insensitive,
    /// so "Photo.jpg" and "photo.jpg" count as the same name.
    /// </summary>
    public static HashSet<string> FindConflicts(IEnumerable<string> names, StringComparer? comparer = null)
    {
        comparer ??= StringComparer.OrdinalIgnoreCase;
        var seen = new HashSet<string>(comparer);
        var conflicts = new HashSet<string>(comparer);
        foreach (var name in names)
        {
            if (!seen.Add(name)) conflicts.Add(name);
        }
        return conflicts;
    }
}
