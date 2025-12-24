using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Tessera.ViewModels;

namespace Tessera.Views.Explorer;

public partial class ExplorerView : UserControl
{
    public ExplorerView()
    {
        InitializeComponent();
    }
    
    /// <summary>
    /// Handle KeyDown on the rename TextBox: Enter commits, Escape cancels.
    /// </summary>
    private void OnRenameTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (textBox.DataContext is not FileNode fileNode) return;
        
        if (e.Key == Key.Enter)
        {
            // Commit rename
            CommitRename(fileNode, textBox.Text);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Cancel rename
            fileNode.IsEditing = false;
            e.Handled = true;
        }
    }
    
    /// <summary>
    /// Handle LostFocus on the rename TextBox: commit the rename.
    /// </summary>
    private void OnRenameTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (textBox.DataContext is not FileNode fileNode) return;
        
        // Commit rename when focus is lost
        if (fileNode.IsEditing)
        {
            CommitRename(fileNode, textBox.Text);
        }
    }
    
    /// <summary>
    /// Auto-focus and select all text when the TextBox becomes visible.
    /// </summary>
    private void OnRenameTextBoxAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (textBox.DataContext is not FileNode fileNode) return;
        
        if (fileNode.IsEditing)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }
    
    /// <summary>
    /// Commits the rename operation via the ViewModel.
    /// </summary>
    private void CommitRename(FileNode fileNode, string? newName)
    {
        fileNode.IsEditing = false;
        
        if (string.IsNullOrWhiteSpace(newName) || newName == fileNode.Name) return;
        
        // Get the FileExplorerViewModel from DataContext hierarchy
        if (DataContext is MainWindowViewModel mainVm)
        {
            fileNode.EditName = newName;
            mainVm.FileExplorer.CommitRename();
        }
    }
}
