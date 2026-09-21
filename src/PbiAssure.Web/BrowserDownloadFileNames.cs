using PbiAssure.Reporting.Exports;

namespace PbiAssure.Web;

public static class BrowserDownloadFileNames
{
    /// <summary>The interactive report. The name predates the review and is kept for compatibility.</summary>
    public static string Html(string projectDisplayName) => $"{BaseName(projectDisplayName)}.pbiassure.html";

    /// <summary>The apparently unused review, distinct from the interactive report saved beside it.</summary>
    public static string ApparentlyUnusedHtml(string projectDisplayName) => $"{BaseName(projectDisplayName)}.apparently-unused.html";

    public static string SemanticUsageCsv(string projectDisplayName) => $"{BaseName(projectDisplayName)}.semantic-usage.csv";

    public static string ExportCsv(string projectDisplayName, ExportPreset preset) => ExportCsvFileNames.Create(projectDisplayName, preset);

    private static string BaseName(string value)
    {
        var normalized = string.Concat(value
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) || character is ' ' or '-' or '_' or '.'
                ? character
                : ' '));
        normalized = string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Trim(' ', '.', '-');

        return string.IsNullOrWhiteSpace(normalized) ? "pbi-assure" : normalized[..Math.Min(normalized.Length, 120)];
    }
}
