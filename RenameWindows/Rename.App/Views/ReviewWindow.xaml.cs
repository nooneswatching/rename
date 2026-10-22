using System.Windows;
using Rename.Core.State;

namespace Rename.App.Views;

/// <summary>Before/after list. Returns true from ShowDialog when the user confirms; the caller performs the rename.</summary>
public partial class ReviewWindow : Window
{
    public ReviewWindow()
    {
        InitializeComponent();
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppState { HasConflicts: true }) return;
        DialogResult = true;
    }
}
