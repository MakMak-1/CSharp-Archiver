using System.Windows;
using BLL.Archives;
using GUI.Services;
using GUI.ViewModels;

namespace GUI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Create Services
        IFileService fileService = new FileService();
        IDialogService dialogService = new DialogService();

        // 2. Create BLL Factory 
        IArchiveFactory factory = new ArchiveFactory();

        // 3. Create Main ViewModel
        var mainViewModel = new MainViewModel(factory, fileService, dialogService);

        // 4. Create and Show Window
        var mainWindow = new MainWindow();
        mainWindow.DataContext = mainViewModel;
        mainWindow.Show();
    }
}