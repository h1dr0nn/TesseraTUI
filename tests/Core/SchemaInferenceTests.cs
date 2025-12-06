using System.Collections.Generic;
using Tessera.Core.IO;
using Tessera.Core.Models;
using Xunit;

namespace Tessera.Tests.Core;

public class SchemaInferenceTests
{
    [Fact]
    public void InfersBasicTypes()
    {
        var columns = new List<ColumnModel> { new("Id"), new("Value"), new("Flag") };
        var rows = new List<RowModel>
        {
            new(new List<string?> { "1", "10.5", "true" }),
            new(new List<string?> { "2", "11.0", "false" }),
            new(new List<string?> { "3", null, "true" })
        };
        var table = new TableModel(columns, rows);

        var loader = new CsvLoader();
        var schema = loader.InferSchema(table);

        Assert.Equal(DataType.Int, schema.Columns[0].Type);
        Assert.Equal(DataType.Float, schema.Columns[1].Type);
        Assert.True(schema.Columns[1].IsNullable);
        Assert.Equal(DataType.Bool, schema.Columns[2].Type);
        Assert.Equal(3, schema.Columns[0].DistinctCount);
        Assert.Contains("10.5", schema.Columns[1].SampleValues);
    }

    [Fact]
    public void PrefersIntegersOverFloatsWhenAllValuesWhole()
    {
        var columns = new List<ColumnModel> { new("Count") };
        var rows = new List<RowModel>
        {
            new(new List<string?> { "10" }),
            new(new List<string?> { "20" })
        };

        var table = new TableModel(columns, rows);
        var loader = new CsvLoader();
        var schema = loader.InferSchema(table);

        Assert.Equal(DataType.Int, schema.Columns[0].Type);
    }
}
