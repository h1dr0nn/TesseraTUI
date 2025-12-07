using System;
using System.IO;
using System.Threading.Tasks;

namespace Tessera.Services;

/// <summary>
/// Service for file operations (create, rename, delete, move)
/// </summary>
public class FileOperationsService
{
    /// <summary>
    /// Create a new file in the specified directory
    /// </summary>
    public async Task<string> CreateFileAsync(string directoryPath, string fileName)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var fullPath = Path.Combine(directoryPath, fileName);
        
        if (File.Exists(fullPath))
        {
            throw new IOException($"File already exists: {fileName}");
        }

        await File.WriteAllTextAsync(fullPath, string.Empty);
        return fullPath;
    }

    /// <summary>
    /// Create a new folder in the specified directory
    /// </summary>
    public string CreateFolder(string directoryPath, string folderName)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var fullPath = Path.Combine(directoryPath, folderName);
        
        if (Directory.Exists(fullPath))
        {
            throw new IOException($"Folder already exists: {folderName}");
        }

        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Delete a file or folder
    /// </summary>
    public void Delete(string path, bool isDirectory)
    {
        if (isDirectory)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        else
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>
    /// Rename a file or folder
    /// </summary>
    public string Rename(string oldPath, string newName, bool isDirectory)
    {
        var directory = Path.GetDirectoryName(oldPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("Could not determine parent directory");
        }

        var newPath = Path.Combine(directory, newName);

        if (isDirectory)
        {
            Directory.Move(oldPath, newPath);
        }
        else
        {
            File.Move(oldPath, newPath);
        }

        return newPath;
    }

    /// <summary>
    /// Reveal file/folder in system file explorer
    /// </summary>
    public void RevealInFinder(string path)
    {
        if (OperatingSystem.IsMacOS())
        {
            System.Diagnostics.Process.Start("open", $"-R \"{path}\"");
        }
        else if (OperatingSystem.IsWindows())
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else if (OperatingSystem.IsLinux())
        {
            // Try xdg-open on the parent directory
            var directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            System.Diagnostics.Process.Start("xdg-open", $"\"{directory}\"");
        }
    }
}
