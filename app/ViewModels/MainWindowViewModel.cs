using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Tessera.Agents;
using Tessera.Core.Agents;
using Tessera.Utils;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Tessera.Core.Models;
using System.Globalization;
using System.Threading.Tasks;

namespace Tessera.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsAgent _settingsAgent = new();
    private readonly UIToastAgent _toastAgent = new();
    private readonly NavigationAgent _navigationAgent = new();
    private readonly HistoryAgent _historyAgent = new();

    private string _currentFileName = "No file opened";
    private WorkspaceStatus _status = WorkspaceStatus.Idle;
    private string _statusMessage = "Idle";
    private bool _isSettingsOpen;
    private DispatcherTimer? _autoSaveTimer;
    private string _currentFilePath = "";

    public FileExplorerViewModel FileExplorer { get; }
    public SettingsViewModel SettingsViewModel { get; }
    
    public TableViewModel TableViewModel { get; }
    public SchemaViewModel SchemaViewModel { get; }
    public JsonViewModel JsonViewModel { get; }
    public GraphViewModel GraphViewModel { get; }

    public MainWindowViewModel()
    {
        _settingsAgent.ThemeChanged += ApplyTheme;

        var (table, schema, json, validator, jsonAgent) = SampleDataFactory.CreateEmptyWorkspace();
        _jsonAgent = jsonAgent;
        _dataSyncAgent = new DataSyncAgent(table, schema, json, validator, jsonAgent);
        
        // Initialize Child ViewModels
        FileExplorer = new FileExplorerViewModel();
        FileExplorer.FileSelected += OnFileSelected;
        
        // Tab Layout
        TableViewModel = new TableViewModel(_settingsAgent, _dataSyncAgent, _historyAgent);
        SchemaViewModel = new SchemaViewModel(_dataSyncAgent);
        JsonViewModel = new JsonViewModel(_dataSyncAgent, validator, jsonAgent, _toastAgent);
        GraphViewModel = new GraphViewModel(_dataSyncAgent, jsonAgent);

        _navigationAgent.RegisterView(TableViewModel);
        _navigationAgent.RegisterView(SchemaViewModel);
        _navigationAgent.RegisterView(JsonViewModel);
        _navigationAgent.RegisterView(GraphViewModel);
        _navigationAgent.ActiveViewChanged += SyncActiveViewState;

        SettingsViewModel = new SettingsViewModel(_settingsAgent);

        SaveCommand = new DelegateCommand(_ => SaveFile());
        ReloadCommand = new DelegateCommand(_ => OnReloadRequested());
        ToggleSettingsCommand = new DelegateCommand(_ => IsSettingsOpen = !IsSettingsOpen);
        ToggleViewCommand = new DelegateCommand(param => 
        {
            if (param is WorkspaceViewModel view) ToggleView(view);
        });
    }

    public ObservableCollection<WorkspaceViewModel> Views => _navigationAgent.Views;

    public WorkspaceViewModel? ActiveView
    {
        get => _navigationAgent.ActiveView;
        set => _navigationAgent.ActiveView = value;
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
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
                RaisePropertyChanged(nameof(StatusBrush));
                foreach (var view in Views)
                {
                    view.Status = value;
                }
            }
        }
    }

    public IBrush StatusBrush => Status switch
    {
        WorkspaceStatus.Editing => new SolidColorBrush(Color.Parse("#E9C46A")), // Warning/Editing
        WorkspaceStatus.Error => new SolidColorBrush(Color.Parse("#E76F51")),   // Error
        _ => new SolidColorBrush(Color.Parse("#2A9D8F"))                        // Accent/Idle
    };

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

    public ICommand ToggleSettingsCommand { get; }
    public ICommand ToggleViewCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand ReloadCommand { get; }

    public UIToastAgent ToastAgent => _toastAgent;

    public SettingsAgent Settings => _settingsAgent;

    public NavigationAgent Navigation => _navigationAgent;

    public event Action? ReloadRequested;

    private string _fileTextContent = "No file loaded.";
    public string FileTextContent
    {
        get => _fileTextContent;
        set => SetProperty(ref _fileTextContent, value);
    }

    private readonly DataSyncAgent _dataSyncAgent;
    private readonly JsonAgent _jsonAgent;

    private async void OnFileSelected(string path)
    {
        _currentFilePath = path;
        CurrentFileName = Path.GetFileName(path);
        
        try 
        {
            if (File.Exists(path))
            {
                var text = await File.ReadAllTextAsync(path);
                FileTextContent = text;
                RaisePropertyChanged(nameof(FileTextContent));

                await LoadDataFromTextAsync(text, isInitialLoad: true);
            }
        }
        catch (Exception ex)
        {
            FileTextContent = $"Error reading file: {ex.Message}";
            RaisePropertyChanged(nameof(FileTextContent));
            _toastAgent.ShowToast($"Error opening file: {ex.Message}", ToastLevel.Error);
        }
    }

    private async Task LoadDataFromTextAsync(string text, bool isInitialLoad = false)
    {
        try
        {
            var (tableModel, schemaModel) = await Task.Run(() => 
            {
                // 1. Try JSON Parsing first if it looks like JSON
                var trimmed = text.TrimStart();
                if (trimmed.StartsWith("[") || trimmed.StartsWith("{"))
                {
                    var jsonResult = _jsonAgent.Parse(text);
                    if (jsonResult.IsValid && jsonResult.Model != null)
                    {
                        var jsonSchema = InferSchemaFromJson(jsonResult.Model);
                        var jsonTable = _jsonAgent.BuildTableFromJson(jsonResult.Model, jsonSchema);
                        return (jsonTable, jsonSchema);
                    }
                }

                // 2. Fallback to CSV Parsing
                var rows = ClipboardCsvHelper.Parse(text, _settingsAgent.DelimiterChar);
                
                if (rows.Count > 0)
                {
                    // Assume first row is header
                    var headerRow = rows[0];
                    var dataRows = rows.Skip(1).ToList();

                    var columns = new List<ColumnModel>();
                    var columnSchemas = new List<ColumnSchema>();

                    for (int i = 0; i < headerRow.Count; i++)
                    {
                        var header = headerRow[i] ?? $"Column {i + 1}";
                        columns.Add(new ColumnModel(header));
                        
                        // Infer Data Type
                        var inferredType = InferColumnType(dataRows, i);
                        columnSchemas.Add(new ColumnSchema(header, inferredType, true)); 
                    }

                    var tableRows = new List<RowModel>();
                    foreach (var row in dataRows)
                    {
                        // Ensure row has correct number of cells
                        var cells = new List<string?>(row);
                        while (cells.Count < columns.Count) cells.Add(null);
                        while (cells.Count > columns.Count) cells.RemoveAt(cells.Count - 1);
                        
                        tableRows.Add(new RowModel(cells));
                    }
                    
                    return (new TableModel(columns, tableRows), new SchemaModel(columnSchemas));
                }
                
                return ((TableModel?)null, (SchemaModel?)null);
            });

            if (tableModel != null && schemaModel != null)
            {
                _dataSyncAgent.LoadNewData(tableModel, schemaModel);
                
                if (isInitialLoad)
                {
                     // _dataSyncAgent.NotifyTableChanged(); // Explicitly notify
                     // _dataSyncAgent.LoadNewData calls NotifyTableChanged internaly via TableChanged?.Invoke()
                     // but let's be safe as LoadNewData implementations vary
                     
                     _toastAgent.ShowToast($"Opened {CurrentFileName}", ToastLevel.Success);
                }
            }
            else
            {
                 if (isInitialLoad) _toastAgent.ShowToast("File is empty or format not recognized", ToastLevel.Warning);
            }
        }
        catch (Exception ex)
        {
            // If parsing fails during save, we might want to know
            if (!isInitialLoad)
            {
                _toastAgent.ShowToast($"Warning: Could not sync data model: {ex.Message}", ToastLevel.Warning);
            }
            else 
            {
                _toastAgent.ShowToast($"Error parsing file: {ex.Message}", ToastLevel.Error);
            }
        }
    }

    private SchemaModel InferSchemaFromJson(JsonModel model)
    {
        // 1. Collect all unique keys
        var keys = new List<string>();
        foreach (var record in model.Records)
        {
            foreach (var key in record.Keys)
            {
                if (!keys.Contains(key)) keys.Add(key);
            }
        }

        // 2. Infer types for each key
        var schemas = new List<ColumnSchema>();
        foreach (var key in keys)
        {
            var values = model.Records.Select(r => r.TryGetValue(key, out var v) ? v : null).ToList();
            var type = InferTypeFromValues(values);
            schemas.Add(new ColumnSchema(key, type, true));
        }

        return new SchemaModel(schemas);
    }

    private DataType InferTypeFromValues(List<object?> values)
    {
        bool canBeInt = true;
        bool canBeFloat = true;
        bool canBeBool = true;
        bool canBeDate = true;
        bool hasValues = false;

        foreach (var val in values)
        {
            if (val == null) continue;
            hasValues = true;

            // Strict Type Checks based on JSON types
            if (val is long) 
            {
                // Int fits in Float, forbids Bool/Date (unless ts?)
                canBeBool = false;
                canBeDate = false; 
                // Any number breaks date parsing usually unless distinct
            }
            else if (val is double)
            {
                canBeInt = false;
                canBeBool = false;
                canBeDate = false;
            }
            else if (val is bool)
            {
                canBeInt = false;
                canBeFloat = false;
                canBeDate = false;
            }
            else if (val is string s)
            {
                if (string.IsNullOrWhiteSpace(s)) continue;
                
                // If it's a string, it kills Int/Float/Bool direct typing unless we want to parse strings inside JSON?
                // Usually JSON is typed. If we see a string "123", should it be Int?
                // For now, let's assume if it is a string in JSON, it is a String or Date.
                canBeInt = false;
                canBeFloat = false;
                canBeBool = false;

                if (canBeDate && !DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    canBeDate = false;
                }
            }
            else
            {
                // Complex object or array -> Treat as String (serialized) or fail?
                // For this simple tabular view, let's treat as String.
                return DataType.String;
            }

            if (!canBeInt && !canBeFloat && !canBeBool && !canBeDate) return DataType.String;
        }

        if (!hasValues) return DataType.String;

        if (canBeInt) return DataType.Int;
        if (canBeFloat) return DataType.Float;
        if (canBeBool) return DataType.Bool;
        if (canBeDate) return DataType.Date;

        return DataType.String;
    }

    private void OnTableChanged()
    {
        if (_settingsAgent.AutoSave)
        {
            _autoSaveTimer?.Stop();
            _autoSaveTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (s, e) => 
            {
                _autoSaveTimer?.Stop();
                SaveFile(true);
            });
            _autoSaveTimer.Start();
        }
    }

    private async void SaveFile(bool isAutoSave = false)
    {
        if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath))
        {
            if (!isAutoSave) _toastAgent.ShowToast("No file to save", ToastLevel.Warning);
            return;
        }

        try
        {
            Status = WorkspaceStatus.Editing;
            StatusMessage = isAutoSave ? "Auto Saving..." : "Saving...";

            // CRITICAL: Allow active view to commit changes to the shared model before we save
            if (ActiveView != null)
            {
                await ActiveView.OnSaveAsync();
            }

            if (ActiveView is null)
            {
                // Save Plain Text content directly
                File.WriteAllText(_currentFilePath, FileTextContent);
                
                // CRITICAL FIX: Sync changes back to Data Model so other views (Table/Json) are updated
                await LoadDataFromTextAsync(FileTextContent, isInitialLoad: false);
            }
            else
            {
                // Save from Table Model
                var headers = _dataSyncAgent.Table.Columns.Select(c => c.Name).ToList();
                var rows = _dataSyncAgent.Table.Rows.Select(r => r.Cells).ToList();
                
                // Combine headers and rows
                var allData = new List<IEnumerable<string?>> { headers };
                allData.AddRange(rows);

                var csvContent = ClipboardCsvHelper.Serialize(allData, _settingsAgent.DelimiterChar);
                File.WriteAllText(_currentFilePath, csvContent);
                
                // Ensure Plain Text view is updated
                FileTextContent = csvContent;
            }

            Status = WorkspaceStatus.Idle;
            StatusMessage = "Saved";
            if (!isAutoSave) _toastAgent.ShowToast("File saved", ToastLevel.Success);
        }
        catch (Exception ex)
        {
            Status = WorkspaceStatus.Error;
            StatusMessage = "Save Failed";
            _toastAgent.ShowToast($"Save failed: {ex.Message}", ToastLevel.Error);
        }
    }
    
    public void ToggleView(WorkspaceViewModel view)
    {
        _navigationAgent.ToggleView(view);
    }

    private void OnSaveRequested()
    {
        SaveFile();
    }


    private void OnReloadRequested()
    {
        Status = WorkspaceStatus.Editing;
        StatusMessage = "Reload requested";
        _toastAgent.ShowToast("Reload triggered", ToastLevel.Warning);
        ReloadRequested?.Invoke();
        // Note: Status should be reset by the reload handler when complete
    }


    private void SyncActiveViewState(WorkspaceViewModel view)
    {
        view.CurrentFileName = CurrentFileName;
        view.Status = Status;
        view.StatusMessage = StatusMessage;
        
        // CRITICAL: Notify view that ActiveView has changed!
        RaisePropertyChanged(nameof(ActiveView));
    }

    private void ApplyTheme(ThemeVariant variant)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = variant;
        }
    }

    private DataType InferColumnType(IEnumerable<IList<string?>> rows, int colIndex)
    {
        bool canBeInt = true;
        bool canBeFloat = true;
        bool canBeBool = true;
        bool canBeDate = true;
        bool hasValues = false;

        foreach (var row in rows)
        {
            if (colIndex >= row.Count) continue;
            
            var val = row[colIndex];
            if (string.IsNullOrWhiteSpace(val)) continue;

            hasValues = true;

            if (canBeInt && !int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) canBeInt = false;
            
            if (canBeFloat) 
            {
                bool parsed = double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                if (!parsed && val.Contains(','))
                {
                    parsed = double.TryParse(val.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                }
                if (!parsed) canBeFloat = false;
            }
            if (canBeBool && !bool.TryParse(val, out _)) canBeBool = false;
            if (canBeDate && !DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) canBeDate = false;

            // Short circuit if everything failed
            if (!canBeInt && !canBeFloat && !canBeBool && !canBeDate) return DataType.String;
        }

        if (!hasValues) return DataType.String; // Default to string if empty

        if (canBeInt) return DataType.Int;
        if (canBeFloat) return DataType.Float;
        if (canBeBool) return DataType.Bool;
        if (canBeDate) return DataType.Date;

        return DataType.String;
    }
}
