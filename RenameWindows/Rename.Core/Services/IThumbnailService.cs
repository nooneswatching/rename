namespace Rename.Core.Services;

/// <summary>Produces a platform image for a file (a WPF ImageSource in the desktop app).</summary>
public interface IThumbnailService
{
    Task<object?> GetThumbnailAsync(string path, CancellationToken cancellationToken = default);
    void ClearCache();
}

public sealed class NoOpThumbnailService : IThumbnailService
{
    public Task<object?> GetThumbnailAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult<object?>(null);
    public void ClearCache() { }
}
