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

public class SchemaColumn
{
    public SchemaColumn(string name, DataType inferredType, bool isNullable)
    {
        Name = name;
        InferredType = inferredType;
        IsNullable = isNullable;
    }

    public string Name { get; }

    public DataType InferredType { get; }

    public bool IsNullable { get; }
}

public class SchemaModel
{
    public SchemaModel(List<SchemaColumn> columns)
    {
        Columns = columns;
    }

    public List<SchemaColumn> Columns { get; }
}
