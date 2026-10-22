using Rename.Core.Services;
using Xunit;

namespace Rename.Core.Tests;

public class FileNameValidatorTests
{
    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("my photo (1).png")]
    [InlineData("README")]
    [InlineData("naïve_日本.txt")]
    public void ValidNames(string name) => Assert.True(FileNameValidator.IsValid(name));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a:b.txt")]
    [InlineData("a?b.txt")]
    [InlineData("a/b.txt")]
    [InlineData("a\\b.txt")]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("LPT1.jpg")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    public void InvalidNames(string name) => Assert.False(FileNameValidator.IsValid(name));
}
