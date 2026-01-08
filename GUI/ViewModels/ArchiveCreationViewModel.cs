using BLL;
using BLL.Archives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GUI.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;

namespace GUI.ViewModels;

public partial class ArchiveCreationViewModel : ObservableObject
{
    private readonly IArchiveFactory _factory;
    private readonly IFileService _fileService;
    private readonly IDialogService _dialogService;

    public event Action? CloseRequested;
    public event Action<IArchive>? ArchiveCreated;

    // --- NEW: List of items to archive ---
    public ObservableCollection<string> ItemsToArchive { get; } = new();

    // --- OUTPUT SETTINGS ---
    private string _archiveName = "NewArchive";
    public string ArchiveName
    {
        get => _archiveName;
        set { if (SetProperty(ref _archiveName, value)) OnPropertyChanged(nameof(FullOutputPath)); }
    }

    private string _destinationFolder;
    public string DestinationFolder
    {
        get => _destinationFolder;
        set { if (SetProperty(ref _destinationFolder, value)) OnPropertyChanged(nameof(FullOutputPath)); }
    }

    public string FullOutputPath =>
        Path.Combine(DestinationFolder ?? "", $"{ArchiveName}{(IsZip ? ".zip" : ".7z")}");

    // --- FORMAT & SECURITY ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullOutputPath))]
    private bool _isZip = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullOutputPath))]
    private bool _is7z = false;

    [ObservableProperty] private string _password = "";
    [ObservableProperty] private ArchiveCompressionLevel _selectedCompression = ArchiveCompressionLevel.Normal;
    public IEnumerable<ArchiveCompressionLevel> CompressionOptions => Enum.GetValues<ArchiveCompressionLevel>();

    // --- PROGRESS ---
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private string _progressMessage = "Ready";
    private CancellationTokenSource? _cts;

    public ArchiveCreationViewModel(IArchiveFactory factory, IFileService fileService, IDialogService dialogService)
    {
        _factory = factory;
        _fileService = fileService;
        _dialogService = dialogService;
    }

    // --- NEW COMMANDS ---

    [RelayCommand]
    public void AddFiles()
    {
        // Requires IFileService to support Multiselect or returning a list
        // For now, let's assume OpenFileDialog returns one string, 
        // but ideally update IFileService to return string[]
        var file = _fileService.OpenFileDialog("All files|*.*");
        if (!string.IsNullOrEmpty(file) && !ItemsToArchive.Contains(file))
        {
            ItemsToArchive.Add(file);
            AutoSetDestination(file);
        }
    }

    [RelayCommand]
    public void AddFolder()
    {
        var folder = _fileService.OpenFolderDialog();
        if (!string.IsNullOrEmpty(folder) && !ItemsToArchive.Contains(folder))
        {
            ItemsToArchive.Add(folder);
            AutoSetDestination(folder);
        }
    }

    [RelayCommand]
    public void RemoveItem(string path)
    {
        if (ItemsToArchive.Contains(path)) ItemsToArchive.Remove(path);
    }

    [RelayCommand]
    public void BrowseDestination()
    {
        var folder = _fileService.OpenFolderDialog();
        if (!string.IsNullOrEmpty(folder)) DestinationFolder = folder;
    }

    private void AutoSetDestination(string sourcePath)
    {
        if (string.IsNullOrEmpty(DestinationFolder))
        {
            // Set default output folder to the parent of the first item added
            DestinationFolder = Directory.GetParent(sourcePath)?.FullName ?? sourcePath;
        }
    }

    [RelayCommand]
    public async Task CreateArchiveAsync()
    {
        // Validate Source Items
        if (ItemsToArchive.Count == 0)
        {
            _dialogService.ShowError("Please add at least one file or folder.");
            return;
        }

        // Validate Archive Name
        if (!IsArchiveNameValid(ArchiveName, out string nameError))
        {
            _dialogService.ShowError(nameError);
            return; 
        }

        // Validate Password
        if (!string.IsNullOrWhiteSpace(Password))
        {
            if (!IsPasswordValid(Password))
            {
                _dialogService.ShowError(
                    "Password requirements not met:\n" +
                    "- Length must be between 6 and 30 characters.\n" +
                    "- Allowed characters: Latin letters, digits, and !@#$%^&+=");
                return;
            }
        }

        try
        {
            IsBusy = true;
            ProgressValue = 0;
            _cts = new CancellationTokenSource();
            var progress = new Progress<ProgressReport>(r => { ProgressValue = r.Percentage; ProgressMessage = r.Message; });
            string? finalPass = string.IsNullOrWhiteSpace(Password) ? null : Password;

            // Create Empty Archive
            IArchive archive = await _factory.CreateFromDirectoryAsync(null, FullOutputPath, finalPass, SelectedCompression, progress, _cts.Token);

            // Add Items One by One
            double currentProg = 0;
            double step = 100.0 / ItemsToArchive.Count;

            foreach (var itemPath in ItemsToArchive)
            {
                if (_cts.Token.IsCancellationRequested) break;

                // Case A: It's a Directory
                if (Directory.Exists(itemPath))
                {
                    var rootDirInfo = new DirectoryInfo(itemPath);
                    string rootFolderName = rootDirInfo.Name; // e.g., "csv_files"

                    // 1. Get ALL files recursively
                    var allFiles = Directory.GetFiles(itemPath, "*", SearchOption.AllDirectories);

                    // 2. Group them by their physical folder on disk
                    // Key: "C:\Path\csv_files\docx_files", Value: ["Lab1.docx", "Lab2.docx"...]
                    var filesByDirectory = allFiles.GroupBy(f => Path.GetDirectoryName(f));

                    foreach (var group in filesByDirectory)
                    {
                        // 3. Check for cancellation
                        if (_cts.Token.IsCancellationRequested) break;

                        // 4. Calculate the relative path for this specific group
                        string sourceDir = group.Key!;

                        // This calculates "docx_files" relative to "csv_files"
                        string relativePath = Path.GetRelativePath(itemPath, sourceDir);

                        // If it's the root folder itself, GetRelativePath returns "."
                        if (relativePath == ".") relativePath = "";

                        // 5. Build the final archive path
                        // Result: "csv_files/docx_files/"
                        string targetPathInArchive = Path.Combine(rootFolderName, relativePath)
                                                     .Replace('\\', '/'); // Ensure Zip compatibility

                        // 6. Add this batch to the archive
                        await archive.AddFilesAsync(
                            group.ToList(),
                            targetPathInArchive,
                            finalPass,
                            SelectedCompression,
                            progress,
                            _cts.Token
                        );
                    }
                }
                // Case B: It's a File
                else if (File.Exists(itemPath))
                {
                    await archive.AddFilesAsync(new[] { itemPath }, "", finalPass, SelectedCompression, progress, _cts.Token);
                }

                currentProg += step;
                ProgressValue = (int)currentProg;
            }

            _dialogService.ShowMessage("Archive created successfully!");
            ArchiveCreated?.Invoke(archive);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void Cancel() => _cts?.Cancel();
    [RelayCommand]
    public void GoBack() => CloseRequested?.Invoke();

    private bool IsPasswordValid(string password)
    {
        // Check Length (6 to 30 characters)
        if (password.Length < 6 || password.Length > 30)
            return false;

        // Checking allowed characters using regex
        string pattern = @"^[a-zA-Z0-9!@#$%^&+=]+$";

        return Regex.IsMatch(password, pattern);
    }

    private bool IsArchiveNameValid(string name, out string error)
    {
        error = string.Empty;

        // Check Length (1 to 255)
        if (string.IsNullOrWhiteSpace(name) || name.Length < 1 || name.Length > 255)
        {
            error = "Archive name length must be between 1 and 255 characters.";
            return false;
        }

        // Check Forbidden Characters
        char[] forbiddenChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

        if (name.IndexOfAny(forbiddenChars) != -1)
        {
            error = "Archive name cannot contain the following characters:\n< > : \" / \\ | ? *";
            return false;
        }

        return true;
    }
}