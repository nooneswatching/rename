using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Rename.Core.Models;
using Rename.Core.Services;

namespace Rename.Core.State;

/// <summary>
/// Single source of truth for the app. Owns the ordered file list, the ordered rule list, selection,
/// the undo stack and the derived name/conflict state. Views bind to it; services are injected so it
/// can be exercised without a UI.
/// </summary>
public sealed class AppState : ObservableObject
{
    private const int MaxUndoDepth = 10;

    private readonly IFileDialogService _dialogs;
    private readonly IThumbnailService _thumbnails;
    private readonly SynchronizationContext? _syncContext;
    private readonly HashSet<Guid> _selected = [];
    private readonly List<RenameOperation> _undoStack = [];
    private readonly HashSet<RenameRuleItem> _hookedRules = [];
    private bool _isRecomputing;

    public RangeObservableCollection<RenameFile> Files { get; } = [];
    public ObservableCollection<RenameRuleItem> Rules { get; } = [];
    public IReadOnlyList<RenameOperation> UndoStack => _undoStack;

    public AppState(IFileDialogService? dialogs = null, IThumbnailService? thumbnails = null)
    {
        _dialogs = dialogs ?? new NoOpFileDialogService();
        _thumbnails = thumbnails ?? new NoOpThumbnailService();
        _syncContext = SynchronizationContext.Current;
        Files.CollectionChanged += OnFilesChanged;
        Rules.CollectionChanged += OnRulesChanged;
    }

    // MARK: - Derived state

    public bool CanUndo => _undoStack.Count > 0;
    public int FileCount => Files.Count;
    public bool HasFiles => Files.Count > 0;
    public bool CanApply => HasFiles && !HasConflicts;

    private IReadOnlySet<string> _conflictedNames = new HashSet<string>();
    /// <summary>Computed names that more than one file would receive.</summary>
    public IReadOnlySet<string> ConflictedNames { get => _conflictedNames; private set => SetProperty(ref _conflictedNames, value); }

    private int _conflictCount;
    /// <summary>Duplicate names plus names that are invalid on Windows.</summary>
    public int ConflictCount { get => _conflictCount; private set => SetProperty(ref _conflictCount, value); }

    public bool HasConflicts => ConflictCount > 0;

    private string? _errorMessage;
    public string? ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

    private string _extensionFilter = string.Empty;
    /// <summary>Lower-case extension without dot; empty shows every file.</summary>
    public string ExtensionFilter
    {
        get => _extensionFilter;
        set
        {
            if (SetProperty(ref _extensionFilter, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(VisibleFiles));
            }
        }
    }

    private IReadOnlyList<string> _availableExtensions = [];
    public IReadOnlyList<string> AvailableExtensions { get => _availableExtensions; private set => SetProperty(ref _availableExtensions, value); }

    public IReadOnlyCollection<Guid> SelectedFileIds => _selected;
    public int SelectionCount => _selected.Count;
    public IReadOnlyList<RenameFile> SelectedFiles => Files.Where(f => _selected.Contains(f.Id)).ToList();

    /// <summary>Files passing the extension filter, in visual order.</summary>
    public IReadOnlyList<RenameFile> VisibleFiles => Files.Where(IsVisible).ToList();

    public bool IsVisible(RenameFile file)
        => _extensionFilter.Length == 0 || string.Equals(file.Extension, _extensionFilter, StringComparison.OrdinalIgnoreCase);

    /// <summary>The file shown in the preview bar: the single selected file, otherwise the first file.</summary>
    public RenameFile? PreviewFile
    {
        get
        {
            if (_selected.Count == 1)
            {
                var id = _selected.First();
                var match = Files.FirstOrDefault(f => f.Id == id);
                if (match is not null) return match;
            }
            return Files.FirstOrDefault();
        }
    }

    // MARK: - File loading

    public void AddFiles(IEnumerable<string> paths)
    {
        var newFiles = FileLoader.MakeFiles(paths, Files);
        if (newFiles.Count == 0) return;
        Files.AddRange(newFiles);
        LoadThumbnails(newFiles);
    }

    public void AddFilesFromFolder(string folder) => AddFiles(FileLoader.TopLevelFilePaths(folder));

    public void HandleDroppedPath(string path)
    {
        if (Directory.Exists(path)) AddFilesFromFolder(path);
        else AddFiles([path]);
    }

    public void ClearFiles()
    {
        Files.ReplaceAll([]);
        _selected.Clear();
        RaiseSelectionChanged();
    }

    public void RemoveFiles(IEnumerable<Guid> ids)
    {
        var set = ids as ISet<Guid> ?? new HashSet<Guid>(ids);
        if (set.Count == 0) return;
        Files.RemoveAll(f => set.Contains(f.Id));
        _selected.ExceptWith(set);
        RaiseSelectionChanged();
    }

    public void OpenFolderPicker()
    {
        var folder = _dialogs.PickFolder();
        if (folder is not null) AddFilesFromFolder(folder);
    }

    public void OpenFilePicker()
    {
        var files = _dialogs.PickFiles();
        if (files.Count > 0) AddFiles(files);
    }

    // MARK: - Ordering

    /// <summary>Swift-style move: the moved files end up before the element originally at <paramref name="toOffset"/>.</summary>
    public void MoveFiles(IEnumerable<int> fromIndices, int toOffset) => Files.MoveItems(fromIndices, toOffset);

    // MARK: - Selection

    public void SetSelection(IEnumerable<Guid> ids)
    {
        _selected.Clear();
        _selected.UnionWith(ids);
        RaiseSelectionChanged();
    }

    public void Select(Guid id) => SetSelection([id]);

    public void AddToSelection(IEnumerable<Guid> ids)
    {
        _selected.UnionWith(ids);
        RaiseSelectionChanged();
    }

    public void ToggleSelection(Guid id)
    {
        if (!_selected.Remove(id)) _selected.Add(id);
        RaiseSelectionChanged();
    }

    public void ClearSelection()
    {
        if (_selected.Count == 0) return;
        _selected.Clear();
        RaiseSelectionChanged();
    }

    private void RaiseSelectionChanged()
    {
        foreach (var file in Files) file.IsSelected = _selected.Contains(file.Id);
        OnPropertyChanged(nameof(SelectedFileIds));
        OnPropertyChanged(nameof(SelectionCount));
        OnPropertyChanged(nameof(SelectedFiles));
        OnPropertyChanged(nameof(PreviewFile));
    }

    // MARK: - Rules

    public void AddRule(RenameRule rule) => Rules.Add(new RenameRuleItem(rule));

    public void MoveRules(IEnumerable<int> fromIndices, int toOffset)
    {
        var reordered = Rules.ToList();
        if (!MoveSemantics.Apply(reordered, fromIndices, toOffset)) return;
        Rules.Clear();
        foreach (var item in reordered) Rules.Add(item);
    }

    public void RemoveRule(Guid id)
    {
        var item = Rules.FirstOrDefault(r => r.Id == id);
        if (item is not null) Rules.Remove(item);
    }

    public void ToggleRule(Guid id)
    {
        var item = Rules.FirstOrDefault(r => r.Id == id);
        if (item is not null) item.IsEnabled = !item.IsEnabled;
    }

    // MARK: - Rename

    /// <summary>Changes that Apply would perform: every file whose computed name differs from its current name.</summary>
    public List<RenameChange> PendingChanges()
        => Files.Where(f => !string.Equals(f.ComputedName, f.FileName, StringComparison.Ordinal))
                .Select(f => new RenameChange(f.OriginalPath, Path.Combine(f.Directory, f.ComputedName)))
                .ToList();

    public void ApplyRename()
    {
        if (HasConflicts) return;
        var changes = PendingChanges();
        if (changes.Count == 0) return;
        try
        {
            var operation = RenameExecutor.Execute(changes);
            PushUndo(operation);
            UpdatePaths(changes.Select(c => (c.From, c.To)));
        }
        catch (RenameExecutorException e)
        {
            ErrorMessage = e.Message;
        }
    }

    public void PerformUndo()
    {
        var operation = PopUndo();
        if (operation is null) return;
        try
        {
            RenameExecutor.Undo(operation);
            UpdatePaths(operation.Changes.Select(c => (c.To, c.From)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ErrorMessage = e.Message;
        }
    }

    /// <summary>Points files at their new paths in place so cached thumbnails and ids survive a rename.</summary>
    private void UpdatePaths(IEnumerable<(string From, string To)> moves)
    {
        var map = new Dictionary<string, string>(FileLoader.PathComparer);
        foreach (var (from, to) in moves) map[from] = to;
        foreach (var file in Files)
        {
            if (map.TryGetValue(file.OriginalPath, out var updated)) file.OriginalPath = updated;
        }
        RecomputeNames();
        OnPropertyChanged(nameof(AvailableExtensions));
        OnPropertyChanged(nameof(PreviewFile));
    }

    // MARK: - Undo stack

    public void PushUndo(RenameOperation operation)
    {
        _undoStack.Add(operation);
        if (_undoStack.Count > MaxUndoDepth) _undoStack.RemoveAt(0);
        OnPropertyChanged(nameof(UndoStack));
        OnPropertyChanged(nameof(CanUndo));
    }

    public RenameOperation? PopUndo()
    {
        if (_undoStack.Count == 0) return null;
        var top = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        OnPropertyChanged(nameof(UndoStack));
        OnPropertyChanged(nameof(CanUndo));
        return top;
    }

    // MARK: - Change handlers

    private void OnFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var ids = new HashSet<Guid>(Files.Select(f => f.Id));
        var pruned = _selected.RemoveWhere(id => !ids.Contains(id)) > 0;

        RecomputeNames();
        AvailableExtensions = Files.Select(f => f.Extension)
            .Where(x => x.Length > 0)
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        if (_extensionFilter.Length > 0 && !AvailableExtensions.Contains(_extensionFilter))
        {
            ExtensionFilter = string.Empty;
        }

        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(VisibleFiles));
        OnPropertyChanged(nameof(PreviewFile));
        if (pruned) RaiseSelectionChanged();
    }

    private void OnRulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Re-sync subscriptions against the live list; handles Reset (Clear) as well as Add/Remove/Move.
        var live = new HashSet<RenameRuleItem>(Rules);
        foreach (var item in _hookedRules.Where(i => !live.Contains(i)).ToList())
        {
            item.PropertyChanged -= OnRuleItemChanged;
            _hookedRules.Remove(item);
        }
        foreach (var item in Rules)
        {
            if (_hookedRules.Add(item)) item.PropertyChanged += OnRuleItemChanged;
        }
        RecomputeNames();
    }

    private void OnRuleItemChanged(object? sender, PropertyChangedEventArgs e) => RecomputeNames();

    private void RecomputeNames()
    {
        if (_isRecomputing) return;
        _isRecomputing = true;
        try
        {
            var names = RenameEngine.Compute(Files, Rules);
            for (var i = 0; i < Files.Count; i++) Files[i].ComputedName = names[i];

            var duplicates = ConflictDetector.FindConflicts(names);
            var invalidCount = 0;
            for (var i = 0; i < Files.Count; i++)
            {
                var invalid = !FileNameValidator.IsValid(names[i]);
                if (invalid) invalidCount++;
                Files[i].IsConflict = invalid || duplicates.Contains(names[i]);
            }

            ConflictedNames = duplicates;
            ConflictCount = duplicates.Count + invalidCount;
            OnPropertyChanged(nameof(HasConflicts));
            OnPropertyChanged(nameof(CanApply));
        }
        finally
        {
            _isRecomputing = false;
        }
    }

    // MARK: - Thumbnails

    private void LoadThumbnails(IEnumerable<RenameFile> targets)
    {
        foreach (var file in targets) _ = LoadThumbnailAsync(file);
    }

    private async Task LoadThumbnailAsync(RenameFile file)
    {
        object? image;
        try
        {
            image = await _thumbnails.GetThumbnailAsync(file.OriginalPath).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return;
        }
        if (image is null) return;
        RunOnStateThread(() =>
        {
            if (Files.Any(f => f.Id == file.Id)) file.Thumbnail = image;
        });
    }

    private void RunOnStateThread(Action action)
    {
        if (_syncContext is null || ReferenceEquals(_syncContext, SynchronizationContext.Current)) action();
        else _syncContext.Post(_ => action(), null);
    }
}
