namespace GUI.Services;

public interface IFileService
{
    string OpenFolderDialog();

    string OpenFileDialog(string filter);
}
