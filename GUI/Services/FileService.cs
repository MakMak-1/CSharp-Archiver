using Microsoft.Win32;

namespace GUI.Services;

public class FileService : IFileService
{
    public string OpenFolderDialog()
    {
        var dialog = new OpenFolderDialog();
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string OpenFileDialog(string filter)
    {
        var dialog = new OpenFileDialog();
        dialog.Filter = filter;
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}