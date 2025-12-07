using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Tessera.ViewModels;

namespace Tessera.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SettingsViewModel vm)
        {
            vm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.SelectedCategory))
        {
            if (DataContext is SettingsViewModel vm)
            {
                ScrollToCategory(vm.SelectedCategory);
            }
        }
    }

    private void ScrollToCategory(string category)
    {
        Control? target = null;
        switch (category)
        {
            case "Appearance": target = this.FindControl<Control>("AppearanceSection"); break;
            case "Editor": target = this.FindControl<Control>("EditorSection"); break;
            case "Data Processing": target = this.FindControl<Control>("DataProcessingSection"); break;
            case "About": target = this.FindControl<Control>("AboutSection"); break;
        }
        target?.BringIntoView();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
