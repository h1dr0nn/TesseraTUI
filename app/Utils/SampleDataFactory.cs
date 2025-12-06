using System;
using System.Collections.Generic;
using System.Linq;
using Tessera.Core.Agents;
using Tessera.Core.Models;

namespace Tessera.Utils;

public static class SampleDataFactory
{
    public static (TableModel Table, SchemaModel Schema, JsonModel Json, ValidationAgent Validator, JsonAgent JsonAgent) CreateWorkspace()
    {
        var schema = BuildSampleSchema();
        var table = BuildSampleTable(schema);
        var jsonAgent = new JsonAgent();
        var validator = new ValidationAgent(jsonAgent);
        var json = jsonAgent.BuildJsonFromTable(table, schema);

        return (table, schema, json, validator, jsonAgent);
    }

    public static SchemaModel BuildSampleSchema()
    {
        return new SchemaModel(new List<ColumnSchema>
        {
            new("Name", DataType.String, false),
            new("Active", DataType.Bool, false),
            new("Score", DataType.Int, false, 0, 5000),
            new("Date", DataType.Date, false)
        });
    }

    public static TableModel BuildSampleTable(SchemaModel schema, int rowCount = 50)
    {
        var columns = schema.Columns.Select(c => new ColumnModel(c.Name)).ToList();
        var rows = new List<RowModel>();
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < rowCount; i++)
        {
            rows.Add(new RowModel(new List<string?>
            {
                $"Row {i}",
                (i % 2 == 0).ToString(),
                (i * 10).ToString(),
                baseDate.AddDays(i).ToString("yyyy-MM-dd")
            }));
        }

        return new TableModel(columns, rows);
    }
}
