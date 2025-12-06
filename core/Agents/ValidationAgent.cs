using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tessera.Core.Models;

namespace Tessera.Core.Agents;

public class ValidationAgent
{
    private readonly JsonAgent _jsonAgent;

    public ValidationAgent(JsonAgent? jsonAgent = null)
    {
        _jsonAgent = jsonAgent ?? new JsonAgent();
    }

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

    public JsonValidationResult ValidateJsonText(string jsonText, SchemaModel schema)
    {
        var parseResult = _jsonAgent.Parse(jsonText);
        if (!parseResult.IsValid || parseResult.Model is null)
        {
            return JsonValidationResult.Failure(parseResult.ErrorMessage ?? "Invalid JSON", JsonValidationErrorType.Syntax, parseResult.LineNumber);
        }

        return ValidateJsonModel(parseResult.Model, schema);
    }

    public JsonValidationResult ValidateJsonModel(JsonModel json, SchemaModel schema)
    {
        var errors = new List<JsonValidationError>();

        for (var rowIndex = 0; rowIndex < json.Records.Count; rowIndex++)
        {
            var record = json.Records[rowIndex];
            foreach (var column in schema.Columns)
            {
                if (!record.TryGetValue(column.Name, out var value))
                {
                    errors.Add(new JsonValidationError(rowIndex, $"Missing required key '{column.Name}'.", column.Name, JsonValidationErrorType.MissingKey));
                    continue;
                }

                if (value is null)
                {
                    if (!column.IsNullable)
                    {
                        errors.Add(new JsonValidationError(rowIndex, $"{column.Name} cannot be null.", column.Name, JsonValidationErrorType.NullNotAllowed));
                    }

                    continue;
                }

                var compatibility = IsValueCompatibleWithSchema(value, column);
                if (compatibility is not null)
                {
                    errors.Add(new JsonValidationError(rowIndex, compatibility, column.Name, JsonValidationErrorType.TypeMismatch));
                }
            }

            foreach (var key in record.Keys)
            {
                if (schema.Columns.All(c => c.Name != key))
                {
                    errors.Add(new JsonValidationError(rowIndex, $"Unknown key '{key}'.", key, JsonValidationErrorType.UnknownKey));
                }
            }
        }

        return new JsonValidationResult(errors.Count == 0, errors, errors.Count == 0 ? json : null);
    }

    private static string? IsValueCompatibleWithSchema(object value, ColumnSchema column)
    {
        switch (column.Type)
        {
            case DataType.String when value is string:
                return null;
            case DataType.Bool when value is bool:
                return null;
            case DataType.Int:
                if (value is long)
                {
                    return null;
                }

                if (value is double number)
                {
                    if (Math.Abs(number % 1) < double.Epsilon)
                    {
                        return null;
                    }

                    return $"{column.Name} expects an integer.";
                }

                break;
            case DataType.Float:
                if (value is double or long)
                {
                    return null;
                }

                break;
            case DataType.Date:
                if (value is string s && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    return null;
                }

                break;
            default:
                return null;
        }

        return $"{column.Name} is incompatible with schema type {column.Type}.";
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
