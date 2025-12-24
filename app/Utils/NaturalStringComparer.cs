using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Tessera.Utils;

/// <summary>
/// Compares strings in a natural way (e.g., "1" < "2" < "10"), handling numeric segments intelligently.
/// </summary>
public class NaturalStringComparer : IComparer<string>
{
    private static readonly Regex ChunkRegex = new Regex(@"(\d+)|(\D+)", RegexOptions.Compiled);

    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        var xChunks = GetChunks(x);
        var yChunks = GetChunks(y);

        var count = Math.Min(xChunks.Count, yChunks.Count);

        for (int i = 0; i < count; i++)
        {
            var xChunk = xChunks[i];
            var yChunk = yChunks[i];

            int result;
            if (long.TryParse(xChunk, out var xNum) && long.TryParse(yChunk, out var yNum))
            {
                result = xNum.CompareTo(yNum);
            }
            else
            {
                result = string.Compare(xChunk, yChunk, StringComparison.OrdinalIgnoreCase);
            }

            if (result != 0) return result;
        }

        return xChunks.Count.CompareTo(yChunks.Count);
    }

    private List<string> GetChunks(string s)
    {
        var chunks = new List<string>();
        foreach (Match match in ChunkRegex.Matches(s))
        {
            chunks.Add(match.Value);
        }
        return chunks;
    }
}
