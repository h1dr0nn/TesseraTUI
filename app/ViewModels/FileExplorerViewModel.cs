using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Tessera.Models;
using Tessera.Services;
using Tessera.Utils;

namespace Tessera.ViewModels;

public class FileExplorerViewModel : ViewModelBase
{
    private string? _rootPath;
    private FileNode? _selectedNode;
    private bool _isWorkspaceOpen;

    public ObservableCollection<FileNode> Files { get; } = new();

    public FileNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
            {
                if (value != null && !value.IsDirectory)
                {
                    FileSelected?.Invoke(value.Path);
                }
            }
        }
    }

    public string? RootPath
    {
        get => _rootPath;
        private set => SetProperty(ref _rootPath, value);
    }
    
    public bool IsWorkspaceOpen
    {
        get => _isWorkspaceOpen;
        private set => SetProperty(ref _isWorkspaceOpen, value);
    }

    public ICommand OpenFolderCommand { get; }
    public ICommand CloseWorkspaceCommand { get; }
    public ICommand NewFileCommand { get; }
    public ICommand NewFolderCommand { get; }
    public ICommand RefreshCommand { get; }

    public event Action<string>? FileSelected;
    public event Func<Task<string?>>? FolderPickerRequested; // RENAMED from FolderPickRequested
    public Func<Task<string?>>? NewFileNameRequested { get; set; } // CHANGED from event to property

    private readonly WorkspaceService _workspaceService; // NEW
    private readonly FileOperationsService _fileOperationsService; // NEW

    public FileExplorerViewModel()
    {
        _workspaceService = new WorkspaceService(); // NEW
        _fileOperationsService = new FileOperationsService(); // NEW

        OpenFolderCommand = new DelegateCommand(async _ => await OpenFolderAsync()); // CHANGED method name
        CloseWorkspaceCommand = new DelegateCommand(_ => CloseWorkspace());
        NewFileCommand = new DelegateCommand(async _ => await CreateNewFileAsync());
        NewFolderCommand = new DelegateCommand(async _ => await CreateNewFolderAsync()); // NEW
        RefreshCommand = new DelegateCommand(_ => RefreshWorkspace()); // NEW
        
        // Default to closed state
        IsWorkspaceOpen = false;
    }

    public WorkspaceContext? CurrentWorkspace => _workspaceService.CurrentWorkspace; // NEW

    private async Task OpenFolderAsync() // RENAMED from RequestOpenFolderAsync
    {
        if (FolderPickerRequested == null) return; // RENAMED event
        
        var path = await FolderPickerRequested.Invoke(); // RENAMED event, changed var name
        if (string.IsNullOrEmpty(path)) return;
        
        try // NEW try-catch block
        {
            var workspace = _workspaceService.OpenWorkspace(path); // NEW service call
            IsWorkspaceOpen = true;
            RaisePropertyChanged(nameof(CurrentWorkspace)); // NEW
            LoadDirectory(workspace.Path); // CHANGED to use workspace.Path
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening workspace: {ex.Message}"); // NEW
        }
    }
    
    private async Task CreateNewFileAsync()
    {
        if (!IsWorkspaceOpen || CurrentWorkspace == null) return; // CHANGED condition
        if (NewFileNameRequested == null) return;
        
        var fileName = await NewFileNameRequested.Invoke();
        if (string.IsNullOrEmpty(fileName)) return;
        
        try
        {
            await _fileOperationsService.CreateFileAsync(CurrentWorkspace.Path, fileName);
            RefreshWorkspace();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating file: {ex.Message}");
        }
    }

    private async Task CreateNewFolderAsync()
    {
        if (!IsWorkspaceOpen || CurrentWorkspace == null) return;
        if (NewFileNameRequested == null) return; // Reuse for folder name input
        
        var folderName = await NewFileNameRequested.Invoke();
        if (string.IsNullOrEmpty(folderName)) return;
        
        try
        {
            _fileOperationsService.CreateFolder(CurrentWorkspace.Path, folderName);
            RefreshWorkspace();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating folder: {ex.Message}");
        }
    }

    private void RefreshWorkspace()
    {
        if (CurrentWorkspace != null)
        {
            LoadDirectory(CurrentWorkspace.Path);
        }
    }
    
    private void CloseWorkspace()
    {
        _workspaceService.CloseWorkspace();
        Files.Clear();
        RootPath = null;
        IsWorkspaceOpen = false;
        RaisePropertyChanged(nameof(CurrentWorkspace));
    }

    public void LoadDirectory(string path)
    {
        if (!Directory.Exists(path)) return;

        // Use DirectoryInfo to safely get folder name even if path ends with separator
        var rootDir = new DirectoryInfo(path);
        RootPath = rootDir.Name;
        Files.Clear();

        try
        {
            // Load directories first (with recursive children)
            foreach (var dir in rootDir.GetDirectories().OrderBy(d => d.Name))
            {
               if (dir.Name.StartsWith(".")) continue; // Skip hidden
               var node = LoadDirectoryNode(dir);
               Files.Add(node);
            }

            // Then files
            foreach (var file in rootDir.GetFiles().OrderBy(f => f.Name))
            {
                if (file.Name.StartsWith(".") || file.Extension == ".dll" || file.Extension == ".pdb") continue;
                Files.Add(new FileNode(file.Name, file.FullName, false));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading directory: {ex.Message}");
        }
    }

    private FileNode LoadDirectoryNode(DirectoryInfo dir)
    {
        var node = new FileNode(dir.Name, dir.FullName, true);
        
        try
        {
            // Load subdirectories
            foreach (var subDir in dir.GetDirectories().OrderBy(d => d.Name))
            {
                if (subDir.Name.StartsWith(".")) continue;
                node.Children.Add(LoadDirectoryNode(subDir));
            }
            
            // Load files
            foreach (var file in dir.GetFiles().OrderBy(f => f.Name))
            {
                if (file.Name.StartsWith(".") || file.Extension == ".dll" || file.Extension == ".pdb") continue;
                node.Children.Add(new FileNode(file.Name, file.FullName, false));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading subdirectory {dir.Name}: {ex.Message}");
        }
        
        return node;
    }
}
