using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DesktopHover
{
    // Read-only display formatting; values and formulas are never modified/recalculated.
    internal static class SpreadsheetNumberFormat
    {
        internal static string Format(string value, int id, string custom, bool date1904)
        {
            double number;
            if (!Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                Double.IsNaN(number) || Double.IsInfinity(number)) return value;
            string code = custom ?? BuiltIn(id);
            if (code == null || code.Length > 512) return value;
            try
            {
                code = SelectSection(code, ref number);
                if (code.Length == 0) return "";
                code = Normalize(code);
                if (String.Equals(code, "General", StringComparison.OrdinalIgnoreCase) || code == "@") return value;
                string plain = Regex.Replace(code, "\"[^\"]*\"|\\\\.", "");
                if (Regex.IsMatch(plain, "[ymdhs]", RegexOptions.IgnoreCase))
                    return FormatDate(number, code, date1904);
                // Fractions need a rational-number renderer; retain their saved value.
                if (plain.IndexOf('/') >= 0) return value;
                return number.ToString(code, CultureInfo.InvariantCulture);
            }
            catch (FormatException) { return value; }
            catch (ArgumentException) { return value; }
            catch (OverflowException) { return value; }
        }

        private static string BuiltIn(int id)
        {
            switch (id)
            {
                case 1: return "0"; case 2: return "0.00";
                case 3: return "#,##0"; case 4: return "#,##0.00";
                case 5: case 6: return "\"$\"#,##0;(\"$\"#,##0)";
                case 7: case 8: return "\"$\"#,##0.00;(\"$\"#,##0.00)";
                case 9: return "0%"; case 10: return "0.00%"; case 11: return "0.00E+00";
                case 14: return "yyyy-mm-dd"; case 15: return "d-mmm-yy";
                case 16: return "d-mmm"; case 17: return "mmm-yy";
                case 18: return "h:mm AM/PM"; case 19: return "h:mm:ss AM/PM";
                case 20: return "h:mm"; case 21: return "h:mm:ss"; case 22: return "yyyy-mm-dd h:mm";
                case 37: case 38: return "#,##0;(#,##0)";
                case 39: case 40: return "#,##0.00;(#,##0.00)";
                case 45: return "mm:ss"; case 46: return "[h]:mm:ss"; case 47: return "mm:ss.0";
                case 48: return "##0.0E+0";
                default: return null;
            }
        }

        private static string SelectSection(string code, ref double number)
        {
            var sections = new List<string>(); int start = 0; bool quoted = false;
            for (int i = 0; i < code.Length; i++)
            {
                if (code[i] == '\\') { i++; continue; }
                if (code[i] == '"') quoted = !quoted;
                else if (code[i] == ';' && !quoted) { sections.Add(code.Substring(start, i - start)); start = i + 1; }
            }
            sections.Add(code.Substring(start));
            bool conditional = false;
            foreach (string section in sections)
                conditional |= Regex.IsMatch(section, @"\[(?:[<>]=?|=|<>)[^\]]+\]");
            if (conditional)
            {
                foreach (string section in sections)
                {
                    Match match = Regex.Match(section, @"\[(<=|>=|<>|=|<|>)([^\]]+)\]");
                    if (!match.Success) return section;
                    double threshold;
                    if (!Double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out threshold)) continue;
                    string op = match.Groups[1].Value;
                    bool yes = op == "<" ? number < threshold : op == ">" ? number > threshold :
                        op == "<=" ? number <= threshold : op == ">=" ? number >= threshold : op == "<>" ? number != threshold : number == threshold;
                    if (yes) return section;
                }
                throw new FormatException("No matching number format section.");
            }
            if (number < 0 && sections.Count >= 2) { number = Math.Abs(number); return sections[1]; }
            return sections[number == 0 && sections.Count >= 3 ? 2 : 0];
        }

        private static string Normalize(string code)
        {
            var result = new StringBuilder(); bool quoted = false;
            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                if (c == '\\' && i + 1 < code.Length) { result.Append(c).Append(code[++i]); continue; }
                if (c == '"') { quoted = !quoted; result.Append(c); continue; }
                if (quoted) { result.Append(c); continue; }
                if (c == '_' || c == '*') { i++; continue; } // alignment/fill has no meaning in this preview
                if (c == '[')
                {
                    int end = code.IndexOf(']', i + 1); if (end < 0) throw new FormatException();
                    string tag = code.Substring(i + 1, end - i - 1);
                    if (Regex.IsMatch(tag, "^(h+|m+|s+)$", RegexOptions.IgnoreCase)) result.Append('[').Append(tag.ToLowerInvariant()).Append(']');
                    else if (tag.StartsWith("$", StringComparison.Ordinal))
                    {
                        int locale = tag.LastIndexOf('-');
                        string symbol = tag.Substring(1, (locale < 0 ? tag.Length : locale) - 1);
                        if (symbol.Length > 0) Literal(result, symbol);
                    }
                    // Color/condition/locale markers do not change the underlying value.
                    i = end; continue;
                }
                result.Append(c == '?' ? '#' : c);
            }
            return result.ToString();
        }

        private static void Literal(StringBuilder result, string text)
        { foreach (char c in text) result.Append('\\').Append(c); }

        private static string FormatDate(double number, string code, bool date1904)
        {
            if (number < 0 || number >= 2950000) throw new FormatException();
            DateTime date = DateTime.FromOADate(number + (date1904 ? 1462 : number < 60 ? 1 : 0));
            bool leapDay = !date1904 && (int)number == 60;
            bool ampm = code.IndexOf("AM/PM", StringComparison.OrdinalIgnoreCase) >= 0 || code.IndexOf("A/P", StringComparison.OrdinalIgnoreCase) >= 0;
            var pattern = new StringBuilder(); char previous = '\0';
            for (int i = 0; i < code.Length;)
            {
                char c = code[i];
                if (c == '\\' && i + 1 < code.Length) { pattern.Append(c).Append(code[i + 1]); i += 2; continue; }
                if (c == '"')
                {
                    int end = code.IndexOf('"', i + 1); if (end < 0) throw new FormatException();
                    pattern.Append(code, i, end - i + 1); i = end + 1; continue;
                }
                if (String.Compare(code, i, "AM/PM", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
                { pattern.Append("tt"); i += 5; continue; }
                if (String.Compare(code, i, "A/P", 0, 3, StringComparison.OrdinalIgnoreCase) == 0)
                { Literal(pattern, date.Hour < 12 ? "A" : "P"); i += 3; continue; }
                if (c == '[')
                {
                    int end = code.IndexOf(']', i + 1); if (end < 0) throw new FormatException();
                    string unit = code.Substring(i + 1, end - i - 1); previous = unit[0];
                    double total = number * (previous == 'h' ? 24 : previous == 'm' ? 1440 : 86400);
                    Literal(pattern, Math.Floor(total + .0000001).ToString(new string('0', Math.Min(8, unit.Length)), CultureInfo.InvariantCulture));
                    i = end + 1; continue;
                }
                char token = Char.ToLowerInvariant(c); int count = 1;
                while (i + count < code.Length && Char.ToLowerInvariant(code[i + count]) == token) count++;
                if (token == 'y') pattern.Append(count <= 2 ? "yy" : "yyyy");
                else if (token == 'd')
                {
                    if (leapDay && count <= 2) Literal(pattern, "29");
                    else pattern.Append(count == 1 ? "%d" : new string('d', Math.Min(4, count)));
                }
                else if (token == 'm')
                {
                    int next = i + count;
                    while (next < code.Length && !Char.IsLetter(code[next])) next++;
                    bool minute = count <= 2 && (previous == 'h' || (next < code.Length && Char.ToLowerInvariant(code[next]) == 's'));
                    if (count >= 5) Literal(pattern, date.ToString("MMMM", CultureInfo.InvariantCulture).Substring(0, 1));
                    else pattern.Append(count == 1 ? (minute ? "%m" : "%M") : new string(minute ? 'm' : 'M', count));
                }
                else if (token == 'h') pattern.Append(count == 1 ? (ampm ? "%h" : "%H") : (ampm ? "hh" : "HH"));
                else if (token == 's') pattern.Append(count == 1 ? "%s" : "ss");
                else if (token == '0' && previous == 's') pattern.Append(new string('f', Math.Min(7, count)));
                else { Literal(pattern, code.Substring(i, count)); i += count; continue; }
                previous = token; i += count;
            }
            return date.ToString(pattern.ToString(), CultureInfo.InvariantCulture);
        }
    }
}
