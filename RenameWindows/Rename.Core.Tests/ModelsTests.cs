using Rename.Core.Models;
using Xunit;

namespace Rename.Core.Tests;

public class ModelsTests
{
    [Fact]
    public void RenameFile_IdIsStableAcrossMutation()
    {
        var file = new RenameFile("/tmp/test.jpg");
        var id = file.Id;
        file.ComputedName = "modified.jpg";
        Assert.Equal(id, file.Id);
    }

    [Fact]
    public void RenameFile_ComputedNameDefaultsToOriginal()
    {
        var file = new RenameFile("/tmp/photo.jpg");
        Assert.Equal("photo.jpg", file.ComputedName);
        Assert.Equal("photo.jpg", file.FileName);
        Assert.Equal("jpg", file.Extension);
    }

    [Fact]
    public void RenameFile_ChangingPathUpdatesFileName()
    {
        var file = new RenameFile("/tmp/photo.jpg");
        var raised = new List<string?>();
        file.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        file.OriginalPath = "/tmp/other.PNG";
        Assert.Equal("other.PNG", file.FileName);
        Assert.Equal("png", file.Extension);
        Assert.Contains(nameof(RenameFile.FileName), raised);
    }

    [Fact]
    public void RenameRuleItem_DefaultsToEnabled() => Assert.True(new RenameRuleItem(new PrefixRule("IMG_")).IsEnabled);

    [Fact]
    public void RenameRuleItem_ForwardsRuleEdits()
    {
        var rule = new PrefixRule("a");
        var item = new RenameRuleItem(rule);
        var raised = 0;
        item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(RenameRuleItem.Rule)) raised++; };
        rule.Text = "b";
        Assert.Equal(1, raised);
    }

    [Fact]
    public void RenameOperation_StoresChanges()
    {
        var op = new RenameOperation([new RenameChange("/tmp/old.jpg", "/tmp/new.jpg")]);
        Assert.Single(op.Changes);
        Assert.Equal("/tmp/old.jpg", op.Changes[0].From);
        Assert.Equal("/tmp/new.jpg", op.Changes[0].To);
    }

    [Fact]
    public void RuleCatalog_CreatesFreshInstances()
    {
        var entry = RuleCatalog.Templates.First();
        Assert.NotSame(entry.Create(), entry.Create());
        Assert.Equal(11, RuleCatalog.Templates.Count);
    }

    [Fact]
    public void MoveSemantics_MatchesSwiftMoveFromOffsets()
    {
        var list = new List<string> { "a", "b", "c", "d", "e" };
        MoveSemantics.Apply(list, [0], 3);            // move "a" before "d"
        Assert.Equal(["b", "c", "a", "d", "e"], list);

        list = ["a", "b", "c", "d", "e"];
        MoveSemantics.Apply(list, [3, 4], 1);         // move "d","e" before "b"
        Assert.Equal(["a", "d", "e", "b", "c"], list);

        list = ["a", "b", "c", "d", "e"];
        MoveSemantics.Apply(list, [1, 3], 5);         // move "b","d" to the end
        Assert.Equal(["a", "c", "e", "b", "d"], list);

        list = ["a", "b", "c"];
        Assert.False(MoveSemantics.Apply(list, [1], 1)); // already there
        Assert.False(MoveSemantics.Apply(list, [1], 2)); // dropping just after itself is a no-op
        Assert.Equal(["a", "b", "c"], list);
    }
}
