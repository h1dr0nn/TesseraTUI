using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Tessera.Core.Agents;
using Tessera.Core.Models;

namespace Tessera.Core.IO;

public class CsvLoader
{
    public (TableModel Table, SchemaModel Schema, JsonModel Json) Load(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, true);
        var lines = new List<string>();

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            lines.Add(line);
        }

        if (lines.Count == 0)
        {
            var emptyTable = new TableModel(new List<ColumnModel>(), new List<RowModel>());
            var emptySchema = new SchemaModel(new List<ColumnSchema>());
            return (emptyTable, emptySchema, new JsonModel(new List<Dictionary<string, object?>>()));
        }

        var delimiter = DetectDelimiter(lines[0]);
        Console.WriteLine($"[Tessera] Detected delimiter: '{delimiter}' for line: {lines[0]}");
        
        var headers = ParseLine(lines[0], delimiter);
        Console.WriteLine($"[Tessera] Parsed {headers.Count} columns from header.");
        
        var columns = headers.Select(h => new ColumnModel(h ?? string.Empty)).ToList();

        var rows = new List<RowModel>();
        foreach (var line in lines.Skip(1))
        {
            var cells = ParseLine(line, delimiter);
            if (rows.Count < 5) Console.WriteLine($"[Tessera] Row {rows.Count} parsed into {cells.Count} cells: {string.Join(" | ", cells)}");
            rows.Add(new RowModel(cells));
        }

        var table = new TableModel(columns, rows);
        var schemaAgent = new SchemaAgent();
        var schema = schemaAgent.InferSchema(table);
        var json = ToJsonModel(table, schema);

        return (table, schema, json);
    }

    public SchemaModel InferSchema(TableModel table)
    {
        var agent = new SchemaAgent();
        return agent.InferSchema(table);
    }

    public char DetectDelimiter(string sample)
    {
        // Robust detection ignoring quoted sections
        int commaCount = 0;
        int semicolonCount = 0;
        bool inQuotes = false;
        
        for (int i = 0; i < sample.Length; i++)
        {
            char c = sample[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes)
            {
                if (c == ',') commaCount++;
                else if (c == ';') semicolonCount++;
            }
        }
        
        return semicolonCount > commaCount ? ';' : ',';
    }

    private List<string?> ParseLine(string line, char delimiter)
    {
        var values = new List<string?>();
        var current = new StringBuilder();
        var inQuotes = false;
        
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    // Check for escaped quote ("")
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // Skip next quote
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
                    var value = current.ToString();
                    // Trim only whitespace, preserve empty strings as empty (not null)
                    values.Add(value);
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
        
        // Add last value
        values.Add(current.ToString());
        
        return values;
    }

    private JsonModel ToJsonModel(TableModel table, SchemaModel schema)
    {
        var records = new List<Dictionary<string, object?>>();

        foreach (var row in table.Rows)
        {
            var record = new Dictionary<string, object?>();
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var columnName = table.Columns[i].Name;
                var schemaColumn = schema.Columns.ElementAtOrDefault(i);
                var value = i < row.Cells.Count ? row.Cells[i] : null;
                record[columnName] = schemaColumn == null ? value : ParseByType(value, schemaColumn.Type);
            }

            records.Add(record);
        }

        return new JsonModel(records);
    }

    private object? ParseByType(string? value, DataType dataType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Try to detect and parse JSON arrays/objects from CSV cells
        // This handles cases where CSV contains JSON arrays like: {"x":0,"y":3},{"x":0,"y":2},...
        var trimmed = value.TrimStart();
        
        // Check for comma-separated JSON objects (array in single-line mode from CSV)
        if (trimmed.StartsWith("{"))
        {
            var openBraces = value.Count(c => c == '{');
            if (openBraces > 1 || value.Contains("},{"))
            {
                try
                {
                    // Try to parse as JSON array by wrapping in brackets
                    var jsonArray = "[" + value + "]";
                    using var doc = JsonDocument.Parse(jsonArray);
                    return ConvertJsonElement(doc.RootElement);
                }
                catch
                {
                    // If that fails, try splitting by "},{" pattern
                    try
                    {
                        var parts = value.Split(new[] { "},{" }, StringSplitOptions.None);
                        var objects = new List<object?>();
                        
                        for (int i = 0; i < parts.Length; i++)
                        {
                            var part = parts[i];
                            if (i == 0 && i == parts.Length - 1)
                            {
                                // Single object
                            }
                            else if (i == 0)
                            {
                                part = part + "}";
                            }
                            else if (i == parts.Length - 1)
                            {
                                part = "{" + part;
                            }
                            else
                            {
                                part = "{" + part + "}";
                            }
                            
                            try
                            {
                                using var doc = JsonDocument.Parse(part);
                                objects.Add(ConvertJsonElement(doc.RootElement));
                            }
                            catch
                            {
                                // Skip invalid objects
                            }
                        }
                        
                        if (objects.Count > 0)
                        {
                            return objects;
                        }
                    }
                    catch
                    {
                        // Fall through to normal parsing
                    }
                }
            }
            else
            {
                // Single JSON object
                try
                {
                    using var doc = JsonDocument.Parse(value);
                    return ConvertJsonElement(doc.RootElement);
                }
                catch
                {
                    // Not valid JSON, continue with normal parsing
                }
            }
        }
        
        // Try to parse as JSON array
        if (trimmed.StartsWith("["))
        {
            try
            {
                using var doc = JsonDocument.Parse(value);
                return ConvertJsonElement(doc.RootElement);
            }
            catch
            {
                // Not valid JSON, continue with normal parsing
            }
        }

        return dataType switch
        {
            DataType.Int when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i,
            DataType.Float when double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            DataType.Bool when bool.TryParse(value, out var b) => b,
            DataType.Date when DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt) => dt,
            _ => value
        };
    }
    
    private static object? ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = ConvertJsonElement(prop.Value);
                }
                return dict;

            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertJsonElement(item));
                }
                return list;

            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l)) return l;
                return element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
            default:
                return element.GetRawText();
        }
    }
}
