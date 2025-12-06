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

        var column = schema.Columns[columnIndex];

        if (string.IsNullOrWhiteSpace(value))
        {
            if (!column.IsNullable)
            {
                return ValidationResult.Error($"{column.Name} cannot be empty.");
            }

            return ValidationResult.Success(null);
        }

        switch (column.InferredType)
        {
            case DataType.Int:
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                {
                    return ValidationResult.Success(longValue.ToString(CultureInfo.InvariantCulture));
                }

                return ValidationResult.Error($"{column.Name} expects an integer.");
            case DataType.Float:
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                {
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
}

public record ValidationResult(bool IsValid, string? Message, string? NormalizedValue)
{
    public static ValidationResult Success(string? normalized) => new(true, null, normalized);

    public static ValidationResult Error(string message) => new(false, message, null);
}
