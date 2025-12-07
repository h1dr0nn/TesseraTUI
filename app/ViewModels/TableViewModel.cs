using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Controls.ApplicationLifetimes;
using Tessera.Agents;
using Tessera.Core.Agents;
using Tessera.Core.Models;
using Tessera.Utils;

namespace Tessera.ViewModels;

public class TableViewModel : WorkspaceViewModel
{
    private readonly UIToastAgent _toastAgent = new();
    private readonly TableViewAgent _tableViewAgent;
    private readonly SelectionModel _selection = new();

    private readonly SettingsAgent _settingsAgent;

    public TableViewModel(SettingsAgent settingsAgent, DataSyncAgent? dataSyncAgent = null, HistoryAgent? historyAgent = null)
    {
        _settingsAgent = settingsAgent;
        var (table, schema, json, validator, jsonAgent) = SampleDataFactory.CreateEmptyWorkspace();
        var sync = dataSyncAgent ?? new DataSyncAgent(table, schema, json, validator, jsonAgent);
        var history = historyAgent ?? new HistoryAgent();

        _tableViewAgent = new TableViewAgent(_settingsAgent, sync, history);
        _tableViewAgent.TableChanged += SyncFromModel;

        Columns = new ObservableCollection<TableColumnViewModel>(
            _tableViewAgent.Table.Columns.Select((c, i) => new TableColumnViewModel(c.Name, i, _tableViewAgent)));
        Rows = BuildRowsFromModel();
        
        // Initialize selection bounds
        _selection.UpdateBounds(_tableViewAgent.Table.Rows.Count, _tableViewAgent.Table.Columns.Count);
        _selection.SelectionChanged += OnSelectionChanged;

        // Existing commands
        CopyCommand = new DelegateCommand(async _ => await CopyAsync());
        PasteCommand = new DelegateCommand(async _ => await PasteAsync());
        UndoCommand = new DelegateCommand(_ => Undo());
        RedoCommand = new DelegateCommand(_ => Redo());
        AddRowCommand = new DelegateCommand(_ => AddRow());
        AddColumnCommand = new DelegateCommand(_ => AddColumn());
        
        DeleteRowCommand = new DelegateCommand(_ => DeleteRow());
        DeleteColumnCommand = new DelegateCommand(_ => DeleteColumn());
        
        InsertRowAboveCommand = new DelegateCommand(_ => InsertRow(true));
        InsertRowBelowCommand = new DelegateCommand(_ => InsertRow(false));
        InsertColumnLeftCommand = new DelegateCommand(_ => InsertColumn(true));
        InsertColumnRightCommand = new DelegateCommand(_ => InsertColumn(false));
        
        // New selection commands
        SelectRowCommand = new DelegateCommand(_ => SelectCurrentRow());
        SelectColumnCommand = new DelegateCommand(_ => SelectCurrentColumn());
        SelectAllCommand = new DelegateCommand(_ => _selection.SelectAll());
        ClearSelectionCommand = new DelegateCommand(_ => _selection.Clear());
        ClearCellsCommand = new DelegateCommand(_ => ClearSelectedCells());
    }

    public override string Title => "Table View";

    // Table icon geometry (mdi-table)
    public override string IconName => "mdi-table";

    public override string Subtitle => "Edit and inspect CSV rows";

    public ObservableCollection<TableColumnViewModel> Columns { get; }

    public ObservableCollection<TableRowViewModel> Rows 
    { 
        get => _rows; 
        private set => SetProperty(ref _rows, value); 
    }
    private ObservableCollection<TableRowViewModel> _rows = new();

    public UIToastAgent ToastAgent => _toastAgent;
    
    public SettingsAgent Settings => _settingsAgent;
    
    /// <summary>
    /// The selection model for multi-cell, row, column selection.
    /// </summary>
    public SelectionModel Selection => _selection;

    // Legacy properties for backward compatibility with existing code
    public int SelectedRowIndex
    {
        get => _selection.CurrentCell.Row;
        set => _selection.SelectCell(value, _selection.CurrentCell.Col);
    }

    public int SelectedColumnIndex
    {
        get => _selection.CurrentCell.Col;
        set => _selection.SelectCell(_selection.CurrentCell.Row, value);
    }

    public ICommand CopyCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand AddRowCommand { get; }
    public ICommand AddColumnCommand { get; }
    
    public ICommand DeleteRowCommand { get; }
    public ICommand DeleteColumnCommand { get; }
    public ICommand InsertRowAboveCommand { get; }
    public ICommand InsertRowBelowCommand { get; }
    public ICommand InsertColumnLeftCommand { get; }
    public ICommand InsertColumnRightCommand { get; }
    
    // New selection commands
    public ICommand SelectRowCommand { get; }
    public ICommand SelectColumnCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand ClearCellsCommand { get; }

    public void SyncFromModel()
    {
        Console.WriteLine("[TableViewModel] Syncing from model...");
        
        var tableColumns = _tableViewAgent.Table.Columns;
        bool columnsChanged = Columns.Count != tableColumns.Count;
        if (!columnsChanged)
        {
            for (int i = 0; i < tableColumns.Count; i++)
            {
                if (Columns[i].Header != tableColumns[i].Name)
                {
                    columnsChanged = true;
                    break;
                }
            }
        }

        if (columnsChanged)
        {
            Columns.Clear();
            Console.WriteLine($"[TableViewModel] Rebuilding columns. Count: {tableColumns.Count}");
            for (int i = 0; i < tableColumns.Count; i++)
            {
                Columns.Add(new TableColumnViewModel(tableColumns[i].Name, i, _tableViewAgent));
            }
        }

        // Rebuild Rows - REPLACE collection to avoid O(N) notification crash
        Console.WriteLine($"[TableViewModel] Rebuilding rows. Count: {_tableViewAgent.Table.Rows.Count}");
        var newRows = new ObservableCollection<TableRowViewModel>();
        
        foreach (var rowModel in _tableViewAgent.Table.Rows.Select((r, i) => (Model: r, Index: i)))
        {
            var cells = new List<TableCellViewModel>();
            for (var colIndex = 0; colIndex < _tableViewAgent.Table.Columns.Count; colIndex++)
            {
                var cellValue = colIndex < rowModel.Model.Cells.Count ? rowModel.Model.Cells[colIndex] : "";
                var column = colIndex < Columns.Count ? Columns[colIndex] : null;
                cells.Add(new TableCellViewModel(_tableViewAgent, rowModel.Index, colIndex, cellValue, column));
            }

            newRows.Add(new TableRowViewModel(rowModel.Index, new ObservableCollection<TableCellViewModel>(cells)));
        }
        
        Rows = newRows;
        
        // Update selection bounds after table data changes
        _selection.UpdateBounds(_tableViewAgent.Table.Rows.Count, _tableViewAgent.Table.Columns.Count);
        
        Console.WriteLine("[TableViewModel] Sync complete.");
    }
    
    public void UpdateSelection(int rowIndex, int columnIndex)
    {
        _selection.SelectCell(rowIndex, columnIndex);
    }
    
    /// <summary>
    /// Called when selection changes, for UI refresh triggers.
    /// </summary>
    private void OnSelectionChanged()
    {
        RaisePropertyChanged(nameof(Selection));
        RaisePropertyChanged(nameof(SelectedRowIndex));
        RaisePropertyChanged(nameof(SelectedColumnIndex));
        
        // Fire event for view to refresh highlights
        SelectionVisualChanged?.Invoke();
    }
    
    /// <summary>
    /// Event fired when selection visual needs to be updated.
    /// </summary>
    public event Action? SelectionVisualChanged;
    
    /// <summary>
    /// Select the entire row where the current cell is located.
    /// </summary>
    private void SelectCurrentRow()
    {
        var current = _selection.CurrentCell;
        if (current.Row >= 0)
        {
            _selection.SelectRow(current.Row);
        }
    }
    
    /// <summary>
    /// Select the entire column where the current cell is located.
    /// </summary>
    private void SelectCurrentColumn()
    {
        var current = _selection.CurrentCell;
        if (current.Col >= 0)
        {
            _selection.SelectColumn(current.Col);
        }
    }
    
    /// <summary>
    /// Clear content of all selected cells.
    /// </summary>
    private void ClearSelectedCells()
    {
        var cells = _selection.GetSelectedCells().ToList();
        if (cells.Count == 0) return;
        
        foreach (var (row, col) in cells)
        {
            if (row >= 0 && row < Rows.Count && col >= 0 && col < Columns.Count)
            {
                var cellVm = Rows[row].Cells[col];
                cellVm.Value = "";
            }
        }
        
        _toastAgent.ShowToast($"Cleared {cells.Count} cells", ToastLevel.Success);
    }

    private ObservableCollection<TableRowViewModel> BuildRowsFromModel()
    {
        var rows = new ObservableCollection<TableRowViewModel>();
        for (var rowIndex = 0; rowIndex < _tableViewAgent.Table.Rows.Count; rowIndex++)
        {
            var rowModel = _tableViewAgent.Table.Rows[rowIndex];
            var cells = new List<TableCellViewModel>();
            for (var colIndex = 0; colIndex < _tableViewAgent.Table.Columns.Count; colIndex++)
            {
                var column = colIndex < Columns.Count ? Columns[colIndex] : null;
                cells.Add(new TableCellViewModel(_tableViewAgent, rowIndex, colIndex, rowModel.Cells[colIndex], column));
            }

            rows.Add(new TableRowViewModel(rowIndex, new ObservableCollection<TableCellViewModel>(cells)));
        }

        return rows;
    }
    
    private async Task CopyAsync()
    {
        var clipboard = GetClipboard();
        if (clipboard is null) return;
        
        // Use the new selection model to get all selected cells
        var cells = _selection.GetSelectedCells().ToList();
        if (cells.Count == 0) return;

        var content = _tableViewAgent.CopySelection(cells);
        await clipboard.SetTextAsync(content);
        _toastAgent.ShowToast($"Copied {cells.Count} cells", ToastLevel.Info);
    }

    private async Task PasteAsync()
    {
        var clipboard = GetClipboard();
        if (clipboard is null) return;
        if (SelectedRowIndex < 0 || SelectedColumnIndex < 0) return;

        var text = await clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text)) return;

        var parsed = ClipboardCsvHelper.Parse(text, _settingsAgent.DelimiterChar);

        if (_tableViewAgent.TryPaste(SelectedRowIndex, SelectedColumnIndex, parsed, out var error))
        {
            _toastAgent.ShowToast("Pasted data", ToastLevel.Success);
        }
        else if (!string.IsNullOrEmpty(error))
        {
            Status = WorkspaceStatus.Error;
            StatusMessage = error;
            _toastAgent.ShowToast(error, ToastLevel.Error);
        }
    }

    private void Undo()
    {
        if (_tableViewAgent.TryUndo(out var error))
        {
            Status = WorkspaceStatus.Editing;
            StatusMessage = "Undid change";
        }
        else if (error != null)
        {
            Status = WorkspaceStatus.Error;
            StatusMessage = error;
            _toastAgent.ShowToast(error, ToastLevel.Error);
        }
    }

    private void Redo()
    {
        if (_tableViewAgent.TryRedo(out var error))
        {
            Status = WorkspaceStatus.Editing;
            StatusMessage = "Redid change";
        }
        else if (error != null)
        {
            Status = WorkspaceStatus.Error;
            StatusMessage = error;
            _toastAgent.ShowToast(error, ToastLevel.Error);
        }
    }

    private void AddRow()
    {
        try
        {
            var newRow = new List<string?>();
            for (int i = 0; i < _tableViewAgent.Table.Columns.Count; i++) newRow.Add("");
            _tableViewAgent.Table.Rows.Add(new RowModel(newRow));
            _tableViewAgent.RefreshData();
            _toastAgent.ShowToast($"Added row {_tableViewAgent.Table.Rows.Count}", ToastLevel.Success);
        }
        catch (Exception ex)
        {
            _toastAgent.ShowToast($"Error adding row: {ex.Message}", ToastLevel.Error);
        }
    }

    private void AddColumn()
    {
        try
        {
            var newColumnName = $"Column{_tableViewAgent.Table.Columns.Count + 1}";
            _tableViewAgent.Schema.Columns.Add(new ColumnSchema(name: newColumnName, type: DataType.String, isNullable: true));
            _tableViewAgent.Table.Columns.Add(new ColumnModel(newColumnName));
            foreach (var row in _tableViewAgent.Table.Rows) row.Cells.Add("");
            Columns.Add(new TableColumnViewModel(newColumnName, Columns.Count));
            _tableViewAgent.RefreshData();
            _toastAgent.ShowToast($"Added column: {newColumnName}", ToastLevel.Success);
        }
        catch (Exception ex)
        {
            _toastAgent.ShowToast($"Error adding column: {ex.Message}", ToastLevel.Error);
        }
    }

    // ... commands implementations ...
    
    private void DeleteRow()
    {
        if (SelectedRowIndex < 0 || SelectedRowIndex >= _tableViewAgent.Table.Rows.Count) return;
        
        try
        {
             _tableViewAgent.Table.Rows.RemoveAt(SelectedRowIndex);
             _tableViewAgent.RefreshData();
             _toastAgent.ShowToast("Deleted row", ToastLevel.Success);
        }
        catch (Exception ex) { _toastAgent.ShowToast($"Error deleting row: {ex.Message}", ToastLevel.Error); }
    }

    private void DeleteColumn()
    {
        if (SelectedColumnIndex < 0 || SelectedColumnIndex >= _tableViewAgent.Table.Columns.Count) return;
        
        try 
        {
            _tableViewAgent.Schema.Columns.RemoveAt(SelectedColumnIndex);
            _tableViewAgent.Table.Columns.RemoveAt(SelectedColumnIndex);
            foreach(var row in _tableViewAgent.Table.Rows)
            {
                if (SelectedColumnIndex < row.Cells.Count) row.Cells.RemoveAt(SelectedColumnIndex);
            }
            SyncFromModel(); // Column deletion is complex for binding, explicit sync first
            _tableViewAgent.RefreshData();
            _toastAgent.ShowToast("Deleted column", ToastLevel.Success);
        }
        catch (Exception ex) { _toastAgent.ShowToast($"Error deleting column: {ex.Message}", ToastLevel.Error); }
    }

    private void InsertRow(bool above)
    {
        int index = SelectedRowIndex;
        if (index < 0) index = _tableViewAgent.Table.Rows.Count; // Default to end
        if (!above) index++; // Below

        try
        {
             var newRow = new List<string?>();
             for (int i = 0; i < _tableViewAgent.Table.Columns.Count; i++) newRow.Add("");
             
             if (index >= _tableViewAgent.Table.Rows.Count) _tableViewAgent.Table.Rows.Add(new RowModel(newRow));
             else _tableViewAgent.Table.Rows.Insert(index, new RowModel(newRow));
             
             SyncFromModel(); 
             _tableViewAgent.RefreshData();
             _toastAgent.ShowToast("Inserted row", ToastLevel.Success);
        }
         catch (Exception ex) { _toastAgent.ShowToast($"Error inserting row: {ex.Message}", ToastLevel.Error); }
    }
    
    private void InsertColumn(bool left)
    {
        int index = SelectedColumnIndex;
        if (index < 0) index = _tableViewAgent.Table.Columns.Count;
        if (!left) index++; // Right
        
        try {
            var newName = $"Column{_tableViewAgent.Table.Columns.Count + 1}";
            // Insert into schema and table
            var colSchema = new ColumnSchema(newName, DataType.String, true);
            var colModel = new ColumnModel(newName);
            
            if (index >= _tableViewAgent.Table.Columns.Count)
            {
                _tableViewAgent.Schema.Columns.Add(colSchema);
                _tableViewAgent.Table.Columns.Add(colModel);
                foreach(var row in _tableViewAgent.Table.Rows) row.Cells.Add("");
            }
            else 
            {
                _tableViewAgent.Schema.Columns.Insert(index, colSchema);
                _tableViewAgent.Table.Columns.Insert(index, colModel);
                foreach(var row in _tableViewAgent.Table.Rows) row.Cells.Insert(index, "");
            }
            
            SyncFromModel();
            _tableViewAgent.RefreshData();
            _toastAgent.ShowToast("Inserted column", ToastLevel.Success);
        }
        catch (Exception ex) { _toastAgent.ShowToast($"Error inserting column: {ex.Message}", ToastLevel.Error); }
    }
    private IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return window.Clipboard;
        }

        return null;
    }
}

public class TableColumnViewModel : ViewModelBase
{
    private string _header;
    private bool _isRenaming;
    private readonly TableViewAgent? _agent;

    public TableColumnViewModel(string header, int index, TableViewAgent? agent = null)
    {
        _header = header;
        Index = index;
        _agent = agent;
        StartRenameCommand = new DelegateCommand(_ => IsRenaming = true);
    }

    public string Header 
    { 
        get => _header;
        set
        {
            if (SetProperty(ref _header, value))
            {
                // Propagate to agent if available
                if (_agent != null)
                {
                    // Update Schema
                    if (Index < _agent.Schema.Columns.Count)
                    {
                        var oldName = _agent.Schema.Columns[Index].Name;
                        _agent.Schema.Columns[Index] = new ColumnSchema(value, _agent.Schema.Columns[Index].Type, _agent.Schema.Columns[Index].IsNullable);
                        
                        // Update Table Column Model
                        if (Index < _agent.Table.Columns.Count)
                        {
                            _agent.Table.Columns[Index] = new ColumnModel(value);
                        }
                        
                        _agent.NotifyTableChanged(); 
                    }
                }
            }
        }
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        set => SetProperty(ref _isRenaming, value);
    }
    
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    
    private double _width = 120;
    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }
    
    public ICommand StartRenameCommand { get; }

    public int Index { get; }
}

public class TableRowViewModel
{
    public TableRowViewModel(int index, ObservableCollection<TableCellViewModel> cells)
    {
        Index = index;
        Cells = cells;
    }

    public int Index { get; }

    public ObservableCollection<TableCellViewModel> Cells { get; }
}

public class TableCellViewModel : ViewModelBase
{
    private readonly TableViewAgent _tableViewAgent;
    private readonly int _rowIndex;
    private readonly int _columnIndex;
    private string? _value;
    private string? _errorMessage;
    private bool _suppressCommit;
    
    // Reference to parent column for width binding
    public TableColumnViewModel? Column { get; }

    public TableCellViewModel(TableViewAgent tableViewAgent, int rowIndex, int columnIndex, string? value, TableColumnViewModel? column = null)
    {
        _tableViewAgent = tableViewAgent;
        _rowIndex = rowIndex;
        _columnIndex = columnIndex;
        _value = value;
        Column = column;
    }

    public string? Value
    {
        get => _value;
        set
        {
            if (_suppressCommit)
            {
                SetProperty(ref _value, value);
                return;
            }

            var previous = _value;
            if (!SetProperty(ref _value, value))
            {
                return;
            }

            if (_tableViewAgent.TryCommitEdit(_rowIndex, _columnIndex, value, out var normalized, out var error))
            {
                _suppressCommit = true;
                SetProperty(ref _value, normalized);
                _suppressCommit = false;
                ErrorMessage = null;
            }
            else
            {
                _suppressCommit = true;
                SetProperty(ref _value, previous);
                _suppressCommit = false;
                ErrorMessage = error;
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                RaisePropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public void RefreshValue(string? value)
    {
        _suppressCommit = true;
        SetProperty(ref _value, value);
        _suppressCommit = false;
    }

    public void ClearError()
    {
        ErrorMessage = null;
    }
}
