using PbiAssure.Reporting.Exports;

namespace PbiAssure.Web;

public static class BrowserDownloadFileNames
{
    public static string Html(string projectDisplayName) => $"{BaseName(projectDisplayName)}.pbiassure.html";

    public static string SemanticUsageCsv(string projectDisplayName) => $"{BaseName(projectDisplayName)}.semantic-usage.csv";

    public static string ExportCsv(string projectDisplayName, ExportPreset preset) => preset switch
    {
        ExportPreset.DataCatalogue => $"{BaseName(projectDisplayName)}.data-catalogue.csv",
        ExportPreset.UsageMapping => $"{BaseName(projectDisplayName)}.usage-mapping.csv",
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported export preset."),
    };

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
