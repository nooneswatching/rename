using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Rename.Core.Models;
using Rename.Core.State;

namespace Rename.App.Views;

/// <summary>Stacked rule cards applied top to bottom. Cards are reordered by dragging their grip.</summary>
public partial class RulesPanelView : UserControl
{
    private const string RuleIdFormat = "Rename.RuleId";

    private AppState? State => DataContext as AppState;

    private RenameRuleItem? _pressItem;
    private RuleCardView? _pressCard;
    private Point _pressPoint;
    private int? _dropIndex;

    public RulesPanelView()
    {
        InitializeComponent();
    }

    private Brush AccentBrush => TryFindResource("AccentFillColorDefaultBrush") as Brush ?? SystemColors.HighlightBrush;

    // MARK: Add / remove

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (AddButton.ContextMenu is not { } menu) return;
        menu.PlacementTarget = AddButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnAddRuleClick(object sender, RoutedEventArgs e)
    {
        if (State is null || e.OriginalSource is not MenuItem { DataContext: RuleCatalog.Entry entry }) return;
        State.AddRule(entry.Create());
        RulesScroll.ScrollToBottom();
    }

    private void OnRemoveRule(object sender, RoutedEventArgs e)
    {
        if (State is null || sender is not RuleCardView { DataContext: RenameRuleItem item }) return;
        State.RemoveRule(item.Id);
    }

    // MARK: Drag to reorder

    private void OnRuleGripPressed(object sender, RoutedEventArgs e)
    {
        if (sender is not RuleCardView { DataContext: RenameRuleItem item } card) return;
        _pressItem = item;
        _pressCard = card;
        _pressPoint = Mouse.GetPosition(RulesHost);
    }

    private void OnHostMouseMove(object sender, MouseEventArgs e)
    {
        if (State is null || _pressItem is null || e.LeftButton != MouseButtonState.Pressed) return;
        var delta = e.GetPosition(RulesHost) - _pressPoint;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var item = _pressItem;
        var card = _pressCard;
        _pressItem = null;
        _pressCard = null;

        if (card is not null) card.Opacity = 0.4;
        try
        {
            DragDrop.DoDragDrop(RulesHost, new DataObject(RuleIdFormat, item.Id.ToString()), DragDropEffects.Move);
        }
        finally
        {
            if (card is not null) card.Opacity = 1.0;
            ClearIndicator();
        }
    }

    private void OnHostMouseUp(object sender, MouseButtonEventArgs e)
    {
        _pressItem = null;
        _pressCard = null;
    }

    private void OnHostDragOver(object sender, DragEventArgs e)
    {
        if (State is null || !e.Data.GetDataPresent(RuleIdFormat)) return;
        e.Handled = true;
        e.Effects = DragDropEffects.Move;

        var position = e.GetPosition(RulesHost);
        var hit = VisualTreeHelper.HitTest(RulesHost, position)?.VisualHit;
        var container = hit is null ? null : ItemsControl.ContainerFromElement(RulesItems, hit) as FrameworkElement;
        if (container is null || RulesItems.ItemContainerGenerator.ItemFromContainer(container) is not RenameRuleItem target)
        {
            if (_dropIndex is null && State.Rules.Count > 0 &&
                RulesItems.ItemContainerGenerator.ContainerFromIndex(State.Rules.Count - 1) is FrameworkElement last)
            {
                _dropIndex = State.Rules.Count;
                ShowIndicator(Bounds(last), before: false);
            }
            return;
        }

        var bounds = Bounds(container);
        var before = position.Y < bounds.Top + bounds.Height / 2;
        var index = State.Rules.IndexOf(target);
        _dropIndex = before ? index : index + 1;
        ShowIndicator(bounds, before);
    }

    private void OnHostDragLeave(object sender, DragEventArgs e)
    {
        if (!new Rect(RulesHost.RenderSize).Contains(e.GetPosition(RulesHost))) ClearIndicator();
    }

    private void OnHostDrop(object sender, DragEventArgs e)
    {
        if (State is null || !e.Data.GetDataPresent(RuleIdFormat)) return;
        e.Handled = true;
        var insertAt = _dropIndex ?? State.Rules.Count;
        ClearIndicator();

        if (e.Data.GetData(RuleIdFormat) is not string text || !Guid.TryParse(text, out var id)) return;
        var from = State.Rules.ToList().FindIndex(r => r.Id == id);
        if (from < 0) return;
        State.MoveRules([from], insertAt);
    }

    private Rect Bounds(FrameworkElement element)
        => element.TransformToAncestor(RulesHost).TransformBounds(new Rect(element.RenderSize));

    private void ShowIndicator(Rect bounds, bool before)
    {
        RulesOverlay.Children.Clear();
        var bar = new Rectangle
        {
            Fill = AccentBrush,
            RadiusX = 1,
            RadiusY = 1,
            Height = 3,
            Width = Math.Max(0, bounds.Width),
        };
        Canvas.SetLeft(bar, bounds.Left);
        Canvas.SetTop(bar, before ? bounds.Top - 4 : bounds.Bottom + 1);
        RulesOverlay.Children.Add(bar);
    }

    private void ClearIndicator()
    {
        _dropIndex = null;
        RulesOverlay.Children.Clear();
    }
}
