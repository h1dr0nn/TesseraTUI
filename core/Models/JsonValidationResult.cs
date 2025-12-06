using System.Collections.Generic;

namespace Tessera.Core.Models;

public enum JsonValidationErrorType
{
    Syntax,
    Structure,
    MissingKey,
    UnknownKey,
    TypeMismatch,
    NullNotAllowed
}

public record JsonValidationError(int? RowIndex, string Message, string? Key, JsonValidationErrorType Type, int? LineNumber = null);

public record JsonValidationResult(bool IsValid, List<JsonValidationError> Errors, JsonModel? Model)
{
    public static JsonValidationResult Failure(string message, JsonValidationErrorType type = JsonValidationErrorType.Syntax, int? lineNumber = null) =>
        new(false, new List<JsonValidationError> { new(null, message, null, type, lineNumber) }, null);
}
