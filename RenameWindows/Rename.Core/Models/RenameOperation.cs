namespace Rename.Core.Models;

public sealed record RenameChange(string From, string To);

/// <summary>A completed batch rename, kept on the undo stack.</summary>
public sealed class RenameOperation
{
    public DateTime Timestamp { get; }
    public IReadOnlyList<RenameChange> Changes { get; }

    public RenameOperation(IReadOnlyList<RenameChange> changes, DateTime? timestamp = null)
    {
        Changes = changes;
        Timestamp = timestamp ?? DateTime.Now;
    }
}
