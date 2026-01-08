using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GUI.Services;
using BLL.Archives;

namespace GUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IArchiveFactory _factory;
    private readonly IFileService _fileService;
    private readonly IDialogService _dialogService;

    // This property determines what is shown in the window
    [ObservableProperty]
    private object _currentView;

    public MainViewModel(IArchiveFactory factory, IFileService fileService, IDialogService dialogService)
    {
        _factory = factory;
        _fileService = fileService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public void OpenArchive()
    {
        var path = _fileService.OpenFileDialog("Archives|*.zip;*.7z");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var archive = _factory.GetArchive(path);

            var editorVm = new ArchiveExplorerViewModel(archive, _dialogService, _fileService);

            // Subscribe to the close event
            editorVm.CloseRequested += () =>
            {
                // Setting this to null triggers the "Fallback" Start Screen in MainWindow.xaml
                CurrentView = null;
            };

            // Load the data
            editorVm.LoadArchiveCommand.Execute(null);

            // Switch the View
            CurrentView = editorVm;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Could not open archive: {ex.Message}");
        }
    }

    [RelayCommand]
    public void CreateArchive()
    { 
        var creationVm = new ArchiveCreationViewModel(_factory, _fileService, _dialogService);

        creationVm.CloseRequested += () => CurrentView = null;

        creationVm.ArchiveCreated += (newArchive) =>
        {
            var editorVm = new ArchiveExplorerViewModel(newArchive, _dialogService, _fileService);
            editorVm.CloseRequested += () => CurrentView = null;
            editorVm.LoadArchiveCommand.Execute(null);
            CurrentView = editorVm;
        };

        CurrentView = creationVm;
    }
}