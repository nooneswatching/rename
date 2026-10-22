using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Rename.App.Views;
using Rename.Core.State;

namespace Rename.App;

public partial class MainWindow : Window
{
    private AppState State => (AppState)DataContext;
    private AppState? StateOrNull => DataContext as AppState;

    public RelayCommand UndoCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand AddFilesCommand { get; }
    public RelayCommand ApplyCommand { get; }

    public MainWindow()
    {
        UndoCommand = new RelayCommand(() => StateOrNull?.PerformUndo(), () => StateOrNull?.CanUndo == true);
        OpenFolderCommand = new RelayCommand(() => StateOrNull?.OpenFolderPicker());
        AddFilesCommand = new RelayCommand(() => StateOrNull?.OpenFilePicker());
        ApplyCommand = new RelayCommand(ShowReview, () => StateOrNull?.CanApply == true);
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Keyboard shortcuts (InputBindings cannot bind to window properties from XAML).
        InputBindings.Add(new KeyBinding(UndoCommand, Key.Z, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(OpenFolderCommand, Key.O, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(AddFilesCommand, Key.O, ModifierKeys.Control | ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(ApplyCommand, Key.Return, ModifierKeys.Control));
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AppState old) old.PropertyChanged -= OnStateChanged;
        if (e.NewValue is AppState state) state.PropertyChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppState.ErrorMessage) when State.ErrorMessage is { } message:
                MessageBox.Show(this, message, "Rename", MessageBoxButton.OK, MessageBoxImage.Error);
                State.ErrorMessage = null;
                break;
            case nameof(AppState.CanUndo):
                UndoCommand.RaiseCanExecuteChanged();
                break;
            case nameof(AppState.CanApply):
                ApplyCommand.RaiseCanExecuteChanged();
                break;
        }
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e) => State.OpenFolderPicker();
    private void OnAddFiles(object sender, RoutedEventArgs e) => State.OpenFilePicker();
    private void OnUndo(object sender, RoutedEventArgs e) => State.PerformUndo();
    private void OnApplyRequested(object sender, RoutedEventArgs e) => ShowReview();

    private void ShowReview()
    {
        if (!State.CanApply) return;
        var review = new ReviewWindow { Owner = this, DataContext = State };
        if (review.ShowDialog() == true) State.ApplyRename();
    }

    // MARK: Dropping files or folders from Explorer anywhere on the window

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        e.Handled = true;
        foreach (var path in paths)
        {
            if (File.Exists(path) || Directory.Exists(path)) State.HandleDroppedPath(path);
        }
    }
}
