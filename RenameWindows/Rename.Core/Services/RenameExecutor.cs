using Rename.Core.Models;

namespace Rename.Core.Services;

public sealed class RenameExecutorException : Exception
{
    public RenameChange Change { get; }

    public RenameExecutorException(RenameChange change, Exception inner)
        : base($"Could not rename \"{Path.GetFileName(change.From)}\" to \"{Path.GetFileName(change.To)}\": {inner.Message}", inner)
    {
        Change = change;
    }
}

public static class RenameExecutor
{
    /// <summary>
    /// Renames every file in the list. On any failure, rolls back the renames already done (in reverse order)
    /// and throws. Returns a RenameOperation suitable for the undo stack on success.
    /// </summary>
    public static RenameOperation Execute(IReadOnlyList<RenameChange> changes)
    {
        var completed = new List<RenameChange>(changes.Count);
        foreach (var change in changes)
        {
            try
            {
                MoveExclusive(change.From, change.To);
                completed.Add(change);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                for (var i = completed.Count - 1; i >= 0; i--)
                {
                    try { File.Move(completed[i].To, completed[i].From); }
                    catch (Exception) { /* best effort rollback */ }
                }
                throw new RenameExecutorException(change, e);
            }
        }
        return new RenameOperation(changes);
    }

    /// <summary>Reverts an operation by moving files back to their original paths, last change first.</summary>
    public static void Undo(RenameOperation operation)
    {
        for (var i = operation.Changes.Count - 1; i >= 0; i--)
        {
            var change = operation.Changes[i];
            MoveExclusive(change.To, change.From);
        }
    }

    /// <summary>
    /// Like File.Move but never overwrites. Changing only the letter case of a name is allowed
    /// (on a case-insensitive volume the destination "exists" yet is the same file).
    /// </summary>
    private static void MoveExclusive(string from, string to)
    {
        var sameFile = string.Equals(Path.GetFullPath(from), Path.GetFullPath(to),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        if (!sameFile && (File.Exists(to) || Directory.Exists(to)))
        {
            throw new IOException($"\"{Path.GetFileName(to)}\" already exists");
        }
        File.Move(from, to, overwrite: false);
    }
}
