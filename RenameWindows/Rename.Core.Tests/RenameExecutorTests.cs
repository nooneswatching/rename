using Rename.Core.Models;
using Rename.Core.Services;
using Xunit;

namespace Rename.Core.Tests;

public class RenameExecutorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public RenameExecutorTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() { try { Directory.Delete(_tempDir, true); } catch { } }

    private string MakeFile(string name, string content = "x")
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private string PathFor(string name) => Path.Combine(_tempDir, name);

    [Fact]
    public void Execute_RenamesFile()
    {
        var from = MakeFile("old.txt");
        var to = PathFor("new.txt");
        var op = RenameExecutor.Execute([new RenameChange(from, to)]);
        Assert.True(File.Exists(to));
        Assert.False(File.Exists(from));
        Assert.Single(op.Changes);
    }

    [Fact]
    public void Execute_RollsBackOnPartialFailure()
    {
        var from1 = MakeFile("a.txt");
        var from2 = MakeFile("b.txt");
        var to1 = PathFor("a_new.txt");
        var to2 = Path.Combine(_tempDir, "nonexistent_dir", "b_new.txt");

        var ex = Assert.Throws<RenameExecutorException>(() => RenameExecutor.Execute([new RenameChange(from1, to1), new RenameChange(from2, to2)]));
        Assert.Contains("b.txt", ex.Message);
        Assert.True(File.Exists(from1));
        Assert.False(File.Exists(to1));
    }

    [Fact]
    public void Execute_RefusesToOverwriteExistingFile()
    {
        var from = MakeFile("a.txt", "source");
        var to = MakeFile("b.txt", "target");
        Assert.Throws<RenameExecutorException>(() => RenameExecutor.Execute([new RenameChange(from, to)]));
        Assert.Equal("source", File.ReadAllText(from));
        Assert.Equal("target", File.ReadAllText(to));
    }

    [Fact]
    public void Undo_RevertsRename()
    {
        var from = MakeFile("original.txt");
        var to = PathFor("renamed.txt");
        var op = RenameExecutor.Execute([new RenameChange(from, to)]);
        RenameExecutor.Undo(op);
        Assert.True(File.Exists(from));
        Assert.False(File.Exists(to));
    }

    [Fact]
    public void Execute_MultipleFiles()
    {
        var from1 = MakeFile("1.txt");
        var from2 = MakeFile("2.txt");
        var to1 = PathFor("one.txt");
        var to2 = PathFor("two.txt");
        RenameExecutor.Execute([new RenameChange(from1, to1), new RenameChange(from2, to2)]);
        Assert.True(File.Exists(to1));
        Assert.True(File.Exists(to2));
        Assert.False(File.Exists(from1));
        Assert.False(File.Exists(from2));
    }
}
