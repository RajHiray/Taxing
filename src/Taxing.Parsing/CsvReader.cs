using System.Text;

namespace Taxing.Parsing;

/// <summary>
/// Minimal RFC-4180-style CSV reader supporting quoted fields, escaped quotes
/// ("") and embedded newlines. Returns rows as string arrays.
/// </summary>
public static class CsvReader
{
    public static IReadOnlyList<string[]> Parse(string content)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(content)) return rows;

        var field = new StringBuilder();
        var record = new List<string>();
        bool inQuotes = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    record.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    // handled together with \n
                    break;
                case '\n':
                    record.Add(field.ToString());
                    field.Clear();
                    rows.Add(record.ToArray());
                    record = new List<string>();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        // Flush trailing field/record if present.
        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            rows.Add(record.ToArray());
        }

        return rows;
    }
}
