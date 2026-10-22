namespace Rename.Core.Models;

/// <summary>
/// One file loaded into the app. Its position in <see cref="State.AppState.Files"/> is its visual order.
/// Selection and conflict flags live here so the UI can bind to them per item.
/// </summary>
public sealed class RenameFile : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    private string _originalPath;
    public string OriginalPath
    {
        get => _originalPath;
        set
        {
            if (SetProperty(ref _originalPath, value))
            {
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(Extension));
                OnPropertyChanged(nameof(Directory));
            }
        }
    }

    /// <summary>File name including extension.</summary>
    public string FileName => Path.GetFileName(OriginalPath);

    /// <summary>Extension without the leading dot, lower-cased. Empty when the file has none.</summary>
    public string Extension => PathParts.Extension(FileName).ToLowerInvariant();

    public string Directory => Path.GetDirectoryName(OriginalPath) ?? string.Empty;

    private string _computedName;
    /// <summary>Output of the rename engine for the current rules and ordering.</summary>
    public string ComputedName { get => _computedName; set => SetProperty(ref _computedName, value); }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    private bool _isConflict;
    /// <summary>True when the computed name collides with another file's or is not a valid Windows file name.</summary>
    public bool IsConflict { get => _isConflict; set => SetProperty(ref _isConflict, value); }

    private object? _thumbnail;
    /// <summary>Platform image (a WPF ImageSource in the desktop app). Typed as object so Core stays UI-free.</summary>
    public object? Thumbnail { get => _thumbnail; set => SetProperty(ref _thumbnail, value); }

    public RenameFile(string originalPath)
    {
        _originalPath = originalPath;
        _computedName = Path.GetFileName(originalPath);
    }
}

/// <summary>Splits a file name into stem and extension the way the macOS app does.</summary>
public static class PathParts
{
    public static string Stem(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        // No dot, or a leading dot only (".gitignore"): the whole name is the stem.
        return dot <= 0 ? fileName : fileName[..dot];
    }

    public static string Extension(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot <= 0 ? string.Empty : fileName[(dot + 1)..];
    }
}
