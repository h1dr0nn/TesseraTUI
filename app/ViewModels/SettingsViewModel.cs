using System;
using Avalonia.Styling;
using Tessera.Agents;
using Tessera.Utils;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Tessera.Core;

namespace Tessera.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsAgent _settingsAgent;
    private bool _isDarkMode;
    private bool _highContrastMode;
    private string _selectedCategory = "General";

    public SettingsViewModel(SettingsAgent settingsAgent)
    {
        _settingsAgent = settingsAgent;
        _isDarkMode = settingsAgent.PreferredTheme == ThemeVariant.Dark;
        
        Categories = new ObservableCollection<string>
        {
            "Appearance",
            "Editor",
            "Data Processing",
            "About"
        };
        
        _settingsAgent.ThemeChanged += (variant) => 
        {
             _isDarkMode = variant == ThemeVariant.Dark || variant == SettingsAgent.HighContrastDark;
             RaisePropertyChanged(nameof(IsDarkMode));
        };

        ToggleThemeCommand = new DelegateCommand(_ => IsDarkMode = !IsDarkMode);
    }

    public ObservableCollection<string> Categories { get; }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (SetProperty(ref _isDarkMode, value))
            {
                UpdateTheme();
            }
        }
    }

    public bool HighContrastMode
    {
        get => _highContrastMode;
        set
        {
            if (SetProperty(ref _highContrastMode, value))
            {
                UpdateTheme();
            }
        }
    }
    
    private void UpdateTheme()
    {
        ThemeVariant variant;
        if (_highContrastMode)
        {
            variant = _isDarkMode ? SettingsAgent.HighContrastDark : SettingsAgent.HighContrastLight;
        }
        else
        {
            variant = _isDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        }
        
        _settingsAgent.SetTheme(variant);
    }


    public string FontSize
    {
        get => _settingsAgent.FontSize;
        set
        {
            if (_settingsAgent.FontSize != value)
            {
                _settingsAgent.FontSize = value;
                RaisePropertyChanged();
                _settingsAgent.SaveSettings();
            }
        }
    }

    public bool AutoSave
    {
        get => _settingsAgent.AutoSave;
        set
        {
            if (_settingsAgent.AutoSave != value)
            {
                _settingsAgent.AutoSave = value;
                RaisePropertyChanged();
                _settingsAgent.SaveSettings();
            }
        }
    }
    
    public bool ShowLineNumbers
    {
        get => _settingsAgent.ShowLineNumbers;
        set
        {
            if (_settingsAgent.ShowLineNumbers != value)
            {
                _settingsAgent.ShowLineNumbers = value;
                RaisePropertyChanged();
                _settingsAgent.SaveSettings();
            }
        }
    }
    
    public bool WordWrap
    {
        get => _settingsAgent.WordWrap;
        set
        {
            if (_settingsAgent.WordWrap != value)
            {
                _settingsAgent.WordWrap = value;
                RaisePropertyChanged();
                _settingsAgent.SaveSettings();
            }
        }
    }

    public string CsvDelimiter
    {
        get => _settingsAgent.CsvDelimiter;
        set
        {
            if (_settingsAgent.CsvDelimiter != value)
            {
                _settingsAgent.CsvDelimiter = value;
                RaisePropertyChanged();
                _settingsAgent.SaveSettings();
            }
        }
    }
    
    public bool TrimWhitespace
    {
        get => _settingsAgent.TrimWhitespace;
        set
        {
            if (_settingsAgent.TrimWhitespace != value)
            {
                _settingsAgent.TrimWhitespace = value;
                RaisePropertyChanged();
                _settingsAgent.SaveSettings();
            }
        }
    }

    public int RowHeight
    {
        get => _settingsAgent.RowHeight;
        set
        {
            if (_settingsAgent.RowHeight != value)
            {
                _settingsAgent.RowHeight = value;
                RaisePropertyChanged();
                _settingsAgent.SaveSettings();
            }
        }
    }

    public bool RenderWhitespace
    {
        get => _settingsAgent.RenderWhitespace;
        set
        {
            if (_settingsAgent.RenderWhitespace != value)
            {
                _settingsAgent.RenderWhitespace = value;
                RaisePropertyChanged();
                _settingsAgent.SaveSettings();
            }
        }
    }

    public bool ArrayDisplayMultiLine
    {
        get => _settingsAgent.ArrayDisplayMultiLine;
        set
        {
            if (_settingsAgent.ArrayDisplayMultiLine != value)
            {
                _settingsAgent.ArrayDisplayMultiLine = value;
                RaisePropertyChanged();
                _settingsAgent.SaveSettings();
            }
        }
    }

    public ObservableCollection<string> FontSizes { get; } = new ObservableCollection<string> { "12", "13", "14", "16", "18", "20", "24" };
    public ObservableCollection<string> Delimiters { get; } = new ObservableCollection<string> { "Comma (,)", "Semicolon (;)", "Tab (\\t)", "Pipe (|)" };

    public ICommand ToggleThemeCommand { get; }
}
