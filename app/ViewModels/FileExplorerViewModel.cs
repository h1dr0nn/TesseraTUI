using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Tessera.Agents; // NEW
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
    
    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _searchSubject.OnNext(value); // Push to Rx subject (debounced)
            }
        }
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
    public ICommand RenameCommand { get; } // NEW
    public ICommand OpenInExplorerCommand { get; } // NEW

    public event Action<string>? FileSelected;
    public event Func<Task<string?>>? FolderPickerRequested; // RENAMED from FolderPickRequested
    public Func<Task<string?>>? NewFileNameRequested { get; set; } // CHANGED from event to property

    private readonly WorkspaceService _workspaceService; 
    private readonly FileOperationsService _fileOperationsService;
    private readonly SettingsAgent _settingsAgent; // NEW
    private readonly Subject<string> _searchSubject = new(); // Rx debounce

    public FileExplorerViewModel(SettingsAgent settingsAgent) // CHANGED
    {
        _settingsAgent = settingsAgent;
        _workspaceService = new WorkspaceService(); 
        _fileOperationsService = new FileOperationsService(); 
        
        _settingsAgent.SettingsChanged += () => RefreshWorkspace(); // Refresh on settings change
        
        // Setup debounced search using System.Reactive
        _searchSubject
            .Throttle(TimeSpan.FromMilliseconds(300))
            .DistinctUntilChanged()
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(_ => RefreshWorkspace());

        OpenFolderCommand = new DelegateCommand(async _ => await OpenFolderAsync()); // CHANGED method name
        CloseWorkspaceCommand = new DelegateCommand(_ => CloseWorkspace());
        NewFileCommand = new DelegateCommand(async _ => await CreateNewFileAsync());
        NewFolderCommand = new DelegateCommand(async _ => await CreateNewFolderAsync()); // NEW
        RefreshCommand = new DelegateCommand(_ => RefreshWorkspace()); // NEW
        RenameCommand = new DelegateCommand(async _ => await RenameAsync()); // NEW
        OpenInExplorerCommand = new DelegateCommand(_ => OpenInExplorer()); // NEW
        
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

    private async Task RenameAsync()
    {
        if (SelectedNode == null) return;

        // Start inline editing
        SelectedNode.EditName = SelectedNode.Name;
        SelectedNode.IsEditing = true;
    }

    public void CommitRename()
    {
        if (SelectedNode == null || !SelectedNode.IsEditing) return;

        var oldName = SelectedNode.Name;
        var newName = SelectedNode.EditName?.Trim();
        SelectedNode.IsEditing = false;

        if (string.IsNullOrEmpty(newName) || newName == oldName) return;

        try
        {
            _fileOperationsService.Rename(SelectedNode.Path, newName, SelectedNode.IsDirectory);
            RefreshWorkspace();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error renaming: {ex.Message}");
        }
    }

    public void CancelRename()
    {
        if (SelectedNode == null) return;
        SelectedNode.IsEditing = false;
    }

    private void OpenInExplorer()
    {
        if (SelectedNode == null) return;
        
        try
        {
            _fileOperationsService.RevealInFinder(SelectedNode.Path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening in explorer: {ex.Message}");
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
            var comparer = new NaturalStringComparer();
            foreach (var dir in rootDir.GetDirectories().OrderBy(d => d.Name, comparer))
            {
               if (dir.Name.StartsWith(".")) continue; // Skip hidden
               var node = LoadDirectoryNode(dir, SearchText);
               if (node != null) Files.Add(node);
            }

            // Then files
            foreach (var file in rootDir.GetFiles().OrderBy(f => f.Name, comparer))
            {
                if (file.Name.StartsWith(".") || file.Extension == ".dll" || file.Extension == ".pdb") continue;
                
                // Filter Logic: Extension
                if (_settingsAgent.ShowCsvJsonOnly && !IsAllowedExtension(file.Extension)) continue;
                
                // Filter Logic: Search
                if (!string.IsNullOrEmpty(SearchText) && !file.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) continue;

                Files.Add(new FileNode(file.Name, file.FullName, false));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading directory: {ex.Message}");
        }
    }

    private FileNode? LoadDirectoryNode(DirectoryInfo dir, string searchText)
    {
        var node = new FileNode(dir.Name, dir.FullName, true);
        bool hasMatchingChildren = false;
        
        try
        {
            // Load subdirectories
            var comparer = new NaturalStringComparer();
            foreach (var subDir in dir.GetDirectories().OrderBy(d => d.Name, comparer))
            {
                if (subDir.Name.StartsWith(".")) continue;
                var subNode = LoadDirectoryNode(subDir, searchText);
                if (subNode != null) 
                {
                    node.Children.Add(subNode);
                    hasMatchingChildren = true;
                }
            }
            
            // Load files
            foreach (var file in dir.GetFiles().OrderBy(f => f.Name, comparer))
            {
                if (file.Name.StartsWith(".") || file.Extension == ".dll" || file.Extension == ".pdb") continue;

                // Filter Logic: Extension
                if (_settingsAgent.ShowCsvJsonOnly && !IsAllowedExtension(file.Extension)) continue;

                // Filter Logic: Search
                bool matchesSearch = string.IsNullOrEmpty(searchText) || file.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);

                if (matchesSearch)
                {
                    node.Children.Add(new FileNode(file.Name, file.FullName, false));
                    hasMatchingChildren = true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading subdirectory {dir.Name}: {ex.Message}");
        }
        
        // Filter this node
        if (string.IsNullOrEmpty(searchText)) return node; // No filter
        
        bool selfMatches = dir.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        
        if (selfMatches || hasMatchingChildren)
        {
            node.IsExpanded = true; // Expand if matching or has matching children
            return node;
        }
        
        return null;
    }
    
    private bool IsAllowedExtension(string extension)
    {
        var ext = extension.ToLowerInvariant();
        return ext == ".csv" || ext == ".json" || ext == ".tsv" || ext == ".txt";
    }
}
