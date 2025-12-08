namespace Tessera.ViewModels;

public enum WorkspaceStatus
{
    Idle,
    Editing,
    Error
}

public abstract class WorkspaceViewModel : ViewModelBase
{
    private string _currentFileName = "No file opened";
    private string _statusMessage = "Ready";
    private WorkspaceStatus _status = WorkspaceStatus.Idle;
    private bool _isSelected;

    public abstract string Title { get; }

    public abstract string IconName { get; }

    public virtual string Subtitle => "";

    public string CurrentFileName
    {
        get => _currentFileName;
        set => SetProperty(ref _currentFileName, value);
    }

    public WorkspaceStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    
    /// <summary>
    /// Called before the application saves. Views should override this to commit any pending changes to the shared model.
    /// </summary>
    public virtual System.Threading.Tasks.Task OnSaveAsync()
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
