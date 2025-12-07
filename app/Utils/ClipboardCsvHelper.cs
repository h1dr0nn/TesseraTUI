using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tessera.Utils;

public static class ClipboardCsvHelper
{
    public static string Serialize(IEnumerable<IEnumerable<string?>> rows, char delimiter = ',')
    {
        var builder = new StringBuilder();
        var firstRow = true;

        foreach (var row in rows)
        {
            if (!firstRow)
            {
                builder.AppendLine();
            }

            firstRow = false;
            builder.Append(string.Join(delimiter, row.Select(v => Escape(v, delimiter)))); 
        }

        return builder.ToString();
    }

    public static IList<IList<string?>> Parse(string text, char delimiter = ',')
    {
        var rows = new List<IList<string?>>();
        using var reader = new StringReader(text);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            rows.Add(ParseLine(line, delimiter));
        }

        if (text.EndsWith('\n'))
        {
            rows.Add(new List<string?> { string.Empty });
        }

        return rows;
    }

    private static string Escape(string? value, char delimiter)
    {
        var safe = value ?? string.Empty;
        var requiresQuotes = safe.IndexOfAny(new[] { delimiter, '\n', '\r', '"' }) >= 0;
        if (safe.Contains('"'))
        {
            safe = safe.Replace("\"", "\"\"");
        }

        return requiresQuotes ? $"\"{safe}\"" : safe;
    }

    private static IList<string?> ParseLine(string line, char delimiter)
    {
        var values = new List<string?>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == delimiter)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        values.Add(current.ToString());
        return values;
    }
}
