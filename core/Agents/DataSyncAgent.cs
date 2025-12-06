using System;
using System.Collections.Generic;
using System.Linq;
using Tessera.Core.Models;

namespace Tessera.Core.Agents;

public class DataSyncAgent
{
    private readonly JsonAgent _jsonAgent;

    public DataSyncAgent(
        TableModel table,
        SchemaModel schema,
        JsonModel json,
        ValidationAgent? validationAgent = null,
        JsonAgent? jsonAgent = null)
    {
        _jsonAgent = jsonAgent ?? new JsonAgent();
        Validator = validationAgent ?? new ValidationAgent(_jsonAgent);

        EnsureTableMatchesSchema(table, schema);
        Table = table;
        Schema = schema;
        Json = json.Records.Count > 0 ? json : _jsonAgent.BuildJsonFromTable(table, schema);
    }

    public TableModel Table { get; private set; }

    public SchemaModel Schema { get; private set; }

    public JsonModel Json { get; private set; }

    public ValidationAgent Validator { get; }

    public event Action? TableChanged;

    public void ApplyTableEdit(TableModel updatedTable)
    {
        EnsureTableMatchesSchema(updatedTable, Schema);
        Table = updatedTable;
        Json = _jsonAgent.BuildJsonFromTable(Table, Schema);
        TableChanged?.Invoke();
    }

    public void ApplyJsonEdit(JsonModel updatedJson)
    {
        var validation = Validator.ValidateJsonModel(updatedJson, Schema);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("JSON payload failed validation against schema.");
        }

        var tableFromJson = _jsonAgent.BuildTableFromJson(updatedJson, Schema);
        EnsureTableMatchesSchema(tableFromJson, Schema);
        Json = updatedJson;
        Table = tableFromJson;
        TableChanged?.Invoke();
    }

    public bool TryApplyJson(JsonModel updatedJson, out JsonDiffResult diff, out JsonValidationResult validation)
    {
        validation = Validator.ValidateJsonModel(updatedJson, Schema);
        if (!validation.IsValid)
        {
            diff = JsonDiffResult.Empty;
            return false;
        }

        diff = _jsonAgent.BuildDiff(Json, updatedJson, Schema);
        ApplyJsonEdit(updatedJson);
        return true;
    }

    public bool TryUpdateCell(int rowIndex, int columnIndex, string? newValue, out string? normalizedValue, out string? errorMessage)
    {
        normalizedValue = newValue;
        errorMessage = null;

        if (rowIndex < 0 || rowIndex >= Table.Rows.Count)
        {
            errorMessage = "Row index is out of range.";
            return false;
        }

        var validation = Validator.ValidateCell(Schema, columnIndex, newValue);
        if (!validation.IsValid)
        {
            errorMessage = validation.Message;
            return false;
        }

        normalizedValue = validation.NormalizedValue;

        var row = Table.Rows[rowIndex];
        if (columnIndex < 0 || columnIndex >= row.Cells.Count)
        {
            errorMessage = "Column index is out of range.";
            return false;
        }

        row.Cells[columnIndex] = normalizedValue;
        Json = _jsonAgent.BuildJsonFromTable(Table, Schema);
        TableChanged?.Invoke();
        return true;
    }

    public bool TryUpdateSchema(int columnIndex, ColumnSchema updatedSchema, out ValidationReport report)
    {
        report = Validator.ValidateColumn(Table, columnIndex, updatedSchema);
        if (!report.IsValid)
        {
            return false;
        }

        for (var rowIndex = 0; rowIndex < Table.Rows.Count; rowIndex++)
        {
            var row = Table.Rows[rowIndex];
            row.Cells[columnIndex] = report.NormalizedValues.ElementAtOrDefault(rowIndex);
        }

        Schema.Columns[columnIndex] = updatedSchema;
        Json = _jsonAgent.BuildJsonFromTable(Table, Schema);
        TableChanged?.Invoke();
        return true;
    }

    public bool TryRenameColumn(int columnIndex, string newName, out string? errorMessage)
    {
        errorMessage = null;
        if (columnIndex < 0 || columnIndex >= Schema.Columns.Count)
        {
            errorMessage = "Column index is out of range.";
            return false;
        }

        Schema.Columns[columnIndex].Name = newName;
        Table.Columns[columnIndex] = new ColumnModel(newName);
        Json = _jsonAgent.BuildJsonFromTable(Table, Schema);
        TableChanged?.Invoke();
        return true;
    }

    private static void EnsureTableMatchesSchema(TableModel table, SchemaModel schema)
    {
        if (table.Columns.Count != schema.Columns.Count)
        {
            throw new InvalidOperationException("Table columns do not match schema definition.");
        }

        foreach (var row in table.Rows)
        {
            if (row.Cells.Count != table.Columns.Count)
            {
                throw new InvalidOperationException("Row does not match table column count.");
            }

            for (var i = 0; i < row.Cells.Count; i++)
            {
                var value = row.Cells[i];
                if (!IsValueCompatible(value, schema.Columns[i].Type))
                {
                    throw new InvalidOperationException($"Value '{value}' is incompatible with schema type {schema.Columns[i].Type}.");
                }
            }
        }
    }

    private static bool IsValueCompatible(string? value, DataType dataType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return dataType switch
        {
            DataType.Int => int.TryParse(value, out _),
            DataType.Float => double.TryParse(value, out _),
            DataType.Bool => bool.TryParse(value, out _),
            DataType.Date => DateTime.TryParse(value, out _),
            _ => true
        };
    }

}
