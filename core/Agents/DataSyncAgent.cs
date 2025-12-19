using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Tessera.Core.Models;

namespace Tessera.Core.Agents;

public class DataSyncAgent
{
    private readonly JsonAgent _jsonAgent;
    private readonly FormulaAgent _formulaAgent;
    private bool _arrayDisplayMultiLine = true;

    public DataSyncAgent(
        TableModel table,
        SchemaModel schema,
        JsonModel json,
        ValidationAgent? validationAgent = null,
        JsonAgent? jsonAgent = null,
        FormulaAgent? formulaAgent = null)
    {
        _jsonAgent = jsonAgent ?? new JsonAgent();
        Validator = validationAgent ?? new ValidationAgent(_jsonAgent);
        _formulaAgent = formulaAgent ?? new FormulaAgent();

        // Subscribe to formula calculation events
        _formulaAgent.FormulaCalculated += OnFormulaCalculated;

        EnsureTableMatchesSchema(table, schema);
        Table = table;
        Schema = schema;
        Json = json.Records.Count > 0 ? json : _jsonAgent.BuildJsonFromTable(table, schema);
    }

    public bool ArrayDisplayMultiLine
    {
        get => _arrayDisplayMultiLine;
        set
        {
            if (_arrayDisplayMultiLine != value)
            {
                _arrayDisplayMultiLine = value;
                // Refresh table if JSON exists
                if (Json.Records.Count > 0)
                {
                    var tableFromJson = _jsonAgent.BuildTableFromJson(Json, Schema, _arrayDisplayMultiLine);
                    EnsureTableMatchesSchema(tableFromJson, Schema);
                    Table = tableFromJson;
                    TableChanged?.Invoke();
                }
            }
        }
    }

    public TableModel Table { get; private set; }

    public SchemaModel Schema { get; private set; }

    public JsonModel Json { get; private set; }

    public ValidationAgent Validator { get; }

    public FormulaAgent FormulaAgent => _formulaAgent;

    public event Action? TableChanged;
    
    public void NotifyTableChanged() => TableChanged?.Invoke();

    public void LoadNewData(TableModel table, SchemaModel schema)
    {
        EnsureTableMatchesSchema(table, schema);
        Table = table;
        Schema = schema;
        _formulaAgent.ClearAll();
        RecalculateAllFormulas();
        RecalculateSchemaStats();
        Json = _jsonAgent.BuildJsonFromTable(table, schema);
        TableChanged?.Invoke();
    }

    public void ApplyTableEdit(TableModel updatedTable)
    {
        EnsureTableMatchesSchema(updatedTable, Schema);
        Table = updatedTable;
        RecalculateSchemaStats();
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

        var tableFromJson = _jsonAgent.BuildTableFromJson(updatedJson, Schema, _arrayDisplayMultiLine);
        EnsureTableMatchesSchema(tableFromJson, Schema);
        Json = updatedJson;
        Table = tableFromJson;
        RecalculateSchemaStats();
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

        var row = Table.Rows[rowIndex];
        if (columnIndex < 0 || columnIndex >= row.Cells.Count)
        {
            errorMessage = "Column index is out of range.";
            return false;
        }

        // Check if this is a formula
        if (!string.IsNullOrWhiteSpace(newValue) && newValue.Trim().StartsWith('='))
        {
            var formula = newValue.Trim();
            
            // Check for circular dependency before setting
            var tempFormulaAgent = new FormulaAgent();
            tempFormulaAgent.SetFormula(rowIndex, columnIndex, formula);
            if (tempFormulaAgent.HasCircularDependency(rowIndex, columnIndex))
            {
                errorMessage = "Circular dependency detected in formula";
                return false;
            }

            // Set formula in FormulaAgent
            _formulaAgent.SetFormula(rowIndex, columnIndex, formula);

            // Calculate formula result
            var (result, calcError) = _formulaAgent.CalculateFormula(rowIndex, columnIndex, Table, Schema);
            if (calcError != null)
            {
                errorMessage = calcError;
                return false;
            }

            // Store formula string in cell (not the result)
            row.Cells[columnIndex] = formula;
            normalizedValue = formula;

            // Validate the calculated result against schema (type check only, no normalization)
            // This ensures the formula result is compatible with column type
            var validation = Validator.ValidateCell(Schema, columnIndex, result);
            if (!validation.IsValid)
            {
                errorMessage = $"Formula result validation failed: {validation.Message}";
                // Formula is already stored, but mark as invalid
                return false;
            }

            // Recalculate dependent formulas
            _formulaAgent.RecalculateDependentCells(rowIndex, columnIndex, Table, Schema);
        }
        else
        {
            // Regular cell update (not a formula)
            // Clear formula if it existed
            if (_formulaAgent.HasFormula(rowIndex, columnIndex))
            {
                _formulaAgent.ClearFormula(rowIndex, columnIndex);
            }

            var validation = Validator.ValidateCell(Schema, columnIndex, newValue);
            if (!validation.IsValid)
            {
                errorMessage = validation.Message;
                return false;
            }

            normalizedValue = validation.NormalizedValue;
            row.Cells[columnIndex] = normalizedValue;

            // Recalculate formulas that might depend on this cell
            _formulaAgent.RecalculateDependentCells(rowIndex, columnIndex, Table, Schema);
        }

        RecalculateSchemaStats();
        Json = _jsonAgent.BuildJsonFromTable(Table, Schema);
        TableChanged?.Invoke();
        return true;
    }

    public bool TryUpdateSchema(int columnIndex, ColumnSchema updatedSchema, out ValidationReport report)
    {
        // Build validation report, but skip formula cells (they shouldn't be normalized)
        var errors = new List<ValidationError>();
        var normalized = new List<string?>();

        for (var rowIndex = 0; rowIndex < Table.Rows.Count; rowIndex++)
        {
            var row = Table.Rows[rowIndex];
            var cellValue = row.Cells.ElementAtOrDefault(columnIndex);
            
            // Skip validation/normalization for formula cells
            if (!string.IsNullOrWhiteSpace(cellValue) && cellValue.Trim().StartsWith('='))
            {
                normalized.Add(cellValue); // Keep formula as-is
                continue;
            }
            
            var result = Validator.ValidateCell(updatedSchema, cellValue);
            if (!result.IsValid)
            {
                errors.Add(new ValidationError(rowIndex, result.Message ?? "Invalid value"));
                normalized.Add(cellValue);
                continue;
            }

            normalized.Add(result.NormalizedValue);
        }

        report = new ValidationReport(errors.Count == 0, errors, normalized);
        if (!report.IsValid)
        {
            return false;
        }

        // Apply normalized values, but preserve formulas
        for (var rowIndex = 0; rowIndex < Table.Rows.Count; rowIndex++)
        {
            var row = Table.Rows[rowIndex];
            var normalizedValue = report.NormalizedValues.ElementAtOrDefault(rowIndex);
            
            // Only update if not a formula (formulas are already in normalized list as-is)
            if (normalizedValue != null && !normalizedValue.Trim().StartsWith('='))
            {
                row.Cells[columnIndex] = normalizedValue;
            }
        }

        Schema.Columns[columnIndex] = updatedSchema;
        RecalculateSchemaStats();
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

    public void RecalculateSchemaStats()
    {
        for (int i = 0; i < Schema.Columns.Count; i++)
        {
            var col = Schema.Columns[i];
            var values = new List<string?>();
            
            // Collect values, using display values for formula cells
            for (int rowIndex = 0; rowIndex < Table.Rows.Count; rowIndex++)
            {
                var row = Table.Rows[rowIndex];
                if (row.Cells.Count > i)
                {
                    var cellValue = row.Cells[i];
                    // If this is a formula, get the computed display value instead
                    if (_formulaAgent.HasFormula(rowIndex, i))
                    {
                        var displayValue = GetCellDisplayValue(rowIndex, i);
                        values.Add(displayValue);
                    }
                    else
                    {
                        values.Add(cellValue);
                    }
                }
            }

            var nonEmpty = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            col.DistinctCount = nonEmpty.Distinct().Count();
            col.SampleValues.Clear();
            col.SampleValues.AddRange(nonEmpty.Take(5));

            if (col.Type == DataType.Int || col.Type == DataType.Float)
            {
                var nums = nonEmpty
                    .Select(v => TryParseDouble(v, out var d) ? d : (double?)null)
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .ToList();

                if (nums.Any())
                {
                    col.Min = nums.Min();
                    col.Max = nums.Max();
                }
                else
                {
                    col.Min = null;
                    col.Max = null;
                }
            }
        }
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
                // Skip validation for formula cells (they contain formula strings, not values)
                // Formula validation happens when calculating the result
                if (!string.IsNullOrWhiteSpace(value) && value.Trim().StartsWith('='))
                {
                    continue;
                }
                
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
            DataType.Int => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            DataType.Float => TryParseDouble(value, out _),
            DataType.Bool => bool.TryParse(value, out _),
            DataType.Date => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => true
        };
    }

    private static bool TryParseDouble(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Try strict invariant first (Dot)
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) return true;
        
        // Fallback: Replace comma with dot for European format "88,9" -> "88.9"
        if (value.Contains(','))
        {
             return double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        return false;
    }

    /// <summary>
    /// Recalculate all formulas in the table
    /// </summary>
    public void RecalculateAllFormulas()
    {
        var formulas = _formulaAgent.GetAllFormulas();
        foreach (var ((row, col), formula) in formulas)
        {
            var (result, error) = _formulaAgent.CalculateFormula(row, col, Table, Schema);
            if (error == null && result != null)
            {
                // Update cell with calculated result (but keep formula in FormulaAgent)
                var rowModel = Table.Rows[row];
                if (col < rowModel.Cells.Count)
                {
                    // Validate result before storing
                    var validation = Validator.ValidateCell(Schema, col, result);
                    if (validation.IsValid)
                    {
                        rowModel.Cells[col] = formula; // Store formula, FormulaAgent tracks computed value
                    }
                }
            }
        }
    }

    /// <summary>
    /// Get computed value for a cell (handles formulas)
    /// </summary>
    public string? GetCellDisplayValue(int rowIndex, int columnIndex)
    {
        if (_formulaAgent.HasFormula(rowIndex, columnIndex))
        {
            var (result, error) = _formulaAgent.CalculateFormula(rowIndex, columnIndex, Table, Schema);
            if (error != null)
            {
                return $"#ERROR: {error}";
            }
            return result;
        }

        // Regular cell value
        if (rowIndex >= 0 && rowIndex < Table.Rows.Count)
        {
            var row = Table.Rows[rowIndex];
            if (columnIndex >= 0 && columnIndex < row.Cells.Count)
            {
                return row.Cells[columnIndex];
            }
        }

        return null;
    }

    /// <summary>
    /// Event handler when formula is calculated
    /// </summary>
    private void OnFormulaCalculated((int row, int col) cell, string? result)
    {
        // Formula result is already computed, just notify UI
        TableChanged?.Invoke();
    }
}
