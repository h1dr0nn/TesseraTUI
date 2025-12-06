using System.Collections.Generic;

namespace Tessera.Core.Agents;

public class HistoryAgent
{
    private readonly Stack<CellChange> _undoStack = new();
    private readonly Stack<CellChange> _redoStack = new();

    public void Record(CellChange change)
    {
        _undoStack.Push(change);
        _redoStack.Clear();
    }

    public CellChange? Undo()
    {
        if (_undoStack.Count == 0)
        {
            return null;
        }

        var change = _undoStack.Pop();
        _redoStack.Push(change);
        return change;
    }

    public CellChange? Redo()
    {
        if (_redoStack.Count == 0)
        {
            return null;
        }

        var change = _redoStack.Pop();
        _undoStack.Push(change);
        return change;
    }
}

public record CellChange(int RowIndex, int ColumnIndex, string? OldValue, string? NewValue);
