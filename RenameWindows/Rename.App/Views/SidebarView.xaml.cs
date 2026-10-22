using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Rename.Core.State;

namespace Rename.App.Views;

public partial class SidebarView : UserControl
{
    private AppState? State => DataContext as AppState;

    public SidebarView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AppState old) old.PropertyChanged -= OnStateChanged;
        if (e.NewValue is AppState state) state.PropertyChanged += OnStateChanged;
    }

    // The state clears the filter itself when the filtered extension disappears; keep the "All" row in sync.
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.ExtensionFilter) && State?.ExtensionFilter.Length == 0)
        {
            AllFilter.IsChecked = true;
        }
    }

    private void OnClearAll(object sender, RoutedEventArgs e) => State?.ClearFiles();

    private void OnFilterChecked(object sender, RoutedEventArgs e)
    {
        if (State is null || sender is not RadioButton button) return;
        State.ExtensionFilter = button.Tag as string ?? string.Empty;
    }
}
