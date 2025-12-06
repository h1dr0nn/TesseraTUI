using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tessera.Core.Models;

namespace Tessera.Core.Agents;

public class SchemaAgent
{
    public SchemaModel InferSchema(TableModel table)
    {
        var columns = new List<ColumnSchema>();

        for (var i = 0; i < table.Columns.Count; i++)
        {
            var values = table.Rows
                .Select(r => i < r.Cells.Count ? r.Cells[i] : null)
                .ToList();

            columns.Add(InferColumn(table.Columns[i].Name, values));
        }

        return new SchemaModel(columns);
    }

    public ColumnSchema InferColumn(string name, List<string?> values)
    {
        var nonNullValues = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .ToList();

        var isNullable = values.Any(string.IsNullOrWhiteSpace);
        var sampleValues = values.Take(5).ToList();
        var distinct = new HashSet<string?>(values);

        if (nonNullValues.Count == 0)
        {
            return new ColumnSchema(name, DataType.String, true, null, null, distinct.Count, sampleValues);
        }

        var inferredType = InferDataType(nonNullValues);
        var stats = CalculateStats(nonNullValues, inferredType);

        return new ColumnSchema(
            name,
            inferredType,
            isNullable,
            stats.Min,
            stats.Max,
            distinct.Count,
            sampleValues);
    }

    private static DataType InferDataType(List<string> values)
    {
        if (values.All(IsBool))
        {
            return DataType.Bool;
        }

        if (values.All(IsInt))
        {
            return DataType.Int;
        }

        if (values.All(IsFloat))
        {
            return DataType.Float;
        }

        if (values.All(IsDate))
        {
            return DataType.Date;
        }

        return DataType.String;
    }

    private static (double? Min, double? Max) CalculateStats(List<string> values, DataType type)
    {
        if (type is not DataType.Int and not DataType.Float)
        {
            return (null, null);
        }

        var numbers = new List<double>();
        foreach (var value in values)
        {
            if (TryParseNumber(value, out var number))
            {
                numbers.Add(number);
            }
        }

        if (numbers.Count == 0)
        {
            return (null, null);
        }

        return (numbers.Min(), numbers.Max());
    }

    private static bool IsBool(string value) => bool.TryParse(value, out _);

    private static bool IsInt(string value) => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    private static bool IsFloat(string value)
    {
        if (IsInt(value))
        {
            return true;
        }

        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsDate(string value) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool TryParseNumber(string value, out double number)
    {
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out number))
        {
            return true;
        }

        number = 0;
        return false;
    }
}
