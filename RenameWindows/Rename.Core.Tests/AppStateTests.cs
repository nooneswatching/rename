using Rename.Core.Models;
using Rename.Core.Services;
using Rename.Core.State;
using Xunit;

namespace Rename.Core.Tests;

public class AppStateTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public AppStateTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() { try { Directory.Delete(_tempDir, true); } catch { } }

    private string MakeFile(string name)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, "x");
        return path;
    }

    private static RenameFile F(string name) => new("/" + name);

    [Fact]
    public void InitialState_IsEmpty()
    {
        var s = new AppState();
        Assert.Empty(s.Files);
        Assert.Empty(s.Rules);
        Assert.Empty(s.UndoStack);
        Assert.Empty(s.SelectedFileIds);
        Assert.False(s.CanUndo);
        Assert.False(s.CanApply);
        Assert.Null(s.PreviewFile);
    }

    [Fact]
    public void UndoStack_CappedAtTen()
    {
        var s = new AppState();
        for (var i = 0; i < 15; i++) s.PushUndo(new RenameOperation([]));
        Assert.Equal(10, s.UndoStack.Count);
    }

    [Fact]
    public void UndoStack_DropsOldestWhenCapped()
    {
        var s = new AppState();
        var first = new RenameOperation([], new DateTime(2000, 1, 1));
        s.PushUndo(first);
        for (var i = 2; i <= 11; i++) s.PushUndo(new RenameOperation([], new DateTime(2000, 1, i)));
        Assert.Equal(10, s.UndoStack.Count);
        Assert.NotEqual(first.Timestamp, s.UndoStack[0].Timestamp);
    }

    [Fact]
    public void CanUndo_TracksStack()
    {
        var s = new AppState();
        Assert.False(s.CanUndo);
        s.PushUndo(new RenameOperation([]));
        Assert.True(s.CanUndo);
        Assert.NotNull(s.PopUndo());
        Assert.False(s.CanUndo);
        Assert.Null(s.PopUndo());
    }

    // RemoveFiles

    [Fact]
    public void RemoveFiles_RemovesOnlyTargeted()
    {
        var s = new AppState();
        var a = F("a.txt"); var b = F("b.txt"); var c = F("c.txt");
        s.Files.AddRange([a, b, c]);
        s.RemoveFiles([a.Id, c.Id]);
        Assert.Equal([b.Id], s.Files.Select(f => f.Id));
    }

    [Fact]
    public void RemoveFiles_RemovingAllLeavesEmpty()
    {
        var s = new AppState();
        var a = F("a.txt"); var b = F("b.txt");
        s.Files.AddRange([a, b]);
        s.RemoveFiles([a.Id, b.Id]);
        Assert.Empty(s.Files);
    }

    [Fact]
    public void RemoveFiles_PrunesSelection()
    {
        var s = new AppState();
        var a = F("a.txt"); var b = F("b.txt");
        s.Files.AddRange([a, b]);
        s.SetSelection([a.Id, b.Id]);
        s.RemoveFiles([a.Id]);
        Assert.Equal([b.Id], s.SelectedFileIds);
        Assert.True(b.IsSelected);
    }

    [Fact]
    public void RemoveFiles_UnknownIdsAreNoOp()
    {
        var s = new AppState();
        s.Files.AddRange([F("a.txt")]);
        s.RemoveFiles([Guid.NewGuid()]);
        Assert.Single(s.Files);
    }

    // Names, conflicts, rules

    [Fact]
    public void Names_RecomputeWhenRulesAndFilesChange()
    {
        var s = new AppState();
        var a = F("a.jpg"); var b = F("b.jpg");
        s.Files.AddRange([a, b]);
        Assert.Equal("a.jpg", a.ComputedName);

        s.AddRule(new PrefixRule("X_"));
        Assert.Equal("X_a.jpg", a.ComputedName);
        Assert.Equal("X_b.jpg", b.ComputedName);

        ((PrefixRule)s.Rules[0].Rule).Text = "Y_";
        Assert.Equal("Y_a.jpg", a.ComputedName);

        s.ToggleRule(s.Rules[0].Id);
        Assert.Equal("a.jpg", a.ComputedName);

        s.ToggleRule(s.Rules[0].Id);
        s.RemoveRule(s.Rules[0].Id);
        Assert.Empty(s.Rules);
        Assert.Equal("a.jpg", a.ComputedName);
    }

    [Fact]
    public void Conflicts_BlockApply()
    {
        var s = new AppState();
        var a = F("a.jpg"); var b = F("b.jpg");
        s.Files.AddRange([a, b]);
        Assert.True(s.CanApply);

        s.AddRule(new NumberSequenceRule(1, 1, 1, NumberPosition.ReplaceAll));
        Assert.False(s.HasConflicts);

        s.AddRule(new TemplateRule("same"));
        Assert.True(s.HasConflicts);
        Assert.Equal(1, s.ConflictCount);
        Assert.True(a.IsConflict);
        Assert.True(b.IsConflict);
        Assert.False(s.CanApply);
    }

    [Fact]
    public void InvalidWindowsNames_CountAsConflicts()
    {
        var s = new AppState();
        var a = F("a.jpg");
        s.Files.AddRange([a]);
        s.AddRule(new PrefixRule("bad:"));
        Assert.True(a.IsConflict);
        Assert.True(s.HasConflicts);
        Assert.False(s.CanApply);
    }

    [Fact]
    public void MoveFiles_ReordersAndRenumbers()
    {
        var s = new AppState();
        var a = F("a.jpg"); var b = F("b.jpg"); var c = F("c.jpg");
        s.Files.AddRange([a, b, c]);
        s.AddRule(new NumberSequenceRule(1, 1, 1, NumberPosition.Prefix));
        s.MoveFiles([2], 0);
        Assert.Equal([c, a, b], s.Files);
        Assert.Equal("1_c.jpg", c.ComputedName);
        Assert.Equal("2_a.jpg", a.ComputedName);
    }

    [Fact]
    public void MoveRules_ChangesApplicationOrder()
    {
        var s = new AppState();
        var a = F("photo.jpg");
        s.Files.AddRange([a]);
        s.AddRule(new PrefixRule("img_"));
        s.AddRule(new ChangeCaseRule(CaseStyle.Upper));
        Assert.Equal("IMG_PHOTO.jpg", a.ComputedName);

        s.MoveRules([1], 0);   // uppercase first, then prefix
        Assert.Equal("img_PHOTO.jpg", a.ComputedName);

        // Edits to a moved rule still propagate.
        ((PrefixRule)s.Rules[1].Rule).Text = "x_";
        Assert.Equal("x_PHOTO.jpg", a.ComputedName);
    }

    // Selection & preview

    [Fact]
    public void Selection_DrivesPreviewFile()
    {
        var s = new AppState();
        var a = F("a.jpg"); var b = F("b.jpg");
        s.Files.AddRange([a, b]);
        Assert.Same(a, s.PreviewFile);
        s.Select(b.Id);
        Assert.Same(b, s.PreviewFile);
        s.ToggleSelection(a.Id);
        Assert.Same(a, s.PreviewFile);   // two selected → first file
        s.ClearSelection();
        Assert.Empty(s.SelectedFileIds);
        Assert.False(b.IsSelected);
    }

    [Fact]
    public void ClearFiles_ResetsEverything()
    {
        var s = new AppState();
        var a = F("a.jpg");
        s.Files.AddRange([a]);
        s.Select(a.Id);
        s.ClearFiles();
        Assert.Empty(s.Files);
        Assert.Empty(s.SelectedFileIds);
        Assert.Equal(0, s.FileCount);
    }

    // Filter

    [Fact]
    public void ExtensionFilter_LimitsVisibleFiles()
    {
        var s = new AppState();
        var a = F("a.jpg"); var b = F("b.PNG"); var c = F("README");
        s.Files.AddRange([a, b, c]);
        Assert.Equal(["jpg", "png"], s.AvailableExtensions);
        s.ExtensionFilter = "png";
        Assert.Equal([b], s.VisibleFiles);
        s.RemoveFiles([b.Id]);
        Assert.Equal(string.Empty, s.ExtensionFilter);   // filter cleared when its extension is gone
        Assert.Equal(2, s.VisibleFiles.Count);
    }

    // Loading

    [Fact]
    public void AddFiles_DedupesAndLoadsThumbnails()
    {
        var thumbs = new RecordingThumbnailService();
        var s = new AppState(thumbnails: thumbs);
        var p = MakeFile("a.jpg");
        s.AddFiles([p, p]);
        s.AddFiles([p]);
        Assert.Single(s.Files);
        Assert.Equal([p], thumbs.Requested);
        Assert.Equal("thumb", s.Files[0].Thumbnail);
    }

    [Fact]
    public void HandleDroppedPath_FolderLoadsTopLevelFiles()
    {
        var s = new AppState();
        MakeFile("a.jpg"); MakeFile("b.jpg");
        s.HandleDroppedPath(_tempDir);
        Assert.Equal(2, s.FileCount);
        s.HandleDroppedPath(MakeFile("c.jpg"));
        Assert.Equal(3, s.FileCount);
    }

    [Fact]
    public void Pickers_UseInjectedDialogService()
    {
        var p = MakeFile("picked.jpg");
        var s = new AppState(dialogs: new StubDialogs { Folder = _tempDir, Files = [p] });
        s.OpenFilePicker();
        Assert.Single(s.Files);
        s.OpenFolderPicker();
        Assert.Single(s.Files);   // same file, deduplicated
    }

    // Apply / undo against a real temp directory

    [Fact]
    public void ApplyRename_RenamesOnDiskAndIsUndoable()
    {
        var s = new AppState();
        var a = MakeFile("a.jpg"); var b = MakeFile("b.jpg");
        s.AddFiles([a, b]);
        s.AddRule(new PrefixRule("X_"));
        s.ApplyRename();

        Assert.Null(s.ErrorMessage);
        Assert.True(File.Exists(Path.Combine(_tempDir, "X_a.jpg")));
        Assert.False(File.Exists(a));
        Assert.Equal("X_a.jpg", s.Files[0].FileName);
        Assert.True(s.CanUndo);
        Assert.Equal("X_X_a.jpg", s.Files[0].ComputedName);   // rules re-apply to the new names, as on macOS

        s.PerformUndo();
        Assert.Null(s.ErrorMessage);
        Assert.True(File.Exists(a));
        Assert.Equal("a.jpg", s.Files[0].FileName);
        Assert.False(s.CanUndo);
    }

    [Fact]
    public void ApplyRename_SkipsUnchangedFiles()
    {
        var s = new AppState();
        var a = MakeFile("a.jpg"); var b = MakeFile("b.jpg");
        s.AddFiles([a, b]);
        s.AddRule(new FindReplaceRule("a", "z"));
        Assert.Single(s.PendingChanges());
        s.ApplyRename();
        Assert.Null(s.ErrorMessage);
        Assert.True(File.Exists(Path.Combine(_tempDir, "z.jpg")));
        Assert.True(File.Exists(b));
        Assert.Single(s.UndoStack[0].Changes);
    }

    [Fact]
    public void ApplyRename_FailureSurfacesErrorAndRollsBack()
    {
        var s = new AppState();
        var a = MakeFile("a.jpg");
        MakeFile("X_b.jpg");           // not loaded, but occupies the target name of b
        var b = MakeFile("b.jpg");
        s.AddFiles([a, b]);
        s.AddRule(new PrefixRule("X_"));
        s.ApplyRename();

        Assert.NotNull(s.ErrorMessage);
        Assert.Contains("b.jpg", s.ErrorMessage);
        Assert.True(File.Exists(a));   // rolled back
        Assert.False(s.CanUndo);
        Assert.Equal("a.jpg", s.Files[0].FileName);
    }

    [Fact]
    public void ApplyRename_DoesNothingWithConflicts()
    {
        var s = new AppState();
        var a = MakeFile("a.jpg"); var b = MakeFile("b.jpg");
        s.AddFiles([a, b]);
        s.AddRule(new TemplateRule("same"));
        s.ApplyRename();
        Assert.True(File.Exists(a));
        Assert.False(s.CanUndo);
    }

    private sealed class StubDialogs : IFileDialogService
    {
        public string? Folder { get; init; }
        public IReadOnlyList<string> Files { get; init; } = [];
        public string? PickFolder() => Folder;
        public IReadOnlyList<string> PickFiles() => Files;
    }

    private sealed class RecordingThumbnailService : IThumbnailService
    {
        public List<string> Requested { get; } = [];
        public Task<object?> GetThumbnailAsync(string path, CancellationToken cancellationToken = default)
        {
            Requested.Add(path);
            return Task.FromResult<object?>("thumb");
        }
        public void ClearCache() { }
    }
}
