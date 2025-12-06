using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
            var emptySchema = new SchemaModel(new List<SchemaColumn>());
            return (emptyTable, emptySchema, new JsonModel(new List<Dictionary<string, object?>>()));
        }

        var delimiter = DetectDelimiter(lines[0]);
        var headers = ParseLine(lines[0], delimiter);
        var columns = headers.Select(h => new ColumnModel(h ?? string.Empty)).ToList();

        var rows = new List<RowModel>();
        foreach (var line in lines.Skip(1))
        {
            var cells = ParseLine(line, delimiter);
            rows.Add(new RowModel(cells));
        }

        var table = new TableModel(columns, rows);
        var schema = InferSchema(table);
        var json = ToJsonModel(table, schema);

        return (table, schema, json);
    }

    public SchemaModel InferSchema(TableModel table)
    {
        var columns = new List<SchemaColumn>();

        for (var i = 0; i < table.Columns.Count; i++)
        {
            var values = table.Rows
                .Select(r => i < r.Cells.Count ? r.Cells[i] : null)
                .ToList();

            columns.Add(InferColumn(table.Columns[i].Name, values));
        }

        return new SchemaModel(columns);
    }

    public char DetectDelimiter(string sample)
    {
        var commaCount = sample.Count(c => c == ',');
        var semicolonCount = sample.Count(c => c == ';');
        return semicolonCount > commaCount ? ';' : ',';
    }

    private SchemaColumn InferColumn(string name, List<string?> values)
    {
        var nonNullValues = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        var isNullable = values.Any(string.IsNullOrWhiteSpace);

        if (nonNullValues.Count == 0)
        {
            return new SchemaColumn(name, DataType.String, true);
        }

        var allBool = nonNullValues.All(IsBool);
        var allInt = nonNullValues.All(IsInt);
        var allFloat = nonNullValues.All(IsFloat);
        var allDate = nonNullValues.All(IsDate);

        if (allBool)
        {
            return new SchemaColumn(name, DataType.Bool, isNullable);
        }

        if (allInt)
        {
            return new SchemaColumn(name, DataType.Int, isNullable);
        }

        if (allFloat && !allInt)
        {
            return new SchemaColumn(name, DataType.Float, isNullable);
        }

        if (allDate)
        {
            return new SchemaColumn(name, DataType.Date, isNullable);
        }

        return new SchemaColumn(name, DataType.String, isNullable);
    }

    private List<string?> ParseLine(string line, char delimiter)
    {
        return line
            .Split(delimiter)
            .Select(cell =>
            {
                var trimmed = cell.Trim();
                return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
            })
            .ToList();
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
                record[columnName] = schemaColumn == null ? value : ParseByType(value, schemaColumn.InferredType);
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

        return dataType switch
        {
            DataType.Int when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i,
            DataType.Float when double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            DataType.Bool when bool.TryParse(value, out var b) => b,
            DataType.Date when DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt) => dt,
            _ => value
        };
    }

    private bool IsBool(string value) => bool.TryParse(value, out _);

    private bool IsInt(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    private bool IsFloat(string value) => double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _);

    private bool IsDate(string value) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
