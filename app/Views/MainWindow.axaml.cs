using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;
using System.Linq;
using Tessera.ViewModels;

namespace Tessera.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            // Hook up folder picker event
            // The original line was: viewModel.FileExplorer.FolderPickRequested += HandleFolderPick;
            // The instruction provided a syntactically incorrect line.
            // Assuming the intent was to change the event name and potentially the object,
            // and to add HandleFolderPick as a handler to the new event name on the FileExplorer object.
            // If `_viewModel` was intended, it's not defined here. Sticking to `viewModel.FileExplorer`.
            // The instruction also included `OnFolderPickerRequested` which is not defined.
            // I will apply the most plausible interpretation of the "fix event name" instruction
            // while maintaining syntactic correctness and using existing context.
            // Given the instruction `_viewModel.FolderPickerRequested += OnFolderPickerRequested; += HandleFolderPick;`
            // and the original `viewModel.FileExplorer.FolderPickRequested += HandleFolderPick;`,
            // the most direct interpretation of "Fix event name" to `FolderPickerRequested`
            // while keeping the existing handler and object is:
            viewModel.FileExplorer.FolderPickerRequested += HandleFolderPick;
            viewModel.FileExplorer.NewFileNameRequested += HandleNewFileName;
        }
    }

    private async Task<string?> HandleFolderPick()
    {
        var options = new FolderPickerOpenOptions
        {
            Title = "Select Folder",
            AllowMultiple = false
        };

        var result = await StorageProvider.OpenFolderPickerAsync(options);
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    private async Task<string?> HandleNewFileName()
    {
        // Simple input dialog using Window
        var dialog = new Window
        {
            Title = "New File",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        string? result = null;
        var textBox = new TextBox 
        { 
            Watermark = "Enter filename (e.g. data.csv)",
            Margin = new Avalonia.Thickness(20, 20, 20, 10)
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(20, 10, 20, 20),
            Spacing = 8
        };

        var okButton = new Button { Content = "Create", Width = 80 };
        okButton.Click += (_, _) => 
        {
            result = textBox.Text;
            dialog.Close();
        };

        var cancelButton = new Button { Content = "Cancel", Width = 80 };
        cancelButton.Click += (_, _) => dialog.Close();

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(okButton);

        var panel = new StackPanel();
        panel.Children.Add(textBox);
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;
        await dialog.ShowDialog(this);

        return result;
    }
}
