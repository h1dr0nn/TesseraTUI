using System;
using System.Collections.Generic;

namespace Tessera.Core.Models;

public enum JsonKeyDifferenceType
{
    Missing,
    Unknown
}

public record JsonKeyDifference(int? RowIndex, string Key, JsonKeyDifferenceType Type);

public record JsonDiffResult(
    IReadOnlyList<int> AddedRows,
    IReadOnlyList<int> RemovedRows,
    IReadOnlyList<int> ModifiedRows,
    IReadOnlyList<JsonKeyDifference> KeyMismatches)
{
    public static JsonDiffResult Empty { get; } = new(
        Array.Empty<int>(),
        Array.Empty<int>(),
        Array.Empty<int>(),
        Array.Empty<JsonKeyDifference>());

    public bool HasChanges => AddedRows.Count > 0 || RemovedRows.Count > 0 || ModifiedRows.Count > 0 || KeyMismatches.Count > 0;
}
