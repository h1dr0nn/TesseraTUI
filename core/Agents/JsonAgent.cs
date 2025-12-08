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

        foreach (var row in table.Rows)
        {
            var record = new Dictionary<string, object?>();
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var columnName = table.Columns[i].Name;
                var schemaColumn = schema.Columns[i];
                record[columnName] = ConvertValue(row.Cells[i], schemaColumn.Type);
            }

            records.Add(record);
        }

        return new JsonModel(records);
    }

    public TableModel BuildTableFromJson(JsonModel json, SchemaModel schema)
    {
        var columns = schema.Columns.Select(c => new ColumnModel(c.Name)).ToList();
        var rows = new List<RowModel>();

        foreach (var record in json.Records)
        {
            var cells = new List<string?>();
            foreach (var column in schema.Columns)
            {
                record.TryGetValue(column.Name, out var value);
                cells.Add(value?.ToString());
            }

            rows.Add(new RowModel(cells));
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
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };
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
