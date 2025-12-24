using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tessera.ViewModels;

namespace Tessera.Controls;

/// <summary>
/// Custom spreadsheet-like grid with cell selection support.
/// </summary>
public partial class SpreadsheetGrid : UserControl
{
    private bool _isDragging;
    private int _dragStartRow = -1;
    private int _dragStartCol = -1;

    
    // Column resize state
    private bool _isResizingColumn;
    private int _resizeColumnIndex = -1;
    private double _resizeStartX;
    private double _resizeStartWidth;
    
    // Cell dimensions for overlay calculations (defaults, can be resized)
    private double[] _columnWidths = Array.Empty<double>();
    private const double DefaultCellWidth = 120;
    private const double CellHeight = 28;
    private const double MinColumnWidth = 40;
    
    private double CellWidth => DefaultCellWidth; // For overlay calculations
    
    public SpreadsheetGrid()
    {
        InitializeComponent();
        
        // Handle keyboard events
        KeyDown += OnGridKeyDown;
        TextInput += OnTextInput;
        Focusable = true;
        
        // Initialize column widths when data changes
        DataContextChanged += OnDataContextChanged;
        
        // Handle pointer pressed on grid to commit cell edits
        AddHandler(PointerPressedEvent, OnGridPointerPressed, RoutingStrategies.Tunnel);
    }
    
    /// <summary>
    /// Handles pointer pressed on the grid. Commits any active cell edit when clicking outside the edit TextBox.
    /// Uses Tunnel routing to catch the event before it reaches individual cells.
    /// </summary>
    private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Check if there's an active edit TextBox
        var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focusedElement is TextBox editingTextBox)
        {
            // Check if the click is on a different element (not the TextBox itself)
            var hitElement = e.Source as Visual;
            if (hitElement != null && !IsDescendantOf(hitElement, editingTextBox))
            {
                // Commit by removing focus from TextBox (triggers LostFocus)
                Focus();
            }
        }
    }
    
    private static bool IsDescendantOf(Visual element, Visual potentialAncestor)
    {
        Visual? current = element;
        while (current != null)
        {
            if (current == potentialAncestor) return true;
            current = current.GetVisualParent();
        }
        return false;
    }
    
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.Columns.CollectionChanged -= OnColumnsChanged;
            ViewModel.Columns.CollectionChanged += OnColumnsChanged;
        }

        InitializeColumnWidths();
    }

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InitializeColumnWidths();
        Selection?.Clear();
        UpdateSelectionOverlay();
    }
    
    private void InitializeColumnWidths()
    {
        if (ViewModel == null) return;
        
        var colCount = ViewModel.Columns.Count;
        if (colCount == 0) return;
        
        _columnWidths = new double[colCount];
        for (int i = 0; i < colCount; i++)
        {
            _columnWidths[i] = DefaultCellWidth;
        }
    }

    private TableViewModel? ViewModel => DataContext as TableViewModel;
    private SelectionModel? Selection => ViewModel?.Selection;

    #region Corner (Select All)
    
    private void OnCornerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        Selection?.SelectAll();
        UpdateSelectionOverlay();
        e.Handled = true;
    }
    
    #endregion

    #region Column Header Events
    
    private void OnColumnHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        if (sender is not Border { Tag: int colIndex }) return;
        
        var point = e.GetCurrentPoint(sender as Visual);
        var modifiers = e.KeyModifiers;
        
        if (modifiers.HasFlag(KeyModifiers.Shift) && Selection != null && Selection.AnchorCell.Row >= 0)
        {
            // Extend column selection from anchor
            var anchorCol = Selection.AnchorCell.Col;
            Selection.SelectColumnRange(Math.Min(anchorCol, colIndex), Math.Max(anchorCol, colIndex));
        }
        else if (modifiers.HasFlag(KeyModifiers.Control))
        {
            // Toggle column
            Selection?.ToggleColumn(colIndex);
        }
        else
        {
            // Single column selection
            Selection?.SelectColumn(colIndex);
        }
        
        UpdateSelectionOverlay();
        e.Handled = true;
    }
    
    private void OnColumnHeaderDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border { Tag: int colIndex } headerBorder) return;
        if (ViewModel == null || colIndex >= ViewModel.Columns.Count) return;
        
        var columnVm = ViewModel.Columns[colIndex];
        
        // Create inline TextBox for editing - stretch to fill the header
        var textBox = new TextBox
        {
            Text = columnVm.Header ?? "",
            Padding = new Thickness(8, 4),
            BorderThickness = new Thickness(0),
            Background = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            MinHeight = CellHeight
        };
        
        // Save original content
        var originalChild = headerBorder.Child;
        
        textBox.KeyDown += (s, ke) =>
        {
            if (ke.Key == Key.Enter)
            {
                columnVm.Header = textBox.Text;
                headerBorder.Child = originalChild;
                if (originalChild is TextBlock tb)
                {
                    tb.Text = columnVm.Header;
                }
                Focus();
                ke.Handled = true;
            }
            else if (ke.Key == Key.Escape)
            {
                headerBorder.Child = originalChild;
                Focus();
                ke.Handled = true;
            }
        };
        
        textBox.LostFocus += (s, lfe) =>
        {
            columnVm.Header = textBox.Text;
            headerBorder.Child = originalChild;
            if (originalChild is TextBlock tb)
            {
                tb.Text = columnVm.Header;
            }
        };
        
        // Replace header content with TextBox
        headerBorder.Child = textBox;
        textBox.Focus();
        textBox.SelectAll();
        
        e.Handled = true;
    }
    
    #endregion

    #region Column Resize
    
    private void OnColumnResizeStart(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: int colIndex }) return;
        
        _isResizingColumn = true;
        _resizeColumnIndex = colIndex;
        _resizeStartX = e.GetPosition(this).X;
        
        // Find current column width from the header Grid
        if (sender is Border grip && grip.Parent is Grid headerGrid)
        {
            _resizeStartWidth = headerGrid.Width;
        }
        else
        {
            _resizeStartWidth = DefaultCellWidth;
        }
        
        // Capture pointer
        e.Pointer.Capture((IInputElement)sender!);
        e.Handled = true;
    }
    
    private void OnColumnResizeMove(object? sender, PointerEventArgs e)
    {
        if (!_isResizingColumn || _resizeColumnIndex < 0) return;
        
        var currentX = e.GetPosition(this).X;
        var delta = currentX - _resizeStartX;
        var newWidth = Math.Max(MinColumnWidth, _resizeStartWidth + delta);
        
        // Update header width
        if (sender is Border grip && grip.Parent is Grid headerGrid)
        {
            headerGrid.Width = newWidth;
        }
        
        // Update ViewModel column width (for data binding consistency)
        if (ViewModel != null && _resizeColumnIndex < ViewModel.Columns.Count)
        {
            ViewModel.Columns[_resizeColumnIndex].Width = newWidth;
        }
        
        // Update all cells in this column visually
        UpdateColumnCellWidths(_resizeColumnIndex, newWidth);
        
        // Update overlay
        UpdateSelectionOverlay();
    }
    
    private void OnColumnResizeEnd(object? sender, PointerReleasedEventArgs e)
    {
        if (_isResizingColumn)
        {
            _isResizingColumn = false;
            _resizeColumnIndex = -1;
            e.Pointer.Capture(null);
        }
    }
    
    private void UpdateColumnCellWidths(int columnIndex, double newWidth)
    {
        // Store column width
        if (_columnWidths.Length <= columnIndex)
        {
            var newArray = new double[columnIndex + 1];
            Array.Copy(_columnWidths, newArray, _columnWidths.Length);
            for (int i = _columnWidths.Length; i < newArray.Length; i++)
            {
                newArray[i] = DefaultCellWidth;
            }
            _columnWidths = newArray;
        }
        _columnWidths[columnIndex] = newWidth;
        
        // Update cell widths in the grid
        if (CellGrid?.ItemsSource is not System.Collections.IEnumerable rows) return;
        
        int rowIndex = 0;
        foreach (var row in rows)
        {
            // Find the row container (ListBoxItem for ListBox, ContentPresenter for ItemsControl)
            var rowContainer = CellGrid.ContainerFromIndex(rowIndex) as ListBoxItem;
            if (rowContainer == null)
            {
                rowIndex++;
                continue;
            }
            
            // Find the inner ItemsControl (cells)
            var cellsControl = FindChild<ItemsControl>(rowContainer);
            if (cellsControl?.ItemsSource is not System.Collections.IEnumerable)
            {
                rowIndex++;
                continue;
            }
            
            // Find the cell at columnIndex
            var cellContainer = cellsControl.ContainerFromIndex(columnIndex) as ContentPresenter;
            if (cellContainer != null)
            {
                var cellBorder = FindChild<Border>(cellContainer);
                if (cellBorder != null)
                {
                    cellBorder.Width = newWidth;
                }
            }
            
            rowIndex++;
        }
    }
    
    private static T? FindChild<T>(Visual parent) where T : Visual
    {
        if (parent is T result) return result;
        
        var childCount = parent.GetVisualChildren().Count();
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is T found) return found;
            var descendant = FindChild<T>(child);
            if (descendant != null) return descendant;
        }
        
        return null;
    }
    
    #endregion

    #region Row Header Events
    
    private void OnRowHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        if (sender is not Border border) return;
        
        // Get row index from the DataContext (TableRowViewModel)
        if (border.DataContext is not TableRowViewModel rowVm) return;
        var rowIndex = rowVm.Index;
        
        var modifiers = e.KeyModifiers;
        
        if (modifiers.HasFlag(KeyModifiers.Shift) && Selection != null && Selection.AnchorCell.Row >= 0)
        {
            // Extend row selection from anchor
            var anchorRow = Selection.AnchorCell.Row;
            Selection.SelectRowRange(Math.Min(anchorRow, rowIndex), Math.Max(anchorRow, rowIndex));
        }
        else if (modifiers.HasFlag(KeyModifiers.Control))
        {
            // Toggle row
            Selection?.ToggleRow(rowIndex);
        }
        else
        {
            // Single row selection
            Selection?.SelectRow(rowIndex);
        }
        
        UpdateSelectionOverlay();
        e.Handled = true;
    }
    
    #endregion

    #region Cell Events
    
    private void OnCellPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        if (sender is not Border border) return;
        if (border.DataContext is not TableCellViewModel cellVm) return;
        
        var (row, col) = GetCellPosition(cellVm);
        if (row < 0 || col < 0) return;
        
        var modifiers = e.KeyModifiers;
        var point = e.GetCurrentPoint(border);
        
        if (point.Properties.IsLeftButtonPressed)
        {
            // Start drag selection
            _isDragging = true;
            _dragStartRow = row;
            _dragStartCol = col;
            
            if (modifiers.HasFlag(KeyModifiers.Shift) && Selection != null && Selection.AnchorCell.Row >= 0)
            {
                // Extend selection from anchor
                Selection.ExtendTo(row, col);
            }
            else if (modifiers.HasFlag(KeyModifiers.Control))
            {
                // Toggle cell
                Selection?.ToggleCell(row, col);
            }
            else
            {
                // New selection
                Selection?.SelectCell(row, col);
            }
            
            UpdateSelectionOverlay();
        }
        
        e.Handled = true;
    }
    
    private void OnCellPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;
        if (sender is not Border border) return;
        if (border.DataContext is not TableCellViewModel cellVm) return;
        
        var (row, col) = GetCellPosition(cellVm);
        if (row < 0 || col < 0) return;
        
        // Update drag selection
        Selection?.SelectRange(_dragStartRow, _dragStartCol, row, col);
        UpdateSelectionOverlay();
    }
    
    private void OnCellPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
    }
    
    private void OnCellDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not TableCellViewModel cellVm) return;
        
        // Start editing
        StartCellEdit(cellVm, border);
        e.Handled = true;
    }
    
    private void StartCellEdit(TableCellViewModel cellVm, Border cellBorder, string? initialText = null)
    {
        // Get actual cell width
        var cellWidth = cellBorder.Bounds.Width > 0 ? cellBorder.Bounds.Width : DefaultCellWidth;
        
        // Get raw value (formula string if formula, or actual value if regular cell)
        string? editText = initialText;
        if (editText == null && ViewModel != null)
        {
            var (row, col) = GetCellPosition(cellVm);
            if (row >= 0 && col >= 0)
            {
                // Get raw value from table (formula string or actual value)
                editText = ViewModel.GetCellRawValue(row, col) ?? cellVm.Value;
            }
            else
            {
                editText = cellVm.Value;
            }
        }
        
        // Create inline TextBox for editing
        var textBox = new TextBox
        {
            Text = editText ?? "",
            Width = cellWidth,
            Height = CellHeight,
            Padding = new Thickness(8, 4),
            BorderThickness = new Thickness(0),
            Background = Brushes.White,
            FontSize = 14,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };

        if (!string.IsNullOrEmpty(initialText))
        {
            textBox.CaretIndex = textBox.Text.Length;
        }
        
        textBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                cellVm.Value = textBox.Text;
                // Refresh UI to show computed result if formula
                RefreshCellDisplay(cellBorder, cellVm);
                EndCellEdit(cellBorder, cellVm);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                EndCellEdit(cellBorder, cellVm);
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                // Commit and move to next cell
                cellVm.Value = textBox.Text;
                RefreshCellDisplay(cellBorder, cellVm);
                EndCellEdit(cellBorder, cellVm);
                // Move to next cell (handled by grid navigation)
                e.Handled = false;
            }
        };
        
        textBox.LostFocus += (s, e) =>
        {
            cellVm.Value = textBox.Text;
            RefreshCellDisplay(cellBorder, cellVm);
            EndCellEdit(cellBorder, cellVm);
        };
        
        // Add autocomplete for formulas
        AddFormulaAutocomplete(textBox, cellVm);
        
        // Replace cell content with TextBox
        cellBorder.Child = textBox;
        textBox.Focus();
        if (string.IsNullOrEmpty(initialText))
        {
            textBox.SelectAll();
        }
    }
    
    private void RefreshCellDisplay(Border cellBorder, TableCellViewModel cellVm)
    {
        // Force refresh the cell value to get computed result if it's a formula
        // Wait for the commit to complete, then refresh
        Dispatcher.UIThread.Post(() =>
        {
            if (ViewModel != null)
            {
                var (row, col) = GetCellPosition(cellVm);
                if (row >= 0 && col >= 0 && row < ViewModel.Rows.Count && col < ViewModel.Rows[row].Cells.Count)
                {
                    // Get the updated display value from agent (computed result for formulas)
                    var displayValue = ViewModel.GetCellDisplayValue(row, col);
                    var cell = ViewModel.Rows[row].Cells[col];
                    
                    // Refresh the cell's display value
                    if (cell != null && displayValue != cell.Value)
                    {
                        cell.RefreshValue(displayValue);
                    }
                }
            }
        }, DispatcherPriority.Render);
    }
    
    private void EndCellEdit(Border cellBorder, TableCellViewModel cellVm)
    {
        // Get display value (computed result for formulas, raw value for regular cells)
        string? displayText = cellVm.Value;
        if (ViewModel != null)
        {
            var (row, col) = GetCellPosition(cellVm);
            if (row >= 0 && col >= 0)
            {
                displayText = ViewModel.GetCellDisplayValue(row, col) ?? cellVm.Value;
            }
        }
        
        // Restore display mode with computed value
        cellBorder.Child = new TextBlock
        {
            Text = displayText ?? "",
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Padding = new Thickness(8, 4),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        
        Focus();
    }
    
    private void AddFormulaAutocomplete(TextBox textBox, TableCellViewModel cellVm)
    {
        if (ViewModel == null) return;
        
        // Simple watermark/hint instead of complex popup
        // User can see available functions in tooltip or just type
        textBox.Watermark = "Type =SUM(ColumnName) for formulas";
        
        // Add tooltip with available functions
        var tooltip = new ToolTip
        {
            Content = "Available functions: SUM, AVG, MIN, MAX, COUNT\nExample: =SUM(Price)"
        };
        ToolTip.SetTip(textBox, tooltip);
    }
    
    private (int Row, int Col) GetCellPosition(TableCellViewModel cellVm)
    {
        if (ViewModel == null) return (-1, -1);
        
        for (int r = 0; r < ViewModel.Rows.Count; r++)
        {
            var row = ViewModel.Rows[r];
            for (int c = 0; c < row.Cells.Count; c++)
            {
                if (ReferenceEquals(row.Cells[c], cellVm))
                {
                    return (r, c);
                }
            }
        }
        
        return (-1, -1);
    }
    
    #endregion

    #region Keyboard Navigation
    
    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (Selection == null || ViewModel == null) return;
        
        var currentCell = Selection.CurrentCell;
        int row = currentCell.Row >= 0 ? currentCell.Row : 0;
        int col = currentCell.Col >= 0 ? currentCell.Col : 0;
        var maxRow = ViewModel.Rows.Count - 1;
        var maxCol = ViewModel.Columns.Count - 1;
        
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        
        switch (e.Key)
        {
            case Key.Up:
                if (ctrl) row = 0;
                else row = Math.Max(0, row - 1);
                NavigateTo(row, col, shift);
                e.Handled = true;
                break;
                
            case Key.Down:
                if (ctrl) row = maxRow;
                else row = Math.Min(maxRow, row + 1);
                NavigateTo(row, col, shift);
                e.Handled = true;
                break;
                
            case Key.Left:
                if (ctrl) col = 0;
                else col = Math.Max(0, col - 1);
                NavigateTo(row, col, shift);
                e.Handled = true;
                break;
                
            case Key.Right:
                if (ctrl) col = maxCol;
                else col = Math.Min(maxCol, col + 1);
                NavigateTo(row, col, shift);
                e.Handled = true;
                break;
                
            case Key.Tab:
                if (shift)
                    col = col > 0 ? col - 1 : maxCol;
                else
                    col = col < maxCol ? col + 1 : 0;
                Selection.SelectCell(row, col);
                UpdateSelectionOverlay();
                e.Handled = true;
                break;
                
            case Key.Enter:
                StartEditCurrentCell();
                e.Handled = true;
                break;
                
            case Key.F2:
                // Start edit on current cell
                StartEditCurrentCell();
                e.Handled = true;
                break;
                
            case Key.Delete:
            case Key.Back:
                // Map Delete/Back to ClearCellsCommand (if not in edit mode)
                ViewModel.ClearCellsCommand.Execute(null);
                e.Handled = true;
                break;
                
            case Key.Home:
                if (ctrl)
                    NavigateTo(0, 0, shift);
                else
                    NavigateTo(row, 0, shift);
                e.Handled = true;
                break;
                
            case Key.End:
                if (ctrl)
                    NavigateTo(maxRow, maxCol, shift);
                else
                    NavigateTo(row, maxCol, shift);
                e.Handled = true;
                break;
        }
    }
    
    private void NavigateTo(int row, int col, bool extend)
    {
        if (extend && Selection != null && Selection.AnchorCell.Row >= 0)
        {
            Selection.ExtendTo(row, col);
        }
        else
        {
            Selection?.SelectCell(row, col);
        }
        UpdateSelectionOverlay();
        ScrollToCell(row, col);
    }
    
    private void ScrollToCell(int row, int col)
    {
        if (CellScroll == null) return;
        
        var targetX = col * CellWidth;
        var targetY = row * CellHeight;
        
        // Check if cell is visible, if not scroll to it
        var viewport = CellScroll.Viewport;
        var offset = CellScroll.Offset;
        
        if (targetX < offset.X || targetX + CellWidth > offset.X + viewport.Width)
        {
            CellScroll.Offset = new Vector(targetX, offset.Y);
        }
        
        if (targetY < offset.Y || targetY + CellHeight > offset.Y + viewport.Height)
        {
            CellScroll.Offset = new Vector(offset.X, targetY);
        }
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;
        
        // If we are already editing (TextBox is focused), do nothing (Bubble event)
        // Check if focus is within a TextBox
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;
        
        // Start editing with the typed text
        StartEditCurrentCell(e.Text);
        e.Handled = true;
    }
    
    private void StartEditCurrentCell(string? initialText = null)
    {
        if (Selection == null || Selection.CurrentCell.Row < 0 || ViewModel == null) return;
        
        var curRow = Selection.CurrentCell.Row;
        var curCol = Selection.CurrentCell.Col;
        if (curRow >= ViewModel.Rows.Count || curCol >= ViewModel.Rows[curRow].Cells.Count) return;

        var cellBorder = GetCellBorder(curRow, curCol);
        if (cellBorder != null)
        {
            var cellVm = ViewModel.Rows[curRow].Cells[curCol];
            StartCellEdit(cellVm, cellBorder, initialText);
        }
    }

    
    #endregion

    #region Scroll Sync
    
    private void OnCellScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // Sync header scrolls with main grid
        if (ColumnHeaderScroll != null)
        {
            ColumnHeaderScroll.Offset = new Vector(CellScroll?.Offset.X ?? 0, 0);
        }
        
        if (RowHeaderScroll != null)
        {
            RowHeaderScroll.Offset = new Vector(0, CellScroll?.Offset.Y ?? 0);
        }
        
        // Update overlay position
        UpdateSelectionOverlay();
    }
    
    #endregion

    #region Selection Overlay
    
    private Rect? GetCellBounds(int row, int col)
    {
        var cellBorder = GetCellBorder(row, col);
        if (cellBorder == null) return null;
        
        // Transform to CellGrid coordinates (parent of overlay)
        try
        {
            var transform = cellBorder.TransformToVisual(CellGrid);
            if (transform == null) return null;
            
            var topLeft = transform.Value.Transform(new Point(0, 0));
            return new Rect(topLeft, cellBorder.Bounds.Size);
        }
        catch
        {
            return null;
        }
    }
    
    private void UpdateSelectionOverlay()
    {
        if (SelectionOverlay == null || Selection == null || ViewModel == null) return;
        
        SelectionOverlay.Children.Clear();
        
        var selectedCells = Selection.GetSelectedCells().ToList();
        if (selectedCells.Count == 0) return;
        
        // Draw selection rectangles using actual cell positions
        foreach (var (row, col) in selectedCells)
        {
            var bounds = GetCellBounds(row, col);
            if (bounds == null)
            {
                // Fallback to calculated position
                bounds = new Rect(col * DefaultCellWidth, row * CellHeight, DefaultCellWidth, CellHeight);
            }
            
            var rect = new Rectangle
            {
                Width = bounds.Value.Width,
                Height = bounds.Value.Height,
                Fill = new SolidColorBrush(Color.FromArgb(60, 0, 120, 215)),
                Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            
            Canvas.SetLeft(rect, bounds.Value.X);
            Canvas.SetTop(rect, bounds.Value.Y);
            
            SelectionOverlay.Children.Add(rect);
        }
        
        // Draw current cell border (thicker) only for Cell/Range selection mode
        if (Selection.CurrentCell.Row >= 0 && 
            (Selection.Type == SelectionModel.SelectionType.Cell || 
             Selection.Type == SelectionModel.SelectionType.Range))
        {
            var curRow = Selection.CurrentCell.Row;
            var curCol = Selection.CurrentCell.Col;
            var bounds = GetCellBounds(curRow, curCol);
            
            if (bounds != null)
            {
                var currentRect = new Rectangle
                {
                    Width = bounds.Value.Width,
                    Height = bounds.Value.Height,
                    Fill = Brushes.Transparent,
                    Stroke = new SolidColorBrush(Color.FromRgb(0, 102, 204)),
                    StrokeThickness = 2,
                    IsHitTestVisible = false
                };
                
                Canvas.SetLeft(currentRect, bounds.Value.X);
                Canvas.SetTop(currentRect, bounds.Value.Y);
                
                SelectionOverlay.Children.Add(currentRect);
            }
        }
    }
    
    #endregion

    private Border? GetCellBorder(int row, int col)
    {
        if (CellGrid?.ItemsSource is not System.Collections.IEnumerable) return null;
        
        // ListBox uses ListBoxItem, ItemsControl uses ContentPresenter
        var rowContainer = CellGrid.ContainerFromIndex(row) as Control;
        if (rowContainer == null) return null;
        
        var cellsControl = FindChild<ItemsControl>(rowContainer);
        if (cellsControl == null) return null;
        
        var cellContainer = cellsControl.ContainerFromIndex(col) as Control;
        if (cellContainer == null) return null;
        
        return FindChild<Border>(cellContainer);
    }
}
