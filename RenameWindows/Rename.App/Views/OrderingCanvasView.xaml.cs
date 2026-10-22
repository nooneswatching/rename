using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Rename.Core.Models;
using Rename.Core.State;

namespace Rename.App.Views;

/// <summary>
/// The ordering canvas. Files are shown as a wrapping grid of cards or as a list; their order in
/// <see cref="AppState.Files"/> is the visual order and drives numbering. Supports Explorer-style
/// click / Ctrl-click / Shift-click selection, rubber-band selection (grid), drag-to-reorder with an
/// insertion indicator, a context menu and Delete-key removal.
/// </summary>
public partial class OrderingCanvasView : UserControl
{
    private const string FileIdsFormat = "Rename.FileIds";

    public static readonly DependencyProperty ThumbnailSizeProperty = DependencyProperty.Register(
        nameof(ThumbnailSize), typeof(double), typeof(OrderingCanvasView), new PropertyMetadata(80.0));

    /// <summary>Thumbnail height in device-independent pixels; cards are 1.25× as wide.</summary>
    public double ThumbnailSize
    {
        get => (double)GetValue(ThumbnailSizeProperty);
        set => SetValue(ThumbnailSizeProperty, value);
    }

    private AppState? _state;
    private ListCollectionView? _view;

    // Mouse press tracking
    private Grid? _pressHost;
    private Point _pressPoint;
    private RenameFile? _pressFile;
    private bool _deferredSelect;
    private bool _dragStarted;
    private bool _rubberBand;
    private Guid? _anchorId;

    // Drag/drop state
    private HashSet<Guid>? _draggingIds;
    private int? _dropInsertIndex;
    private DragGhostAdorner? _ghost;
    private Vector _ghostGrab;

    public OrderingCanvasView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private bool IsGridMode => GridModeButton.IsChecked == true;

    // MARK: Wiring

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_state is not null) _state.PropertyChanged -= OnStateChanged;
        if (_view is not null) ((INotifyCollectionChanged)_view).CollectionChanged -= OnViewChanged;

        _state = e.NewValue as AppState;
        if (_state is null) return;

        _view = new ListCollectionView(_state.Files) { Filter = o => o is RenameFile f && _state.IsVisible(f) };
        ((INotifyCollectionChanged)_view).CollectionChanged += OnViewChanged;
        GridItems.ItemsSource = _view;
        ListItems.ItemsSource = _view;
        _state.PropertyChanged += OnStateChanged;
        ApplyMode();
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.ExtensionFilter))
        {
            _view?.Refresh();
            ApplyMode();
        }
    }

    private void OnViewChanged(object? sender, NotifyCollectionChangedEventArgs e) => ApplyMode();

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded && GridScroll is null) return;
        ApplyMode();
    }

    private void ApplyMode()
    {
        if (GridScroll is null || ListScroll is null) return;
        var empty = _view?.IsEmpty ?? true;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        GridScroll.Visibility = !empty && IsGridMode ? Visibility.Visible : Visibility.Collapsed;
        ListScroll.Visibility = !empty && !IsGridMode ? Visibility.Visible : Visibility.Collapsed;
        SizeControls.Visibility = IsGridMode ? Visibility.Visible : Visibility.Collapsed;
        ClearIndicator();
    }

    private ItemsControl ItemsFor(Grid host) => ReferenceEquals(host, GridHost) ? GridItems : ListItems;
    private Canvas OverlayFor(Grid host) => ReferenceEquals(host, GridHost) ? GridOverlay : ListOverlay;
    private ScrollViewer ScrollFor(Grid host) => ReferenceEquals(host, GridHost) ? GridScroll : ListScroll;

    private IReadOnlyList<RenameFile> VisibleFiles => _view?.Cast<RenameFile>().ToList() ?? [];

    private Brush AccentBrush => TryFindResource("AccentFillColorDefaultBrush") as Brush ?? SystemColors.HighlightBrush;

    // MARK: Hit testing

    private RenameFile? FileAt(Grid host, Point position)
    {
        var hit = VisualTreeHelper.HitTest(host, position)?.VisualHit;
        if (hit is null) return null;
        var items = ItemsFor(host);
        var container = ItemsControl.ContainerFromElement(items, hit);
        return container is null ? null : items.ItemContainerGenerator.ItemFromContainer(container) as RenameFile;
    }

    private static FrameworkElement? ContainerOf(ItemsControl items, RenameFile file)
        => items.ItemContainerGenerator.ContainerFromItem(file) as FrameworkElement;

    private static Rect BoundsIn(FrameworkElement element, Visual ancestor)
        => element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

    // MARK: Selection

    private void OnHostMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_state is null || sender is not Grid host) return;
        Focus();

        _pressHost = host;
        _pressPoint = e.GetPosition(host);
        _pressFile = FileAt(host, _pressPoint);
        _deferredSelect = false;
        _dragStarted = false;
        _rubberBand = false;

        if (_pressFile is null) return;   // empty space: decide on mouse-up (clear) or on move (rubber band)

        var modifiers = Keyboard.Modifiers;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            _state.ToggleSelection(_pressFile.Id);
            _anchorId = _pressFile.Id;
        }
        else if (modifiers.HasFlag(ModifierKeys.Shift) && _anchorId is { } anchor)
        {
            var ids = VisibleFiles.Select(f => f.Id).ToList();
            var a = ids.IndexOf(anchor);
            var b = ids.IndexOf(_pressFile.Id);
            if (a >= 0 && b >= 0)
            {
                var (lo, hi) = a <= b ? (a, b) : (b, a);
                _state.AddToSelection(ids.Skip(lo).Take(hi - lo + 1));
            }
            else
            {
                _state.Select(_pressFile.Id);
                _anchorId = _pressFile.Id;
            }
        }
        else if (!_pressFile.IsSelected)
        {
            _state.Select(_pressFile.Id);
            _anchorId = _pressFile.Id;
        }
        else
        {
            // Clicking an already-selected file: keep the multi-selection for a possible drag,
            // collapse to this file only if the press ends without dragging.
            _deferredSelect = true;
        }
    }

    private void OnHostMouseMove(object sender, MouseEventArgs e)
    {
        if (_state is null || sender is not Grid host || !ReferenceEquals(host, _pressHost)) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var position = e.GetPosition(host);
        if (_rubberBand)
        {
            UpdateRubberBand(host, position);
            return;
        }
        if (_dragStarted) return;

        var delta = position - _pressPoint;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _dragStarted = true;
        if (_pressFile is not null)
        {
            StartItemDrag(host, _pressFile);
        }
        else if (ReferenceEquals(host, GridHost))
        {
            _rubberBand = true;
            host.CaptureMouse();
            UpdateRubberBand(host, position);
        }
    }

    private void OnHostMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_state is null || sender is not Grid host || !ReferenceEquals(host, _pressHost)) return;

        if (_rubberBand)
        {
            EndRubberBand(host);
        }
        else if (!_dragStarted)
        {
            if (_pressFile is not null && _deferredSelect)
            {
                _state.Select(_pressFile.Id);
                _anchorId = _pressFile.Id;
            }
            else if (_pressFile is null)
            {
                _state.ClearSelection();
            }
        }
        ResetPress();
    }

    private void OnHostLostCapture(object sender, MouseEventArgs e)
    {
        if (_rubberBand && sender is Grid host) EndRubberBand(host);
    }

    private void ResetPress()
    {
        _pressHost = null;
        _pressFile = null;
        _deferredSelect = false;
        _dragStarted = false;
        _rubberBand = false;
    }

    // MARK: Rubber-band selection (grid)

    private void UpdateRubberBand(Grid host, Point position)
    {
        if (_state is null) return;
        var rect = new Rect(_pressPoint, position);
        var overlay = OverlayFor(host);
        overlay.Children.Clear();

        var fill = AccentBrush.Clone();
        fill.Opacity = 0.15;
        var stroke = AccentBrush.Clone();
        stroke.Opacity = 0.5;
        var marquee = new Rectangle { Width = rect.Width, Height = rect.Height, Fill = fill, Stroke = stroke, StrokeThickness = 1 };
        Canvas.SetLeft(marquee, rect.X);
        Canvas.SetTop(marquee, rect.Y);
        overlay.Children.Add(marquee);

        var items = ItemsFor(host);
        var hits = new List<Guid>();
        foreach (var file in VisibleFiles)
        {
            if (ContainerOf(items, file) is { } container && BoundsIn(container, host).IntersectsWith(rect))
            {
                hits.Add(file.Id);
            }
        }
        _state.SetSelection(hits);
    }

    private void EndRubberBand(Grid host)
    {
        _rubberBand = false;
        OverlayFor(host).Children.Clear();
        if (host.IsMouseCaptured) host.ReleaseMouseCapture();
    }

    // MARK: Drag to reorder

    private void StartItemDrag(Grid host, RenameFile file)
    {
        if (_state is null) return;
        var items = ItemsFor(host);

        var ids = new HashSet<Guid>(_state.SelectedFileIds) { file.Id };
        _state.SetSelection(ids);
        _draggingIds = ids;

        // Snapshot the pressed card for the drag ghost before the originals are dimmed.
        if (ContainerOf(items, file) is { } source && AdornerLayer.GetAdornerLayer(this) is { } layer)
        {
            var bounds = BoundsIn(source, host);
            _ghostGrab = _pressPoint - bounds.TopLeft;
            _ghost = new DragGhostAdorner(this, Snapshot(source), source.RenderSize, ids.Count, AccentBrush);
            layer.Add(_ghost);
        }

        foreach (var f in _state.Files)
        {
            if (ids.Contains(f.Id) && ContainerOf(items, f) is { } container) container.Opacity = 0.4;
        }

        try
        {
            var data = new DataObject(FileIdsFormat, string.Join(",", ids));
            DragDrop.DoDragDrop(host, data, DragDropEffects.Move);
        }
        finally
        {
            foreach (var f in _state.Files)
            {
                if (ContainerOf(items, f) is { } container) container.Opacity = 1.0;
            }
            if (_ghost is not null)
            {
                AdornerLayer.GetAdornerLayer(this)?.Remove(_ghost);
                _ghost = null;
            }
            _draggingIds = null;
            ClearIndicator();
            ResetPress();
        }
    }

    private ImageSource Snapshot(FrameworkElement source)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var size = source.RenderSize;
        var width = Math.Max(1, (int)Math.Ceiling(size.Width * dpi.DpiScaleX));
        var height = Math.Max(1, (int)Math.Ceiling(size.Height * dpi.DpiScaleY));
        var target = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new VisualBrush(source), null, new Rect(size));
        }
        target.Render(visual);
        target.Freeze();
        return target;
    }

    private void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        if (_ghost is null) return;
        if (GetCursorPos(out var cursor))
        {
            var local = PointFromScreen(new Point(cursor.X, cursor.Y));
            _ghost.Offset = local - _ghostGrab;
            _ghost.InvalidateVisual();
        }
        e.UseDefaultCursors = true;
        e.Handled = true;
    }

    private void OnHostDragOver(object sender, DragEventArgs e)
    {
        // Files dragged in from Explorer are handled by the window; only our own reorder payload is handled here.
        if (_state is null || sender is not Grid host || !e.Data.GetDataPresent(FileIdsFormat)) return;
        e.Handled = true;

        var scroll = ScrollFor(host);
        AutoScroll(scroll, e.GetPosition(scroll));

        var position = e.GetPosition(host);
        var target = FileAt(host, position);
        var items = ItemsFor(host);

        if (target is not null)
        {
            if (_draggingIds?.Contains(target.Id) == true)
            {
                e.Effects = DragDropEffects.None;
                ClearIndicator();
                return;
            }
            if (ContainerOf(items, target) is { } container)
            {
                var bounds = BoundsIn(container, host);
                var before = IsGridMode ? position.X < bounds.Left + bounds.Width / 2 : position.Y < bounds.Top + bounds.Height / 2;
                var index = _state.Files.IndexOf(target);
                _dropInsertIndex = before ? index : index + 1;
                ShowIndicator(host, bounds, before);
            }
        }
        else if (_dropInsertIndex is null && VisibleFiles.LastOrDefault() is { } last && ContainerOf(items, last) is { } lastContainer)
        {
            // Empty space with no target yet: append after the last visible file.
            _dropInsertIndex = _state.Files.IndexOf(last) + 1;
            ShowIndicator(host, BoundsIn(lastContainer, host), before: false);
        }
        e.Effects = DragDropEffects.Move;
    }

    private void OnHostDragLeave(object sender, DragEventArgs e)
    {
        if (sender is not Grid host) return;
        var position = e.GetPosition(host);
        if (!new Rect(host.RenderSize).Contains(position)) ClearIndicator();
    }

    private void OnHostDrop(object sender, DragEventArgs e)
    {
        if (_state is null || sender is not Grid || !e.Data.GetDataPresent(FileIdsFormat)) return;
        e.Handled = true;

        var insertAt = _dropInsertIndex ?? _state.Files.Count;
        ClearIndicator();

        if (e.Data.GetData(FileIdsFormat) is not string payload) return;
        var ids = payload.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var fromIndices = _state.Files.Select((f, i) => (f, i)).Where(t => ids.Contains(t.f.Id)).Select(t => t.i).ToList();
        if (fromIndices.Count == 0) return;
        _state.MoveFiles(fromIndices, insertAt);
    }

    private static void AutoScroll(ScrollViewer scroll, Point position)
    {
        const double edge = 28;
        const double step = 12;
        if (position.Y < edge) scroll.ScrollToVerticalOffset(scroll.VerticalOffset - step);
        else if (position.Y > scroll.ViewportHeight - edge) scroll.ScrollToVerticalOffset(scroll.VerticalOffset + step);
    }

    // MARK: Insertion indicator

    private void ShowIndicator(Grid host, Rect bounds, bool before)
    {
        var overlay = OverlayFor(host);
        overlay.Children.Clear();
        var bar = new Rectangle { Fill = AccentBrush, RadiusX = 1, RadiusY = 1 };
        if (IsGridMode)
        {
            bar.Width = 3;
            bar.Height = Math.Max(0, bounds.Height - 12);
            Canvas.SetLeft(bar, before ? bounds.Left - 5 : bounds.Right + 2);
            Canvas.SetTop(bar, bounds.Top + 6);
        }
        else
        {
            bar.Height = 3;
            bar.Width = Math.Max(0, bounds.Width - 16);
            Canvas.SetLeft(bar, bounds.Left + 8);
            Canvas.SetTop(bar, before ? bounds.Top - 2 : bounds.Bottom - 1);
        }
        overlay.Children.Add(bar);
    }

    private void ClearIndicator()
    {
        _dropInsertIndex = null;
        if (!_rubberBand)
        {
            GridOverlay?.Children.Clear();
            ListOverlay?.Children.Clear();
        }
    }

    // MARK: Removal, context menu, keyboard

    private void OnRemoveSelected(object sender, RoutedEventArgs e) => RemoveSelected();

    private void RemoveSelected()
    {
        if (_state is null) return;
        var visibleSelected = VisibleFiles.Where(f => f.IsSelected).Select(f => f.Id).ToList();
        if (visibleSelected.Count == 0) return;
        _state.RemoveFiles(visibleSelected);
        _anchorId = null;
    }

    private void OnHostRightClick(object sender, MouseButtonEventArgs e)
    {
        if (_state is null || sender is not Grid host) return;
        var file = FileAt(host, e.GetPosition(host));
        if (file is null) return;
        e.Handled = true;

        var targets = file.IsSelected ? _state.SelectedFileIds.ToList() : [file.Id];
        var label = targets.Count > 1 ? $"Remove {targets.Count} Files" : "Remove from List";
        var item = new MenuItem
        {
            Header = label,
            Icon = new TextBlock { Text = "", FontFamily = (FontFamily)FindResource("IconFont"), FontSize = 14 },
        };
        item.Click += (_, _) => { _state.RemoveFiles(targets); _anchorId = null; };

        var menu = new ContextMenu { PlacementTarget = host, Placement = PlacementMode.MousePoint };
        menu.Items.Add(item);
        menu.IsOpen = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_state is null || e.OriginalSource is TextBoxBase) return;

        if ((e.Key == Key.Delete || e.Key == Key.Back) && _state.SelectionCount > 0)
        {
            RemoveSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _state.SetSelection(VisibleFiles.Select(f => f.Id));
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _state.SelectionCount > 0)
        {
            _state.ClearSelection();
            e.Handled = true;
        }
    }

    // MARK: Interop

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT point);
}

/// <summary>Floating snapshot of the dragged card, with a count badge when several files move together.</summary>
internal sealed class DragGhostAdorner : Adorner
{
    private readonly ImageSource _image;
    private readonly Size _size;
    private readonly int _count;
    private readonly Brush _accent;

    public Point Offset { get; set; }

    public DragGhostAdorner(UIElement adornedElement, ImageSource image, Size size, int count, Brush accent)
        : base(adornedElement)
    {
        _image = image;
        _size = size;
        _count = count;
        _accent = accent;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var rect = new Rect(Offset, _size);
        dc.PushOpacity(0.85);
        dc.DrawImage(_image, rect);
        dc.Pop();

        if (_count <= 1) return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var text = new FormattedText(_count.ToString(), System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brushes.White, dpi) { TextAlignment = TextAlignment.Center };
        var badgeWidth = Math.Max(22, text.Width + 12);
        var badge = new Rect(rect.Right - badgeWidth + 6, rect.Top - 6, badgeWidth, 22);
        dc.DrawRoundedRectangle(_accent, null, badge, 11, 11);
        dc.DrawText(text, new Point(badge.X + badge.Width / 2, badge.Y + (badge.Height - text.Height) / 2));
    }
}
