using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Tessera.Core.Models;

namespace Tessera.Editor
{
    public class SchemaViewComponent
    {
        private TesseraEditorState _state;
        private Vector2 _scrollPosition;
        
        public SchemaViewComponent(TesseraEditorState state)
        {
            _state = state;
        }

        public void Refresh()
        {
            // No-op for IMGUI as OnGUI redraws every frame, 
            // but kept for compatibility with TesseraWindow calls.
        }
        
        public void DrawGUI()
        {
            if (_state.Schema == null || _state.Schema.Columns.Count == 0)
            {
                GUILayout.Label("No schema available", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scroll.scrollPosition;
                
                EditorGUILayout.LabelField("Column Schemas", EditorStyles.boldLabel);
                GUILayout.Space(10);
                
                for (int i = 0; i < _state.Schema.Columns.Count; i++)
                {
                    DrawColumnSchemaCard(_state.Schema.Columns[i], i);
                }
            }
        }
        
        private void DrawColumnSchemaCard(ColumnSchema column, int index)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                // Column name (editable)
                string newName = EditorGUILayout.TextField("Column Name", column.Name);
                if (newName != column.Name && !string.IsNullOrWhiteSpace(newName))
                {
                    // Update schema
                    column.Name = newName;
                    
                    // Also update Table.Columns to keep them in sync
                    if (_state.Table != null && index < _state.Table.Columns.Count)
                    {
                        _state.Table.Columns[index] = new ColumnModel(newName);
                    }
                }
                
                // Type dropdown
                var newType = (DataType)EditorGUILayout.EnumPopup("Data Type", column.Type);
                if (newType != column.Type)
                {
                    column.Type = newType;
                }
                
                // Nullable toggle
                column.IsNullable = EditorGUILayout.Toggle("Nullable", column.IsNullable);
                
                // Min/Max for numeric types
                if (column.Type == DataType.Int || column.Type == DataType.Float)
                {
                    double currentMin = column.Min ?? double.MinValue;
                    double currentMax = column.Max ?? double.MaxValue;
                    
                    // Specific handling for Int vs Float display could be added, but DoubleField works for both storage
                    double newMin = EditorGUILayout.DoubleField("Min Value", currentMin);
                    if (newMin != currentMin) column.Min = newMin;

                    double newMax = EditorGUILayout.DoubleField("Max Value", currentMax);
                    if (newMax != currentMax) column.Max = newMax;
                }
                
                // Stats
                GUILayout.Space(5);
                var sampleValue = column.SampleValues.Count > 0 ? column.SampleValues[0] ?? "N/A" : "N/A";
                EditorGUILayout.LabelField($"Distinct: {column.DistinctCount}, Sample: {sampleValue}", EditorStyles.miniLabel);
            }
            GUILayout.Space(5);
        }
    }
}
