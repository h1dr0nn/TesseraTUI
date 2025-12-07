using System;
using System.Collections.Generic;
using System.Linq;

namespace Tessera.ViewModels;

/// <summary>
/// Manages spreadsheet-like selection for TableView.
/// Supports cell, row, column, and range selection with multi-select capabilities.
/// </summary>
public class SelectionModel : ViewModelBase
{
    public enum SelectionType { None, Cell, Row, Column, Range }

    private SelectionType _type = SelectionType.None;
    private readonly HashSet<int> _selectedRows = new();
    private readonly HashSet<int> _selectedColumns = new();
    private (int Row, int Col) _anchorCell = (-1, -1);
    private (int Row, int Col) _currentCell = (-1, -1);
    
    // Table bounds for clamping
    private int _rowCount;
    private int _columnCount;

    public event Action? SelectionChanged;

    // Properties
    public SelectionType Type
    {
        get => _type;
        private set
        {
            if (SetProperty(ref _type, value))
                NotifySelectionChanged();
        }
    }

    public IReadOnlySet<int> SelectedRows => _selectedRows;
    public IReadOnlySet<int> SelectedColumns => _selectedColumns;
    
    public (int Row, int Col) AnchorCell
    {
        get => _anchorCell;
        private set => SetProperty(ref _anchorCell, value);
    }
    
    public (int Row, int Col) CurrentCell
    {
        get => _currentCell;
        private set => SetProperty(ref _currentCell, value);
    }

    public bool HasSelection => _type != SelectionType.None;

    // Update bounds when table changes
    public void UpdateBounds(int rowCount, int columnCount)
    {
        _rowCount = rowCount;
        _columnCount = columnCount;
    }

    // Clear all selection
    public void Clear()
    {
        _selectedRows.Clear();
        _selectedColumns.Clear();
        _anchorCell = (-1, -1);
        _currentCell = (-1, -1);
        Type = SelectionType.None;
    }

    /// <summary>
    /// Select a single cell or extend selection to include it.
    /// </summary>
    public void SelectCell(int row, int col, bool extend = false)
    {
        row = ClampRow(row);
        col = ClampCol(col);

        if (!extend)
        {
            _selectedRows.Clear();
            _selectedColumns.Clear();
            AnchorCell = (row, col);
        }

        CurrentCell = (row, col);
        Type = SelectionType.Cell;
        NotifySelectionChanged();
    }

    /// <summary>
    /// Select an row. Supports extend (Shift) and toggle (Ctrl).
    /// </summary>
    public void SelectRow(int row, bool extend = false, bool toggle = false)
    {
        row = ClampRow(row);

        if (toggle)
        {
            // Toggle mode: add or remove from selection
            if (_selectedRows.Contains(row))
                _selectedRows.Remove(row);
            else
                _selectedRows.Add(row);
        }
        else if (extend && Type == SelectionType.Row && _selectedRows.Count > 0)
        {
            // Extend mode: select range from anchor to current
            var anchor = _selectedRows.Min();
            _selectedRows.Clear();
            var start = Math.Min(anchor, row);
            var end = Math.Max(anchor, row);
            for (int i = start; i <= end; i++)
                _selectedRows.Add(i);
        }
        else
        {
            // Normal mode: clear and select single row
            _selectedRows.Clear();
            _selectedColumns.Clear();
            _selectedRows.Add(row);
        }

        _anchorCell = (row, 0);
        _currentCell = (row, _columnCount - 1);
        Type = _selectedRows.Count > 0 ? SelectionType.Row : SelectionType.None;
        NotifySelectionChanged();
    }

    /// <summary>
    /// Select a column. Supports extend (Shift) and toggle (Ctrl).
    /// </summary>
    public void SelectColumn(int col, bool extend = false, bool toggle = false)
    {
        col = ClampCol(col);

        if (toggle)
        {
            if (_selectedColumns.Contains(col))
                _selectedColumns.Remove(col);
            else
                _selectedColumns.Add(col);
        }
        else if (extend && Type == SelectionType.Column && _selectedColumns.Count > 0)
        {
            var anchor = _selectedColumns.Min();
            _selectedColumns.Clear();
            var start = Math.Min(anchor, col);
            var end = Math.Max(anchor, col);
            for (int i = start; i <= end; i++)
                _selectedColumns.Add(i);
        }
        else
        {
            _selectedRows.Clear();
            _selectedColumns.Clear();
            _selectedColumns.Add(col);
        }

        _anchorCell = (0, col);
        _currentCell = (_rowCount - 1, col);
        Type = _selectedColumns.Count > 0 ? SelectionType.Column : SelectionType.None;
        NotifySelectionChanged();
    }

    /// <summary>
    /// Select a rectangular range of cells.
    /// </summary>
    public void SelectRange(int startRow, int startCol, int endRow, int endCol)
    {
        startRow = ClampRow(startRow);
        startCol = ClampCol(startCol);
        endRow = ClampRow(endRow);
        endCol = ClampCol(endCol);

        _selectedRows.Clear();
        _selectedColumns.Clear();
        
        AnchorCell = (startRow, startCol);
        CurrentCell = (endRow, endCol);
        Type = SelectionType.Range;
        NotifySelectionChanged();
    }

    /// <summary>
    /// Toggle a single cell selection (Ctrl+Click).
    /// </summary>
    public void ToggleCell(int row, int col)
    {
        // For single cell toggle, we just select/deselect
        if (Type == SelectionType.Cell && _currentCell.Row == row && _currentCell.Col == col)
        {
            Clear();
        }
        else
        {
            SelectCell(row, col);
        }
    }

    /// <summary>
    /// Toggle row selection (Ctrl+Click on row header).
    /// </summary>
    public void ToggleRow(int row)
    {
        SelectRow(row, extend: false, toggle: true);
    }

    /// <summary>
    /// Toggle column selection (Ctrl+Click on column header).
    /// </summary>
    public void ToggleColumn(int col)
    {
        SelectColumn(col, extend: false, toggle: true);
    }

    /// <summary>
    /// Extend selection from anchor to target cell (Shift+Click).
    /// </summary>
    public void ExtendTo(int row, int col)
    {
        if (_anchorCell.Row < 0 || _anchorCell.Col < 0)
        {
            SelectCell(row, col);
            return;
        }
        
        SelectRange(_anchorCell.Row, _anchorCell.Col, row, col);
    }

    /// <summary>
    /// Select a range of rows (Shift+Click on row headers).
    /// </summary>
    public void SelectRowRange(int startRow, int endRow)
    {
        startRow = ClampRow(startRow);
        endRow = ClampRow(endRow);
        
        _selectedRows.Clear();
        _selectedColumns.Clear();
        
        for (int i = Math.Min(startRow, endRow); i <= Math.Max(startRow, endRow); i++)
        {
            _selectedRows.Add(i);
        }
        
        AnchorCell = (startRow, 0);
        CurrentCell = (endRow, _columnCount - 1);
        Type = SelectionType.Row;
        NotifySelectionChanged();
    }

    /// <summary>
    /// Select a range of columns (Shift+Click on column headers).
    /// </summary>
    public void SelectColumnRange(int startCol, int endCol)
    {
        startCol = ClampCol(startCol);
        endCol = ClampCol(endCol);
        
        _selectedRows.Clear();
        _selectedColumns.Clear();
        
        for (int i = Math.Min(startCol, endCol); i <= Math.Max(startCol, endCol); i++)
        {
            _selectedColumns.Add(i);
        }
        
        AnchorCell = (0, startCol);
        CurrentCell = (_rowCount - 1, endCol);
        Type = SelectionType.Column;
        NotifySelectionChanged();
    }

    /// <summary>
    /// Select all cells in the table.
    /// </summary>
    public void SelectAll()
    {
        if (_rowCount == 0 || _columnCount == 0)
        {
            Clear();
            return;
        }

        SelectRange(0, 0, _rowCount - 1, _columnCount - 1);
    }

    // Query methods

    /// <summary>
    /// Check if a specific cell is within the current selection.
    /// </summary>
    public bool IsCellSelected(int row, int col)
    {
        return Type switch
        {
            SelectionType.None => false,
            SelectionType.Cell => _currentCell.Row == row && _currentCell.Col == col,
            SelectionType.Row => _selectedRows.Contains(row),
            SelectionType.Column => _selectedColumns.Contains(col),
            SelectionType.Range => IsInRange(row, col),
            _ => false
        };
    }

    /// <summary>
    /// Check if an entire row is selected.
    /// </summary>
    public bool IsRowSelected(int row)
    {
        if (Type == SelectionType.Row)
            return _selectedRows.Contains(row);
        
        if (Type == SelectionType.Range)
        {
            var minRow = Math.Min(_anchorCell.Row, _currentCell.Row);
            var maxRow = Math.Max(_anchorCell.Row, _currentCell.Row);
            var minCol = Math.Min(_anchorCell.Col, _currentCell.Col);
            var maxCol = Math.Max(_anchorCell.Col, _currentCell.Col);
            
            // Row is fully selected if range spans all columns
            return row >= minRow && row <= maxRow && minCol == 0 && maxCol == _columnCount - 1;
        }

        return false;
    }

    /// <summary>
    /// Check if an entire column is selected.
    /// </summary>
    public bool IsColumnSelected(int col)
    {
        if (Type == SelectionType.Column)
            return _selectedColumns.Contains(col);
        
        if (Type == SelectionType.Range)
        {
            var minRow = Math.Min(_anchorCell.Row, _currentCell.Row);
            var maxRow = Math.Max(_anchorCell.Row, _currentCell.Row);
            var minCol = Math.Min(_anchorCell.Col, _currentCell.Col);
            var maxCol = Math.Max(_anchorCell.Col, _currentCell.Col);
            
            // Column is fully selected if range spans all rows
            return col >= minCol && col <= maxCol && minRow == 0 && maxRow == _rowCount - 1;
        }

        return false;
    }

    /// <summary>
    /// Check if cell is in the current range.
    /// </summary>
    private bool IsInRange(int row, int col)
    {
        var minRow = Math.Min(_anchorCell.Row, _currentCell.Row);
        var maxRow = Math.Max(_anchorCell.Row, _currentCell.Row);
        var minCol = Math.Min(_anchorCell.Col, _currentCell.Col);
        var maxCol = Math.Max(_anchorCell.Col, _currentCell.Col);

        return row >= minRow && row <= maxRow && col >= minCol && col <= maxCol;
    }

    /// <summary>
    /// Get all selected cells as (row, col) tuples.
    /// </summary>
    public IEnumerable<(int Row, int Col)> GetSelectedCells()
    {
        switch (Type)
        {
            case SelectionType.None:
                yield break;

            case SelectionType.Cell:
                yield return _currentCell;
                break;

            case SelectionType.Row:
                foreach (var row in _selectedRows)
                    for (int col = 0; col < _columnCount; col++)
                        yield return (row, col);
                break;

            case SelectionType.Column:
                foreach (var col in _selectedColumns)
                    for (int row = 0; row < _rowCount; row++)
                        yield return (row, col);
                break;

            case SelectionType.Range:
                var minRow = Math.Min(_anchorCell.Row, _currentCell.Row);
                var maxRow = Math.Max(_anchorCell.Row, _currentCell.Row);
                var minCol = Math.Min(_anchorCell.Col, _currentCell.Col);
                var maxCol = Math.Max(_anchorCell.Col, _currentCell.Col);

                for (int row = minRow; row <= maxRow; row++)
                    for (int col = minCol; col <= maxCol; col++)
                        yield return (row, col);
                break;
        }
    }

    /// <summary>
    /// Get the bounding rectangle of the current selection.
    /// </summary>
    public (int MinRow, int MinCol, int MaxRow, int MaxCol) GetSelectionBounds()
    {
        switch (Type)
        {
            case SelectionType.None:
                return (-1, -1, -1, -1);

            case SelectionType.Cell:
                return (_currentCell.Row, _currentCell.Col, _currentCell.Row, _currentCell.Col);

            case SelectionType.Row:
                if (_selectedRows.Count == 0) return (-1, -1, -1, -1);
                return (_selectedRows.Min(), 0, _selectedRows.Max(), _columnCount - 1);

            case SelectionType.Column:
                if (_selectedColumns.Count == 0) return (-1, -1, -1, -1);
                return (0, _selectedColumns.Min(), _rowCount - 1, _selectedColumns.Max());

            case SelectionType.Range:
                var minRow = Math.Min(_anchorCell.Row, _currentCell.Row);
                var maxRow = Math.Max(_anchorCell.Row, _currentCell.Row);
                var minCol = Math.Min(_anchorCell.Col, _currentCell.Col);
                var maxCol = Math.Max(_anchorCell.Col, _currentCell.Col);
                return (minRow, minCol, maxRow, maxCol);

            default:
                return (-1, -1, -1, -1);
        }
    }

    // Helpers
    private int ClampRow(int row) => _rowCount > 0 ? Math.Clamp(row, 0, _rowCount - 1) : 0;
    private int ClampCol(int col) => _columnCount > 0 ? Math.Clamp(col, 0, _columnCount - 1) : 0;

    private void NotifySelectionChanged()
    {
        SelectionChanged?.Invoke();
        RaisePropertyChanged(nameof(HasSelection));
        RaisePropertyChanged(nameof(SelectedRows));
        RaisePropertyChanged(nameof(SelectedColumns));
    }
}
