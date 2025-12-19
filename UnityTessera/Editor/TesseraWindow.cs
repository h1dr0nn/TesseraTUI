using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
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
        
        [MenuItem("Window/Tessera - CSV Editor")]
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
                if (GUILayout.Button("Load CSV", EditorStyles.toolbarButton)) LoadCsvFile();
                
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

            using (var scroll = new EditorGUILayout.ScrollViewScope(_tableScrollPosition))
            {
                _tableScrollPosition = scroll.scrollPosition;
                
                // Using HorizontalScope for rows. 
                // Note: Standard IMGUI table performance is poor for large datasets.
                // Optimizations like reusing GUIStyles or virtualizing are advanced but good to keep in mind.
                
                // Header
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    foreach (var col in _state.Schema.Columns)
                    {
                        GUILayout.Label(col.Name, EditorStyles.boldLabel, GUILayout.Width(TesseraSettings.FontSize * 8)); // Approx width
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
                            var newVal = EditorGUILayout.TextField(val, GUILayout.Width(TesseraSettings.FontSize * 8));
                            if (newVal != val)
                            {
                                row.Cells[j] = newVal;
                            }
                        }
                    }
                }
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
                    
                    TesseraSettings.FontSize = EditorGUILayout.IntPopup("Font Size", TesseraSettings.FontSize, fontSizeLabels, fontSizes);

                    // Row Height
                    TesseraSettings.RowHeight = EditorGUILayout.IntField(new GUIContent("Row Height", "0 for Auto"), TesseraSettings.RowHeight);
                    if (TesseraSettings.RowHeight < 0) TesseraSettings.RowHeight = 0;

                    // Toggles
                    TesseraSettings.AutoSave = EditorGUILayout.Toggle("Auto Save", TesseraSettings.AutoSave);
                    TesseraSettings.ShowLineNumbers = EditorGUILayout.Toggle("Show Line Numbers", TesseraSettings.ShowLineNumbers);
                    TesseraSettings.WordWrap = EditorGUILayout.Toggle("Word Wrap", TesseraSettings.WordWrap);
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

                    int newIndex = EditorGUILayout.Popup("CSV Delimiter", currentIndex, TesseraSettings.AvailableDelimiters);
                    if (newIndex != currentIndex && newIndex >= 0 && newIndex < TesseraSettings.AvailableDelimiters.Length)
                    {
                        TesseraSettings.CsvDelimiter = TesseraSettings.AvailableDelimiters[newIndex];
                    }

                    // Toggles
                    TesseraSettings.TrimWhitespace = EditorGUILayout.Toggle("Trim Whitespace", TesseraSettings.TrimWhitespace);
                    TesseraSettings.ArrayDisplayMultiLine = EditorGUILayout.Toggle("Array Display Multi-line", TesseraSettings.ArrayDisplayMultiLine);
                }
                
                GUILayout.Space(20);
            }
        }

        private void LoadCsvFile()
        {
            string path = EditorUtility.OpenFilePanel("Open CSV File", "", "csv");
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
    }
}
