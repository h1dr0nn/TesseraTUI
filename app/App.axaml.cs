using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Tessera.ViewModels;
using Tessera.Views;

namespace Tessera;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel(desktop.Args);
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            
            // Apply saved theme after window is created
            RequestedThemeVariant = viewModel.Settings.PreferredTheme;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
