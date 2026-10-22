using System.IO;
using System.Windows;
using System.Windows.Threading;
using Rename.App.Services;
using Rename.Core.State;

namespace Rename.App;

public partial class App : Application
{
    /// <summary>The single app state instance, created on the UI thread so background work can marshal back to it.</summary>
    public static AppState State { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        State = new AppState(new WpfFileDialogService(), new WpfThumbnailService());
        var window = new MainWindow { DataContext = State };
        MainWindow = window;
        window.Show();

        // Files passed on the command line (e.g. "Open with" or a shortcut) are loaded on launch.
        foreach (var arg in e.Args)
        {
            if (File.Exists(arg) || Directory.Exists(arg)) State.HandleDroppedPath(arg);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var text = e.Exception.Message;
        if (MainWindow is { IsLoaded: true } owner) MessageBox.Show(owner, text, "Rename", MessageBoxButton.OK, MessageBoxImage.Error);
        else MessageBox.Show(text, "Rename", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
