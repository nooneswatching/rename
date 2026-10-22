using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Rename.Core.State;

namespace Rename.App.Views;

public partial class PreviewBarView : UserControl
{
    public static readonly RoutedEvent ApplyRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(ApplyRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PreviewBarView));

    /// <summary>Raised when the user presses Apply; the window opens the review sheet.</summary>
    public event RoutedEventHandler ApplyRequested
    {
        add => AddHandler(ApplyRequestedEvent, value);
        remove => RemoveHandler(ApplyRequestedEvent, value);
    }

    public PreviewBarView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AppState old) old.PropertyChanged -= OnStateChanged;
        if (e.NewValue is AppState state)
        {
            state.PropertyChanged += OnStateChanged;
            UpdateToolTip(state);
        }
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.HasConflicts) or nameof(AppState.CanApply) && sender is AppState state) UpdateToolTip(state);
    }

    private void UpdateToolTip(AppState state)
    {
        ApplyButton.ToolTip = state.HasConflicts ? "Resolve naming conflicts before applying"
            : state.HasFiles ? "Review and apply the new names (Ctrl+Enter)"
            : null;
    }

    private void OnApply(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ApplyRequestedEvent));
}
