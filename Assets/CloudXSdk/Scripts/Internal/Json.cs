#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CloudX
{
    /*
     * Minimal JSON reader and writer used by the iOS event bridge and the arbiter
     * bid serializer. No external dependency — avoids requiring Newtonsoft.Json as
     * a .unitypackage consumer dependency.
     *
     * Parse returns: Dictionary<string,object> for objects, List<object> for arrays,
     * string, double, long, bool, or null. The writer accepts the same types plus
     * IDictionary and IEnumerable as catch-alls for typed collections.
     */
    internal static class Json
    {
        public static object? Parse(string? json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int i = 0;
            SkipWhitespace(json, ref i);
            return ParseValue(json, ref i);
        }

        public static string Write(object? value)
        {
            var sb = new StringBuilder();
            WriteValue(value, sb);
            return sb.ToString();
        }

        #region Reader

        private static object? ParseValue(string s, ref int i)
        {
            if (i >= s.Length) return null;
            switch (s[i])
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                default:
                    if (s[i] == 't' && i + 4 <= s.Length && s.Substring(i, 4) == "true") { i += 4; return true; }
                    if (s[i] == 'f' && i + 5 <= s.Length && s.Substring(i, 5) == "false") { i += 5; return false; }
                    if (s[i] == 'n' && i + 4 <= s.Length && s.Substring(i, 4) == "null") { i += 4; return null; }
                    if (s[i] == '-' || (s[i] >= '0' && s[i] <= '9')) return ParseNumber(s, ref i);
                    return null;
            }
        }

        internal static Dictionary<string, object>? ParseObject(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '{') return null;
            i++;
            var result = new Dictionary<string, object>();
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }
            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                var key = ParseString(s, ref i);
                if (key == null) return null;
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') return null;
                i++;
                SkipWhitespace(s, ref i);
                var value = ParseValue(s, ref i);
                /*
                 * Drop JSON nulls. The iOS bridge omits absent keys rather than emitting null,
                 * so a null here means "explicitly null" — which the C# consumers treat as
                 * "missing" anyway. Dropping keeps GetString/GetDouble lookups uniform.
                 */
                if (value != null) result[key] = value;
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return result; }
                return null;
            }
            return null;
        }

        private static List<object>? ParseArray(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '[') return null;
            i++;
            var result = new List<object>();
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }
            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                var value = ParseValue(s, ref i);
                /*
                 * Drop JSON nulls. The iOS bridge omits absent keys rather than emitting null,
                 * so a null here means "explicitly null" — which the C# consumers treat as
                 * "missing" anyway. Dropping keeps GetString/GetDouble lookups uniform.
                 */
                if (value != null) result.Add(value);
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return result; }
                return null;
            }
            return null;
        }

        private static string? ParseString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c == '\\' && i < s.Length)
                {
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case '/':  sb.Append('/');  break;
                        case 'b':  sb.Append('\b'); break;
                        case 'f':  sb.Append('\f'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) return null;
                            sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                            i += 4;
                            break;
                        default: return null;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return null;
        }

        private static object? ParseNumber(string s, ref int i)
        {
            int start = i;
            if (s[i] == '-') i++;
            bool isFloat = false;
            while (i < s.Length)
            {
                char c = s[i];
                if (c >= '0' && c <= '9') { i++; continue; }
                if (c == '.' || c == 'e' || c == 'E') { isFloat = true; i++; continue; }
                if (c == '+' || c == '-') { i++; continue; }
                break;
            }
            var numStr = s.Substring(start, i - start);
            if (isFloat) return double.Parse(numStr, CultureInfo.InvariantCulture);
            if (long.TryParse(numStr, out var l)) return l;
            return double.Parse(numStr, CultureInfo.InvariantCulture);
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        #endregion

        #region Writer

        private static void WriteValue(object? value, StringBuilder sb)
        {
            if (value == null) { sb.Append("null"); return; }
            if (value is string str) { WriteString(str, sb); return; }
            if (value is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (value is int iv) { sb.Append(iv); return; }
            if (value is long lv) { sb.Append(lv); return; }
            if (value is float fv) { sb.Append(fv.ToString("G", CultureInfo.InvariantCulture)); return; }
            if (value is double dv) { sb.Append(dv.ToString("G", CultureInfo.InvariantCulture)); return; }
            if (value is decimal dec) { sb.Append(dec.ToString(CultureInfo.InvariantCulture)); return; }
            if (value is IDictionary dict) { WriteDict(dict, sb); return; }
            if (value is IEnumerable list) { WriteList(list, sb); return; }
            WriteString(value.ToString() ?? "", sb);
        }

        private static void WriteString(string s, StringBuilder sb)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        private static void WriteDict(IDictionary dict, StringBuilder sb)
        {
            sb.Append('{');
            var first = true;
            foreach (DictionaryEntry entry in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(entry.Key.ToString()!, sb);
                sb.Append(':');
                WriteValue(entry.Value, sb);
            }
            sb.Append('}');
        }

        private static void WriteList(IEnumerable list, StringBuilder sb)
        {
            sb.Append('[');
            var first = true;
            foreach (var item in list)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteValue(item, sb);
            }
            sb.Append(']');
        }

        #endregion
    }
}
