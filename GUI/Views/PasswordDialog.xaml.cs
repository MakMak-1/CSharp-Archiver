using System.Windows;

namespace GUI.Views.Dialogs;

public partial class PasswordDialog : Window
{
    // Public property to retrieve the password after the window closes
    public string Password { get; private set; } = string.Empty;

    public PasswordDialog()
    {
        InitializeComponent();

        // Auto-focus the input box so user can type immediately
        InputBox.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Password = InputBox.Password;
        DialogResult = true; // Closes the window and returns 'true' to ShowDialog()
    }
}