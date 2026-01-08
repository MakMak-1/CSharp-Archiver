using System.Windows;
using BLL.Archives;

namespace GUI.Views.Dialogs;

public partial class AddFileOptionsDialog : Window
{
    // Public property to read the result
    public ArchiveCompressionLevel SelectedLevel { get; private set; }

    public AddFileOptionsDialog()
    {
        InitializeComponent();

        // Populate ComboBox with Enum values
        LevelBox.ItemsSource = Enum.GetValues(typeof(ArchiveCompressionLevel));

        // Default to 'Normal'
        LevelBox.SelectedItem = ArchiveCompressionLevel.Normal;
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (LevelBox.SelectedItem is ArchiveCompressionLevel level)
        {
            SelectedLevel = level;
            DialogResult = true; // Close and return success
        }
    }
}