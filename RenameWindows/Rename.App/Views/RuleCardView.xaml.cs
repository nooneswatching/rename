using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Rename.Core.Models;

namespace Rename.App.Views;

/// <summary>One rule in the rules panel. DataContext is a <see cref="RenameRuleItem"/>.</summary>
public partial class RuleCardView : UserControl
{
    public static readonly RoutedEvent RemoveRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(RemoveRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(RuleCardView));

    public static readonly RoutedEvent DragHandlePressedEvent = EventManager.RegisterRoutedEvent(
        nameof(DragHandlePressed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(RuleCardView));

    public event RoutedEventHandler RemoveRequested
    {
        add => AddHandler(RemoveRequestedEvent, value);
        remove => RemoveHandler(RemoveRequestedEvent, value);
    }

    /// <summary>Raised on mouse-down over the grip; the panel turns the following mouse move into a drag.</summary>
    public event RoutedEventHandler DragHandlePressed
    {
        add => AddHandler(DragHandlePressedEvent, value);
        remove => RemoveHandler(DragHandlePressedEvent, value);
    }

    public RuleCardView()
    {
        InitializeComponent();
    }

    private RenameRuleItem? Item => DataContext as RenameRuleItem;

    private void OnDelete(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RemoveRequestedEvent));

    private void OnGripDown(object sender, MouseButtonEventArgs e) => RaiseEvent(new RoutedEventArgs(DragHandlePressedEvent));

    private void OnToggleExpanded(object sender, RoutedEventArgs e)
    {
        if (Item is { } item) item.IsExpanded = !item.IsExpanded;
    }

    private void OnInsertNameToken(object sender, RoutedEventArgs e) => AppendToken(sender, TemplateRule.NameToken);

    private void OnInsertNumberToken(object sender, RoutedEventArgs e) => AppendToken(sender, TemplateRule.NumberToken);

    private static void AppendToken(object sender, string token)
    {
        if (sender is FrameworkElement { DataContext: TemplateRule rule }) rule.Format += token;
    }
}
