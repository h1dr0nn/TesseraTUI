using System;
using System.Collections.ObjectModel;
using Tessera.ViewModels;

namespace Tessera.Agents;

public class NavigationAgent : ViewModelBase
{
    private WorkspaceViewModel? _activeView;

    public NavigationAgent()
    {
        Views = new ObservableCollection<WorkspaceViewModel>();
    }

    public ObservableCollection<WorkspaceViewModel> Views { get; }

    public WorkspaceViewModel? ActiveView
    {
        get => _activeView;
        set
        {
            if (SetProperty(ref _activeView, value))
            {
                foreach (var view in Views)
                {
                    view.IsSelected = view == value;
                }

                if (value != null)
                {
                    ActiveViewChanged?.Invoke(value);
                }
            }
        }
    }

    public event Action<WorkspaceViewModel>? ActiveViewChanged;

    public void RegisterView(WorkspaceViewModel view)
    {
        Views.Add(view);
        // Don't auto-select - default to plain text view (ActiveView = null)
    }

    public void ToggleView(WorkspaceViewModel view)
    {
        if (ActiveView == view)
        {
            ActiveView = null; // Deselect
        }
        else
        {
            ActiveView = view;
        }
    }
}
