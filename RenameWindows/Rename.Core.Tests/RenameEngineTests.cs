using Rename.Core.Models;
using Rename.Core.Services;
using Xunit;

namespace Rename.Core.Tests;

public class RenameEngineTests
{
    private static RenameFile File_(string name) => new(Path.Combine(Path.GetTempPath(), name));
    private static RenameRuleItem Item(RenameRule rule, bool enabled = true) => new(rule, enabled);
    private static List<string> Compute(RenameFile[] files, params RenameRuleItem[] rules) => RenameEngine.Compute(files, rules);

    // Prefix
    [Fact] public void Prefix_PrependsToStem() => Assert.Equal(["IMG_photo.jpg"], Compute([File_("photo.jpg")], Item(new PrefixRule("IMG_"))));
    [Fact] public void Prefix_EmptyStringIsNoOp() => Assert.Equal(["photo.jpg"], Compute([File_("photo.jpg")], Item(new PrefixRule(""))));

    // Suffix
    [Fact] public void Suffix_AppendsToStemBeforeExtension() => Assert.Equal(["photo_edited.jpg"], Compute([File_("photo.jpg")], Item(new SuffixRule("_edited"))));
    [Fact] public void Suffix_NoExtension() => Assert.Equal(["README_v2"], Compute([File_("README")], Item(new SuffixRule("_v2"))));

    // Find & Replace
    [Fact] public void FindReplace_ReplacesOccurrences() => Assert.Equal(["my_photo.jpg"], Compute([File_("my photo.jpg")], Item(new FindReplaceRule(" ", "_", true))));
    [Fact] public void FindReplace_CaseInsensitive() => Assert.Equal(["img.jpg"], Compute([File_("Photo.jpg")], Item(new FindReplaceRule("photo", "img", false))));
    [Fact] public void FindReplace_CaseSensitiveNoMatch() => Assert.Equal(["Photo.jpg"], Compute([File_("Photo.jpg")], Item(new FindReplaceRule("photo", "img", true))));
    [Fact] public void FindReplace_EmptyFindIsNoOp() => Assert.Equal(["Photo.jpg"], Compute([File_("Photo.jpg")], Item(new FindReplaceRule("", "x", true))));

    // Regex
    [Fact] public void Regex_Basic() => Assert.Equal(["IMG_999.jpg"], Compute([File_("IMG_001.jpg")], Item(new RegexFindReplaceRule(@"\d+", "999"))));
    [Fact] public void Regex_CaptureGroup() => Assert.Equal(["[2024]-photo.jpg"], Compute([File_("2024_photo.jpg")], Item(new RegexFindReplaceRule(@"(\d{4})_", "[$1]-"))));
    [Fact] public void Regex_InvalidPatternLeavesNameUnchanged() => Assert.Equal(["photo.jpg"], Compute([File_("photo.jpg")], Item(new RegexFindReplaceRule("[invalid", "x"))));
    [Fact] public void Regex_InvalidPatternIsFlagged() { Assert.False(new RegexFindReplaceRule("[invalid").IsPatternValid); Assert.True(new RegexFindReplaceRule("").IsPatternValid); Assert.True(new RegexFindReplaceRule(@"\d+").IsPatternValid); }

    // Change Case
    [Fact] public void ChangeCase_Lower() => Assert.Equal(["myphoto.jpg"], Compute([File_("MyPHOTO.jpg")], Item(new ChangeCaseRule(CaseStyle.Lower))));
    [Fact] public void ChangeCase_Upper() => Assert.Equal(["MY_PHOTO.jpg"], Compute([File_("my_photo.jpg")], Item(new ChangeCaseRule(CaseStyle.Upper))));
    [Fact] public void ChangeCase_Title() => Assert.Equal(["My Photo.jpg"], Compute([File_("my photo.jpg")], Item(new ChangeCaseRule(CaseStyle.Title))));
    [Fact] public void ChangeCase_TitleLowersRestOfWord() => Assert.Equal(["My_Photo.jpg"], Compute([File_("mY_PHOTO.jpg")], Item(new ChangeCaseRule(CaseStyle.Title))));
    [Fact] public void ChangeCase_Camel() => Assert.Equal(["myPhotoFile.jpg"], Compute([File_("my photo file.jpg")], Item(new ChangeCaseRule(CaseStyle.Camel))));

    // Insert at
    [Fact] public void InsertAt_Start() => Assert.Equal(["IMG_photo.jpg"], Compute([File_("photo.jpg")], Item(new InsertAtRule("IMG_", 0))));
    [Fact] public void InsertAt_Mid() => Assert.Equal(["photo_edit.jpg"], Compute([File_("photo.jpg")], Item(new InsertAtRule("_edit", 5))));
    [Fact] public void InsertAt_BeyondEndAppends() => Assert.Equal(["abc_X.jpg"], Compute([File_("abc.jpg")], Item(new InsertAtRule("_X", 100))));
    [Fact] public void InsertAt_CountsGraphemesNotCodeUnits() => Assert.Equal(["👍🏽_x.txt"], Compute([File_("👍🏽x.txt")], Item(new InsertAtRule("_", 1))));

    // Remove range
    [Fact] public void RemoveRange_Removes() => Assert.Equal(["photo.jpg"], Compute([File_("IMG_photo.jpg")], Item(new RemoveRangeRule(0, 4))));
    [Fact] public void RemoveRange_Clamps() => Assert.Equal(["a.jpg"], Compute([File_("abc.jpg")], Item(new RemoveRangeRule(1, 100))));
    [Fact] public void RemoveRange_ZeroCountIsNoOp() => Assert.Equal(["photo.jpg"], Compute([File_("photo.jpg")], Item(new RemoveRangeRule(0, 0))));

    // Change extension
    [Fact] public void ChangeExtension_Replaces() => Assert.Equal(["photo.png"], Compute([File_("photo.jpg")], Item(new ChangeExtensionRule("png"))));
    [Fact] public void ChangeExtension_AddsToNoExtFile() => Assert.Equal(["README.md"], Compute([File_("README")], Item(new ChangeExtensionRule("md"))));
    [Fact] public void ChangeExtension_EmptyRemovesExtension() => Assert.Equal(["photo"], Compute([File_("photo.jpg")], Item(new ChangeExtensionRule(""))));
    [Fact] public void ChangeExtension_LeadingDotIgnored() => Assert.Equal(["photo.png"], Compute([File_("photo.jpg")], Item(new ChangeExtensionRule(".png"))));

    // Disabled / ordering / multiple
    [Fact] public void DisabledRule_IsSkipped() => Assert.Equal(["photo.jpg"], Compute([File_("photo.jpg")], Item(new PrefixRule("IMG_"), enabled: false)));
    [Fact] public void RulesApplyInOrder() => Assert.Equal(["IMG_PHOTO.jpg"], Compute([File_("photo.jpg")], Item(new PrefixRule("img_")), Item(new ChangeCaseRule(CaseStyle.Upper))));
    [Fact] public void MultipleFiles_AllProcessed() => Assert.Equal(["X_a.jpg", "X_b.jpg", "X_c.jpg"], Compute([File_("a.jpg"), File_("b.jpg"), File_("c.jpg")], Item(new PrefixRule("X_"))));

    // Numbering
    [Fact] public void Numbering_PrefixSequence() => Assert.Equal(["01_a.jpg", "02_b.jpg", "03_c.jpg"], Compute([File_("a.jpg"), File_("b.jpg"), File_("c.jpg")], Item(new NumberSequenceRule(1, 1, 2, NumberPosition.Prefix))));
    [Fact] public void Numbering_SuffixSequence() => Assert.Equal(["a_010.jpg", "b_015.jpg"], Compute([File_("a.jpg"), File_("b.jpg")], Item(new NumberSequenceRule(10, 5, 3, NumberPosition.Suffix))));
    [Fact] public void Numbering_ReplaceAll() => Assert.Equal(["1.jpg", "2.jpg", "3.jpg"], Compute([File_("a.jpg"), File_("b.jpg"), File_("c.jpg")], Item(new NumberSequenceRule(1, 1, 1, NumberPosition.ReplaceAll))));
    [Fact] public void Numbering_ZeroPadded()
    {
        var files = Enumerable.Range(1, 12).Select(i => File_($"{i}.jpg")).ToArray();
        var result = Compute(files, Item(new NumberSequenceRule(1, 1, 3, NumberPosition.ReplaceAll)));
        Assert.Equal("001.jpg", result.First());
        Assert.Equal("012.jpg", result.Last());
    }
    [Fact] public void Numbering_StepGreaterThanOne() => Assert.Equal(["00_x.jpg", "10_y.jpg", "20_z.jpg"], Compute([File_("x.jpg"), File_("y.jpg"), File_("z.jpg")], Item(new NumberSequenceRule(0, 10, 2, NumberPosition.Prefix))));
    [Fact] public void Numbering_FollowsVisualOrder()
    {
        var a = File_("a.jpg"); var b = File_("b.jpg");
        Assert.Equal(["1_b.jpg", "2_a.jpg"], Compute([b, a], Item(new NumberSequenceRule(1, 1, 1, NumberPosition.Prefix))));
    }

    // Date
    [Fact] public void DateBased_Today_PrependsDateToStem()
    {
        var result = Compute([File_("photo.jpg")], Item(new DateBasedRule("yyyy", DateSource.Today)));
        Assert.Equal([$"{DateTime.Now.Year}_photo.jpg"], result);
    }
    [Fact] public void DateBased_SingleCharFormatIsCustom() => Assert.Equal("7", RenameEngine.FormatDate(new DateTime(2026, 7, 4), "M"));

    // Template
    [Fact] public void Template_NameAndNumberTokens()
    {
        var result = Compute([File_("a.jpg"), File_("b.jpg")], Item(new TemplateRule("{name}_{n}", 1, 3)));
        Assert.Equal(["a_001.jpg", "b_002.jpg"], result);
    }
    [Fact] public void Template_IgnoresEarlierStemChanges()
    {
        var result = Compute([File_("a.jpg")], Item(new PrefixRule("X")), Item(new TemplateRule("{name}", 1, 2)));
        Assert.Equal(["a.jpg"], result);
    }

    // Stem/extension edge cases
    [Fact] public void LeadingDotFile_IsTreatedAsStem() => Assert.Equal(["X.gitignore"], Compute([File_(".gitignore")], Item(new PrefixRule("X"))));
    [Fact] public void MultiDotName_KeepsInnerDots() => Assert.Equal(["X_archive.tar.gz"], Compute([File_("archive.tar.gz")], Item(new PrefixRule("X_"))));
}
