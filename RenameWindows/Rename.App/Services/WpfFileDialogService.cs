using System.Windows;
using Microsoft.Win32;
using Rename.Core.Services;

namespace Rename.App.Services;

public sealed class WpfFileDialogService : IFileDialogService
{
    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose a folder to load files from",
            Multiselect = false,
        };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FolderName : null;
    }

    public IReadOnlyList<string> PickFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose files to rename",
            Multiselect = true,
            CheckFileExists = true,
            Filter = "All files (*.*)|*.*",
        };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileNames : [];
    }
}
