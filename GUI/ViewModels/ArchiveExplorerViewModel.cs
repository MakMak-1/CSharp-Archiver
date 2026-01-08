using BLL;
using BLL.Archives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GUI.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace GUI.ViewModels;

public partial class ArchiveExplorerViewModel : ObservableObject
{
    public event Action? CloseRequested;
    private readonly IArchive _archive;
    private readonly IDialogService _dialogService;
    private readonly IFileService _fileService;
    private CancellationTokenSource? _extractionCts;
    private string? _cachedPassword;

    // We keep a cache of ALL entries from the BLL to avoid re-reading the file constantly
    private List<IArchiveEntry> _allEntriesCache = new();

    // The current "virtual" path inside the archive (e.g., "Folder1/")
    [ObservableProperty]
    private string _currentPath = string.Empty;

    // The collection bound to your WPF DataGrid/ListView
    [ObservableProperty]
    private ObservableCollection<IArchiveEntry> _currentEntries = new();

    // Binding for the Progress Bar (0-100)
    [ObservableProperty]
    private int _progressValue;

    // Binding for the Progress Text (e.g., "Extracting file.txt...")
    [ObservableProperty]
    private string _progressMessage = "Ready";

    // Used to lock the UI during operations
    [ObservableProperty]
    private bool _isBusy;

    // Controls visibility of the Cancel button
    [ObservableProperty]
    private bool _isCancellable;

    private IArchiveEntry? _selectedEntry;
    public IArchiveEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                DeleteItemCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ArchiveExplorerViewModel(
        IArchive archive,
        IDialogService dialogService,
        IFileService fileService)
    {
        _archive = archive;
        _dialogService = dialogService;
        _fileService = fileService;
    }

    /// <summary>
    /// Initial loader called when the View opens.
    /// </summary>
    [RelayCommand]
    public async Task LoadArchiveAsync()
    {
        try
        {
            IsCancellable = false;
            IsBusy = true;
            ProgressMessage = "Reading archive structure...";

            string password = GetOrRequestPassword();
            if (_archive.IsPasswordProtected && password == null) // If protected and user cancelled (returned null), stop.
            {
                CloseRequested?.Invoke();
                return;
            } 

            var progress = new Progress<ProgressReport>(ReportProgress);
            var token = CancellationToken.None;

            // Get Real Entries
            var realEntries = _archive.IsPasswordProtected
                ? await _archive.GetEntriesAsync(password, progress, token)
                : await _archive.GetEntriesAsync(progress, token);

            // Synthesize Directories
            // We use a HashSet to ensure we don't add the same folder twice
            // (and to check if the archive ALREADY has an explicit entry for it)
            var allPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var finalEntryList = new List<IArchiveEntry>();

            foreach (var entry in realEntries)
            {
                // Normalize path separators to '/' for consistency
                string normalizedPath = entry.FullName.Replace('\\', '/');

                // Add the file itself
                finalEntryList.Add(entry);
                allPaths.Add(normalizedPath);

                // Now look for parent folders
                // Example: "A/B/C/file.txt" -> Needs "A/", "A/B/", "A/B/C/"
                var parts = normalizedPath.Split('/');

                // We iterate up to Length - 1 because the last part is the file itself
                string currentBuild = "";
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    currentBuild += parts[i] + "/";

                    // If we haven't seen this directory yet, create it
                    if (!allPaths.Contains(currentBuild))
                    {
                        finalEntryList.Add(new GUI.Models.VirtualArchiveEntry(currentBuild));
                        allPaths.Add(currentBuild);
                    }
                }
            }

            _allEntriesCache = finalEntryList;

            // Render
            RefreshFileList();
        }
        catch (Exception ex)
        {
            _cachedPassword = null;
            _dialogService.ShowError($"Failed to load archive: {ex.Message}");
            CloseRequested?.Invoke();
        }
        finally
        {
            IsBusy = false;
            ProgressMessage = "Ready";
            ProgressValue = 0;
        }
    }

    [RelayCommand]
    public async Task AddFileToArchiveAsync()
    {
        // Select the file to add
        string filePath = _fileService.OpenFileDialog("All Files|*.*");
        if (string.IsNullOrEmpty(filePath)) return;

        var compressionLevel = _dialogService.AskCompressionLevel();
        if (compressionLevel == null) return; // User cancelled the options dialog

        try
        {
            IsCancellable = false;
            IsBusy = true;
            ProgressMessage = "Adding file...";

            // Handle Password
            string password = GetOrRequestPassword();
            if (_archive.IsPasswordProtected && password == null) return;

            // Prepare BLL arguments
            var progress = new Progress<ProgressReport>(ReportProgress);
            var token = CancellationToken.None;

            // Call BLL
            await _archive.AddFilesAsync(
                new[] { filePath },
                CurrentPath,
                password,
                (ArchiveCompressionLevel)compressionLevel, // Cast to non-nullable
                progress,
                token
            );

            // Refresh the list to show the new file
            await LoadArchiveAsync();

            _dialogService.ShowMessage("File added successfully!");
        }
        catch (Exception ex)
        {
            _cachedPassword = null;
            _dialogService.ShowError($"Error adding file: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Called when user double-clicks a row.
    /// </summary>
    [RelayCommand]
    public void Navigate(IArchiveEntry entry)
    {
        if (entry == null || !entry.IsDirectory) return;

        // Append the folder name to the current path
        // e.g. "Data/" + "Logs/" = "Data/Logs/"
        // Note: We ensure we don't double-slash.
        string newSegment = entry.Name.EndsWith("/") ? entry.Name : entry.Name + "/";
        CurrentPath += newSegment;

        RefreshFileList();
    }

    /// <summary>
    /// Closes the current archive and goes back
    /// </summary>
    [RelayCommand]
    public void GoBack()
    {
        // Cancel any ongoing operations first
        _extractionCts?.Cancel();

        // Trigger the event
        CloseRequested?.Invoke();
    }

    /// <summary>
    /// Called when user clicks "Up".
    /// </summary>
    [RelayCommand]
    public void NavigateUp()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;

        // Logic: 
        // 1. Trim the trailing slash "Data/Logs/" -> "Data/Logs"
        // 2. Find the last slash index
        var cleanPath = CurrentPath.TrimEnd('/');
        int lastSlash = cleanPath.LastIndexOf('/');

        if (lastSlash < 0)
        {
            // No slashes left (e.g. "Data"), so we go to Root
            CurrentPath = string.Empty;
        }
        else
        {
            // Cut string up to the last slash (e.g. "Data/Logs" -> "Data/")
            CurrentPath = cleanPath.Substring(0, lastSlash + 1);
        }

        RefreshFileList();
    }

    /// <summary>
    /// Called when the user clicks "Cancel" on the progress overlay.
    /// </summary>
    [RelayCommand]
    public void Cancel()
    {
        _extractionCts?.Cancel();
    }

    /// <summary>
    /// Filters the flat list of entries to show only what is in the CurrentPath.
    /// </summary>
    private void RefreshFileList()
    {
        CurrentEntries.Clear();

        // Simple logic: Find entries that start with CurrentPath 
        // and don't have extra slashes (meaning they are in sub-sub-folders)

        foreach (var entry in _allEntriesCache)
        {
            // Logic to determine if entry is a direct child of CurrentPath
            // (Implementation depends on whether your paths use / or \)
            if (IsDirectChild(entry.FullName, CurrentPath))
            {
                CurrentEntries.Add(entry);
            }
        }
    }

    /// <summary>
    /// Extracts the ENTIRE archive to a folder selected by the user.
    /// </summary>
    [RelayCommand]
    public async Task ExtractAllAsync()
    {
        // Select Destination
        string destination = _fileService.OpenFolderDialog();
        if (string.IsNullOrEmpty(destination)) return;

        try
        {
            IsCancellable = true;
            IsBusy = true;
            ProgressValue = 0;

            // Setup BLL params
            var progress = new Progress<ProgressReport>(ReportProgress);
            _extractionCts = new CancellationTokenSource();

            // Handle Password
            string password = GetOrRequestPassword();
            if (_archive.IsPasswordProtected && password == null) return;

            // Call BLL
            if (_archive.IsPasswordProtected)
            {
                await _archive.ExtractAsync(destination, password, progress, _extractionCts.Token);
            }
            else
            {
                await _archive.ExtractAsync(destination, progress, _extractionCts.Token);
            }

            _dialogService.ShowMessage("Extraction Complete!");
        }
        catch (OperationCanceledException)
        {
            _dialogService.ShowMessage("Extraction Cancelled.");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error extracting: {ex.Message}");
            _cachedPassword = null;
        }
        finally
        {
            IsBusy = false;
            ProgressMessage = "Ready";
            ProgressValue = 0;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    public async Task DeleteItemAsync()
    {
        if (SelectedEntry == null) return;

        // Confirm
        if (!_dialogService.AskConfirmation($"Are you sure you want to delete '{SelectedEntry.Name}'?"))
            return;

        try
        {
            IsCancellable = false;
            IsBusy = true;
            ProgressMessage = "Deleting...";

            // Identify what to delete
            var pathsToDelete = new List<string>();

            if (SelectedEntry.IsDirectory)
            {
                // We must find ALL files in the cache that start with this folder's path.

                // Ensure directory path ends with '/' for safe matching
                string dirPath = SelectedEntry.FullName.Replace('\\', '/').TrimEnd('/') + "/";

                // Find descendants in the flat list
                var descendants = _allEntriesCache
                    .Where(e => e.FullName.Replace('\\', '/').StartsWith(dirPath, StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.FullName)
                    .ToList();

                pathsToDelete.AddRange(descendants);
            }
            else
            {
                // It's just a file
                pathsToDelete.Add(SelectedEntry.FullName);
            }

            // Call BLL
            var progress = new Progress<ProgressReport>(ReportProgress);
            var token = CancellationToken.None;

            if (_archive.IsPasswordProtected)
            {
                string password = GetOrRequestPassword();
                if (_archive.IsPasswordProtected && password == null) return;

                await _archive.DeleteEntriesAsync(pathsToDelete, password, progress, token);
            }
            else
            {
                await _archive.DeleteEntriesAsync(pathsToDelete, progress, token);
            }

            // Reload the view
            await LoadArchiveAsync(); // Re-read the archive to refresh the list
            _dialogService.ShowMessage("Item deleted.");
        }
        catch (Exception ex)
        {
            _cachedPassword = null;
            _dialogService.ShowError($"Error deleting: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task AddFolderToArchiveAsync()
    {
        // Select the Folder
        string sourceFolderPath = _fileService.OpenFolderDialog();
        if (string.IsNullOrEmpty(sourceFolderPath)) return;

        // Ask for Compression
        var compressionLevel = _dialogService.AskCompressionLevel();
        if (compressionLevel == null) return;

        try
        {
            IsCancellable = false;
            IsBusy = true;
            ProgressMessage = "Scanning folder...";

            // Handle Password
            string password = GetOrRequestPassword();
            if (_archive.IsPasswordProtected && password == null) return;

            // PREPARE THE DATA
            // We need to preserve the directory structure.
            // Example: User picks "C:\Photos". 
            // We want the archive to have "CurrentPath/Photos/..."

            var rootDirInfo = new DirectoryInfo(sourceFolderPath);
            string rootFolderName = rootDirInfo.Name; // e.g., "Photos"

            // Get all files recursively
            var allFiles = Directory.GetFiles(sourceFolderPath, "*.*", SearchOption.AllDirectories);

            var progress = new Progress<ProgressReport>(ReportProgress);
            var token = CancellationToken.None;

            // STRATEGY: Group files by their directory to minimize BLL calls
            // key: Directory Path, value: List of files in that directory
            var filesByDirectory = allFiles.GroupBy(f => Path.GetDirectoryName(f));

            double currentProg = 0;
            double step = 100.0 / filesByDirectory.Count();

            foreach (var group in filesByDirectory)
            {
                // group.Key is the full path on disk (e.g., C:\Photos\2023)
                // We need to convert this to relative archive path (e.g., Photos/2023)

                string dirPath = group.Key;

                // Calculate relative path from the source root
                // If source is C:\Photos and file is in C:\Photos\Sub, relative is "Sub"
                string relativePath = Path.GetRelativePath(sourceFolderPath, dirPath);

                // If relativePath is ".", it means it's the root folder itself
                if (relativePath == ".") relativePath = "";

                // Construct the final path inside the archive
                // CurrentViewPath + RootFolderName + RelativeSubFolder
                // e.g., "" + "Photos/" + "Sub/"
                string targetPathInArchive = Path.Combine(CurrentPath, rootFolderName, relativePath)
                                             .Replace('\\', '/'); // Standardize slashes

                if (!targetPathInArchive.EndsWith("/")) targetPathInArchive += "/";

                ProgressMessage = $"Adding files to '{targetPathInArchive}'...";

                // Call BLL for this batch of files
                await _archive.AddFilesAsync(
                    group.ToList(),
                    targetPathInArchive,
                    password,
                    compressionLevel.Value,
                    progress,
                    token
                );

                currentProg += step;
                ProgressValue = (int)currentProg;
            }

            // Refresh
            await LoadArchiveAsync();
            _dialogService.ShowMessage("Folder added successfully!");
        }
        catch (Exception ex)
        {
            _cachedPassword = null; // Clear cache on error
            _dialogService.ShowError($"Error adding folder: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanDelete() => SelectedEntry != null;

    // Helper method for the Progress<T> callback
    private void ReportProgress(ProgressReport report)
    {
        ProgressValue = report.Percentage;
        ProgressMessage = report.Message;
    }

    // Basic logic to simulate folder structure from flat paths
    private bool IsDirectChild(string entryPath, string currentDirectory)
    {
        // Normalize both inputs to use '/'
        string normEntry = entryPath.Replace('\\', '/');
        string normCurrent = currentDirectory.Replace('\\', '/');

        // If at root, we want entries that have NO slashes, 
        // or exactly one slash at the very end (if it's a folder).
        if (string.IsNullOrEmpty(normCurrent))
        {
            string cleanPath = normEntry.TrimEnd('/');
            return !cleanPath.Contains('/');
        }

        // Must start with current directory
        if (!normEntry.StartsWith(normCurrent)) return false;

        // Get the relative part
        // Entry: "A/B/file.txt", Current: "A/" -> Relative: "B/file.txt"
        string relative = normEntry.Substring(normCurrent.Length);

        if (string.IsNullOrEmpty(relative)) return false; // It's the folder itself

        // If relative part has no slashes (file) 
        // OR just one trailing slash (direct subfolder), it's a child.
        string cleanRelative = relative.TrimEnd('/');
        return !cleanRelative.Contains('/');
    }

    private string? GetOrRequestPassword()
    {
        // If archive is not encrypted, no password needed
        if (!_archive.IsPasswordProtected) return null;

        // If we already have a password in memory, use it
        if (!string.IsNullOrEmpty(_cachedPassword)) return _cachedPassword;

        // Otherwise, ask the user
        string? pass = _dialogService.RequestPassword();

        // If user entered something, cache it for future use
        if (!string.IsNullOrEmpty(pass))
        {
            _cachedPassword = pass;
        }

        return _cachedPassword;
    }
}