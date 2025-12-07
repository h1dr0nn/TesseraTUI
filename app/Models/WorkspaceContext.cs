namespace Tessera.Models;

/// <summary>
/// Represents a workspace (folder) context
/// </summary>
public class WorkspaceContext
{
    public WorkspaceContext(string name, string path)
    {
        Name = name;
        Path = path;
        LastOpened = DateTime.Now;
    }

    /// <summary>
    /// Display name of the workspace (folder name)
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Full path to the workspace directory
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// When this workspace was last opened
    /// </summary>
    public DateTime LastOpened { get; set; }
}
