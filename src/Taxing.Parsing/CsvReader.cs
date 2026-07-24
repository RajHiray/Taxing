using System.Text;

namespace Taxing.Parsing;

/// <summary>
/// Minimal RFC-4180-style CSV reader supporting quoted fields, escaped quotes
/// ("") and embedded newlines. Returns rows as string arrays.
/// The field delimiter is auto-detected (comma, semicolon or tab) so exports
/// re-saved by spreadsheet apps in non-US locales are still read correctly.
/// </summary>
public static class CsvReader
{
    public static IReadOnlyList<string[]> Parse(string content)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(content)) return rows;

        var delimiter = DetectDelimiter(content);

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

            if (c == delimiter)
            {
                record.Add(field.ToString());
                field.Clear();
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
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

    /// <summary>
    /// Detects the field delimiter from the first non-empty line, counting delimiter
    /// characters outside of quoted fields. Commas are preferred; a semicolon or tab is only
    /// chosen when it is strictly more frequent, so ordinary comma CSVs are never misread.
    /// </summary>
    private static char DetectDelimiter(string content)
    {
        int comma = 0, semicolon = 0, tab = 0;
        bool inQuotes = false;
        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (inQuotes) continue;
            if (c == '\n') { if (comma + semicolon + tab > 0) break; else continue; }
            if (c == '\r') continue;
            switch (c)
            {
                case ',': comma++; break;
                case ';': semicolon++; break;
                case '\t': tab++; break;
            }
        }

        if (semicolon > comma && semicolon >= tab) return ';';
        if (tab > comma && tab > semicolon) return '\t';
        return ',';
    }
}
