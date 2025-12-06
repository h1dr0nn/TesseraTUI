using System.Collections.Generic;
using Tessera.Core.Agents;
using Tessera.Core.Models;
using Xunit;

namespace Tessera.Tests.Core;

public class SchemaValidationTests
{
    [Fact]
    public void ValidatesColumnRangeAndNormalization()
    {
        var table = new TableModel(
            new List<ColumnModel> { new("Score") },
            new List<RowModel>
            {
                new(new List<string?> { "10" }),
                new(new List<string?> { "20.5" })
            });

        var schema = new ColumnSchema("Score", DataType.Float, false, 0, 100);
        var validator = new ValidationAgent();

        var report = validator.ValidateColumn(table, 0, schema);

        Assert.True(report.IsValid);
        Assert.Equal("10", report.NormalizedValues[0]);
        Assert.Equal("20.5", report.NormalizedValues[1]);
    }

    [Fact]
    public void RejectsSchemaChangeWhenDataInvalid()
    {
        var table = new TableModel(
            new List<ColumnModel> { new("Flag") },
            new List<RowModel>
            {
                new(new List<string?> { "yes" }),
                new(new List<string?> { "no" })
            });

        var stringSchema = new SchemaModel(new List<ColumnSchema> { new("Flag", DataType.String, false) });
        var dataSync = new DataSyncAgent(table, stringSchema, new JsonModel(new List<Dictionary<string, object?>>()), new ValidationAgent());

        var boolSchema = new ColumnSchema("Flag", DataType.Bool, false);
        var success = dataSync.TryUpdateSchema(0, boolSchema, out var report);

        Assert.False(success);
        Assert.False(report.IsValid);
        Assert.Equal(DataType.String, dataSync.Schema.Columns[0].Type);
        Assert.Equal("yes", dataSync.Table.Rows[0].Cells[0]);
    }
}
