using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Tessera.Core.Models;
using Tessera.Core.Native;

namespace Tessera.Core.Agents;

/// <summary>
/// Handles formula parsing, calculation, and dependency tracking
/// Integrates with Rust native module for performance-critical calculations
/// </summary>
public class FormulaAgent
{
    // Track which cells contain formulas: (rowIndex, columnIndex) -> formula string
    private readonly Dictionary<(int row, int col), string> _formulas = new();

    // Dependency graph: cell A depends on cells B, C, D...
    private readonly Dictionary<(int row, int col), HashSet<(int row, int col)>> _dependencies = new();

    public event Action<(int row, int col), string?>? FormulaCalculated;

    /// <summary>
    /// Set formula for a cell
    /// </summary>
    public void SetFormula(int rowIndex, int columnIndex, string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            _formulas.Remove((rowIndex, columnIndex));
            _dependencies.Remove((rowIndex, columnIndex));
            return;
        }

        formula = formula.Trim();
        if (!formula.StartsWith('='))
        {
            throw new ArgumentException("Formula must start with '='", nameof(formula));
        }

        _formulas[(rowIndex, columnIndex)] = formula;
        UpdateDependencies(rowIndex, columnIndex, formula);
    }

    /// <summary>
    /// Clear formula for a cell
    /// </summary>
    public void ClearFormula(int rowIndex, int columnIndex)
    {
        _formulas.Remove((rowIndex, columnIndex));
        _dependencies.Remove((rowIndex, columnIndex));
    }

    /// <summary>
    /// Get formula for a cell (if exists)
    /// </summary>
    public string? GetFormula(int rowIndex, int columnIndex)
    {
        return _formulas.TryGetValue((rowIndex, columnIndex), out var formula) ? formula : null;
    }

    /// <summary>
    /// Check if a cell has a formula
    /// </summary>
    public bool HasFormula(int rowIndex, int columnIndex)
    {
        return _formulas.ContainsKey((rowIndex, columnIndex));
    }

    /// <summary>
    /// Calculate formula result for a cell
    /// </summary>
    public (string? Result, string? Error) CalculateFormula(
        int rowIndex,
        int columnIndex,
        TableModel table,
        SchemaModel schema)
    {
        if (!_formulas.TryGetValue((rowIndex, columnIndex), out var formula))
        {
            return (null, "No formula found for cell");
        }

        // Check for circular dependencies
        var visited = new HashSet<(int row, int col)>();
        if (HasCircularDependency(rowIndex, columnIndex, visited))
        {
            return (null, "Circular dependency detected");
        }

        try
        {
            // Parse formula using native parser (with fallback)
            var (parsed, parseError) = FormulaNative.ParseFormula(formula);
            if (parseError != null)
            {
                return (null, parseError);
            }

            if (string.IsNullOrWhiteSpace(parsed))
            {
                return (null, "Failed to parse formula");
            }

            // Parse result format: "FUNCTION:ColumnName"
            var parts = parsed.Split(':', 2);
            if (parts.Length != 2)
            {
                return (null, "Invalid formula format");
            }

            var functionName = parts[0].Trim().ToUpperInvariant();
            var columnName = parts[1].Trim();

            // Find column index
            var colIndex = table.Columns.FindIndex(c => 
                c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
            
            if (colIndex < 0)
            {
                return (null, $"Column '{columnName}' not found");
            }

            // Execute function
            return functionName switch
            {
                "SUM" => CalculateSum(colIndex, table),
                "AVG" => CalculateAvg(colIndex, table),
                "MIN" => CalculateMin(colIndex, table),
                "MAX" => CalculateMax(colIndex, table),
                "COUNT" => CalculateCount(colIndex, table),
                _ => (null, $"Unknown function: {functionName}")
            };
        }
        catch (Exception ex)
        {
            return (null, $"Error calculating formula: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculate SUM for a column
    /// </summary>
    private (string? Result, string? Error) CalculateSum(int columnIndex, TableModel table)
    {
        var values = new List<string?>();
        foreach (var row in table.Rows)
        {
            if (columnIndex < row.Cells.Count)
            {
                values.Add(row.Cells[columnIndex]);
            }
        }

        var (sum, error) = FormulaNative.Sum(table.Columns[columnIndex].Name, values);
        if (error != null)
        {
            return (null, error);
        }

        if (!sum.HasValue)
        {
            return (null, "SUM calculation returned no value");
        }

        // Format result based on column type (could be Int or Float)
        var result = sum.Value;
        
        // Return as string (consistent with table cell format)
        // Check if it's a whole number
        if (Math.Abs(result % 1.0) < double.Epsilon)
        {
            return (((long)result).ToString(CultureInfo.InvariantCulture), null);
        }

        return (result.ToString(CultureInfo.InvariantCulture), null);
    }

    /// <summary>
    /// Calculate AVG for a column
    /// </summary>
    private (string? Result, string? Error) CalculateAvg(int columnIndex, TableModel table)
    {
        var values = new List<string?>();
        foreach (var row in table.Rows)
        {
            if (columnIndex < row.Cells.Count)
            {
                values.Add(row.Cells[columnIndex]);
            }
        }

        var (avg, error) = FormulaNative.Avg(table.Columns[columnIndex].Name, values);
        if (error != null)
        {
            return (null, error);
        }

        if (!avg.HasValue)
        {
            return (null, "AVG calculation returned no value");
        }

        var result = avg.Value;
        if (Math.Abs(result % 1.0) < double.Epsilon)
        {
            return (((long)result).ToString(CultureInfo.InvariantCulture), null);
        }

        return (result.ToString(CultureInfo.InvariantCulture), null);
    }

    /// <summary>
    /// Calculate MIN for a column
    /// </summary>
    private (string? Result, string? Error) CalculateMin(int columnIndex, TableModel table)
    {
        var values = new List<string?>();
        foreach (var row in table.Rows)
        {
            if (columnIndex < row.Cells.Count)
            {
                values.Add(row.Cells[columnIndex]);
            }
        }

        var (min, error) = FormulaNative.Min(table.Columns[columnIndex].Name, values);
        if (error != null)
        {
            return (null, error);
        }

        if (!min.HasValue)
        {
            return (null, "MIN calculation returned no value");
        }

        var result = min.Value;
        if (Math.Abs(result % 1.0) < double.Epsilon)
        {
            return (((long)result).ToString(CultureInfo.InvariantCulture), null);
        }

        return (result.ToString(CultureInfo.InvariantCulture), null);
    }

    /// <summary>
    /// Calculate MAX for a column
    /// </summary>
    private (string? Result, string? Error) CalculateMax(int columnIndex, TableModel table)
    {
        var values = new List<string?>();
        foreach (var row in table.Rows)
        {
            if (columnIndex < row.Cells.Count)
            {
                values.Add(row.Cells[columnIndex]);
            }
        }

        var (max, error) = FormulaNative.Max(table.Columns[columnIndex].Name, values);
        if (error != null)
        {
            return (null, error);
        }

        if (!max.HasValue)
        {
            return (null, "MAX calculation returned no value");
        }

        var result = max.Value;
        if (Math.Abs(result % 1.0) < double.Epsilon)
        {
            return (((long)result).ToString(CultureInfo.InvariantCulture), null);
        }

        return (result.ToString(CultureInfo.InvariantCulture), null);
    }

    /// <summary>
    /// Calculate COUNT for a column
    /// </summary>
    private (string? Result, string? Error) CalculateCount(int columnIndex, TableModel table)
    {
        var values = new List<string?>();
        foreach (var row in table.Rows)
        {
            if (columnIndex < row.Cells.Count)
            {
                values.Add(row.Cells[columnIndex]);
            }
        }

        var (count, error) = FormulaNative.Count(table.Columns[columnIndex].Name, values);
        if (error != null)
        {
            return (null, error);
        }

        if (!count.HasValue)
        {
            return (null, "COUNT calculation returned no value");
        }

        // COUNT always returns an integer
        return (((long)count.Value).ToString(CultureInfo.InvariantCulture), null);
    }

    /// <summary>
    /// Recalculate all formulas that depend on a changed cell
    /// </summary>
    public void RecalculateDependentCells(
        int changedRow,
        int changedCol,
        TableModel table,
        SchemaModel schema)
    {
        // Find all cells that depend on the changed cell
        var cellsToRecalc = new HashSet<(int row, int col)>();
        
        foreach (var (cell, deps) in _dependencies)
        {
            if (deps.Contains((changedRow, changedCol)))
            {
                cellsToRecalc.Add(cell);
            }
        }

        // Recalculate each dependent cell
        foreach (var (row, col) in cellsToRecalc)
        {
            var (result, error) = CalculateFormula(row, col, table, schema);
            FormulaCalculated?.Invoke((row, col), error ?? result);
        }
    }

    /// <summary>
    /// Check if setting a formula would create a circular dependency
    /// </summary>
    public bool HasCircularDependency(int rowIndex, int columnIndex, HashSet<(int row, int col)>? visited = null)
    {
        visited ??= new HashSet<(int row, int col)>();
        
        if (!_dependencies.TryGetValue((rowIndex, columnIndex), out var deps))
        {
            return false; // No dependencies, no cycle
        }

        if (visited.Contains((rowIndex, columnIndex)))
        {
            return true; // Cycle detected
        }

        visited.Add((rowIndex, columnIndex));

        foreach (var dep in deps)
        {
            if (HasCircularDependency(dep.row, dep.col, visited))
            {
                return true;
            }
        }

        visited.Remove((rowIndex, columnIndex));
        return false;
    }

    /// <summary>
    /// Update dependency graph for a formula
    /// </summary>
    private void UpdateDependencies(int rowIndex, int columnIndex, string formula)
    {
        // For now, SUM(ColumnName) doesn't create cell-to-cell dependencies
        // It depends on all cells in the referenced column
        // In future, cell references like ColumnA[0] will create specific dependencies
        
        _dependencies[(rowIndex, columnIndex)] = new HashSet<(int row, int col)>();
        
        // TODO: When we add cell references, parse formula to extract specific cell dependencies
        // For now, column-based formulas don't need cell-level dependency tracking
    }

    /// <summary>
    /// Clear all formulas (e.g., when loading new table)
    /// </summary>
    public void ClearAll()
    {
        _formulas.Clear();
        _dependencies.Clear();
    }

    /// <summary>
    /// Get all cells that have formulas
    /// </summary>
    public IReadOnlyDictionary<(int row, int col), string> GetAllFormulas()
    {
        return _formulas;
    }
}

