using UnityEditor;
using UnityEngine;
using Tessera.Core.Agents;
using Tessera.Core.IO;
using Tessera.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;

namespace Tessera.Editor
{
    public class TesseraEditorState
    {
        public string CurrentFilePath { get; set; }
        public TableModel Table { get; set; }
        public SchemaModel Schema { get; set; }
        
        // Core components
        private CsvLoader _csvLoader;
        private JsonAgent _jsonAgent;
        public SchemaAgent SchemaAgent { get; private set; }
        public ValidationAgent ValidationAgent { get; private set; }
        
        public TesseraEditorState()
        {
            _csvLoader = new CsvLoader();
            _jsonAgent = new JsonAgent();
            SchemaAgent = new SchemaAgent();
            ValidationAgent = new ValidationAgent();
        }
        
        public void LoadCsv(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[Tessera] File not found: {filePath}");
                return;
            }
            
            try
            {
                CurrentFilePath = filePath;
                
                // Read file content
                var fileContent = File.ReadAllText(filePath, Encoding.UTF8);
                
                // Check if it's a JSON file (by extension or content)
                var isJsonFile = filePath.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase);
                var trimmedContent = fileContent.TrimStart();
                var looksLikeJson = trimmedContent.StartsWith("[") || trimmedContent.StartsWith("{");
                
                if (isJsonFile || looksLikeJson)
                {
                    // Parse as JSON
                    var jsonResult = _jsonAgent.Parse(fileContent);
                    if (jsonResult.IsValid && jsonResult.Model != null)
                    {
                        var jsonSchema = InferSchemaFromJson(jsonResult.Model);
                        var jsonTable = _jsonAgent.BuildTableFromJson(jsonResult.Model, jsonSchema, TesseraSettings.ArrayDisplayMultiLine);
                        
                        Table = jsonTable;
                        Schema = jsonSchema;
                        Debug.Log($"[Tessera] Loaded JSON: {Path.GetFileName(filePath)} - {Table.Rows.Count} rows, {Table.Columns.Count} columns");
                    }
                    else
                    {
                        Debug.LogError($"[Tessera] Failed to parse JSON: {jsonResult.ErrorMessage}");
                    }
                }
                else
                {
                    // Parse as CSV
                    var (table, schema, json) = _csvLoader.Load(filePath);
                    
                    Table = table;
                    Schema = schema;
                    Debug.Log($"[Tessera] Loaded CSV: {Path.GetFileName(filePath)} - {Table.Rows.Count} rows, {Table.Columns.Count} columns");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Tessera] Failed to load file: {ex.Message}");
            }
        }
        
        public void SaveCsv()
        {
            if (string.IsNullOrEmpty(CurrentFilePath) || Table == null || Schema == null)
            {
                Debug.LogWarning("[Tessera] No file loaded to save");
                return;
            }
            
            try
            {
                var isJsonFile = CurrentFilePath.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase);
                
                if (isJsonFile)
                {
                    // Save as JSON
                    var jsonModel = _jsonAgent.BuildJsonFromTable(Table, Schema);
                    var jsonContent = _jsonAgent.Serialize(jsonModel);
                    File.WriteAllText(CurrentFilePath, jsonContent, Encoding.UTF8);
                    Debug.Log($"[Tessera] Saved JSON: {Path.GetFileName(CurrentFilePath)}");
                }
                else
                {
                    // Save as CSV
                    var headers = Table.Columns.Select(c => c.Name).ToList();
                    var rows = Table.Rows.Select(r => r.Cells).ToList();
                    
                    var csvContent = SerializeCsv(headers, rows, TesseraSettings.GetDelimiterChar());
                    File.WriteAllText(CurrentFilePath, csvContent, Encoding.UTF8);
                    
                    Debug.Log($"[Tessera] Saved CSV: {Path.GetFileName(CurrentFilePath)}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Tessera] Failed to save file: {ex.Message}");
            }
        }
        
        private string SerializeCsv(List<string> headers, List<List<string?>> rows, char delimiter = ',')
        {
            var builder = new StringBuilder();
            
            // Write headers
            builder.Append(string.Join(delimiter.ToString(), headers.Select(h => EscapeCsvValue(h, delimiter))));
            builder.AppendLine();
            
            // Write rows
            foreach (var row in rows)
            {
                // Trim whitespace if enabled
                var processedRow = TesseraSettings.TrimWhitespace 
                    ? row.Select(c => c?.Trim()).ToList() 
                    : row;

                builder.Append(string.Join(delimiter.ToString(), processedRow.Select(v => EscapeCsvValue(v, delimiter))));
                builder.AppendLine();
            }
            
            return builder.ToString();
        }
        
        private string EscapeCsvValue(string? value, char delimiter)
        {
            var safe = value ?? string.Empty;
            var requiresQuotes = safe.IndexOfAny(new[] { delimiter, '\n', '\r', '"' }) >= 0;
            
            if (safe.Contains('"'))
            {
                safe = safe.Replace("\"", "\"\"");
            }
            
            return requiresQuotes ? $"\"{safe}\"" : safe;
        }
        
        private SchemaModel InferSchemaFromJson(JsonModel model)
        {
            // 1. Collect all unique keys
            var keys = new List<string>();
            foreach (var record in model.Records)
            {
                foreach (var key in record.Keys)
                {
                    if (!keys.Contains(key)) keys.Add(key);
                }
            }

            // 2. Infer types for each key
            var schemas = new List<ColumnSchema>();
            foreach (var key in keys)
            {
                var values = model.Records.Select(r => r.TryGetValue(key, out var v) ? v : null).ToList();
                var type = InferTypeFromValues(values);
                schemas.Add(new ColumnSchema(key, type, true));
            }

            return new SchemaModel(schemas);
        }

        private DataType InferTypeFromValues(List<object?> values)
        {
            bool canBeInt = true;
            bool canBeFloat = true;
            bool canBeBool = true;
            bool canBeDate = true;
            bool hasValues = false;

            foreach (var val in values)
            {
                if (val == null) continue;
                hasValues = true;

                // Strict Type Checks based on JSON types
                if (val is long)
                {
                    // Int fits in Float, forbids Bool/Date
                    canBeBool = false;
                    canBeDate = false;
                }
                else if (val is double)
                {
                    canBeInt = false;
                    canBeBool = false;
                    canBeDate = false;
                }
                else if (val is bool)
                {
                    canBeInt = false;
                    canBeFloat = false;
                    canBeDate = false;
                }
                else if (val is string s)
                {
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    
                    // If it's a string, it kills Int/Float/Bool direct typing
                    canBeInt = false;
                    canBeFloat = false;
                    canBeBool = false;

                    if (canBeDate && !DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    {
                        canBeDate = false;
                    }
                }
                else
                {
                    // Complex object or array -> Treat as String (serialized)
                    return DataType.String;
                }

                if (!canBeInt && !canBeFloat && !canBeBool && !canBeDate) return DataType.String;
            }

            if (!hasValues) return DataType.String;

            if (canBeInt) return DataType.Int;
            if (canBeFloat) return DataType.Float;
            if (canBeBool) return DataType.Bool;
            if (canBeDate) return DataType.Date;

            return DataType.String;
        }
    }
}
