using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
        var headers = ParseLine(lines[0], delimiter);
        var columns = headers.Select(h => new ColumnModel(h ?? string.Empty)).ToList();

        var rows = new List<RowModel>();
        foreach (var line in lines.Skip(1))
        {
            var cells = ParseLine(line, delimiter);
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
        var commaCount = sample.Count(c => c == ',');
        var semicolonCount = sample.Count(c => c == ';');
        return semicolonCount > commaCount ? ';' : ',';
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

        return dataType switch
        {
            DataType.Int when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i,
            DataType.Float when double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            DataType.Bool when bool.TryParse(value, out var b) => b,
            DataType.Date when DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt) => dt,
            _ => value
        };
    }
}
