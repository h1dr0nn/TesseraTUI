using System;
using System.Collections.Generic;
using Avalonia.Styling;
using System.IO;
using System.Text.Json;

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Tessera.Agents;

public class SettingsAgent : INotifyPropertyChanged
{
    public static readonly ThemeVariant HighContrastDark = new("HighContrastDark", ThemeVariant.Dark);
    public static readonly ThemeVariant HighContrastLight = new("HighContrastLight", ThemeVariant.Light);

    public event Action<ThemeVariant>? ThemeChanged;
    public event Action? SettingsChanged;

    public ThemeVariant PreferredTheme { get; private set; } = ThemeVariant.Light;

    // Persisted Settings
    private string _fontSize = "14";
    public string FontSize 
    { 
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }

    private bool _autoSave = true;
    public bool AutoSave
    {
        get => _autoSave;
        set => SetProperty(ref _autoSave, value);
    }

    private bool _showLineNumbers = true;
    public bool ShowLineNumbers
    {
         get => _showLineNumbers;
         set => SetProperty(ref _showLineNumbers, value);
    }

    private bool _wordWrap = false;
    public bool WordWrap
    {
         get => _wordWrap;
         set => SetProperty(ref _wordWrap, value);
    }
    
    private string _csvDelimiter = "Comma (,)";
    public string CsvDelimiter
    {
         get => _csvDelimiter;
         set => SetProperty(ref _csvDelimiter, value);
    }

    private bool _trimWhitespace = true;
    public bool TrimWhitespace
    {
         get => _trimWhitespace;
         set => SetProperty(ref _trimWhitespace, value);
    }

    private int _rowHeight = 0; // 0 = Auto
    public int RowHeight
    {
         get => _rowHeight;
         set => SetProperty(ref _rowHeight, value);
    }

    private bool _renderWhitespace = false;
    public bool RenderWhitespace
    {
        get => _renderWhitespace;
        set => SetProperty(ref _renderWhitespace, value);
    }

    private bool _arrayDisplayMultiLine = true;
    public bool ArrayDisplayMultiLine
    {
        get => _arrayDisplayMultiLine;
        set => SetProperty(ref _arrayDisplayMultiLine, value);
    }

    private bool _showCsvJsonOnly = true;
    public bool ShowCsvJsonOnly
    {
        get => _showCsvJsonOnly;
        set => SetProperty(ref _showCsvJsonOnly, value);
    }

    public char DelimiterChar
    {
        get
        {
            if (CsvDelimiter.Contains("Semicolon")) return ';';
            if (CsvDelimiter.Contains("Tab")) return '\t';
            if (CsvDelimiter.Contains("Pipe")) return '|';
            return ',';
        }
    }

    private readonly string _configPath;

    public SettingsAgent()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appData, "TesseraTUI");
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
        _configPath = Path.Combine(configDir, "settings.json");
        LoadSettings();
    }

    public void SetTheme(ThemeVariant variant)
    {
        PreferredTheme = variant;
        if (Avalonia.Application.Current != null)
        {
            Avalonia.Application.Current.RequestedThemeVariant = variant;
        }
        ThemeChanged?.Invoke(variant);
        SaveSettings();
    }

    public void SaveSettings()
    {
        try
        {
            var model = new SettingsModel
            {
                Theme = PreferredTheme.Key?.ToString() ?? "Light",
                FontSize = FontSize,
                AutoSave = AutoSave,
                ShowLineNumbers = ShowLineNumbers,
                WordWrap = WordWrap,
                CsvDelimiter = CsvDelimiter,
                TrimWhitespace = TrimWhitespace,
                RowHeight = RowHeight,
                RenderWhitespace = RenderWhitespace,
                ArrayDisplayMultiLine = ArrayDisplayMultiLine,
                ShowCsvJsonOnly = ShowCsvJsonOnly
            };

            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
            SettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var model = JsonSerializer.Deserialize<SettingsModel>(json);
                if (model != null)
                {
                    // Apply Theme
                    if (model.Theme == "Dark") PreferredTheme = ThemeVariant.Dark;
                    else if (model.Theme == "Light") PreferredTheme = ThemeVariant.Light;
                    else if (model.Theme == "HighContrastDark") PreferredTheme = HighContrastDark;
                    else if (model.Theme == "HighContrastLight") PreferredTheme = HighContrastLight;
                    
                    if (Avalonia.Application.Current != null)
                    {
                        Avalonia.Application.Current.RequestedThemeVariant = PreferredTheme;
                    }

                    // Apply other settings
                    FontSize = model.FontSize ?? "14";
                    AutoSave = model.AutoSave;
                    ShowLineNumbers = model.ShowLineNumbers;
                    WordWrap = model.WordWrap;
                    CsvDelimiter = model.CsvDelimiter ?? "Comma (,)";
                    TrimWhitespace = model.TrimWhitespace;
                    RowHeight = model.RowHeight;
                    RenderWhitespace = model.RenderWhitespace;
                    ArrayDisplayMultiLine = model.ArrayDisplayMultiLine;
                    ShowCsvJsonOnly = model.ShowCsvJsonOnly;
                }
            }
        }
        catch (Exception ex)
        {
             Console.WriteLine($"Failed to load settings: {ex.Message}");
        }
    }

    private class SettingsModel
    {
        public string? Theme { get; set; }
        public string? FontSize { get; set; }
        public bool AutoSave { get; set; }
        public bool ShowLineNumbers { get; set; }
        public bool WordWrap { get; set; }
        public string? CsvDelimiter { get; set; }
        public bool TrimWhitespace { get; set; }
        public int RowHeight { get; set; }
        public bool RenderWhitespace { get; set; }
        public bool ArrayDisplayMultiLine { get; set; } = true;
        public bool ShowCsvJsonOnly { get; set; } = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
