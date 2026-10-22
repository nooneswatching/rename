namespace Rename.Core.Services;

/// <summary>Abstracts the platform file pickers so AppState can be unit tested without a UI.</summary>
public interface IFileDialogService
{
    /// <summary>Returns the chosen folder, or null if cancelled.</summary>
    string? PickFolder();

    /// <summary>Returns the chosen files; empty if cancelled.</summary>
    IReadOnlyList<string> PickFiles();
}

public sealed class NoOpFileDialogService : IFileDialogService
{
    public string? PickFolder() => null;
    public IReadOnlyList<string> PickFiles() => [];
}
