using System.Globalization;
using Tessera.Core.Models;

namespace Tessera.Core.Agents;

public class ValidationAgent
{
    public ValidationResult ValidateCell(SchemaModel schema, int columnIndex, string? value)
    {
        if (columnIndex < 0 || columnIndex >= schema.Columns.Count)
        {
            return ValidationResult.Error("Column index is out of range.");
        }

        return ValidateCell(schema.Columns[columnIndex], value);
    }

    public ValidationResult ValidateCell(ColumnSchema column, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (!column.IsNullable)
            {
                return ValidationResult.Error($"{column.Name} cannot be empty.");
            }

            return ValidationResult.Success(null);
        }

        switch (column.Type)
        {
            case DataType.Int:
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                {
                    if (!IsWithinRange(longValue, column))
                    {
                        return ValidationResult.Error(BuildRangeMessage(column));
                    }

                    return ValidationResult.Success(longValue.ToString(CultureInfo.InvariantCulture));
                }

                return ValidationResult.Error($"{column.Name} expects an integer.");
            case DataType.Float:
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                {
                    if (!IsWithinRange(floatValue, column))
                    {
                        return ValidationResult.Error(BuildRangeMessage(column));
                    }

                    return ValidationResult.Success(floatValue.ToString(CultureInfo.InvariantCulture));
                }

                return ValidationResult.Error($"{column.Name} expects a floating point number.");
            case DataType.Bool:
                if (bool.TryParse(value, out var boolValue))
                {
                    return ValidationResult.Success(boolValue.ToString());
                }

                return ValidationResult.Error($"{column.Name} expects a boolean value.");
            case DataType.Date:
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dateValue))
                {
                    return ValidationResult.Success(dateValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }

                return ValidationResult.Error($"{column.Name} expects a date value.");
            default:
                return ValidationResult.Success(value);
        }
    }

    public ValidationReport ValidateColumn(TableModel table, int columnIndex, ColumnSchema schema)
    {
        var errors = new List<ValidationError>();
        var normalized = new List<string?>();

        for (var i = 0; i < table.Rows.Count; i++)
        {
            var cellValue = table.Rows[i].Cells.ElementAtOrDefault(columnIndex);
            var result = ValidateCell(schema, cellValue);
            if (!result.IsValid)
            {
                errors.Add(new ValidationError(i, result.Message ?? "Invalid value"));
                normalized.Add(cellValue);
                continue;
            }

            normalized.Add(result.NormalizedValue);
        }

        return new ValidationReport(errors.Count == 0, errors, normalized);
    }

    private static bool IsWithinRange(double value, ColumnSchema schema)
    {
        if (schema.Min.HasValue && value < schema.Min.Value)
        {
            return false;
        }

        if (schema.Max.HasValue && value > schema.Max.Value)
        {
            return false;
        }

        return true;
    }

    private static string BuildRangeMessage(ColumnSchema schema)
    {
        if (schema.Min.HasValue && schema.Max.HasValue)
        {
            return $"Value must be between {schema.Min} and {schema.Max}.";
        }

        if (schema.Min.HasValue)
        {
            return $"Value must be greater than or equal to {schema.Min}.";
        }

        if (schema.Max.HasValue)
        {
            return $"Value must be less than or equal to {schema.Max}.";
        }

        return "Value is outside the allowed range.";
    }
}

public record ValidationResult(bool IsValid, string? Message, string? NormalizedValue)
{
    public static ValidationResult Success(string? normalized) => new(true, null, normalized);

    public static ValidationResult Error(string message) => new(false, message, null);
}

public record ValidationError(int RowIndex, string Message);

public record ValidationReport(bool IsValid, List<ValidationError> Errors, List<string?> NormalizedValues);
