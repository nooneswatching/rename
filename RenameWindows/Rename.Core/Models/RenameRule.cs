using System.Text.RegularExpressions;

namespace Rename.Core.Models;

public enum NumberPosition { Prefix, Suffix, ReplaceAll }
public enum CaseStyle { Lower, Upper, Title, Camel }
public enum DateSource { FileModified, FileCreated, Today }

/// <summary>User-facing labels for the rule enums (mirrors the raw values of the Swift enums).</summary>
public static class EnumLabels
{
    public static string Label(NumberPosition value) => value switch
    {
        NumberPosition.Prefix => "Prefix",
        NumberPosition.Suffix => "Suffix",
        NumberPosition.ReplaceAll => "Replace All",
        _ => value.ToString(),
    };

    public static string Label(CaseStyle value) => value switch
    {
        CaseStyle.Lower => "lowercase",
        CaseStyle.Upper => "UPPERCASE",
        CaseStyle.Title => "Title Case",
        CaseStyle.Camel => "camelCase",
        _ => value.ToString(),
    };

    public static string Label(DateSource value) => value switch
    {
        DateSource.FileModified => "File Modified",
        DateSource.FileCreated => "File Created",
        DateSource.Today => "Today",
        _ => value.ToString(),
    };

    public static string Label(object? value) => value switch
    {
        NumberPosition p => Label(p),
        CaseStyle c => Label(c),
        DateSource d => Label(d),
        null => string.Empty,
        _ => value.ToString() ?? string.Empty,
    };

    public static readonly NumberPosition[] NumberPositions = Enum.GetValues<NumberPosition>();
    public static readonly CaseStyle[] CaseStyles = Enum.GetValues<CaseStyle>();
    public static readonly DateSource[] DateSources = Enum.GetValues<DateSource>();
}

/// <summary>
/// A single rename rule. Rules are mutable observable objects so rule cards can bind directly to them;
/// any property change bubbles up through <see cref="RenameRuleItem"/> to the app state, which recomputes names.
/// </summary>
public abstract class RenameRule : ObservableObject
{
    public abstract string DisplayName { get; }
}

public sealed class PrefixRule : RenameRule
{
    private string _text;
    public PrefixRule(string text = "") => _text = text;
    public string Text { get => _text; set => SetProperty(ref _text, value); }
    public override string DisplayName => "Prefix";
}

public sealed class SuffixRule : RenameRule
{
    private string _text;
    public SuffixRule(string text = "") => _text = text;
    public string Text { get => _text; set => SetProperty(ref _text, value); }
    public override string DisplayName => "Suffix";
}

public sealed class FindReplaceRule : RenameRule
{
    private string _find, _replace;
    private bool _caseSensitive;
    public FindReplaceRule(string find = "", string replace = "", bool caseSensitive = true)
    { _find = find; _replace = replace; _caseSensitive = caseSensitive; }
    public string Find { get => _find; set => SetProperty(ref _find, value); }
    public string Replace { get => _replace; set => SetProperty(ref _replace, value); }
    public bool CaseSensitive { get => _caseSensitive; set => SetProperty(ref _caseSensitive, value); }
    public override string DisplayName => "Find & Replace";
}

public sealed class RegexFindReplaceRule : RenameRule
{
    private string _pattern, _replacement;
    public RegexFindReplaceRule(string pattern = "", string replacement = "")
    { _pattern = pattern; _replacement = replacement; }
    public string Pattern
    {
        get => _pattern;
        set { if (SetProperty(ref _pattern, value)) OnPropertyChanged(nameof(IsPatternValid)); }
    }
    public string Replacement { get => _replacement; set => SetProperty(ref _replacement, value); }
    /// <summary>True for an empty pattern or one that compiles; drives the inline error in the rule card.</summary>
    public bool IsPatternValid => Pattern.Length == 0 || TryCompile(Pattern) is not null;
    public override string DisplayName => "Regex Find & Replace";

    public static Regex? TryCompile(string pattern)
    {
        try { return new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1)); }
        catch (ArgumentException) { return null; }
    }
}

public sealed class NumberSequenceRule : RenameRule
{
    private int _start, _step, _digits;
    private NumberPosition _position;
    public NumberSequenceRule(int start = 1, int step = 1, int digits = 2, NumberPosition position = NumberPosition.Prefix)
    { _start = start; _step = step; _digits = digits; _position = position; }
    public int Start { get => _start; set => SetProperty(ref _start, value); }
    public int Step { get => _step; set => SetProperty(ref _step, value); }
    public int Digits { get => _digits; set => SetProperty(ref _digits, value); }
    public NumberPosition Position { get => _position; set => SetProperty(ref _position, value); }
    public override string DisplayName => "Numbering";
}

public sealed class ChangeCaseRule : RenameRule
{
    private CaseStyle _style;
    public ChangeCaseRule(CaseStyle style = CaseStyle.Lower) => _style = style;
    public CaseStyle Style { get => _style; set => SetProperty(ref _style, value); }
    public override string DisplayName => "Change Case";
}

public sealed class InsertAtRule : RenameRule
{
    private string _text;
    private int _index;
    public InsertAtRule(string text = "", int index = 0) { _text = text; _index = index; }
    public string Text { get => _text; set => SetProperty(ref _text, value); }
    public int Index { get => _index; set => SetProperty(ref _index, value); }
    public override string DisplayName => "Insert at Position";
}

public sealed class RemoveRangeRule : RenameRule
{
    private int _from, _count;
    public RemoveRangeRule(int from = 0, int count = 1) { _from = from; _count = count; }
    public int From { get => _from; set => SetProperty(ref _from, value); }
    public int Count { get => _count; set => SetProperty(ref _count, value); }
    public override string DisplayName => "Remove Range";
}

public sealed class ChangeExtensionRule : RenameRule
{
    private string _extension;
    public ChangeExtensionRule(string extension = "") => _extension = extension;
    public string Extension { get => _extension; set => SetProperty(ref _extension, value); }
    public override string DisplayName => "Change Extension";
}

public sealed class DateBasedRule : RenameRule
{
    private string _format;
    private DateSource _source;
    public DateBasedRule(string format = "yyyy-MM-dd", DateSource source = DateSource.FileModified)
    { _format = format; _source = source; }
    public string Format { get => _format; set => SetProperty(ref _format, value); }
    public DateSource Source { get => _source; set => SetProperty(ref _source, value); }
    public override string DisplayName => "Date-based Naming";
}

public sealed class TemplateRule : RenameRule
{
    public const string NameToken = "{name}";
    public const string NumberToken = "{n}";

    private string _format;
    private int _numberStart, _numberDigits;
    public TemplateRule(string format = "", int numberStart = 1, int numberDigits = 2)
    { _format = format; _numberStart = numberStart; _numberDigits = numberDigits; }
    public string Format
    {
        get => _format;
        set { if (SetProperty(ref _format, value)) OnPropertyChanged(nameof(HasNumber)); }
    }
    public int NumberStart { get => _numberStart; set => SetProperty(ref _numberStart, value); }
    public int NumberDigits { get => _numberDigits; set => SetProperty(ref _numberDigits, value); }
    public bool HasNumber => Format.Contains(NumberToken, StringComparison.Ordinal);
    public override string DisplayName => "Custom Name";
}

/// <summary>A rule plus its enabled flag, as shown in the rules panel.</summary>
public sealed class RenameRuleItem : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    private RenameRule _rule;
    public RenameRule Rule
    {
        get => _rule;
        set
        {
            if (ReferenceEquals(_rule, value)) return;
            _rule.PropertyChanged -= OnRulePropertyChanged;
            _rule = value;
            _rule.PropertyChanged += OnRulePropertyChanged;
            OnPropertyChanged();
        }
    }

    private bool _isEnabled;
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

    private bool _isExpanded = true;
    /// <summary>Whether the rule card shows its controls. Kept here so it survives card re-creation on reorder.</summary>
    public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }

    public RenameRuleItem(RenameRule rule, bool isEnabled = true)
    {
        _rule = rule;
        _rule.PropertyChanged += OnRulePropertyChanged;
        _isEnabled = isEnabled;
    }

    // Any edit inside the rule is surfaced as a change of the Rule property so one subscription covers everything.
    private void OnRulePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => OnPropertyChanged(nameof(Rule));
}

/// <summary>The "Add Rule" menu entries, in display order.</summary>
public static class RuleCatalog
{
    public sealed record Entry(string Name, Func<RenameRule> Create);

    public static readonly IReadOnlyList<Entry> Templates =
    [
        new("Prefix", () => new PrefixRule()),
        new("Suffix", () => new SuffixRule()),
        new("Find & Replace", () => new FindReplaceRule()),
        new("Regex Find & Replace", () => new RegexFindReplaceRule()),
        new("Numbering", () => new NumberSequenceRule()),
        new("Change Case", () => new ChangeCaseRule()),
        new("Insert at Position", () => new InsertAtRule()),
        new("Remove Range", () => new RemoveRangeRule()),
        new("Change Extension", () => new ChangeExtensionRule()),
        new("Date-based Naming", () => new DateBasedRule()),
        new("Custom Name", () => new TemplateRule()),
    ];
}
