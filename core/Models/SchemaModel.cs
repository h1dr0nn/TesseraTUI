using System.Collections.Generic;

namespace Tessera.Core.Models;

public enum DataType
{
    String,
    Int,
    Float,
    Bool,
    Date
}

public class ColumnSchema
{
    public ColumnSchema(
        string name,
        DataType type,
        bool isNullable,
        double? min = null,
        double? max = null,
        int distinctCount = 0,
        List<string?>? sampleValues = null)
    {
        Name = name;
        Type = type;
        IsNullable = isNullable;
        Min = min;
        Max = max;
        DistinctCount = distinctCount;
        SampleValues = sampleValues ?? new List<string?>();
    }

    public string Name { get; set; }

    public DataType Type { get; set; }

    public bool IsNullable { get; set; }

    public double? Min { get; set; }

    public double? Max { get; set; }

    public int DistinctCount { get; set; }

    public List<string?> SampleValues { get; }
}

public class SchemaModel
{
    public SchemaModel(List<ColumnSchema> columns)
    {
        Columns = columns;
    }

    public List<ColumnSchema> Columns { get; }
}
