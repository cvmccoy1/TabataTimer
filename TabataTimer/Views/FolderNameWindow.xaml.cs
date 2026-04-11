using System.Windows;
using System.Windows.Input;

namespace TabataTimer.Views;

public partial class FolderNameWindow : Window
{
    private readonly HashSet<string> _usedNames;

    public string? ResultName { get; private set; }

    public FolderNameWindow(string? existingName, IEnumerable<string> usedNames)
    {
        InitializeComponent();
        _usedNames = new HashSet<string>(usedNames, StringComparer.OrdinalIgnoreCase);
        Title = existingName == null ? "New Folder" : "Rename Folder";
        OkButton.Content = existingName == null ? "Save Folder" : "Update Folder";
        NameBox.Text = existingName ?? string.Empty;
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
        Validate();
    }

    private void NameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => Validate();

    private void Validate()
    {
        string name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorText.Text = "Name cannot be empty.";
            OkButton.IsEnabled = false;
        }
        else if (_usedNames.Contains(name))
        {
            ErrorText.Text = "A folder with that name already exists here.";
            OkButton.IsEnabled = false;
        }
        else
        {
            ErrorText.Text = string.Empty;
            OkButton.IsEnabled = true;
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        ResultName = NameBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && OkButton.IsEnabled)
            OK_Click(sender, e);
        else if (e.Key == Key.Escape)
            Cancel_Click(sender, e);
    }
}
