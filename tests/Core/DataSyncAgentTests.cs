using System;
using System.Collections.Generic;
using Tessera.Core.Agents;
using Tessera.Core.Models;
using Xunit;

namespace Tessera.Tests.Core;

public class DataSyncAgentTests
{
    [Fact]
    public void RejectsTableUpdatesThatBreakSchema()
    {
        var schema = new SchemaModel(new List<ColumnSchema> { new("Id", DataType.Int, false) });
        var initialTable = new TableModel(new List<ColumnModel> { new("Id") }, new List<RowModel> { new(new List<string?> { "1" }) });
        var json = new JsonModel(new List<Dictionary<string, object?>>());
        var agent = new DataSyncAgent(initialTable, schema, json);

        var badTable = new TableModel(new List<ColumnModel> { new("Id") }, new List<RowModel> { new(new List<string?> { "abc" }) });

        Assert.Throws<InvalidOperationException>(() => agent.ApplyTableEdit(badTable));
    }

    [Fact]
    public void UpdatesTableFromJsonWhenValid()
    {
        var schemaColumns = new List<ColumnSchema> { new("Name", DataType.String, false), new("Active", DataType.Bool, false) };
        var schema = new SchemaModel(schemaColumns);
        var initialTable = new TableModel(
            new List<ColumnModel> { new("Name"), new("Active") },
            new List<RowModel> { new(new List<string?> { "Alice", "true" }) });
        var json = new JsonModel(new List<Dictionary<string, object?>>());
        var agent = new DataSyncAgent(initialTable, schema, json);

        var updatedJson = new JsonModel(new List<Dictionary<string, object?>>
        {
            new()
            {
                { "Name", "Bob" },
                { "Active", false }
            }
        });

        agent.ApplyJsonEdit(updatedJson);

        Assert.Equal("Bob", agent.Table.Rows[0].Cells[0]);
        Assert.Equal("False", agent.Table.Rows[0].Cells[1]);
        Assert.Equal(updatedJson, agent.Json);
    }
}
