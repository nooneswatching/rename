using Rename.Core.Models;
using Rename.Core.Services;
using Xunit;

namespace Rename.Core.Tests;

public class FileLoaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public FileLoaderTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() { try { Directory.Delete(_tempDir, true); } catch { } }

    private string MakeFile(string name)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, []);
        return path;
    }

    [Fact]
    public void MakeFiles_ReturnsOneFilePerPath() => Assert.Equal(2, FileLoader.MakeFiles([MakeFile("a.jpg"), MakeFile("b.jpg")], []).Count);

    [Fact]
    public void MakeFiles_DeduplicatesAgainstExisting()
    {
        var path = MakeFile("a.jpg");
        Assert.Empty(FileLoader.MakeFiles([path], [new RenameFile(path)]));
    }

    [Fact]
    public void MakeFiles_DeduplicatesWithinBatch()
    {
        var path = MakeFile("a.jpg");
        Assert.Single(FileLoader.MakeFiles([path, path], []));
    }

    [Fact]
    public void MakeFiles_SkipsDirectories()
    {
        var dir = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(dir);
        Assert.Empty(FileLoader.MakeFiles([dir], []));
    }

    [Fact]
    public void MakeFiles_SkipsNonExistentFiles() => Assert.Empty(FileLoader.MakeFiles([Path.Combine(_tempDir, "ghost.jpg")], []));

    [Fact]
    public void TopLevel_ReturnsTopLevelFilesOnly()
    {
        MakeFile("a.txt");
        MakeFile("b.txt");
        var sub = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllBytes(Path.Combine(sub, "c.txt"), []);

        var paths = FileLoader.TopLevelFilePaths(_tempDir);
        Assert.Equal(2, paths.Count);
        Assert.DoesNotContain(Path.Combine(sub, "c.txt"), paths);
    }

    [Fact]
    public void TopLevel_ExcludesHiddenFiles()
    {
        MakeFile("visible.txt");
        MakeFile(".hidden");
        var paths = FileLoader.TopLevelFilePaths(_tempDir);
        Assert.Single(paths);
        Assert.Equal("visible.txt", Path.GetFileName(paths[0]));
    }

    [Fact]
    public void TopLevel_SortedAlphabetically()
    {
        MakeFile("c.txt"); MakeFile("a.txt"); MakeFile("B.txt");
        Assert.Equal(["a.txt", "B.txt", "c.txt"], FileLoader.TopLevelFilePaths(_tempDir).Select(Path.GetFileName));
    }

    [Fact]
    public void TopLevel_MissingFolderReturnsEmpty() => Assert.Empty(FileLoader.TopLevelFilePaths(Path.Combine(_tempDir, "nope")));
}
