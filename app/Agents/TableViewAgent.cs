using System;
using System.Collections.Generic;
using System.Linq;
using Tessera.Core.Agents;
using Tessera.Core.Models;
using Tessera.Utils;

namespace Tessera.Agents;

public class TableViewAgent
{
    private readonly SettingsAgent _settingsAgent;
    private readonly DataSyncAgent _dataSyncAgent;
    private readonly HistoryAgent _historyAgent;

    public TableViewAgent(SettingsAgent settingsAgent, DataSyncAgent dataSyncAgent, HistoryAgent historyAgent)
    {
        _settingsAgent = settingsAgent;
        _dataSyncAgent = dataSyncAgent;
        _historyAgent = historyAgent;
    }

    public event Action? TableChanged
    {
        add => _dataSyncAgent.TableChanged += value;
        remove => _dataSyncAgent.TableChanged -= value;
    }
    
    public void NotifyTableChanged() => _dataSyncAgent.NotifyTableChanged();
    
    public void RefreshData()
    {
        _dataSyncAgent.RecalculateSchemaStats();
        _dataSyncAgent.NotifyTableChanged();
    }

    public TableModel Table => _dataSyncAgent.Table;

    public SchemaModel Schema => _dataSyncAgent.Schema;

    public bool TryCommitEdit(int rowIndex, int columnIndex, string? newValue, out string? normalizedValue, out string? errorMessage)
    {
        var oldValue = _dataSyncAgent.Table.Rows[rowIndex].Cells[columnIndex];
        
        if (_settingsAgent.TrimWhitespace && newValue != null)
        {
            newValue = newValue.Trim();
        }

        var success = _dataSyncAgent.TryUpdateCell(rowIndex, columnIndex, newValue, out normalizedValue, out errorMessage);
        if (success && !Equals(oldValue, normalizedValue))
        {
            _historyAgent.Record(new CellChange(rowIndex, columnIndex, oldValue, normalizedValue));
        }

        return success;
    }

    public bool TryUndo(out string? errorMessage)
    {
        errorMessage = null;
        var change = _historyAgent.Undo();
        if (change is null)
        {
            errorMessage = "Nothing to undo.";
            return false;
        }

        var success = _dataSyncAgent.TryUpdateCell(change.RowIndex, change.ColumnIndex, change.OldValue, out _, out var validationError);
        if (!success)
        {
            _historyAgent.CancelUndo(change);
            errorMessage = validationError ?? "Undo failed validation.";
            return false;
        }

        return true;
    }

    public bool TryRedo(out string? errorMessage)
    {
        errorMessage = null;
        var change = _historyAgent.Redo();
        if (change is null)
        {
            errorMessage = "Nothing to redo.";
            return false;
        }

        var success = _dataSyncAgent.TryUpdateCell(change.RowIndex, change.ColumnIndex, change.NewValue, out _, out var validationError);
        if (!success)
        {
            _historyAgent.CancelRedo(change);
            errorMessage = validationError ?? "Redo failed validation.";
            return false;
        }

        return true;
    }

    public string CopySelection(IEnumerable<(int Row, int Column)> cells)
    {
        var lookup = new Dictionary<int, Dictionary<int, string?>>();
        foreach (var cell in cells)
        {
            if (!lookup.TryGetValue(cell.Row, out var columns))
            {
                columns = new Dictionary<int, string?>();
                lookup[cell.Row] = columns;
            }

            columns[cell.Column] = _dataSyncAgent.Table.Rows[cell.Row].Cells[cell.Column];
        }

        var orderedRows = new List<int>(lookup.Keys);
        orderedRows.Sort();
        var values = orderedRows
            .Select(rowIndex =>
            {
                var columns = lookup[rowIndex];
                var orderedCols = new List<int>(columns.Keys);
                orderedCols.Sort();
                return orderedCols.Select(colIndex => columns[colIndex]).ToList();
            });

        return ClipboardCsvHelper.Serialize(values, _settingsAgent.DelimiterChar);
    }

    public bool TryPaste(int startRow, int startColumn, IList<IList<string?>> pastedCells, out string? errorMessage)
    {
        errorMessage = null;
        var currentRow = startRow;

        foreach (var row in pastedCells)
        {
            if (currentRow >= _dataSyncAgent.Table.Rows.Count)
            {
                break;
            }

            var currentColumn = startColumn;
            foreach (var value in row)
            {
                if (currentColumn >= _dataSyncAgent.Table.Columns.Count)
                {
                    break;
                }

                var existing = _dataSyncAgent.Table.Rows[currentRow].Cells[currentColumn];
                if (_dataSyncAgent.TryUpdateCell(currentRow, currentColumn, value, out var normalized, out var validationError))
                {
                    _historyAgent.Record(new CellChange(currentRow, currentColumn, existing, normalized));
                }
                else
                {
                    errorMessage = validationError;
                    return false;
                }

                currentColumn++;
            }

            currentRow++;
        }

        return true;
    }
}
