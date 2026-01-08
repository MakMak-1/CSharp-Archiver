using BLL.Archives;

namespace GUI.Services;

public interface IDialogService
{
    void ShowError(string message);
    void ShowMessage(string message);
    string? RequestPassword(); // Returns null if cancelled
    bool AskConfirmation(string message);
    ArchiveCompressionLevel? AskCompressionLevel();
}
