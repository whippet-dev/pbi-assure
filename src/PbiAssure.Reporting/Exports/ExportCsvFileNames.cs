namespace PbiAssure.Reporting.Exports;

/// <summary>Safe, deterministic filenames for the fixed Export CSV presets.</summary>
public static class ExportCsvFileNames
{
    public static string Create(string projectDisplayName, ExportPreset preset)
    {
        var suffix = preset switch
        {
            ExportPreset.DataCatalogue => "data-catalogue",
            ExportPreset.UsageMapping => "usage-mapping",
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported export preset."),
        };

        return $"{BaseName(projectDisplayName)}.{suffix}.csv";
    }

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
