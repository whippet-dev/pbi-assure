using System.Text;

namespace PbiAssure.Reporting;

/// <summary>Shared RFC-style CSV writing with the spreadsheet-safety policy used by PBI Assure exports.</summary>
internal static class CsvWriter
{
    public static void AppendRow(StringBuilder csv, IEnumerable<string?> values)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                csv.Append(',');
            }

            var text = NeutralizeSpreadsheetFormula(value ?? string.Empty);
            if (text.IndexOfAny([',', '"', '\r', '\n']) >= 0)
            {
                csv.Append('"').Append(text.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
            }
            else
            {
                csv.Append(text);
            }

            first = false;
        }

        csv.Append("\r\n");
    }

    private static string NeutralizeSpreadsheetFormula(string value) => value.Length == 0
        ? value
        : value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n'
            ? "'" + value
            : value;
}
