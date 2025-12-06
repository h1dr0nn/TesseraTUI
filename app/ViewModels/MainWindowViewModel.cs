using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Styling;
using Tessera.Agents;

namespace Tessera.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsAgent _settingsAgent = new();
    private readonly UIToastAgent _toastAgent = new();
    private readonly NavigationAgent _navigationAgent = new();

    private string _currentFileName = "No file opened";
    private WorkspaceStatus _status = WorkspaceStatus.Idle;
    private string _statusMessage = "Idle";
    private bool _isDarkMode;

    public MainWindowViewModel()
    {
        _settingsAgent.ThemeChanged += ApplyTheme;
        _settingsAgent.SetTheme(ThemeVariant.Light);

        _navigationAgent.RegisterView(new TableViewModel());
        _navigationAgent.RegisterView(new SchemaViewModel());
        _navigationAgent.RegisterView(new JsonViewModel());
        _navigationAgent.ActiveViewChanged += SyncActiveViewState;

        SaveCommand = new DelegateCommand(_ => OnSaveRequested());
        ReloadCommand = new DelegateCommand(_ => OnReloadRequested());
        ToggleThemeCommand = new DelegateCommand(_ => IsDarkMode = !IsDarkMode);
    }

    public ObservableCollection<WorkspaceViewModel> Views => _navigationAgent.Views;

    public WorkspaceViewModel? ActiveView
    {
        get => _navigationAgent.ActiveView;
        set => _navigationAgent.ActiveView = value;
    }

    public string CurrentFileName
    {
        get => _currentFileName;
        set
        {
            if (SetProperty(ref _currentFileName, value))
            {
                foreach (var view in Views)
                {
                    view.CurrentFileName = value;
                }
            }
        }
    }

    public WorkspaceStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                foreach (var view in Views)
                {
                    view.Status = value;
                }
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                foreach (var view in Views)
                {
                    view.StatusMessage = value;
                }
            }
        }
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (SetProperty(ref _isDarkMode, value))
            {
                var variant = value ? ThemeVariant.Dark : ThemeVariant.Light;
                _settingsAgent.SetTheme(variant);
            }
        }
    }

    public ICommand SaveCommand { get; }

    public ICommand ReloadCommand { get; }

    public ICommand ToggleThemeCommand { get; }

    public UIToastAgent ToastAgent => _toastAgent;

    public SettingsAgent Settings => _settingsAgent;

    public NavigationAgent Navigation => _navigationAgent;

    public event Action? SaveRequested;

    public event Action? ReloadRequested;

    private void ApplyTheme(ThemeVariant variant)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = variant;
        }
    }

    private void OnSaveRequested()
    {
        Status = WorkspaceStatus.Editing;
        StatusMessage = "Save requested";
        _toastAgent.ShowToast("Save triggered", ToastLevel.Info);
        SaveRequested?.Invoke();
        // Note: Status should be reset by the save handler when complete
    }
    }

    private void OnReloadRequested()
    {
        Status = WorkspaceStatus.Editing;
        StatusMessage = "Reload requested";
        _toastAgent.ShowToast("Reload triggered", ToastLevel.Warning);
        ReloadRequested?.Invoke();
        // Note: Status should be reset by the reload handler when complete
    }
    }

    private void SyncActiveViewState(WorkspaceViewModel view)
    {
        view.CurrentFileName = CurrentFileName;
        view.Status = Status;
        view.StatusMessage = StatusMessage;
    }
}
