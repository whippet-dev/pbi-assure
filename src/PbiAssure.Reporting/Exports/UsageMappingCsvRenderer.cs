using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Reporting.Exports;

/// <summary>One row per retained direct semantic usage record; no context is filtered from this export.</summary>
public static class UsageMappingCsvRenderer
{
    public static string Render(ProjectInventory inventory, ExportRequest? request = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        request ??= new ExportRequest(ExportPreset.UsageMapping);
        if (request.Preset != ExportPreset.UsageMapping)
        {
            throw new ArgumentException("The Usage mapping renderer requires the UsageMapping preset.", nameof(request));
        }

        var columns = ExportPresetCatalog.ResolveColumns(request);
        var csv = new StringBuilder();
        CsvWriter.AppendRow(csv, columns.Select(column => column.Header));
        foreach (var usage in LogicalDirectUsageAnalyzer.Analyze(inventory))
        {
            CsvWriter.AppendRow(csv, columns.Select(column => Value(column.Id, usage)));
        }

        return csv.ToString();
    }

    private static string? Value(string column, LogicalDirectUsage usage) => column switch
    {
        "SemanticModel" => usage.SemanticModel,
        "Table" => usage.Table,
        "Object" => usage.ObjectName,
        "ObjectType" => usage.ObjectType,
        "Report" => usage.Report,
        "ReportPath" => usage.ReportPath,
        "Page" => usage.Page,
        "PageId" => usage.PageId,
        "Visual" => usage.Visual,
        "VisualId" => usage.VisualId,
        "VisualType" => usage.VisualType,
        "UsageContext" => usage.UsageContext,
        "UsageRole" => usage.UsageRole,
        "UserFacing" => usage.UserFacing,
        "EvidenceCount" => usage.EvidenceCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "ArtifactPaths" => Join(usage.ArtifactPaths),
        "EvidencePaths" => Join(usage.EvidencePaths),
        "SemanticUsage" => usage.SemanticUsage,
        "ClassificationConfidence" => usage.ClassificationConfidence,
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unsupported Usage mapping column."),
    };

    private static string Join(IEnumerable<string> values) => string.Join(" | ", values);
}
