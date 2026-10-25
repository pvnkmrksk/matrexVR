using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>Culture-independent CSV fields shared by acquisition and replay regression tests.</summary>
public static class RecordedCsv
{
    public static void Append(StringBuilder output, object value)
    {
        if (value == null) return;
        string text = value is float single ? single.ToString("R", CultureInfo.InvariantCulture) :
            value is double number ? number.ToString("R", CultureInfo.InvariantCulture) :
            value is IFormattable formatted ? formatted.ToString(null, CultureInfo.InvariantCulture) : value.ToString();
        if (text.IndexOf(',') < 0 && text.IndexOf('"') < 0 && text.IndexOf('\r') < 0 && text.IndexOf('\n') < 0) { output.Append(text); return; }
        output.Append('"').Append(text.Replace("\"", "\"\"")).Append('"');
    }

    public static string Join(IEnumerable<string> values)
    {
        var output = new StringBuilder();
        bool first = true;
        foreach (string value in values)
        {
            if (!first) output.Append(',');
            Append(output, value); first = false;
        }
        return output.ToString();
    }
}
