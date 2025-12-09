using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Globalization;
using Tessera.Core.Models;

namespace Tessera.Core.Agents;

public class JsonAgent
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
    };

    public JsonParseResult Parse(string jsonText)
    {
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return JsonParseResult.Failure("JSON cannot be empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(jsonText);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return JsonParseResult.Failure("JSON must be an array of objects.");
            }

            var records = new List<Dictionary<string, object?>>();
            var index = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    return JsonParseResult.Failure($"Entry at index {index} is not an object.");
                }

                var record = new Dictionary<string, object?>();
                foreach (var property in element.EnumerateObject())
                {
                    record[property.Name] = ConvertElement(property.Value);
                }

                records.Add(record);
                index++;
            }

            return JsonParseResult.Success(new JsonModel(records));
        }
        catch (JsonException ex)
        {
            return JsonParseResult.Failure($"Invalid JSON: {ex.Message}", lineNumber: ex.LineNumber);
        }
    }

    public string Format(string jsonText)
    {
        var parsed = Parse(jsonText);
        if (!parsed.IsValid || parsed.Model is null)
        {
            return jsonText;
        }

        return Serialize(parsed.Model);
    }

    public string Serialize(JsonModel model)
    {
        return JsonSerializer.Serialize(model.Records, _serializerOptions);
    }

    public JsonModel BuildJsonFromTable(TableModel table, SchemaModel schema)
    {
        var records = new List<Dictionary<string, object?>>();
        if (table.Rows.Count == 0 || table.Columns.Count == 0) return new JsonModel(records);

        Dictionary<string, object?>? currentRecord = null;
        // Track values for each column within the current "group" (record)
        // Key: Column Name, Value: List of values encountered
        var currentGroupValues = new Dictionary<string, List<object?>>();

        foreach (var row in table.Rows)
        {
            // Determine if New Record: First column has value?
            var isNewRecord = !string.IsNullOrWhiteSpace(row.Cells[0]);
            
            if (isNewRecord)
            {
                // Flush previous
                if (currentRecord != null)
                {
                    FinalizeRecord(currentRecord, currentGroupValues, records);
                }

                // Start new
                currentRecord = new Dictionary<string, object?>();
                currentGroupValues = new Dictionary<string, List<object?>>();
                foreach (var col in schema.Columns) currentGroupValues[col.Name] = new List<object?>();
            }

            if (currentRecord == null) continue; // Should not happen if first row has ID

            // Collect values
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var colName = table.Columns[i].Name;
                var rawValue = row.Cells[i];
                var dataType = schema.Columns[i].Type;
                
                if (!string.IsNullOrWhiteSpace(rawValue))
                {
                    var converted = ConvertValue(rawValue, dataType);
                    currentGroupValues[colName].Add(converted);
                }
            }
        }

        // Flush last
        if (currentRecord != null)
        {
            FinalizeRecord(currentRecord, currentGroupValues, records);
        }

        return new JsonModel(records);
    }

    private void FinalizeRecord(Dictionary<string, object?> record, Dictionary<string, List<object?>> groupValues, List<Dictionary<string, object?>> records)
    {
        foreach (var kvp in groupValues)
        {
            var key = kvp.Key;
            var values = kvp.Value;

            if (values.Count == 0)
            {
                record[key] = null;
            }
            else if (values.Count == 1)
            {
                // Single value -> Scalar
                record[key] = values[0];
            }
            else
            {
                // Multiple values -> Array
                record[key] = values;
            }
        }
        records.Add(record);
    }

    public TableModel BuildTableFromJson(JsonModel json, SchemaModel schema)
    {
        var columns = schema.Columns.Select(c => new ColumnModel(c.Name)).ToList();
        var rows = new List<RowModel>();

        foreach (var record in json.Records)
        {
            // 1. Analyze record to determine max rows needed
            // Map: ColumnName -> List of Values (Scalar becomes single item list)
            var columnData = new Dictionary<string, List<string?>>();
            int maxDepth = 1;

            foreach (var column in schema.Columns)
            {
                var values = new List<string?>();
                if (record.TryGetValue(column.Name, out var value))
                {
                   if (value is System.Collections.IEnumerable list && value is not string)
                   {
                       foreach (var item in list) values.Add(item?.ToString());
                       if (values.Count == 0) values.Add(null); // Empty array placeholder
                   }
                   else
                   {
                       values.Add(value?.ToString());
                   }
                }
                else
                {
                    values.Add(null);
                }

                columnData[column.Name] = values;
                if (values.Count > maxDepth) maxDepth = values.Count;
            }

            // 2. Generate Rows
            for (int i = 0; i < maxDepth; i++)
            {
                var cells = new List<string?>();
                foreach (var column in schema.Columns)
                {
                    var vals = columnData[column.Name];
                    // Logic:
                    // If it's a scalar (count 1), only show on Row 0 (i=0).
                    // If it's an array (count > 1 or identified as list), map index.
                    
                    if (vals.Count > 1)
                    {
                        // Array: show if index exists in bounds
                        cells.Add(i < vals.Count ? vals[i] : null);
                    }
                    else
                    {
                        // Scalar (or empty list): show only on first row
                         cells.Add(i == 0 && vals.Count > 0 ? vals[0] : null);
                    }
                }
                rows.Add(new RowModel(cells));
            }
        }

        return new TableModel(columns, rows);
    }


    public JsonDiffResult BuildDiff(JsonModel current, JsonModel updated, SchemaModel schema)
    {
        var added = new List<int>();
        var removed = new List<int>();
        var modified = new List<int>();
        var keyMismatches = new List<JsonKeyDifference>();

        var minLength = Math.Min(current.Records.Count, updated.Records.Count);
        for (var i = 0; i < minLength; i++)
        {
            var currentRow = current.Records[i];
            var updatedRow = updated.Records[i];

            foreach (var column in schema.Columns)
            {
                var currentHasKey = currentRow.TryGetValue(column.Name, out var currentValue);
                var updatedHasKey = updatedRow.TryGetValue(column.Name, out var updatedValue);

                if (!currentHasKey || !updatedHasKey)
                {
                    keyMismatches.Add(new JsonKeyDifference(i, column.Name, updatedHasKey ? JsonKeyDifferenceType.Missing : JsonKeyDifferenceType.Unknown));
                    modified.Add(i);
                    continue;
                }

                if (!Equals(NormalizeComparisonValue(currentValue), NormalizeComparisonValue(updatedValue)))
                {
                    modified.Add(i);
                    break;
                }
            }

            foreach (var key in updatedRow.Keys)
            {
                if (schema.Columns.All(c => c.Name != key))
                {
                    keyMismatches.Add(new JsonKeyDifference(i, key, JsonKeyDifferenceType.Unknown));
                    if (!modified.Contains(i))
                    {
                        modified.Add(i);
                    }
                }
            }
        }

        if (updated.Records.Count > current.Records.Count)
        {
            for (var i = current.Records.Count; i < updated.Records.Count; i++)
            {
                added.Add(i);
            }
        }

        if (current.Records.Count > updated.Records.Count)
        {
            for (var i = updated.Records.Count; i < current.Records.Count; i++)
            {
                removed.Add(i);
            }
        }

        return new JsonDiffResult(added, removed, modified.Distinct().OrderBy(i => i).ToList(), keyMismatches);
    }

    private static object? NormalizeComparisonValue(object? value)
    {
        return value switch
        {
            null => null,
            double d => Math.Round(d, 6),
            _ => value,
        };
    }

    private static object? ConvertElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = ConvertElement(prop.Value);
                }
                return dict;

            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertElement(item));
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

    private static object? ConvertValue(string? value, DataType dataType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        switch (dataType)
        {
            case DataType.Int:
                 // Disallow thousands separator
                 return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
            case DataType.Float:
                 // Try strict invariant first (Dot)
                 if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
                 // Fallback: Replace comma with dot for European format "88,9" -> "88.9"
                 if (value.Contains(',') && double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d2)) return d2;
                 return null;
            case DataType.Bool:
                 return bool.TryParse(value, out var b) ? b : null;
            case DataType.Date:
                 return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null;
            default:
                 return value;
        }
    }
}

public record JsonParseResult(bool IsValid, JsonModel? Model, string? ErrorMessage, long? LineNumber)
{
    public static JsonParseResult Success(JsonModel model) => new(true, model, null, null);

    public static JsonParseResult Failure(string message, long? lineNumber = null) => new(false, null, message, lineNumber);
}
