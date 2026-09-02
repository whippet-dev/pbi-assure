namespace PbiAssure.Reporting.Exports;

/// <summary>The fixed, evidence-led CSV presets available before Export Builder UI configuration.</summary>
public enum ExportPreset
{
    DataCatalogue,
    UsageMapping,
}

/// <summary>A fixed-preset export request. Null columns select that preset's documented defaults.</summary>
public sealed record ExportRequest(ExportPreset Preset, IReadOnlyList<string>? SelectedColumns = null);

/// <summary>A selectable CSV column, identified by a stable v1 contract identifier.</summary>
public sealed record ExportColumnDefinition(string Id, string Header);

/// <summary>Column catalogues and request validation shared by the fixed v1 exports and later UI.</summary>
public static class ExportPresetCatalog
{
    private static readonly ExportColumnDefinition[] DataCatalogueColumns =
    [
        new("SemanticModel", "SemanticModel"), new("Table", "Table"), new("Object", "Object"),
        new("ObjectType", "ObjectType"), new("SemanticUsage", "SemanticUsage"),
        new("ClassificationConfidence", "ClassificationConfidence"), new("UserFacing", "UserFacing"),
        new("DirectUsageCount", "DirectUsageCount"), new("ReportCount", "ReportCount"),
        new("PageCount", "PageCount"), new("VisualCount", "VisualCount"), new("UsageContexts", "UsageContexts"),
        new("ReportNames", "ReportNames"), new("PageNames", "PageNames"), new("UsageRoles", "UsageRoles"),
        new("SemanticReason", "SemanticReason"),
    ];

    private static readonly string[] DataCatalogueDefaults =
    [
        "SemanticModel", "Table", "Object", "ObjectType", "SemanticUsage", "ClassificationConfidence",
        "UserFacing", "DirectUsageCount", "ReportCount", "PageCount", "VisualCount", "UsageContexts",
    ];

    private static readonly ExportColumnDefinition[] UsageMappingColumns =
    [
        new("SemanticModel", "SemanticModel"), new("Table", "Table"), new("Object", "Object"),
        new("ObjectType", "ObjectType"), new("Report", "Report"), new("ReportPath", "ReportPath"),
        new("Page", "Page"), new("PageId", "PageId"), new("Visual", "Visual"), new("VisualId", "VisualId"),
        new("VisualType", "VisualType"), new("UsageContext", "UsageContext"), new("UsageRole", "UsageRole"),
        new("UserFacing", "UserFacing"), new("EvidenceCount", "EvidenceCount"), new("ArtifactPaths", "ArtifactPaths"),
        new("EvidencePaths", "EvidencePaths"),
        new("SemanticUsage", "SemanticUsage"), new("ClassificationConfidence", "ClassificationConfidence"),
    ];

    private static readonly string[] UsageMappingDefaults =
    [
        "SemanticModel", "Table", "Object", "ObjectType", "Report", "ReportPath", "Page", "PageId",
        "Visual", "VisualId", "VisualType", "UsageContext", "UsageRole", "UserFacing",
    ];

    public static IReadOnlyList<ExportColumnDefinition> GetAllowedColumns(ExportPreset preset) =>
        ColumnsFor(preset).ToArray();

    public static IReadOnlyList<string> GetDefaultColumnIds(ExportPreset preset) =>
        DefaultsFor(preset).ToArray();

    internal static IReadOnlyList<ExportColumnDefinition> ResolveColumns(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = ColumnsFor(request.Preset);
        var requested = request.SelectedColumns ?? DefaultsFor(request.Preset);
        if (requested.Count == 0)
        {
            throw new ArgumentException("At least one export column must be selected.", nameof(request));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var resolved = new List<ExportColumnDefinition>(requested.Count);
        foreach (var id in requested)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Export column identifiers cannot be blank.", nameof(request));
            }

            if (!seen.Add(id))
            {
                throw new ArgumentException($"Export column '{id}' was selected more than once.", nameof(request));
            }

            var column = allowed.FirstOrDefault(candidate => candidate.Id == id);
            if (column is null)
            {
                throw new ArgumentException($"Export column '{id}' is not allowed for the {request.Preset} preset.", nameof(request));
            }

            resolved.Add(column);
        }

        return resolved;
    }

    private static ExportColumnDefinition[] ColumnsFor(ExportPreset preset) => preset switch
    {
        ExportPreset.DataCatalogue => DataCatalogueColumns,
        ExportPreset.UsageMapping => UsageMappingColumns,
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported export preset."),
    };

    private static string[] DefaultsFor(ExportPreset preset) => preset switch
    {
        ExportPreset.DataCatalogue => DataCatalogueDefaults,
        ExportPreset.UsageMapping => UsageMappingDefaults,
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported export preset."),
    };
}
