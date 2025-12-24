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

    public MainWindowViewModel(string[]? args = null)
    {
        _settingsAgent.ThemeChanged += ApplyTheme;

        var (table, schema, json, validator, jsonAgent) = SampleDataFactory.CreateEmptyWorkspace();
        _jsonAgent = jsonAgent;
        _dataSyncAgent = new DataSyncAgent(table, schema, json, validator, jsonAgent);
        _dataSyncAgent.ArrayDisplayMultiLine = _settingsAgent.ArrayDisplayMultiLine;
        
        // Subscribe to settings changes
        _settingsAgent.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SettingsAgent.ArrayDisplayMultiLine))
            {
                _dataSyncAgent.ArrayDisplayMultiLine = _settingsAgent.ArrayDisplayMultiLine;
            }
        };
        
        // Initialize Child ViewModels
        FileExplorer = new FileExplorerViewModel(_settingsAgent); // CHANGED
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

        // Handle Startup File
        if (args != null && args.Length > 0)
        {
            var initialFile = args[0];
            if (File.Exists(initialFile))
            {
                // Defer slightly to ensure UI binding is ready? 
                // Actually calling OnFileSelected is fine, it updates properties.
                // We might want to set RootPath of Explorer too?
                // For now, simple file open:
                OnFileSelected(initialFile);
                
                // Optionally open the folder in explorer too?
                // FileExplorer.LoadDirectory(Path.GetDirectoryName(initialFile));
            }
        }
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
                        var jsonTable = _jsonAgent.BuildTableFromJson(jsonResult.Model, jsonSchema, _settingsAgent.ArrayDisplayMultiLine);
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
                // Check if file is JSON
                var isJsonFile = _currentFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                
                if (isJsonFile)
                {
                    // Save as JSON - build JSON from table
                    var jsonModel = BuildSimpleJsonFromTable(_dataSyncAgent.Table, _dataSyncAgent.Schema);
                    var jsonContent = _jsonAgent.Serialize(jsonModel);
                    File.WriteAllText(_currentFilePath, jsonContent);
                    FileTextContent = jsonContent;
                }
                else
                {
                    // Save as CSV
                    var headers = _dataSyncAgent.Table.Columns.Select(c => c.Name).ToList();
                    var rows = _dataSyncAgent.Table.Rows.Select(r => r.Cells).ToList();
                    
                    var allData = new List<IEnumerable<string?>> { headers };
                    allData.AddRange(rows);

                    var csvContent = ClipboardCsvHelper.Serialize(allData, _settingsAgent.DelimiterChar);
                    File.WriteAllText(_currentFilePath, csvContent);
                    FileTextContent = csvContent;
                }
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

    /// <summary>
    /// Build JSON model from table with simple 1:1 row-to-record mapping.
    /// </summary>
    private JsonModel BuildSimpleJsonFromTable(TableModel table, SchemaModel schema)
    {
        var records = new List<Dictionary<string, object?>>();
        
        foreach (var row in table.Rows)
        {
            var record = new Dictionary<string, object?>();
            
            for (int i = 0; i < table.Columns.Count && i < row.Cells.Count; i++)
            {
                var colName = table.Columns[i].Name;
                var rawValue = row.Cells[i];
                var dataType = i < schema.Columns.Count ? schema.Columns[i].Type : DataType.String;
                
                record[colName] = ConvertCellToJsonValue(rawValue, dataType);
            }
            
            records.Add(record);
        }
        
        return new JsonModel(records);
    }
    
    private object? ConvertCellToJsonValue(string? value, DataType dataType)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        
        // First, check if value is a JSON array or object string (common for complex nested data)
        var trimmed = value.Trim();
        if (trimmed.StartsWith("[") || trimmed.StartsWith("{"))
        {
            try
            {
                // Try to parse as JSON
                using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                return ConvertJsonElement(doc.RootElement);
            }
            catch
            {
                // Not valid JSON, fall through to normal handling
            }
        }
        
        // Check for comma-separated JSON objects pattern: {x:1},{x:2},...
        if (trimmed.Contains("},{") || (trimmed.StartsWith("{") && trimmed.EndsWith("}")))
        {
            try
            {
                // Wrap in array brackets if needed
                var arrayStr = trimmed.StartsWith("[") ? trimmed : "[" + trimmed + "]";
                using var doc = System.Text.Json.JsonDocument.Parse(arrayStr);
                return ConvertJsonElement(doc.RootElement);
            }
            catch
            {
                // Not valid JSON, fall through
            }
        }
        
        switch (dataType)
        {
            case DataType.Int:
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longVal))
                    return longVal;
                break;
            case DataType.Float:
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleVal))
                    return doubleVal;
                if (value.Contains(',') && double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleVal2))
                    return doubleVal2;
                break;
            case DataType.Bool:
                if (bool.TryParse(value, out var boolVal))
                    return boolVal;
                break;
        }
        
        return value;
    }
    
    private object? ConvertJsonElement(System.Text.Json.JsonElement element)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = ConvertJsonElement(prop.Value);
                }
                return dict;
            case System.Text.Json.JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertJsonElement(item));
                }
                return list;
            case System.Text.Json.JsonValueKind.String:
                return element.GetString();
            case System.Text.Json.JsonValueKind.Number:
                if (element.TryGetInt64(out var l)) return l;
                return element.GetDouble();
            case System.Text.Json.JsonValueKind.True:
                return true;
            case System.Text.Json.JsonValueKind.False:
                return false;
            case System.Text.Json.JsonValueKind.Null:
                return null;
            default:
                return element.GetRawText();
        }
    }
}
