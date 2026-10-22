using Rename.Core.Services;
using Xunit;

namespace Rename.Core.Tests;

public class ConflictDetectorTests
{
    [Fact] public void NoConflicts_ReturnsEmpty() => Assert.Empty(ConflictDetector.FindConflicts(["a.jpg", "b.jpg", "c.jpg"]));
    [Fact] public void DuplicateNames_Returned() => Assert.Equal(new HashSet<string> { "a.jpg" }, ConflictDetector.FindConflicts(["a.jpg", "b.jpg", "a.jpg"]));
    [Fact] public void MultipleDuplicates_AllReturned() => Assert.Equal(new HashSet<string> { "a.jpg", "b.jpg" }, ConflictDetector.FindConflicts(["a.jpg", "a.jpg", "b.jpg", "b.jpg", "c.jpg"]));
    [Fact] public void EmptyInput_ReturnsEmpty() => Assert.Empty(ConflictDetector.FindConflicts([]));
    [Fact] public void AllSameName_ReturnsThatName() => Assert.Equal(new HashSet<string> { "x.jpg" }, ConflictDetector.FindConflicts(["x.jpg", "x.jpg", "x.jpg"]));
    [Fact] public void SingleItem_NoConflict() => Assert.Empty(ConflictDetector.FindConflicts(["only.jpg"]));
    [Fact] public void CaseOnlyDifference_IsConflictOnWindowsFileSystems()
    {
        var conflicts = ConflictDetector.FindConflicts(["Photo.jpg", "photo.jpg"]);
        Assert.Single(conflicts);
        Assert.Contains("PHOTO.JPG", conflicts);
    }
}
