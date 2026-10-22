using System.Globalization;
using System.Text;
using Rename.Core.Models;

namespace Rename.Core.Services;

/// <summary>Pure function from (ordered files, rules) to output names. No side effects.</summary>
public static class RenameEngine
{
    public static List<string> Compute(IReadOnlyList<RenameFile> files, IReadOnlyList<RenameRuleItem> rules)
    {
        var enabled = rules.Where(r => r.IsEnabled).Select(r => r.Rule).ToList();
        var result = new List<string>(files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            result.Add(ApplyRules(enabled, files[i], i));
        }
        return result;
    }

    private static string ApplyRules(List<RenameRule> rules, RenameFile file, int index)
    {
        var fileName = file.FileName;
        var originalStem = PathParts.Stem(fileName);
        var ext = PathParts.Extension(fileName);
        var stem = originalStem;
        string? extensionOverride = null;

        foreach (var rule in rules)
        {
            switch (rule)
            {
                case PrefixRule r:
                    stem = r.Text + stem;
                    break;

                case SuffixRule r:
                    stem += r.Text;
                    break;

                case FindReplaceRule r:
                    if (r.Find.Length == 0) break;
                    stem = stem.Replace(r.Find, r.Replace,
                        r.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                    break;

                case RegexFindReplaceRule r:
                    stem = ApplyRegex(r.Pattern, r.Replacement, stem);
                    break;

                case ChangeCaseRule r:
                    stem = ApplyCase(r.Style, stem);
                    break;

                case InsertAtRule r:
                    stem = InsertAt(stem, r.Text, r.Index);
                    break;

                case RemoveRangeRule r:
                    stem = RemoveRange(stem, r.From, r.Count);
                    break;

                case ChangeExtensionRule r:
                    extensionOverride = r.Extension.TrimStart('.');
                    break;

                case NumberSequenceRule r:
                {
                    var padded = Pad(r.Start + index * r.Step, r.Digits);
                    stem = r.Position switch
                    {
                        NumberPosition.Prefix => $"{padded}_{stem}",
                        NumberPosition.Suffix => $"{stem}_{padded}",
                        _ => padded,
                    };
                    break;
                }

                case DateBasedRule r:
                {
                    var date = ResolvedDate(file.OriginalPath, r.Source);
                    stem = $"{FormatDate(date, r.Format)}_{stem}";
                    break;
                }

                case TemplateRule r:
                {
                    var padded = Pad(r.NumberStart + index, Math.Max(1, r.NumberDigits));
                    stem = r.Format
                        .Replace(TemplateRule.NameToken, originalStem, StringComparison.Ordinal)
                        .Replace(TemplateRule.NumberToken, padded, StringComparison.Ordinal);
                    break;
                }
            }
        }

        var finalExt = extensionOverride ?? ext;
        return finalExt.Length == 0 ? stem : $"{stem}.{finalExt}";
    }

    // MARK: Helpers

    private static string Pad(int number, int digits)
        => number.ToString("D" + Math.Clamp(digits, 1, 20), CultureInfo.InvariantCulture);

    private static string ApplyRegex(string pattern, string replacement, string input)
    {
        var regex = RegexFindReplaceRule.TryCompile(pattern);
        if (regex is null) return input;
        try { return regex.Replace(input, replacement); }
        catch (Exception e) when (e is ArgumentException or TimeoutException) { return input; }
    }

    private static string ApplyCase(CaseStyle style, string input) => style switch
    {
        CaseStyle.Lower => input.ToLowerInvariant(),
        CaseStyle.Upper => input.ToUpperInvariant(),
        CaseStyle.Title => Capitalized(input),
        CaseStyle.Camel => CamelCased(input),
        _ => input,
    };

    /// <summary>Matches Foundation's <c>capitalized</c>: first letter of each word upper-cased, the rest lower-cased.
    /// A word starts after any non-letter character ("my_photo" → "My_Photo").</summary>
    public static string Capitalized(string input)
    {
        var sb = new StringBuilder(input.Length);
        var startOfWord = true;
        foreach (var ch in input)
        {
            if (char.IsLetter(ch))
            {
                sb.Append(startOfWord ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch));
                startOfWord = false;
            }
            else
            {
                sb.Append(ch);
                startOfWord = true;
            }
        }
        return sb.ToString();
    }

    private static string CamelCased(string input)
    {
        var words = SplitAlphanumeric(input);
        if (words.Count == 0) return input;
        var sb = new StringBuilder(words[0].ToLowerInvariant());
        foreach (var word in words.Skip(1)) sb.Append(Capitalized(word));
        return sb.ToString();
    }

    private static List<string> SplitAlphanumeric(string input)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var ch in input)
        {
            if (char.IsLetterOrDigit(ch)) current.Append(ch);
            else if (current.Length > 0) { words.Add(current.ToString()); current.Clear(); }
        }
        if (current.Length > 0) words.Add(current.ToString());
        return words;
    }

    // Insert/remove operate on user-perceived characters (grapheme clusters), like Swift's String.
    private static List<string> TextElements(string input)
    {
        var elements = new List<string>(input.Length);
        var e = StringInfo.GetTextElementEnumerator(input);
        while (e.MoveNext()) elements.Add(e.GetTextElement());
        return elements;
    }

    private static string InsertAt(string stem, string text, int index)
    {
        var elements = TextElements(stem);
        var clamped = Math.Clamp(index, 0, elements.Count);
        elements.Insert(clamped, text);
        return string.Concat(elements);
    }

    private static string RemoveRange(string stem, int from, int count)
    {
        var elements = TextElements(stem);
        var start = Math.Clamp(from, 0, elements.Count);
        var end = Math.Min(start + Math.Max(0, count), elements.Count);
        if (start >= end) return stem;
        elements.RemoveRange(start, end - start);
        return string.Concat(elements);
    }

    private static DateTime ResolvedDate(string path, DateSource source)
    {
        try
        {
            return source switch
            {
                DateSource.FileModified => File.GetLastWriteTime(path),
                DateSource.FileCreated => File.GetCreationTime(path),
                _ => DateTime.Now,
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return DateTime.Now;
        }
    }

    /// <summary>Formats with .NET custom date patterns (yyyy, MM, dd, HH, mm, ss …), which match the common ICU patterns the macOS app accepts.</summary>
    public static string FormatDate(DateTime date, string format)
    {
        if (string.IsNullOrEmpty(format)) return string.Empty;
        // A single character would be interpreted as a standard format specifier; force custom-format parsing.
        var custom = format.Length == 1 ? "%" + format : format;
        try { return date.ToString(custom, CultureInfo.InvariantCulture); }
        catch (FormatException) { return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
    }
}
