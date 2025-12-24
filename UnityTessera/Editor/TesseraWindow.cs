using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Tessera.Core.Models;

namespace Tessera.Editor
{
    public class TesseraWindow : EditorWindow
    {
        private TesseraEditorState _state;
        private SchemaViewComponent _schemaComponent;
        private JsonViewComponent _jsonComponent;
        
        private enum ViewMode { Table, Schema, Json, Settings }
        private ViewMode _currentView = ViewMode.Table;
        
        private string _loadedFilePath;

        // Scroll states
        private Vector2 _tableScrollPosition;
        private Vector2 _settingsScrollPosition;
        
        // Selection tracking
        private int _selectedRowIndex = -1;
        private int _selectedColumnIndex = -1;
        private HashSet<(int row, int col)> _selectedCells = new HashSet<(int, int)>();
        
        // Column width cache (dynamic based on header name)
        private List<float> _columnWidths = new List<float>();
        private List<float> _columnMinWidths = new List<float>(); // Minimum widths based on header
        private int _resizingColumnIndex = -1; // Column being resized
        private float _resizeStartX;
        private float _resizeStartWidth;
        
        // Copy button state
        private int _pressedCopyButtonIndex = -1;
        private int _lastHoveredCopyButton = -1;
        
        [MenuItem("Tools/Tessera - CSV Editor")]
        public static void ShowWindow()
        {
            TesseraWindow wnd = GetWindow<TesseraWindow>();
            wnd.titleContent = new GUIContent("Tessera");
            wnd.minSize = new Vector2(800, 500);
        }

        private void OnEnable()
        {
            _state = new TesseraEditorState();
            _schemaComponent = new SchemaViewComponent(_state);
            _jsonComponent = new JsonViewComponent(_state);
            
            // Check for initial selection load
            OnSelectionChange();
        }
        
        private void OnSelectionChange()
        {
            var selected = Selection.activeObject;
            if (selected == null) return;
            
            var path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path)) return;
            
            if (path.EndsWith(".csv") || path.EndsWith(".json"))
            {
                var fullPath = System.IO.Path.GetFullPath(path);
                _loadedFilePath = path; // Store relative path for display
                _state.LoadCsv(fullPath);
                
                // Refresh components to ensure JSON view and others are up to date
                _schemaComponent.Refresh();
                _jsonComponent.Refresh();
                
                Repaint();
            }
        }

        private void OnGUI()
        {
            // Handle click outside text fields to unfocus (Bug fix: edit state not exiting)
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                // Clear focus when clicking - actual controls will re-capture focus as needed
                GUI.FocusControl(null);
            }
            
            DrawHeader();
            DrawToolbar();
            DrawContent();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    var titleStyle = new GUIStyle(EditorStyles.boldLabel);
                    titleStyle.fontSize = 20;
                    GUILayout.Label("Tessera - CSV Editor", titleStyle);
                    
                    if (!string.IsNullOrEmpty(_loadedFilePath))
                    {
                        GUILayout.Label($"File: {_loadedFilePath}", EditorStyles.miniLabel);
                    }
                }
                
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button(_currentView == ViewMode.Settings ? "Back" : "Settings", GUILayout.Width(80)))
                {
                    _currentView = _currentView == ViewMode.Settings ? ViewMode.Table : ViewMode.Settings;
                }
            }
            GUILayout.Space(10);
        }

        private void DrawToolbar()
        {
            // If in Settings, we might hide the main toolbar tabs or keep them
            // UIElements logic hid them. Let's hide them here too.
            if (_currentView == ViewMode.Settings) return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Tabs using Toolbar for proper event handling
                // Map current view to index (0, 1, 2). If Settings (3), we don't show this toolbar anyway due to check above.
                int currentTab = (int)_currentView;
                if (currentTab > 2) currentTab = -1; // Should not happen given the check above

                int newTab = GUILayout.Toolbar(currentTab, new string[] { "Table", "Schema", "JSON" }, EditorStyles.toolbarButton);
                
                if (newTab != currentTab && newTab >= 0 && newTab <= 2)
                {
                    SwitchView((ViewMode)newTab);
                }
                
                GUILayout.FlexibleSpace();
                
                // Actions
                if (GUILayout.Button("Load", EditorStyles.toolbarButton)) LoadCsvFile();
                
                // View specific actions
                if (_currentView == ViewMode.Table)
                {
                    if (GUILayout.Button("Add Row", EditorStyles.toolbarButton)) AddRow();
                    if (GUILayout.Button("Add Column", EditorStyles.toolbarButton)) AddColumn();
                }
                else if (_currentView == ViewMode.Json)
                {
                    if (GUILayout.Button("Format", EditorStyles.toolbarButton)) _jsonComponent.FormatJson();
                    if (GUILayout.Button("Apply Changes", EditorStyles.toolbarButton)) _jsonComponent.ApplyJson();
                }
                
                if (GUILayout.Button("Save", EditorStyles.toolbarButton)) _state.SaveCsv();
            }
            GUILayout.Space(5);
        }

        private void DrawContent()
        {
            if (_currentView == ViewMode.Settings)
            {
                DrawSettingsGUI();
                return;
            }

            // Status Label
            if (_state.Table == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("No file loaded. Select a CSV file in the Project window.", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            switch (_currentView)
            {
                case ViewMode.Table:
                    DrawTableView();
                    break;
                case ViewMode.Schema:
                    _schemaComponent.DrawGUI();
                    break;
                case ViewMode.Json:
                    _jsonComponent.DrawGUI();
                    break;
            }
        }

        private void DrawTableView()
        {
            if (_state.Table == null) return;

            // Handle right-click for context menu
            bool showContextMenu = false;

            using (var scroll = new EditorGUILayout.ScrollViewScope(_tableScrollPosition))
            {
                _tableScrollPosition = scroll.scrollPosition;
                
                // Using HorizontalScope for rows. 
                // Note: Standard IMGUI table performance is poor for large datasets.
                // Optimizations like reusing GUIStyles or virtualizing are advanced but good to keep in mind.
                
                // Calculate column widths (dynamic based on header length)
                UpdateColumnWidths();
                
                // Header
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    for (int j = 0; j < _state.Schema.Columns.Count; j++)
                    {
                        var col = _state.Schema.Columns[j];
                        float columnWidth = GetColumnWidth(j);
                        float copyButtonWidth = 18f;
                        float resizeHandleWidth = 6f;
                        float labelWidth = columnWidth - copyButtonWidth - resizeHandleWidth - 2f;
                        
                        // Get rect for the entire header cell
                        var headerRect = GUILayoutUtility.GetRect(
                            new GUIContent(col.Name), 
                            EditorStyles.boldLabel, 
                            GUILayout.Width(columnWidth));
                        
                        // Split into label rect, copy button rect, and resize handle rect
                        var labelRect = new Rect(headerRect.x, headerRect.y, labelWidth, headerRect.height);
                        var copyButtonRect = new Rect(headerRect.x + labelWidth + 2f, headerRect.y + (headerRect.height - 12f) / 2f, 12f, 12f);
                        var resizeHandleRect = new Rect(headerRect.xMax - resizeHandleWidth, headerRect.y, resizeHandleWidth, headerRect.height);
                        
                        // Handle column resize
                        EditorGUIUtility.AddCursorRect(resizeHandleRect, MouseCursor.ResizeHorizontal);
                        
                        if (Event.current.type == EventType.MouseDown && resizeHandleRect.Contains(Event.current.mousePosition))
                        {
                            _resizingColumnIndex = j;
                            _resizeStartX = Event.current.mousePosition.x;
                            _resizeStartWidth = columnWidth;
                            Event.current.Use();
                        }
                        
                        if (_resizingColumnIndex == j)
                        {
                            if (Event.current.type == EventType.MouseDrag)
                            {
                                float delta = Event.current.mousePosition.x - _resizeStartX;
                                float newWidth = Mathf.Max(_resizeStartWidth + delta, GetColumnMinWidth(j));
                                _columnWidths[j] = newWidth;
                                Repaint();
                                Event.current.Use();
                            }
                            else if (Event.current.type == EventType.MouseUp)
                            {
                                _resizingColumnIndex = -1;
                                Event.current.Use();
                            }
                        }
                        
                        // Handle header click (on label area only)
                        if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition))
                        {
                            if (Event.current.button == 0) // Left click
                            {
                                _selectedColumnIndex = j;
                                _selectedRowIndex = -1;
                                _selectedCells.Clear();
                                Repaint();
                                Event.current.Use();
                            }
                            else if (Event.current.button == 1) // Right click
                            {
                                _selectedColumnIndex = j;
                                _selectedRowIndex = -1;
                                _selectedCells.Clear();
                                showContextMenu = true;
                                Event.current.Use();
                            }
                        }
                        
                        // Highlight selected column header
                        if (_selectedColumnIndex == j && _selectedRowIndex == -1)
                        {
                            EditorGUI.DrawRect(headerRect, new Color(0.3f, 0.5f, 0.8f, 0.3f));
                        }
                        
                        GUI.Label(labelRect, col.Name, EditorStyles.boldLabel);
                        
                        // Copy column button - simple flat icon with hover effect
                        int currentColIndex = j;
                        DrawCopyButton(copyButtonRect, currentColIndex);
                        
                        // Draw resize handle visual
                        var handleColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                        EditorGUI.DrawRect(new Rect(resizeHandleRect.x + 2, resizeHandleRect.y + 4, 1, resizeHandleRect.height - 8), handleColor);
                    }
                }

                // Rows
                for (int i = 0; i < _state.Table.Rows.Count; i++)
                {
                    var row = _state.Table.Rows[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (int j = 0; j < row.Cells.Count; j++)
                        {
                            var val = row.Cells[j] ?? "";
                            
                            // Get rect for this cell (use same width as header)
                            var cellRect = GUILayoutUtility.GetRect(
                                new GUIContent(val), 
                                EditorStyles.textField, 
                                GUILayout.Width(GetColumnWidth(j)));
                            
                            // Handle cell click - only for selection, don't interfere with TextField editing
                            if (Event.current.type == EventType.MouseDown && cellRect.Contains(Event.current.mousePosition))
                            {
                                if (Event.current.button == 0) // Left click
                                {
                                    if (Event.current.control || Event.current.command)
                                    {
                                        // Multi-select - consume event
                                        if (_selectedCells.Contains((i, j)))
                                            _selectedCells.Remove((i, j));
                                        else
                                            _selectedCells.Add((i, j));
                                        Repaint();
                                        Event.current.Use();
                                    }
                                    else if (Event.current.shift)
                                    {
                                        // Range select - consume event
                                        int startRow = _selectedRowIndex >= 0 ? _selectedRowIndex : i;
                                        int startCol = _selectedColumnIndex >= 0 ? _selectedColumnIndex : j;
                                        _selectedCells.Clear();
                                        for (int r = Mathf.Min(startRow, i); r <= Mathf.Max(startRow, i); r++)
                                        {
                                            for (int c = Mathf.Min(startCol, j); c <= Mathf.Max(startCol, j); c++)
                                            {
                                                _selectedCells.Add((r, c));
                                            }
                                        }
                                        Repaint();
                                        Event.current.Use();
                                    }
                                    else
                                    {
                                        // Single select - update selection but don't consume event
                                        // This allows TextField to handle the click for editing
                                        _selectedRowIndex = i;
                                        _selectedColumnIndex = j;
                                        _selectedCells.Clear();
                                        _selectedCells.Add((i, j));
                                        Repaint();
                                        // Don't call Event.current.Use() here - let TextField handle it
                                    }
                                }
                                else if (Event.current.button == 1) // Right click
                                {
                                    if (!_selectedCells.Contains((i, j)))
                                    {
                                        _selectedRowIndex = i;
                                        _selectedColumnIndex = j;
                                        _selectedCells.Clear();
                                        _selectedCells.Add((i, j));
                                    }
                                    showContextMenu = true;
                                    Event.current.Use();
                                }
                            }
                            
                            // Highlight selected cell
                            if (_selectedCells.Contains((i, j)) || (_selectedRowIndex == i && _selectedColumnIndex == j))
                            {
                                EditorGUI.DrawRect(cellRect, new Color(0.3f, 0.5f, 0.8f, 0.3f));
                            }
                            
                            var newVal = EditorGUI.TextField(cellRect, val);
                            if (newVal != val)
                            {
                                row.Cells[j] = newVal;
                            }
                        }
                    }
                }
            }
            
            // Show context menu
            if (showContextMenu)
            {
                ShowContextMenu();
            }
        }
        
        private void UpdateColumnWidths()
        {
            if (_state.Schema == null) return;
            
            // Resize lists if needed
            while (_columnWidths.Count < _state.Schema.Columns.Count)
                _columnWidths.Add(0);
            while (_columnWidths.Count > _state.Schema.Columns.Count)
                _columnWidths.RemoveAt(_columnWidths.Count - 1);
                
            while (_columnMinWidths.Count < _state.Schema.Columns.Count)
                _columnMinWidths.Add(0);
            while (_columnMinWidths.Count > _state.Schema.Columns.Count)
                _columnMinWidths.RemoveAt(_columnMinWidths.Count - 1);
            
            // Calculate min width for each column based on header name
            for (int j = 0; j < _state.Schema.Columns.Count; j++)
            {
                var headerName = _state.Schema.Columns[j].Name ?? "";
                float textWidth = EditorStyles.boldLabel.CalcSize(new GUIContent(headerName)).x;
                float buttonSpace = 36f; // Space for copy button + resize handle
                
                _columnMinWidths[j] = textWidth + buttonSpace + 8f;
                
                // Initialize width to min if not set or less than min
                if (_columnWidths[j] < _columnMinWidths[j])
                {
                    _columnWidths[j] = _columnMinWidths[j];
                }
            }
        }
        
        private float GetColumnWidth(int columnIndex)
        {
            if (columnIndex >= 0 && columnIndex < _columnWidths.Count)
                return _columnWidths[columnIndex];
            return TesseraSettings.FontSize * 8; // Default fallback
        }
        
        private float GetColumnMinWidth(int columnIndex)
        {
            if (columnIndex >= 0 && columnIndex < _columnMinWidths.Count)
                return _columnMinWidths[columnIndex];
            return TesseraSettings.FontSize * 6; // Default fallback
        }
        
        private void DrawCopyButton(Rect rect, int columnIndex)
        {
            // Hover and press detection
            bool isHovered = rect.Contains(Event.current.mousePosition);
            bool isPressed = _pressedCopyButtonIndex == columnIndex;
            
            // Track hover changes for immediate repaint
            if (isHovered && _lastHoveredCopyButton != columnIndex)
            {
                _lastHoveredCopyButton = columnIndex;
                Repaint();
            }
            else if (!isHovered && _lastHoveredCopyButton == columnIndex)
            {
                _lastHoveredCopyButton = -1;
                Repaint();
            }
            
            // Draw background on hover/press
            if (isPressed)
            {
                EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            }
            else if (isHovered)
            {
                EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            }
            
            // Draw simple copy icon (single square with border)
            var iconColor = isPressed ? new Color(0.7f, 0.7f, 0.7f) : 
                           (isHovered ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.6f, 0.6f, 0.6f));
            var bgColor = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.76f, 0.76f, 0.76f);
            
            float padding = 2f;
            float pressOffset = isPressed ? 1f : 0f; // Visual "press down" offset
            var squareRect = new Rect(
                rect.x + padding + pressOffset, 
                rect.y + padding + pressOffset, 
                rect.width - padding * 2, 
                rect.height - padding * 2);
            
            // Outer border
            EditorGUI.DrawRect(squareRect, iconColor);
            // Inner fill
            var innerRect = new Rect(squareRect.x + 1, squareRect.y + 1, squareRect.width - 2, squareRect.height - 2);
            EditorGUI.DrawRect(innerRect, bgColor);
            
            // Handle mouse down (start press)
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _pressedCopyButtonIndex = columnIndex;
                Event.current.Use();
                Repaint();
            }
            
            // Handle mouse up (execute copy if still over button)
            if (_pressedCopyButtonIndex == columnIndex && Event.current.type == EventType.MouseUp)
            {
                if (rect.Contains(Event.current.mousePosition))
                {
                    CopyColumn(columnIndex);
                }
                _pressedCopyButtonIndex = -1;
                Event.current.Use();
                Repaint();
            }
        }

        private void SwitchView(ViewMode view)
        {
            _currentView = view;
            
            // Refresh components if needed
            if (_state.Table != null && _state.Schema != null)
            {
                if (view == ViewMode.Schema) _schemaComponent.Refresh();
                if (view == ViewMode.Json) _jsonComponent.Refresh();
            }
        }

        private void DrawSettingsGUI()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_settingsScrollPosition))
            {
                _settingsScrollPosition = scroll.scrollPosition;

                var titleStyle = new GUIStyle(EditorStyles.boldLabel);
                titleStyle.fontSize = 24;
                titleStyle.margin = new RectOffset(0, 0, 10, 10);
                titleStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
                GUILayout.Label("Settings", titleStyle);
                GUILayout.Space(10);

                // Editor Section
                EditorGUILayout.LabelField("Editor", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    // Font Size
                    int[] fontSizes = TesseraSettings.AvailableFontSizes;
                    string[] fontSizeLabels = new string[fontSizes.Length];
                    for(int i=0; i<fontSizes.Length; i++) fontSizeLabels[i] = fontSizes[i].ToString();
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(new GUIContent("Font Size", "Controls the font size used in the table view. Larger values make text easier to read but show fewer cells."), GUILayout.Width(EditorGUIUtility.labelWidth));
                        TesseraSettings.FontSize = EditorGUILayout.IntPopup(TesseraSettings.FontSize, fontSizeLabels, fontSizes);
                    }

                    // Row Height
                    TesseraSettings.RowHeight = EditorGUILayout.IntField(
                        new GUIContent("Row Height", "Height of each row in pixels. Set to 0 for automatic height based on font size."), 
                        TesseraSettings.RowHeight);
                    if (TesseraSettings.RowHeight < 0) TesseraSettings.RowHeight = 0;

                    // Toggles
                    TesseraSettings.AutoSave = EditorGUILayout.Toggle(
                        new GUIContent("Auto Save", "Automatically save changes to the file when editing. When disabled, you must manually click Save."), 
                        TesseraSettings.AutoSave);
                    TesseraSettings.ShowLineNumbers = EditorGUILayout.Toggle(
                        new GUIContent("Show Line Numbers", "Display row numbers in the table view for easier navigation."), 
                        TesseraSettings.ShowLineNumbers);
                    TesseraSettings.WordWrap = EditorGUILayout.Toggle(
                        new GUIContent("Word Wrap", "Wrap long text within cells to multiple lines instead of truncating."), 
                        TesseraSettings.WordWrap);
                }

                GUILayout.Space(10);

                // Data Processing Section
                EditorGUILayout.LabelField("Data Processing", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    // CSV Delimiter
                    string currentDelimiter = TesseraSettings.CsvDelimiter;
                    int currentIndex = System.Array.IndexOf(TesseraSettings.AvailableDelimiters, currentDelimiter);
                    if (currentIndex < 0) currentIndex = 0;

                    int newIndex = EditorGUILayout.Popup(
                        new GUIContent("CSV Delimiter", "Character used to separate columns in CSV files. Common delimiters: Comma (,) for standard CSV, Semicolon (;) for European formats, Tab for TSV files."), 
                        currentIndex, TesseraSettings.AvailableDelimiters);
                    if (newIndex != currentIndex && newIndex >= 0 && newIndex < TesseraSettings.AvailableDelimiters.Length)
                    {
                        TesseraSettings.CsvDelimiter = TesseraSettings.AvailableDelimiters[newIndex];
                    }

                    // Toggles
                    TesseraSettings.TrimWhitespace = EditorGUILayout.Toggle(
                        new GUIContent("Trim Whitespace", "Remove leading and trailing spaces from cell values when loading and saving files."), 
                        TesseraSettings.TrimWhitespace);
                    TesseraSettings.ArrayDisplayMultiLine = EditorGUILayout.Toggle(
                        new GUIContent("Array Display Multi-line", "When displaying JSON arrays in cells, show each element on a new line for better readability."), 
                        TesseraSettings.ArrayDisplayMultiLine);
                }
                
                GUILayout.Space(20);
            }
        }

        private void LoadCsvFile()
        {
            // Support both CSV and JSON files
            string path = EditorUtility.OpenFilePanel("Open Data File", "", "csv,json");
            if (!string.IsNullOrEmpty(path))
            {
                _state.LoadCsv(path);
                // Relative path for display if possible, or just filename
                _loadedFilePath = path;
                
                // Refresh components
                _schemaComponent.Refresh(); 
                _jsonComponent.Refresh();
            }
        }
        
        private void AddRow()
        {
            if (_state.Table == null || _state.Schema == null) return;
            
            var cells = new List<string?>();
            for (int i = 0; i < _state.Schema.Columns.Count; i++) cells.Add("");
            
            _state.Table.Rows.Add(new RowModel(cells));
        }
        
        private void AddColumn()
        {
            if (_state.Table == null || _state.Schema == null) return;
            
            int colIndex = _state.Table.Columns.Count;
            string columnName = $"Column{colIndex + 1}";
            
            _state.Table.Columns.Add(new ColumnModel(columnName));
            _state.Schema.Columns.Add(new ColumnSchema(name: columnName, type: DataType.String, isNullable: true));
            
            foreach (var row in _state.Table.Rows) row.Cells.Add("");
        }
        
        private void ShowContextMenu()
        {
            GenericMenu menu = new GenericMenu();
            
            // Copy
            bool hasSelection = _selectedCells.Count > 0 || (_selectedRowIndex >= 0 && _selectedColumnIndex >= 0);
            if (hasSelection)
                menu.AddItem(new GUIContent("Copy"), false, () => CopySelection());
            else
                menu.AddDisabledItem(new GUIContent("Copy"));
            
            // Paste
            menu.AddItem(new GUIContent("Paste"), false, () => PasteSelection());
            
            // Clear Cells
            if (hasSelection)
                menu.AddItem(new GUIContent("Clear Cells"), false, () => ClearSelectedCells());
            else
                menu.AddDisabledItem(new GUIContent("Clear Cells"));
            
            menu.AddSeparator("");
            
            // Add Row/Column
            menu.AddItem(new GUIContent("Add Row"), false, () => AddRow());
            menu.AddItem(new GUIContent("Add Column"), false, () => AddColumn());
            
            menu.AddSeparator("");
            
            // Insert Row Above/Below
            bool canInsertRow = _selectedRowIndex >= 0 && _selectedRowIndex < _state.Table.Rows.Count;
            if (canInsertRow)
            {
                menu.AddItem(new GUIContent("Insert Row Above"), false, () => InsertRow(true));
                menu.AddItem(new GUIContent("Insert Row Below"), false, () => InsertRow(false));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Insert Row Above"));
                menu.AddDisabledItem(new GUIContent("Insert Row Below"));
            }
            
            // Insert Column Left/Right
            bool canInsertColumn = _selectedColumnIndex >= 0 && _selectedColumnIndex < _state.Schema.Columns.Count;
            if (canInsertColumn)
            {
                menu.AddItem(new GUIContent("Insert Column Left"), false, () => InsertColumn(true));
                menu.AddItem(new GUIContent("Insert Column Right"), false, () => InsertColumn(false));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Insert Column Left"));
                menu.AddDisabledItem(new GUIContent("Insert Column Right"));
            }
            
            menu.AddSeparator("");
            
            // Delete Row/Column
            if (canInsertRow)
                menu.AddItem(new GUIContent("Delete Row"), false, () => DeleteRow());
            else
                menu.AddDisabledItem(new GUIContent("Delete Row"));
                
            if (canInsertColumn)
                menu.AddItem(new GUIContent("Delete Column"), false, () => DeleteColumn());
            else
                menu.AddDisabledItem(new GUIContent("Delete Column"));
            
            menu.AddSeparator("");
            
            // Select All
            menu.AddItem(new GUIContent("Select All"), false, () => SelectAll());
            
            menu.ShowAsContext();
        }
        
        private void CopySelection()
        {
            if (_state.Table == null) return;
            
            var cellsToCopy = _selectedCells.Count > 0 ? _selectedCells.ToList() : 
                (_selectedRowIndex >= 0 && _selectedColumnIndex >= 0 ? 
                    new List<(int, int)> { (_selectedRowIndex, _selectedColumnIndex) } : 
                    new List<(int, int)>());
            
            if (cellsToCopy.Count == 0) return;
            
            // Group by row and sort
            var lookup = new Dictionary<int, Dictionary<int, string?>>();
            foreach (var (row, col) in cellsToCopy)
            {
                if (row >= 0 && row < _state.Table.Rows.Count && 
                    col >= 0 && col < _state.Table.Rows[row].Cells.Count)
                {
                    if (!lookup.TryGetValue(row, out var columns))
                    {
                        columns = new Dictionary<int, string?>();
                        lookup[row] = columns;
                    }
                    columns[col] = _state.Table.Rows[row].Cells[col];
                }
            }
            
            var orderedRows = new List<int>(lookup.Keys);
            orderedRows.Sort();
            
            var rows = orderedRows.Select(rowIndex =>
            {
                var columns = lookup[rowIndex];
                var orderedCols = new List<int>(columns.Keys);
                orderedCols.Sort();
                return orderedCols.Select(colIndex => columns[colIndex]).ToList();
            }).ToList();
            
            string content = SerializeCsvForClipboard(rows);
            EditorGUIUtility.systemCopyBuffer = content;
        }
        
        private void CopyColumn(int columnIndex)
        {
            if (_state.Table == null) return;
            if (columnIndex < 0 || columnIndex >= _state.Schema.Columns.Count) return;
            
            var columnValues = new StringBuilder();
            for (int i = 0; i < _state.Table.Rows.Count; i++)
            {
                var row = _state.Table.Rows[i];
                if (columnIndex < row.Cells.Count)
                {
                    if (i > 0) columnValues.AppendLine();
                    columnValues.Append(row.Cells[columnIndex] ?? "");
                }
            }
            
            EditorGUIUtility.systemCopyBuffer = columnValues.ToString();
        }
        
        private void PasteSelection()
        {
            if (_state.Table == null || _state.Schema == null) return;
            if (_selectedRowIndex < 0 || _selectedColumnIndex < 0) return;
            
            string clipboardText = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(clipboardText)) return;
            
            var parsed = ParseCsvFromClipboard(clipboardText);
            if (parsed.Count == 0) return;
            
            int startRow = _selectedRowIndex;
            int startCol = _selectedColumnIndex;
            
            for (int i = 0; i < parsed.Count; i++)
            {
                int targetRow = startRow + i;
                if (targetRow >= _state.Table.Rows.Count) break;
                
                var row = parsed[i];
                for (int j = 0; j < row.Count; j++)
                {
                    int targetCol = startCol + j;
                    if (targetCol >= _state.Table.Rows[targetRow].Cells.Count) break;
                    
                    _state.Table.Rows[targetRow].Cells[targetCol] = row[j];
                }
            }
        }
        
        private void ClearSelectedCells()
        {
            if (_state.Table == null) return;
            
            var cellsToClear = _selectedCells.Count > 0 ? _selectedCells.ToList() : 
                (_selectedRowIndex >= 0 && _selectedColumnIndex >= 0 ? 
                    new List<(int, int)> { (_selectedRowIndex, _selectedColumnIndex) } : 
                    new List<(int, int)>());
            
            int clearedCount = 0;
            foreach (var (row, col) in cellsToClear)
            {
                if (row >= 0 && row < _state.Table.Rows.Count && 
                    col >= 0 && col < _state.Table.Rows[row].Cells.Count)
                {
                    _state.Table.Rows[row].Cells[col] = "";
                    clearedCount++;
                }
            }
        }
        
        private void InsertRow(bool above)
        {
            if (_state.Table == null || _state.Schema == null) return;
            
            int index = _selectedRowIndex;
            if (index < 0) index = _state.Table.Rows.Count;
            if (!above) index++;
            
            var cells = new List<string?>();
            for (int i = 0; i < _state.Schema.Columns.Count; i++) cells.Add("");
            
            if (index >= _state.Table.Rows.Count)
                _state.Table.Rows.Add(new RowModel(cells));
            else
                _state.Table.Rows.Insert(index, new RowModel(cells));
        }
        
        private void InsertColumn(bool left)
        {
            if (_state.Table == null || _state.Schema == null) return;
            
            int index = _selectedColumnIndex;
            if (index < 0) index = _state.Table.Columns.Count;
            if (!left) index++;
            
            string columnName = $"Column{_state.Table.Columns.Count + 1}";
            var colSchema = new ColumnSchema(columnName, DataType.String, true);
            var colModel = new ColumnModel(columnName);
            
            if (index >= _state.Table.Columns.Count)
            {
                _state.Schema.Columns.Add(colSchema);
                _state.Table.Columns.Add(colModel);
                foreach (var row in _state.Table.Rows) row.Cells.Add("");
            }
            else
            {
                _state.Schema.Columns.Insert(index, colSchema);
                _state.Table.Columns.Insert(index, colModel);
                foreach (var row in _state.Table.Rows) row.Cells.Insert(index, "");
            }
        }
        
        private void DeleteRow()
        {
            if (_state.Table == null) return;
            if (_selectedRowIndex < 0 || _selectedRowIndex >= _state.Table.Rows.Count) return;
            
            _state.Table.Rows.RemoveAt(_selectedRowIndex);
            _selectedRowIndex = -1;
            _selectedCells.Clear();
        }
        
        private void DeleteColumn()
        {
            if (_state.Table == null || _state.Schema == null) return;
            if (_selectedColumnIndex < 0 || _selectedColumnIndex >= _state.Schema.Columns.Count) return;
            
            _state.Schema.Columns.RemoveAt(_selectedColumnIndex);
            _state.Table.Columns.RemoveAt(_selectedColumnIndex);
            foreach (var row in _state.Table.Rows)
            {
                if (_selectedColumnIndex < row.Cells.Count)
                    row.Cells.RemoveAt(_selectedColumnIndex);
            }
            
            _selectedColumnIndex = -1;
            _selectedCells.Clear();
        }
        
        private void SelectAll()
        {
            if (_state.Table == null) return;
            
            _selectedCells.Clear();
            for (int i = 0; i < _state.Table.Rows.Count; i++)
            {
                for (int j = 0; j < _state.Table.Rows[i].Cells.Count; j++)
                {
                    _selectedCells.Add((i, j));
                }
            }
            
            _selectedRowIndex = 0;
            _selectedColumnIndex = 0;
        }
        
        private string SerializeCsvForClipboard(List<List<string?>> rows)
        {
            var builder = new StringBuilder();
            char delimiter = TesseraSettings.GetDelimiterChar();
            
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) builder.AppendLine();
                builder.Append(string.Join(delimiter.ToString(), rows[i].Select(v => EscapeCsvValue(v, delimiter))));
            }
            
            return builder.ToString();
        }
        
        private List<List<string?>> ParseCsvFromClipboard(string text)
        {
            var rows = new List<List<string?>>();
            char delimiter = TesseraSettings.GetDelimiterChar();
            
            using (var reader = new StringReader(text))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    rows.Add(ParseCsvLine(line, delimiter));
                }
            }
            
            return rows;
        }
        
        private List<string?> ParseCsvLine(string line, char delimiter)
        {
            var values = new List<string?>();
            var current = new StringBuilder();
            bool inQuotes = false;
            
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == delimiter)
                    {
                        values.Add(current.ToString());
                        current.Clear();
                    }
                    else if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }
            
            values.Add(current.ToString());
            return values;
        }
        
        private string EscapeCsvValue(string? value, char delimiter)
        {
            var safe = value ?? string.Empty;
            bool requiresQuotes = safe.IndexOfAny(new[] { delimiter, '\n', '\r', '"' }) >= 0;
            
            if (safe.Contains('"'))
            {
                safe = safe.Replace("\"", "\"\"");
            }
            
            return requiresQuotes ? $"\"{safe}\"" : safe;
        }
    }
}
