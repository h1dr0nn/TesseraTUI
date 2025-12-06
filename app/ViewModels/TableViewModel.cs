using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Input.Platform;
using Tessera.Agents;
using Tessera.Core.Agents;
using Tessera.Core.Models;
using Tessera.Utils;

namespace Tessera.ViewModels;

public class TableViewModel : WorkspaceViewModel
{
    private readonly UIToastAgent _toastAgent = new();
    private readonly TableViewAgent _tableViewAgent;
    private int _selectedRowIndex;
    private int _selectedColumnIndex;

    public TableViewModel(DataSyncAgent? dataSyncAgent = null, HistoryAgent? historyAgent = null)
    {
        var (table, schema, json, validator, jsonAgent) = SampleDataFactory.CreateWorkspace();
        var sync = dataSyncAgent ?? new DataSyncAgent(table, schema, json, validator, jsonAgent);
        var history = historyAgent ?? new HistoryAgent();

        _tableViewAgent = new TableViewAgent(sync, history);
        _tableViewAgent.TableChanged += SyncFromModel;

        Columns = new ObservableCollection<TableColumnViewModel>(
            _tableViewAgent.Table.Columns.Select((c, i) => new TableColumnViewModel(c.Name, i)));
        Rows = BuildRowsFromModel();

        CopyCommand = new DelegateCommand(async _ => await CopyAsync());
        PasteCommand = new DelegateCommand(async _ => await PasteAsync());
        UndoCommand = new DelegateCommand(_ => Undo());
        RedoCommand = new DelegateCommand(_ => Redo());
    }

    public override string Title => "Table View";

    public override string Subtitle => "Edit and inspect CSV rows";

    public ObservableCollection<TableColumnViewModel> Columns { get; }

    public ObservableCollection<TableRowViewModel> Rows { get; }

    public UIToastAgent ToastAgent => _toastAgent;

    public int SelectedRowIndex
    {
        get => _selectedRowIndex;
        set => SetProperty(ref _selectedRowIndex, value);
    }

    public int SelectedColumnIndex
    {
        get => _selectedColumnIndex;
        set => SetProperty(ref _selectedColumnIndex, value);
    }

    public ICommand CopyCommand { get; }

    public ICommand PasteCommand { get; }

    public ICommand UndoCommand { get; }

    public ICommand RedoCommand { get; }

    public void SyncFromModel()
    {
        Rows.Clear();
        foreach (var rowModel in _tableViewAgent.Table.Rows.Select((r, i) => (Model: r, Index: i)))
        {
            var cells = new List<TableCellViewModel>();
            for (var colIndex = 0; colIndex < _tableViewAgent.Table.Columns.Count; colIndex++)
            {
                cells.Add(new TableCellViewModel(_tableViewAgent, rowModel.Index, colIndex, rowModel.Model.Cells[colIndex]));
            }

            Rows.Add(new TableRowViewModel(rowModel.Index, new ObservableCollection<TableCellViewModel>(cells)));
        }
    }

    public void UpdateSelection(int rowIndex, int columnIndex)
    {
        SelectedRowIndex = rowIndex;
        SelectedColumnIndex = columnIndex;
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
                cells.Add(new TableCellViewModel(_tableViewAgent, rowIndex, colIndex, rowModel.Cells[colIndex]));
            }

            rows.Add(new TableRowViewModel(rowIndex, new ObservableCollection<TableCellViewModel>(cells)));
        }

        return rows;
    }

    private async Task CopyAsync()
    {
        if (Application.Current?.Clipboard is not IClipboard clipboard)
        {
            return;
        }

        if (SelectedRowIndex < 0 || SelectedColumnIndex < 0)
        {
            return;
        }

        var content = _tableViewAgent.CopySelection(new[] { (SelectedRowIndex, SelectedColumnIndex) });
        await clipboard.SetTextAsync(content);
        _toastAgent.ShowToast("Copied selection", ToastLevel.Info);
    }

    private async Task PasteAsync()
    {
        if (Application.Current?.Clipboard is not IClipboard clipboard)
        {
            return;
        }

        if (SelectedRowIndex < 0 || SelectedColumnIndex < 0)
        {
            return;
        }

        var text = await clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var parsed = ClipboardCsvHelper.Parse(text);

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
}

public class TableColumnViewModel
{
    public TableColumnViewModel(string header, int index)
    {
        Header = header;
        Index = index;
    }

    public string Header { get; }

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

    public TableCellViewModel(TableViewAgent tableViewAgent, int rowIndex, int columnIndex, string? value)
    {
        _tableViewAgent = tableViewAgent;
        _rowIndex = rowIndex;
        _columnIndex = columnIndex;
        _value = value;
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
