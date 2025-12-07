using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using Avalonia.Media;
using Tessera.ViewModels;

namespace Tessera.Agents;

public enum ToastLevel
{
    Info,
    Warning,
    Error,
    Success
}

public class ToastViewModel : ViewModelBase
{
    private bool _isVisible = true;

    public ToastViewModel(string message, ToastLevel level, Action<ToastViewModel> dismiss)
    {
        Message = message;
        Level = level;
        DismissCommand = new DelegateCommand(_ => dismiss(this));
    }

    public string Message { get; }

    public ToastLevel Level { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public DelegateCommand DismissCommand { get; }

    public IBrush BackgroundBrush => Level switch
    {
        ToastLevel.Warning => new SolidColorBrush(Color.Parse("#E9C46A")),
        ToastLevel.Error => new SolidColorBrush(Color.Parse("#E76F51")),
        ToastLevel.Success => new SolidColorBrush(Color.Parse("#2A9D8F")),
        _ => new SolidColorBrush(Color.Parse("#2A9D8F")) // Info/Default
    };
}

public class UIToastAgent
{
    public ObservableCollection<ToastViewModel> Toasts { get; } = new();

    public void ShowToast(string message, ToastLevel level = ToastLevel.Info, TimeSpan? duration = null)
    {
        var toast = new ToastViewModel(message, level, DismissToast);
        Toasts.Add(toast);

        var lifetime = duration ?? TimeSpan.FromSeconds(3.5);
        DispatcherTimer.RunOnce(() => DismissToast(toast), lifetime);
    }

    private void DismissToast(ToastViewModel toast)
    {
        if (Toasts.Contains(toast))
        {
            toast.IsVisible = false;
            DispatcherTimer.RunOnce(() => Toasts.Remove(toast), TimeSpan.FromMilliseconds(180));
        }
    }
}
