using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DesktopHover
{
    internal sealed partial class TextPreviewContent
    {
        private const int MaximumTableColumns = 128;

        private void ReadTable(string text, bool tsv, CancellationToken cancellation)
        {
            int start = 0;
            char separator = tsv ? '\t' : ',';
            if (!tsv)
            {
                if (text.Length >= 6 && text.StartsWith("sep=", StringComparison.OrdinalIgnoreCase) &&
                    (text[5] == '\r' || text[5] == '\n'))
                {
                    separator = text[4];
                    start = text[5] == '\r' && text.Length > 6 && text[6] == '\n' ? 7 : 6;
                }
                else separator = DetectCsvSeparator(text);
            }
            var rows = new List<string[]>();
            var cells = new List<string>();
            var field = new StringBuilder();
            bool quoted = false, atStart = true;
            for (int i = start; i <= text.Length; i++)
            {
                if ((i & 1023) == 0) cancellation.ThrowIfCancellationRequested();
                bool end = i == text.Length;
                if (end && atStart && cells.Count == 0) break;
                char c = end ? '\0' : text[i];
                if (!end && c == '"')
                {
                    if (quoted && i + 1 < text.Length && text[i + 1] == '"') i++;
                    else if (quoted || atStart) { quoted = !quoted; atStart = false; continue; }
                }
                if (end || (!quoted && (c == separator || c == '\r' || c == '\n')))
                {
                    if (cells.Count < MaximumTableColumns)
                    {
                        string value = NormalizeLine(field.ToString(), 0, field.Length);
                        if (value.Length > MaximumColumns) { value = value.Substring(0, MaximumColumns); Truncated = true; }
                        cells.Add(value);
                    }
                    else Truncated = true;
                    field.Length = 0; atStart = true;
                    if (!end && c == separator) continue;
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    TableColumns = Math.Max(TableColumns, cells.Count);
                    rows.Add(cells.ToArray()); cells.Clear();
                    if (rows.Count == MaximumLines) { if (i + 1 < text.Length) Truncated = true; break; }
                }
                else
                {
                    if (quoted && c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    if (field.Length < MaximumColumns) field.Append(c);
                    else Truncated = true;
                    atStart = false;
                }
            }
            TableRows = rows.ToArray();
        }

        private static char DetectCsvSeparator(string text)
        {
            // Count separators outside quotes in the first nonempty records. The
            // header breaks ties for semicolon exports containing decimal commas.
            int commaRows = 0, semiRows = 0, commas = 0, semis = 0, records = 0;
            char first = ','; bool quoted = false;
            for (int i = 0; i <= text.Length && records < 12; i++)
            {
                char c = i == text.Length ? '\n' : text[i];
                if (c == '"') { quoted = !quoted; continue; }
                if (quoted) continue;
                if (c == ',') commas++;
                else if (c == ';') semis++;
                else if ((c == '\r' || c == '\n') && (commas > 0 || semis > 0))
                {
                    if (records++ == 0) first = semis > commas ? ';' : ',';
                    if (commas > 0) commaRows++;
                    if (semis > 0) semiRows++;
                    commas = semis = 0;
                }
            }
            return commaRows == semiRows ? first : semiRows > commaRows ? ';' : ',';
        }
    }
}
