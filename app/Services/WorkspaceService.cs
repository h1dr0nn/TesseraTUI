using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Tessera.Models;

namespace Tessera.Services;

/// <summary>
/// Service for managing workspaces (opening, closing, recent workspaces)
/// </summary>
public class WorkspaceService
{
    private const int MaxRecentWorkspaces = 10;
    private readonly ObservableCollection<WorkspaceContext> _recentWorkspaces = new();

    public WorkspaceContext? CurrentWorkspace { get; private set; }

    public ObservableCollection<WorkspaceContext> RecentWorkspaces => _recentWorkspaces;

    /// <summary>
    /// Open a workspace from a directory path
    /// </summary>
    public WorkspaceContext OpenWorkspace(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        var name = Path.GetFileName(path);
        var workspace = new WorkspaceContext(name, path);

        CurrentWorkspace = workspace;
        AddToRecent(workspace);

        return workspace;
    }

    /// <summary>
    /// Close the current workspace
    /// </summary>
    public void CloseWorkspace()
    {
        CurrentWorkspace = null;
    }

    /// <summary>
    /// Add workspace to recent list
    /// </summary>
    private void AddToRecent(WorkspaceContext workspace)
    {
        // Remove if already exists
        var existing = _recentWorkspaces.FirstOrDefault(w => w.Path == workspace.Path);
        if (existing != null)
        {
            _recentWorkspaces.Remove(existing);
        }

        // Add to front
        _recentWorkspaces.Insert(0, workspace);

        // Trim to max
        while (_recentWorkspaces.Count > MaxRecentWorkspaces)
        {
            _recentWorkspaces.RemoveAt(_recentWorkspaces.Count - 1);
        }
    }

    /// <summary>
    /// Load recent workspaces (from settings in future)
    /// </summary>
    public void LoadRecent()
    {
        // TODO: Load from settings/preferences file
        // For now, just empty
    }

    /// <summary>
    /// Save recent workspaces (to settings in future)
    /// </summary>
    public void SaveRecent()
    {
        // TODO: Save to settings/preferences file
    }
}
