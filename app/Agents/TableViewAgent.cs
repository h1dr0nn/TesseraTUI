using System;
using System.Collections.Generic;
using Tessera.Core.Agents;
using Tessera.Core.Models;

namespace Tessera.Agents;

public class TableViewAgent
{
    private readonly DataSyncAgent _dataSyncAgent;
    private readonly HistoryAgent _historyAgent;

    public TableViewAgent(DataSyncAgent dataSyncAgent, HistoryAgent historyAgent)
    {
        _dataSyncAgent = dataSyncAgent;
        _historyAgent = historyAgent;
    }

    public event Action? TableChanged
    {
        add => _dataSyncAgent.TableChanged += value;
        remove => _dataSyncAgent.TableChanged -= value;
    }

    public TableModel Table => _dataSyncAgent.Table;

    public SchemaModel Schema => _dataSyncAgent.Schema;

    public bool TryCommitEdit(int rowIndex, int columnIndex, string? newValue, out string? normalizedValue, out string? errorMessage)
    {
        var oldValue = _dataSyncAgent.Table.Rows[rowIndex].Cells[columnIndex];
        var success = _dataSyncAgent.TryUpdateCell(rowIndex, columnIndex, newValue, out normalizedValue, out errorMessage);
        if (success)
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

        _dataSyncAgent.TryUpdateCell(change.RowIndex, change.ColumnIndex, change.OldValue, out _, out _);
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

        _dataSyncAgent.TryUpdateCell(change.RowIndex, change.ColumnIndex, change.NewValue, out _, out _);
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
        var lines = new List<string>();

        foreach (var rowIndex in orderedRows)
        {
            var cols = lookup[rowIndex];
            var orderedCols = new List<int>(cols.Keys);
            orderedCols.Sort();
            var values = new List<string>();
            foreach (var colIndex in orderedCols)
            {
                values.Add(cols[colIndex] ?? string.Empty);
            }

            lines.Add(string.Join(',', values));
        }

        return string.Join('\n', lines);
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
