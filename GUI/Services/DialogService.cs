using BLL.Archives;
using GUI.Views.Dialogs;
using System.Windows;

namespace GUI.Services;

public class DialogService : IDialogService
{
    public void ShowError(string message)
        => MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowMessage(string message)
        => MessageBox.Show(message, "Info", MessageBoxButton.OK, MessageBoxImage.Information);

    public string? RequestPassword()
    {
        var dialog = new PasswordDialog();

        // Set owner so it centers over the main app
        if (Application.Current.MainWindow != null)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        // ShowDialog pauses execution here until the user closes the window
        bool? result = dialog.ShowDialog();

        if (result == true)
        {
            return dialog.Password;
        }

        // Return null if user clicked Cancel or closed the window
        return null;
    }

    public bool AskConfirmation(string message)
    {
        var result = MessageBox.Show(message, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public ArchiveCompressionLevel? AskCompressionLevel()
    {
        var dialog = new AddFileOptionsDialog();

        if (Application.Current.MainWindow != null)
            dialog.Owner = Application.Current.MainWindow;

        if (dialog.ShowDialog() == true)
        {
            return dialog.SelectedLevel;
        }
        return null; // User clicked Cancel
    }
}